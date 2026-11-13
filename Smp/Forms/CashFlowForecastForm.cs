using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace SMP.Forms
{
    public class CashFlowForecastForm : Form
    {
        private readonly string _connectionString;
        private readonly CashCheckRepository _checkRepo;
        private readonly BankAccountRepository _bankRepo;

        private Label lblCurrentLiquidity = null!, lblIncomingChecks = null!, lblOutgoingChecks = null!, lblProjectedBalance = null!;
        private Chart chartForecast = null!;
        private DataGridView dgvIncoming = null!, dgvOutgoing = null!;
        private TabControl tabControl = null!;

        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public CashFlowForecastForm(string connectionString)
        {
            _connectionString = connectionString;
            _checkRepo = new CashCheckRepository(connectionString);
            _bankRepo = new BankAccountRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("پیش‌بینی جریان نقدی (Cash Flow Forecast)", new Size(1300, 850));

            Shown += async (s, e) => await LoadDataAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _loadLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F)); // KPI Cards
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));   // Chart
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));   // Grid

            // ═══════ ۱. کارت‌های KPI ═══════
            var tblKPI = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            for (int i = 0; i < 4; i++) tblKPI.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            lblCurrentLiquidity = CreateKpiCard(tblKPI, "موجودی نقدی فعلی", "0", UIHelper.SectionBlue, 0);
            lblIncomingChecks = CreateKpiCard(tblKPI, "مجموع چک‌های دریافتی (آینده)", "0", UIHelper.ActionGreen, 1);
            lblOutgoingChecks = CreateKpiCard(tblKPI, "مجموع چک‌های پرداختی (آینده)", "0", UIHelper.ActionRed, 2);
            lblProjectedBalance = CreateKpiCard(tblKPI, "موجودی پیش‌بینی‌شده (پایان دوره)", "0", UIHelper.SectionPurple, 3);

            // ═══════ ۲. نمودار ═══════
            chartForecast = new Chart { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5), Padding = new Padding(5) };

            var chartArea = new ChartArea();
            chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisX.LabelStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold);
            chartArea.AxisY.LabelStyle.Font = UIHelper.PersianFont(9f);
            chartArea.AxisY.LabelStyle.Format = "#,#,, 'M'"; // میلیون
            chartForecast.ChartAreas.Add(chartArea);

            chartForecast.Legends.Add(new Legend("Legend1") { Docking = Docking.Bottom, Font = UIHelper.PersianFont(10f, FontStyle.Bold) });
            chartForecast.Titles.Add(new Title("روند پیش‌بینی موجودی نقدی (۶ ماه آینده)", Docking.Top, UIHelper.PersianFont(13f, FontStyle.Bold), UIHelper.WarmText));

            chartForecast.Series.Add(new Series("موجودی نقدی") { ChartType = SeriesChartType.SplineArea, Color = Color.FromArgb(100, UIHelper.SectionBlue), BorderWidth = 3, IsValueShownAsLabel = false });
            chartForecast.Series.Add(new Series("چک‌های دریافتی") { ChartType = SeriesChartType.Column, Color = UIHelper.ActionGreen, IsValueShownAsLabel = false });
            chartForecast.Series.Add(new Series("چک‌های پرداختی") { ChartType = SeriesChartType.Column, Color = UIHelper.ActionRed, IsValueShownAsLabel = false });

            // ═══════ ۳. جدول چک‌ها ═══════
            var pnlGrid = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5) };
            tabControl = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
            ModernLayoutHelper.StyleModernTabControl(tabControl);

            var tabIncoming = new TabPage("💳 چک‌های دریافتی پیش‌رو");
            dgvIncoming = CreateGrid();
            tabIncoming.Controls.Add(dgvIncoming);

            var tabOutgoing = new TabPage("🧾 چک‌های پرداختی پیش‌رو");
            dgvOutgoing = CreateGrid();
            tabOutgoing.Controls.Add(dgvOutgoing);

            tabControl.TabPages.Add(tabIncoming);
            tabControl.TabPages.Add(tabOutgoing);
            pnlGrid.Controls.Add(tabControl);

            mainLayout.Controls.Add(tblKPI, 0, 0);
            mainLayout.Controls.Add(chartForecast, 0, 1);
            mainLayout.Controls.Add(pnlGrid, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private Label CreateKpiCard(TableLayoutPanel parent, string title, string value, Color color, int col)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5) };
            pnl.Paint += (s, e) => { ControlPaint.DrawBorder(e.Graphics, pnl.ClientRectangle, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid); };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(5) };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));

            var lblTitle = new Label { Text = title, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(10f, FontStyle.Bold), ForeColor = color, TextAlign = ContentAlignment.MiddleCenter };
            var lblValue = new Label { Text = value, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(14f, FontStyle.Bold), ForeColor = UIHelper.WarmText, TextAlign = ContentAlignment.MiddleCenter };

            layout.Controls.Add(lblTitle, 0, 0);
            layout.Controls.Add(lblValue, 0, 1);
            pnl.Controls.Add(layout);

            parent.Controls.Add(pnl, col, 0);
            return lblValue;
        }

        private DataGridView CreateGrid()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                RightToLeft = RightToLeft.Yes
            };
            dgv.ApplyModernStyle();

            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CheckNumber", HeaderText = "شماره چک", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BankName", HeaderText = "بانک", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RecipientName", HeaderText = "طرف حساب", FillWeight = 20 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DueDate", HeaderText = "تاریخ سررسید", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PaymentScopeDisplay", HeaderText = "نوع پرداخت", FillWeight = 20 });

            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgv.Columns[e.ColumnIndex].DataPropertyName == "Amount" && e.Value is decimal d)
                {
                    e.Value = NumberHelper.FormatThousands(d, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            };

            return dgv;
        }

        private async Task LoadDataAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var accounts = await _bankRepo.GetActiveAsync();
                    var checks = await _checkRepo.GetAllAsync();

                    if (IsDisposed) return;

                    // ۱. موجودی فعلی
                    decimal currentLiquidity = accounts.Sum(a => a.CurrentBalance);
                    lblCurrentLiquidity.Text = NumberHelper.FormatThousands(currentLiquidity, usePersianDigits: true);

                    // ۲. فیلتر چک‌های در جریان (پاس نشده)
                    var pendingChecks = checks.Where(c => c.Status == "در جریان" && c.DueDate.Date >= DateTime.Today).ToList();

                    var incomingChecks = pendingChecks.Where(c => c.CheckType == "دریافتی").OrderBy(c => c.DueDate).ToList();
                    var outgoingChecks = pendingChecks.Where(c => c.CheckType == "پرداختی").OrderBy(c => c.DueDate).ToList();

                    decimal totalIncoming = incomingChecks.Sum(c => c.Amount);
                    decimal totalOutgoing = outgoingChecks.Sum(c => c.Amount);

                    lblIncomingChecks.Text = NumberHelper.FormatThousands(totalIncoming, usePersianDigits: true);
                    lblOutgoingChecks.Text = NumberHelper.FormatThousands(totalOutgoing, usePersianDigits: true);

                    // ۳. پر کردن جدول‌ها
                    var incomingView = incomingChecks.Select(c => new
                    {
                        c.CheckNumber,
                        c.BankName,
                        c.RecipientName,
                        DueDate = DateHelper.ToPersianDate(c.DueDate),
                        Amount = c.Amount,
                        PaymentScopeDisplay = string.IsNullOrWhiteSpace(c.PaymentScope) ? "---" : c.PaymentScope
                    }).ToList();

                    var outgoingView = outgoingChecks.Select(c => new
                    {
                        c.CheckNumber,
                        c.BankName,
                        c.RecipientName,
                        DueDate = DateHelper.ToPersianDate(c.DueDate),
                        Amount = c.Amount,
                        PaymentScopeDisplay = string.IsNullOrWhiteSpace(c.PaymentScope) ? "---" : c.PaymentScope
                    }).ToList();

                    dgvIncoming.DataSource = incomingView;
                    dgvOutgoing.DataSource = outgoingView;

                    // ۴. محاسبه نمودار (۶ ماه آینده)
                    chartForecast.Series["موجودی نقدی"].Points.Clear();
                    chartForecast.Series["چک‌های دریافتی"].Points.Clear();
                    chartForecast.Series["چک‌های پرداختی"].Points.Clear();

                    var pc = new PersianCalendar();
                    DateTime today = DateTime.Today;
                    decimal runningBalance = currentLiquidity;

                    for (int i = 0; i < 6; i++)
                    {
                        DateTime monthStart = pc.AddMonths(today, i);
                        int year = pc.GetYear(monthStart);
                        int month = pc.GetMonth(monthStart);
                        string monthName = TranslationHelper.GetMonthName(month);

                        // چک‌های این ماه
                        var monthIncoming = incomingChecks.Where(c => pc.GetYear(c.DueDate) == year && pc.GetMonth(c.DueDate) == month).Sum(c => c.Amount);
                        var monthOutgoing = outgoingChecks.Where(c => pc.GetYear(c.DueDate) == year && pc.GetMonth(c.DueDate) == month).Sum(c => c.Amount);

                        // محاسبه موجودی پایان ماه
                        runningBalance = runningBalance + monthIncoming - monthOutgoing;

                        // افزودن به نمودار
                        chartForecast.Series["موجودی نقدی"].Points.AddXY(monthName, runningBalance);
                        chartForecast.Series["چک‌های دریافتی"].Points.AddXY(monthName, monthIncoming);
                        chartForecast.Series["چک‌های پرداختی"].Points.AddXY(monthName, monthOutgoing);
                    }

                    lblProjectedBalance.Text = NumberHelper.FormatThousands(runningBalance, usePersianDigits: true);
                    lblProjectedBalance.ForeColor = runningBalance >= 0 ? UIHelper.SectionPurple : UIHelper.ActionRed;

                }, "در حال محاسبه پیش‌بینی جریان نقدی...");

                if (!success && !IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات.");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "CashFlowForecastForm.LoadDataAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }
    }
}