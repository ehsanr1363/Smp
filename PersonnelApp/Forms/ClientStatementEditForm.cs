using System;
using System.Drawing;
using System.Windows.Forms;
using PersonnelApp.Models;
using PersonnelApp.Helpers;
using PersonnelApp.Data;

namespace PersonnelApp.Forms
{
    public class ClientStatementEditForm : Form
    {
        private readonly string _connectionString;
        private readonly ClientContract _contract;
        private readonly ClientStatement? _existingStatement;
        private readonly bool _isEdit;

        private TextBox txtStatementNumber = null!, txtStatementDate = null!, txtGrossAmount = null!;
        private TextBox txtVat = null!, txtDeductions = null!, txtNet = null!;
        private Button btnSave = null!;

        public ClientStatementEditForm(string connectionString, ClientContract contract, ClientStatement? statement = null)
        {
            _connectionString = connectionString;
            _contract = contract;
            _existingStatement = statement;
            _isEdit = statement != null;

            BuildUI();

            // فعال‌سازی میانبرهای Ctrl+S (ذخیره) و Esc (بستن)
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            if (_isEdit && _existingStatement != null)
                LoadData();
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, _isEdit ? "ویرایش صورت وضعیت کارفرما" : $"ثبت صورت وضعیت - قرارداد {_contract.ContractNumber}", new Size(550, 500));
            ShowInTaskbar = false;

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, AutoSize = true, Padding = new Padding(15) };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            for (int i = 0; i < 7; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            // --- فیلدها ---
            txtStatementNumber = new TextBox();

            txtStatementDate = new TextBox();
            DateHelper.ApplyDateMask(txtStatementDate); // 👈 ماسک تاریخ

            txtGrossAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtGrossAmount); // 👈 فرمت سه‌رقمی

            txtVat = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtVat);

            txtDeductions = new TextBox { Text = "0" };
            SmartInputHelper.EnableSmartAmountTextBox(txtDeductions);

            txtNet = new TextBox
            {
                ReadOnly = true,
                BackColor = Color.FromArgb(220, 252, 231), // پس‌زمینه سبز کمرنگ
                Font = UIHelper.PersianFont(12f, FontStyle.Bold)
            };

            UIHelper.AddField(formLayout, "شماره صورت وضعیت:", txtStatementNumber, 0);
            UIHelper.AddField(formLayout, "تاریخ صورت وضعیت:", txtStatementDate, 1);
            UIHelper.AddField(formLayout, "مبلغ کارکرد (ناخالص):", txtGrossAmount, 2);
            UIHelper.AddField(formLayout, "ارزش افزوده (ریال):", txtVat, 3);
            UIHelper.AddField(formLayout, "کسورات (ریال):", txtDeductions, 4);

            formLayout.Controls.Add(new Label { Text = "", Height = 10 }, 0, 5);
            UIHelper.AddField(formLayout, "مبلغ خالص دریافتی:", txtNet, 6);

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

            // مقدار پیش‌فرض تاریخ
            if (!_isEdit) txtStatementDate.Text = DateHelper.ToPersianDate(DateTime.Now);

            // رویداد محاسبه خودکار مبلغ خالص
            txtGrossAmount.TextChanged += (s, e) => CalculateNet();
            txtVat.TextChanged += (s, e) => CalculateNet();
            txtDeductions.TextChanged += (s, e) => CalculateNet();
        }

        private void CalculateNet()
        {
            // 👈 استفاده از ParseFormatted برای پشتیبانی از ارقام فارسی
            decimal gross = NumberHelper.ParseFormatted(txtGrossAmount.Text);
            decimal vat = NumberHelper.ParseFormatted(txtVat.Text);
            decimal ded = NumberHelper.ParseFormatted(txtDeductions.Text);

            decimal net = (gross + vat) - ded;

            // 👈 نمایش با ارقام فارسی و سه‌رقمی
            txtNet.Text = NumberHelper.FormatThousands(net, usePersianDigits: true);
        }

        private void LoadData()
        {
            if (_existingStatement == null) return;

            txtStatementNumber.Text = _existingStatement.StatementNumber;
            txtStatementDate.Text = DateHelper.ToPersianDate(_existingStatement.StatementDate);

            // 👈 فرمت‌دهی مقادیر در زمان ویرایش
            txtGrossAmount.Text = NumberHelper.FormatThousands(_existingStatement.GrossAmount, usePersianDigits: true);
            txtVat.Text = NumberHelper.FormatThousands(_existingStatement.VAT, usePersianDigits: true);
            txtDeductions.Text = NumberHelper.FormatThousands(_existingStatement.Deductions, usePersianDigits: true);

            CalculateNet();
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            // ۱. اعتبارسنجی شماره صورت وضعیت
            if (string.IsNullOrWhiteSpace(txtStatementNumber.Text))
            {
                ModernMessageBox.ShowWarning("لطفاً شماره صورت وضعیت را وارد کنید.");
                txtStatementNumber.Focus();
                return;
            }

            // ۲. اعتبارسنجی تاریخ
            if (!DateHelper.IsValidPersianDate(txtStatementDate.Text))
            {
                ModernMessageBox.ShowWarning("تاریخ وارد شده معتبر نیست.");
                txtStatementDate.Focus();
                return;
            }

            // ۳. پارس مبالغ
            decimal gross = NumberHelper.ParseFormatted(txtGrossAmount.Text);
            decimal vat = NumberHelper.ParseFormatted(txtVat.Text);
            decimal ded = NumberHelper.ParseFormatted(txtDeductions.Text);
            decimal net = (gross + vat) - ded;

            if (gross <= 0)
            {
                ModernMessageBox.ShowWarning("مبلغ کارکرد باید بزرگتر از صفر باشد.");
                txtGrossAmount.Focus();
                return;
            }

            // ۴. ساخت مدل
            var statement = new ClientStatement
            {
                Id = _isEdit && _existingStatement != null ? _existingStatement.Id : 0,
                ContractId = _contract.Id,
                StatementNumber = txtStatementNumber.Text.Trim(),
                StatementDate = DateHelper.ParsePersianDate(txtStatementDate.Text) ?? DateTime.Now,
                GrossAmount = gross,
                VAT = vat,
                Deductions = ded,
                NetReceivableAmount = net
            };

            // مهر زمانی سیستم
            if (_isEdit) DateStampHelper.StampUpdated(statement);
            else DateStampHelper.StampCreated(statement);

            // ۵. ذخیره در دیتابیس به همراه نمایش Overlay
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ClientStatementRepository(_connectionString);
                if (_isEdit)
                    await Task.Run(() => repo.Update(statement));
                else
                    await Task.Run(() => repo.Add(statement));
            }, "در حال ذخیره صورت وضعیت...");

            // ۶. بستن فرم در صورت موفقیت
            if (success)
            {
                ToastHelper.Show("صورت وضعیت با موفقیت ذخیره شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}