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
    public class ContractorGuaranteeEditForm : Form
    {
        private readonly string _connectionString;
        private readonly ContractorContract _contract;
        private readonly ContractorGuarantee? _existingGuarantee;
        private readonly bool _isEdit;

        private TextBox txtGuaranteeNumber = null!, txtBankName = null!, txtAmount = null!;
        private TextBox txtIssueDate = null!, txtMaturityDate = null!, txtDescription = null!;
        private ComboBox cmbGuaranteeType = null!, cmbStatus = null!;
        private Button btnSave = null!;

        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public ContractorGuaranteeEditForm(string connectionString, ContractorContract contract, ContractorGuarantee? guarantee = null)
        {
            _connectionString = connectionString;
            _contract = contract;
            _existingGuarantee = guarantee;
            _isEdit = guarantee != null;

            BuildUI();

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            ShowInTaskbar = false;

            if (_isEdit && _existingGuarantee != null)
                LoadData();
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
                _isEdit ? "ویرایش ضمانت‌نامه" : $"ثبت ضمانت‌نامه - قرارداد {_contract.ContractNumber}",
                new Size(800, 550));

            // ─── ساخت فیلدها ───
            txtGuaranteeNumber = new TextBox();
            txtBankName = new TextBox();

            txtAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount);

            txtIssueDate = new TextBox();
            DateHelper.ApplyDateMask(txtIssueDate);

            txtMaturityDate = new TextBox();
            DateHelper.ApplyDateMask(txtMaturityDate);

            cmbGuaranteeType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbGuaranteeType.Items.AddRange(new[] { "حسن انجام کار", "پیش‌پرداخت", "سایر" });
            cmbGuaranteeType.SelectedIndex = 0;

            cmbStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbStatus.Items.AddRange(new[] { "فعال", "آزاد شده", "ضبط شده" });
            cmbStatus.SelectedIndex = 0;

            txtDescription = new TextBox { Multiline = true, Dock = DockStyle.Fill };

            // ─── ردیف ۰: شماره + بانک ───
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "شماره ضمانت‌نامه:", txtGuaranteeNumber, 0, 0, 1);
            UIHelper.AddField(formLayout, "بانک صادرکننده:", txtBankName, 0, 2, 3);

            // ─── ردیف ۱: مبلغ + نوع ───
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "مبلغ ضمانت (ریال):", txtAmount, 1, 0, 1);
            UIHelper.AddField(formLayout, "نوع ضمانت‌نامه:", cmbGuaranteeType, 1, 2, 3);

            // ─── ردیف ۲: تاریخ صدور + سررسید ───
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "تاریخ صدور:", txtIssueDate, 2, 0, 1);
            UIHelper.AddField(formLayout, "تاریخ سررسید:", txtMaturityDate, 2, 2, 3);

            // ─── ردیف ۳: وضعیت ───
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "وضعیت:", cmbStatus, 3, 0, 1);

            // ─── ردیف ۴: توضیحات (تمام عرض) ───
            StandardFormHelper.AddRow(formLayout, 100F);
            UIHelper.AddField(formLayout, "توضیحات:", txtDescription, 4, 0, 1, 3);

            // ─── دکمه ذخیره ───
            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.ActionGreen, 200, 45);
            btnSave.Font = UIHelper.PersianFont(12f, FontStyle.Bold);
            btnSave.Click += async (s, e) => await SaveSafeAsync();
            btnPanel.Controls.Add(btnSave);

            // مقدار پیش‌فرض تاریخ‌ها برای ثبت جدید
            if (!_isEdit)
            {
                txtIssueDate.Text = DateHelper.ToPersianDate(DateTime.Now);
                txtMaturityDate.Text = DateHelper.ToPersianDate(DateTime.Now.AddYears(1));
            }
        }

        private void LoadData()
        {
            if (_existingGuarantee == null) return;

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
                    Id = _isEdit && _existingGuarantee != null ? _existingGuarantee.Id : 0,
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
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}