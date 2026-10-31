using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class CashTransactionEditForm : Form
    {
        private readonly CashTransactionRepository _repository;
        private readonly BankAccountRepository _accountRepo;
        private ComboBox cmbAccount = null!, cmbType = null!, cmbCategory = null!;
        private TextBox txtDate = null!, txtAmount = null!, txtDescription = null!, txtReference = null!;
        private Button btnSave = null!;

        public CashTransactionEditForm(string connectionString)
        {
            _repository = new CashTransactionRepository(connectionString);
            _accountRepo = new BankAccountRepository(connectionString);
            BuildUI();
            this.ApplyStandardHeader("ثبت تراکنش نقدی جدید", new Size(550, 480));
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            Shown += async (s, e) => await LoadAccountsAsync();
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(15) };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 7; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 6 ? 60F : 50F));

            cmbAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Title", ValueMember = "Id" };
            cmbType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; cmbType.Items.AddRange(new[] { "دریافت", "پرداخت" }); cmbType.SelectedIndex = 0;
            cmbCategory = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown }; cmbCategory.Items.AddRange(new[] { "فروش", "حقوق و دستمزد", "هزینه‌های جاری", "پیمانکاران", "دریافت از کارفرما", "سایر" }); cmbCategory.SelectedIndex = 0;
            txtDate = new TextBox(); DateHelper.ApplyDateMask(txtDate); txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);
            txtAmount = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
            txtDescription = new TextBox { Multiline = true, Height = 60 };
            txtReference = new TextBox();

            UIHelper.AddField(formLayout, "حساب / صندوق:", cmbAccount, 0); UIHelper.AddField(formLayout, "نوع تراکنش:", cmbType, 1);
            UIHelper.AddField(formLayout, "تاریخ:", txtDate, 2); UIHelper.AddField(formLayout, "مبلغ (ریال):", txtAmount, 3);
            UIHelper.AddField(formLayout, "دسته‌بندی:", cmbCategory, 4); UIHelper.AddField(formLayout, "شماره پیگیری:", txtReference, 5);
            UIHelper.AddField(formLayout, "توضیحات:", txtDescription, 6);

            btnSave = UIHelper.CreateButton("💾 ثبت تراکنش", UIHelper.ActionGreen, 200, 45);
            var btnPanel = new Panel { Dock = DockStyle.Fill };
            btnSave.Anchor = AnchorStyles.None; btnPanel.Controls.Add(btnSave);
            btnPanel.Resize += (s, e) => { btnSave.Left = (btnPanel.Width - btnSave.Width) / 2; btnSave.Top = (btnPanel.Height - btnSave.Height) / 2; };
            btnSave.Click += BtnSave_Click;

            mainLayout.Controls.Add(formLayout, 0, 0); mainLayout.Controls.Add(btnPanel, 0, 1); Controls.Add(mainLayout);
        }

        private async Task LoadAccountsAsync()
        {
            try { cmbAccount.DataSource = await _accountRepo.GetAllAsync(); }
            catch (Exception ex) { ModernMessageBox.ShowError("خطا در بارگذاری حساب‌ها:\n" + ex.Message); }
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (cmbAccount.SelectedValue == null) { ModernMessageBox.ShowWarning("لطفاً یک حساب انتخاب کنید."); return; }
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            if (amount <= 0) { ModernMessageBox.ShowWarning("مبلغ باید بزرگتر از صفر باشد."); return; }
            DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
            if (date == null) { ModernMessageBox.ShowWarning("تاریخ معتبر نیست."); return; }

            var tx = new CashTransaction
            {
                AccountId = (int)cmbAccount.SelectedValue,
                TransactionDate = date.Value,
                Amount = amount,
                TransactionType = cmbType.Text,
                Category = cmbCategory.Text,
                Description = txtDescription.Text.Trim(),
                ReferenceNumber = txtReference.Text.Trim()
            };

            bool success = await AsyncHelper.RunWithLoading(this, async () => { await _repository.AddAsync(tx); }, "در حال ثبت تراکنش...");
            if (success) { ToastHelper.Show("تراکنش با موفقیت ثبت شد.", true); DialogResult = DialogResult.OK; Close(); }
        }
    }
}