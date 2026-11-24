using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;
using Smp.Forms.Controls;

namespace Smp.Forms;

public partial class EmployeeEditForm : Form
{
    private readonly EmployeeService? _employeeService;
    private Employee? _employee;

    private ModernTabHost tabs = null!;
    private ucEmployeePersonal ucPersonal = null!;
    private ucEmployeeJob ucJob = null!;
    private ucEmployeeSalary ucSalary = null!;
    private ucEmployeeDependents ucDependents = null!;
    private ucEmployeeBankAccounts ucBankAccounts = null!;

    private ModernButton btnSave = null!, btnCancel = null!, btnManageContract = null!;
    private ModernButton btnPrev = null!, btnNext = null!;
    private Label lblStep = null!;

    public int CurrentEmployeeId => _employee?.Id ?? 0;

    public EmployeeEditForm() : this("مدیریت کارمند")
    {
    }

    // عنوان در هدر استاندارد فرم ساخته می‌شود، پس باید قبل از BuildUI مشخص باشد
    private EmployeeEditForm(string title)
    {
        BuildUI(title);
        ShowInTaskbar = false;
    }

    public EmployeeEditForm(EmployeeService employeeService, DepartmentService departmentService,
                            LookupService lookupService, Employee? employee = null)
        : this(employee is not null ? "ویرایش کارمند" : "افزودن کارمند")
    {
        _employeeService = employeeService;
        _employee = employee;

        var companyService = new CompanyService(new CompanyRepository(AppConfig.ConnectionString));
        var dependentService = new DependentService(new DependentRepository(AppConfig.ConnectionString));
        var bankAccountRepo = new EmployeeBankAccountRepository(AppConfig.ConnectionString);

        ucPersonal = new ucEmployeePersonal();
        ucJob = new ucEmployeeJob(companyService, departmentService, lookupService);
        ucSalary = new ucEmployeeSalary();
        ucDependents = new ucEmployeeDependents(dependentService);
        ucBankAccounts = new ucEmployeeBankAccounts(bankAccountRepo);

        ucPersonal.Dock = DockStyle.Fill;
        ucJob.Dock = DockStyle.Fill;
        ucSalary.Dock = DockStyle.Fill;
        ucDependents.Dock = DockStyle.Fill;
        ucBankAccounts.Dock = DockStyle.Fill;

        tabs.Pages[0].Controls.Add(ucPersonal);
        tabs.Pages[1].Controls.Add(ucJob);
        tabs.Pages[2].Controls.Add(ucSalary);
        tabs.Pages[3].Controls.Add(ucDependents);
        tabs.Pages[4].Controls.Add(ucBankAccounts);

        // با ورود به تب «قرارداد و حقوق»، آیتم‌ها متناسب با نوع پرسنلیِ انتخاب‌شده در تب شغلی (روزمزد/ساعتی/قراردادی) نمایش داده شود
        tabs.SelectedIndexChanged += (s, ev) =>
        {
            if (tabs.SelectedIndex == 2) ucSalary.SetPersonnelType(ucJob.CurrentPersonnelType);
        };

        this.Shown += async (s, ev) => await InitializeDataAsync(employee);
    }

    private async Task InitializeDataAsync(Employee? employee)
    {
        await AsyncHelper.RunWithLoading(this, async () =>
        {
            ucPersonal.LoadData(employee);
            await ucJob.LoadCombosAsync();

            int year = employee?.ContractYear ?? new System.Globalization.PersianCalendar().GetYear(DateTime.Now);
            var settingsRepo = new SalarySettingsRepository(AppConfig.ConnectionString);
            var settings = await settingsRepo.GetByYearAsync(year);

            if (employee is not null)
            {
                ucJob.LoadData(employee);

                // ✅ FIX: خواندن مستقیم اطلاعات حقوق از آخرین قرارداد فعال کارمند
                var contractRepo = new EmployeeContractRepository(AppConfig.ConnectionString);
                var activeContract = await contractRepo.GetActiveContractByEmployeeIdAsync(employee.Id);

                if (activeContract is not null)
                {
                    employee.BaseSalary = activeContract.BaseSalary;
                    employee.YearsBonus = activeContract.YearsBonus;
                    employee.AttractionAllowance = activeContract.AttractionAllowance;
                    employee.ResponsibilityAllowance = activeContract.ResponsibilityAllowance;
                    employee.ManagementAllowance = activeContract.ManagementAllowance;
                    employee.TechnicalAllowance = activeContract.TechnicalAllowance;
                    employee.TransportationAllowance = activeContract.TransportationAllowance;
                    employee.OtherAllowances = activeContract.OtherAllowances;
                    employee.DailyWage = activeContract.DailyWage;
                    employee.HourlyWage = activeContract.HourlyWage;
                }

                ucSalary.LoadData(employee, settings);
                await ucDependents.LoadDataAsync(employee.Id);
                await ucBankAccounts.LoadDataAsync(employee.Id);
                btnManageContract.Enabled = true;
            }
            else
            {
                ucSalary.LoadData(new Employee { ContractYear = year }, settings);
                await ucDependents.LoadDataAsync(null);
                await ucBankAccounts.LoadDataAsync(null);
                btnManageContract.Enabled = false;
            }
        }, "در حال بارگذاری اطلاعات فرم...");
    }

    private void BuildUI(string title)
    {
        // ───────────── تب‌های مدرن ─────────────
        tabs = new ModernTabHost { Dock = DockStyle.Fill, AccentColor = ModernTheme.Primary };
        tabs.AddPage("👤  مشخصات فردی");
        tabs.AddPage("🏢  اطلاعات شغلی");
        tabs.AddPage("💰  قرارداد و حقوق");
        tabs.AddPage("👪  افراد تحت تکفل");
        tabs.AddPage("🏦  حساب‌های بانکی");

        // ───────────── نوار پایین: عملیات (راست) + مرحله قبل/بعد (چپ) ─────────────
        btnSave = ModernButton.Create("💾  ذخیره", ModernTheme.Success, 200, 46);
        btnSave.Font = ModernTheme.GetFont(12f, FontStyle.Bold);
        btnCancel = ModernButton.Create("انصراف", ModernTheme.Neutral, 110, 46, ModernButtonStyle.Outline);
        btnManageContract = ModernButton.Create("📑  مدیریت قراردادها", ModernTheme.Primary, 190, 46, ModernButtonStyle.Soft);

        btnSave.Click += BtnSave_Click;
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        btnManageContract.Click += async (s, e) => await BtnManageContract_ClickAsync();

        btnPrev = ModernButton.Create("▶  قبلی", ModernTheme.Neutral, 105, 46, ModernButtonStyle.Outline);
        btnNext = ModernButton.Create("بعدی  ◀", ModernTheme.Primary, 105, 46, ModernButtonStyle.Soft);
        btnPrev.TabStop = false;
        btnNext.TabStop = false;
        btnPrev.Click += (s, e) => { if (tabs.SelectedIndex > 0) tabs.SelectedIndex--; };
        btnNext.Click += (s, e) => { if (tabs.SelectedIndex < tabs.Count - 1) tabs.SelectedIndex++; };
        lblStep = new Label
        {
            AutoSize = false,
            Size = new Size(110, 46),
            Margin = new Padding(5),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(10f, FontStyle.Bold),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent
        };
        tabs.SelectedIndexChanged += (s, e) => UpdateStepNavigation();

        var flowButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,     // فرم Mirror شده: اولین دکمه در راست
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(14, 9, 14, 0),
            Margin = Padding.Empty
        };
        flowButtons.Controls.Add(btnSave);
        flowButtons.Controls.Add(btnCancel);
        flowButtons.Controls.Add(btnManageContract);

        var flowNav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(14, 9, 14, 0),
            Margin = Padding.Empty
        };
        flowNav.Controls.Add(btnPrev);
        flowNav.Controls.Add(lblStep);
        flowNav.Controls.Add(btnNext);

        var footerLayout = new BufferedTable { Dock = DockStyle.Fill, BackColor = Color.White };
        footerLayout.ColumnCount = 2; footerLayout.RowCount = 1;
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));   // ستون ۰ = راست
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        footerLayout.Controls.Add(flowButtons, 0, 0);
        footerLayout.Controls.Add(flowNav, 1, 0);
        footerLayout.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, footerLayout.Width, 0);
        };

        UpdateStepNavigation();

        var mainLayout = new BufferedTable { Dock = DockStyle.Fill, BackColor = ModernTheme.Background };
        mainLayout.ColumnCount = 1; mainLayout.RowCount = 2;
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
        mainLayout.Controls.Add(tabs, 0, 0);
        mainLayout.Controls.Add(footerLayout, 0, 1);
        this.Controls.Add(mainLayout);

        // هدر استاندارد (لوگو + عنوان) مثل فرم چک؛ ارتفاع فرم به اندازه‌ی صفحه‌ی نمایش محدود می‌شود
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        this.ApplyStandardHeader(title, new Size(1120, Math.Min(830, wa.Height - 10)));
        EnterNavigationHelper.EnableForForm(this);
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave, btnCancel);
    }

    private void UpdateStepNavigation()
    {
        int i = tabs.SelectedIndex, n = tabs.Count;
        btnPrev.Enabled = i > 0;
        btnNext.Enabled = i >= 0 && i < n - 1;
        lblStep.Text = $"مرحله {ToFa(i + 1)} از {ToFa(n)}";
    }

    private static string ToFa(int value)
    {
        var chars = value.ToString().ToCharArray();
        for (int k = 0; k < chars.Length; k++)
            if (chars[k] >= '0' && chars[k] <= '9') chars[k] = (char)('۰' + (chars[k] - '0'));
        return new string(chars);
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (_employeeService is null)
        {
            ModernMessageBox.ShowError("سرویس کارمند مقداردهی نشده است.");
            return;
        }

        // ✅ FIX: برای ویرایش، از رکورد فعلی دیتابیس شروع می‌کنیم تا فیلدهایی که این فرم
        // نمایش/ویرایش نمی‌کند (مبالغ حقوق، عکس، نام کاربری، وضعیت و ...) پاک نشوند.
        Employee tempEmp = new Employee();
        if (_employee is not null)
            tempEmp = _employeeService.GetById(_employee.Id) ?? new Employee { Id = _employee.Id };

        // تب «مشخصات فردی» باید دیده شود تا پیام خطا و فوکوس روی فیلد اشتباه درست کار کند
        int previousTab = tabs.SelectedIndex;
        tabs.SelectedIndex = 0;
        if (!ucPersonal.SaveData(tempEmp)) return;
        tabs.SelectedIndex = previousTab;

        string? jobError = ucJob.ValidateJob();
        if (jobError is not null)
        {
            tabs.SelectedIndex = 1;
            ModernMessageBox.ShowWarning(jobError);
            return;
        }

        ucJob.SaveData(tempEmp);
        ucSalary.SaveData(tempEmp);

        int? excludeId = _employee?.Id;
        if (_employeeService.IsPersonnelCodeTaken(tempEmp.PersonnelCode!, excludeId))
        {
            ModernMessageBox.ShowWarning($"کد پرسنلی «{tempEmp.PersonnelCode}» قبلاً ثبت شده است.");
            return;
        }
        if (!string.IsNullOrWhiteSpace(tempEmp.NationalCode) && _employeeService.IsNationalCodeTaken(tempEmp.NationalCode, excludeId))
        {
            ModernMessageBox.ShowWarning($"کد ملی «{tempEmp.NationalCode}» قبلاً ثبت شده است.");
            return;
        }

        DateTime? terminationDate = null;
        if (_employee is not null && _employee.IsActive && !tempEmp.IsActive)
        {
            var result = ModernMessageBox.Show(
                "شما در حال غیرفعال کردن این پرسنل هستید.\nآیا قراردادهای فعال این شخص نیز باید خاتمه یابند؟",
                "خاتمه قرارداد",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel) return;
            else if (result == DialogResult.Yes) terminationDate = DateTime.Now;
        }

        bool saveSuccess = await AsyncHelper.RunWithLoading(this, async () =>
        {
            if (_employee is not null)
            {
                await _employeeService.UpdateAsync(tempEmp, terminationDate);
            }
            else
            {
                await _employeeService.AddAsync(tempEmp);
            }

            if (tempEmp.Id <= 0)
                throw new InvalidOperationException("خطا در ذخیره اطلاعات اصلی کارمند. لطفاً دوباره تلاش کنید.");

            await ucDependents.SaveDataAsync(tempEmp.Id);
            await ucBankAccounts.SaveDataAsync(tempEmp.Id);
        }, "در حال ذخیره اطلاعات کارمند...");

        if (saveSuccess)
        {
            ToastHelper.Show("اطلاعات کارمند با موفقیت ذخیره شد.", isSuccess: true);
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ✅ FIX: تبدیل به Async و ارسال تنظیمات سالانه برای لود دوباره اطلاعات
    private async Task BtnManageContract_ClickAsync()
    {
        if (_employee is null || _employee.Id <= 0)
        {
            ModernMessageBox.ShowWarning("ابتدا اطلاعات هویتی کارمند را ذخیره کنید.");
            return;
        }

        using var contractForm = new EmployeeContractForm(AppConfig.ConnectionString, _employee);
        if (contractForm.ShowDialog(this) == DialogResult.OK)
        {
            var updatedEmployee = _employeeService?.GetById(_employee.Id);
            if (updatedEmployee is not null)
            {
                _employee = updatedEmployee;

                // گرفتن تنظیمات سالانه برای لود صحیح فرم حقوق
                int year = _employee.ContractYear ?? new System.Globalization.PersianCalendar().GetYear(DateTime.Now);
                var settingsRepo = new SalarySettingsRepository(AppConfig.ConnectionString);
                var settings = await settingsRepo.GetByYearAsync(year);

                ucSalary.LoadData(_employee, settings);
                ToastHelper.Show("اطلاعات قرارداد با موفقیت به‌روزرسانی شد.", isSuccess: true);
            }
        }
    }
}
