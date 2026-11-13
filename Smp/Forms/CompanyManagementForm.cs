using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Models;
using SMP.Helpers;
using Microsoft.Data.SqlClient;

namespace SMP.Forms
{
    public class CompanyManagementForm : Form
    {
        private readonly CompanyRepository _repository;
        private int? _editingId = null;
        private bool _logoChanged = false;
        private byte[]? _originalLogoBytes = null;

        private DataGridView dgv = null!;
        private TextBox txtName = null!, txtNationalId = null!, txtEconomicCode = null!;
        private TextBox txtAddress = null!, txtPostalCode = null!, txtPhone = null!, txtWorkshopCode = null!;
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
                try { picLogo?.Image?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var (formLayout, grid, bottomPanel) = StandardFormHelper.SetupMasterDetailLayout(
                this, "مدیریت شرکت‌ها", new Size(1200, 750));

            dgv = grid;
            dgv.AutoGenerateColumns = false; // ✅ تعریف دستی ستون‌ها
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", DataPropertyName = "Name", HeaderText = "نام شرکت", FillWeight = 25 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "WorkshopCode", DataPropertyName = "WorkshopCode", HeaderText = "کد کارگاه", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "NationalId", DataPropertyName = "NationalId", HeaderText = "شناسه ملی", FillWeight = 20 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EconomicCode", DataPropertyName = "EconomicCode", HeaderText = "کد اقتصادی", FillWeight = 20 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Phone", DataPropertyName = "Phone", HeaderText = "تلفن", FillWeight = 20 });

            dgv.CellClick += Dgv_CellClick;

            // فیلدها
            var flpLogo = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(0) };
            picLogo = new PictureBox { Width = 100, Height = 100, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = UIHelper.WarmInputBackground, Margin = new Padding(0, 0, 0, 5) };
            btnBrowseLogo = UIHelper.CreateButton("انتخاب آرم", UIHelper.WarmSecondary, 100, 30);
            btnBrowseLogo.Click += BtnBrowseLogo_Click;
            flpLogo.Controls.Add(picLogo);
            flpLogo.Controls.Add(btnBrowseLogo);

            txtName = new TextBox(); txtNationalId = new TextBox(); txtEconomicCode = new TextBox();
            txtPhone = new TextBox(); txtPostalCode = new TextBox(); txtWorkshopCode = new TextBox();
            txtAddress = new TextBox();

            // چیدمان فیلدها
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "نام شرکت:", txtName, 0, 0, 1);
            UIHelper.AddField(formLayout, "شناسه ملی:", txtNationalId, 0, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "کد اقتصادی:", txtEconomicCode, 1, 0, 1);
            UIHelper.AddField(formLayout, "کد کارگاه:", txtWorkshopCode, 1, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "تلفن:", txtPhone, 2, 0, 1);
            UIHelper.AddField(formLayout, "کد پستی:", txtPostalCode, 2, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "آدرس:", txtAddress, 3, 0, 1);
            formLayout.SetColumnSpan(txtAddress, 3);

            StandardFormHelper.AddRow(formLayout, 110F);
            formLayout.Controls.Add(flpLogo, 0, 4);
            formLayout.SetColumnSpan(flpLogo, 4);

            // دکمه‌ها
            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.WarmPrimary, 100, 42);
            btnDelete = UIHelper.CreateButton("🗑️ حذف", UIHelper.WarmDanger, 100, 42);
            btnClear = UIHelper.CreateButton("➕ جدید", UIHelper.WarmSecondary, 100, 42);

            btnSave.Click += BtnSave_Click;
            btnDelete.Click += BtnDelete_Click;
            btnClear.Click += BtnClear_Click;
            btnDelete.Enabled = false;

            var btnContainer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            btnContainer.Controls.Add(btnSave);
            btnContainer.Controls.Add(btnDelete);
            btnContainer.Controls.Add(btnClear);
            bottomPanel.Controls.Add(btnContainer);
        }

        private async Task LoadDataAsync()
        {
            var (success, list) = await AsyncHelper.RunWithLoading(this, () => Task.Run(() => _repository.GetAll()), "در حال بارگذاری لیست شرکت‌ها...");
            if (!success || list == null) return;

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
                if (existing == null)
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

            if (_logoChanged && picLogo.Image != null)
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
            txtName.Clear(); txtNationalId.Clear(); txtEconomicCode.Clear();
            txtPhone.Clear(); txtPostalCode.Clear(); txtWorkshopCode.Clear(); txtAddress.Clear();
            var oldImage = picLogo.Image; picLogo.Image = null; oldImage?.Dispose();
            btnDelete.Enabled = false; txtName.Focus();
        }
    }
}