using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class EmployeeProfileForm : Form
    {
        private readonly string _connectionString;
        private readonly Employee _employee;

        private TabControl tabControl = null!;

        private DataGridView dgvPayroll = null!;
        private DataGridView dgvLoans = null!;
        private DataGridView dgvLeaves = null!;

        public EmployeeProfileForm(string connectionString, Employee employee)
        {
            _connectionString = connectionString;
            _employee = employee;

            BuildUI();
            this.ApplyStandardHeader($"پرونده پرسنلی: {employee.FullName}", new Size(1100, 750));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this);

            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void BuildUI()
        {
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(11f),
                Padding = new Point(20, 10)
            };
            ModernLayoutHelper.StyleModernTabControl(tabControl);

            tabControl.TabPages.Add(CreatePersonalInfoTab());

            var tabPayroll = new TabPage { Text = "سوابق حقوق", Name = "tabPayroll" };
            dgvPayroll = CreateGrid();
            tabPayroll.Controls.Add(dgvPayroll);
            tabControl.TabPages.Add(tabPayroll);

            var tabLoans = new TabPage { Text = "وام و مساعده", Name = "tabLoans" };
            dgvLoans = CreateGrid();
            tabLoans.Controls.Add(dgvLoans);
            tabControl.TabPages.Add(tabLoans);

            var tabLeaves = new TabPage { Text = "سوابق مرخصی", Name = "tabLeaves" };
            dgvLeaves = CreateGrid();
            tabLeaves.Controls.Add(dgvLeaves);
            tabControl.TabPages.Add(tabLeaves);

            this.Controls.Add(tabControl);
        }

        private DataGridView CreateGrid()
        {
            var dgv = new DataGridView { Dock = DockStyle.Fill };
            dgv.ApplyModernStyle();

            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                string colName = dgv.Columns[e.ColumnIndex].Name;

                if (colName.Contains("Date") || colName.Contains("تاریخ"))
                {
                    if (e.Value is DateTime dt)
                    {
                        e.Value = DateHelper.ToPersianDate(dt);
                        e.FormattingApplied = true;
                    }
                }
                else if (e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
                else if (e.Value is int i && (colName.Contains("Amount") || colName.Contains("Salary") || colName.Contains("Pay")))
                {
                    e.Value = NumberHelper.FormatThousands((decimal)i, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            };

            return dgv;
        }

        private TabPage CreatePersonalInfoTab()
        {
            var page = new TabPage { Text = "اطلاعات شخصی", BackColor = Color.White, Padding = new Padding(20) };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

            AddInfoRow(layout, "کد پرسنلی:", _employee.PersonnelCode, "کد ملی:", _employee.NationalCode, 0);
            AddInfoRow(layout, "تاریخ استخدام:", DateHelper.ToPersianDate(_employee.HireDate), "وضعیت تأهل:", _employee.MaritalStatus, 1);
            AddInfoRow(layout, "شماره بیمه:", _employee.InsuranceNumber, "تعداد فرزند:", StringHelper.ConvertToPersianDigits(_employee.ChildrenCount.ToString()), 2);
            AddInfoRow(layout, "واحد سازمانی:", _employee.DepartmentName, "سمت:", _employee.Position, 3);
            AddInfoRow(layout, "تلفن تماس:", _employee.PhoneNumber, "شرکت محل کار:", _employee.CompanyTitle, 4);
            AddInfoRow(layout, "حقوق پایه:", NumberHelper.FormatThousands(_employee.BaseSalary, usePersianDigits: true) + " ریال", "پایه سنوات:", NumberHelper.FormatThousands(_employee.YearsBonus, usePersianDigits: true) + " ریال", 5);

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            page.Controls.Add(layout);
            return page;
        }

        private void AddInfoRow(TableLayoutPanel layout, string label1, string? value1, string label2, string? value2, int row)
        {
            layout.Controls.Add(new Label { Text = label1, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f), ForeColor = UIHelper.WarmText }, 0, row);
            layout.Controls.Add(new Label { Text = string.IsNullOrWhiteSpace(value1) ? "---" : value1, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = Color.Black }, 1, row);
            layout.Controls.Add(new Label { Text = label2, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f), ForeColor = UIHelper.WarmText }, 2, row);
            layout.Controls.Add(new Label { Text = string.IsNullOrWhiteSpace(value2) ? "---" : value2, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = Color.Black }, 3, row);
        }

        private async Task LoadDataSafeAsync()
        {
            if (IsDisposed) return;

            try
            {
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var payrollTask = Task.Run(() =>
                    {
                        var payrollRepo = new PayrollRepository(_connectionString);
                        return payrollRepo.GetEmployeePayrollHistoryAsync(_employee.Id);
                    });

                    var loansTask = Task.Run(async () =>
                    {
                        var loanRepo = new LoanRepository(_connectionString);
                        return await loanRepo.GetLoansByEmployeeIdAsync(_employee.Id);
                    });

                    var leavesTask = Task.Run(async () =>
                    {
                        var leaveRepo = new LeaveRepository(_connectionString);
                        return await leaveRepo.GetLeavesByEmployeeIdAsync(_employee.Id);
                    });

                    await Task.WhenAll(payrollTask, loansTask, leavesTask);

                    if (IsDisposed) return; // ✅ FIX: جلوگیری از کرش

                    dgvPayroll.DataSource = payrollTask.Result;
                    dgvLoans.DataSource = loansTask.Result;
                    dgvLeaves.DataSource = leavesTask.Result;
                }, "در حال بارگذاری پرونده پرسنلی...");

                if (!success && !IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری پرونده.");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "EmployeeProfileForm.LoadDataSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
        }
    }
}