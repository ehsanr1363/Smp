using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Models;
using PersonnelApp.Helpers;
using PersonnelApp.Data;

namespace PersonnelApp.Forms
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

        public ContractorGuaranteeEditForm(string connectionString, ContractorContract contract, ContractorGuarantee? guarantee = null)
        {
            _connectionString = connectionString;
            _contract = contract;
            _existingGuarantee = guarantee;
            _isEdit = guarantee != null;

            BuildUI();

            // فعال‌سازی میانبرهای Ctrl+S (ذخیره) و Esc (بستن)
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            ShowInTaskbar = false;

            if (_isEdit && _existingGuarantee != null)
                LoadData();
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, _isEdit ? "ویرایش ضمانت‌نامه" : $"ثبت ضمانت‌نامه - قرارداد {_contract.ContractNumber}", new Size(600, 600));

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8, AutoSize = true, Padding = new Padding(15) };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            for (int i = 0; i < 8; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            // --- فیلدها ---
            txtGuaranteeNumber = new TextBox();
            txtBankName = new TextBox();

            txtAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount); // 👈 فرمت سه‌رقمی مبلغ

            txtIssueDate = new TextBox();
            DateHelper.ApplyDateMask(txtIssueDate); // 👈 ماسک تاریخ

            txtMaturityDate = new TextBox();
            DateHelper.ApplyDateMask(txtMaturityDate); // 👈 ماسک تاریخ

            txtDescription = new TextBox { Multiline = true, Height = 60 };

            cmbGuaranteeType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbGuaranteeType.Items.AddRange(new[] { "حسن انجام کار", "پیش‌پرداخت", "سایر" });
            cmbGuaranteeType.SelectedIndex = 0;

            cmbStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbStatus.Items.AddRange(new[] { "فعال", "آزاد شده", "ضبط شده" });
            cmbStatus.SelectedIndex = 0;

            UIHelper.AddField(formLayout, "شماره ضمانت‌نامه:", txtGuaranteeNumber, 0);
            UIHelper.AddField(formLayout, "بانک صادرکننده:", txtBankName, 1);
            UIHelper.AddField(formLayout, "مبلغ ضمانت (ریال):", txtAmount, 2);
            UIHelper.AddField(formLayout, "تاریخ صدور:", txtIssueDate, 3);
            UIHelper.AddField(formLayout, "تاریخ سررسید:", txtMaturityDate, 4);
            UIHelper.AddField(formLayout, "نوع ضمانت‌نامه:", cmbGuaranteeType, 5);
            UIHelper.AddField(formLayout, "وضعیت:", cmbStatus, 6);
            UIHelper.AddField(formLayout, "توضیحات:", txtDescription, 7);

            // --- دکمه ذخیره ---
            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.WarmPrimary, 250, 50);
            btnSave.Font = UIHelper.PersianFont(14f, FontStyle.Bold);

            var btnPanel = new Panel { Dock = DockStyle.Fill };
            btnSave.Anchor = AnchorStyles.None;
            btnPanel.Controls.Add(btnSave);

            btnPanel.Resize += (s, e) => {
                btnSave.Left = (btnPanel.Width - btnSave.Width) / 2;
                btnSave.Top = (btnPanel.Height - btnSave.Height) / 2;
            };
            btnSave.Click += BtnSave_Click;

            mainLayout.Controls.Add(formLayout, 0, 0);
            mainLayout.Controls.Add(btnPanel, 0, 1);

            Controls.Add(mainLayout);

            // اگر حالت ثبت جدید بود، تاریخ‌ها را پیش‌فرض پر کن
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

            // 👈 استفاده از NumberHelper برای نمایش عدد
            txtAmount.Text = NumberHelper.FormatThousands(_existingGuarantee.Amount, usePersianDigits: true);

            txtIssueDate.Text = DateHelper.ToPersianDate(_existingGuarantee.IssueDate);
            txtMaturityDate.Text = DateHelper.ToPersianDate(_existingGuarantee.MaturityDate);

            cmbGuaranteeType.Text = _existingGuarantee.GuaranteeType;
            cmbStatus.Text = _existingGuarantee.Status;
            txtDescription.Text = _existingGuarantee.Description;
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            // ۱. پارس مبلغ (با پشتیبانی از ارقام فارسی)
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            if (amount <= 0)
            {
                ModernMessageBox.ShowWarning("لطفاً مبلغ ضمانت‌نامه را به درستی وارد کنید.");
                txtAmount.Focus();
                return;
            }

            // ۲. اعتبارسنجی تاریخ‌ها
            if (!DateHelper.IsValidPersianDate(txtIssueDate.Text) || !DateHelper.IsValidPersianDate(txtMaturityDate.Text))
            {
                ModernMessageBox.ShowWarning("تاریخ صدور یا سررسید معتبر نیست.");
                return;
            }

            DateTime? issue = DateHelper.ParsePersianDate(txtIssueDate.Text);
            DateTime? maturity = DateHelper.ParsePersianDate(txtMaturityDate.Text);

            // ۳. ساخت مدل
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

            // مهر زمانی سیستم
            if (_isEdit) DateStampHelper.StampUpdated(guarantee);
            else DateStampHelper.StampCreated(guarantee);

            // ۴. ذخیره در دیتابیس به همراه نمایش Overlay
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorGuaranteeRepository(_connectionString);
                if (_isEdit)
                    await Task.Run(() => repo.Update(guarantee));
                else
                    await Task.Run(() => repo.Add(guarantee));
            }, "در حال ذخیره اطلاعات...");

            // ۵. بستن فرم در صورت موفقیت
            if (success)
            {
                ToastHelper.Show("اطلاعات با موفقیت ذخیره شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}