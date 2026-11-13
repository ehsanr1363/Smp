using SMP.Data;
using SMP.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

// 👇 جلوگیری از تداخل نام Chart با کتابخانه ClosedXML 👇
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;

namespace SMP.Forms
{
    public class ManagementDashboardForm : Form
    {
        private readonly DashboardRepository _repository;

        // KPI Cards
        private Label lblTotalEmployees = null!, lblActiveEmployees = null!, lblInactiveEmployees = null!;
        private Label lblMonthlyPayroll = null!, lblAvgSalary = null!, lblExpiringContracts = null!;

        // Charts
        private Chart chartEducation = null!, chartPersonnelType = null!;

        // Expiring grid
        private DataGridView dgvExpiring = null!;

        // ✅ FIX P2: جلوگیری از بارگذاری همزمان و کرش
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public ManagementDashboardForm(string connectionString)
        {
            _repository = new DashboardRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("داشبورد مدیریتی", new Size(1300, 850));
            ShowInTaskbar = false;

            // فعال‌سازی میانبر Esc برای بستن
            ShortcutHelper.ApplyStandardShortcuts(this);

            // بارگذاری Async پس از نمایش فرم
            this.Shown += async (s, e) =>
            {
                try { await LoadAllDataSafeAsync(); }
                catch (Exception ex) { LogHelper.LogException(ex, "ManagementDashboardForm.Shown"); }
            };
        }

        // ✅ FIX P2: آزادسازی SemaphoreSlim
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _loadLock?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            // ─── پنل اصلی ───
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));  // KPI Cards
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));  // Charts
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // Expiring grid

            // ─── ردیف اول: ۶ کارت KPI ───
            var kpiLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(5) };
            for (int i = 0; i < 3; i++) kpiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            kpiLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            kpiLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            lblTotalEmployees = CreateKpiCard(kpiLayout, "👥 کل پرسنل", UIHelper.SectionBlue, 0, 0);
            lblActiveEmployees = CreateKpiCard(kpiLayout, "✅ پرسنل فعال", UIHelper.ActionGreen, 1, 0);
            lblInactiveEmployees = CreateKpiCard(kpiLayout, "⛔ پرسنل غیرفعال", UIHelper.SectionGray, 2, 0);

            lblMonthlyPayroll = CreateKpiCard(kpiLayout, "💰 جمع حقوق آخرین ماه", UIHelper.SectionPurple, 0, 1);
            lblAvgSalary = CreateKpiCard(kpiLayout, "📊 میانگین حقوق", UIHelper.SectionOrange, 1, 1);
            lblExpiringContracts = CreateKpiCard(kpiLayout, "⚠️ قراردادهای در آستانه انقضا", UIHelper.ActionRed, 2, 1);

            // ─── ردیف دوم: دو نمودار ───
            var chartsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(5) };
            chartsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            chartsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            chartEducation = CreatePieChart("توزیع پرسنل بر اساس تحصیلات");
            chartPersonnelType = CreatePieChart("توزیع پرسنل بر اساس نوع پرسنلی");

            chartsLayout.Controls.Add(chartEducation, 0, 0);
            chartsLayout.Controls.Add(chartPersonnelType, 1, 0);

            // ─── ردیف سوم: جدول قراردادهای در آستانه انقضا ───
            var bottomPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };

            var lblExpiringTitle = new Label
            {
                Text = "⚠️ قراردادهای در آستانه انقضا (۳۰ روز آینده)",
                Dock = DockStyle.Top,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = UIHelper.ActionRed,
                Height = 30,
                TextAlign = ContentAlignment.MiddleRight
            };

            dgvExpiring = new DataGridView { Dock = DockStyle.Fill };
            dgvExpiring.ApplyModernStyle();

            // فرمت‌دهی تاریخ به شمسی در گرید
            dgvExpiring.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                if (e.Value is DateTime dt)
                {
                    e.Value = DateHelper.ToPersianDate(dt);
                    e.FormattingApplied = true;
                }
            };

            // رنگ‌آمیزی ردیف‌های هشدار
            dgvExpiring.RowPrePaint += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    dgvExpiring.Rows[e.RowIndex].DefaultCellStyle.BackColor = UIHelper.WarmWarningBackground;
                    dgvExpiring.Rows[e.RowIndex].DefaultCellStyle.ForeColor = UIHelper.WarmWarningText;
                }
            };

            bottomPanel.Controls.Add(dgvExpiring);
            bottomPanel.Controls.Add(lblExpiringTitle);

            mainLayout.Controls.Add(kpiLayout, 0, 0);
            mainLayout.Controls.Add(chartsLayout, 0, 1);
            mainLayout.Controls.Add(bottomPanel);

            this.Controls.Add(mainLayout);
        }

        private Label CreateKpiCard(TableLayoutPanel parent, string title, Color color, int col, int row)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(6) };
            card.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle,
                    color, 3, ButtonBorderStyle.Solid,
                    color, 3, ButtonBorderStyle.Solid,
                    color, 3, ButtonBorderStyle.Solid,
                    color, 3, ButtonBorderStyle.Solid);
            };

            var lblTitle = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(10f, FontStyle.Bold),
                ForeColor = color
            };

            var lblValue = new Label
            {
                Text = "0",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(20f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText
            };

            card.Controls.Add(lblValue);
            card.Controls.Add(lblTitle);
            parent.Controls.Add(card, col, row);

            return lblValue;
        }

        private Chart CreatePieChart(string title)
        {
            var chart = new Chart { Dock = DockStyle.Fill };
            var area = new ChartArea("MainArea") { BackColor = Color.White };
            chart.ChartAreas.Add(area);

            var legend = new Legend("Legend1")
            {
                Docking = Docking.Bottom,
                Font = UIHelper.PersianFont(9f),
                Alignment = StringAlignment.Center
            };
            chart.Legends.Add(legend);

            chart.Titles.Add(new Title(title, Docking.Top, UIHelper.PersianFont(11f, FontStyle.Bold), UIHelper.WarmText));

            var series = new Series("Data")
            {
                ChartType = SeriesChartType.Pie,
                IsValueShownAsLabel = true
            };
            chart.Series.Add(series);

            return chart;
        }

        // ═══════════════════════════════════════════════════════════════
        // ✅ FIX P2: بارگذاری ایمن (جلوگیری از کرش در صورت بسته شدن فرم)
        // ═══════════════════════════════════════════════════════════════
        private async Task LoadAllDataSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return; // اگر در حال بارگذاری است، درخواست جدید را رد کن

            try
            {
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    // دریافت تمام اطلاعات به صورت همزمان برای افزایش سرعت
                    var statsTask = _repository.GetEmployeeStatsAsync();
                    var payrollTask = _repository.GetLastPayrollTotalAsync();
                    var expiringTask = _repository.GetExpiringContractsCountAsync();
                    var eduTask = _repository.GetEducationDistributionAsync();
                    var typeTask = _repository.GetPersonnelTypeDistributionAsync();
                    var expiringListTask = _repository.GetExpiringContractsListAsync();

                    await Task.WhenAll(statsTask, payrollTask, expiringTask, eduTask, typeTask, expiringListTask);

                    // ✅ FIX: اگر کاربر فرم را در حین بارگذاری بست، از ادامه کار جلوگیری کن
                    if (IsDisposed) return;

                    // ۱. به‌روزرسانی KPI ها
                    var dtStats = statsTask.Result;
                    if (dtStats.Rows.Count > 0)
                    {
                        var row = dtStats.Rows[0];
                        lblTotalEmployees.Text = NumberHelper.FormatThousands(Convert.ToInt32(row["TotalCount"]), usePersianDigits: true);
                        lblActiveEmployees.Text = NumberHelper.FormatThousands(Convert.ToInt32(row["ActiveCount"]), usePersianDigits: true);
                        lblInactiveEmployees.Text = NumberHelper.FormatThousands(Convert.ToInt32(row["InactiveCount"]), usePersianDigits: true);
                        lblAvgSalary.Text = NumberHelper.FormatThousands(Convert.ToDecimal(row["AvgSalary"]), usePersianDigits: true);
                    }

                    decimal monthlyPayroll = payrollTask.Result;
                    lblMonthlyPayroll.Text = NumberHelper.FormatThousands(monthlyPayroll, usePersianDigits: true);

                    int expiringCount = expiringTask.Result;
                    lblExpiringContracts.Text = NumberHelper.FormatThousands(expiringCount, usePersianDigits: true);

                    // ۲. نمودار تحصیلات
                    var dtEdu = eduTask.Result;
                    chartEducation.Series[0].Points.Clear();
                    FillPieChart(chartEducation, dtEdu, "Education");

                    // ۳. نمودار نوع پرسنلی
                    var dtType = typeTask.Result;
                    chartPersonnelType.Series[0].Points.Clear();
                    FillPieChart(chartPersonnelType, dtType, "PersonnelType");

                    // ۴. جدول قراردادهای در آستانه انقضا
                    dgvExpiring.DataSource = expiringListTask.Result;
                }, "در حال بارگذاری داشبورد مدیریتی...");
            }
            catch (ObjectDisposedException) { /* فرم بسته شده — نادیده بگیر */ }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ManagementDashboardForm.LoadAllDataSafeAsync");
                if (!IsDisposed)
                    ModernMessageBox.ShowError("خطا در بارگذاری داشبورد:\n" + ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }

        // متد کمکی برای پر کردن نمودار دایره‌ای با رنگ‌های استاندارد
        private void FillPieChart(Chart chart, DataTable dt, string categoryColumn)
        {
            if (IsDisposed || chart == null || chart.IsDisposed) return;

            // پالت رنگ استاندارد پروژه (جلوگیری از رنگ‌های تصادفی و زشت)
            Color[] chartColors =
            {
                UIHelper.SectionBlue, UIHelper.ActionGreen, UIHelper.ActionAmber,
                UIHelper.WarmDanger, UIHelper.WarmPrimary, UIHelper.SectionPurple,
                UIHelper.SectionTeal, UIHelper.SectionOrange
            };

            int total = dt.Rows.Count > 0 ? dt.AsEnumerable().Sum(r => Convert.ToInt32(r["EmployeeCount"])) : 0;
            int idx = 0;

            foreach (DataRow row in dt.Rows)
            {
                string category = row[categoryColumn].ToString();
                int count = Convert.ToInt32(row["EmployeeCount"]);
                double percent = total > 0 ? (count * 100.0 / total) : 0;

                int pointIdx = chart.Series[0].Points.AddXY(category, count);

                // درصد فارسی داخل دایره
                string percentText = StringHelper.ConvertToPersianDigits(percent.ToString("0")) + "٪";
                chart.Series[0].Points[pointIdx].Label = percentText;
                chart.Series[0].Points[pointIdx].Font = UIHelper.PersianFont(10f, FontStyle.Bold);
                chart.Series[0].Points[pointIdx].LegendText = category;

                // استفاده از رنگ‌های پالت به جای فرمول‌های تصادفی
                chart.Series[0].Points[pointIdx].Color = chartColors[idx % chartColors.Length];
                idx++;
            }
        }
    }
}