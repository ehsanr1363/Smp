using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace SMP.Forms
{
    public class LiquidityDashboardForm : Form
    {
        private readonly LiquidityDashboardRepository _repository;

        private ComboBox cmbDateRange = null!;
        private TextBox txtFromDate = null!, txtToDate = null!;
        private Button btnFilter = null!, btnOpenLedger = null!;

        private Label lblTotalBalance = null!, lblPeriodReceipts = null!, lblPeriodPayments = null!;
        private Label lblNetCashFlow = null!, lblAdminExpense = null!, lblDirectPurchaseExpense = null!;
        private Label lblInformalContractor = null!, lblFormalContractor = null!;

        private Chart chartTrend = null!;
        private FlowLayoutPanel pnlAccountsList = null!;

        private Label lblUnpaidTotal = null!;
        private Label lblUnpaidCount = null!;
        private FlowLayoutPanel pnlUnpaidCards = null!;
        private Button btnViewUnpaidDetails = null!;
        private List<CashCheck> _unpaidChecks = new();

        private Label lblTotalPettyCash = null!;
        private FlowLayoutPanel pnlPettyCashCards = null!;
        private Button btnSettlePettyCash = null!;

        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public LiquidityDashboardForm(string connectionString)
        {
            _repository = new LiquidityDashboardRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("داشبورد تحلیلی نقدینگی", new Size(1450, 1000));

            Load += async (s, e) => await LoadDashboardDataAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _loadLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

            var pnlFilter = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5, 10, 5, 5) };

            var lblRange = new Label { Text = "بازه زمانی:", Font = UIHelper.PersianFont(10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(10, 8, 5, 5) };
            cmbDateRange = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(10f) };
            cmbDateRange.Items.AddRange(new[] { "همه تراکنش‌ها", "این ماه", "بازه دلخواه" });
            cmbDateRange.SelectedIndex = 0;
            cmbDateRange.SelectedIndexChanged += (s, e) =>
            {
                bool isCustom = cmbDateRange.SelectedItem.ToString() == "بازه دلخواه";
                txtFromDate.Enabled = isCustom;
                txtToDate.Enabled = isCustom;
                if (!isCustom) { txtFromDate.Text = ""; txtToDate.Text = ""; }
            };

            var lblFrom = new Label { Text = "از:", Font = UIHelper.PersianFont(10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(15, 8, 5, 5) };
            txtFromDate = new TextBox { Width = 100, Text = "", Font = UIHelper.PersianFont(10f), Enabled = false };
            DateHelper.ApplyDateMask(txtFromDate);

            var lblTo = new Label { Text = "تا:", Font = UIHelper.PersianFont(10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(10, 8, 5, 5) };
            txtToDate = new TextBox { Width = 100, Text = "", Font = UIHelper.PersianFont(10f), Enabled = false };
            DateHelper.ApplyDateMask(txtToDate);

            btnFilter = UIHelper.CreateButton("🔍 اعمال فیلتر", UIHelper.ActionBlue, 120, 35);
            btnFilter.Click += async (s, e) => await LoadDashboardDataAsync();

            btnOpenLedger = UIHelper.CreateButton("📒 دفتر روزنامه جامع", UIHelper.SectionTeal, 150, 35);
            btnOpenLedger.Click += (s, e) =>
            {
                using var form = new ComprehensiveLedgerForm(AppConfig.ConnectionString);
                form.ShowDialog(this);
            };

            pnlFilter.Controls.Add(btnFilter);
            pnlFilter.Controls.Add(btnOpenLedger);
            pnlFilter.Controls.Add(txtToDate);
            pnlFilter.Controls.Add(lblTo);
            pnlFilter.Controls.Add(txtFromDate);
            pnlFilter.Controls.Add(lblFrom);
            pnlFilter.Controls.Add(cmbDateRange);
            pnlFilter.Controls.Add(lblRange);

            var tblKPIs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
            for (int i = 0; i < 4; i++) tblKPIs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tblKPIs.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tblKPIs.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            lblTotalBalance = CreateKpiCard(tblKPIs, "موجودی کل نقدی", "0", UIHelper.SectionBlue, 0, 0);
            lblPeriodReceipts = CreateKpiCard(tblKPIs, "دریافتی دوره", "0", UIHelper.ActionGreen, 1, 0);
            lblPeriodPayments = CreateKpiCard(tblKPIs, "کل پرداختی دوره", "0", UIHelper.ActionRed, 2, 0);
            lblNetCashFlow = CreateKpiCard(tblKPIs, "جریان نقدی خالص", "0", UIHelper.SectionPurple, 3, 0);

            lblAdminExpense = CreateKpiCard(tblKPIs, "هزینه‌های اداری", "0", UIHelper.WarmText, 0, 1);
            lblDirectPurchaseExpense = CreateKpiCard(tblKPIs, "خرید مستقیم پروژه", "0", UIHelper.SectionOrange, 1, 1);
            lblInformalContractor = CreateKpiCard(tblKPIs, "پیمانکار بدون قرارداد", "0", UIHelper.ActionAmber, 2, 1);
            lblFormalContractor = CreateKpiCard(tblKPIs, "پیمانکار دارای قرارداد", "0", UIHelper.SectionDark, 3, 1);

            var tblCharts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            // ✅ اصلاح شد: نمودار کوچکتر (40%) و لیست بانک‌ها بزرگتر (60%)
            tblCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F)); // Chart (Right)
            tblCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F)); // Accounts List (Left)

            chartTrend = CreateChart("روند دریافت و پرداخت", tblCharts, 0);

            var pnlAccounts = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5), Padding = new Padding(5) };
            var lblAccountsTitle = new Label { Text = "موجودی تفکیکی حساب‌ها و صندوق‌ها", Dock = DockStyle.Top, Font = UIHelper.PersianFont(12f, FontStyle.Bold), ForeColor = UIHelper.WarmText, TextAlign = ContentAlignment.MiddleCenter, Height = 30 };

            // ✅ اصلاح شد: چیدمان کارت‌ها به صورت راست‌به‌چپ و دو ستونه با شکستن خط (WrapContents)
            pnlAccountsList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(10)
            };

            pnlAccounts.Controls.Add(pnlAccountsList);
            pnlAccounts.Controls.Add(lblAccountsTitle);
            tblCharts.Controls.Add(pnlAccounts, 1, 0);

            var pnlUnpaidWidget = CreateUnpaidChecksWidget();
            var pnlPettyCashWidget = CreatePettyCashWidget();

            mainLayout.Controls.Add(pnlFilter, 0, 0);
            mainLayout.Controls.Add(tblKPIs, 0, 1);
            mainLayout.Controls.Add(tblCharts, 0, 2);
            mainLayout.Controls.Add(pnlUnpaidWidget, 0, 3);
            mainLayout.Controls.Add(pnlPettyCashWidget, 0, 4);

            this.Controls.Add(mainLayout);
        }

        private Label CreateKpiCard(TableLayoutPanel parent, string title, string value, Color color, int col, int row)
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

            parent.Controls.Add(pnl, col, row);
            return lblValue;
        }

        private Chart CreateChart(string title, TableLayoutPanel parent, int col)
        {
            var chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5), Padding = new Padding(5) };

            var chartArea = new ChartArea();
            chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisX.LabelStyle.Font = UIHelper.PersianFont(9f, FontStyle.Bold);
            chartArea.AxisY.LabelStyle.Font = UIHelper.PersianFont(8f);
            chartArea.AxisX.LabelStyle.Angle = 45;
            chart.ChartAreas.Add(chartArea);

            var legend = new Legend { Docking = Docking.Bottom, Font = UIHelper.PersianFont(9f, FontStyle.Bold), Alignment = StringAlignment.Center };
            chart.Legends.Add(legend);

            var chartTitle = new Title(title, Docking.Top, UIHelper.PersianFont(12f, FontStyle.Bold), UIHelper.WarmText);
            chart.Titles.Add(chartTitle);

            parent.Controls.Add(chart, col, 0);
            return chart;
        }

        private Panel CreateUnpaidChecksWidget()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5) };
            pnl.Paint += (s, e) => { ControlPaint.DrawBorder(e.Graphics, pnl.ClientRectangle, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid); };

            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(10, 5, 10, 5) };
            var lblHeaderTitle = new Label { Text = "⚠️ چک‌های پاس‌نشده (در جریان و برگشتی)", Dock = DockStyle.Right, Font = UIHelper.PersianFont(12f, FontStyle.Bold), ForeColor = UIHelper.ActionRed, TextAlign = ContentAlignment.MiddleRight, AutoSize = true };

            btnViewUnpaidDetails = UIHelper.CreateButton("🔍 مشاهده لیست جزئیات", UIHelper.SectionBlue, 160, 30);
            btnViewUnpaidDetails.Dock = DockStyle.Left;
            btnViewUnpaidDetails.Enabled = false;
            btnViewUnpaidDetails.Click += (s, e) =>
            {
                if (_unpaidChecks.Any())
                {
                    using var form = new UnpaidChecksDetailForm(_unpaidChecks);
                    form.ShowDialog(this);
                }
            };

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(btnViewUnpaidDetails);

            var pnlTotal = new Panel { Dock = DockStyle.Right, Width = 250, Padding = new Padding(10) };
            pnlTotal.Paint += (s, e) => { ControlPaint.DrawBorder(e.Graphics, pnlTotal.ClientRectangle, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid); };

            var lblTotalCaption = new Label { Text = "مجموع کل تعهدات", Dock = DockStyle.Top, Height = 25, Font = UIHelper.PersianFont(10f), ForeColor = UIHelper.WarmText, TextAlign = ContentAlignment.MiddleCenter };
            lblUnpaidTotal = new Label { Text = "0", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(18f, FontStyle.Bold), ForeColor = UIHelper.ActionRed, TextAlign = ContentAlignment.MiddleCenter };
            lblUnpaidCount = new Label { Text = "0 چک", Dock = DockStyle.Bottom, Height = 20, Font = UIHelper.PersianFont(10f), ForeColor = UIHelper.WarmText, TextAlign = ContentAlignment.MiddleCenter };

            pnlTotal.Controls.Add(lblUnpaidTotal);
            pnlTotal.Controls.Add(lblUnpaidCount);
            pnlTotal.Controls.Add(lblTotalCaption);

            pnlUnpaidCards = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, BackColor = Color.FromArgb(248, 249, 250), Padding = new Padding(10) };

            pnl.Controls.Add(pnlUnpaidCards);
            pnl.Controls.Add(pnlTotal);
            pnl.Controls.Add(pnlHeader);

            return pnl;
        }

        private Panel CreatePettyCashWidget()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5) };
            pnl.Paint += (s, e) => { ControlPaint.DrawBorder(e.Graphics, pnl.ClientRectangle, UIHelper.SectionOrange, 2, ButtonBorderStyle.Solid, UIHelper.SectionOrange, 2, ButtonBorderStyle.Solid, UIHelper.SectionOrange, 2, ButtonBorderStyle.Solid, UIHelper.SectionOrange, 2, ButtonBorderStyle.Solid); };

            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(10, 5, 10, 5) };
            var lblHeaderTitle = new Label { Text = "💰 مدیریت تنخواه‌ها (وجوه در دسترس)", Dock = DockStyle.Right, Font = UIHelper.PersianFont(12f, FontStyle.Bold), ForeColor = UIHelper.SectionOrange, TextAlign = ContentAlignment.MiddleRight, AutoSize = true };

            btnSettlePettyCash = UIHelper.CreateButton("🔍 تسویه و مدیریت تنخواه", UIHelper.ActionGreen, 180, 30);
            btnSettlePettyCash.Dock = DockStyle.Left;
            btnSettlePettyCash.Enabled = false;
            btnSettlePettyCash.Click += (s, e) =>
            {
                using var form = new AccountTransferForm(AppConfig.ConnectionString);
                form.ShowDialog(this);
                _ = LoadDashboardDataAsync();
            };

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(btnSettlePettyCash);

            var pnlTotal = new Panel { Dock = DockStyle.Right, Width = 250, Padding = new Padding(10) };
            pnlTotal.Paint += (s, e) => { ControlPaint.DrawBorder(e.Graphics, pnlTotal.ClientRectangle, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid); };

            var lblTotalCaption = new Label { Text = "کل پول در دست تنخواه‌داران", Dock = DockStyle.Top, Height = 25, Font = UIHelper.PersianFont(10f), ForeColor = UIHelper.WarmText, TextAlign = ContentAlignment.MiddleCenter };
            lblTotalPettyCash = new Label { Text = "0", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(18f, FontStyle.Bold), ForeColor = UIHelper.SectionOrange, TextAlign = ContentAlignment.MiddleCenter };

            pnlTotal.Controls.Add(lblTotalPettyCash);
            pnlTotal.Controls.Add(lblTotalCaption);

            pnlPettyCashCards = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, BackColor = Color.FromArgb(255, 252, 248), Padding = new Padding(10) };

            pnl.Controls.Add(pnlPettyCashCards);
            pnl.Controls.Add(pnlTotal);
            pnl.Controls.Add(pnlHeader);

            return pnl;
        }

        private async Task LoadDashboardDataAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                DateTime fromDate = new DateTime(1753, 1, 1);
                DateTime toDate = new DateTime(9999, 12, 31, 23, 59, 59);
                string selectedRange = cmbDateRange.SelectedItem?.ToString() ?? "همه تراکنش‌ها";

                if (selectedRange == "بازه دلخواه")
                {
                    fromDate = DateHelper.ParsePersianDate(txtFromDate.Text) ?? new DateTime(1753, 1, 1);
                    toDate = DateHelper.ParsePersianDate(txtToDate.Text) ?? new DateTime(9999, 12, 31, 23, 59, 59);
                }
                else if (selectedRange == "این ماه")
                {
                    var pc = new PersianCalendar();
                    int currentYear = pc.GetYear(DateTime.Now);
                    int currentMonth = pc.GetMonth(DateTime.Now);

                    int daysInMonth = pc.GetDaysInMonth(currentYear, currentMonth);
                    fromDate = pc.ToDateTime(currentYear, currentMonth, 1, 0, 0, 0, 0);
                    toDate = pc.ToDateTime(currentYear, currentMonth, daysInMonth, 23, 59, 59, 0);
                }

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var ds = await _repository.GetDashboardDataAsync(fromDate, toDate);

                    if (IsDisposed || ds.Tables.Count < 5) return;

                    var kpiRow = ds.Tables[0].Rows[0];
                    decimal totalLiquidity = Convert.ToDecimal(kpiRow["TotalLiquidity"]);
                    decimal totalIncome = Convert.ToDecimal(kpiRow["TotalIncome"]);
                    decimal totalExpense = Convert.ToDecimal(kpiRow["TotalExpense"]);

                    lblTotalBalance.Text = NumberHelper.FormatThousands(totalLiquidity, usePersianDigits: true);
                    lblPeriodReceipts.Text = NumberHelper.FormatThousands(totalIncome, usePersianDigits: true);
                    lblPeriodPayments.Text = NumberHelper.FormatThousands(totalExpense, usePersianDigits: true);
                    lblNetCashFlow.Text = NumberHelper.FormatThousands(totalIncome - totalExpense, usePersianDigits: true);

                    lblAdminExpense.Text = NumberHelper.FormatThousands(Convert.ToDecimal(kpiRow["AdminExp"]), usePersianDigits: true);
                    lblDirectPurchaseExpense.Text = NumberHelper.FormatThousands(Convert.ToDecimal(kpiRow["DirectExp"]), usePersianDigits: true);
                    lblInformalContractor.Text = NumberHelper.FormatThousands(Convert.ToDecimal(kpiRow["InformalExp"]), usePersianDigits: true);
                    lblFormalContractor.Text = NumberHelper.FormatThousands(Convert.ToDecimal(kpiRow["FormalExp"]), usePersianDigits: true);

                    chartTrend.Series.Clear();
                    var sTrendIn = new Series("دریافت") { ChartType = SeriesChartType.Line, Color = Color.Green, BorderWidth = 3 };
                    var sTrendOut = new Series("پرداخت") { ChartType = SeriesChartType.Line, Color = Color.Red, BorderWidth = 3 };
                    chartTrend.Series.Add(sTrendIn);
                    chartTrend.Series.Add(sTrendOut);

                    foreach (DataRow row in ds.Tables[1].Rows)
                    {
                        DateTime date = Convert.ToDateTime(row["TxDate"]);
                        string dayStr = DateHelper.ToPersianDate(date);
                        decimal inc = Convert.ToDecimal(row["TotalIncome"]);
                        decimal exp = Convert.ToDecimal(row["TotalExpense"]);
                        sTrendIn.Points.AddXY(dayStr, inc);
                        sTrendOut.Points.AddXY(dayStr, exp);
                    }

                    pnlAccountsList.Controls.Clear();
                    foreach (DataRow row in ds.Tables[2].Rows)
                    {
                        // ✅ اصلاح شد: خواندن عنوان حساب (جاری/پشتیبان) از دیتابیس
                        string accountTitle = row["Title"].ToString() ?? "";
                        string bankCode = row["BankCode"].ToString() ?? "";
                        string bankName = BankHelper.GetBankNameByCode(bankCode);
                        if (string.IsNullOrWhiteSpace(bankName)) bankName = "نامشخص";

                        string displayTitle = string.IsNullOrWhiteSpace(accountTitle) ? bankName : $"{accountTitle} ({bankName})";

                        string accNo = row["AccountNumber"].ToString() ?? "";
                        decimal balance = Convert.ToDecimal(row["CurrentBalance"]);

                        // ✅ اصلاح شد: عرض کارت‌ها به صورت ثابت (مثلاً 320 پیکسل) تا در دو ستون جا شوند
                        var card = new Panel { Height = 75, Width = 330, BackColor = Color.White, Margin = new Padding(5) };
                        card.Paint += (s, e) => { ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid, UIHelper.WarmBorder, 1, ButtonBorderStyle.Solid); };

                        var lblBank = new Label { Text = displayTitle, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 5, 10, 0) };
                        var lblAccNo = new Label { Text = accNo, Font = UIHelper.PersianFont(9f), ForeColor = Color.Gray, Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 10, 0) };
                        var lblBalance = new Label { Text = NumberHelper.FormatThousands(balance, usePersianDigits: true), Font = UIHelper.PersianFont(12f, FontStyle.Bold), ForeColor = balance > 0 ? UIHelper.ActionGreen : (balance < 0 ? UIHelper.ActionRed : UIHelper.WarmText), Dock = DockStyle.Left, Width = 130, TextAlign = ContentAlignment.MiddleCenter };

                        card.Controls.Add(lblAccNo); card.Controls.Add(lblBank); card.Controls.Add(lblBalance);
                        pnlAccountsList.Controls.Add(card);
                    }

                    _unpaidChecks.Clear();
                    foreach (DataRow row in ds.Tables[3].Rows)
                    {
                        _unpaidChecks.Add(new CashCheck
                        {
                            Id = Convert.ToInt32(row["Id"]),
                            CheckNumber = row["CheckNumber"].ToString() ?? "",
                            DueDate = Convert.ToDateTime(row["DueDate"]),
                            Amount = Convert.ToDecimal(row["Amount"]),
                            CheckType = row["CheckType"].ToString() ?? "",
                            Status = row["Status"].ToString() ?? "",
                            RecipientName = row["RecipientName"].ToString() ?? "",
                            BankName = row["BankName"].ToString() ?? "",
                            Description = row["Description"].ToString() ?? ""
                        });
                    }

                    if (!_unpaidChecks.Any())
                    {
                        lblUnpaidTotal.Text = "0"; lblUnpaidTotal.ForeColor = UIHelper.ActionGreen;
                        lblUnpaidCount.Text = "هیچ چک پاس‌نشده‌ای وجود ندارد"; lblUnpaidCount.ForeColor = UIHelper.ActionGreen;
                        btnViewUnpaidDetails.Enabled = false; pnlUnpaidCards.Controls.Clear();
                    }
                    else
                    {
                        decimal totalUnpaidAmount = _unpaidChecks.Sum(c => c.Amount);
                        lblUnpaidTotal.Text = NumberHelper.FormatThousands(totalUnpaidAmount, usePersianDigits: true); lblUnpaidTotal.ForeColor = UIHelper.ActionRed;
                        lblUnpaidCount.Text = $"{_unpaidChecks.Count} چک در انتظار"; lblUnpaidCount.ForeColor = UIHelper.WarmText;
                        btnViewUnpaidDetails.Enabled = true;

                        pnlUnpaidCards.Controls.Clear();
                        var groupedByDate = _unpaidChecks.GroupBy(c => c.DueDate.Date).Select(g => new { RawDate = g.Key, PersianDate = DateHelper.ToPersianDate(g.Key), Count = g.Count(), Sum = g.Sum(c => c.Amount) }).OrderBy(g => g.RawDate).ToList();
                        DateTime today = DateTime.Now.Date;

                        foreach (var group in groupedByDate)
                        {
                            Color cardColor = group.RawDate < today ? Color.FromArgb(254, 226, 226) : (group.RawDate <= today.AddDays(7) ? Color.FromArgb(255, 237, 213) : Color.FromArgb(219, 234, 254));
                            Color textColor = group.RawDate < today ? UIHelper.ActionRed : (group.RawDate <= today.AddDays(7) ? UIHelper.SectionOrange : UIHelper.SectionBlue);

                            var dateCard = new Panel { Width = 140, Height = 110, BackColor = cardColor, Margin = new Padding(5) };
                            dateCard.Paint += (s, e) => { ControlPaint.DrawBorder(e.Graphics, dateCard.ClientRectangle, textColor, 1, ButtonBorderStyle.Solid, textColor, 1, ButtonBorderStyle.Solid, textColor, 1, ButtonBorderStyle.Solid, textColor, 1, ButtonBorderStyle.Solid); };

                            var lblDate = new Label { Text = group.PersianDate, Dock = DockStyle.Top, Height = 25, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = textColor, TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(0, 5, 0, 0) };
                            var lblSum = new Label { Text = NumberHelper.FormatThousands(group.Sum, usePersianDigits: true), Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = Color.Black, TextAlign = ContentAlignment.MiddleCenter };
                            var lblStatus = new Label { Text = $"{group.Count} چک", Dock = DockStyle.Bottom, Height = 25, Font = UIHelper.PersianFont(8.5f), ForeColor = textColor, TextAlign = ContentAlignment.MiddleCenter };

                            dateCard.Controls.Add(lblSum); dateCard.Controls.Add(lblDate); dateCard.Controls.Add(lblStatus);
                            pnlUnpaidCards.Controls.Add(dateCard);
                        }
                    }

                    var pettyCashList = new List<CompanyBankAccount>();
                    foreach (DataRow row in ds.Tables[4].Rows)
                    {
                        pettyCashList.Add(new CompanyBankAccount
                        {
                            Id = Convert.ToInt32(row["Id"]),
                            Title = row["Title"].ToString() ?? "",
                            CurrentBalance = Convert.ToDecimal(row["CurrentBalance"]),
                            CreatedAt = Convert.ToDateTime(row["LastActivityDate"])
                        });
                    }

                    if (!pettyCashList.Any())
                    {
                        lblTotalPettyCash.Text = "0";
                        lblTotalPettyCash.ForeColor = UIHelper.ActionGreen;
                        btnSettlePettyCash.Enabled = false;
                        pnlPettyCashCards.Controls.Clear();
                    }
                    else
                    {
                        decimal totalPettyCash = pettyCashList.Sum(pc => pc.CurrentBalance);
                        lblTotalPettyCash.Text = NumberHelper.FormatThousands(totalPettyCash, usePersianDigits: true);
                        lblTotalPettyCash.ForeColor = UIHelper.SectionOrange;
                        btnSettlePettyCash.Enabled = true;

                        pnlPettyCashCards.Controls.Clear();
                        DateTime today = DateTime.Now.Date;

                        foreach (var pc in pettyCashList)
                        {
                            int daysSinceLastActivity = (today - pc.CreatedAt.Date).Days;

                            Color cardColor = UIHelper.WarmSuccessBackground;
                            Color textColor = UIHelper.ActionGreen;
                            string statusText = "عادی";

                            if (daysSinceLastActivity > 30) { cardColor = UIHelper.WarmErrorBackground; textColor = UIHelper.ActionRed; statusText = "معوق (بیش از ۱ ماه)"; }
                            else if (daysSinceLastActivity > 7) { cardColor = UIHelper.WarmWarningBackground; textColor = UIHelper.SectionOrange; statusText = "نیاز به تسویه"; }

                            var card = new Panel { Width = 220, Height = 110, BackColor = cardColor, Margin = new Padding(5) };
                            card.Paint += (s, e) => { ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle, textColor, 1, ButtonBorderStyle.Solid, textColor, 1, ButtonBorderStyle.Solid, textColor, 1, ButtonBorderStyle.Solid, textColor, 1, ButtonBorderStyle.Solid); };

                            var lblTitle = new Label { Text = pc.Title, Dock = DockStyle.Top, Height = 25, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(0, 5, 0, 0) };
                            var lblBalance = new Label { Text = NumberHelper.FormatThousands(pc.CurrentBalance, usePersianDigits: true), Dock = DockStyle.Fill, Font = UIHelper.PersianFont(14f, FontStyle.Bold), ForeColor = textColor, TextAlign = ContentAlignment.MiddleCenter };
                            var lblStatus = new Label { Text = statusText, Dock = DockStyle.Bottom, Height = 25, Font = UIHelper.PersianFont(9f, FontStyle.Bold), ForeColor = textColor, TextAlign = ContentAlignment.MiddleCenter };

                            card.Controls.Add(lblBalance); card.Controls.Add(lblTitle); card.Controls.Add(lblStatus);
                            pnlPettyCashCards.Controls.Add(card);
                        }
                    }

                }, "در حال بارگذاری داشبورد نقدینگی...");

            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "LiquidityDashboardForm.LoadDashboardDataAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری داشبورد نقدینگی:\n" + ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }
    }
}