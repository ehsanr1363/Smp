using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class CashCheckEditForm : Form
    {
        private readonly CashCheckRepository _repository;
        private TextBox txtCheckNumber = null!, txtBankName = null!, txtDueDate = null!, txtAmount = null!, txtDescription = null!;
        private ComboBox cmbType = null!;
        private Button btnSave = null!;

        public CashCheckEditForm(string connectionString)
        {
            _repository = new CashCheckRepository(connectionString);
            BuildUI();
            this.ApplyStandardHeader("ثبت چک جدید", new Size(550, 450));
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(15) };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F)); formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 6; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            txtCheckNumber = new TextBox(); txtBankName = new TextBox();
            txtDueDate = new TextBox(); DateHelper.ApplyDateMask(txtDueDate); txtDueDate.Text = DateHelper.ToPersianDate(DateTime.Now);
            txtAmount = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
            cmbType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; cmbType.Items.AddRange(new[] { "دریافتی", "پرداختی" }); cmbType.SelectedIndex = 0;
            txtDescription = new TextBox { Multiline = true, Height = 60 };

            UIHelper.AddField(formLayout, "نوع چک:", cmbType, 0); UIHelper.AddField(formLayout, "شماره چک:", txtCheckNumber, 1);
            UIHelper.AddField(formLayout, "بانک:", txtBankName, 2); UIHelper.AddField(formLayout, "تاریخ سررسید:", txtDueDate, 3);
            UIHelper.AddField(formLayout, "مبلغ (ریال):", txtAmount, 4); UIHelper.AddField(formLayout, "توضیحات:", txtDescription, 5);

            btnSave = UIHelper.CreateButton("💾 ثبت چک", UIHelper.ActionGreen, 200, 45);
            var btnPanel = new Panel { Dock = DockStyle.Fill };
            btnSave.Anchor = AnchorStyles.None; btnPanel.Controls.Add(btnSave);
            btnPanel.Resize += (s, e) => { btnSave.Left = (btnPanel.Width - btnSave.Width) / 2; btnSave.Top = (btnPanel.Height - btnSave.Height) / 2; };
            btnSave.Click += BtnSave_Click;

            mainLayout.Controls.Add(formLayout, 0, 0); mainLayout.Controls.Add(btnPanel, 0, 1); Controls.Add(mainLayout);
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCheckNumber.Text)) { ModernMessageBox.ShowWarning("شماره چک الزامی است."); return; }
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            if (amount <= 0) { ModernMessageBox.ShowWarning("مبلغ باید بزرگتر از صفر باشد."); return; }
            DateTime? date = DateHelper.ParsePersianDate(txtDueDate.Text);
            if (date == null) { ModernMessageBox.ShowWarning("تاریخ سررسید معتبر نیست."); return; }

            var check = new CashCheck
            {
                CheckNumber = txtCheckNumber.Text.Trim(),
                BankName = txtBankName.Text.Trim(),
                DueDate = date.Value,
                Amount = amount,
                CheckType = cmbType.Text,
                Status = "در جریان",
                Description = txtDescription.Text.Trim()
            };

            bool success = await AsyncHelper.RunWithLoading(this, async () => { await _repository.AddAsync(check); }, "در حال ثبت چک...");
            if (success) { ToastHelper.Show("چک با موفقیت ثبت شد.", true); DialogResult = DialogResult.OK; Close(); }
        }
    }
}