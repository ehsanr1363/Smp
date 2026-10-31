using PersonnelApp.Data;
using PersonnelApp.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;

namespace PersonnelApp.Forms
{
    public class LiquidityDashboardForm : Form
    {
        private readonly string _connectionString;
        private readonly LiquidityRepository _liquidityRepo;

        private Label lblTotalLiquidity = null!, lblTotalIncome = null!, lblTotalExpense = null!, lblNetCashFlow = null!;
        private Label lblIncomeChecks = null!, lblPayableChecks = null!;

        private Chart chartCashFlow = null!, chartExpense = null!, chartAccounts = null!;
        private ComboBox cmbDateFilter = null!;
        private TextBox txtFromDate = null!, txtToDate = null!;
        private Button btnApplyFilter = null!;
        private DataGridView dgvUpcomingChecks = null!;

        public LiquidityDashboardForm(string connectionString)
        {
            _connectionString = connectionString;
            _liquidityRepo = new LiquidityRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("داشبورد تحلیلی نقدینگی", new Size(1400, 900));
            this.WindowState = FormWindowState.Maximized;
            ShortcutHelper.ApplyStandardShortcuts(this);
            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));

            var filterPanel = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground };
            var flowFilter = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(10) };

            cmbDateFilter = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f) };
            cmbDateFilter.Items.AddRange(new[] { "این هفته", "این ماه", "ماه گذشته", "این سال", "بازه دلخواه" });
            cmbDateFilter.SelectedIndex = 0;
            cmbDateFilter.SelectedIndexChanged += (s, e) => OnDateFilterChanged();

            var lblFilter = new Label { Text = "بازه زمانی:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Margin = new Padding(10, 10, 5, 5) };
            txtFromDate = new TextBox { Width = 100, Font = UIHelper.PersianFont(11f), TextAlign = HorizontalAlignment.Center };
            DateHelper.ApplyDateMask(txtFromDate);
            var lblFrom = new Label { Text = "از:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(10, 10, 5, 5) };
            txtToDate = new TextBox { Width = 100, Font = UIHelper.PersianFont(11f), TextAlign = HorizontalAlignment.Center };
            DateHelper.ApplyDateMask(txtToDate);
            var lblTo = new Label { Text = "تا:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(10, 10, 5, 5) };

            btnApplyFilter = UIHelper.CreateButton("🔍 اعمال فیلتر", UIHelper.SectionBlue, 120, 32);
            btnApplyFilter.Margin = new Padding(10, 4, 10, 4);
            btnApplyFilter.Click += async (s, e) => await LoadDataSafeAsync();

            flowFilter.Controls.Add(lblFilter); flowFilter.Controls.Add(cmbDateFilter);
            flowFilter.Controls.Add(lblFrom); flowFilter.Controls.Add(txtFromDate);
            flowFilter.Controls.Add(lblTo); flowFilter.Controls.Add(txtToDate);
            flowFilter.Controls.Add(btnApplyFilter);
            filterPanel.Controls.Add(flowFilter);

            var kpiLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1 };
            for (int i = 0; i < 6; i++) kpiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66F));

            lblTotalLiquidity = CreateKpiCard(kpiLayout, "کل موجودی نقدی", UIHelper.SectionBlue, 0);
            lblTotalIncome = CreateKpiCard(kpiLayout, "دریافتی دوره", UIHelper.ActionGreen, 1);
            lblTotalExpense = CreateKpiCard(kpiLayout, "پرداختی دوره", UIHelper.ActionRed, 2);
            lblNetCashFlow = CreateKpiCard(kpiLayout, "جریان نقدی خالص", UIHelper.SectionPurple, 3);
            lblIncomeChecks = CreateKpiCard(kpiLayout, "چک‌های دریافتی (در جریان)", UIHelper.SectionTeal, 4);
            lblPayableChecks = CreateKpiCard(kpiLayout, "چک‌های پرداختی (در جریان)", UIHelper.WarmDanger, 5);

            var chartsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(5) };
            chartsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            chartsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            chartsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            chartCashFlow = CreateChart("روند دریافت و پرداخت");
            chartCashFlow.Series.Add(new Series("دریافت") { ChartType = SeriesChartType.Column, Color = UIHelper.ActionGreen, IsValueShownAsLabel = false });
            chartCashFlow.Series.Add(new Series("پرداخت") { ChartType = SeriesChartType.Column, Color = UIHelper.ActionRed, IsValueShownAsLabel = false });

            chartExpense = CreateChart("تفکیک هزینه‌ها");
            chartExpense.Series.Add(new Series("هزینه") { ChartType = SeriesChartType.Pie, IsValueShownAsLabel = true, LabelFormat = "#PERCENT{P0}" });

            chartAccounts = CreateChart("موجودی حساب‌ها");
            chartAccounts.Series.Add(new Series("حساب") { ChartType = SeriesChartType.Doughnut, IsValueShownAsLabel = true, LabelFormat = "#PERCENT{P0}" });

            chartsLayout.Controls.Add(chartCashFlow, 0, 0);
            chartsLayout.Controls.Add(chartExpense, 1, 0);
            chartsLayout.Controls.Add(chartAccounts, 2, 0);

            var groupChecks = new GroupBox { Text = "📄 چک‌های در جریان (تا ۳۰ روز آینده)", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText };
            dgvUpcomingChecks = new DataGridView { Dock = DockStyle.Fill };
            dgvUpcomingChecks.ApplyModernStyle();
            dgvUpcomingChecks.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgvUpcomingChecks.Columns[e.ColumnIndex].Name == "DueDate" && e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
                else if (dgvUpcomingChecks.Columns[e.ColumnIndex].Name == "Amount" && e.Value is decimal dec) { e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true); e.FormattingApplied = true; }
            };
            groupChecks.Controls.Add(dgvUpcomingChecks);

            mainLayout.Controls.Add(filterPanel, 0, 0); mainLayout.Controls.Add(kpiLayout, 0, 1);
            mainLayout.Controls.Add(chartsLayout, 0, 2); mainLayout.Controls.Add(groupChecks, 0, 3);
            Controls.Add(mainLayout);

            OnDateFilterChanged();
        }

        private Label CreateKpiCard(TableLayoutPanel parent, string title, Color color, int col)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5) };
            card.Paint += (s, e) => { ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid); };
            var lblTitle = new Label { Text = title, Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(9.5f, FontStyle.Bold), ForeColor = color };
            var lblValue = new Label { Text = "0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(14f, FontStyle.Bold), ForeColor = UIHelper.WarmText };
            card.Controls.Add(lblValue); card.Controls.Add(lblTitle); parent.Controls.Add(card, col, 0); return lblValue;
        }

        private Chart CreateChart(string title)
        {
            var chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.White };
            var area = new ChartArea("MainArea") { BackColor = Color.White };
            area.AxisX.MajorGrid.LineColor = Color.LightGray; area.AxisY.MajorGrid.LineColor = Color.LightGray;
            area.AxisX.LabelStyle.Font = UIHelper.PersianFont(8f); area.AxisY.LabelStyle.Font = UIHelper.PersianFont(8f);
            area.AxisY.LabelStyle.Format = "#,#,, 'M'";
            chart.ChartAreas.Add(area);
            chart.Legends.Add(new Legend("Legend1") { Docking = Docking.Bottom, Font = UIHelper.PersianFont(9f), Alignment = StringAlignment.Center });
            chart.Titles.Add(new Title(title, Docking.Top, UIHelper.PersianFont(11f, FontStyle.Bold), UIHelper.WarmText));
            return chart;
        }

        private void OnDateFilterChanged()
        {
            var pc = new PersianCalendar();
            DateTime now = DateTime.Now;
            if (cmbDateFilter.SelectedItem?.ToString() == "این هفته")
            {
                DateTime weekStart = now.AddDays(-(int)pc.GetDayOfWeek(now)).Date;
                txtFromDate.Text = DateHelper.ToPersianDate(weekStart); txtToDate.Text = DateHelper.ToPersianDate(now);
            }
            else if (cmbDateFilter.SelectedItem?.ToString() == "این ماه")
            {
                DateTime monthStart = pc.ToDateTime(pc.GetYear(now), pc.GetMonth(now), 1, 0, 0, 0, 0);
                txtFromDate.Text = DateHelper.ToPersianDate(monthStart); txtToDate.Text = DateHelper.ToPersianDate(now);
            }
            else if (cmbDateFilter.SelectedItem?.ToString() == "ماه گذشته")
            {
                DateTime lastMonth = now.AddMonths(-1);
                DateTime monthStart = pc.ToDateTime(pc.GetYear(lastMonth), pc.GetMonth(lastMonth), 1, 0, 0, 0, 0);
                DateTime monthEnd = pc.ToDateTime(pc.GetYear(lastMonth), pc.GetMonth(lastMonth), pc.GetDaysInMonth(pc.GetYear(lastMonth), pc.GetMonth(lastMonth)), 23, 59, 59, 0);
                txtFromDate.Text = DateHelper.ToPersianDate(monthStart); txtToDate.Text = DateHelper.ToPersianDate(monthEnd);
            }
            else if (cmbDateFilter.SelectedItem?.ToString() == "این سال")
            {
                DateTime yearStart = pc.ToDateTime(pc.GetYear(now), 1, 1, 0, 0, 0, 0);
                txtFromDate.Text = DateHelper.ToPersianDate(yearStart); txtToDate.Text = DateHelper.ToPersianDate(now);
            }
            txtFromDate.ReadOnly = cmbDateFilter.SelectedItem?.ToString() != "بازه دلخواه";
            txtToDate.ReadOnly = cmbDateFilter.SelectedItem?.ToString() != "بازه دلخواه";
        }

        private async Task LoadDataSafeAsync()
        {
            DateTime? fromDate = DateHelper.ParsePersianDate(txtFromDate.Text);
            DateTime? toDate = DateHelper.ParsePersianDate(txtToDate.Text);
            if (fromDate == null || toDate == null) { ModernMessageBox.ShowWarning("تاریخ شروع یا پایان معتبر نیست."); return; }

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var data = await _liquidityRepo.GetDashboardDataAsync(fromDate.Value, toDate.Value);
                lblTotalLiquidity.Text = NumberHelper.FormatThousands(data.TotalLiquidity, usePersianDigits: true);
                lblTotalIncome.Text = NumberHelper.FormatThousands(data.TotalIncome, usePersianDigits: true);
                lblTotalExpense.Text = NumberHelper.FormatThousands(data.TotalExpense, usePersianDigits: true);
                lblNetCashFlow.Text = NumberHelper.FormatThousands(data.NetCashFlow, usePersianDigits: true);
                lblNetCashFlow.ForeColor = data.NetCashFlow >= 0 ? UIHelper.ActionGreen : UIHelper.ActionRed;
                lblIncomeChecks.Text = NumberHelper.FormatThousands(data.TotalReceivableChecks, usePersianDigits: true);
                lblPayableChecks.Text = NumberHelper.FormatThousands(data.TotalPayableChecks, usePersianDigits: true);

                var dtTrend = await _liquidityRepo.GetCashFlowTrendAsync(fromDate.Value, toDate.Value);
                chartCashFlow.Series["دریافت"].Points.Clear(); chartCashFlow.Series["پرداخت"].Points.Clear();
                foreach (DataRow row in dtTrend.Rows)
                {
                    string dateLabel = DateHelper.ToPersianDate(Convert.ToDateTime(row["TxDate"]));
                    chartCashFlow.Series["دریافت"].Points.AddXY(dateLabel, Convert.ToDecimal(row["Income"]));
                    chartCashFlow.Series["پرداخت"].Points.AddXY(dateLabel, Convert.ToDecimal(row["Expense"]));
                }

                var dtExpense = await _liquidityRepo.GetExpenseBreakdownAsync(fromDate.Value, toDate.Value);
                chartExpense.Series["هزینه"].Points.Clear();
                Color[] colors = { UIHelper.WarmDanger, UIHelper.ActionRed, UIHelper.SectionOrange, UIHelper.SectionPurple, UIHelper.SectionTeal };
                int cIdx = 0;
                foreach (DataRow row in dtExpense.Rows)
                {
                    int pIdx = chartExpense.Series["هزینه"].Points.AddXY(row["Category"].ToString(), Convert.ToDecimal(row["TotalAmount"]));
                    chartExpense.Series["هزینه"].Points[pIdx].Color = colors[cIdx++ % colors.Length];
                }

                var dtAccounts = await _liquidityRepo.GetAccountBalancesAsync();
                chartAccounts.Series["حساب"].Points.Clear(); cIdx = 0;
                foreach (DataRow row in dtAccounts.Rows)
                {
                    int pIdx = chartAccounts.Series["حساب"].Points.AddXY(row["Title"].ToString(), Convert.ToDecimal(row["CurrentBalance"]));
                    chartAccounts.Series["حساب"].Points[pIdx].Color = colors[cIdx++ % colors.Length];
                }

                var dtChecks = await _liquidityRepo.GetUpcomingChecksAsync();
                dgvUpcomingChecks.DataSource = dtChecks;
            }, "در حال تحلیل داده‌های نقدینگی...");
        }
    }
}