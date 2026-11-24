using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
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

public class BankInterestForm : Form
{
    private readonly string _connectionString;
    private readonly int? _transactionId;
    private readonly CashTransactionRepository _cashRepo;

    private ComboBox cmbAccount = null!;
    private TextBox txtAmount = null!, txtDate = null!, txtDescription = null!;
    private Button btnSave = null!, btnDownloadTemplate = null!, btnImportExcel = null!;
    private Button btnEdit = null!, btnDelete = null!, btnCancelEdit = null!;
    private ModernCard formCardRef = null!;

    // ویرایش داخل همین فرم (انتخاب از جدول)؛ در کنار حالت ویرایشِ ورودی سازنده (_transactionId)
    private int? _editingId;
    private int? CurrentId => _editingId ?? _transactionId;

    private DataGridView dgvInterests = null!;

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    // ✅ FIX: نگه‌داری شناسه اصلی حساب
    private int? _originalAccountId = null;

    public BankInterestForm(string connectionString, int? transactionId = null)
    {
        _connectionString = connectionString;
        _transactionId = transactionId;
        _cashRepo = new CashTransactionRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader(_transactionId.HasValue ? "ویرایش سود بانکی" : "ثبت سود بانکی", new Size(780, 840));
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
        Shown += async (s, e) =>
        {
            await LoadAccountsAsync();
            await LoadGridDataAsync();
            if (_transactionId.HasValue) await LoadTransactionDataAsync(_transactionId.Value);
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "BankInterestForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── ورودی‌ها (همان کنترل‌های قبلی؛ ModernUI آن‌ها را در قاب مدرن می‌گذارد) ──
        cmbAccount = ModernUI.Combo();
        cmbAccount.DisplayMember = "DisplayTitle";
        cmbAccount.ValueMember = "Id";

        txtAmount = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
        txtDate = new TextBox(); DateHelper.ApplyDateMask(txtDate); txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);
        txtDescription = new TextBox { PlaceholderText = "مثلاً: سود سپرده کوتاه مدت، سود اوراق و..." };

        // ── کارت فرم ──
        int rh = ModernUI.RowHeight;
        var formCard = new ModernCard("اطلاعات سود", ModernTheme.Success, 2, rh, rh, rh);
        formCardRef = formCard;
        formCard.Body.Controls.Add(ModernUI.Row("حساب بانکی", cmbAccount), 0, 0);
        formCard.Body.SetColumnSpan(formCard.Body.GetControlFromPosition(0, 0)!, 2);
        formCard.Body.Controls.Add(ModernUI.Row("مبلغ سود (ریال)", txtAmount), 0, 1);
        formCard.Body.Controls.Add(ModernUI.Row("تاریخ واریز", txtDate), 1, 1);
        formCard.Body.Controls.Add(ModernUI.Row("توضیحات (بابت)", txtDescription), 0, 2);
        formCard.Body.SetColumnSpan(formCard.Body.GetControlFromPosition(0, 2)!, 2);

        // ── دکمه‌های اکسل ──
        btnDownloadTemplate = ModernButton.Soft("دانلود قالب اکسل", ModernTheme.Neutral, 170, 38);
        btnDownloadTemplate.Click += BtnDownloadTemplate_Click;
        btnImportExcel = ModernButton.Soft("ایمپورت از اکسل", ModernTheme.Teal, 170, 38);
        btnImportExcel.Click += BtnImportExcel_Click;
        var pnlExcel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(8, 6, 8, 0),
            BackColor = Color.Transparent
        };
        pnlExcel.Controls.Add(btnImportExcel);
        pnlExcel.Controls.Add(btnDownloadTemplate);

        // ── جدول سودهای اخیر ──
        dgvInterests = ModernGrid.Create("Id", "AccountId", "Category", "CostType", "TransactionType", "Direction", "PaymentScope", "ContractId", "ContractorContractId", "ClientStatementId");
        dgvInterests.AutoGenerateColumns = false;
        dgvInterests.FillColumns();
        dgvInterests.MultiSelect = true;
        dgvInterests.CellDoubleClick += async (s, e) => { if (e.RowIndex >= 0) await StartEditSelectedAsync(); };
        dgvInterests.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Delete) { e.Handled = true; await DeleteSelectedAsync(); }
        };

        dgvInterests.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TransactionDate", HeaderText = "تاریخ واریز", FillWeight = 20 });
        dgvInterests.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AccountTitle", HeaderText = "حساب بانکی", FillWeight = 32 });
        dgvInterests.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 20 });
        dgvInterests.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 28 });
        dgvInterests.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvInterests.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        dgvInterests.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvInterests.Columns[e.ColumnIndex].DataPropertyName;
            if (colName == "TransactionDate" && e.Value is DateTime dt)
            {
                e.Value = DateHelper.ToPersianDate(dt);
                e.FormattingApplied = true;
            }
            else if (colName == "Amount" && e.Value is decimal dec)
            {
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.CellStyle.ForeColor = ModernTheme.Success;
                e.CellStyle.Font = ModernTheme.GetFont(11f, FontStyle.Bold);
                e.FormattingApplied = true;
            }
        };

        var gridCard = new ModernCard("لیست سودهای ثبت‌شده اخیر", ModernTheme.Teal, new[] { new ColumnStyle(SizeType.Percent, 100F) });
        dgvInterests.Dock = DockStyle.Fill;
        dgvInterests.Margin = Padding.Empty;
        gridCard.Body.Controls.Add(dgvInterests, 0, 0);

        // ── دکمه‌های پایین ──
        btnSave = ModernButton.Success(_transactionId.HasValue ? "ذخیره تغییرات" : "ثبت سود بانکی", 200, 44);
        btnSave.Click += async (s, e) => await SaveSafeAsync();
        btnCancelEdit = ModernButton.Cancel("انصراف از ویرایش", 150, 44);
        btnCancelEdit.Visible = false;
        btnCancelEdit.Click += (s, e) => ExitEditMode(clearInputs: true);
        btnEdit = ModernButton.Soft("ویرایش انتخاب‌شده", ModernTheme.Primary, 160, 44);
        btnEdit.Click += async (s, e) => await StartEditSelectedAsync();
        btnDelete = ModernButton.Soft("حذف انتخاب‌شده‌ها", ModernTheme.Danger, 160, 44);
        btnDelete.Click += async (s, e) => await DeleteSelectedAsync();
        var btnClose = ModernButton.Cancel("بستن", 110, 44);
        btnClose.Click += (s, e) => Close();
        var bar = ModernForm.CreateButtonBar(btnSave, btnCancelEdit, btnEdit, btnDelete, btnClose);

        // ── چیدمان صفحه ──
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12, 10, 12, 0), BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, formCard.Height + 6));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        formCard.Dock = DockStyle.Fill;
        gridCard.Dock = DockStyle.Fill;
        root.Controls.Add(formCard, 0, 0);
        root.Controls.Add(pnlExcel, 0, 1);
        root.Controls.Add(gridCard, 0, 2);
        root.Controls.Add(bar, 0, 3);
        this.Controls.Add(root);
    }

    private async Task LoadGridDataAsync()
    {
        if (IsDisposed) return;
        try
        {
            string query = @"
                    SELECT Id, TransactionDate, AccountTitle, Amount, Description 
                    FROM CashTransactions 
                    WHERE Category = N'سود' OR TransactionType = N'سود بانکی'
                    ORDER BY TransactionDate DESC, Id DESC";

            DataTable dt = null!;
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                dt = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, query));
            }, "در حال بارگذاری لیست سودها...");

            if (dt is not null && !IsDisposed)
            {
                dgvInterests.DataSource = dt;
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "BankInterestForm.LoadGridDataAsync");
        }
    }

    private async Task LoadAccountsAsync()
    {
        if (IsDisposed) return;
        try
        {
            var bankRepo = new BankAccountRepository(_connectionString);
            var accounts = await bankRepo.GetActiveAsync();
            if (IsDisposed) return;
            cmbAccount.DataSource = accounts;
            if (cmbAccount.Items.Count > 0) cmbAccount.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "BankInterestForm.LoadAccounts");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری حساب‌ها:\n" + ex.Message);
        }
    }

    // ✅ FIX: همان الگوی BankFeeForm
    private async Task LoadTransactionDataAsync(int id)
    {
        try
        {
            var trans = await _cashRepo.GetByIdAsync(id);
            if (trans is null || IsDisposed) return;

            _originalAccountId = trans.AccountId;

            if (trans.AccountId.HasValue)
            {
                cmbAccount.SelectedValue = trans.AccountId.Value;
                bool selected = cmbAccount.SelectedValue is not null &&
                                Convert.ToInt32(cmbAccount.SelectedValue) == trans.AccountId.Value;

                if (!selected)
                {
                    var bankRepo = new BankAccountRepository(_connectionString);
                    var allAccounts = await bankRepo.GetAllAsync();
                    var storedAcc = allAccounts.FirstOrDefault(a => a.Id == trans.AccountId.Value);

                    CompanyBankAccount displayEntry = storedAcc is not null
                        ? new CompanyBankAccount
                        {
                            Id = storedAcc.Id,
                            Title = $"[غیرفعال] {storedAcc.Title}",
                            BankName = storedAcc.BankName,
                            AccountNumber = storedAcc.AccountNumber,
                            IsActive = false,
                            IsCashDesk = storedAcc.IsCashDesk
                        }
                        : new CompanyBankAccount
                        {
                            Id = trans.AccountId.Value,
                            Title = $"❌ حساب حذف‌شده (#{trans.AccountId.Value})",
                            BankName = "---",
                            AccountNumber = "---",
                            IsActive = false,
                            IsCashDesk = false
                        };

                    var currentList = new List<CompanyBankAccount>();
                    if (cmbAccount.DataSource is IEnumerable<CompanyBankAccount> existing)
                        currentList.AddRange(existing);
                    currentList.Add(displayEntry);

                    cmbAccount.DataSource = null;
                    cmbAccount.DataSource = currentList;
                    cmbAccount.SelectedValue = displayEntry.Id;

                    ModernMessageBox.ShowWarning(
                        $"حساب بانکی اصلی این سود {(storedAcc != null ? "غیرفعال شده است" : "از سیستم حذف شده است")}.\n\n" +
                        "اگر بدون تغییر حساب ذخیره کنید، سود روی همان حساب اصلی باقی می‌ماند.");
                }
            }

            txtAmount.Text = NumberHelper.FormatThousands(trans.Amount, usePersianDigits: true);
            txtDate.Text = DateHelper.ToPersianDate(trans.TransactionDate);
            txtDescription.Text = trans.Description;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "BankInterestForm.LoadTransactionData");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات سود:\n" + ex.Message);
        }
    }

    private static string NormalizeAccountTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return title;
        const string inactivePrefix = "[غیرفعال] ";
        if (title.StartsWith(inactivePrefix))
            return title.Substring(inactivePrefix.Length);
        if (title.StartsWith("❌ حساب حذف‌شده"))
            return "حساب حذف‌شده";
        return title;
    }

    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (cmbAccount.SelectedValue is null) { ModernMessageBox.ShowWarning("لطفاً یک حساب بانکی را انتخاب کنید."); return; }
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            if (amount <= 0) { ModernMessageBox.ShowWarning("مبلغ سود باید بزرگتر از صفر باشد."); return; }
            DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
            if (date is null) { ModernMessageBox.ShowWarning("تاریخ وارد شده معتبر نیست."); return; }

            var selectedAcc = (CompanyBankAccount)cmbAccount.SelectedItem!;
            string desc = string.IsNullOrWhiteSpace(txtDescription.Text) ? "سود بانکی" : txtDescription.Text.Trim();
            string cleanAccountTitle = NormalizeAccountTitle(selectedAcc.Title);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var trans = new CashTransaction
                {
                    Id = CurrentId ?? 0,
                    TransactionDate = date.Value,
                    Amount = amount,
                    TransactionType = "سود بانکی",
                    Direction = DirectionTypes.Incoming,
                    PaymentScope = PaymentScopes.Administrative,
                    AccountId = selectedAcc.Id,
                    AccountTitle = cleanAccountTitle,
                    Category = "سود",
                    CostType = null,
                    Description = desc
                };

                if (CurrentId.HasValue)
                {
                    // ویرایش: فیلدهای غیرقابل‌ویرایش (مثل ارتباط با قرارداد/صورت‌حساب) از رکورد اصلی حفظ می‌شوند
                    var existing = await _cashRepo.GetByIdAsync(CurrentId.Value)
                        ?? throw new InvalidOperationException("این رکورد دیگر وجود ندارد (احتمالاً حذف شده است).");
                    existing.TransactionDate = trans.TransactionDate;
                    existing.Amount = trans.Amount;
                    existing.AccountId = trans.AccountId;
                    existing.AccountTitle = trans.AccountTitle;
                    existing.Description = trans.Description;
                    await _cashRepo.UpdateAsync(existing);
                }
                else
                {
                    await _cashRepo.AddAsync(trans);
                }
            }, "در حال ثبت سود بانکی...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show(CurrentId.HasValue ? "تغییرات با موفقیت ذخیره شد." : "سود بانکی با موفقیت ثبت شد.", isSuccess: true);

                if (_editingId.HasValue || !_transactionId.HasValue)
                {
                    // ویرایش درون‌فرمی یا ثبت جدید: در همین فرم می‌مانیم و لیست را تازه می‌کنیم
                    ExitEditMode(clearInputs: true);
                    await LoadGridDataAsync();
                }
                else
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "BankInterestForm.SaveSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ثبت:\n" + ex.Message);
        }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "BankInterestForm"); } }
    }

    // ═══════════════════════════════════════════════════════════════
    // ویرایش و حذف از داخل جدول
    // ═══════════════════════════════════════════════════════════════
    private List<int> GetSelectedIds()
    {
        var ids = new List<int>();
        foreach (DataGridViewRow gr in dgvInterests.SelectedRows)
            if (gr.DataBoundItem is DataRowView drv && drv.Row["Id"] != DBNull.Value)
                ids.Add(Convert.ToInt32(drv.Row["Id"]));
        return ids;
    }

    private async Task StartEditSelectedAsync()
    {
        var ids = GetSelectedIds();
        if (ids.Count == 0) { ModernMessageBox.ShowWarning("لطفاً یک ردیف را از جدول انتخاب کنید."); return; }
        if (ids.Count > 1) { ModernMessageBox.ShowWarning("برای ویرایش فقط یک ردیف انتخاب کنید."); return; }

        var trans = await _cashRepo.GetByIdAsync(ids[0]);
        if (trans is null)
        {
            ModernMessageBox.ShowWarning("این رکورد دیگر وجود ندارد (احتمالاً حذف شده است).");
            await LoadGridDataAsync();
            return;
        }
        if (trans.ClientStatementId.HasValue)
        {
            ModernMessageBox.ShowWarning("این تراکنش در یک صورت‌حساب ثبت شده و از اینجا قابل ویرایش نیست.");
            return;
        }

        _editingId = ids[0];
        await LoadTransactionDataAsync(ids[0]);

        btnSave.Text = "ذخیره تغییرات";
        btnCancelEdit.Visible = true;
        formCardRef.TitleLabel.Text = "ویرایش سود";
        txtAmount.Focus();
    }

    private void ExitEditMode(bool clearInputs)
    {
        _editingId = null;
        _originalAccountId = null;
        btnSave.Text = _transactionId.HasValue ? "ذخیره تغییرات" : "ثبت سود بانکی";
        btnCancelEdit.Visible = false;
        formCardRef.TitleLabel.Text = "اطلاعات سود";
        if (clearInputs && !_transactionId.HasValue)
        {
            txtAmount.Clear();
            txtDescription.Clear();
            txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        var ids = GetSelectedIds();
        if (ids.Count == 0) { ModernMessageBox.ShowWarning("لطفاً حداقل یک ردیف را از جدول انتخاب کنید."); return; }

        decimal sum = 0;
        foreach (DataGridViewRow gr in dgvInterests.SelectedRows)
            if (gr.DataBoundItem is DataRowView drv && drv.Row["Amount"] != DBNull.Value)
                sum += Convert.ToDecimal(drv.Row["Amount"]);

        string msg = ids.Count == 1
            ? $"آیا از حذف این سود بانکی مطمئن هستید؟\n\nمبلغ: {NumberHelper.FormatThousands(sum, usePersianDigits: true)} ریال"
            : $"آیا از حذف {NumberHelper.FormatThousands(ids.Count, usePersianDigits: true)} سود بانکی انتخاب‌شده مطمئن هستید؟\n\nجمع مبلغ: {NumberHelper.FormatThousands(sum, usePersianDigits: true)} ریال";
        msg += "\n\nتوجه: مبلغ از موجودی حساب بانکی کسر می‌شود.";
        if (!ModernMessageBox.AskConfirm(msg, "حذف سود بانکی")) return;

        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _cashRepo.DeleteBatchAsync(ids);
            }, "در حال حذف سود بانکی...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("سود بانکی حذف و موجودی حساب اصلاح شد.", true);
                if (_editingId.HasValue && ids.Contains(_editingId.Value)) ExitEditMode(clearInputs: true);
                await LoadGridDataAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "BankInterestForm.Delete");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در حذف:\n" + ex.Message);
        }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "BankInterestForm"); } }
    }

    private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = "BankInterest_Import_Template.xlsx", Title = "ذخیره قالب اکسل سود بانکی" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("سود بانکی");
            ws.RightToLeft = true;
            string[] headers = { "تاریخ (شمسی)", "مبلغ (ریال)", "توضیحات" };
            for (int i = 0; i < headers.Length; i++) { var cell = ws.Cell(1, i + 1); cell.Value = headers[i]; cell.Style.Font.Bold = true; cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59); cell.Style.Font.FontColor = XLColor.White; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; }
            ws.Cell(2, 1).Value = DateHelper.ToPersianDate(DateTime.Now); ws.Cell(2, 2).Value = 1000000; ws.Cell(2, 3).Value = "سود سپرده کوتاه مدت";
            ws.Columns().AdjustToContents(); workbook.SaveAs(sfd.FileName); ToastHelper.Show("قالب اکسل ساخته شد.", true);
        }
        catch (Exception ex) { ModernMessageBox.ShowError("خطا در ساخت قالب:\n" + ex.Message); }
    }

    private async void BtnImportExcel_Click(object? sender, EventArgs e)
    {
        if (cmbAccount.SelectedValue is null) { ModernMessageBox.ShowWarning("لطفاً ابتدا حساب بانکی را انتخاب کنید."); return; }
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل سودها" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;
        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            var selectedAcc = (CompanyBankAccount)cmbAccount.SelectedItem!;
            string cleanAccountTitle = NormalizeAccountTitle(selectedAcc.Title);
            int successCount = 0;
            List<string> errors = new();
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                using var workbook = new XLWorkbook(ofd.FileName);
                var ws = workbook.Worksheet(1);
                var rows = ws.RowsUsed().Skip(1);
                foreach (var row in rows)
                {
                    try
                    {
                        string dateStr = row.Cell(1).GetFormattedString().Trim(); string amountStr = row.Cell(2).GetFormattedString().Trim(); string desc = row.Cell(3).GetFormattedString().Trim();
                        if (string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(amountStr)) continue;
                        DateTime? date = DateHelper.ParsePersianDate(dateStr);
                        if (date is null) throw new Exception($"تاریخ «{dateStr}» معتبر نیست.");
                        decimal amount = NumberHelper.ParseFormatted(amountStr);
                        if (amount <= 0) throw new Exception("مبلغ نامعتبر است.");

                        var trans = new CashTransaction
                        {
                            TransactionDate = date.Value,
                            Amount = amount,
                            TransactionType = "سود بانکی",
                            Direction = DirectionTypes.Incoming,
                            PaymentScope = PaymentScopes.Administrative,
                            AccountId = selectedAcc.Id,
                            AccountTitle = cleanAccountTitle,
                            Category = "سود",
                            CostType = null,
                            Description = string.IsNullOrWhiteSpace(desc) ? "سود بانکی" : desc
                        };

                        await _cashRepo.AddAsync(trans); successCount++;
                    }
                    catch (Exception ex) { errors.Add($"سطر {row.RowNumber()}: {ex.Message}"); }
                }
            }, "در حال ایمپورت از اکسل...");
            if (success && !IsDisposed)
            {
                string msg = $"✅ تعداد {successCount} ردیف سود بانکی ثبت شد.";
                if (errors.Count > 0) msg += $"\n\n⚠️ {errors.Count} خطا:\n" + string.Join("\n", errors.Take(5));
                ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                await LoadGridDataAsync();
            }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "BankInterestForm.ImportExcel"); if (!IsDisposed) ModernMessageBox.ShowError("خطا در ایمپورت:\n" + ex.Message); }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "BankInterestForm"); } }
    }
}
