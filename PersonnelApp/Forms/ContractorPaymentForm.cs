using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Models;
using PersonnelApp.Helpers;
using PersonnelApp.Data;

namespace PersonnelApp.Forms
{
    public class ContractorPaymentForm : Form
    {
        private readonly string _connectionString;
        private readonly ContractorContract _contract;

        private TextBox txtAmount = null!, txtDescription = null!, txtPaymentDate = null!;
        private ComboBox cmbPaymentType = null!;
        private Button btnSave = null!;

        public ContractorPaymentForm(string connectionString, ContractorContract contract)
        {
            _connectionString = connectionString;
            _contract = contract;

            BuildUI();

            // فعال‌سازی میانبرهای Ctrl+S (ذخیره) و Esc (بستن)
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            ShowInTaskbar = false;
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, $"ثبت پرداخت - قرارداد {_contract.ContractNumber}", new Size(500, 450));

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, AutoSize = true };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            for (int i = 0; i < 5; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));

            // --- فیلدها ---
            txtPaymentDate = new TextBox();
            DateHelper.ApplyDateMask(txtPaymentDate); // 👈 اعمال ماسک تاریخ

            txtAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount); // 👈 فرمت سه‌رقمی خودکار

            cmbPaymentType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPaymentType.Items.AddRange(new[] { "نقدی", "چک", "حواله بانکی", "سایر" });
            cmbPaymentType.SelectedIndex = 0;

            txtDescription = new TextBox { Multiline = true, Height = 60 };

            UIHelper.AddField(formLayout, "تاریخ پرداخت:", txtPaymentDate, 0);
            UIHelper.AddField(formLayout, "مبلغ پرداختی (ریال):", txtAmount, 1);
            UIHelper.AddField(formLayout, "نوع پرداخت:", cmbPaymentType, 2);
            UIHelper.AddField(formLayout, "توضیحات (اختیاری):", txtDescription, 3);

            // --- دکمه ذخیره ---
            btnSave = UIHelper.CreateButton("💾 ثبت پرداخت", UIHelper.WarmPrimary, 200, 45);

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
            txtPaymentDate.Text = DateHelper.ToPersianDate(DateTime.Now);
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            // ۱. اعتبارسنجی مبلغ (با پشتیبانی از ارقام فارسی)
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            if (amount <= 0)
            {
                ModernMessageBox.ShowWarning("لطفاً مبلغ پرداختی معتبری وارد کنید.");
                txtAmount.Focus();
                return;
            }

            // ۲. اعتبارسنجی تاریخ
            DateTime? pDate = DateHelper.ParsePersianDate(txtPaymentDate.Text);
            if (pDate == null)
            {
                ModernMessageBox.ShowWarning("تاریخ پرداخت معتبر نیست.");
                txtPaymentDate.Focus();
                return;
            }

            // ۳. ساخت مدل
            var payment = new ContractorPayment
            {
                ContractId = _contract.Id,
                PaymentDate = pDate.Value,
                Amount = amount,
                PaymentType = cmbPaymentType.Text,
                Description = txtDescription.Text.Trim()
            };

            // مهر زمانی سیستم
            DateStampHelper.StampCreated(payment);

            // ۴. ذخیره در دیتابیس به همراه نمایش Overlay
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorPaymentRepository(_connectionString);
                await Task.Run(() => repo.Add(payment));
            }, "در حال ثبت پرداخت...");

            // ۵. بستن فرم در صورت موفقیت
            if (success)
            {
                ToastHelper.Show("پرداخت با موفقیت ثبت شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}