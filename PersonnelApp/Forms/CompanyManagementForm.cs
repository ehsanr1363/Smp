using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Data;
using PersonnelApp.Models;
using PersonnelApp.Helpers;
using Microsoft.Data.SqlClient;

namespace PersonnelApp.Forms
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
            ShowInTaskbar = false;

            Shown += async (s, e) => await LoadDataAsync();
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, "مدیریت شرکت‌ها", new Size(1300, 720));
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            dgv = new DataGridView { Dock = DockStyle.Fill };
            dgv.ApplyModernStyle();

            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "نام شرکت", MinimumWidth = 150 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "WorkshopCode", HeaderText = "کد کارگاه", MinimumWidth = 90 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NationalId", HeaderText = "شناسه ملی", MinimumWidth = 110 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EconomicCode", HeaderText = "کد اقتصادی", MinimumWidth = 110 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Phone", HeaderText = "تلفن", MinimumWidth = 100 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PostalCode", HeaderText = "کد پستی", MinimumWidth = 100 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Address", HeaderText = "آدرس", MinimumWidth = 250 });

            dgv.CellClick += Dgv_CellClick;
            mainLayout.Controls.Add(dgv, 0, 0);

            var formPanel = UIHelper.CreateWarmPanel();
            formPanel.Margin = new Padding(10, 0, 0, 0);

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            formLayout.RowCount = 9;
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170F));
            for (int i = 1; i <= 7; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));

            var flpLogo = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Anchor = AnchorStyles.Top | AnchorStyles.Right, AutoSize = true };

            picLogo = new PictureBox { Width = 120, Height = 120, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = UIHelper.WarmInputBackground, Margin = new Padding(0, 0, 0, 5) };
            flpLogo.Controls.Add(picLogo);

            btnBrowseLogo = UIHelper.CreateButton("انتخاب آرم", UIHelper.WarmSecondary, 120, 35);
            btnBrowseLogo.Click += BtnBrowseLogo_Click;
            flpLogo.Controls.Add(btnBrowseLogo);

            formLayout.SetColumnSpan(flpLogo, 2);
            formLayout.Controls.Add(flpLogo, 0, 0);

            txtName = new TextBox();
            txtNationalId = new TextBox();
            txtEconomicCode = new TextBox();
            txtPhone = new TextBox();
            txtPostalCode = new TextBox();
            txtWorkshopCode = new TextBox();
            txtAddress = new TextBox { Multiline = true, Height = 60, ScrollBars = ScrollBars.Vertical };

            UIHelper.AddField(formLayout, "نام شرکت:", txtName, 1);
            UIHelper.AddField(formLayout, "شناسه ملی:", txtNationalId, 2);
            UIHelper.AddField(formLayout, "کد اقتصادی:", txtEconomicCode, 3);
            UIHelper.AddField(formLayout, "تلفن:", txtPhone, 4);
            UIHelper.AddField(formLayout, "کد پستی:", txtPostalCode, 5);
            UIHelper.AddField(formLayout, "کد کارگاه:", txtWorkshopCode, 6);
            UIHelper.AddField(formLayout, "آدرس:", txtAddress, 7);

            var flpBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 15, 0, 0) };

            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.WarmPrimary, 100, 45);
            btnDelete = UIHelper.CreateButton("🗑️ حذف", UIHelper.WarmDanger, 100, 45);
            btnClear = UIHelper.CreateButton("➕ جدید", UIHelper.WarmSecondary, 100, 45);

            btnSave.Click += BtnSave_Click;
            btnDelete.Click += BtnDelete_Click;
            btnClear.Click += BtnClear_Click;

            btnDelete.Enabled = false;

            flpBtns.Controls.Add(btnSave);
            flpBtns.Controls.Add(btnDelete);
            flpBtns.Controls.Add(btnClear);
            formLayout.SetColumnSpan(flpBtns, 2);
            formLayout.Controls.Add(flpBtns, 0, 8);

            formPanel.Controls.Add(formLayout);
            mainLayout.Controls.Add(formPanel, 1, 0);

            Controls.Add(mainLayout);
        }

        private async Task LoadDataAsync()
        {
            var (success, list) = await AsyncHelper.RunWithLoading(
                this,
                () => Task.Run(() => _repository.GetAll()),
                "در حال بارگذاری لیست شرکت‌ها...");

            if (!success || list == null) return;
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
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "CompanyManagementForm.BrowseLogo");
                    ModernMessageBox.ShowError("خطا در بارگذاری تصویر:\n" + ex.Message);
                }
            }
        }

        private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dgv.Rows[e.RowIndex].DataBoundItem is Company comp)
            {
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
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                ModernMessageBox.ShowWarning("نام شرکت الزامی است.");
                txtName.Focus();
                return;
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
                using var bmp = new Bitmap(picLogo.Image);
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                comp.Logo = ms.ToArray();
            }
            else
            {
                comp.Logo = _originalLogoBytes;
            }

            if (_editingId.HasValue) DateStampHelper.StampUpdated(comp);
            else DateStampHelper.StampCreated(comp);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_editingId.HasValue)
                    await Task.Run(() => _repository.Update(comp));
                else
                    await Task.Run(() => _repository.Add(comp));
            }, "در حال ذخیره اطلاعات...");

            if (success)
            {
                ToastHelper.Show("اطلاعات با موفقیت ذخیره شد.", isSuccess: true);
                BtnClear_Click(null, EventArgs.Empty);
                await LoadDataAsync();
            }
        }

        // ✅ FIX P1: چک FK قبل از حذف
        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (!_editingId.HasValue) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                using var conn = new SqlConnection(AppConfig.ConnectionString);
                await conn.OpenAsync();

                // بررسی ارجاعات به این شرکت در جدول Employees
                using (var checkCmd = new SqlCommand("SELECT COUNT(1) FROM Employees WHERE CompanyId = @id", conn))
                {
                    checkCmd.Parameters.AddWithValue("@id", _editingId.Value);
                    int empCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                    if (empCount > 0)
                    {
                        throw new InvalidOperationException(
                            $"این شرکت دارای {empCount} کارمند است و قابل حذف نیست.\n" +
                            "ابتدا کارمندان را به شرکت دیگری منتقل کنید یا حذف نمایید.");
                    }
                }

                await Task.Run(() => _repository.Delete(_editingId.Value));
            }, "در حال بررسی و حذف شرکت...");

            if (success)
            {
                ToastHelper.Show("شرکت حذف شد.", isSuccess: true);
                BtnClear_Click(null, EventArgs.Empty);
                await LoadDataAsync();
            }
        }

        private void BtnClear_Click(object? sender, EventArgs e)
        {
            _editingId = null;
            _logoChanged = false;
            _originalLogoBytes = null;

            txtName.Clear();
            txtNationalId.Clear();
            txtEconomicCode.Clear();
            txtPhone.Clear();
            txtPostalCode.Clear();
            txtWorkshopCode.Clear();
            txtAddress.Clear();

            var oldImage = picLogo.Image;
            picLogo.Image = null;
            oldImage?.Dispose();

            btnDelete.Enabled = false;
            txtName.Focus();
        }
    }
}