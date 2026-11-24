using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Models;
using Smp.Helpers;
using Microsoft.Data.SqlClient;

namespace Smp.Forms;

public class CompanyManagementForm : Form
{
    private readonly CompanyRepository _repository;
    private int? _editingId = null;
    private bool _logoChanged = false;
    private byte[]? _originalLogoBytes = null;

    private DataGridView dgv = null!;
    private ModernTextBox txtName = null!, txtNationalId = null!, txtEconomicCode = null!;
    private ModernTextBox txtAddress = null!, txtPostalCode = null!, txtPhone = null!, txtWorkshopCode = null!;
    private PictureBox picLogo = null!;
    private Button btnBrowseLogo = null!, btnSave = null!, btnDelete = null!, btnClear = null!;

    public CompanyManagementForm(string connectionString)
    {
        _repository = new CompanyRepository(connectionString);

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave, btnClear);

        Shown += async (s, e) => await LoadDataAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { picLogo?.Image?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CompanyManagementForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── جدول ──
        dgv = ModernGrid.Create();
        dgv.AutoGenerateColumns = false;
        dgv.FillColumns();
        dgv.UseAiryLook(46);
        dgv.UseSelectionAccent(ModernTheme.Primary);
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", DataPropertyName = "Name", HeaderText = "نام شرکت", FillWeight = 30, MinimumWidth = 170 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "WorkshopCode", DataPropertyName = "WorkshopCode", HeaderText = "کد کارگاه", FillWeight = 14, MinimumWidth = 100, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "NationalId", DataPropertyName = "NationalId", HeaderText = "شناسه ملی", FillWeight = 18, MinimumWidth = 110, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EconomicCode", DataPropertyName = "EconomicCode", HeaderText = "کد اقتصادی", FillWeight = 18, MinimumWidth = 110, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Phone", DataPropertyName = "Phone", HeaderText = "تلفن", FillWeight = 16, MinimumWidth = 100, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        ModernListKit.PinHeaders(dgv);   // جلوگیری از جایگزینی «نام شرکت» با «نام / شرکت»
        dgv.CellClick += Dgv_CellClick;

        // ── فیلدها ──
        txtName = ModernUI.Input(); txtNationalId = ModernUI.Input(); txtEconomicCode = ModernUI.Input();
        txtPhone = ModernUI.Input(); txtPostalCode = ModernUI.Input(); txtWorkshopCode = ModernUI.Input();
        txtAddress = ModernUI.Input();

        int rh = ModernUI.RowHeight;
        var info = new ModernCard("مشخصات شرکت", ModernTheme.Primary, 2, rh, rh, rh, rh);
        info.Body.Controls.Add(ModernUI.Row("نام شرکت", txtName), 0, 0);
        info.Body.Controls.Add(ModernUI.Row("شناسه ملی", txtNationalId), 1, 0);
        info.Body.Controls.Add(ModernUI.Row("کد اقتصادی", txtEconomicCode), 0, 1);
        info.Body.Controls.Add(ModernUI.Row("کد کارگاه", txtWorkshopCode), 1, 1);
        info.Body.Controls.Add(ModernUI.Row("تلفن", txtPhone), 0, 2);
        info.Body.Controls.Add(ModernUI.Row("کد پستی", txtPostalCode), 1, 2);
        var addressRow = ModernUI.Row("آدرس", txtAddress);
        info.Body.Controls.Add(addressRow, 0, 3);
        info.Body.SetColumnSpan(addressRow, 2);

        // ── کارت آرم ──
        picLogo = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = ModernTheme.InputFill, Margin = new Padding(0, 0, 0, 6) };
        btnBrowseLogo = ModernButton.Soft("انتخاب آرم", ModernTheme.Teal, 120, 34);
        btnBrowseLogo.Anchor = AnchorStyles.None;
        btnBrowseLogo.Click += BtnBrowseLogo_Click;
        var logoLayout = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
        logoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        logoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        logoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        logoLayout.Controls.Add(picLogo, 0, 0);
        logoLayout.Controls.Add(btnBrowseLogo, 0, 1);
        var logoCard = new ModernCard("آرم شرکت", ModernTheme.Teal, new[] { new ColumnStyle(SizeType.Percent, 100F) });
        logoCard.Body.Controls.Add(logoLayout, 0, 0);

        var top = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = Padding.Empty };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));    // ستون ۰ = راست: مشخصات
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));   // آرم
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        info.Dock = DockStyle.Fill; info.Margin = new Padding(0, 0, 6, 0);
        logoCard.Dock = DockStyle.Fill; logoCard.Margin = new Padding(6, 0, 0, 0);
        top.Controls.Add(info, 0, 0);
        top.Controls.Add(logoCard, 1, 0);

        // ── دکمه‌ها ──
        btnSave = ModernButton.Save("ذخیره", 120, 36);
        btnClear = ModernButton.Soft("جدید", ModernTheme.Success, 110, 36);
        btnDelete = ModernButton.Soft("حذف", ModernTheme.Danger, 110, 36);

        btnSave.Click += BtnSave_Click;
        btnDelete.Click += BtnDelete_Click;
        btnClear.Click += BtnClear_Click;
        btnDelete.Enabled = false;

        var actions = ModernListKit.BuildActionBar(new Control[] { btnSave, btnClear }, new Control[] { btnDelete });

        ModernListKit.ComposeRows(this, "مدیریت شرکت‌ها", "اطلاعات ثبتی شرکت‌ها و آرم چاپ‌شده روی گزارش‌ها", new Size(1200, 750),
            (top, info.Height + 12), (ModernReport.WrapGrid(dgv), 0), (actions, 64));
    }

    private async Task LoadDataAsync()
    {
        var (success, list) = await AsyncHelper.RunWithLoading(this, () => Task.Run(() => _repository.GetAll()), "در حال بارگذاری لیست شرکت‌ها...");
        if (!success || list is null) return;

        if (_editingId.HasValue && !list.Any(c => c.Id == _editingId.Value)) BtnClear_Click(null, EventArgs.Empty);

        dgv.DataSource = list;
    }

    private void BtnBrowseLogo_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" };
        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                using var stream = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read);
                using var img = Image.FromStream(stream);
                var oldImage = picLogo.Image;
                picLogo.Image = new Bitmap(img);
                oldImage?.Dispose();
                _logoChanged = true;
            }
            catch (Exception ex) { ModernMessageBox.ShowError("خطا در بارگذاری تصویر:\n" + ex.Message); }
        }
    }

    private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || dgv.Rows[e.RowIndex].DataBoundItem is not Company comp) return;

        _editingId = comp.Id;
        txtName.Text = comp.Name;
        txtNationalId.Text = comp.NationalId ?? "";
        txtEconomicCode.Text = comp.EconomicCode ?? "";
        txtPhone.Text = comp.Phone ?? "";
        txtPostalCode.Text = comp.PostalCode ?? "";
        txtWorkshopCode.Text = comp.WorkshopCode ?? "";
        txtAddress.Text = comp.Address ?? "";

        _originalLogoBytes = comp.Logo;
        var oldImage = picLogo.Image;
        picLogo.Image = ImageHelper.ByteArrayToImage(comp.Logo);
        oldImage?.Dispose();

        _logoChanged = false;
        btnDelete.Enabled = true;
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text)) { ModernMessageBox.ShowWarning("نام شرکت الزامی است."); return; }

        if (_editingId.HasValue)
        {
            Company? existing = null;
            await AsyncHelper.RunWithLoading(this, async () => { existing = await Task.Run(() => _repository.GetById(_editingId.Value)); }, "در حال بررسی رکورد...");
            if (existing is null)
            {
                ModernMessageBox.ShowWarning("این شرکت حذف شده است. فرم بازنشانی می‌شود.");
                BtnClear_Click(null, EventArgs.Empty); await LoadDataAsync(); return;
            }
        }

        var comp = new Company
        {
            Id = _editingId ?? 0,
            Name = txtName.Text.Trim(),
            NationalId = txtNationalId.Text.Trim(),
            EconomicCode = txtEconomicCode.Text.Trim(),
            Phone = txtPhone.Text.Trim(),
            PostalCode = txtPostalCode.Text.Trim(),
            WorkshopCode = txtWorkshopCode.Text.Trim(),
            Address = txtAddress.Text.Trim()
        };

        if (_logoChanged && picLogo.Image is not null)
        {
            using var bmp = new Bitmap(picLogo.Image); using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png); comp.Logo = ms.ToArray();
        }
        else comp.Logo = _originalLogoBytes;

        bool success = await AsyncHelper.RunWithLoading(this, async () => {
            if (_editingId.HasValue) await Task.Run(() => _repository.Update(comp));
            else await Task.Run(() => _repository.Add(comp));
        }, "در حال ذخیره...");

        if (success) { ToastHelper.Show("ذخیره شد.", true); BtnClear_Click(null, EventArgs.Empty); await LoadDataAsync(); }
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (!_editingId.HasValue) return;
        bool success = await AsyncHelper.RunWithLoading(this, async () => {
            int empCount = await Task.Run(() => Convert.ToInt32(DbHelper.ExecuteScalar(AppConfig.ConnectionString, "SELECT COUNT(1) FROM Employees WHERE CompanyId = @id", new SqlParameter("@id", _editingId.Value))));
            if (empCount > 0) throw new InvalidOperationException($"این شرکت دارای {empCount} کارمند است و قابل حذف نیست.");
            await Task.Run(() => _repository.Delete(_editingId.Value));
        }, "در حال حذف...");

        if (success) { ToastHelper.Show("حذف شد.", true); BtnClear_Click(null, EventArgs.Empty); await LoadDataAsync(); }
    }

    private void BtnClear_Click(object? sender, EventArgs e)
    {
        _editingId = null; _logoChanged = false; _originalLogoBytes = null;
        txtName.Text = string.Empty; txtNationalId.Text = string.Empty; txtEconomicCode.Text = string.Empty;
        txtPhone.Text = string.Empty; txtPostalCode.Text = string.Empty; txtWorkshopCode.Text = string.Empty; txtAddress.Text = string.Empty;
        var oldImage = picLogo.Image; picLogo.Image = null; oldImage?.Dispose();
        btnDelete.Enabled = false; txtName.Focus();
    }
}
