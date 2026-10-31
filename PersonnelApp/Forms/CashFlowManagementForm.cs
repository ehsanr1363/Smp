using PersonnelApp.Helpers;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class CashFlowManagementForm : Form
    {
        private readonly string _connectionString;

        public CashFlowManagementForm(string connectionString)
        {
            _connectionString = connectionString;
            BuildUI();
            this.ApplyStandardHeader("مدیریت تراکنش‌ها و چک‌ها", new Size(600, 400));
            ShortcutHelper.ApplyStandardShortcuts(this);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(30) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

            var lblTitle = new Label { Text = "لطفاً یکی از عملیات زیر را انتخاب کنید:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(14f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            var btnTx = UIHelper.CreateButton("📥 ثبت دریافت / پرداخت نقدی", UIHelper.ActionGreen, 300, 50);
            btnTx.Dock = DockStyle.Fill;
            btnTx.Click += (s, e) => { using var f = new CashTransactionEditForm(_connectionString); f.ShowDialog(this); };

            var btnCheck = UIHelper.CreateButton("📄 ثبت چک جدید", UIHelper.SectionOrange, 300, 50);
            btnCheck.Dock = DockStyle.Fill;
            btnCheck.Click += (s, e) => { using var f = new CashCheckEditForm(_connectionString); f.ShowDialog(this); };

            var btnAccounts = UIHelper.CreateButton("🏦 مدیریت حساب‌های بانکی و صندوق", UIHelper.SectionBlue, 300, 50);
            btnAccounts.Dock = DockStyle.Fill;
            btnAccounts.Click += (s, e) => { using var f = new BankAccountManagementForm(_connectionString); f.ShowDialog(this); };

            mainLayout.Controls.Add(lblTitle, 0, 0);
            mainLayout.Controls.Add(btnTx, 0, 1);
            mainLayout.Controls.Add(btnCheck, 0, 2);
            mainLayout.Controls.Add(btnAccounts, 0, 3);

            Controls.Add(mainLayout);
        }
    }
}