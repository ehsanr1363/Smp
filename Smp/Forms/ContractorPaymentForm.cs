using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Models;
using SMP.Helpers;
using SMP.Data;

namespace SMP.Forms
{
    public class ContractorPaymentForm : Form
    {
        private readonly string _connectionString;
        private readonly ContractorContract _contract;

        private TextBox txtAmount = null!, txtDescription = null!, txtPaymentDate = null!;
        private ComboBox cmbPaymentType = null!;
        private Button btnSave = null!;

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
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            // ✅ استفاده از StandardFormHelper
            var (formLayout, btnPanel) = StandardFormHelper.SetupStandardLayout(
                this,
                $"ثبت پرداخت - قرارداد {_contract.ContractNumber}",
                new Size(650, 420));

            // ─── ساخت فیلدها ───
            txtPaymentDate = new TextBox();
            DateHelper.ApplyDateMask(txtPaymentDate);
            txtPaymentDate.Text = DateHelper.ToPersianDate(DateTime.Now);

            txtAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount);

            cmbPaymentType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPaymentType.Items.AddRange(new[] { "نقدی", "چک", "حواله بانکی", "سایر" });
            cmbPaymentType.SelectedIndex = 0;

            txtDescription = new TextBox { Multiline = true, Dock = DockStyle.Fill };

            // ─── ردیف ۰: تاریخ + مبلغ ───
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "تاریخ پرداخت:", txtPaymentDate, 0, 0, 1);
            UIHelper.AddField(formLayout, "مبلغ پرداختی (ریال):", txtAmount, 0, 2, 3);

            // ─── ردیف ۱: نوع پرداخت ───
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "نوع پرداخت:", cmbPaymentType, 1, 0, 1);

            // ─── ردیف ۲: توضیحات (تمام عرض) ───
            StandardFormHelper.AddRow(formLayout, 120F);
            UIHelper.AddField(formLayout, "توضیحات (اختیاری):", txtDescription, 2, 0, 1, 3);

            // ─── دکمه ذخیره ───
            btnSave = UIHelper.CreateButton("💾 ثبت پرداخت", UIHelper.ActionGreen, 200, 45);
            btnSave.Font = UIHelper.PersianFont(12f, FontStyle.Bold);
            btnSave.Click += async (s, e) => await SaveSafeAsync();
            btnPanel.Controls.Add(btnSave);
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
                if (pDate == null)
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
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}