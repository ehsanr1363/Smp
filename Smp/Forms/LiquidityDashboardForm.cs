using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;
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

namespace Smp.Forms;

public class LiquidityDashboardForm : Form
{
    private readonly LiquidityDashboardRepository _repository;

    private ModernComboBox cmbDateRange = null!;
    private ModernTextBox txtFromDate = null!, txtToDate = null!;
    private ModernButton btnFilter = null!, btnOpenLedger = null!;

    private ModernStatCard tileTotalBalance = null!, tilePeriodReceipts = null!, tilePeriodPayments = null!, tileNetCashFlow = null!;
    private ModernStatCard tileAdminExpense = null!, tileDirectPurchase = null!, tileInformalContractor = null!, tileFormalContractor = null!;

    private Chart chartTrend = null!;
    private FlowLayoutPanel pnlAccountsList = null!;

    private Label lblUnpaidTotal = null!;
    private Label lblUnpaidCount = null!;
    private FlowLayoutPanel pnlUnpaidCards = null!;
    private ModernButton btnViewUnpaidDetails = null!;
    private List<CashCheck> _unpaidChecks = new();
    private readonly ToolTip _cardTip = new();

    private Label lblTotalPettyCash = null!;
    private FlowLayoutPanel pnlPettyCashCards = null!;
    private ModernButton btnSettlePettyCash = null!;

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
        if (disposing) { try { _cardTip.Dispose(); } catch { } try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "LiquidityDashboardForm"); } }
        base.Dispose(disposing);
    }

    // ═══════════════════════════════════════════════════════════
    //  ساخت رابط کاربری
    // ═══════════════════════════════════════════════════════════
    private void BuildUI()
    {
        var mainLayout = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(14, 8, 14, 10), BackColor = ModernTheme.Background };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 176F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 37F));   // چک‌های پاس‌نشده: جای دو ردیف برچسب
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

        mainLayout.Controls.Add(BuildFilterBar(), 0, 0);
        mainLayout.Controls.Add(BuildKpiGrid(), 0, 1);
        mainLayout.Controls.Add(BuildChartsRow(), 0, 2);
        mainLayout.Controls.Add(CreateUnpaidChecksWidget(), 0, 3);
        mainLayout.Controls.Add(CreatePettyCashWidget(), 0, 4);

        Controls.Add(mainLayout);
    }

    private Control BuildFilterBar()
    {
        var pnlFilter = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(4, 8, 4, 4),
            BackColor = Color.Transparent
        };

        Label MakeCaption(string text, int width) => new Label
        {
            Text = text,
            Width = width,
            Height = ModernTheme.InputHeight,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
            ForeColor = ModernTheme.Text,
            BackColor = Color.Transparent,
            Margin = new Padding(6, 0, 2, 0)
        };

        var lblRange = MakeCaption("بازه زمانی:", 90);
        cmbDateRange = ModernUI.Combo();
        cmbDateRange.Width = 150;
        cmbDateRange.Margin = new Padding(4, 0, 4, 0);
        cmbDateRange.Items.AddRange(new object[] { "همه تراکنش‌ها", "این ماه", "بازه دلخواه" });
        cmbDateRange.SelectedIndex = 0;
        cmbDateRange.SelectedIndexChanged += (s, e) =>
        {
            bool isCustom = cmbDateRange.SelectedItem?.ToString() == "بازه دلخواه";
            txtFromDate.Enabled = isCustom;
            txtToDate.Enabled = isCustom;
            if (!isCustom) { txtFromDate.Text = ""; txtToDate.Text = ""; }
        };

        var lblFrom = MakeCaption("از:", 36);
        txtFromDate = MakeDateInput();

        var lblTo = MakeCaption("تا:", 36);
        txtToDate = MakeDateInput();

        btnFilter = ModernButton.Create("🔍 اعمال فیلتر", ModernTheme.Primary, 140, ModernTheme.InputHeight);
        btnFilter.Margin = new Padding(14, 0, 4, 0);
        btnFilter.Click += async (s, e) => await LoadDashboardDataAsync();

        btnOpenLedger = ModernButton.Create("📒 دفتر روزنامه جامع", ModernTheme.Teal, 180, ModernTheme.InputHeight);
        btnOpenLedger.Margin = new Padding(4, 0, 4, 0);
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
        return pnlFilter;
    }

    private static ModernTextBox MakeDateInput()
    {
        var t = ModernUI.Input();
        t.Width = 130;
        t.Margin = new Padding(2, 0, 2, 0);
        t.TextAlign = HorizontalAlignment.Center;
        t.Enabled = false;
        DateHelper.ApplyDateMask(t.TextBox);
        return t;
    }

    private Control BuildKpiGrid()
    {
        var tblKPIs = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, BackColor = Color.Transparent };
        for (int i = 0; i < 4; i++) tblKPIs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tblKPIs.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tblKPIs.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        tileTotalBalance       = AddKpi(tblKPIs, "موجودی کل نقدی",        ModernTheme.Primary, 0, 0);
        tilePeriodReceipts     = AddKpi(tblKPIs, "دریافتی دوره",           ModernTheme.Success, 1, 0);
        tilePeriodPayments     = AddKpi(tblKPIs, "کل پرداختی دوره",        ModernTheme.Danger,  2, 0);
        tileNetCashFlow        = AddKpi(tblKPIs, "جریان نقدی خالص",        ModernTheme.Purple,  3, 0);

        tileAdminExpense       = AddKpi(tblKPIs, "هزینه‌های اداری",        ModernTheme.Neutral, 0, 1);
        tileDirectPurchase     = AddKpi(tblKPIs, "خرید مستقیم پروژه",      ModernTheme.Orange,  1, 1);
        tileInformalContractor = AddKpi(tblKPIs, "پیمانکار بدون قرارداد",  ModernTheme.Warning, 2, 1);
        tileFormalContractor   = AddKpi(tblKPIs, "پیمانکار دارای قرارداد",  ModernTheme.Dark,    3, 1);
        return tblKPIs;
    }

    private static ModernStatCard AddKpi(TableLayoutPanel parent, string title, Color accent, int col, int row)
    {
        var card = new ModernStatCard { Dock = DockStyle.Fill, Title = title, Accent = accent, TitleFontSize = 10f, ValueFontSize = 15f, Margin = new Padding(5) };
        card.SetAmount(0);
        parent.Controls.Add(card, col, row);
        return card;
    }

    private Control BuildChartsRow()
    {
        var tblCharts = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        // ستون ۰ = راست (نمودار ۴۰٪)، ستون ۱ = چپ (لیست حساب‌ها ۶۰٪)
        tblCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        tblCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        tblCharts.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // ── نمودار روند ──
        var chartCard = new ModernCard("روند دریافت و پرداخت", ModernTheme.Primary, 1) { Margin = new Padding(0, 0, 6, 0) };
        chartTrend = CreateChart();
        chartCard.Body.Controls.Add(chartTrend, 0, 0);
        tblCharts.Controls.Add(chartCard, 0, 0);

        // ── موجودی حساب‌ها ──
        var accountsCard = new ModernCard("موجودی تفکیکی حساب‌ها و صندوق‌ها", ModernTheme.Teal, 1) { Margin = new Padding(6, 0, 0, 0) };
        pnlAccountsList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            BackColor = ModernTheme.SurfaceAlt,
            Padding = new Padding(6)
        };
        accountsCard.Body.Controls.Add(pnlAccountsList, 0, 0);
        tblCharts.Controls.Add(accountsCard, 1, 0);

        return tblCharts;
    }

    private static Chart CreateChart()
    {
        var chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var area = new ChartArea { BackColor = Color.Transparent };
        foreach (var axis in new[] { area.AxisX, area.AxisY })
        {
            axis.MajorGrid.LineColor = ModernTheme.GridLine;
            axis.LineColor = ModernTheme.Border;
            axis.MajorTickMark.LineColor = ModernTheme.Border;
            axis.LabelStyle.ForeColor = ModernTheme.TextMuted;
        }
        area.AxisX.LabelStyle.Font = ModernTheme.GetFont(8.5f, FontStyle.Bold);
        area.AxisY.LabelStyle.Font = ModernTheme.GetFont(8f);
        area.AxisX.LabelStyle.Angle = 45;
        chart.ChartAreas.Add(area);

        chart.Legends.Add(new Legend
        {
            Docking = Docking.Bottom,
            Alignment = StringAlignment.Center,
            Font = ModernTheme.GetFont(9f, FontStyle.Bold),
            ForeColor = ModernTheme.Text,
            BackColor = Color.Transparent
        });
        return chart;
    }

    // ── ویجت چک‌های پاس‌نشده ──
    private Control CreateUnpaidChecksWidget()
    {
        var card = new ModernCard("⚠️ چک‌های پاس‌نشده (در جریان و برگشتی)", ModernTheme.Danger,
            new[] { new ColumnStyle(SizeType.Absolute, 270F), new ColumnStyle(SizeType.Percent, 100F) })
        { Margin = new Padding(0, 8, 0, 0) };

        lblUnpaidTotal = SummaryLabel("0", 18f, true, ModernTheme.Danger);
        lblUnpaidCount = SummaryLabel("0 چک", 10f, false, ModernTheme.TextMuted);

        btnViewUnpaidDetails = ModernButton.Soft("🔍 مشاهده لیست جزئیات", ModernTheme.Primary, 200, 32);
        btnViewUnpaidDetails.Enabled = false;
        btnViewUnpaidDetails.Click += async (s, e) =>
        {
            if (_unpaidChecks.Any())
            {
                bool changed;
                using (var form = new UnpaidChecksDetailForm(_unpaidChecks.ToList()))
                {
                    form.ShowDialog(this);
                    changed = form.Changed;
                }
                if (changed && !IsDisposed) await LoadDashboardDataAsync();   // بعد از ویرایش چک، داشبورد تازه شود
            }
        };

        card.Body.Controls.Add(BuildSummary("مجموع کل تعهدات", lblUnpaidTotal, lblUnpaidCount, btnViewUnpaidDetails), 0, 0);

        pnlUnpaidCards = CreateCardsFlow(ModernTheme.SurfaceAlt);
        card.Body.Controls.Add(pnlUnpaidCards, 1, 0);
        return card;
    }

    // ── ویجت تنخواه‌ها ──
    private Control CreatePettyCashWidget()
    {
        var card = new ModernCard("💰 مدیریت تنخواه‌ها (وجوه در دسترس)", ModernTheme.Orange,
            new[] { new ColumnStyle(SizeType.Absolute, 270F), new ColumnStyle(SizeType.Percent, 100F) })
        { Margin = new Padding(0, 8, 0, 0) };

        lblTotalPettyCash = SummaryLabel("0", 18f, true, ModernTheme.Orange);

        btnSettlePettyCash = ModernButton.Soft("🔍 تسویه و مدیریت تنخواه", ModernTheme.Success, 200, 32);
        btnSettlePettyCash.Enabled = false;
        btnSettlePettyCash.Click += (s, e) =>
        {
            using var form = new AccountTransferForm(AppConfig.ConnectionString);
            form.ShowDialog(this);
            _ = LoadDashboardDataAsync();
        };

        card.Body.Controls.Add(BuildSummary("کل پول در دست تنخواه‌داران", lblTotalPettyCash, null, btnSettlePettyCash), 0, 0);

        pnlPettyCashCards = CreateCardsFlow(ModernTheme.SurfaceAlt);
        card.Body.Controls.Add(pnlPettyCashCards, 1, 0);
        return card;
    }

    private static Label SummaryLabel(string text, float size, bool bold, Color color) => new Label
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = ModernTheme.GetFont(size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = color,
        BackColor = Color.Transparent,
        Margin = Padding.Empty
    };

    /// <summary>ستون خلاصه: عنوان، مبلغ کل، (تعداد)، دکمه</summary>
    private static Control BuildSummary(string caption, Label total, Label? count, ModernButton button)
    {
        var t = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent, Margin = new Padding(0, 0, 10, 0) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, count is not null ? 22F : 0F));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

        t.Controls.Add(SummaryLabel(caption, 10f, false, ModernTheme.TextMuted), 0, 0);
        t.Controls.Add(total, 0, 1);
        if (count is not null) t.Controls.Add(count, 0, 2);

        button.Anchor = AnchorStyles.None;
        t.Controls.Add(button, 0, 3);
        return t;
    }

    private static FlowLayoutPanel CreateCardsFlow(Color back) => new FlowLayoutPanel
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.RightToLeft,
        WrapContents = true,      // به‌جای اسکرول افقی (که برچسب‌ها را نیمه‌پنهان می‌کرد) به ردیف بعد می‌رود
        AutoScroll = true,
        BackColor = back,
        Padding = new Padding(6)
    };

    private static void ClearAndDispose(Control host)
    {
        var old = host.Controls.Cast<Control>().ToList();
        host.Controls.Clear();
        foreach (var c in old) c.Dispose();
    }

    // ═══════════════════════════════════════════════════════════
    //  بارگذاری داده‌ها
    // ═══════════════════════════════════════════════════════════
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

                SuspendLayout();
                try
                {
                    FillKpis(ds.Tables[0].Rows[0]);
                    FillTrendChart(ds.Tables[1]);
                    FillAccounts(ds.Tables[2]);
                    FillUnpaidChecks(ds.Tables[3]);
                    FillPettyCash(ds.Tables[4]);
                }
                finally { ResumeLayout(true); }

            }, "در حال بارگذاری داشبورد نقدینگی...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "LiquidityDashboardForm.LoadDashboardDataAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری داشبورد نقدینگی:\n" + ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "LiquidityDashboardForm"); }
        }
    }

    private static string Fmt(decimal v) => NumberHelper.FormatThousands(v, usePersianDigits: true);

    private void FillKpis(DataRow kpiRow)
    {
        decimal totalLiquidity = Convert.ToDecimal(kpiRow["TotalLiquidity"]);
        decimal totalIncome = Convert.ToDecimal(kpiRow["TotalIncome"]);
        decimal totalExpense = Convert.ToDecimal(kpiRow["TotalExpense"]);
        decimal net = totalIncome - totalExpense;

        tileTotalBalance.SetAmount(totalLiquidity);
        tilePeriodReceipts.SetAmount(totalIncome);
        tilePeriodPayments.SetAmount(totalExpense);
        tileNetCashFlow.SetAmount(net);          // منفی ⇒ خودکار قرمز

        tileAdminExpense.SetAmount(Convert.ToDecimal(kpiRow["AdminExp"]));
        tileDirectPurchase.SetAmount(Convert.ToDecimal(kpiRow["DirectExp"]));
        tileInformalContractor.SetAmount(Convert.ToDecimal(kpiRow["InformalExp"]));
        tileFormalContractor.SetAmount(Convert.ToDecimal(kpiRow["FormalExp"]));
    }

    private void FillTrendChart(DataTable table)
    {
        chartTrend.Series.Clear();
        var sIn = new Series("دریافت") { ChartType = SeriesChartType.Line, Color = ModernTheme.Success, BorderWidth = 3, MarkerStyle = MarkerStyle.Circle, MarkerSize = 6 };
        var sOut = new Series("پرداخت") { ChartType = SeriesChartType.Line, Color = ModernTheme.Danger, BorderWidth = 3, MarkerStyle = MarkerStyle.Circle, MarkerSize = 6 };
        chartTrend.Series.Add(sIn);
        chartTrend.Series.Add(sOut);

        foreach (DataRow row in table.Rows)
        {
            string day = DateHelper.ToPersianDate(Convert.ToDateTime(row["TxDate"]));
            sIn.Points.AddXY(day, Convert.ToDecimal(row["TotalIncome"]));
            sOut.Points.AddXY(day, Convert.ToDecimal(row["TotalExpense"]));
        }
    }

    private void FillAccounts(DataTable table)
    {
        pnlAccountsList.SuspendLayout();
        ClearAndDispose(pnlAccountsList);

        foreach (DataRow row in table.Rows)
        {
            string accountTitle = row["Title"].ToString() ?? "";
            string bankName = BankHelper.GetBankNameByCode(row["BankCode"].ToString() ?? "");
            if (string.IsNullOrWhiteSpace(bankName)) bankName = "نامشخص";
            string displayTitle = string.IsNullOrWhiteSpace(accountTitle) ? bankName : $"{accountTitle} ({bankName})";

            decimal balance = Convert.ToDecimal(row["CurrentBalance"]);
            Color accent = balance > 0 ? ModernTheme.Success : (balance < 0 ? ModernTheme.Danger : ModernTheme.Neutral);

            var card = new ModernStatCard
            {
                Width = 320,
                Height = 84,
                Margin = new Padding(5),
                Title = displayTitle,
                TitleFontSize = 10f,
                ValueFontSize = 13f,
                SubText = row["AccountNumber"].ToString() ?? "",
                Accent = accent
            };
            card.SetAmount(balance);
            pnlAccountsList.Controls.Add(card);
        }
        pnlAccountsList.ResumeLayout(true);
    }

    // نام بانک چک؛ اگر روی خود چک ثبت نشده بود از حساب بانکیِ متصل گرفته می‌شود
    private static string ResolveBankName(DataRow row)
    {
        string name = row["BankName"].ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(name)) return name;
        string code = row["AccountBankCode"].ToString() ?? "";
        if (string.IsNullOrWhiteSpace(code)) return "";
        string byCode = BankHelper.GetBankNameByCode(code);
        return byCode.Contains("نامشخص") ? "" : byCode;
    }

    private void FillUnpaidChecks(DataTable table)
    {
        _unpaidChecks.Clear();
        foreach (DataRow row in table.Rows)
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
                BankName = ResolveBankName(row),
                Description = row["Description"].ToString() ?? ""
            });
        }

        pnlUnpaidCards.SuspendLayout();
        ClearAndDispose(pnlUnpaidCards);

        if (!_unpaidChecks.Any())
        {
            lblUnpaidTotal.Text = "0"; lblUnpaidTotal.ForeColor = ModernTheme.Success;
            lblUnpaidCount.Text = "هیچ چک پاس‌نشده‌ای وجود ندارد"; lblUnpaidCount.ForeColor = ModernTheme.Success;
            btnViewUnpaidDetails.Enabled = false;
        }
        else
        {
            lblUnpaidTotal.Text = Fmt(_unpaidChecks.Sum(c => c.Amount)); lblUnpaidTotal.ForeColor = ModernTheme.Danger;
            lblUnpaidCount.Text = $"{_unpaidChecks.Count} چک در انتظار"; lblUnpaidCount.ForeColor = ModernTheme.TextMuted;
            btnViewUnpaidDetails.Enabled = true;

            DateTime today = DateTime.Now.Date;
            var groups = _unpaidChecks.GroupBy(c => c.DueDate.Date)
                .Select(g => new { RawDate = g.Key, Count = g.Count(), Sum = g.Sum(c => c.Amount) })
                .OrderBy(g => g.RawDate);

            foreach (var g in groups)
            {
                bool overdue = g.RawDate < today;
                bool soon = !overdue && g.RawDate <= today.AddDays(7);
                Color fill = overdue ? ModernTheme.DangerSoft : (soon ? ModernTheme.WarningSoft : ModernTheme.PrimarySoft);
                Color accent = overdue ? ModernTheme.Danger : (soon ? ModernTheme.Orange : ModernTheme.Primary);

                var card = new ModernStatCard
                {
                    Width = 180,
                    Height = 84,
                    Margin = new Padding(4),
                    Title = DateHelper.ToPersianDate(g.RawDate),
                    TitleFontSize = 10f,
                    ValueFontSize = 11.5f,
                    SubText = $"{g.Count} چک",
                    Accent = accent,
                    FillColor = fill,
                    BorderColor = ModernPaint.Lighten(accent, 0.45f)
                };
                card.SetAmount(g.Sum);

                // دابل‌کلیک روی برچسب → لیست چک‌های همان تاریخ سررسید
                var day = g.RawDate;
                card.Cursor = Cursors.Hand;
                _cardTip.SetToolTip(card, "دابل‌کلیک: مشاهده‌ی لیست چک‌های این تاریخ");
                card.DoubleClick += async (s, e) =>
                {
                    var dayChecks = _unpaidChecks.Where(c => c.DueDate.Date == day).ToList();
                    if (dayChecks.Count == 0) return;
                    bool changed;
                    using (var form = new UnpaidChecksDetailForm(dayChecks, $"چک‌های پاس‌نشده‌ی سررسید {DateHelper.ToPersianDate(day)}"))
                    {
                        form.ShowDialog(this);
                        changed = form.Changed;
                    }
                    if (changed && !IsDisposed) await LoadDashboardDataAsync();   // بعد از ویرایش چک، داشبورد تازه شود
                };
                pnlUnpaidCards.Controls.Add(card);
            }
        }
        pnlUnpaidCards.ResumeLayout(true);
    }

    private void FillPettyCash(DataTable table)
    {
        var list = new List<CompanyBankAccount>();
        foreach (DataRow row in table.Rows)
        {
            list.Add(new CompanyBankAccount
            {
                Id = Convert.ToInt32(row["Id"]),
                Title = row["Title"].ToString() ?? "",
                CurrentBalance = Convert.ToDecimal(row["CurrentBalance"]),
                CreatedAt = Convert.ToDateTime(row["LastActivityDate"])
            });
        }

        pnlPettyCashCards.SuspendLayout();
        ClearAndDispose(pnlPettyCashCards);

        if (!list.Any())
        {
            lblTotalPettyCash.Text = "0";
            lblTotalPettyCash.ForeColor = ModernTheme.Success;
            btnSettlePettyCash.Enabled = false;
        }
        else
        {
            lblTotalPettyCash.Text = Fmt(list.Sum(p => p.CurrentBalance));
            lblTotalPettyCash.ForeColor = ModernTheme.Orange;
            btnSettlePettyCash.Enabled = true;

            DateTime today = DateTime.Now.Date;
            foreach (var pc in list)
            {
                int days = (today - pc.CreatedAt.Date).Days;

                Color fill = ModernTheme.SuccessSoft, accent = ModernTheme.Success;
                string status = "عادی";
                if (days > 30) { fill = ModernTheme.DangerSoft; accent = ModernTheme.Danger; status = "معوق (بیش از ۱ ماه)"; }
                else if (days > 7) { fill = ModernTheme.WarningSoft; accent = ModernTheme.Orange; status = "نیاز به تسویه"; }

                var card = new ModernStatCard
                {
                    Width = 250,
                    Height = 84,
                    Margin = new Padding(4),
                    Title = pc.Title,
                    TitleFontSize = 10f,
                    ValueFontSize = 13f,
                    SubText = status,
                    Accent = accent,
                    FillColor = fill,
                    BorderColor = ModernPaint.Lighten(accent, 0.45f)
                };
                card.SetAmount(pc.CurrentBalance);
                pnlPettyCashCards.Controls.Add(card);
            }
        }
        pnlPettyCashCards.ResumeLayout(true);
    }
}
