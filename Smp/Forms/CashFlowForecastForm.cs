using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace Smp.Forms;

public class CashFlowForecastForm : Form
{
    private readonly string _connectionString;
    private readonly CashCheckRepository _checkRepo;
    private readonly BankAccountRepository _bankRepo;

    private ModernStatCard cardCurrent = null!, cardIncoming = null!, cardOutgoing = null!, cardProjected = null!;
    private Chart chartForecast = null!;
    private DataGridView dgvIncoming = null!, dgvOutgoing = null!;

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public CashFlowForecastForm(string connectionString)
    {
        _connectionString = connectionString;
        _checkRepo = new CashCheckRepository(connectionString);
        _bankRepo = new BankAccountRepository(connectionString);

        BuildUI();

        Shown += async (s, e) => await LoadDataAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CashFlowForecastForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ═══════ ۱. کارت‌های KPI ═══════
        var strip = ModernReport.CreateStatStrip(4, 88);
        strip.Dock = DockStyle.Fill;
        cardCurrent = strip.Add("موجودی نقدی فعلی", ModernTheme.Primary);
        cardIncoming = strip.Add("مجموع چک‌های دریافتی (آینده)", ModernTheme.Success);
        cardOutgoing = strip.Add("مجموع چک‌های پرداختی (آینده)", ModernTheme.Danger);
        cardProjected = strip.Add("موجودی پیش‌بینی‌شده (پایان دوره)", ModernTheme.Purple);
        foreach (var c in new[] { cardCurrent, cardIncoming, cardOutgoing, cardProjected })
        {
            c.TitleFontSize = 9.5f;
            c.ValueFontSize = 13f;
            c.SetAmount(0);
        }
        cardProjected.NegativeRed = true;

        // ═══════ ۲. نمودار (۶ ماه آینده) ═══════
        chartForecast = ModernChart.CreateCartesian("#,#,, 'M'");   // میلیون
        var balance = ModernChart.AddSeries(chartForecast, "موجودی نقدی", ModernTheme.Primary, SeriesChartType.SplineArea);
        balance.Color = Color.FromArgb(100, ModernTheme.Primary);
        balance.BorderColor = ModernTheme.Primary;
        ModernChart.AddSeries(chartForecast, "چک‌های دریافتی", ModernTheme.Success, SeriesChartType.Column);
        ModernChart.AddSeries(chartForecast, "چک‌های پرداختی", ModernTheme.Danger, SeriesChartType.Column);
        var chartCard = ModernChart.InCard("روند پیش‌بینی موجودی نقدی (۶ ماه آینده)", ModernTheme.Primary, chartForecast);

        // ═══════ ۳. جدول چک‌ها (دو تب) ═══════
        dgvIncoming = CreateGrid(ModernTheme.Success);
        dgvOutgoing = CreateGrid(ModernTheme.Danger);

        var tabs = new ModernTabHost { Dock = DockStyle.Fill, AccentColor = ModernTheme.Primary };
        tabs.AddPage("💳 چک‌های دریافتی پیش‌رو").Controls.Add(ModernReport.WrapGrid(dgvIncoming));
        tabs.AddPage("🧾 چک‌های پرداختی پیش‌رو").Controls.Add(ModernReport.WrapGrid(dgvOutgoing));

        var tablesCard = new ModernCard("", ModernTheme.Teal, 1);
        tablesCard.Body.Controls.Add(tabs, 0, 0);

        ModernListKit.ComposeRows(this, "پیش‌بینی جریان نقدی (Cash Flow Forecast)",
            "موجودی فعلی، چک‌های در جریان و روند موجودی ۶ ماه آینده", new Size(1300, 850),
            (strip, 92), (chartCard, 330), (tablesCard, 0));
    }

    private static DataGridView CreateGrid(Color accent)
    {
        var dgv = ModernGrid.Create();
        dgv.AutoGenerateColumns = false;
        dgv.AllowUserToAddRows = false;
        dgv.ReadOnly = true;
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.FillColumns();
        dgv.UseAiryLook(42);
        dgv.UseSelectionAccent(accent);

        AddCol(dgv, "CheckNumber", "شماره چک", 15, true);
        AddCol(dgv, "BankName", "بانک", 15);
        AddCol(dgv, "RecipientName", "طرف حساب", 20);
        AddCol(dgv, "DueDate", "تاریخ سررسید", 15, true);
        AddCol(dgv, "Amount", "مبلغ (ریال)", 15, true);
        AddCol(dgv, "PaymentScopeDisplay", "نوع پرداخت", 20);
        ModernListKit.PinHeaders(dgv);   // جلوگیری از جایگزینی عنوان ستون‌ها با نام ستون

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

    private static void AddCol(DataGridView dgv, string name, string header, float weight, bool center = false)
    {
        var col = new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = header, FillWeight = weight, MinimumWidth = 90 };
        col.DefaultCellStyle.Alignment = center ? DataGridViewContentAlignment.MiddleCenter : DataGridViewContentAlignment.MiddleRight;
        dgv.Columns.Add(col);
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
                cardCurrent.SetAmount(currentLiquidity);

                // ۲. فیلتر چک‌های در جریان (پاس نشده)
                // ✅ چک‌های در جریانِ سررسید گذشته هم لحاظ می‌شوند (هنوز وصول/پاس نشده‌اند و روی موجودی اثر می‌گذارند)
                var pendingChecks = checks.Where(c => c.Status == "در جریان").ToList();

                var incomingChecks = pendingChecks.Where(c => c.CheckType == "دریافتی").OrderBy(c => c.DueDate).ToList();
                var outgoingChecks = pendingChecks.Where(c => c.CheckType == "پرداختی").OrderBy(c => c.DueDate).ToList();

                decimal totalIncoming = incomingChecks.Sum(c => c.Amount);
                decimal totalOutgoing = outgoingChecks.Sum(c => c.Amount);

                cardIncoming.SetAmount(totalIncoming);
                cardOutgoing.SetAmount(totalOutgoing);

                // ۳. پر کردن جدول‌ها
                var incomingView = incomingChecks.Select(c => new
                {
                    c.CheckNumber,
                    c.BankName,
                    c.RecipientName,
                    DueDate = DateHelper.ToPersianDate(c.DueDate) + (c.DueDate.Date < DateTime.Today ? " (سررسید گذشته)" : ""),
                    Amount = c.Amount,
                    PaymentScopeDisplay = string.IsNullOrWhiteSpace(c.PaymentScope) ? "---" : c.PaymentScope
                }).ToList();

                var outgoingView = outgoingChecks.Select(c => new
                {
                    c.CheckNumber,
                    c.BankName,
                    c.RecipientName,
                    DueDate = DateHelper.ToPersianDate(c.DueDate) + (c.DueDate.Date < DateTime.Today ? " (سررسید گذشته)" : ""),
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

                // چک‌های گذشته‌ی پاس‌نشده در ماه جاری حساب می‌شوند
                DateTime EffectiveDue(DateTime d) => d.Date < today ? today : d.Date;

                for (int i = 0; i < 6; i++)
                {
                    DateTime monthStart = pc.AddMonths(today, i);
                    int year = pc.GetYear(monthStart);
                    int month = pc.GetMonth(monthStart);
                    string monthName = TranslationHelper.GetMonthName(month);

                    // چک‌های این ماه
                    var monthIncoming = incomingChecks.Where(c => pc.GetYear(EffectiveDue(c.DueDate)) == year && pc.GetMonth(EffectiveDue(c.DueDate)) == month).Sum(c => c.Amount);
                    var monthOutgoing = outgoingChecks.Where(c => pc.GetYear(EffectiveDue(c.DueDate)) == year && pc.GetMonth(EffectiveDue(c.DueDate)) == month).Sum(c => c.Amount);

                    // محاسبه موجودی پایان ماه
                    runningBalance = runningBalance + monthIncoming - monthOutgoing;

                    // افزودن به نمودار
                    chartForecast.Series["موجودی نقدی"].Points.AddXY(monthName, runningBalance);
                    chartForecast.Series["چک‌های دریافتی"].Points.AddXY(monthName, monthIncoming);
                    chartForecast.Series["چک‌های پرداختی"].Points.AddXY(monthName, monthOutgoing);
                }

                cardProjected.SetAmount(runningBalance);
                cardProjected.Accent = runningBalance >= 0 ? ModernTheme.Purple : ModernTheme.Danger;

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
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CashFlowForecastForm"); }
        }
    }
}
