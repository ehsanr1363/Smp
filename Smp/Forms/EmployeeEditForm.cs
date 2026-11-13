using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;
using SMP.Forms.Controls;

namespace SMP.Forms
{
    public partial class EmployeeEditForm : Form
    {
        private readonly EmployeeService? _employeeService;
        private Employee? _employee;

        private TabControl tabControl = null!;
        private ucEmployeePersonal ucPersonal = null!;
        private ucEmployeeJob ucJob = null!;
        private ucEmployeeSalary ucSalary = null!;
        private ucEmployeeDependents ucDependents = null!;
        private ucEmployeeBankAccounts ucBankAccounts = null!;

        private Button btnSave = null!, btnCancel = null!, btnManageContract = null!;

        public int CurrentEmployeeId => _employee?.Id ?? 0;

        public EmployeeEditForm()
        {
            BuildUI();
            ShowInTaskbar = false;
        }

        public EmployeeEditForm(EmployeeService employeeService, DepartmentService departmentService,
                                LookupService lookupService, Employee? employee = null) : this()
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

            tabControl.TabPages[0].Controls.Add(ucPersonal);
            tabControl.TabPages[1].Controls.Add(ucJob);
            tabControl.TabPages[2].Controls.Add(ucSalary);
            tabControl.TabPages[3].Controls.Add(ucDependents);
            tabControl.TabPages[4].Controls.Add(ucBankAccounts);

            Text = employee != null ? "ویرایش کارمند" : "افزودن کارمند";

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

                if (employee != null)
                {
                    ucJob.LoadData(employee);

                    // ✅ FIX: خواندن مستقیم اطلاعات حقوق از آخرین قرارداد فعال کارمند
                    var contractRepo = new EmployeeContractRepository(AppConfig.ConnectionString);
                    var activeContract = await contractRepo.GetActiveContractByEmployeeIdAsync(employee.Id);

                    if (activeContract != null)
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

        private void BuildUI()
        {
            UIHelper.SetupForm(this, "مدیریت کارمند", new Size(1050, 850));
            this.MinimumSize = new Size(1000, 800);

            tabControl = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(12f, FontStyle.Bold), SizeMode = TabSizeMode.Fixed, ItemSize = new Size(160, 40) };

            tabControl.TabPages.Add(new TabPage("مشخصات فردی") { AutoScroll = true, Padding = new Padding(0) });
            tabControl.TabPages.Add(new TabPage("اطلاعات شغلی") { AutoScroll = true, Padding = new Padding(0) });
            tabControl.TabPages.Add(new TabPage("قرارداد و حقوق") { AutoScroll = true, Padding = new Padding(0) });
            tabControl.TabPages.Add(new TabPage("افراد تحت تکفل") { AutoScroll = true, Padding = new Padding(0) });
            tabControl.TabPages.Add(new TabPage("حساب‌های بانکی") { AutoScroll = true, Padding = new Padding(0) });

            var pnlBottom = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = UIHelper.WarmPanelBackground,
                ColumnCount = 3,
                RowCount = 1
            };
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var flowButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.None,
                Padding = new Padding(10)
            };

            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.WarmPrimary, 120, 40);
            btnCancel = UIHelper.CreateButton("انصراف", UIHelper.WarmSecondary, 120, 40);
            btnManageContract = UIHelper.CreateButton("مدیریت قراردادها", UIHelper.WarmSecondary, 160, 40);

            btnSave.Click += BtnSave_Click;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnManageContract.Click += async (s, e) => await BtnManageContract_ClickAsync();

            flowButtons.Controls.Add(btnSave);
            flowButtons.Controls.Add(btnCancel);
            flowButtons.Controls.Add(btnManageContract);

            pnlBottom.Controls.Add(flowButtons, 1, 0);

            this.Controls.Add(tabControl);
            this.Controls.Add(pnlBottom);

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave, btnCancel);
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_employeeService == null)
            {
                ModernMessageBox.ShowError("سرویس کارمند مقداردهی نشده است.");
                return;
            }

            var tempEmp = new Employee();
            if (_employee != null) tempEmp.Id = _employee.Id;

            if (!ucPersonal.SaveData(tempEmp)) return;

            ucJob.SaveData(tempEmp);
            ucSalary.SaveData(tempEmp);

            if (_employee != null)
            {
                tempEmp.ContractNumber = _employee.ContractNumber;
                tempEmp.ContractStartDate = _employee.ContractStartDate;
                tempEmp.ContractEndDate = _employee.ContractEndDate;
                tempEmp.ContractYear = _employee.ContractYear;
            }

            int? excludeId = _employee?.Id;
            if (_employeeService.IsPersonnelCodeTaken(tempEmp.PersonnelCode!, excludeId))
            {
                ModernMessageBox.ShowWarning($"کد پرسنلی «{tempEmp.PersonnelCode}» قبلاً ثبت شده است.");
                return;
            }
            if (_employeeService.IsNationalCodeTaken(tempEmp.NationalCode!, excludeId))
            {
                ModernMessageBox.ShowWarning($"کد ملی «{tempEmp.NationalCode}» قبلاً ثبت شده است.");
                return;
            }

            DateTime? terminationDate = null;
            if (_employee != null && _employee.IsActive && !tempEmp.IsActive)
            {
                var result = MessageBox.Show(
                    "شما در حال غیرفعال کردن این پرسنل هستید.\nآیا قراردادهای فعال این شخص نیز باید خاتمه یابند؟",
                    "خاتمه قرارداد",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Cancel) return;
                else if (result == DialogResult.Yes) terminationDate = DateTime.Now;
            }

            bool saveSuccess = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_employee != null)
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
            if (_employee == null || _employee.Id <= 0)
            {
                ModernMessageBox.ShowWarning("ابتدا اطلاعات هویتی کارمند را ذخیره کنید.");
                return;
            }

            using var contractForm = new EmployeeContractForm(AppConfig.ConnectionString, _employee);
            if (contractForm.ShowDialog(this) == DialogResult.OK)
            {
                var updatedEmployee = _employeeService?.GetById(_employee.Id);
                if (updatedEmployee != null)
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
}