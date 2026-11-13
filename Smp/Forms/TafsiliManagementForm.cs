using ClosedXML.Excel;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class TafsiliManagementForm : Form
    {
        private readonly TafsiliRepository _repository;

        // کنترل‌های اصلی
        private DataGridView dgvTafsili = null!;
        private ComboBox cmbFilterType = null!;
        private TextBox txtSearch = null!;

        // کنترل‌های فرم ثبت
        private TextBox txtCode = null!, txtFirstName = null!, txtLastName = null!, txtCompanyName = null!, txtNationalCode = null!, txtNationalId = null!, txtEconomicCode = null!;
        private ComboBox cmbType = null!, cmbPersonType = null!;
        private CheckBox chkIsActive = null!;

        // کنترل‌های شبا
        private DataGridView dgvShebas = null!;
        private Button btnAddSheba = null!, btnDeleteSheba = null!, btnSetMainSheba = null!;

        private Button btnAdd = null!, btnUpdate = null!, btnDelete = null!, btnImport = null!, btnDownloadTemplate = null!;

        private int _editingId = 0;
        private List<Tafsili> _allTafsilis = new();
        private BindingList<TafsiliBankAccount> _bankAccounts = new();

        private bool _isInitializing = true;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        private readonly string[] _roles = {
            "کارمند", "پیمانکار", "کارفرما", "تأمین‌کننده", "شخص وابسته", "سهامدار", "بانک", "صندوق", "سایر بدهکاران", "سایر بستانکاران", "سایر نقش‌ها"
        };

        public TafsiliManagementForm(string connectionString)
        {
            _repository = new TafsiliRepository(connectionString);
            BuildUI();
            this.ApplyStandardHeader("مدیریت تفصیلی‌های حسابداری", new Size(1150, 850));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnAdd);
            Shown += async (s, e) => await LoadTafsilisSafeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _loadLock.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));   // نوار ابزار بالا
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 340F));  // فرم ثبت و شبا
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // جدول اصلی

            // ==========================================
            // ۱. نوار ابزار بالا (جستجو و اکسل)
            // ==========================================
            var topPanel = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(10, 5, 10, 5) };
            var topFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };

            txtSearch = new TextBox { Width = 250, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 15, 5), PlaceholderText = "جستجو (کد، نام یا شبا)..." };
            txtSearch.TextChanged += (s, e) => ApplyFilters();

            cmbFilterType = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 5, 5) };
            var filterItems = new List<string> { "همه" };
            filterItems.AddRange(_roles);
            cmbFilterType.Items.AddRange(filterItems.ToArray());
            cmbFilterType.SelectedIndex = 0;
            cmbFilterType.SelectedIndexChanged += (s, e) => ApplyFilters();

            btnDownloadTemplate = UIHelper.CreateButton("📥 نمونه فایل اکسل", UIHelper.SectionTeal, 160, 35);
            btnDownloadTemplate.Margin = new Padding(5, 3, 5, 5);
            btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

            btnImport = UIHelper.CreateButton("📤 ایمپورت از اکسل", UIHelper.ActionGreen, 160, 35);
            btnImport.Margin = new Padding(5, 3, 5, 5);
            btnImport.Click += BtnImport_Click;

            topFlow.Controls.Add(new Label { Text = "جستجو:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(txtSearch);
            topFlow.Controls.Add(new Label { Text = "فیلتر نقش:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(15, 13, 5, 5) });
            topFlow.Controls.Add(cmbFilterType);
            topFlow.Controls.Add(btnDownloadTemplate);
            topFlow.Controls.Add(btnImport);

            topPanel.Controls.Add(topFlow);

            // ==========================================
            // ۲. فرم ثبت و جدول شبا (چیدمان دو ستونی)
            // ==========================================
            var inputAreaLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            inputAreaLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F)); // سمت راست: فرم
            inputAreaLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F)); // سمت چپ: شبا

            // --- فرم اطلاعات تفصیلی ---
            var formGroup = new GroupBox { Text = "اطلاعات تفصیلی", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Padding = new Padding(10) };
            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5 };

            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            for (int i = 0; i < 5; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            txtCode = new TextBox();
            cmbType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbType.Items.AddRange(_roles);
            cmbType.SelectedIndex = 0;

            cmbPersonType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPersonType.Items.AddRange(new object[] { "شخص حقیقی", "شخص حقوقی" });
            cmbPersonType.SelectedIndex = 0;

            txtFirstName = new TextBox();
            txtLastName = new TextBox();
            txtCompanyName = new TextBox();
            txtNationalCode = new TextBox();
            txtNationalId = new TextBox();
            txtEconomicCode = new TextBox();
            chkIsActive = new CheckBox { Text = "فعال", Checked = true };
            UIHelper.StyleToggle(chkIsActive);

            UIHelper.AddField(formLayout, "کد تفصیلی:", txtCode, 0, 0, 1);
            UIHelper.AddField(formLayout, "نوع شخص:", cmbPersonType, 0, 2, 3);

            UIHelper.AddField(formLayout, "نام:", txtFirstName, 1, 0, 1);
            UIHelper.AddField(formLayout, "نام خانوادگی:", txtLastName, 1, 2, 3);

            UIHelper.AddField(formLayout, "نام شرکت:", txtCompanyName, 2, 0, 1);
            UIHelper.AddField(formLayout, "نقش:", cmbType, 2, 2, 3);

            UIHelper.AddField(formLayout, "کد ملی:", txtNationalCode, 3, 0, 1);
            UIHelper.AddField(formLayout, "شناسه ملی:", txtNationalId, 3, 2, 3);

            UIHelper.AddField(formLayout, "کد اقتصادی:", txtEconomicCode, 4, 0, 1);
            formLayout.Controls.Add(chkIsActive, 3, 4);

            formGroup.Controls.Add(formLayout);

            // --- پنل شماره شباها ---
            var shebaGroup = new GroupBox { Text = "شماره‌های شبا", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Padding = new Padding(10) };
            var shebaLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            shebaLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            shebaLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            dgvShebas = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, EditMode = DataGridViewEditMode.EditOnEnter };
            dgvShebas.ApplyModernStyle();
            dgvShebas.ReadOnly = false;
            dgvShebas.Columns.Add(new DataGridViewTextBoxColumn { Name = "ShebaNumber", DataPropertyName = "ShebaNumber", HeaderText = "شماره شبا", FillWeight = 70 });
            dgvShebas.Columns.Add(new DataGridViewTextBoxColumn { Name = "BankName", DataPropertyName = "BankName", HeaderText = "بانک", FillWeight = 30, ReadOnly = true });
            dgvShebas.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsMainAccount", DataPropertyName = "IsMainAccount", HeaderText = "اصلی", FillWeight = 20 });

            var shebaBtnFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            btnAddSheba = UIHelper.CreateButton("➕", UIHelper.ActionGreen, 40, 30);
            btnDeleteSheba = UIHelper.CreateButton("🗑️", UIHelper.ActionRed, 40, 30);
            btnSetMainSheba = UIHelper.CreateButton("⭐", UIHelper.WarmSecondary, 40, 30);

            btnAddSheba.Click += BtnAddSheba_Click;
            btnDeleteSheba.Click += BtnDeleteSheba_Click;
            btnSetMainSheba.Click += BtnSetMainSheba_Click;

            shebaBtnFlow.Controls.Add(btnAddSheba);
            shebaBtnFlow.Controls.Add(btnDeleteSheba);
            shebaBtnFlow.Controls.Add(btnSetMainSheba);

            shebaLayout.Controls.Add(dgvShebas, 0, 0);
            shebaLayout.Controls.Add(shebaBtnFlow, 0, 1);
            shebaGroup.Controls.Add(shebaLayout);

            inputAreaLayout.Controls.Add(formGroup, 0, 0);
            inputAreaLayout.Controls.Add(shebaGroup, 1, 0);

            // ==========================================
            // ۳. جدول اصلی و دکمه‌های پایین
            // ==========================================
            var gridAreaLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            gridAreaLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            gridAreaLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            dgvTafsili = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
            dgvTafsili.ApplyModernStyle();

            // ستون‌های ضروری برای نمایش در جدول (بقیه مخفی می‌شوند)
            dgvTafsili.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Code", HeaderText = "کد", FillWeight = 15 });
            dgvTafsili.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "نام / شرکت", FillWeight = 35 });
            dgvTafsili.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Type", HeaderText = "نقش", FillWeight = 15 });
            dgvTafsili.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NationalCode", HeaderText = "کد/شناسه ملی", FillWeight = 15 });
            dgvTafsili.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ShebaNumber", HeaderText = "شبا (اصلی)", FillWeight = 25 });
            dgvTafsili.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "IsActive", HeaderText = "وضعیت", FillWeight = 10 });

            dgvTafsili.SelectionChanged += (s, e) => SelectRow();

            dgvTafsili.CellFormatting += (s, e) => {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgvTafsili.Columns[e.ColumnIndex].DataPropertyName == "IsActive" && e.Value != null)
                {
                    bool isActive = Convert.ToBoolean(e.Value);
                    e.Value = isActive ? "✔ فعال" : "✖ غیرفعال";
                    e.CellStyle.ForeColor = isActive ? UIHelper.ActionGreen : UIHelper.WarmDanger;
                    e.FormattingApplied = true;
                }
            };

            var bottomPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 5, 0, 0) };
            btnAdd = UIHelper.CreateButton("➕ ثبت جدید", UIHelper.ActionGreen, 140, 40);
            btnAdd.Click += BtnAdd_Click;

            btnUpdate = UIHelper.CreateButton("💾 ذخیره ویرایش", UIHelper.SectionBlue, 140, 40);
            btnUpdate.Enabled = false;
            btnUpdate.Click += BtnUpdate_Click;

            btnDelete = UIHelper.CreateButton("🗑️ حذف انتخاب‌شده", UIHelper.ActionRed, 160, 40);
            btnDelete.Click += BtnDelete_Click;

            bottomPanel.Controls.Add(btnAdd);
            bottomPanel.Controls.Add(btnUpdate);
            bottomPanel.Controls.Add(btnDelete);

            gridAreaLayout.Controls.Add(dgvTafsili, 0, 0);
            gridAreaLayout.Controls.Add(bottomPanel);

            // اضافه کردن به فرم اصلی
            mainLayout.Controls.Add(topPanel, 0, 0);
            mainLayout.Controls.Add(inputAreaLayout, 0, 1);
            mainLayout.Controls.Add(gridAreaLayout, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void BtnAddSheba_Click(object? sender, EventArgs e)
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox("شماره شبا را وارد کنید (با یا بدون IR):", "افزودن شبا", "");
            if (string.IsNullOrWhiteSpace(input)) return;

            string normalized = BankHelper.NormalizeSheba(input);
            if (string.IsNullOrEmpty(normalized) || normalized.Length != 24)
            {
                ModernMessageBox.ShowWarning("شماره شبا نامعتبر است. باید ۲۴ رقم باشد.");
                return;
            }

            string fullSheba = "IR" + normalized;
            if (_bankAccounts.Any(x => x.ShebaNumber == fullSheba))
            {
                ModernMessageBox.ShowWarning("این شبا قبلاً ثبت شده است.");
                return;
            }

            var newAcc = new TafsiliBankAccount
            {
                ShebaNumber = fullSheba,
                BankName = BankHelper.GetBankNameFromSheba(normalized),
                IsMainAccount = _bankAccounts.Count == 0
            };

            _bankAccounts.Add(newAcc);
            if (newAcc.IsMainAccount)
            {
                foreach (var a in _bankAccounts) if (a != newAcc) a.IsMainAccount = false;
                dgvShebas.Refresh();
            }
        }

        private void BtnDeleteSheba_Click(object? sender, EventArgs e)
        {
            if (dgvShebas.CurrentRow?.DataBoundItem is not TafsiliBankAccount acc) return;
            if (_bankAccounts.Count <= 1)
            {
                ModernMessageBox.ShowWarning("حداقل یک شبا باید باقی بماند.");
                return;
            }

            bool wasMain = acc.IsMainAccount;
            _bankAccounts.Remove(acc);
            if (wasMain && _bankAccounts.Count > 0) _bankAccounts[0].IsMainAccount = true;
            dgvShebas.Refresh();
        }

        private void BtnSetMainSheba_Click(object? sender, EventArgs e)
        {
            if (dgvShebas.CurrentRow?.DataBoundItem is not TafsiliBankAccount acc) return;
            foreach (var a in _bankAccounts) a.IsMainAccount = (a == acc);
            dgvShebas.Refresh();
        }

        private async Task LoadTafsilisSafeAsync()
        {
            if (IsDisposed) return;
            try { await _loadLock.WaitAsync(); } catch { return; }

            try
            {
                var (success, list) = await AsyncHelper.RunWithLoading(this, () => _repository.GetAllAsync(""), "در حال بارگذاری...");
                if (!success || list == null) return;

                _allTafsilis = list;
                ApplyFilters();
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
                _isInitializing = false;
            }
        }

        private void ApplyFilters()
        {
            var filtered = _allTafsilis.AsEnumerable();
            string typeFilter = cmbFilterType.SelectedItem?.ToString() ?? "همه";
            if (typeFilter != "همه") filtered = filtered.Where(x => x.Type == typeFilter);

            string searchText = txtSearch.Text.Trim();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(x =>
                    (x.Code != null && x.Code.Contains(searchText)) ||
                    (x.Name != null && x.Name.Contains(searchText)) ||
                    (x.ShebaNumber != null && x.ShebaNumber.Contains(searchText))
                );
            }

            bool prevInit = _isInitializing;
            _isInitializing = true;
            try
            {
                dgvTafsili.DataSource = filtered.ToList();
                ClearForm();
            }
            finally { _isInitializing = prevInit; }
        }

        private async void SelectRow()
        {
            if (_isInitializing) return;
            if (dgvTafsili.CurrentRow?.DataBoundItem is not Tafsili t) return;

            _editingId = t.Id;
            txtCode.Text = t.Code;
            cmbPersonType.SelectedItem = t.PersonType.Contains("حقوقی") ? "شخص حقوقی" : "شخص حقیقی";
            txtFirstName.Text = t.FirstName; txtLastName.Text = t.LastName; txtCompanyName.Text = t.CompanyName;
            cmbType.SelectedItem = t.Type;
            txtNationalCode.Text = t.NationalCode; txtNationalId.Text = t.NationalId; txtEconomicCode.Text = t.EconomicCode;
            chkIsActive.Checked = t.IsActive;

            btnAdd.Enabled = false;
            btnUpdate.Enabled = true;

            var accounts = await _repository.GetBankAccountsAsync(t.Id);
            _bankAccounts = new BindingList<TafsiliBankAccount>(accounts);
            dgvShebas.DataSource = _bankAccounts;
        }

        private void ClearForm()
        {
            _editingId = 0;
            txtCode.Text = ""; txtFirstName.Text = ""; txtLastName.Text = ""; txtCompanyName.Text = "";
            txtNationalCode.Text = ""; txtNationalId.Text = ""; txtEconomicCode.Text = "";
            chkIsActive.Checked = true;
            cmbType.SelectedIndex = 0; cmbPersonType.SelectedIndex = 0;
            btnAdd.Enabled = true; btnUpdate.Enabled = false;

            _bankAccounts.Clear();
            dgvShebas.DataSource = null;

            if (dgvTafsili.Rows.Count > 0) { dgvTafsili.ClearSelection(); dgvTafsili.CurrentCell = null; }
        }

        private async void BtnAdd_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCode.Text)) { ModernMessageBox.ShowWarning("کد الزامی است."); return; }
            if (_allTafsilis.Any(x => x.Code == txtCode.Text.Trim())) { ModernMessageBox.ShowWarning("این کد قبلاً ثبت شده است."); return; }

            var t = new Tafsili
            {
                Code = txtCode.Text.Trim(),
                PersonType = cmbPersonType.Text,
                FirstName = txtFirstName.Text.Trim(),
                LastName = txtLastName.Text.Trim(),
                CompanyName = txtCompanyName.Text.Trim(),
                Type = cmbType.Text,
                NationalCode = txtNationalCode.Text.Trim(),
                NationalId = txtNationalId.Text.Trim(),
                EconomicCode = txtEconomicCode.Text.Trim(),
                IsActive = chkIsActive.Checked
            };

            bool success = await AsyncHelper.RunWithLoading(this, async () => {
                await _repository.AddAsync(t);
                await _repository.SaveBankAccountsAsync(t.Id, _bankAccounts.ToList());
            }, "در حال ثبت...");

            if (success) { ToastHelper.Show("ثبت شد.", true); await LoadTafsilisSafeAsync(); }
        }

        private async void BtnUpdate_Click(object? sender, EventArgs e)
        {
            if (_editingId == 0) return;

            var t = new Tafsili
            {
                Id = _editingId,
                Code = txtCode.Text.Trim(),
                PersonType = cmbPersonType.Text,
                FirstName = txtFirstName.Text.Trim(),
                LastName = txtLastName.Text.Trim(),
                CompanyName = txtCompanyName.Text.Trim(),
                Type = cmbType.Text,
                NationalCode = txtNationalCode.Text.Trim(),
                NationalId = txtNationalId.Text.Trim(),
                EconomicCode = txtEconomicCode.Text.Trim(),
                IsActive = chkIsActive.Checked
            };

            bool success = await AsyncHelper.RunWithLoading(this, async () => {
                await _repository.UpdateAsync(t);
                await _repository.SaveBankAccountsAsync(t.Id, _bankAccounts.ToList());
            }, "در حال ذخیره...");

            if (success) { ToastHelper.Show("ویرایش شد.", true); await LoadTafsilisSafeAsync(); }
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (_editingId == 0) return;
            if (!ModernMessageBox.AskConfirm("حذف شود؟", "تایید")) return;
            await AsyncHelper.RunWithLoading(this, () => _repository.DeleteAsync(_editingId), "در حال حذف...");
            await LoadTafsilisSafeAsync();
        }

        private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = "Tafsili_Import_Template.xlsx",
                Title = "ذخیره قالب اکسل تفصیلی‌ها"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("تفصیلی‌ها");
                ws.RightToLeft = true;

                string[] headers = {
                    "کد تفصیلی", "نوع شخص (حقیقی/حقوقی)", "نام", "نام خانوادگی", "نام شرکت",
                    "کد ملی", "شناسه ملی", "نقش", "شماره شبا (با یا بدون IR)"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                ws.Cell(2, 1).Value = "1001";
                ws.Cell(2, 2).Value = "شخص حقیقی";
                ws.Cell(2, 3).Value = "علی";
                ws.Cell(2, 4).Value = "رضایی";
                ws.Cell(2, 8).Value = "کارمند";
                ws.Cell(2, 9).Value = "120170000000000000000001";

                ws.Columns().AdjustToContents();
                ws.Row(1).Height = 30;
                workbook.SaveAs(sfd.FileName);
                ToastHelper.Show("قالب اکسل ساخته شد.", true);
            }
            catch (Exception ex)
            {
                ModernMessageBox.ShowError("خطا در ساخت قالب:\n" + ex.Message);
            }
        }

        private async void BtnImport_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل تفصیلی‌ها" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            (int Inserted, int Updated, List<string> Errors)? result = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                try { result = await _repository.ImportFromExcelAsync(ofd.FileName); }
                catch (IOException) { throw new IOException("فایل اکسل در حال حاضر باز است."); }
            }, "در حال ایمپورت...");

            if (!success || result == null) return;

            var (inserted, updated, errors) = result.Value;
            string resultMessage = $"✅ ایمپورت انجام شد!\n\n➕ جدید: {inserted}\n🔄 به‌روزرسانی: {updated}";
            if (errors.Count > 0)
            {
                resultMessage += $"\n\n⚠️ {errors.Count} خطا:\n" + string.Join("\n", errors.Take(10));
            }

            if (inserted == 0 && updated == 0) ModernMessageBox.ShowWarning(resultMessage, "نتیجه ایمپورت");
            else { ModernMessageBox.ShowInfo(resultMessage, "نتیجه ایمپورت"); }

            await LoadTafsilisSafeAsync();
        }
    }
}