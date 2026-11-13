using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class SalaryForecastForm : Form
    {
        private readonly EmployeeRepository _empRepo;
        private readonly SalarySettingsRepository _settingsRepo;
        private readonly LoanRepository _loanRepo;
        private readonly SupplementaryInsuranceRepository _suppInsRepo;
        private readonly CarInsuranceRepository _carInsRepo;
        private readonly PayrollCalculatorService _calculator;

        private ComboBox cmbYear = null!, cmbMonth = null!;
        private Button btnCalculate = null!;
        private DataGridView dgvForecast = null!;

        private Label lblTotalGross = null!, lblTotalInsurance = null!, lblTotalTax = null!, lblTotalNet = null!;

        // ✅ FIX P2
        private readonly SemaphoreSlim _calcLock = new(1, 1);

        public SalaryForecastForm(string connectionString)
        {
            _empRepo = new EmployeeRepository(connectionString);
            _settingsRepo = new SalarySettingsRepository(connectionString);
            _loanRepo = new LoanRepository(connectionString);
            _suppInsRepo = new SupplementaryInsuranceRepository(connectionString);
            _carInsRepo = new CarInsuranceRepository(connectionString);
            _calculator = new PayrollCalculatorService();

            BuildUI();
            this.ApplyStandardHeader("پیش‌بینی هزینه حقوق (Forecast)", new Size(1150, 750));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this);
            LoadDefaults();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _calcLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void LoadDefaults()
        {
            var pc = new PersianCalendar();
            int currentYear = pc.GetYear(DateTime.Now);
            int currentMonth = pc.GetMonth(DateTime.Now);

            if (cmbYear.Items.Contains(currentYear)) cmbYear.SelectedItem = currentYear;
            else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;

            if (cmbMonth.Items.Contains(currentMonth)) cmbMonth.SelectedItem = currentMonth;
            else if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
        }

        private void BuildUI()
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
            var topFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };

            cmbYear = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 15, 5) };
            for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);

            cmbMonth = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 15, 5) };
            for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);

            btnCalculate = UIHelper.CreateButton("📊 محاسبه پیش‌بینی", UIHelper.SectionPurple, 160, 35);
            btnCalculate.Margin = new Padding(5, 3, 15, 5);
            btnCalculate.Click += async (s, e) => await CalculateSafeAsync();

            var lblHint = new Label
            {
                Text = "💡 پیش‌بینی بر اساس کارکرد استاندارد (۳۰ روز) + وام + بیمه تکمیلی + بیمه خودرو",
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = UIHelper.PersianFont(9f),
                Margin = new Padding(5, 13, 5, 5)
            };

            topFlow.Controls.Add(new Label { Text = "سال:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbYear);
            topFlow.Controls.Add(new Label { Text = "ماه:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbMonth);
            topFlow.Controls.Add(btnCalculate);
            topFlow.Controls.Add(lblHint);

            topPanel.Controls.Add(topFlow);

            var summaryPanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 90, ColumnCount = 4, Padding = new Padding(10) };
            for (int i = 0; i < 4; i++) summaryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            lblTotalGross = CreateSummaryCard(summaryPanel, "جمع مزایا (برآوردی)", UIHelper.SectionBlue, 0);
            lblTotalInsurance = CreateSummaryCard(summaryPanel, "جمع بیمه", UIHelper.SectionOrange, 1);
            lblTotalTax = CreateSummaryCard(summaryPanel, "جمع مالیات", UIHelper.ActionRed, 2);
            lblTotalNet = CreateSummaryCard(summaryPanel, "خالص پرداختی", UIHelper.ActionGreen, 3);

            dgvForecast = new DataGridView { Dock = DockStyle.Fill };
            dgvForecast.ApplyModernStyle();

            dgvForecast.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                if (e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
                else if (e.Value is int i)
                {
                    e.Value = NumberHelper.FormatThousands(i, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            };

            this.Controls.Add(dgvForecast);
            this.Controls.Add(summaryPanel);
            this.Controls.Add(topPanel);
        }

        private Label CreateSummaryCard(TableLayoutPanel parent, string title, Color color, int col)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5) };
            card.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle,
                    color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid,
                    color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid);
            };

            var lblTitle = new Label { Text = title, Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(9.5f, FontStyle.Bold), ForeColor = color };
            var lblValue = new Label { Text = "0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(14f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            card.Controls.Add(lblValue);
            card.Controls.Add(lblTitle);
            parent.Controls.Add(card, col, 0);

            return lblValue;
        }

        private async Task CalculateSafeAsync()
        {
            if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null || IsDisposed) return;
            if (!await _calcLock.WaitAsync(0)) return; // ✅ FIX

            try
            {
                int year = Convert.ToInt32(cmbYear.SelectedItem);
                int month = Convert.ToInt32(cmbMonth.SelectedItem);

                List<PayrollResult>? results = null;
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var settings = await _settingsRepo.GetByYearAsync(year);
                    if (settings == null)
                        throw new InvalidOperationException($"تنظیمات حقوق برای سال {year} یافت نشد.");

                    var employees = await _empRepo.GetEmployeesWithDetailsAsync("", false);
                    var activeLoans = await _loanRepo.GetActiveLoansAsync();
                    var activeSuppIns = await _suppInsRepo.GetActiveInsurancesAsync();
                    var activeCarIns = await _carInsRepo.GetActiveCarInsurancesAsync();

                    var loansByEmployee = activeLoans.GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
                    var suppInsByEmployee = activeSuppIns.GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
                    var carInsByEmployee = activeCarIns.GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

                    var taxBrackets = _calculator.GetTaxBracketsSafe(year);

                    results = await Task.Run(() =>
                    {
                        var list = new List<PayrollResult>();
                        foreach (var emp in employees)
                        {
                            var att = new Attendance
                            {
                                EmployeeId = emp.Id,
                                Year = year,
                                Month = month,
                                WorkingDays = 30,
                                HoursWorked = 220,
                                OvertimeHours = 0,
                                AbsenceDays = 0
                            };

                            decimal loanInst = 0;
                            if (loansByEmployee.TryGetValue(emp.Id, out var empLoans) && empLoans.Count > 0)
                                loanInst = Math.Min(empLoans.Sum(l => l.MonthlyInstallment), empLoans.Sum(l => l.RemainingAmount));

                            decimal suppInsInst = 0;
                            if (suppInsByEmployee.TryGetValue(emp.Id, out var empIns) && empIns.Count > 0)
                                suppInsInst = Math.Min(empIns.Sum(i => i.MonthlyInstallment), empIns.Sum(i => i.RemainingAmount));

                            decimal carInsInst = 0;
                            if (carInsByEmployee.TryGetValue(emp.Id, out var empCarIns) && empCarIns.Count > 0)
                                carInsInst = Math.Min(empCarIns.Sum(i => i.MonthlyInstallment), empCarIns.Sum(i => i.RemainingAmount));

                            var result = _calculator.CalculatePayroll(emp, att, settings, loanInst, suppInsInst, carInsInst, 0, taxBrackets);
                            list.Add(result);
                        }
                        return list;
                    });
                }, "در حال محاسبه پیش‌بینی حقوق...");

                if (!success || results == null || IsDisposed) return;

                dgvForecast.DataSource = null;
                dgvForecast.DataSource = results;

                if (dgvForecast.Columns.Contains("EmployeeId")) dgvForecast.Columns["EmployeeId"].Visible = false;
                if (dgvForecast.Columns.Contains("Year")) dgvForecast.Columns["Year"].Visible = false;
                if (dgvForecast.Columns.Contains("Month")) dgvForecast.Columns["Month"].Visible = false;

                decimal totalGross = results.Sum(r => r.TotalEarnings);
                decimal totalIns = results.Sum(r => r.InsuranceEmployee);
                decimal totalTax = results.Sum(r => r.IncomeTax);
                decimal totalNet = results.Sum(r => r.NetPay);

                lblTotalGross.Text = NumberHelper.FormatThousands(totalGross, usePersianDigits: true);
                lblTotalInsurance.Text = NumberHelper.FormatThousands(totalIns, usePersianDigits: true);
                lblTotalTax.Text = NumberHelper.FormatThousands(totalTax, usePersianDigits: true);
                lblTotalNet.Text = NumberHelper.FormatThousands(totalNet, usePersianDigits: true);

                ToastHelper.Show($"پیش‌بینی برای {TranslationHelper.GetMonthName(month)} {StringHelper.ConvertToPersianDigits(year.ToString())} انجام شد.", isSuccess: true);
            }
            catch (ObjectDisposedException) { /* نادیده بگیر */ }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "SalaryForecastForm.CalculateSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در پیش‌بینی حقوق:\n" + ex.Message);
            }
            finally
            {
                try { _calcLock.Release(); } catch { }
            }
        }
    }
}