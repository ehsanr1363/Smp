using System;
using System.Drawing;
using System.Windows.Forms;
using PersonnelApp.Models;
using PersonnelApp.Helpers;
using PersonnelApp.Data;

namespace PersonnelApp.Forms
{
    public class ClientContractEditForm : Form
    {
        private readonly string _connectionString;
        private readonly ClientContract _contract;
        private readonly bool _isEdit;

        private TextBox txtPartyName = null!;
        private int _selectedPartyId = 0;

        private TextBox txtContractNumber = null!, txtContractRow = null!, txtSubject = null!, txtAmount = null!;
        private TextBox txtContractDate = null!, txtEndDate = null!;
        private Button btnSave = null!;

        public ClientContractEditForm(string connectionString, ClientContract? contract = null)
        {
            _connectionString = connectionString;
            _contract = contract ?? new ClientContract();
            _isEdit = contract != null;

            BuildUI();

            // فعال‌سازی Ctrl+S برای ذخیره و Esc برای بستن
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            if (_isEdit) LoadData();
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5, AutoSize = true, Padding = new Padding(15) };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            for (int i = 0; i < 5; i++)
                formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            // --- ردیف ۰: انتخاب کارفرما ---
            var lblParty = new Label { Text = "کارفرما:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
            UIHelper.StyleLabel(lblParty);

            txtPartyName = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, TextAlign = HorizontalAlignment.Center };
            txtPartyName.BackColor = UIHelper.WarmInputBackground;
            txtPartyName.BorderStyle = BorderStyle.FixedSingle;
            txtPartyName.Font = UIHelper.PersianFont(12f);
            txtPartyName.Margin = new Padding(5, 8, 5, 5);

            var btnSelect = UIHelper.CreateButton("🔍 انتخاب", UIHelper.WarmSecondary, 80, 35);
            btnSelect.Margin = new Padding(5, 8, 5, 5);
            btnSelect.Click += BtnSelect_Click;

            formLayout.Controls.Add(lblParty, 0, 0);
            formLayout.Controls.Add(txtPartyName, 1, 0);
            formLayout.SetColumnSpan(txtPartyName, 2);
            formLayout.Controls.Add(btnSelect, 3, 0);

            // --- ردیف ۱: شماره و ردیف قرارداد ---
            txtContractNumber = new TextBox();
            txtContractRow = new TextBox();

            UIHelper.AddField(formLayout, "شماره قرارداد:", txtContractNumber, 1, labelCol: 0, controlCol: 1);
            UIHelper.AddField(formLayout, "ردیف قرارداد:", txtContractRow, 1, labelCol: 2, controlCol: 3);

            // --- ردیف ۲: موضوع ---
            txtSubject = new TextBox();
            UIHelper.AddField(formLayout, "موضوع:", txtSubject, 2, labelCol: 0, controlCol: 1, controlSpan: 3);

            // --- ردیف ۳: مبلغ و تاریخ قرارداد ---
            txtAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount); // فرمت سه‌رقمی خودکار

            txtContractDate = new TextBox();
            DateHelper.ApplyDateMask(txtContractDate);

            UIHelper.AddField(formLayout, "مبلغ (ریال):", txtAmount, 3, labelCol: 0, controlCol: 1);
            UIHelper.AddField(formLayout, "تاریخ قرارداد:", txtContractDate, 3, labelCol: 2, controlCol: 3);

            // --- ردیف ۴: تاریخ پایان ---
            txtEndDate = new TextBox();
            DateHelper.ApplyDateMask(txtEndDate);

            UIHelper.AddField(formLayout, "تاریخ پایان:", txtEndDate, 4, labelCol: 0, controlCol: 1);

            // --- دکمه ذخیره ---
            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.WarmPrimary, 200, 45);
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

            this.Controls.Add(mainLayout);

            // اعمال هدر و فعال‌سازی خودکار Enter-Navigation
            this.ApplyStandardHeader(_isEdit ? "ویرایش قرارداد کارفرما" : "افزودن قرارداد کارفرما", new Size(800, 650));
        }

        private void BtnSelect_Click(object? sender, EventArgs e)
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
            _selectedPartyId = _contract.PartyId;
            txtPartyName.Text = _contract.DisplayName;

            txtContractNumber.Text = _contract.ContractNumber;
            txtContractRow.Text = _contract.ContractRow;
            txtSubject.Text = _contract.Subject;
            txtAmount.Text = NumberHelper.FormatThousands(_contract.Amount);

            txtContractDate.Text = DateHelper.ToPersianDate(_contract.ContractDate);
            txtEndDate.Text = DateHelper.ToPersianDate(_contract.EndDate);
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            // ۱. اعتبارسنجی کارفرما
            if (_selectedPartyId == 0)
            {
                ModernMessageBox.ShowWarning("لطفاً یک کارفرما را از لیست انتخاب کنید.");
                return;
            }

            // ۲. اعتبارسنجی تاریخ‌ها (اختیاری اما توصیه‌شده)
            if (!string.IsNullOrWhiteSpace(txtContractDate.Text) && !DateHelper.IsValidPersianDate(txtContractDate.Text))
            {
                ModernMessageBox.ShowWarning("تاریخ قرارداد وارد شده معتبر نیست.");
                txtContractDate.Focus();
                return;
            }

            // ۳. پارس مبلغ
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);

            // ۴. ساخت مدل
            var c = new ClientContract
            {
                Id = _isEdit ? _contract.Id : 0,
                PartyId = _selectedPartyId,
                ContractNumber = txtContractNumber.Text.Trim(),
                ContractRow = txtContractRow.Text.Trim(),
                Subject = txtSubject.Text.Trim(),
                Amount = amount,
                ContractDate = DateHelper.ParsePersianDate(txtContractDate.Text),
                EndDate = DateHelper.ParsePersianDate(txtEndDate.Text)
            };

            // ۵. ذخیره در دیتابیس به همراه نمایش Overlay
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ClientContractRepository(_connectionString);

                if (_isEdit)
                {
                    DateStampHelper.StampUpdated(c);
                    await Task.Run(() => repo.Update(c)); // اگر متد ریپازیتوری Async نیست
                }
                else
                {
                    DateStampHelper.StampCreated(c);
                    await Task.Run(() => repo.Add(c));
                }
            }, "در حال ذخیره قرارداد...");

            // ۶. بستن فرم در صورت موفقیت
            if (success)
            {
                ToastHelper.Show("قرارداد با موفقیت ذخیره شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}