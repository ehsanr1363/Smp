using Microsoft.Data.SqlClient;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class TafsiliManagementForm : Form
{
    private readonly string _connectionString;
    private readonly TafsiliRepository _repository;

    // --- کنترل‌های فرم ---
    private ModernTextBox txtSearch = null!;
    private ComboBox cmbFilterRole = null!;

    // پنل راست: مشخصات تفصیلی
    private ModernTextBox txtCode = null!, txtFirstName = null!, txtLastName = null!, txtCompanyName = null!;
    private ModernTextBox txtNationalCode = null!, txtNationalId = null!, txtEconomicCode = null!;
    private ComboBox cmbPersonType = null!, cmbRole = null!;
    private CheckBox chkIsActive = null!;

    // پنل چپ: شماره‌های شبا
    private DataGridView dgvBankAccounts = null!;
    private ModernTextBox txtSheba = null!;
    private Button btnAddSheba = null!, btnDeleteSheba = null!;

    // لیست پایین
    private DataGridView dgvTafsilis = null!;

    // دکمه‌های پایین
    private Button btnNew = null!, btnSave = null!, btnDelete = null!;

    // --- متغیرهای کنترلی ---
    private int? _editingId = null;
    private bool _isLoadingData = true;
    private List<TafsiliBankAccount> _currentBankAccounts = new();

    public TafsiliManagementForm(string connectionString)
    {
        _connectionString = connectionString;
        _repository = new TafsiliRepository(connectionString);

        BuildUI();

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        Shown += async (s, e) => await LoadDataAsync();
    }

    private void BuildUI()
    {
        // ═══════ ۱. جستجو و فیلتر نقش ═══════
        txtSearch = ModernUI.Input(placeholder: "جستجو در نام، کد تفصیلی، کد ملی...");
        txtSearch.TextChanged += (s, e) => LoadDataAsync();

        cmbFilterRole = ModernUI.Combo();
        cmbFilterRole.Items.AddRange(new object[] { "همه", "پرسنل", "پیمانکار", "کارفرما", "تأمین‌کننده", "سایر نقش‌ها" });
        cmbFilterRole.SelectedIndex = 0;
        cmbFilterRole.SelectedIndexChanged += (s, e) => LoadDataAsync();

        var toolbar = ModernListKit.BuildToolbar(
            (txtSearch, 380),
            (ModernListKit.FilterLabel("نقش:"), 50),
            (cmbFilterRole, 200));

        // ═══════ ۲. کارت اطلاعات تفصیلی (راست) ═══════
        cmbPersonType = ModernUI.Combo();
        cmbPersonType.Items.AddRange(new[] { "شخص حقیقی", "شخص حقوقی" });
        cmbRole = ModernUI.Combo();
        cmbRole.Items.AddRange(new object[] { "پرسنل", "پیمانکار", "کارفرما", "تأمین‌کننده", "سایر نقش‌ها" });

        txtCode = ModernUI.Input();
        txtFirstName = ModernUI.Input();
        txtLastName = ModernUI.Input();
        txtCompanyName = ModernUI.Input();
        txtNationalCode = ModernUI.Input();
        txtNationalCode.TextAlign = HorizontalAlignment.Center;
        txtNationalId = ModernUI.Input();
        txtNationalId.TextAlign = HorizontalAlignment.Center;
        txtEconomicCode = ModernUI.Input();
        txtEconomicCode.TextAlign = HorizontalAlignment.Center;

        chkIsActive = ModernUI.Check("فعال");
        chkIsActive.Checked = true;

        int rh = ModernUI.RowHeight;
        var details = new ModernCard("اطلاعات تفصیلی", ModernTheme.Primary, 2, rh, rh, rh, rh, rh);
        details.Body.Controls.Add(ModernUI.Row("کد تفصیلی", txtCode), 0, 0);
        details.Body.Controls.Add(ModernUI.Row("نوع شخص", cmbPersonType), 1, 0);
        details.Body.Controls.Add(ModernUI.Row("نام", txtFirstName), 0, 1);
        details.Body.Controls.Add(ModernUI.Row("نام خانوادگی", txtLastName), 1, 1);
        details.Body.Controls.Add(ModernUI.Row("نام شرکت", txtCompanyName), 0, 2);
        details.Body.Controls.Add(ModernUI.Row("نقش", cmbRole), 1, 2);
        details.Body.Controls.Add(ModernUI.Row("کد ملی", txtNationalCode), 0, 3);
        details.Body.Controls.Add(ModernUI.Row("شناسه ملی", txtNationalId), 1, 3);
        details.Body.Controls.Add(ModernUI.Row("کد اقتصادی", txtEconomicCode), 0, 4);
        details.Body.Controls.Add(ModernUI.CenterInCell(chkIsActive, 12), 1, 4);
        int middleHeight = details.Height + 12;

        // ═══════ ۳. کارت شماره‌های شبا (چپ) ═══════
        dgvBankAccounts = ModernGrid.Create();
        dgvBankAccounts.AutoGenerateColumns = false;
        dgvBankAccounts.FillColumns();
        dgvBankAccounts.UseAiryLook(40);
        dgvBankAccounts.UseSelectionAccent(ModernTheme.Teal);
        dgvBankAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ShebaNumber", DataPropertyName = "ShebaNumber", HeaderText = "شماره شبا", FillWeight = 52, MinimumWidth = 190, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dgvBankAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "BankName", DataPropertyName = "BankName", HeaderText = "نام بانک", FillWeight = 28, MinimumWidth = 90 });
        dgvBankAccounts.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsMainAccount", DataPropertyName = "IsMainAccount", HeaderText = "حساب اصلی", FillWeight = 20, MinimumWidth = 80 });
        ModernListKit.PinHeaders(dgvBankAccounts);

        var ctxSheba = new ContextMenuStrip { Font = ModernTheme.GetFont(10f), RightToLeft = RightToLeft.Yes };
        var miDeleteSheba = new ToolStripMenuItem("حذف شماره شبا");
        miDeleteSheba.Click += BtnDeleteSheba_Click;
        ctxSheba.Items.Add(miDeleteSheba);
        dgvBankAccounts.ContextMenuStrip = ctxSheba;

        txtSheba = ModernUI.Input(placeholder: "IR...");
        btnAddSheba = ModernButton.Soft("افزودن", ModernTheme.Success, 100, 34);
        btnAddSheba.Click += BtnAddSheba_Click;
        var addBar = ModernListKit.BuildToolbar((txtSheba, -1), (btnAddSheba, 110));

        btnDeleteSheba = ModernButton.Soft("حذف شبا انتخاب‌شده", ModernTheme.Danger, 180, 34);
        btnDeleteSheba.Click += BtnDeleteSheba_Click;
        var deleteBar = ModernListKit.BuildActionBar(new Control[] { btnDeleteSheba }, new Control[0]);

        var shebaLayout = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent, Margin = Padding.Empty };
        shebaLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        shebaLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        shebaLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        shebaLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        addBar.Dock = DockStyle.Fill; deleteBar.Dock = DockStyle.Fill;
        shebaLayout.Controls.Add(ModernReport.WrapGrid(dgvBankAccounts), 0, 0);
        shebaLayout.Controls.Add(addBar, 0, 1);
        shebaLayout.Controls.Add(deleteBar, 0, 2);

        var shebaCard = new ModernCard("شماره‌های شبا", ModernTheme.Teal, new[] { new ColumnStyle(SizeType.Percent, 100F) });
        shebaCard.Body.Controls.Add(shebaLayout, 0, 0);

        var middle = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = Padding.Empty };
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));   // ستون ۰ = راست: اطلاعات
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));   // شبا
        middle.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        details.Dock = DockStyle.Fill; details.Margin = new Padding(0, 0, 6, 0);
        shebaCard.Dock = DockStyle.Fill; shebaCard.Margin = new Padding(6, 0, 0, 0);
        middle.Controls.Add(details, 0, 0);
        middle.Controls.Add(shebaCard, 1, 0);

        // ═══════ ۴. جدول اصلی ═══════
        dgvTafsilis = ModernGrid.Create();
        dgvTafsilis.AutoGenerateColumns = false;
        dgvTafsilis.FillColumns();
        dgvTafsilis.UseAiryLook(44);
        dgvTafsilis.UseSelectionAccent(ModernTheme.Primary);
        dgvTafsilis.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgvTafsilis.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", DataPropertyName = "Code", HeaderText = "کد تفصیلی", FillWeight = 12, MinimumWidth = 90, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dgvTafsilis.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", DataPropertyName = "Name", HeaderText = "نام / شرکت", FillWeight = 28, MinimumWidth = 150 });
        dgvTafsilis.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", DataPropertyName = "Type", HeaderText = "نقش", FillWeight = 13, MinimumWidth = 110 });
        dgvTafsilis.Columns.Add(new DataGridViewTextBoxColumn { Name = "NationalCode", DataPropertyName = "NationalCode", HeaderText = "کد ملی", FillWeight = 13, MinimumWidth = 100, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dgvTafsilis.Columns.Add(new DataGridViewTextBoxColumn { Name = "ShebaNumber", DataPropertyName = "ShebaNumber", HeaderText = "شماره شبا", FillWeight = 22, MinimumWidth = 180, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dgvTafsilis.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsActive", DataPropertyName = "IsActive", HeaderText = "وضعیت", FillWeight = 10, MinimumWidth = 90 });
        ModernListKit.PinHeaders(dgvTafsilis);

        dgvTafsilis.UseBadges("IsActive", new Dictionary<string, Color>
        {
            ["فعال"] = ModernTheme.Success,
            ["غیرفعال"] = ModernTheme.Neutral
        });
        dgvTafsilis.UseBadges("Type", new Dictionary<string, Color>
        {
            ["پرسنل"] = ModernTheme.Primary,
            ["پیمانکار"] = ModernTheme.Teal,
            ["کارفرما"] = ModernTheme.Purple,
            ["تأمین‌کننده"] = ModernTheme.Orange,
            ["سایر نقش‌ها"] = ModernTheme.Neutral
        });
        dgvTafsilis.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string col = dgvTafsilis.Columns[e.ColumnIndex].Name;
            if (col == "IsActive" && e.Value is not null)
            {
                e.Value = Convert.ToBoolean(e.Value) ? "فعال" : "غیرفعال";
                e.FormattingApplied = true;
            }
            else if (col == "Name") e.CellStyle.ForeColor = ModernTheme.TextStrong;
            else if (col == "ShebaNumber" || col == "NationalCode") e.CellStyle.ForeColor = ModernTheme.TextMuted;
        };

        // استفاده از CellClick به جای SelectionChanged
        dgvTafsilis.CellClick += DgvTafsilis_CellClick;
        dgvTafsilis.CellDoubleClick += DgvTafsilis_CellClick;

        // ═══════ ۵. دکمه‌ها ═══════
        btnSave = ModernButton.Save("ذخیره / ویرایش", 160, 36);
        btnSave.Click += async (s, e) => await SaveSafeAsync();
        btnNew = ModernButton.Soft("ثبت جدید", ModernTheme.Success, 130, 36);
        btnNew.Click += (s, e) => ResetForm();
        btnDelete = ModernButton.Soft("حذف انتخاب‌شده", ModernTheme.Danger, 160, 36);
        btnDelete.Click += async (s, e) => await DeleteSafeAsync();
        var actions = ModernListKit.BuildActionBar(new Control[] { btnSave, btnNew }, new Control[] { btnDelete });

        ModernListKit.ComposeRows(this, "مدیریت تفصیلی‌های حسابداری", "اشخاص، شرکت‌ها و شماره‌های شبای آن‌ها", new Size(1200, 840),
            (toolbar, 56), (middle, middleHeight), (ModernReport.WrapGrid(dgvTafsilis), 0), (actions, 64));

        cmbPersonType.SelectedIndexChanged += (s, e) => OnPersonTypeChanged();
        cmbPersonType.SelectedIndex = 0;
    }

    private async Task LoadDataAsync()
    {
        if (IsDisposed) return;
        try
        {
            string search = txtSearch.Text.Trim();
            string role = cmbFilterRole.SelectedItem?.ToString() ?? "همه";
            if (role == "همه") role = "";

            var list = await _repository.GetAllAsync(role);

            if (!string.IsNullOrWhiteSpace(search))
            {
                list = list.Where(x =>
                    (x.Name ?? "").Contains(search) ||
                    (x.Code ?? "").Contains(search) ||
                    (x.NationalCode ?? "").Contains(search) ||
                    (x.NationalId ?? "").Contains(search)
                ).ToList();
            }

            _isLoadingData = true;
            dgvTafsilis.DataSource = list;
            _isLoadingData = false;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TafsiliManagementForm.LoadDataAsync");
        }
    }

    private void OnPersonTypeChanged()
    {
        if (cmbPersonType.SelectedItem is null) return;

        string type = cmbPersonType.SelectedItem.ToString() ?? "";

        if (type == "شخص حقیقی")
        {
            txtNationalCode.Enabled = true;
            txtNationalCode.ReadOnly = false;

            txtNationalId.Enabled = false;
            txtNationalId.ReadOnly = true;
            if (!_isLoadingData) txtNationalId.Text = string.Empty;

            txtFirstName.Enabled = true; txtLastName.Enabled = true;
            txtCompanyName.Enabled = false; txtCompanyName.Text = string.Empty;
        }
        else if (type == "شخص حقوقی")
        {
            txtNationalId.Enabled = true;
            txtNationalId.ReadOnly = false;

            txtNationalCode.Enabled = false;
            txtNationalCode.ReadOnly = true;
            if (!_isLoadingData) txtNationalCode.Text = string.Empty;

            txtCompanyName.Enabled = true;
            txtFirstName.Enabled = false; txtFirstName.Text = string.Empty;
            txtLastName.Enabled = false; txtLastName.Text = string.Empty;
        }
    }

    // ✅ متد اختصاصی برای کلیک روی ردیف‌ها
    private async void DgvTafsilis_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _isLoadingData) return;
        if (dgvTafsilis.Rows[e.RowIndex].DataBoundItem is not Tafsili selected) return;

        if (_editingId == selected.Id) return; // جلوگیری از لود مجدد همان رکورد

        _isLoadingData = true;
        try
        {
            _editingId = selected.Id;
            txtCode.Text = selected.Code;
            cmbPersonType.SelectedItem = selected.PersonType;
            txtFirstName.Text = selected.FirstName;
            txtLastName.Text = selected.LastName;
            txtCompanyName.Text = selected.CompanyName;
            txtNationalCode.Text = selected.NationalCode ?? "";
            txtNationalId.Text = selected.NationalId ?? "";
            txtEconomicCode.Text = selected.EconomicCode ?? "";
            cmbRole.SelectedItem = selected.Type;
            chkIsActive.Checked = selected.IsActive;

            OnPersonTypeChanged();
        }
        finally
        {
            _isLoadingData = false;
        }

        try
        {
            _currentBankAccounts = await _repository.GetBankAccountsAsync(selected.Id);
            dgvBankAccounts.DataSource = new BindingSource { DataSource = _currentBankAccounts };
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TafsiliManagementForm.LoadBankAccounts");
        }
    }

    private void ResetForm()
    {
        _isLoadingData = true;
        _editingId = null;
        txtCode.Text = string.Empty;
        cmbPersonType.SelectedIndex = 0;
        txtFirstName.Text = string.Empty;
        txtLastName.Text = string.Empty;
        txtCompanyName.Text = string.Empty;
        txtNationalCode.Text = string.Empty;
        txtNationalId.Text = string.Empty;
        txtEconomicCode.Text = string.Empty;
        cmbRole.SelectedIndex = 0;
        chkIsActive.Checked = true;

        _currentBankAccounts.Clear();
        dgvBankAccounts.DataSource = null;
        dgvBankAccounts.DataSource = new BindingSource { DataSource = _currentBankAccounts };

        if (dgvTafsilis.Rows.Count > 0) dgvTafsilis.ClearSelection();

        OnPersonTypeChanged();
        _isLoadingData = false;
    }

    private async Task SaveSafeAsync()
    {
        if (string.IsNullOrWhiteSpace(txtCode.Text)) { ModernMessageBox.ShowWarning("کد تفصیلی الزامی است."); return; }

        if (cmbPersonType.Text == "شخص حقیقی" && string.IsNullOrWhiteSpace(txtFirstName.Text))
        {
            ModernMessageBox.ShowWarning("نام شخص حقیقی الزامی است."); return;
        }
        if (cmbPersonType.Text == "شخص حقوقی" && string.IsNullOrWhiteSpace(txtCompanyName.Text))
        {
            ModernMessageBox.ShowWarning("نام شرکت الزامی است."); return;
        }

        var t = new Tafsili
        {
            Id = _editingId ?? 0,
            Code = txtCode.Text.Trim(),
            PersonType = cmbPersonType.Text,
            FirstName = txtFirstName.Text.Trim(),
            LastName = txtLastName.Text.Trim(),
            CompanyName = txtCompanyName.Text.Trim(),
            NationalCode = string.IsNullOrWhiteSpace(txtNationalCode.Text) ? null : txtNationalCode.Text.Trim(),
            NationalId = string.IsNullOrWhiteSpace(txtNationalId.Text) ? null : txtNationalId.Text.Trim(),
            EconomicCode = string.IsNullOrWhiteSpace(txtEconomicCode.Text) ? null : txtEconomicCode.Text.Trim(),
            Type = cmbRole.Text,
            IsActive = chkIsActive.Checked
        };

        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_editingId.HasValue)
                {
                    await _repository.UpdateAsync(t);
                    await _repository.SaveBankAccountsAsync(t.Id, _currentBankAccounts);
                }
                else
                {
                    await _repository.AddAsync(t);
                    await _repository.SaveBankAccountsAsync(t.Id, _currentBankAccounts);
                }
            }, "در حال ذخیره...");

            if (success)
            {
                // 👈 نمایش پیام موفقیت با تشخیص ثبت یا ویرایش
                ToastHelper.Show(_editingId.HasValue ? "تغییرات با موفقیت ویرایش شد." : "تفصیلی جدید با موفقیت ثبت شد.", true);
                await LoadDataAsync();
                ResetForm();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TafsiliManagementForm.SaveSafeAsync");
            ModernMessageBox.ShowError(ex.Message);
        }
    }

    private async Task DeleteSafeAsync()
    {
        if (_editingId is null) { ModernMessageBox.ShowWarning("لطفاً یک ردیف را انتخاب کنید."); return; }
        if (!ModernMessageBox.AskConfirm("آیا از حذف این تفصیلی مطمئن هستید؟", "حذف")) return;

        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repository.DeleteAsync(_editingId.Value);
            }, "در حال حذف...");

            if (success)
            {
                ToastHelper.Show("با موفقیت حذف شد.", true);
                await LoadDataAsync();
                ResetForm();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TafsiliManagementForm.DeleteSafeAsync");
            ModernMessageBox.ShowError(ex.Message);
        }
    }

    // --- مدیریت شبا ---
    private void BtnAddSheba_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtSheba.Text)) return;

        string cleanSheba = BankHelper.NormalizeSheba(txtSheba.Text);
        if (cleanSheba.Length != 24) { ModernMessageBox.ShowWarning("شماره شبا باید 24 رقم باشد."); return; }

        string fullSheba = "IR" + cleanSheba;
        if (_currentBankAccounts.Any(a => a.ShebaNumber == fullSheba)) { ModernMessageBox.ShowWarning("این شبا قبلاً اضافه شده است."); return; }

        _currentBankAccounts.Add(new TafsiliBankAccount
        {
            ShebaNumber = fullSheba,
            BankName = BankHelper.GetBankNameFromSheba(cleanSheba),
            IsMainAccount = _currentBankAccounts.Count == 0
        });

        dgvBankAccounts.DataSource = null;
        dgvBankAccounts.DataSource = new BindingSource { DataSource = _currentBankAccounts };
        txtSheba.Text = string.Empty;
        txtSheba.Focus();
    }

    private void BtnDeleteSheba_Click(object? sender, EventArgs e)
    {
        if (dgvBankAccounts.CurrentRow?.DataBoundItem is not TafsiliBankAccount selected) return;
        _currentBankAccounts.Remove(selected);
        dgvBankAccounts.DataSource = null;
        dgvBankAccounts.DataSource = new BindingSource { DataSource = _currentBankAccounts };
    }
}
