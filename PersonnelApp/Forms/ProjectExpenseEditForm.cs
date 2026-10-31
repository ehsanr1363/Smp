using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Models;
using PersonnelApp.Helpers;
using PersonnelApp.Data;

namespace PersonnelApp.Forms
{
    public class ProjectExpenseEditForm : Form
    {
        private readonly string _connectionString;
        private readonly ClientContract _clientContract;
        private readonly ProjectExpense? _existingExpense;
        private readonly bool _isEdit;

        private TextBox txtInvoiceNumber = null!, txtInvoiceDate = null!, txtAmount = null!, txtDescription = null!;
        private TextBox txtPartyName = null!;
        private Button btnSave = null!;
        private int? _selectedPartyId = null;

        public ProjectExpenseEditForm(string connectionString, ClientContract clientContract, ProjectExpense? expense = null)
        {
            _connectionString = connectionString;
            _clientContract = clientContract;
            _existingExpense = expense;
            _isEdit = expense != null;

            BuildUI();

            // فعال‌سازی میانبرهای Ctrl+S (ذخیره) و Esc (بستن)
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            ShowInTaskbar = false;

            if (_isEdit && _existingExpense != null)
                LoadData();
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, _isEdit ? "ویرایش فاکتور هزینه" : $"ثبت فاکتور هزینه - قرارداد {_clientContract.ContractNumber}", new Size(600, 550));

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, AutoSize = true, Padding = new Padding(15) };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            for (int i = 0; i < 5; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));

            // فیلد انتخاب طرف حساب (فروشنده)
            txtPartyName = new TextBox { ReadOnly = true, BackColor = UIHelper.WarmInputBackground, TextAlign = HorizontalAlignment.Center };
            var btnSelectParty = UIHelper.CreateButton("🔍 انتخاب", UIHelper.WarmSecondary, 80, 35);
            btnSelectParty.Click += BtnSelectParty_Click;

            var partyPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            partyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            partyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            partyPanel.Controls.Add(txtPartyName, 0, 0);
            partyPanel.Controls.Add(btnSelectParty, 1, 0);

            formLayout.Controls.Add(new Label { Text = "طرف حساب (فروشنده):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold) }, 0, 0);
            formLayout.Controls.Add(partyPanel, 1, 0);

            txtInvoiceNumber = new TextBox();

            txtInvoiceDate = new TextBox();
            DateHelper.ApplyDateMask(txtInvoiceDate); // ماسک تاریخ

            txtAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount); // فرمت سه‌رقمی خودکار

            txtDescription = new TextBox { Multiline = true, Height = 60 };

            UIHelper.AddField(formLayout, "شماره فاکتور:", txtInvoiceNumber, 1);
            UIHelper.AddField(formLayout, "تاریخ فاکتور:", txtInvoiceDate, 2);
            UIHelper.AddField(formLayout, "مبلغ فاکتور (ریال):", txtAmount, 3);
            UIHelper.AddField(formLayout, "توضیحات (بابت چه چیزی):", txtDescription, 4);

            // دکمه ذخیره
            btnSave = UIHelper.CreateButton("💾 ذخیره فاکتور", UIHelper.WarmPrimary, 250, 50);
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

            if (!_isEdit) txtInvoiceDate.Text = DateHelper.ToPersianDate(DateTime.Now);
        }

        private void BtnSelectParty_Click(object? sender, EventArgs e)
        {
            var result = PartySelectionHelper.ShowSelectionDialog(_connectionString, isContractor: false, owner: this);
            if (result.Id > 0)
            {
                _selectedPartyId = result.Id;
                txtPartyName.Text = result.Name;
            }
        }

        private void LoadData()
        {
            if (_existingExpense == null) return;

            _selectedPartyId = _existingExpense.PartyId;
            txtPartyName.Text = _existingExpense.PartyName;
            txtInvoiceNumber.Text = _existingExpense.InvoiceNumber;
            txtInvoiceDate.Text = DateHelper.ToPersianDate(_existingExpense.InvoiceDate);

            // استفاده از NumberHelper برای نمایش مبلغ
            txtAmount.Text = NumberHelper.FormatThousands(_existingExpense.Amount, usePersianDigits: true);

            txtDescription.Text = _existingExpense.Description;
        }

        // ==========================================================
        // 💾 ذخیره فاکتور (Async)
        // ==========================================================
        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            // ۱. اعتبارسنجی مبلغ (با پشتیبانی از ارقام فارسی)
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            if (amount <= 0)
            {
                ModernMessageBox.ShowWarning("لطفاً مبلغ فاکتور را به درستی وارد کنید.");
                txtAmount.Focus();
                return;
            }

            // ۲. اعتبارسنجی تاریخ
            if (!DateHelper.IsValidPersianDate(txtInvoiceDate.Text))
            {
                ModernMessageBox.ShowWarning("تاریخ فاکتور معتبر نیست.");
                txtInvoiceDate.Focus();
                return;
            }

            DateTime? invDate = DateHelper.ParsePersianDate(txtInvoiceDate.Text);

            // ۳. ساخت مدل
            var expense = new ProjectExpense
            {
                Id = _isEdit && _existingExpense != null ? _existingExpense.Id : 0,
                ClientContractId = _clientContract.Id,
                PartyId = _selectedPartyId,
                InvoiceNumber = txtInvoiceNumber.Text.Trim(),
                InvoiceDate = invDate.Value,
                Amount = amount,
                Description = txtDescription.Text.Trim()
            };

            // مهر زمانی سیستم
            if (_isEdit) DateStampHelper.StampUpdated(expense);
            else DateStampHelper.StampCreated(expense);

            // ۴. ذخیره در دیتابیس با نمایش Overlay
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ProjectExpenseRepository(_connectionString);
                if (_isEdit)
                    await Task.Run(() => repo.Update(expense));
                else
                    await Task.Run(() => repo.Add(expense));
            }, "در حال ذخیره فاکتور...");

            // ۵. بستن فرم در صورت موفقیت
            if (success)
            {
                ToastHelper.Show("فاکتور با موفقیت ذخیره شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}