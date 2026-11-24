using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

/// <summary>
/// ثبت دریافت متفرقه: هر مبلغی که به حساب بانکی شرکت واریز می‌شود و از کارفرما نیست
/// (سرمایه، وام، برگشت وجه، فروش دارایی و ...). نوع دریافت از لیست «نوع دریافت متفرقه» در مدیریت لیست‌های پایه می‌آید.
/// </summary>
public class OtherReceiptForm : Form
{
    public const string ReceiptCategory = "دریافت متفرقه";
    public const string LookupKey = "OtherReceiptType";
    private static readonly string[] DefaultTypes = { "سرمایه / واریز شریک", "وام و تسهیلات", "برگشت وجه", "فروش دارایی", "سایر" };

    private readonly string _connectionString;
    private readonly CashTransactionRepository _transRepo;
    private readonly BankAccountRepository _bankRepo;
    private readonly LookupRepository _lookupRepo;

    private ComboBox cmbAccount = null!, cmbType = null!;
    private TextBox txtAmount = null!, txtDate = null!, txtPayer = null!, txtDescription = null!;
    private Button btnSave = null!, btnEdit = null!, btnDelete = null!, btnCancelEdit = null!;
    private DataGridView dgv = null!;
    private ModernCard cardInput = null!;
    private ModernStatCard statCount = null!, statTotal = null!;

    private CashTransaction? _editingEntity;
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public OtherReceiptForm(string connectionString)
    {
        _connectionString = connectionString;
        _transRepo = new CashTransactionRepository(connectionString);
        _bankRepo = new BankAccountRepository(connectionString);
        _lookupRepo = new LookupRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("ثبت دریافت متفرقه (غیر از کارفرما)", new Size(900, 820));
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
        Shown += async (s, e) => await LoadDataAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception ex) { LogHelper.LogException(ex, "OtherReceiptForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        cmbAccount = ModernUI.Combo();
        cmbAccount.DisplayMember = "DisplayTitle";
        cmbAccount.ValueMember = "Id";
        cmbType = ModernUI.Combo();
        cmbType.DisplayMember = "Value";
        cmbType.ValueMember = "Id";

        txtAmount = new TextBox();
        SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
        txtDate = new TextBox { TextAlign = HorizontalAlignment.Center };
        DateHelper.ApplyDateMask(txtDate);
        txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);
        txtPayer = new TextBox { PlaceholderText = "نام شخص/شرکت واریزکننده" };
        txtDescription = new TextBox { PlaceholderText = "توضیحات (بابت)" };

        const int rh = 50;
        cardInput = new ModernCard("اطلاعات دریافت", ModernTheme.Success, 2, rh, rh, rh, rh);
        cardInput.Body.Controls.Add(ModernUI.Row("حساب بانکی", cmbAccount), 0, 0);
        cardInput.Body.Controls.Add(ModernUI.Row("نوع دریافت", cmbType), 1, 0);
        cardInput.Body.Controls.Add(ModernUI.Row("مبلغ (ریال)", txtAmount), 0, 1);
        cardInput.Body.Controls.Add(ModernUI.Row("تاریخ", txtDate), 1, 1);
        var rowPayer = ModernUI.Row("واریزکننده", txtPayer);
        cardInput.Body.Controls.Add(rowPayer, 0, 2);
        cardInput.Body.SetColumnSpan(rowPayer, 2);
        var rowDesc = ModernUI.Row("توضیحات", txtDescription);
        cardInput.Body.Controls.Add(rowDesc, 0, 3);
        cardInput.Body.SetColumnSpan(rowDesc, 2);

        var strip = ModernReport.CreateStatStrip(2, 76);
        statCount = strip.Add("تعداد دریافتی‌های ثبت‌شده", ModernTheme.Primary);
        statTotal = strip.Add("جمع دریافتی‌ها", ModernTheme.Success);
        statCount.SetCount(0, "دریافتی");
        statTotal.SetAmount(0, "ریال");
        strip.Dock = DockStyle.Fill;

        var lblGrid = ModernUI.Caption("دریافتی‌های متفرقه ثبت‌شده (دابل‌کلیک = ویرایش، Delete = حذف)", 11f, true, ModernTheme.TextStrong);
        lblGrid.Dock = DockStyle.Fill;

        dgv = ModernGrid.Create();
        dgv.AutoGenerateColumns = false;
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgv.MultiSelect = true;
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "TransactionDate", DataPropertyName = "TransactionDate", HeaderText = "تاریخ", FillWeight = 14 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "AccountTitle", DataPropertyName = "AccountTitle", HeaderText = "حساب بانکی", FillWeight = 22 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReceiptType", DataPropertyName = "ReceiptType", HeaderText = "نوع", FillWeight = 16 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Payer", DataPropertyName = "Payer", HeaderText = "واریزکننده", FillWeight = 18 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 18 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 26 });
        dgv.UseAiryLook(44);
        dgv.UseSelectionAccent();
        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string col = dgv.Columns[e.ColumnIndex].DataPropertyName;
            if (col == "TransactionDate" && e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
            else if (col == "Amount" && e.Value is decimal dec) { e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true); e.FormattingApplied = true; }
        };
        dgv.CellDoubleClick += async (s, e) => { if (e.RowIndex >= 0) await StartEditSelectedAsync(); };
        dgv.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Delete) { e.Handled = true; await DeleteSelectedAsync(); } };

        btnSave = ModernButton.Save("ثبت دریافت", 200, 40);
        btnSave.Click += async (s, e) => await SaveAsync();
        btnCancelEdit = ModernButton.Cancel("انصراف از ویرایش", 150, 40);
        btnCancelEdit.Visible = false;
        btnCancelEdit.Click += (s, e) => ExitEditMode();
        btnEdit = ModernButton.Soft("ویرایش انتخاب‌شده", ModernTheme.Primary, 160, 40);
        btnEdit.Click += async (s, e) => await StartEditSelectedAsync();
        btnDelete = ModernButton.Soft("حذف انتخاب‌شده‌ها", ModernTheme.Danger, 160, 40);
        btnDelete.Click += async (s, e) => await DeleteSelectedAsync();
        var bar = ModernForm.CreateButtonBar(btnSave, btnCancelEdit, btnEdit, btnDelete);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            Padding = new Padding(14, 12, 14, 6), BackColor = ModernTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, cardInput.Height + 10));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        cardInput.Dock = DockStyle.Fill;
        root.Controls.Add(strip, 0, 0);
        root.Controls.Add(cardInput, 0, 1);
        root.Controls.Add(lblGrid, 0, 2);
        root.Controls.Add(ModernReport.WrapGrid(dgv), 0, 3);
        root.Controls.Add(bar, 0, 4);
        Controls.Add(root);
    }

    // ── نوع‌های دریافت از لوک‌آپ؛ اگر هنوز لیستی نیست، مقادیر پیش‌فرض یک‌بار در لوک‌آپ ساخته می‌شود ──
    private async Task<List<LookupItem>> LoadTypesAsync()
    {
        var items = await _lookupRepo.GetByCategoryAsync(LookupKey);
        if (items.Count == 0)
        {
            foreach (var v in DefaultTypes)
                _lookupRepo.Add(new LookupItem { Category = LookupKey, Value = v });
            items = await _lookupRepo.GetByCategoryAsync(LookupKey);
        }
        return items;
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var banks = (await _bankRepo.GetActiveAsync()).Where(a => !a.IsCashDesk).ToList();
            var types = await LoadTypesAsync();
            if (IsDisposed) return;
            cmbAccount.DataSource = banks;
            cmbType.DataSource = types;

            var dt = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, @"
                    SELECT TOP 300 Id, TransactionDate, ISNULL(AccountTitle, N'---') AS AccountTitle,
                           ISNULL(CostType, N'---') AS ReceiptType, ISNULL(RecipientName, N'---') AS Payer,
                           Amount, ISNULL(Description, N'---') AS Description
                    FROM CashTransactions
                    WHERE Category = N'" + ReceiptCategory + @"' AND Direction = N'دریافتی'
                    ORDER BY TransactionDate DESC, Id DESC"));
            if (IsDisposed) return;
            dgv.DataSource = dt;

            decimal total = 0;
            foreach (DataRow r in dt.Rows) if (r["Amount"] != DBNull.Value) total += Convert.ToDecimal(r["Amount"]);
            statCount.SetCount(dt.Rows.Count, "دریافتی");
            statTotal.SetAmount(total, "ریال");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "OtherReceiptForm.LoadData");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات:\n" + ex.Message);
        }
    }

    private static string CleanAccountTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return title;
        const string inactivePrefix = "[غیرفعال] ";
        if (title.StartsWith(inactivePrefix)) return title.Substring(inactivePrefix.Length);
        if (title.StartsWith("❌ حساب حذف‌شده")) return "حساب حذف‌شده";
        return title;
    }

    private async Task SaveAsync()
    {
        if (IsDisposed) return;
        if (cmbAccount.SelectedItem is not CompanyBankAccount acc) { ModernMessageBox.ShowWarning("لطفاً حساب بانکی را انتخاب کنید."); return; }
        if (cmbType.SelectedItem is not LookupItem type) { ModernMessageBox.ShowWarning("لطفاً نوع دریافت را انتخاب کنید."); return; }
        decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
        if (amount <= 0) { ModernMessageBox.ShowWarning("مبلغ نامعتبر است."); return; }
        DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
        if (date is null) { ModernMessageBox.ShowWarning("تاریخ نامعتبر است."); return; }

        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            string payer = txtPayer.Text.Trim();
            string desc = string.IsNullOrWhiteSpace(txtDescription.Text) ? type.Value : txtDescription.Text.Trim();
            bool editing = _editingEntity is not null;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_editingEntity is not null)
                {
                    var e = _editingEntity;   // فقط فیلدهای قابل ویرایش عوض می‌شود
                    e.AccountId = acc.Id;
                    e.AccountTitle = CleanAccountTitle(acc.Title);
                    e.TransactionDate = date.Value;
                    e.Amount = amount;
                    e.CostType = type.Value;
                    e.RecipientName = string.IsNullOrEmpty(payer) ? null : payer;
                    e.Description = desc;
                    await _transRepo.UpdateAsync(e);
                }
                else
                {
                    await _transRepo.AddAsync(new CashTransaction
                    {
                        AccountId = acc.Id,
                        AccountTitle = CleanAccountTitle(acc.Title),
                        TransactionDate = date.Value,
                        Amount = amount,
                        Direction = DirectionTypes.Incoming,
                        TransactionType = ReceiptCategory,
                        PaymentScope = PaymentScopes.Administrative,
                        Category = ReceiptCategory,
                        CostType = type.Value,
                        RecipientName = string.IsNullOrEmpty(payer) ? null : payer,
                        Description = desc
                    });
                }
            }, editing ? "در حال ذخیره تغییرات..." : "در حال ثبت دریافت...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show(editing ? "تغییرات ذخیره شد." : "دریافت با موفقیت ثبت شد.", true);
                ExitEditMode();
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "OtherReceiptForm.Save");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ثبت:\n" + ex.Message);
        }
        finally { try { _actionLock.Release(); } catch (Exception ex) { LogHelper.LogException(ex, "OtherReceiptForm"); } }
    }

    private List<int> GetSelectedIds()
    {
        var ids = new List<int>();
        foreach (DataGridViewRow gr in dgv.SelectedRows)
            if (gr.DataBoundItem is DataRowView drv && drv.Row["Id"] != DBNull.Value) ids.Add(Convert.ToInt32(drv.Row["Id"]));
        return ids;
    }

    private async Task StartEditSelectedAsync()
    {
        var ids = GetSelectedIds();
        if (ids.Count == 0) { ModernMessageBox.ShowWarning("لطفاً یک ردیف را از جدول انتخاب کنید."); return; }
        if (ids.Count > 1) { ModernMessageBox.ShowWarning("برای ویرایش فقط یک ردیف انتخاب کنید."); return; }
        try
        {
            var t = await _transRepo.GetByIdAsync(ids[0]);
            if (t is null) { ModernMessageBox.ShowWarning("این رکورد دیگر وجود ندارد."); await LoadDataAsync(); return; }

            if (t.AccountId.HasValue) await EnsureAccountInComboAsync(t.AccountId.Value);
            _editingEntity = t;
            if (t.AccountId.HasValue) cmbAccount.SelectedValue = t.AccountId.Value;

            // نوعی که بعداً از لیست پایه حذف شده هم باید قابل نمایش بماند
            var types = (cmbType.DataSource as IEnumerable<LookupItem>)?.ToList() ?? new List<LookupItem>();
            var cur = types.FirstOrDefault(x => x.Value == t.CostType);
            if (cur is null && !string.IsNullOrWhiteSpace(t.CostType))
            {
                cur = new LookupItem { Id = -1, Category = LookupKey, Value = t.CostType! };
                types.Add(cur);
                cmbType.DataSource = null;
                cmbType.DataSource = types;
            }
            if (cur is not null) cmbType.SelectedItem = cur;

            txtAmount.Text = NumberHelper.FormatThousands(t.Amount, usePersianDigits: true);
            txtDate.Text = DateHelper.ToPersianDate(t.TransactionDate);
            txtPayer.Text = t.RecipientName ?? "";
            txtDescription.Text = t.Description ?? "";

            btnSave.Text = "ذخیره تغییرات";
            btnCancelEdit.Visible = true;
            cardInput.TitleLabel.Text = "ویرایش دریافت";
            txtAmount.Focus();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "OtherReceiptForm.StartEdit");
            ModernMessageBox.ShowError("خطا در بارگذاری برای ویرایش:\n" + ex.Message);
        }
    }

    private async Task EnsureAccountInComboAsync(int accountId)
    {
        var current = (cmbAccount.DataSource as IEnumerable<CompanyBankAccount>)?.ToList() ?? new List<CompanyBankAccount>();
        if (current.Any(a => a.Id == accountId)) return;
        var stored = (await _bankRepo.GetAllAsync()).FirstOrDefault(a => a.Id == accountId);
        current.Add(stored is not null
            ? new CompanyBankAccount { Id = stored.Id, Title = $"[غیرفعال] {stored.Title}", BankName = stored.BankName, AccountNumber = stored.AccountNumber, IsActive = false, IsCashDesk = stored.IsCashDesk }
            : new CompanyBankAccount { Id = accountId, Title = $"❌ حساب حذف‌شده (#{accountId})", BankName = "---", AccountNumber = "---", IsActive = false, IsCashDesk = false });
        cmbAccount.DataSource = null;
        cmbAccount.DataSource = current;
    }

    private void ExitEditMode()
    {
        _editingEntity = null;
        btnSave.Text = "ثبت دریافت";
        btnCancelEdit.Visible = false;
        cardInput.TitleLabel.Text = "اطلاعات دریافت";
        txtAmount.Clear();
        txtPayer.Clear();
        txtDescription.Clear();
        txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);
    }

    private async Task DeleteSelectedAsync()
    {
        var ids = GetSelectedIds();
        if (ids.Count == 0) { ModernMessageBox.ShowWarning("لطفاً حداقل یک ردیف را از جدول انتخاب کنید."); return; }

        decimal sum = 0;
        foreach (DataGridViewRow gr in dgv.SelectedRows)
            if (gr.DataBoundItem is DataRowView drv && drv.Row["Amount"] != DBNull.Value) sum += Convert.ToDecimal(drv.Row["Amount"]);

        string msg = ids.Count == 1
            ? $"آیا از حذف این دریافت مطمئن هستید؟\n\nمبلغ: {NumberHelper.FormatThousands(sum, usePersianDigits: true)} ریال"
            : $"آیا از حذف {NumberHelper.FormatThousands(ids.Count, usePersianDigits: true)} دریافت انتخاب‌شده مطمئن هستید؟\n\nجمع مبلغ: {NumberHelper.FormatThousands(sum, usePersianDigits: true)} ریال";
        msg += "\n\nتوجه: مبلغ از موجودی حساب بانکی کسر می‌شود.";
        if (!ModernMessageBox.AskConfirm(msg, "حذف دریافت")) return;

        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () => { await _transRepo.DeleteBatchAsync(ids); }, "در حال حذف...");
            if (success && !IsDisposed)
            {
                ToastHelper.Show("دریافت حذف و موجودی حساب اصلاح شد.", true);
                if (_editingEntity is not null && ids.Contains(_editingEntity.Id)) ExitEditMode();
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "OtherReceiptForm.Delete");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در حذف:\n" + ex.Message);
        }
        finally { try { _actionLock.Release(); } catch (Exception ex) { LogHelper.LogException(ex, "OtherReceiptForm"); } }
    }
}
