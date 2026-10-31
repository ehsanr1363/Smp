using System;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;

namespace PersonnelApp.Forms.Controls
{
    public class ucEmployeePersonal : UserControl
    {
        private TextBox txtPersonnelCode = null!, txtFirstName = null!, txtLastName = null!, txtFatherName = null!, txtNationalCode = null!;
        private TextBox txtBirthCertificate = null!, txtBirthPlace = null!, txtIssuancePlace = null!, txtAddress = null!;
        private TextBox txtBirthDate = null!, txtHireDate = null!, txtInsuranceNumber = null!, txtSpecializedField = null!;
        private TextBox txtPhoneNumber = null!;
        private ComboBox cmbGender = null!, cmbMaritalStatus = null!, cmbMilitaryStatus = null!, cmbEducation = null!;
        private NumericUpDown numChildrenCount = null!;
        private CheckBox chkIsActive = null!;
        private PictureBox picPhoto = null!;
        private Button btnBrowsePhoto = null!;

        private bool _photoChanged = false;

        public ucEmployeePersonal()
        {
            this.Dock = DockStyle.Fill;
            this.RightToLeft = RightToLeft.Yes;
            BuildUI();
            LoadCombos();
            DateHelper.ApplyDateMask(txtBirthDate);
            DateHelper.ApplyDateMask(txtHireDate);

            cmbGender.SelectedIndexChanged += (s, e) => ToggleMilitaryStatus();
        }

        public void LoadData(Employee? e)
        {
            if (e == null)
            {
                txtBirthDate.Text = DateHelper.ToPersianDate(DateTime.Today.AddYears(-30));
                txtHireDate.Text = DateHelper.ToPersianDate(DateTime.Today);
                chkIsActive.Checked = true;
                return;
            }

            txtPersonnelCode.Text = e.PersonnelCode ?? "";
            txtFirstName.Text = e.FirstName ?? "";
            txtLastName.Text = e.LastName ?? "";
            txtFatherName.Text = e.FatherName ?? "";
            txtNationalCode.Text = e.NationalCode ?? "";
            txtBirthCertificate.Text = e.BirthCertificateNumber ?? "";
            txtBirthPlace.Text = e.BirthPlace ?? "";
            txtIssuancePlace.Text = e.IssuancePlace ?? "";
            txtSpecializedField.Text = e.SpecializedField ?? "";
            txtInsuranceNumber.Text = e.InsuranceNumber ?? "";
            txtPhoneNumber.Text = e.PhoneNumber ?? "";
            txtAddress.Text = e.Address ?? "";
            txtBirthDate.Text = DateHelper.ToPersianDate(e.BirthDate);
            txtHireDate.Text = DateHelper.ToPersianDate(e.HireDate);
            chkIsActive.Checked = e.IsActive;
            numChildrenCount.Value = Math.Max(0, Math.Min(e.ChildrenCount, (int)numChildrenCount.Maximum));

            SelectComboByText(cmbGender, e.Gender);
            SelectComboByText(cmbMaritalStatus, e.MaritalStatus);
            SelectComboByText(cmbMilitaryStatus, e.MilitaryStatus);
            SelectComboByText(cmbEducation, e.Education);

            picPhoto.Image = ImageHelper.ByteArrayToImage(e.Photo);
            _photoChanged = false;
            ToggleMilitaryStatus();
        }

        public bool SaveData(Employee emp)
        {
            if (string.IsNullOrWhiteSpace(txtPersonnelCode.Text))
            {
                ModernMessageBox.ShowWarning("کد پرسنلی را وارد کنید.");
                txtPersonnelCode.Focus();
                return false;
            }

            if (!ValidationHelper.IsValidIranianNationalCode(txtNationalCode.Text))
            {
                ModernMessageBox.ShowWarning("کد ملی وارد شده معتبر نیست!");
                txtNationalCode.Focus();
                return false;
            }

            DateTime? birthDate = DateHelper.ParsePersianDate(txtBirthDate.Text);
            if (birthDate == null)
            {
                ModernMessageBox.ShowWarning("تاریخ تولد وارد شده معتبر نیست! (مثال: 1370/01/01)");
                txtBirthDate.Focus();
                return false;
            }

            emp.PersonnelCode = txtPersonnelCode.Text.Trim();
            emp.FirstName = txtFirstName.Text.Trim();
            emp.LastName = txtLastName.Text.Trim();
            emp.FatherName = txtFatherName.Text.Trim();
            emp.NationalCode = txtNationalCode.Text.Trim();
            emp.BirthCertificateNumber = txtBirthCertificate.Text.Trim();
            emp.BirthDate = birthDate.Value;
            emp.BirthPlace = txtBirthPlace.Text.Trim();
            emp.IssuancePlace = txtIssuancePlace.Text.Trim();
            emp.Gender = cmbGender.Text;
            emp.MaritalStatus = cmbMaritalStatus.Text;
            emp.MilitaryStatus = cmbMilitaryStatus.Text;
            emp.Education = cmbEducation.Text;
            emp.SpecializedField = txtSpecializedField.Text.Trim();
            emp.ChildrenCount = (int)numChildrenCount.Value;
            emp.InsuranceNumber = txtInsuranceNumber.Text.Trim();
            emp.PhoneNumber = txtPhoneNumber.Text.Trim();
            emp.Address = txtAddress.Text.Trim();
            emp.HireDate = DateHelper.ParsePersianDate(txtHireDate.Text);
            emp.IsActive = chkIsActive.Checked;

            if (_photoChanged && picPhoto.Image != null)
            {
                using var bmp = new Bitmap(picPhoto.Image);
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                emp.Photo = ms.ToArray();
            }

            return true;
        }

        private void BuildUI()
        {
            var tlp1 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 12, Padding = new Padding(15), AutoScroll = true };
            tlp1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f));
            tlp1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
            tlp1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f));
            tlp1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
            tlp1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
            for (int i = 0; i < 12; i++) tlp1.RowStyles.Add(new RowStyle(SizeType.Absolute, 45f));

            var pnlPhoto = new Panel { Dock = DockStyle.Fill };
            picPhoto = new PictureBox { Size = new Size(130, 160), Location = new Point(15, 5), BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom, BackColor = UIHelper.WarmInputBackground };
            btnBrowsePhoto = UIHelper.CreateButton("انتخاب عکس", UIHelper.WarmSecondary, 130, 35);
            btnBrowsePhoto.Location = new Point(15, 175);
            btnBrowsePhoto.Click += BtnBrowsePhoto_Click;
            pnlPhoto.Controls.Add(picPhoto);
            pnlPhoto.Controls.Add(btnBrowsePhoto);
            tlp1.Controls.Add(pnlPhoto, 4, 0);
            tlp1.SetRowSpan(pnlPhoto, 6);

            chkIsActive = new CheckBox { Text = "کارمند فعال است", AutoSize = true, Anchor = AnchorStyles.Right };
            UIHelper.StyleToggle(chkIsActive);
            tlp1.Controls.Add(chkIsActive, 4, 6);
            tlp1.SetRowSpan(chkIsActive, 3);

            UIHelper.AddField(tlp1, "کد پرسنلی:", txtPersonnelCode = new TextBox(), row: 0, labelCol: 0, controlCol: 1);
            UIHelper.AddField(tlp1, "نام:", txtFirstName = new TextBox(), row: 1, labelCol: 0, controlCol: 1);
            UIHelper.AddField(tlp1, "نام خانوادگی:", txtLastName = new TextBox(), row: 1, labelCol: 2, controlCol: 3);
            UIHelper.AddField(tlp1, "نام پدر:", txtFatherName = new TextBox(), row: 2, labelCol: 0, controlCol: 1);
            UIHelper.AddField(tlp1, "کد ملی:", txtNationalCode = new TextBox(), row: 2, labelCol: 2, controlCol: 3);
            UIHelper.AddField(tlp1, "شماره شناسنامه:", txtBirthCertificate = new TextBox(), row: 3, labelCol: 0, controlCol: 1);
            UIHelper.AddField(tlp1, "محل تولد:", txtBirthPlace = new TextBox(), row: 3, labelCol: 2, controlCol: 3);
            UIHelper.AddField(tlp1, "محل صدور:", txtIssuancePlace = new TextBox(), row: 4, labelCol: 0, controlCol: 1);
            UIHelper.AddField(tlp1, "تاریخ تولد:", txtBirthDate = new TextBox(), row: 4, labelCol: 2, controlCol: 3);
            UIHelper.AddField(tlp1, "تاریخ استخدام:", txtHireDate = new TextBox(), row: 5, labelCol: 0, controlCol: 1);
            UIHelper.AddField(tlp1, "تاهل:", cmbMaritalStatus = new ComboBox(), row: 5, labelCol: 2, controlCol: 3);

            UIHelper.AddField(tlp1, "جنسیت:", cmbGender = new ComboBox(), row: 6, labelCol: 0, controlCol: 1);
            UIHelper.AddField(tlp1, "تحصیلات:", cmbEducation = new ComboBox(), row: 6, labelCol: 2, controlCol: 3);

            UIHelper.AddField(tlp1, "رشته تخصصی:", txtSpecializedField = new TextBox(), row: 7, labelCol: 0, controlCol: 1);
            UIHelper.AddField(tlp1, "تعداد فرزند:", numChildrenCount = new NumericUpDown { Minimum = 0, Maximum = 50 }, row: 7, labelCol: 2, controlCol: 3);
            UIHelper.AddField(tlp1, "شماره بیمه:", txtInsuranceNumber = new TextBox(), row: 8, labelCol: 0, controlCol: 1);
            UIHelper.AddField(tlp1, "تلفن:", txtPhoneNumber = new TextBox(), row: 8, labelCol: 2, controlCol: 3);
            UIHelper.AddField(tlp1, "نظام وظیفه:", cmbMilitaryStatus = new ComboBox(), row: 9, labelCol: 0, controlCol: 1);

            Label lblAddress = new Label { Text = "آدرس:", AutoSize = true, Anchor = AnchorStyles.Right };
            UIHelper.StyleLabel(lblAddress);
            txtAddress = new TextBox { Multiline = false, Dock = DockStyle.Fill, RightToLeft = RightToLeft.No, TextAlign = HorizontalAlignment.Right };
            txtAddress.BackColor = UIHelper.WarmInputBackground;
            txtAddress.BorderStyle = BorderStyle.FixedSingle;
            txtAddress.Font = UIHelper.PersianFont(13f);
            tlp1.Controls.Add(lblAddress, 0, 10);
            tlp1.Controls.Add(txtAddress, 1, 10);
            tlp1.SetColumnSpan(txtAddress, 3);

            var flpExcelBtnsPersonal = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
            var btnDownloadPersonnelTemplate = UIHelper.CreateButton("📥 دانلود قالب اکسل", UIHelper.WarmSecondary, 170, 42);
            btnDownloadPersonnelTemplate.Click += (s, ev) => ExcelTemplateGenerator.GeneratePersonnelTemplate();
            var btnImportPersonnelExcel = UIHelper.CreateButton("📤 ایمپورت از اکسل", UIHelper.WarmSecondary, 170, 42);
            btnImportPersonnelExcel.Click += BtnImportPersonnelExcel_Click;
            flpExcelBtnsPersonal.Controls.Add(btnImportPersonnelExcel);
            flpExcelBtnsPersonal.Controls.Add(btnDownloadPersonnelTemplate);
            tlp1.Controls.Add(flpExcelBtnsPersonal, 0, 11);
            tlp1.SetColumnSpan(flpExcelBtnsPersonal, 5);

            this.Controls.Add(tlp1);
        }

        private void LoadCombos()
        {
            cmbGender.Items.AddRange(new object[] { "مرد", "زن" });
            cmbMaritalStatus.Items.AddRange(new object[] { "مجرد", "متاهل" });
            cmbMilitaryStatus.Items.AddRange(new object[] { "مشمول", "معافیت", "پایان خدمت" });
            cmbEducation.Items.AddRange(new object[] { "زیر دیپلم", "دیپلم", "کاردانی", "کارشناسی", "کارشناسی ارشد", "دکتری" });
        }

        private void SelectComboByText(ComboBox cmb, string? text)
        {
            if (cmb == null || cmb.Items.Count == 0) return;
            if (string.IsNullOrWhiteSpace(text)) { cmb.SelectedIndex = -1; return; }
            string cleanText = StringHelper.NormalizePersianText(text);
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                string itemText = StringHelper.NormalizePersianText(cmb.GetItemText(cmb.Items[i]));
                if (itemText == cleanText || itemText.Contains(cleanText) || cleanText.Contains(itemText)) { cmb.SelectedIndex = i; return; }
            }
            cmb.SelectedIndex = -1;
        }

        private void ToggleMilitaryStatus()
        {
            string selectedGender = cmbGender.Text.Trim();
            if (selectedGender == "زن")
            {
                cmbMilitaryStatus.SelectedIndex = -1;
                cmbMilitaryStatus.Enabled = false;
                cmbMilitaryStatus.Text = "";
            }
            else
            {
                cmbMilitaryStatus.Enabled = true;
            }
        }

        private void BtnBrowsePhoto_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    using var stream = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read);
                    using var img = Image.FromStream(stream);
                    var oldImage = picPhoto.Image;
                    picPhoto.Image = new Bitmap(img);
                    oldImage?.Dispose();
                    _photoChanged = true;
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "ucEmployeePersonal.BrowsePhoto");
                    ModernMessageBox.ShowError("خطا در بارگذاری تصویر:\n" + ex.Message);
                }
            }
        }

        // ==========================================================
        // 📤 ایمپورت از اکسل + نمایش پیش‌نمایش و ذخیره
        // ==========================================================
        private async void BtnImportPersonnelExcel_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل جامع پرسنل" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            DataTable? dt = null;
            bool success = await AsyncHelper.RunWithLoading(this.FindForm()!, async () =>
            {
                dt = await ExcelImportHelper.ParseEmployeesExcelAsync(ofd.FileName);
            }, "در حال خواندن فایل اکسل...");

            if (!success || dt == null || dt.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("داده‌ای در فایل اکسل یافت نشد.");
                return;
            }

            ShowEmployeesPreviewForm(dt);
        }

        private void ShowEmployeesPreviewForm(DataTable dt)
        {
            var previewForm = new Form();
            UIHelper.SetupForm(previewForm, "پیش‌نمایش اطلاعات پرسنل ایمپورت‌شده", new Size(1400, 700));
            previewForm.ShowInTaskbar = false;

            var dgvPreview = new DataGridView { Dock = DockStyle.Fill, DataSource = dt };
            dgvPreview.ApplyModernStyle();

            // رنگ‌آمیزی ردیف‌ها بر اساس وضعیت
            dgvPreview.DataBindingComplete += (s, e) => {
                foreach (DataGridViewRow row in dgvPreview.Rows)
                {
                    var statusVal = row.Cells["وضعیت"].Value?.ToString();
                    if (statusVal != "معتبر")
                    {
                        row.DefaultCellStyle.BackColor = UIHelper.WarmErrorBackground;
                        row.DefaultCellStyle.ForeColor = UIHelper.WarmErrorText;
                    }
                    else
                    {
                        row.DefaultCellStyle.BackColor = UIHelper.WarmSuccessBackground;
                    }
                }
            };

            var btnSave = UIHelper.CreateButton("انتقال اطلاعات معتبر به دیتابیس", UIHelper.WarmPrimary, 300, 50);
            btnSave.Dock = DockStyle.Bottom;

            btnSave.Click += async (s, ev) =>
            {
                var validRows = dt.Rows.Cast<DataRow>().Where(r => r["وضعیت"].ToString() == "معتبر").CopyToDataTable();

                if (validRows.Rows.Count == 0)
                {
                    ModernMessageBox.ShowWarning("هیچ ردیف معتبری برای ثبت وجود ندارد.");
                    return;
                }

                if (!ModernMessageBox.AskConfirm($"آیا از ثبت {NumberHelper.FormatThousands(validRows.Rows.Count, usePersianDigits: true)} پرسنل معتبر مطمئن هستید؟", "تایید ایمپورت"))
                    return;

                string msg = "";
                bool saveSuccess = await AsyncHelper.RunWithLoading(previewForm, async () =>
                {
                    msg = await ExcelImportHelper.SaveEmployeesToDbAsync(AppConfig.ConnectionString, validRows);
                }, "در حال ثبت پرسنل در دیتابیس...");

                if (saveSuccess)
                {
                    ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                    ToastHelper.Show("ایمپورت با موفقیت انجام شد.", isSuccess: true);
                    previewForm.Close();

                    // رفرش کردن لیست پرسنل در فرم اصلی (اگر باز باشد)
                    if (this.FindForm() is EmployeeEditForm parentForm)
                    {
                        // اگر نیاز به رفرش لیست بود اینجا انجام شود
                    }
                }
            };

            previewForm.Controls.Add(dgvPreview);
            previewForm.Controls.Add(btnSave);
            previewForm.ShowDialog(this);
        }
    }
}