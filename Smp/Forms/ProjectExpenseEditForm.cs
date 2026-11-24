using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using Smp.Models;
using Smp.Helpers;
using Smp.Data;

namespace Smp.Forms;

public class ProjectExpenseEditForm : Form
{
    private readonly string _connectionString;
    private readonly ClientContract _clientContract;
    private readonly ProjectExpense? _existingExpense;
    private readonly bool _isEdit;
    private readonly LookupRepository _lookupRepo;

    private TextBox txtPartyName = null!;
    private TextBox txtInvoiceNumber = null!, txtInvoiceDate = null!, txtAmount = null!, txtDescription = null!;
    private ComboBox cmbCostType = null!;
    private ModernButton btnSave = null!;
    private int? _selectedPartyId = null;

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public ProjectExpenseEditForm(string connectionString, ClientContract clientContract, ProjectExpense? expense = null)
    {
        _connectionString = connectionString;
        _clientContract = clientContract;
        _existingExpense = expense;
        _isEdit = expense is not null;
        _lookupRepo = new LookupRepository(connectionString);

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        Shown += async (s, e) =>
        {
            await LoadCostTypesAsync();

            if (_isEdit && _existingExpense is not null)
            {
                await LoadDataAsync();
            }
            else
            {
                txtInvoiceDate.Text = DateHelper.ToPersianDate(DateTime.Now);
            }
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ProjectExpenseEditForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        int fh = ModernUI.FieldHeight;

        ModernTextBox Box(bool center = false)
        {
            var b = ModernUI.Input();
            if (center) b.TextAlign = HorizontalAlignment.Center;
            return b;
        }

        // ───── کارت ۱: طرف حساب ─────
        var partyBox = Box(true);
        partyBox.ReadOnly = true;
        partyBox.Font = ModernTheme.GetFont(11f, FontStyle.Bold);
        txtPartyName = partyBox.TextBox;
        partyBox.Dock = DockStyle.Fill;
        partyBox.Margin = new Padding(0, 0, 6, 0);

        var btnSelectParty = ModernButton.Soft("🔍 انتخاب", ModernTheme.Teal, 90, ModernUI.InputHeight);
        btnSelectParty.Anchor = AnchorStyles.None;
        btnSelectParty.Click += BtnSelectParty_Click;

        var pnlParty = new BufferedTable { Margin = Padding.Empty, BackColor = Color.Transparent, ColumnCount = 2, RowCount = 1, RightToLeft = RightToLeft.Yes };
        pnlParty.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pnlParty.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        pnlParty.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        pnlParty.Controls.Add(partyBox, 0, 0);
        pnlParty.Controls.Add(btnSelectParty, 1, 0);

        var cardParty = new ModernCard("طرف حساب", ModernTheme.Primary, 1, fh);
        cardParty.Body.Controls.Add(ModernUI.Field("طرف حساب (تفصیلی)", pnlParty), 0, 0);

        // ───── کارت ۲: مشخصات فاکتور ─────
        var numBox = Box(); txtInvoiceNumber = numBox.TextBox;
        var dateBox = Box(true); txtInvoiceDate = dateBox.TextBox; DateHelper.ApplyDateMask(txtInvoiceDate);
        cmbCostType = ModernUI.Combo();
        var amountBox = Box(true); txtAmount = amountBox.TextBox; SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
        var descBox = ModernUI.Memo(90); txtDescription = descBox.TextBox;

        var cardInvoice = new ModernCard("مشخصات فاکتور", ModernTheme.Teal, 2, fh, fh, 100);
        cardInvoice.Body.Controls.Add(ModernUI.Field("شماره فاکتور", numBox), 0, 0);
        cardInvoice.Body.Controls.Add(ModernUI.Field("تاریخ فاکتور", dateBox), 1, 0);
        cardInvoice.Body.Controls.Add(ModernUI.Field("نوع هزینه (ماهیت)", cmbCostType), 0, 1);
        cardInvoice.Body.Controls.Add(ModernUI.Field("مبلغ فاکتور (ریال)", amountBox), 1, 1);
        var fldDesc = ModernUI.Row("توضیحات (بابت چه چیزی)", descBox, 150);
        cardInvoice.Body.Controls.Add(fldDesc, 0, 2);
        cardInvoice.Body.SetColumnSpan(fldDesc, 2);

        // ───── دکمه‌ها ─────
        btnSave = ModernButton.Success("💾 ذخیره فاکتور", 200, 40);
        btnSave.Click += async (s, e) => await SaveSafeAsync();
        var btnCancel = ModernButton.Cancel();
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var bar = ModernForm.CreateButtonBar(btnSave, btnCancel);
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var page = new ModernPage();
        page.AddCard(cardParty, 8);
        page.AddCard(cardInvoice, 8);

        string title = _isEdit ? "ویرایش فاکتور هزینه" : $"ثبت فاکتور هزینه - قرارداد {_clientContract.ContractNumber}";
        SuspendLayout();
        ModernForm.Setup(this, title, new Size(900, 660), isDialog: true);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(ModernForm.CreateHeader(title, "ثبت هزینه‌ی پروژه به‌همراه طرف حساب"), 0, 0);
        root.Controls.Add(page, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
        ResumeLayout();
    }

    private async Task LoadCostTypesAsync()
    {
        try
        {
            var items = await _lookupRepo.GetByCategoryAsync("CostType");
            if (IsDisposed) return;

            var costTypes = items.Select(i => i.Value).ToList();
            if (costTypes.Count == 0) costTypes.AddRange(new[] { "خرید", "خدمات" });

            cmbCostType.DataSource = costTypes;

            if (_isEdit && _existingExpense is not null && !string.IsNullOrEmpty(_existingExpense.CostType))
            {
                if (cmbCostType.Items.Contains(_existingExpense.CostType))
                    cmbCostType.SelectedItem = _existingExpense.CostType;
            }
            else if (cmbCostType.Items.Count > 0)
            {
                cmbCostType.SelectedIndex = 0;
            }
        }
        catch
        {
            if (IsDisposed) return;
            cmbCostType.Items.Clear();
            cmbCostType.Items.AddRange(new[] { "خرید", "خدمات" });
            cmbCostType.SelectedIndex = 0;
        }
    }

    private void BtnSelectParty_Click(object? sender, EventArgs e)
    {
        using var form = new TafsiliSelectionForm(_connectionString);
        if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed)
        {
            _selectedPartyId = form.SelectedId;
            txtPartyName.Text = form.SelectedName;
        }
    }

    private async Task LoadDataAsync()
    {
        if (_existingExpense is null) return;

        _selectedPartyId = _existingExpense.PartyId;
        txtPartyName.Text = _existingExpense.PartyName ?? "";
        txtInvoiceNumber.Text = _existingExpense.InvoiceNumber;
        txtInvoiceDate.Text = DateHelper.ToPersianDate(_existingExpense.InvoiceDate);
        txtAmount.Text = NumberHelper.FormatThousands(_existingExpense.Amount, usePersianDigits: true);
        txtDescription.Text = _existingExpense.Description;

        // بررسی وجود طرف قرارداد — اگر حذف شده، به کاربر اطلاع بده
        if (_selectedPartyId.HasValue)
        {
            try
            {
                int exists = await Task.Run(() => Convert.ToInt32(
                    DbHelper.ExecuteScalar(_connectionString,
                        "SELECT COUNT(1) FROM Parties WHERE Id = @id",
                        new SqlParameter("@id", _selectedPartyId.Value))));

                if (exists == 0)
                {
                    _selectedPartyId = null;
                    txtPartyName.Text = "";
                    ModernMessageBox.ShowWarning(
                        "طرف قرارداد این فاکتور از سیستم حذف شده است.\n\n" +
                        "فیلد طرف قرارداد خالی شده. اگر می‌خواهید فاکتور را حفظ کنید:\n" +
                        "  • یک طرف قرارداد جدید انتخاب کنید، یا\n" +
                        "  • فیلد را خالی بگذارید و ذخیره کنید (فاکتور بدون طرف قرارداد ذخیره می‌شود).");
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ProjectExpenseEditForm.LoadDataAsync - PartyCheck");
            }
        }
    }

    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            if (amount <= 0)
            {
                ModernMessageBox.ShowWarning("لطفاً مبلغ فاکتور را به درستی وارد کنید.");
                txtAmount.Focus();
                return;
            }

            if (!DateHelper.IsValidPersianDate(txtInvoiceDate.Text))
            {
                ModernMessageBox.ShowWarning("تاریخ فاکتور معتبر نیست.");
                txtInvoiceDate.Focus();
                return;
            }

            // قبل از ذخیره، وجود طرف قرارداد بررسی می‌شود
            if (_selectedPartyId.HasValue)
            {
                int exists;
                try
                {
                    exists = await Task.Run(() => Convert.ToInt32(
                        DbHelper.ExecuteScalar(_connectionString,
                            "SELECT COUNT(1) FROM Parties WHERE Id = @id",
                            new SqlParameter("@id", _selectedPartyId.Value))));
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "ProjectExpenseEditForm.SaveSafeAsync - PartyCheck");
                    ModernMessageBox.ShowError("خطا در بررسی طرف قرارداد:\n" + ex.Message);
                    return;
                }

                if (exists == 0)
                {
                    ModernMessageBox.ShowWarning(
                        "طرف قرارداد انتخاب‌شده از سیستم حذف شده است.\n\n" +
                        "لطفاً از طریق دکمه «🔍 انتخاب» یک طرف قرارداد جدید انتخاب کنید،\n" +
                        "یا فیلد را خالی بگذارید تا فاکتور بدون طرف قرارداد ذخیره شود.");
                    return;
                }
            }

            DateTime? invDate = DateHelper.ParsePersianDate(txtInvoiceDate.Text);

            var expense = new ProjectExpense
            {
                Id = _isEdit && _existingExpense is not null ? _existingExpense.Id : 0,
                ClientContractId = _clientContract.Id,
                PartyId = _selectedPartyId,
                InvoiceNumber = txtInvoiceNumber.Text.Trim(),
                InvoiceDate = invDate.Value,
                Amount = amount,
                CostType = cmbCostType.Text,
                Description = txtDescription.Text.Trim()
            };

            if (_isEdit) DateStampHelper.StampUpdated(expense);
            else DateStampHelper.StampCreated(expense);

            string? fkErrorMessage = null;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                try
                {
                    var repo = new ProjectExpenseRepository(_connectionString);
                    if (_isEdit)
                        await Task.Run(() => repo.Update(expense));
                    else
                        await Task.Run(() => repo.Add(expense));
                }
                catch (SqlException ex) when (ex.Number == 547)
                {
                    fkErrorMessage =
                        "این فاکتور به رکوردی اشاره می‌کند که در سیستم وجود ندارد (احتمالاً طرف قرارداد یا قرارداد پروژه حذف شده است).\n\n" +
                        "لطفاً فیلدهای مرتبط را بازبینی کرده و دوباره تلاش کنید.";
                    throw new InvalidOperationException(fkErrorMessage, ex);
                }
            }, "در حال ذخیره فاکتور...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("فاکتور با موفقیت ذخیره شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ProjectExpenseEditForm.SaveSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ProjectExpenseEditForm"); }
        }
    }
}
