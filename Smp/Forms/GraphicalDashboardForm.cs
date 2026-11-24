using Smp.Data;
using Smp.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;

namespace Smp.Forms;

public class GraphicalDashboardForm : Form
{
    private readonly DashboardRepository _repository;

    private Chart chartTrend = null!;
    private Chart chartDepartment = null!;
    private ModernComboBox cmbYear = null!;
    private ModernButton btnRefresh = null!;
    private ModernStatCard cardGross = null!, cardIns = null!, cardTax = null!, cardNet = null!;
    private ModernCard trendCard = null!;
    private bool _isInitializing = true;

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public GraphicalDashboardForm(string connectionString)
    {
        _repository = new DashboardRepository(connectionString);

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);

        Shown += async (s, e) =>
        {
            _isInitializing = false;
            await LoadChartsSafeAsync();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception ex) { LogHelper.LogException(ex, "GraphicalDashboardForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── نوار فیلتر ──
        cmbYear = ModernUI.Combo();
        cmbYear.Width = 110;
        cmbYear.DropDownStyle = ComboBoxStyle.DropDownList;
        var pc = new PersianCalendar();
        int currentYear = pc.GetYear(DateTime.Now);
        for (int i = currentYear - 3; i <= currentYear + 1; i++) cmbYear.Items.Add(i);
        cmbYear.SelectedItem = currentYear;
        cmbYear.SelectedIndexChanged += async (s, e) => { if (!_isInitializing) await LoadChartsSafeAsync(); };

        btnRefresh = ModernButton.Soft("🔄 بروزرسانی", ModernTheme.Primary, 140, 34);
        btnRefresh.Click += async (s, e) => await LoadChartsSafeAsync();

        var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        bar.Controls.Add(new Label
        {
            Text = "سال مالی:",
            AutoSize = true,
            Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
            ForeColor = ModernTheme.Text,
            BackColor = Color.Transparent,
            Margin = new Padding(6, 8, 4, 0)
        });
        bar.Controls.Add(cmbYear);
        bar.Controls.Add(btnRefresh);

        // ── خلاصه‌ی سالانه (از همان داده‌ی نمودار؛ بدون کوئری اضافه) ──
        var strip = ModernReport.CreateStatStrip(4, 84);
        cardGross = strip.Add("جمع حقوق و مزایا", ModernTheme.Primary);
        cardIns = strip.Add("جمع بیمه", ModernTheme.Success);
        cardTax = strip.Add("جمع مالیات", ModernTheme.Danger);
        cardNet = strip.Add("جمع بیمه و مالیات", ModernTheme.Purple);
        foreach (var c in new[] { cardGross, cardIns, cardTax, cardNet }) { c.TitleFontSize = 9.5f; c.ValueFontSize = 13f; c.SetAmount(0, "ریال"); c.DimZero = true; }

        // ── نمودارها ──
        chartTrend = ModernChart.CreateCartesian();
        ModernChart.AddSeries(chartTrend, "حقوق", ModernTheme.Primary);
        ModernChart.AddSeries(chartTrend, "بیمه", ModernTheme.Success);
        ModernChart.AddSeries(chartTrend, "مالیات", ModernTheme.Danger);

        chartDepartment = ModernChart.CreateDoughnut();

        trendCard = ModernChart.InCard("روند حقوق، بیمه و مالیات (میلیون ریال)", ModernTheme.Primary, chartTrend);
        var depCard = ModernChart.InCard("توزیع پرسنل در دپارتمان‌ها", ModernTheme.Teal, chartDepartment);

        var charts = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        // ستون ۰ = راست: روند (۶۰٪)، ستون ۱ = چپ: دپارتمان‌ها (۴۰٪)
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        trendCard.Margin = new Padding(0, 0, 6, 0);
        depCard.Margin = new Padding(6, 0, 0, 0);
        charts.Controls.Add(trendCard, 0, 0);
        charts.Controls.Add(depCard, 1, 0);

        ModernListKit.ComposeRows(this, "داشبورد گرافیکی و نمودارها", "روند ماهانه‌ی حقوق، بیمه و مالیات و توزیع پرسنل",
            new Size(1350, 800),
            (bar, 46), (strip, 84), (charts, 0));
    }

    private async Task LoadChartsSafeAsync()
    {
        if (cmbYear.SelectedItem is null || IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            btnRefresh.Enabled = false;
            int year = Convert.ToInt32(cmbYear.SelectedItem);

            // سال جاری: تا ماه جاری، سال‌های دیگر: تا ماه ۱۲
            var pc = new PersianCalendar();
            int uptoMonth = year == pc.GetYear(DateTime.Now) ? pc.GetMonth(DateTime.Now) : 12;
            trendCard.Title = $"روند حقوق، بیمه و مالیات تا {TranslationHelper.GetMonthName(uptoMonth)} {StringHelper.ConvertToPersianDigits(year.ToString())} (میلیون ریال)";

            await AsyncHelper.RunWithLoading(this, async () =>
            {
                // دو کوئری هم‌زمان (قبلاً پشت سر هم اجرا می‌شدند)
                var trendTask = _repository.GetPayrollTrendAsync(year, uptoMonth);
                var deptTask = _repository.GetDepartmentDistributionAsync();
                await Task.WhenAll(trendTask, deptTask);
                if (IsDisposed) return;

                FillTrend(trendTask.Result);
                FillDepartments(deptTask.Result);
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
            if (!IsDisposed) btnRefresh.Enabled = true;
            try { _loadLock.Release(); } catch (ObjectDisposedException) { }
        }
    }

    private static decimal Dec(DataRow r, string col) => r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);

    private void FillTrend(DataTable dt)
    {
        chartTrend.SuspendLayout();
        try
        {
            foreach (var s in chartTrend.Series) s.Points.Clear();
            chartTrend.Annotations.Clear();

            decimal gross = 0, ins = 0, tax = 0;
            foreach (DataRow row in dt.Rows)
            {
                string label = TranslationHelper.GetMonthName(Convert.ToInt32(row["Month"]));
                decimal g = Dec(row, "TotalGross"), i = Dec(row, "TotalInsurance"), t = Dec(row, "TotalTax");
                gross += g; ins += i; tax += t;
                chartTrend.Series["حقوق"].Points.AddXY(label, g);
                chartTrend.Series["بیمه"].Points.AddXY(label, i);
                chartTrend.Series["مالیات"].Points.AddXY(label, t);
            }
            if (dt.Rows.Count == 0) ModernChart.ShowEmpty(chartTrend, "برای این سال حقوقی ثبت نشده است");

            cardGross.SetAmount(gross, "ریال");
            cardIns.SetAmount(ins, "ریال");
            cardTax.SetAmount(tax, "ریال");
            cardNet.SetAmount(ins + tax, "ریال");
        }
        finally { chartTrend.ResumeLayout(); }
    }

    private void FillDepartments(DataTable dt)
    {
        ModernChart.FillDoughnut(chartDepartment, dt.AsEnumerable().Select(r =>
        (
            string.IsNullOrWhiteSpace(r["DepartmentName"]?.ToString()) ? "نامشخص" : r["DepartmentName"].ToString()!,
            r["EmployeeCount"] == DBNull.Value ? 0d : Convert.ToDouble(r["EmployeeCount"])
        )));
    }
}
