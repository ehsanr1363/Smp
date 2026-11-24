using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Models;
using Smp.Helpers;
using Smp.Data;

namespace Smp.Forms;

public class ContractorGuaranteeEditForm : Form
{
    private readonly string _connectionString;
    private readonly ContractorContract _contract;
    private readonly ContractorGuarantee? _existingGuarantee;
    private readonly bool _isEdit;

    private TextBox txtGuaranteeNumber = null!, txtBankName = null!, txtAmount = null!;
    private TextBox txtIssueDate = null!, txtMaturityDate = null!, txtDescription = null!;
    private ComboBox cmbGuaranteeType = null!, cmbStatus = null!;
    private ModernButton btnSave = null!;

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public ContractorGuaranteeEditForm(string connectionString, ContractorContract contract, ContractorGuarantee? guarantee = null)
    {
        _connectionString = connectionString;
        _contract = contract;
        _existingGuarantee = guarantee;
        _isEdit = guarantee is not null;

        BuildUI();

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
        ShowInTaskbar = false;

        if (_isEdit && _existingGuarantee is not null)
            LoadData();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorGuaranteeEditForm"); } }
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

        // ───── ساخت فیلدها ─────
        var numBox = Box(); txtGuaranteeNumber = numBox.TextBox;
        var bankBox = Box(); txtBankName = bankBox.TextBox;

        var amountBox = Box(true); txtAmount = amountBox.TextBox;
        SmartInputHelper.EnableSmartAmountTextBox(txtAmount);

        var issueBox = Box(true); txtIssueDate = issueBox.TextBox;
        DateHelper.ApplyDateMask(txtIssueDate);

        var maturityBox = Box(true); txtMaturityDate = maturityBox.TextBox;
        DateHelper.ApplyDateMask(txtMaturityDate);

        cmbGuaranteeType = ModernUI.Combo();
        cmbGuaranteeType.Items.AddRange(new[] { "حسن انجام کار", "پیش‌پرداخت", "سایر" });
        cmbGuaranteeType.SelectedIndex = 0;

        cmbStatus = ModernUI.Combo();
        cmbStatus.Items.AddRange(new[] { "فعال", "آزاد شده", "ضبط شده" });
        cmbStatus.SelectedIndex = 0;

        var descBox = ModernUI.Memo(90);
        txtDescription = descBox.TextBox;

        // ───── کارت ۱: مشخصات ضمانت‌نامه ─────
        var cardInfo = new ModernCard("مشخصات ضمانت‌نامه", ModernTheme.Primary, 2, fh, fh, fh);
        cardInfo.Body.Controls.Add(ModernUI.Field("شماره ضمانت‌نامه", numBox), 0, 0);
        cardInfo.Body.Controls.Add(ModernUI.Field("بانک صادرکننده", bankBox), 1, 0);
        cardInfo.Body.Controls.Add(ModernUI.Field("مبلغ ضمانت (ریال)", amountBox), 0, 1);
        cardInfo.Body.Controls.Add(ModernUI.Field("نوع ضمانت‌نامه", cmbGuaranteeType), 1, 1);
        cardInfo.Body.Controls.Add(ModernUI.Field("تاریخ صدور", issueBox), 0, 2);
        cardInfo.Body.Controls.Add(ModernUI.Field("تاریخ سررسید", maturityBox), 1, 2);

        // ───── کارت ۲: وضعیت و توضیحات ─────
        var cardStatus = new ModernCard("وضعیت و توضیحات", ModernTheme.Teal, 2, fh, 100);
        cardStatus.Body.Controls.Add(ModernUI.Field("وضعیت", cmbStatus), 0, 0);
        var fldDesc = ModernUI.Row("توضیحات", descBox);
        cardStatus.Body.Controls.Add(fldDesc, 0, 1);
        cardStatus.Body.SetColumnSpan(fldDesc, 2);

        // ───── دکمه‌ها ─────
        btnSave = ModernButton.Success("💾 ذخیره", 200, 40);
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
        page.AddCard(cardInfo, 8);
        page.AddCard(cardStatus, 8);

        string title = _isEdit ? "ویرایش ضمانت‌نامه" : $"ثبت ضمانت‌نامه - قرارداد {_contract.ContractNumber}";
        SuspendLayout();
        ModernForm.Setup(this, title, new Size(800, 640), isDialog: true);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(ModernForm.CreateHeader(title, "ثبت و ویرایش ضمانت‌نامه پیمانکار"), 0, 0);
        root.Controls.Add(page, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
        ResumeLayout();

        // مقدار پیش‌فرض تاریخ‌ها برای ثبت جدید
        if (!_isEdit)
        {
            txtIssueDate.Text = DateHelper.ToPersianDate(DateTime.Now);
            txtMaturityDate.Text = DateHelper.ToPersianDate(DateTime.Now.AddYears(1));
        }
    }

    private void LoadData()
    {
        if (_existingGuarantee is null) return;

        txtGuaranteeNumber.Text = _existingGuarantee.GuaranteeNumber;
        txtBankName.Text = _existingGuarantee.BankName;
        txtAmount.Text = NumberHelper.FormatThousands(_existingGuarantee.Amount, usePersianDigits: true);
        txtIssueDate.Text = DateHelper.ToPersianDate(_existingGuarantee.IssueDate);
        txtMaturityDate.Text = DateHelper.ToPersianDate(_existingGuarantee.MaturityDate);
        cmbGuaranteeType.Text = _existingGuarantee.GuaranteeType;
        cmbStatus.Text = _existingGuarantee.Status;
        txtDescription.Text = _existingGuarantee.Description;
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
                ModernMessageBox.ShowWarning("لطفاً مبلغ ضمانت‌نامه را به درستی وارد کنید.");
                txtAmount.Focus();
                return;
            }

            if (!DateHelper.IsValidPersianDate(txtIssueDate.Text) || !DateHelper.IsValidPersianDate(txtMaturityDate.Text))
            {
                ModernMessageBox.ShowWarning("تاریخ صدور یا سررسید معتبر نیست.");
                return;
            }

            DateTime? issue = DateHelper.ParsePersianDate(txtIssueDate.Text);
            DateTime? maturity = DateHelper.ParsePersianDate(txtMaturityDate.Text);

            var guarantee = new ContractorGuarantee
            {
                Id = _isEdit && _existingGuarantee is not null ? _existingGuarantee.Id : 0,
                ContractId = _contract.Id,
                GuaranteeNumber = txtGuaranteeNumber.Text.Trim(),
                BankName = txtBankName.Text.Trim(),
                Amount = amount,
                IssueDate = issue.Value,
                MaturityDate = maturity.Value,
                GuaranteeType = cmbGuaranteeType.Text,
                Status = cmbStatus.Text,
                Description = txtDescription.Text.Trim()
            };

            if (_isEdit) DateStampHelper.StampUpdated(guarantee);
            else DateStampHelper.StampCreated(guarantee);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorGuaranteeRepository(_connectionString);
                if (_isEdit)
                    await Task.Run(() => repo.Update(guarantee));
                else
                    await Task.Run(() => repo.Add(guarantee));
            }, "در حال ذخیره اطلاعات...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("اطلاعات با موفقیت ذخیره شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractorGuaranteeEditForm.SaveSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorGuaranteeEditForm"); }
        }
    }
}
