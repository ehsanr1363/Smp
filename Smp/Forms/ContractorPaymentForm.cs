using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Models;
using Smp.Helpers;
using Smp.Data;

namespace Smp.Forms;

public class ContractorPaymentForm : Form
{
    private readonly string _connectionString;
    private readonly ContractorContract _contract;

    private TextBox txtAmount = null!, txtDescription = null!, txtPaymentDate = null!;
    private ComboBox cmbPaymentType = null!;
    private ModernButton btnSave = null!;

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public ContractorPaymentForm(string connectionString, ContractorContract contract)
    {
        _connectionString = connectionString;
        _contract = contract;

        BuildUI();

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
        ShowInTaskbar = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorPaymentForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        int fh = ModernUI.FieldHeight;

        // ───── ساخت فیلدها ─────
        var dateBox = ModernUI.Input();
        dateBox.TextAlign = HorizontalAlignment.Center;
        txtPaymentDate = dateBox.TextBox;
        DateHelper.ApplyDateMask(txtPaymentDate);
        txtPaymentDate.Text = DateHelper.ToPersianDate(DateTime.Now);

        var amountBox = ModernUI.Input();
        amountBox.TextAlign = HorizontalAlignment.Center;
        txtAmount = amountBox.TextBox;
        SmartInputHelper.EnableSmartAmountTextBox(txtAmount);

        cmbPaymentType = ModernUI.Combo();
        cmbPaymentType.Items.AddRange(new[] { "نقدی", "چک", "حواله بانکی", "سایر" });
        cmbPaymentType.SelectedIndex = 0;

        var descBox = ModernUI.Memo(90);
        txtDescription = descBox.TextBox;

        // ───── کارت: اطلاعات پرداخت ─────
        var card = new ModernCard("اطلاعات پرداخت", ModernTheme.Primary, 2, fh, fh, 100);
        card.Body.Controls.Add(ModernUI.Field("تاریخ پرداخت", dateBox), 0, 0);
        card.Body.Controls.Add(ModernUI.Field("مبلغ پرداختی (ریال)", amountBox), 1, 0);
        card.Body.Controls.Add(ModernUI.Field("نوع پرداخت", cmbPaymentType), 0, 1);
        var fldDesc = ModernUI.Row("توضیحات (اختیاری)", descBox);
        card.Body.Controls.Add(fldDesc, 0, 2);
        card.Body.SetColumnSpan(fldDesc, 2);

        // ───── دکمه‌ها ─────
        btnSave = ModernButton.Success("💾 ثبت پرداخت", 200, 40);
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
        page.AddCard(card, 8);

        string title = $"ثبت پرداخت - قرارداد {_contract.ContractNumber}";
        SuspendLayout();
        ModernForm.Setup(this, title, new Size(650, 520), isDialog: true);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(ModernForm.CreateHeader(title, "ثبت پرداخت به پیمانکار"), 0, 0);
        root.Controls.Add(page, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
        ResumeLayout();
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
                ModernMessageBox.ShowWarning("لطفاً مبلغ پرداختی معتبری وارد کنید.");
                txtAmount.Focus();
                return;
            }

            DateTime? pDate = DateHelper.ParsePersianDate(txtPaymentDate.Text);
            if (pDate is null)
            {
                ModernMessageBox.ShowWarning("تاریخ پرداخت معتبر نیست.");
                txtPaymentDate.Focus();
                return;
            }

            var payment = new ContractorPayment
            {
                ContractId = _contract.Id,
                PaymentDate = pDate.Value,
                Amount = amount,
                PaymentType = cmbPaymentType.Text,
                Description = txtDescription.Text.Trim()
            };

            DateStampHelper.StampCreated(payment);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorPaymentRepository(_connectionString);
                await Task.Run(() => repo.Add(payment));
            }, "در حال ثبت پرداخت...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("پرداخت با موفقیت ثبت شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractorPaymentForm.SaveSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ثبت پرداخت:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorPaymentForm"); }
        }
    }
}
