using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;

namespace SMP.Forms
{
    public class BankAccountManagementForm : Form
    {
        private readonly BankAccountRepository _bankAccountRepository;
        private DataGridView dgvAccounts = null!;

        public BankAccountManagementForm(string connectionString)
        {
            _bankAccountRepository = new BankAccountRepository(connectionString);
            BuildUI();
        }

        private void BuildUI()
        {
            var (topRightPanel, topLeftPanel, grid, bottomPanel) = StandardFormHelper.SetupStandardListLayout(
                this, "مدیریت حساب‌های بانکی", new Size(1100, 700));

            dgvAccounts = grid;

            dgvAccounts.AutoGenerateColumns = false;
            dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", DataPropertyName = "Title", HeaderText = "عنوان حساب", FillWeight = 25 });
            dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "BankName", DataPropertyName = "BankName", HeaderText = "نام بانک", FillWeight = 15 });
            dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "AccountNumber", DataPropertyName = "AccountNumber", HeaderText = "شماره حساب / شبا", FillWeight = 20 });
            dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "InitialBalance", DataPropertyName = "InitialBalance", HeaderText = "موجودی اولیه", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });
            dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentBalance", DataPropertyName = "CurrentBalance", HeaderText = "موجودی فعلی", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });

            // ✅ اضافه شدن ستون دسته چک و تغییر ستون‌های تیک به تکست برای استایل فانتزی
            dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "HasCheckbook", DataPropertyName = "HasCheckbook", HeaderText = "دسته چک", FillWeight = 10 });
            dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsActive", DataPropertyName = "IsActive", HeaderText = "وضعیت", FillWeight = 10 });

            // ✅ FIX: فانتزی کردن ستون‌های وضعیت و دسته چک با ایموجی و رنگ
            dgvAccounts.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string colName = dgvAccounts.Columns[e.ColumnIndex].Name;

                if (colName == "IsActive")
                {
                    if (e.Value != null)
                    {
                        bool isActive = Convert.ToBoolean(e.Value);
                        e.Value = isActive ? "✔️ فعال" : "❌ غیرفعال";
                        e.CellStyle.ForeColor = isActive ? UIHelper.ActionGreen : UIHelper.WarmDanger;
                        e.CellStyle.Font = UIHelper.PersianFont(9.5f, FontStyle.Bold);
                        e.FormattingApplied = true;
                    }
                }
                else if (colName == "HasCheckbook")
                {
                    if (e.Value != null)
                    {
                        bool hasCheckbook = Convert.ToBoolean(e.Value);
                        e.Value = hasCheckbook ? "📑 دارد" : "🚫 ندارد";
                        e.CellStyle.ForeColor = hasCheckbook ? UIHelper.SectionPurple : Color.Gray;
                        e.CellStyle.Font = UIHelper.PersianFont(9.5f, FontStyle.Bold);
                        e.FormattingApplied = true;
                    }
                }
            };

            var btnAdd = UIHelper.CreateButton("➕ افزودن حساب جدید", UIHelper.ActionGreen, 160, 42);
            btnAdd.Click += async (s, e) =>
            {
                using var form = new BankAccountEditForm(AppConfig.ConnectionString);
                if (form.ShowDialog(this) == DialogResult.OK)
                    await LoadDataAsync();
            };

            var btnEdit = UIHelper.CreateButton("✏️ ویرایش حساب", UIHelper.SectionBlue, 130, 42);
            btnEdit.Click += async (s, e) =>
            {
                if (dgvAccounts.CurrentRow?.DataBoundItem is CompanyBankAccount acc)
                {
                    using var form = new BankAccountEditForm(AppConfig.ConnectionString, acc);
                    if (form.ShowDialog(this) == DialogResult.OK)
                        await LoadDataAsync();
                }
                else
                {
                    ModernMessageBox.ShowWarning("لطفاً ابتدا یک حساب را از جدول انتخاب کنید.");
                }
            };

            var btnDelete = UIHelper.CreateButton("🗑️ حذف حساب", UIHelper.ActionRed, 120, 42);
            btnDelete.Click += async (s, e) =>
            {
                if (dgvAccounts.CurrentRow?.DataBoundItem is CompanyBankAccount acc)
                {
                    if (!ModernMessageBox.AskConfirm($"آیا از حذف «{acc.Title}» مطمئن هستید؟\n\nتوجه: حسابی که دارای تراکنش، چک یا دسته‌چک باشد قابل حذف نیست.", "حذف حساب"))
                        return;

                    bool success = await AsyncHelper.RunWithLoading(this, async () =>
                    {
                        await _bankAccountRepository.DeleteAsync(acc.Id);
                    }, "در حال حذف...");

                    if (success)
                    {
                        ToastHelper.Show("حساب حذف شد.", isSuccess: true);
                        await LoadDataAsync();
                    }
                }
                else
                {
                    ModernMessageBox.ShowWarning("لطفاً ابتدا یک حساب را انتخاب کنید.");
                }
            };

            var btnCheckbook = UIHelper.CreateButton("📑 مدیریت دسته چک", UIHelper.SectionPurple, 160, 42);
            btnCheckbook.Click += async (s, e) =>
            {
                if (dgvAccounts.CurrentRow?.DataBoundItem is CompanyBankAccount acc)
                {
                    using var form = new CheckbookManagementForm(AppConfig.ConnectionString, acc);
                    form.ShowDialog(this);
                    await LoadDataAsync();
                }
                else
                {
                    ModernMessageBox.ShowWarning("لطفاً ابتدا یک حساب بانکی را انتخاب کنید.");
                }
            };

            var btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی", UIHelper.SectionGray, 120, 42);
            btnRefresh.Click += async (s, e) => await LoadDataAsync();

            bottomPanel.Controls.Add(btnAdd);
            bottomPanel.Controls.Add(btnEdit);
            bottomPanel.Controls.Add(btnDelete);
            bottomPanel.Controls.Add(btnCheckbook);
            bottomPanel.Controls.Add(btnRefresh);

            this.Shown += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            var (success, accounts) = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var allAccounts = await _bankAccountRepository.GetAllAsync();
                // فقط حساب‌های بانکی نمایش داده می‌شوند (تنخواه‌ها حذف می‌شوند)
                return allAccounts.Where(a => !a.IsCashDesk).ToList();
            }, "در حال بارگذاری حساب‌های بانکی...");

            if (success && accounts != null)
            {
                dgvAccounts.DataSource = accounts;
            }
        }
    }

    // =========================================================================
    // ✅ فرم داخلی برای افزودن و ویرایش حساب بانکی
    // =========================================================================
    public class BankAccountEditForm : Form
    {
        private readonly BankAccountRepository _repo;
        private readonly CompanyBankAccount? _existingAccount;
        private readonly bool _isEdit;

        private TextBox txtTitle = null!;
        private TextBox txtAccountNumber = null!;
        private ComboBox cmbBankCode = null!;
        private TextBox txtInitialBalance = null!;
        private CheckBox chkIsActive = null!;
        private CheckBox chkHasCheckbook = null!;

        public BankAccountEditForm(string connectionString, CompanyBankAccount? account = null)
        {
            _repo = new BankAccountRepository(connectionString);
            _existingAccount = account;
            _isEdit = account != null;

            BuildUI();

            if (_isEdit) LoadData();
            else txtInitialBalance.Text = "0";
        }

        private void BuildUI()
        {
            var (formLayout, btnPanel) = StandardFormHelper.SetupStandardLayout(
                this,
                _isEdit ? "ویرایش حساب بانکی" : "افزودن حساب بانکی جدید",
                new Size(700, 550));

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "عنوان حساب:", txtTitle = new TextBox { PlaceholderText = "مثلاً: جاری اصلی" }, 0, 0, 1, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "شماره حساب / شبا:", txtAccountNumber = new TextBox(), 1, 0, 1);

            cmbBankCode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var bank in BankHelper.BankList)
            {
                cmbBankCode.Items.Add(new BankComboItem { Code = bank.Key, Name = bank.Value });
            }
            UIHelper.AddField(formLayout, "بانک:", cmbBankCode, 1, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "موجودی اولیه (ریال):", txtInitialBalance = new TextBox(), 2, 0, 1);
            SmartInputHelper.EnableSmartAmountTextBox(txtInitialBalance);

            StandardFormHelper.AddRow(formLayout, 50F);
            var pnlCheckboxes = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };

            chkHasCheckbook = new CheckBox { Text = "این حساب دارای دسته چک است", AutoSize = true, Margin = new Padding(20, 10, 20, 10) };
            UIHelper.StyleToggle(chkHasCheckbook);

            chkIsActive = new CheckBox { Text = "حساب فعال است", AutoSize = true, Checked = true, Margin = new Padding(20, 10, 20, 10) };
            UIHelper.StyleToggle(chkIsActive);
            chkIsActive.CheckedChanged += (s, e) => chkHasCheckbook.Enabled = chkIsActive.Checked;

            pnlCheckboxes.Controls.Add(chkHasCheckbook);
            pnlCheckboxes.Controls.Add(chkIsActive);

            formLayout.Controls.Add(pnlCheckboxes, 0, 3);
            formLayout.SetColumnSpan(pnlCheckboxes, 4);

            var btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.ActionGreen, 150, 45);
            btnSave.Click += async (s, e) => await SaveAsync();
            btnPanel.Controls.Add(btnSave);

            var btnCancel = UIHelper.CreateButton("انصراف", UIHelper.SectionGray, 100, 45);
            btnCancel.Click += (s, e) => this.Close();
            btnPanel.Controls.Add(btnCancel);
        }

        private void LoadData()
        {
            if (_existingAccount == null) return;

            txtTitle.Text = _existingAccount.Title;
            txtAccountNumber.Text = _existingAccount.AccountNumber;
            txtInitialBalance.Text = NumberHelper.FormatThousands(_existingAccount.InitialBalance, usePersianDigits: true);

            chkIsActive.Checked = _existingAccount.IsActive;
            chkHasCheckbook.Checked = _existingAccount.HasCheckbook;

            foreach (BankComboItem item in cmbBankCode.Items)
            {
                if (item.Code == _existingAccount.BankCode) { cmbBankCode.SelectedItem = item; break; }
            }
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                ModernMessageBox.ShowWarning("عنوان حساب الزامی است.");
                return;
            }

            var bankItem = cmbBankCode.SelectedItem as BankComboItem;
            string? bankCode = string.IsNullOrEmpty(bankItem?.Code) ? null : bankItem.Code;

            if (string.IsNullOrEmpty(bankCode))
            {
                ModernMessageBox.ShowWarning("لطفاً یک بانک انتخاب کنید.");
                return;
            }

            var acc = new CompanyBankAccount
            {
                Id = _existingAccount?.Id ?? 0,
                Title = txtTitle.Text.Trim(),
                AccountNumber = txtAccountNumber.Text.Trim(),
                BankCode = bankCode,
                InitialBalance = NumberHelper.ParseFormatted(txtInitialBalance.Text),
                IsCashDesk = false,
                IsActive = chkIsActive.Checked,
                HasCheckbook = chkHasCheckbook.Checked
            };

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_isEdit) await _repo.UpdateAsync(acc);
                else await _repo.AddAsync(acc);
            }, "در حال ذخیره...");

            if (success)
            {
                ToastHelper.Show("حساب با موفقیت ذخیره شد.", isSuccess: true);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private class BankComboItem
        {
            public string Code { get; set; } = "";
            public string Name { get; set; } = "";
            public override string ToString() => Name;
        }
    }
}