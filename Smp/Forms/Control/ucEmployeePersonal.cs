using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;

namespace Smp.Forms.Controls;

public class ucEmployeePersonal : UserControl
{
    private TextBox txtPersonnelCode = null!, txtFirstName = null!, txtLastName = null!, txtFatherName = null!, txtNationalCode = null!;
    private TextBox txtBirthCertificate = null!, txtBirthPlace = null!, txtIssuancePlace = null!, txtAddress = null!;
    private TextBox txtBirthDate = null!, txtHireDate = null!, txtInsuranceNumber = null!, txtSpecializedField = null!;
    private TextBox txtPhoneNumber = null!;
    private ComboBox cmbGender = null!, cmbMaritalStatus = null!, cmbMilitaryStatus = null!, cmbEducation = null!;
    private NumericUpDown numChildrenCount = null!;
    private CheckBox chkIsActive = null!;

    // 👈 فیلد جدید برای وضعیت بیمه
    private CheckBox chkHasInsurance = null!;

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { picPhoto?.Image?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ucEmployeePersonal"); }
        }
        base.Dispose(disposing);
    }

    public void LoadData(Employee? e)
    {
        if (e is null)
        {
            txtBirthDate.Text = DateHelper.ToPersianDate(DateTime.Today.AddYears(-30));
            txtHireDate.Text = DateHelper.ToPersianDate(DateTime.Today);
            chkIsActive.Checked = true;
            chkHasInsurance.Checked = true; // پیش‌فرض برای پرسنل جدید
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

        // 👈 بارگذاری وضعیت بیمه
        chkHasInsurance.Checked = e.HasInsurance;

        numChildrenCount.Value = Math.Max(0, Math.Min(e.ChildrenCount, (int)numChildrenCount.Maximum));

        SelectComboByText(cmbGender, e.Gender);
        SelectComboByText(cmbMaritalStatus, e.MaritalStatus);
        SelectComboByText(cmbMilitaryStatus, e.MilitaryStatus);
        SelectComboByText(cmbEducation, e.Education);

        var oldImage = picPhoto.Image;
        picPhoto.Image = ImageHelper.ByteArrayToImage(e.Photo);
        oldImage?.Dispose();

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

        // کد ملی اختیاری است (مثلاً اتباع خارجی)؛ فقط اگر پر شده باشد اعتبارسنجی می‌شود
        if (!string.IsNullOrWhiteSpace(txtNationalCode.Text) && !ValidationHelper.IsValidIranianNationalCode(txtNationalCode.Text))
        {
            ModernMessageBox.ShowWarning("کد ملی وارد شده معتبر نیست!");
            txtNationalCode.Focus();
            return false;
        }

        DateTime? birthDate = DateHelper.ParsePersianDate(txtBirthDate.Text);
        if (birthDate is null)
        {
            ModernMessageBox.ShowWarning("تاریخ تولد وارد شده معتبر نیست! (مثال: 1370/01/01)");
            txtBirthDate.Focus();
            return false;
        }

        emp.PersonnelCode = txtPersonnelCode.Text.Trim();
        emp.FirstName = txtFirstName.Text.Trim();
        emp.LastName = txtLastName.Text.Trim();
        emp.FatherName = txtFatherName.Text.Trim();
        emp.NationalCode = string.IsNullOrWhiteSpace(txtNationalCode.Text) ? null : txtNationalCode.Text.Trim();
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

        // 👈 ذخیره وضعیت بیمه
        emp.HasInsurance = chkHasInsurance.Checked;

        if (_photoChanged && picPhoto.Image is not null)
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
        const int F = ModernUI.FieldHeight;
        var page = new ModernPage();

        // ─── ورودی‌ها ───
        txtPersonnelCode = new TextBox { TextAlign = HorizontalAlignment.Center };
        txtFirstName = new TextBox();
        txtLastName = new TextBox();
        txtFatherName = new TextBox();
        txtNationalCode = new TextBox { TextAlign = HorizontalAlignment.Center };
        txtBirthCertificate = new TextBox { TextAlign = HorizontalAlignment.Center };
        txtBirthPlace = new TextBox();
        txtIssuancePlace = new TextBox();
        txtBirthDate = new TextBox { TextAlign = HorizontalAlignment.Center };
        txtHireDate = new TextBox { TextAlign = HorizontalAlignment.Center };
        txtSpecializedField = new TextBox();
        txtInsuranceNumber = new TextBox { TextAlign = HorizontalAlignment.Center };
        txtPhoneNumber = new TextBox { TextAlign = HorizontalAlignment.Center };
        txtAddress = new TextBox();
        numChildrenCount = new NumericUpDown { Minimum = 0, Maximum = 50 };

        cmbGender = ModernUI.Combo();
        cmbMaritalStatus = ModernUI.Combo();
        cmbEducation = ModernUI.Combo();
        cmbMilitaryStatus = ModernUI.Combo();

        // ─── کارت ۱: مشخصات هویتی (۴ ستون فیلد + ستون عکس) ───
        var cols = new[]
        {
            new ColumnStyle(SizeType.Percent, 25f), new ColumnStyle(SizeType.Percent, 25f),
            new ColumnStyle(SizeType.Percent, 25f), new ColumnStyle(SizeType.Percent, 25f),
            new ColumnStyle(SizeType.Absolute, 190f)
        };
        var cardId = new ModernCard("🪪  مشخصات هویتی", ModernTheme.Primary, cols, F, F, F, F, F);
        var body = cardId.Body;

        body.Controls.Add(ModernUI.Field("کد پرسنلی", txtPersonnelCode), 0, 0);
        body.Controls.Add(ModernUI.Field("نام", txtFirstName), 1, 0);
        body.Controls.Add(ModernUI.Field("نام خانوادگی", txtLastName), 2, 0);
        body.Controls.Add(ModernUI.Field("نام پدر", txtFatherName), 3, 0);

        body.Controls.Add(ModernUI.Field("کد ملی (اختیاری)", txtNationalCode), 0, 1);
        body.Controls.Add(ModernUI.Field("شماره شناسنامه", txtBirthCertificate), 1, 1);
        body.Controls.Add(ModernUI.Field("محل تولد", txtBirthPlace), 2, 1);
        body.Controls.Add(ModernUI.Field("محل صدور", txtIssuancePlace), 3, 1);

        body.Controls.Add(ModernUI.Field("تاریخ تولد", txtBirthDate), 0, 2);
        body.Controls.Add(ModernUI.Field("تاریخ استخدام", txtHireDate), 1, 2);
        body.Controls.Add(ModernUI.Field("جنسیت", cmbGender), 2, 2);
        body.Controls.Add(ModernUI.Field("وضعیت تاهل", cmbMaritalStatus), 3, 2);

        body.Controls.Add(ModernUI.Field("نظام وظیفه", cmbMilitaryStatus), 0, 3);
        body.Controls.Add(ModernUI.Field("تحصیلات", cmbEducation), 1, 3);
        body.Controls.Add(ModernUI.Field("رشته تخصصی", txtSpecializedField), 2, 3);
        body.Controls.Add(ModernUI.Field("تعداد فرزند", numChildrenCount), 3, 3);

        body.Controls.Add(ModernUI.Field("شماره بیمه", txtInsuranceNumber), 0, 4);
        body.Controls.Add(ModernUI.Field("تلفن", txtPhoneNumber), 1, 4);
        var fldAddress = ModernUI.Field("آدرس", txtAddress);
        body.Controls.Add(fldAddress, 2, 4);
        body.SetColumnSpan(fldAddress, 2);

        // عکس پرسنلی
        picPhoto = new PictureBox
        {
            Size = new Size(130, 160),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = ModernTheme.InputFill,
            Anchor = AnchorStyles.None
        };
        btnBrowsePhoto = ModernButton.Create("📷  انتخاب عکس", ModernTheme.Primary, 150, 38, ModernButtonStyle.Soft);
        btnBrowsePhoto.Anchor = AnchorStyles.None;
        btnBrowsePhoto.Click += BtnBrowsePhoto_Click;

        var pnlPhoto = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(8, 0, 0, 0) };
        pnlPhoto.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pnlPhoto.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        pnlPhoto.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
        pnlPhoto.Controls.Add(picPhoto, 0, 0);
        pnlPhoto.Controls.Add(btnBrowsePhoto, 0, 1);
        body.Controls.Add(pnlPhoto, 4, 0);
        body.SetRowSpan(pnlPhoto, 5);

        page.AddCard(cardId);

        // ─── کارت ۲: وضعیت پرسنل و ابزار اکسل ───
        var cols2 = new[]
        {
            new ColumnStyle(SizeType.Absolute, 210f),
            new ColumnStyle(SizeType.Absolute, 310f),
            new ColumnStyle(SizeType.Percent, 100f)
        };
        var cardState = new ModernCard("⚙️  وضعیت پرسنل و ابزارها", ModernTheme.Teal, cols2, 56);

        chkIsActive = ModernUI.Check("کارمند فعال است", ModernTheme.Success);
        chkIsActive.Width = 190;
        chkHasInsurance = ModernUI.Check("بیمه رد می‌شود (تامین اجتماعی)", ModernTheme.Primary);
        chkHasInsurance.Width = 290;
        chkHasInsurance.Font = ModernTheme.GetFont(11f, FontStyle.Bold);
        chkHasInsurance.ForeColor = ModernTheme.Primary;

        var flpExcelBtnsPersonal = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 8, 0, 0)
        };
        var btnImportPersonnelExcel = ModernButton.Create("📤  ایمپورت از اکسل", ModernTheme.Success, 175, 40, ModernButtonStyle.Soft);
        btnImportPersonnelExcel.Click += BtnImportPersonnelExcel_Click;
        var btnDownloadPersonnelTemplate = ModernButton.Create("📥  دانلود قالب اکسل", ModernTheme.Neutral, 175, 40, ModernButtonStyle.Outline);
        btnDownloadPersonnelTemplate.Click += (s, ev) => ExcelTemplateGenerator.GeneratePersonnelTemplate();
        flpExcelBtnsPersonal.Controls.Add(btnImportPersonnelExcel);
        flpExcelBtnsPersonal.Controls.Add(btnDownloadPersonnelTemplate);

        cardState.Body.Controls.Add(ModernUI.CenterInCell(chkIsActive), 0, 0);
        cardState.Body.Controls.Add(ModernUI.CenterInCell(chkHasInsurance), 1, 0);
        cardState.Body.Controls.Add(flpExcelBtnsPersonal, 2, 0);
        page.AddCard(cardState);

        this.Controls.Add(page);
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
        if (cmb is null || cmb.Items.Count == 0) return;
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

    private async void BtnImportPersonnelExcel_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل جامع پرسنل" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        DataTable? dt = null;
        bool success = await AsyncHelper.RunWithLoading(this.FindForm()!, async () =>
        {
            dt = await ExcelImportHelper.ParseEmployeesExcelAsync(ofd.FileName);
        }, "در حال خواندن فایل اکسل...");

        if (!success || dt is null || dt.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("داده‌ای در فایل اکسل یافت نشد.");
            return;
        }

        ShowEmployeesPreviewForm(dt);
    }

    private void ShowEmployeesPreviewForm(DataTable dt)
    {
        var previewForm = new Form();
        ModernForm.Setup(previewForm, "پیش‌نمایش اطلاعات پرسنل ایمپورت‌شده", new Size(1400, 700), isDialog: false);
        previewForm.ShowInTaskbar = false;

        var dgvPreview = new DataGridView { Dock = DockStyle.Fill, DataSource = dt };
        dgvPreview.ApplyModernStyle();

        // ظاهر مدرن: وضعیت هر ردیف به‌صورت بج رنگی؛ فقط ردیف‌های نامعتبر کم‌رنگ قرمز می‌شوند
        dgvPreview.UseAiryLook(44);
        dgvPreview.UseBadges("وضعیت", new Dictionary<string, Color> { ["معتبر"] = ModernTheme.Success }, ModernTheme.Danger);
        dgvPreview.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.CellStyle is null) return;
            var status = dgvPreview.Rows[e.RowIndex].Cells["وضعیت"].Value?.ToString();
            if (status != "معتبر") e.CellStyle.ForeColor = ModernTheme.DangerText;
        };

        var btnSave = ModernButton.Create("انتقال اطلاعات معتبر به دیتابیس", ModernTheme.Brand, 300, 50);
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
            }
        };

        previewForm.Controls.Add(dgvPreview);
        previewForm.Controls.Add(btnSave);
        previewForm.ShowDialog(this);
    }
}
