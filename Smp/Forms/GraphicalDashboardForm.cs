using SMP.Data;
using SMP.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;

namespace SMP.Forms
{
    public class GraphicalDashboardForm : Form
    {
        private readonly DashboardRepository _repository;
        private Chart chartTrend = null!;
        private Chart chartDepartment = null!;
        private ComboBox cmbYear = null!;
        private Button btnRefresh = null!;

        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public GraphicalDashboardForm(string connectionString)
        {
            _repository = new DashboardRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("داشبورد گرافیکی و نمودارها", new Size(1200, 700));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);

            Shown += async (s, e) => await LoadChartsSafeAsync();
        }

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
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
            var topFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };

            cmbYear = new ComboBox
            {
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UIHelper.PersianFont(11f),
                Margin = new Padding(5, 8, 15, 5)
            };

            var pc = new PersianCalendar();
            int currentYear = pc.GetYear(DateTime.Now);
            for (int i = currentYear - 3; i <= currentYear + 2; i++) cmbYear.Items.Add(i);
            cmbYear.SelectedItem = currentYear;

            cmbYear.SelectedIndexChanged += async (s, e) => await LoadChartsSafeAsync();

            btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی نمودارها", UIHelper.SectionBlue, 180, 35);
            btnRefresh.Margin = new Padding(5, 3, 5, 5);
            btnRefresh.Click += async (s, e) => await LoadChartsSafeAsync();

            topFlow.Controls.Add(new Label { Text = "سال مالی:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbYear);
            topFlow.Controls.Add(btnRefresh);
            topPanel.Controls.Add(topFlow);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(10) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

            chartTrend = new Chart { Dock = DockStyle.Fill };
            var chartArea1 = new ChartArea("MainArea");
            chartArea1.BackColor = Color.White;
            chartArea1.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea1.AxisY.MajorGrid.LineColor = Color.LightGray;
            chartArea1.AxisX.LabelStyle.Font = UIHelper.PersianFont(9f);
            chartArea1.AxisY.LabelStyle.Font = UIHelper.PersianFont(9f);

            chartArea1.AxisY.LabelStyle.Format = "#,#,, 'M'";
            chartTrend.ChartAreas.Add(chartArea1);

            chartTrend.Legends.Add(new Legend("Legend1") { Docking = Docking.Top, Font = UIHelper.PersianFont(9f), Alignment = StringAlignment.Center });

            // ✅ FIX: عنوان نمودار حالا دینامیک است — قبلاً «۱۲ ماه اخیر» بود ولی فقط ۶ ماه داده نشان می‌داد
            chartTrend.Titles.Add(new Title("روند حقوق، بیمه و مالیات (میلیون تومان)", Docking.Top, UIHelper.PersianFont(12f, FontStyle.Bold), UIHelper.WarmText));

            chartTrend.Series.Add(new Series("حقوق") { ChartType = SeriesChartType.Column, Color = UIHelper.SectionBlue, IsValueShownAsLabel = false });
            chartTrend.Series.Add(new Series("بیمه") { ChartType = SeriesChartType.Column, Color = UIHelper.ActionGreen, IsValueShownAsLabel = false });
            chartTrend.Series.Add(new Series("مالیات") { ChartType = SeriesChartType.Column, Color = UIHelper.ActionRed, IsValueShownAsLabel = false });

            chartDepartment = new Chart { Dock = DockStyle.Fill };
            var chartArea2 = new ChartArea("PieArea") { BackColor = Color.White };
            chartDepartment.ChartAreas.Add(chartArea2);
            chartDepartment.Legends.Add(new Legend("Legend2") { Docking = Docking.Bottom, Font = UIHelper.PersianFont(9f), Alignment = StringAlignment.Center });
            chartDepartment.Titles.Add(new Title("توزیع پرسنل در دپارتمان‌ها", Docking.Top, UIHelper.PersianFont(12f, FontStyle.Bold), UIHelper.WarmText));
            chartDepartment.Series.Add(new Series("Deps") { ChartType = SeriesChartType.Pie, IsValueShownAsLabel = true, LabelFormat = "#PERCENT{P0}" });

            layout.Controls.Add(chartTrend, 0, 0);
            layout.Controls.Add(chartDepartment, 1, 0);

            this.Controls.Add(layout);
            this.Controls.Add(topPanel);
        }

        // ✅ FIX: currentMonth حالا از تاریخ جاری شمسی گرفته می‌شود (قبلاً 12 هاردکد بود)
        //         ⚠️ هیچ تغییر در GetPayrollTrendAsync و SUM/GROUP BY داده نشده —
        //         فقط پارامتر ورودی درست شد تا نمودار واقعاً ماه‌های منتهی به امروز را نشان دهد.
        private async Task LoadChartsSafeAsync()
        {
            if (cmbYear.SelectedItem == null || IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                int year = Convert.ToInt32(cmbYear.SelectedItem);

                // ✅ FIX: استفاده از ماه شمسی جاری به‌جای هاردکد 12
                var pc = new PersianCalendar();
                int currentMonth = pc.GetMonth(DateTime.Now);
                int currentYearNow = pc.GetYear(DateTime.Now);

                // عنوان دینامیک: اگر سال انتخابی سال جاری است، تا ماه جاری؛ وگرنه تا ماه ۱۲
                int displayUptoMonth = (year == currentYearNow) ? currentMonth : 12;
                chartTrend.Titles[0].Text = $"روند حقوق، بیمه و مالیات تا {TranslationHelper.GetMonthName(displayUptoMonth)} {year} (میلیون تومان)";

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var dtTrend = await _repository.GetPayrollTrendAsync(year, currentMonth);

                    if (IsDisposed) return;

                    chartTrend.Series["حقوق"].Points.Clear();
                    chartTrend.Series["بیمه"].Points.Clear();
                    chartTrend.Series["مالیات"].Points.Clear();

                    foreach (DataRow row in dtTrend.Rows)
                    {
                        int month = Convert.ToInt32(row["Month"]);
                        string label = TranslationHelper.GetMonthName(month);

                        decimal gross = row["TotalGross"] != DBNull.Value ? Convert.ToDecimal(row["TotalGross"]) : 0;
                        decimal ins = row["TotalInsurance"] != DBNull.Value ? Convert.ToDecimal(row["TotalInsurance"]) : 0;
                        decimal tax = row["TotalTax"] != DBNull.Value ? Convert.ToDecimal(row["TotalTax"]) : 0;

                        chartTrend.Series["حقوق"].Points.AddXY(label, gross);
                        chartTrend.Series["بیمه"].Points.AddXY(label, ins);
                        chartTrend.Series["مالیات"].Points.AddXY(label, tax);
                    }

                    var dtDept = await _repository.GetDepartmentDistributionAsync();

                    if (IsDisposed) return;

                    chartDepartment.Series["Deps"].Points.Clear();

                    Color[] chartColors =
                    {
                        UIHelper.SectionBlue, UIHelper.ActionGreen, UIHelper.ActionAmber,
                        UIHelper.WarmDanger, UIHelper.WarmPrimary, UIHelper.SectionPurple,
                        UIHelper.SectionTeal, UIHelper.SectionOrange
                    };

                    int colorIndex = 0;
                    foreach (DataRow row in dtDept.Rows)
                    {
                        string depName = row["DepartmentName"].ToString() ?? "نامشخص";
                        int count = Convert.ToInt32(row["EmployeeCount"]);

                        int pointIndex = chartDepartment.Series["Deps"].Points.AddXY(depName, count);
                        chartDepartment.Series["Deps"].Points[pointIndex].Color = chartColors[colorIndex % chartColors.Length];
                        colorIndex++;
                    }
                }, "در حال بارگذاری نمودارها...");
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "GraphicalDashboardForm.LoadChartsSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری نمودارها:\n" + ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }
    }
}