using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;

namespace Smp.Forms.Controls;

public class ucEmployeeDependents : UserControl
{
    private readonly DependentService _dependentService;
    private readonly List<Dependent> _dependents = new();
    private int? _editingDependentId = null;

    private DataGridView dgvDependents = null!;
    private TextBox txtDepFirstName = null!, txtDepLastName = null!, txtDepNationalCode = null!, txtDepBirthCertificate = null!, txtDepFatherName = null!, txtDepBirthPlace = null!, txtDepBirthDate = null!;
    private ComboBox cmbDepGender = null!, cmbDepRelation = null!;
    private CheckBox chkDepInsurance = null!;
    private Label lblDepAge = null!;
    private Button btnAddDependent = null!, btnDeleteDependent = null!;

    // ✅ FIX: متغیر کنترلی برای جلوگیری از پر شدن خودکار فرم هنگام لود اولیه
    private bool _isLoading = false;

    public ucEmployeeDependents(DependentService dependentService)
    {
        this.Dock = DockStyle.Fill;
        this.RightToLeft = RightToLeft.Yes;

        _dependentService = dependentService;
        BuildUI();
        DateHelper.ApplyDateMask(txtDepBirthDate);

        txtDepBirthDate.TextChanged += TxtDepBirthDate_TextChanged;
        dgvDependents.CellFormatting += DgvDependents_CellFormatting;
        dgvDependents.SelectionChanged += DgvDependents_SelectionChanged;

        btnAddDependent.Click += BtnAddDependent_Click;
        btnDeleteDependent.Click += BtnDeleteDependent_Click;
    }

    public async Task LoadDataAsync(int? employeeId)
    {
        _isLoading = true; // ✅ قفل کردن رویداد انتخاب
        try
        {
            _dependents.Clear();
            if (employeeId.HasValue && employeeId.Value > 0)
            {
                var list = await _dependentService.GetByEmployeeIdAsync(employeeId.Value);
                _dependents.AddRange(list);
            }
            dgvDependents.DataSource = new BindingList<Dependent>(_dependents);

            // پاک کردن فرم بعد از اتصال داده‌ها
            ClearForm();
        }
        finally
        {
            _isLoading = false; // ✅ باز کردن قفل
        }
    }

    public async Task<bool> SaveDataAsync(int employeeId)
    {
        if (employeeId <= 0) return true;
        if (!_dependents.Any(d => d.Id <= 0)) return true;

        foreach (var dep in _dependents.Where(d => d.Id <= 0))
        {
            dep.Id = 0;
            dep.EmployeeId = employeeId;
            try
            {
                await _dependentService.AddAsync(dep);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ucEmployeeDependents.SaveData");
                ModernMessageBox.ShowError($"خطا در ذخیره تحت تکفل {dep.FirstName}:\n{ex.Message}");
                return false;
            }
        }
        return true;
    }

    private void BuildUI()
    {
        const int F = ModernUI.FieldHeight;

        // ─── جدول ───
        dgvDependents = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        dgvDependents.ApplyModernStyle();
        dgvDependents.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvDependents.UseAiryLook(44);
        dgvDependents.UseSelectionAccent();
        dgvDependents.UseBadges("Relation", new Dictionary<string, Color>
        {
            ["فرزند"] = ModernTheme.Teal, ["همسر"] = ModernTheme.Purple, ["پدر"] = ModernTheme.Primary, ["مادر"] = ModernTheme.Orange
        });
        dgvDependents.UseBadges("Gender", new Dictionary<string, Color> { ["مرد"] = ModernTheme.Primary, ["زن"] = ModernTheme.Purple });

        dgvDependents.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FirstName", HeaderText = "نام", FillWeight = 11 });
        dgvDependents.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastName", HeaderText = "نام خانوادگی", FillWeight = 13 });
        dgvDependents.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NationalCode", HeaderText = "کد ملی", FillWeight = 12 });
        dgvDependents.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BirthCertificateNumber", HeaderText = "شماره شناسنامه", FillWeight = 13 });
        dgvDependents.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FatherName", HeaderText = "نام پدر", FillWeight = 11 });
        dgvDependents.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Relation", HeaderText = "نسبت", FillWeight = 8 });
        dgvDependents.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Gender", HeaderText = "جنسیت", FillWeight = 8 });
        dgvDependents.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BirthDate", HeaderText = "تاریخ تولد", FillWeight = 11 });
        dgvDependents.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BirthPlace", HeaderText = "محل تولد", FillWeight = 10 });
        dgvDependents.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "HasSupplementaryInsurance", HeaderText = "بیمه تکمیلی", FillWeight = 10 });

        var gridCard = new ModernCard("👨‍👩‍👧  فهرست افراد تحت تکفل", ModernTheme.Purple, 1);
        gridCard.Margin = new Padding(0, 0, 0, 12);
        gridCard.Body.Controls.Add(dgvDependents, 0, 0);

        // ─── ورودی‌ها ───
        txtDepFirstName = new TextBox();
        txtDepLastName = new TextBox();
        txtDepNationalCode = new TextBox { TextAlign = HorizontalAlignment.Center };
        txtDepBirthCertificate = new TextBox { TextAlign = HorizontalAlignment.Center };
        txtDepFatherName = new TextBox();
        txtDepBirthPlace = new TextBox();
        txtDepBirthDate = new TextBox { TextAlign = HorizontalAlignment.Center };

        cmbDepGender = ModernUI.Combo();
        cmbDepGender.Items.AddRange(new object[] { "مرد", "زن" });
        cmbDepRelation = ModernUI.Combo();
        cmbDepRelation.Items.AddRange(new object[] { "فرزند", "همسر", "پدر", "مادر" });

        chkDepInsurance = ModernUI.Check("بیمه تکمیلی", ModernTheme.Purple);
        chkDepInsurance.Width = 180;

        lblDepAge = new Label
        {
            Text = "",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(12f, FontStyle.Bold),
            ForeColor = ModernTheme.Primary,
            BackColor = Color.Transparent
        };

        var inputCard = new ModernCard("✏️  ثبت / ویرایش فرد تحت تکفل", ModernTheme.Orange, 4, F, F, F);
        var tb = inputCard.Body;
        tb.Controls.Add(ModernUI.Field("نام", txtDepFirstName), 0, 0);
        tb.Controls.Add(ModernUI.Field("نام خانوادگی", txtDepLastName), 1, 0);
        tb.Controls.Add(ModernUI.Field("کد ملی", txtDepNationalCode), 2, 0);
        tb.Controls.Add(ModernUI.Field("شماره شناسنامه", txtDepBirthCertificate), 3, 0);

        tb.Controls.Add(ModernUI.Field("نام پدر", txtDepFatherName), 0, 1);
        tb.Controls.Add(ModernUI.Field("محل تولد", txtDepBirthPlace), 1, 1);
        tb.Controls.Add(ModernUI.Field("تاریخ تولد", txtDepBirthDate), 2, 1);
        tb.Controls.Add(ModernUI.Field("سن", lblDepAge), 3, 1);

        tb.Controls.Add(ModernUI.Field("جنسیت", cmbDepGender), 0, 2);
        tb.Controls.Add(ModernUI.Field("نسبت", cmbDepRelation), 1, 2);
        var cellIns = ModernUI.CenterInCell(chkDepInsurance);
        tb.Controls.Add(cellIns, 2, 2);
        tb.SetColumnSpan(cellIns, 2);

        // ─── دکمه‌ها ───
        var flpBtns = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = Color.Transparent
        };
        btnAddDependent = ModernButton.Create("افزودن / ذخیره", ModernTheme.Success, 190, 44);
        btnDeleteDependent = ModernButton.Create("🗑  حذف", ModernTheme.Danger, 130, 44, ModernButtonStyle.Soft);
        var btnImportDependentExcel = ModernButton.Create("📤  ایمپورت از اکسل", ModernTheme.Success, 175, 44, ModernButtonStyle.Soft);
        btnImportDependentExcel.Click += BtnImportDependentExcel_Click;
        var btnDownloadDependentTemplate = ModernButton.Create("📥  دانلود قالب اکسل", ModernTheme.Neutral, 175, 44, ModernButtonStyle.Outline);
        btnDownloadDependentTemplate.Click += (s, ev) => ExcelTemplateGenerator.GenerateDependentsTemplate();

        flpBtns.Controls.Add(btnAddDependent);
        flpBtns.Controls.Add(btnDeleteDependent);
        flpBtns.Controls.Add(btnImportDependentExcel);
        flpBtns.Controls.Add(btnDownloadDependentTemplate);

        // ─── چیدمان کلی ───
        var mainDepLayout = new BufferedTable
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20, 14, 20, 6),
            BackColor = ModernTheme.Background
        };
        mainDepLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        mainDepLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        mainDepLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, inputCard.Height + 12));
        mainDepLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62f));

        inputCard.Dock = DockStyle.Fill;
        inputCard.Margin = new Padding(0, 0, 0, 12);
        gridCard.Dock = DockStyle.Fill;

        mainDepLayout.Controls.Add(gridCard, 0, 0);
        mainDepLayout.Controls.Add(inputCard, 0, 1);
        mainDepLayout.Controls.Add(flpBtns, 0, 2);

        this.Controls.Add(mainDepLayout);
    }

    private void ClearForm()
    {
        _editingDependentId = null;
        btnAddDependent.Text = "افزودن";
        txtDepFirstName.Clear(); txtDepLastName.Clear(); txtDepNationalCode.Clear();
        txtDepBirthCertificate.Clear(); txtDepFatherName.Clear(); txtDepBirthPlace.Clear(); txtDepBirthDate.Clear();
        cmbDepGender.SelectedIndex = -1; cmbDepRelation.SelectedIndex = -1;
        chkDepInsurance.Checked = false;
        lblDepAge.Text = "";

        // پاک کردن انتخاب گرید تا ردیفی هایلایت نباشد
        if (dgvDependents.Rows.Count > 0)
        {
            dgvDependents.ClearSelection();
            dgvDependents.CurrentCell = null;
        }
    }

    private void TxtDepBirthDate_TextChanged(object? sender, EventArgs e)
    {
        var parsedDate = DateHelper.ParsePersianDate(txtDepBirthDate.Text);
        if (parsedDate.HasValue)
        {
            var today = DateTime.Today;
            var age = today.Year - parsedDate.Value.Year;
            if (parsedDate.Value.Date > today.AddYears(-age)) age--;
            lblDepAge.Text = age < 0 ? "تاریخ نامعتبر" : $"{StringHelper.ConvertToPersianDigits(age.ToString())} سال";
        }
        else lblDepAge.Text = "";
    }

    private void DgvDependents_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        string colName = dgvDependents.Columns[e.ColumnIndex].DataPropertyName;

        if (colName == "BirthDate")
        {
            if (e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
            else if (e.Value is null || e.Value == DBNull.Value) { e.Value = ""; e.FormattingApplied = true; }
        }
        // ✅ FIX: تبدیل کدهای عددی جنسیت به متن (1=مرد، 2=زن)
        else if (colName == "Gender")
        {
            string genderStr = e.Value?.ToString() ?? "";
            e.Value = StringHelper.GenderToPersian(genderStr);
            e.FormattingApplied = true;
        }
    }

    private void DgvDependents_SelectionChanged(object? sender, EventArgs e)
    {
        // ✅ FIX: اگر در حال لود اولیه هستیم، فرم را پر نکن
        if (_isLoading) return;

        if (dgvDependents.CurrentRow?.DataBoundItem is not Dependent dep) return;

        _editingDependentId = dep.Id;
        txtDepFirstName.Text = dep.FirstName ?? "";
        txtDepLastName.Text = dep.LastName ?? "";
        txtDepNationalCode.Text = dep.NationalCode ?? "";
        txtDepBirthCertificate.Text = dep.BirthCertificateNumber ?? "";
        txtDepFatherName.Text = dep.FatherName ?? "";
        txtDepBirthPlace.Text = dep.BirthPlace ?? "";
        txtDepBirthDate.Text = DateHelper.ToPersianDate(dep.BirthDate);

        // تبدیل کد عددی به متن برای نمایش در ComboBox
        cmbDepGender.Text = StringHelper.GenderToPersian(dep.Gender);
        cmbDepRelation.Text = dep.Relation ?? "";

        chkDepInsurance.Checked = dep.HasSupplementaryInsurance;
        btnAddDependent.Text = "ذخیره تغییرات";
    }

    private async void BtnAddDependent_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtDepFirstName.Text) || string.IsNullOrWhiteSpace(txtDepLastName.Text))
        {
            ModernMessageBox.ShowWarning("نام و نام خانوادگی الزامی است.");
            return;
        }

        string depNationalCode = StringHelper.ConvertToEnglishDigits(txtDepNationalCode.Text).Trim();
        if (!string.IsNullOrWhiteSpace(depNationalCode) && !ValidationHelper.IsValidIranianNationalCode(depNationalCode))
        {
            ModernMessageBox.ShowWarning("کد ملی وارد شده برای تحت تکفل معتبر نیست!");
            return;
        }

        DateTime? depBirthDate = DateHelper.ParsePersianDate(txtDepBirthDate.Text);
        if (depBirthDate is null && !string.IsNullOrWhiteSpace(txtDepBirthDate.Text))
        {
            ModernMessageBox.ShowWarning("تاریخ تولد تحت تکفل معتبر نیست.");
            return;
        }

        int parentFormEmpId = GetParentEmployeeId();

        var dep = new Dependent
        {
            Id = _editingDependentId ?? 0,
            EmployeeId = parentFormEmpId,
            FirstName = txtDepFirstName.Text.Trim(),
            LastName = txtDepLastName.Text.Trim(),
            NationalCode = depNationalCode,
            BirthCertificateNumber = txtDepBirthCertificate.Text.Trim(),
            FatherName = txtDepFatherName.Text.Trim(),
            BirthDate = depBirthDate,
            Gender = cmbDepGender.Text,
            BirthPlace = txtDepBirthPlace.Text.Trim(),
            Relation = cmbDepRelation.Text,
            HasSupplementaryInsurance = chkDepInsurance.Checked
        };

        bool isEditing = _editingDependentId.HasValue && _editingDependentId.Value > 0;

        if (parentFormEmpId > 0)
        {
            bool success = await AsyncHelper.RunWithLoading(this.FindForm()!, async () =>
            {
                if (isEditing) await _dependentService.UpdateAsync(dep);
                else await _dependentService.AddAsync(dep);
            }, "در حال ذخیره اطلاعات...");

            if (success) await LoadDataAsync(parentFormEmpId);
        }
        else
        {
            if (isEditing)
            {
                var existing = _dependents.FirstOrDefault(x => x.Id == dep.Id);
                if (existing is not null) _dependents[_dependents.IndexOf(existing)] = dep;
            }
            else
            {
                dep.Id = -_dependents.Count - 1;
                _dependents.Add(dep);
            }
            _isLoading = true;
            dgvDependents.DataSource = new BindingList<Dependent>(_dependents);
            ClearForm();
            _isLoading = false;
        }
    }

    private async void BtnDeleteDependent_Click(object? sender, EventArgs e)
    {
        if (dgvDependents.CurrentRow?.DataBoundItem is not Dependent dep)
        {
            ModernMessageBox.ShowWarning("لطفاً ابتدا یک فرد تحت تکفل را از جدول انتخاب کنید.");
            return;
        }

        if (!ModernMessageBox.AskConfirm($"آیا از حذف «{dep.FirstName} {dep.LastName}» مطمئن هستید؟", "حذف تحت تکفل")) return;

        int parentFormEmpId = GetParentEmployeeId();
        if (parentFormEmpId > 0 && dep.Id > 0)
        {
            bool success = await AsyncHelper.RunWithLoading(this.FindForm()!, async () =>
            {
                await _dependentService.DeleteAsync(dep.Id);
            }, "در حال حذف...");

            if (success) await LoadDataAsync(parentFormEmpId);
        }
        else
        {
            _dependents.Remove(dep);
            _isLoading = true;
            dgvDependents.DataSource = new BindingList<Dependent>(_dependents);
            ClearForm();
            _isLoading = false;
        }
    }

    private int GetParentEmployeeId()
    {
        if (this.ParentForm is EmployeeEditForm parentForm)
            return parentForm.CurrentEmployeeId;
        return 0;
    }

    private async void BtnImportDependentExcel_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Excel Files|*.xlsx;*.xls",
            Title = "انتخاب فایل اکسل افراد تحت تکفل"
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        DataTable? dt = null;
        bool success = await AsyncHelper.RunWithLoading(this.FindForm()!, async () =>
        {
            dt = await ExcelImportHelper.ParseDependentsExcelAsync(ofd.FileName);
        }, "در حال خواندن فایل اکسل...");

        if (!success || dt is null)
        {
            ModernMessageBox.ShowWarning("خطا در خواندن فایل اکسل.");
            return;
        }

        if (dt.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("داده‌ای در فایل اکسل یافت نشد.");
            return;
        }

        ShowDependentsPreviewForm(dt);
    }

    private void ShowDependentsPreviewForm(DataTable dt)
    {
        var previewForm = new Form();
        ModernForm.Setup(previewForm, "پیش‌نمایش افراد تحت تکفل ایمپورت‌شده", new Size(1250, 650), isDialog: false);
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

        var btnSave = ModernButton.Create("انتقال ردیف‌های معتبر به دیتابیس", ModernTheme.Brand, 300, 50);
        btnSave.Dock = DockStyle.Bottom;

        btnSave.Click += async (s, ev) =>
        {
            var validRows = dt.Rows.Cast<DataRow>()
                .Where(r => r["وضعیت"].ToString() == "معتبر")
                .CopyToDataTable();

            if (validRows.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("هیچ ردیف معتبری برای ثبت وجود ندارد.");
                return;
            }

            if (!ModernMessageBox.AskConfirm(
                $"آیا از ثبت {NumberHelper.FormatThousands(validRows.Rows.Count, usePersianDigits: true)} ردیف معتبر مطمئن هستید؟",
                "تایید ایمپورت"))
                return;

            string msg = "";
            bool saveSuccess = await AsyncHelper.RunWithLoading(previewForm, async () =>
            {
                msg = await ExcelImportHelper.SaveDependentsToDbAsync(validRows);
            }, "در حال ثبت در دیتابیس...");

            if (saveSuccess)
            {
                ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                ToastHelper.Show("ایمپورت با موفقیت انجام شد.", isSuccess: true);
                previewForm.Close();

                int parentEmpId = GetParentEmployeeId();
                if (parentEmpId > 0) await LoadDataAsync(parentEmpId);
            }
        };

        previewForm.Controls.Add(dgvPreview);
        previewForm.Controls.Add(btnSave);
        previewForm.ShowDialog(this);
    }
}
