using Smp.Data;
using Smp.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// 👇 جلوگیری از تداخل نام Chart با کتابخانه ClosedXML 👇
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;

namespace Smp.Forms;

public class ManagementDashboardForm : Form
{
    private readonly DashboardRepository _repository;

    private ModernStatCard cardTotal = null!, cardActive = null!, cardInactive = null!;
    private ModernStatCard cardPayroll = null!, cardAvg = null!, cardExpiring = null!;
    private Chart chartEducation = null!, chartPersonnelType = null!;
    private DataGridView dgvExpiring = null!;
    private ModernCard expiringCard = null!;
    private Label lblUpdated = null!;
    private ModernButton btnRefresh = null!;

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public ManagementDashboardForm(string connectionString)
    {
        _repository = new DashboardRepository(connectionString);

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);   // F5/Ctrl+S = بروزرسانی

        Shown += async (s, e) =>
        {
            try { await LoadAllDataSafeAsync(); }
            catch (Exception ex) { LogHelper.LogException(ex, "ManagementDashboardForm.Shown"); }
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception ex) { LogHelper.LogException(ex, "ManagementDashboardForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── نوار بالا: بروزرسانی + زمان آخرین بروزرسانی ──
        btnRefresh = ModernButton.Soft("🔄 بروزرسانی", ModernTheme.Primary, 140, 34);
        btnRefresh.Click += async (s, e) => await LoadAllDataSafeAsync();
        lblUpdated = new Label
        {
            AutoSize = true,
            Font = ModernTheme.GetFont(9.5f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent,
            Margin = new Padding(12, 8, 0, 0)
        };
        var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        bar.Controls.Add(btnRefresh);
        bar.Controls.Add(lblUpdated);

        // ── کارت‌های KPI (یک ردیف) ──
        var strip = ModernReport.CreateStatStrip(6, 92);
        cardTotal = strip.Add("کل پرسنل", ModernTheme.Primary);
        cardActive = strip.Add("پرسنل فعال", ModernTheme.Success);
        cardInactive = strip.Add("پرسنل غیرفعال", ModernTheme.Neutral);
        cardPayroll = strip.Add("جمع حقوق آخرین ماه", ModernTheme.Purple);
        cardAvg = strip.Add("میانگین حقوق", ModernTheme.Orange);
        cardExpiring = strip.Add("قرارداد در آستانه انقضا", ModernTheme.Danger);
        foreach (var c in new[] { cardTotal, cardActive, cardInactive, cardPayroll, cardAvg, cardExpiring })
        { c.TitleFontSize = 9.5f; c.ValueFontSize = 14f; c.SetCount(0); }
        cardPayroll.SetAmount(0); cardAvg.SetAmount(0);
        cardPayroll.DimZero = cardAvg.DimZero = true;

        // ── نمودارها + جدول (بخش اصلی) ──
        chartEducation = ModernChart.CreateDoughnut();
        chartPersonnelType = ModernChart.CreateDoughnut();

        dgvExpiring = ModernGrid.Create();
        dgvExpiring.FillColumns();
        dgvExpiring.UseAiryLook(40);
        dgvExpiring.UseSelectionAccent(ModernTheme.Warning);
        dgvExpiring.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
        };

        expiringCard = new ModernCard("⚠️ قراردادهای در آستانه انقضا (۳۰ روز آینده)", ModernTheme.Danger, 1);
        expiringCard.Body.Controls.Add(ModernReport.WrapGrid(dgvExpiring), 0, 0);

        var main = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 46F));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 54F));

        var c1 = ModernChart.InCard("توزیع پرسنل بر اساس تحصیلات", ModernTheme.Primary, chartEducation);
        var c2 = ModernChart.InCard("توزیع پرسنل بر اساس نوع پرسنلی", ModernTheme.Teal, chartPersonnelType);
        c1.Margin = new Padding(0, 0, 6, 6); c2.Margin = new Padding(6, 0, 0, 6);
        expiringCard.Margin = new Padding(0, 6, 0, 0);
        main.Controls.Add(c1, 0, 0);
        main.Controls.Add(c2, 1, 0);
        main.Controls.Add(expiringCard, 0, 1);
        main.SetColumnSpan(expiringCard, 2);

        ModernListKit.ComposeRows(this, "داشبورد مدیریتی", "نمای کلی پرسنل، حقوق و قراردادهای در آستانه‌ی انقضا",
            new Size(1400, 900),
            (bar, 46), (strip, 92), (main, 0));
    }

    private async Task LoadAllDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;   // بارگذاری هم‌زمان نادیده گرفته می‌شود

        try
        {
            btnRefresh.Enabled = false;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                // همه‌ی کوئری‌ها هم‌زمان اجرا می‌شوند
                var statsTask = _repository.GetEmployeeStatsAsync();
                var payrollTask = _repository.GetLastPayrollTotalAsync();
                var expiringTask = _repository.GetExpiringContractsCountAsync();
                var eduTask = _repository.GetEducationDistributionAsync();
                var typeTask = _repository.GetPersonnelTypeDistributionAsync();
                var expiringListTask = _repository.GetExpiringContractsListAsync();

                await Task.WhenAll(statsTask, payrollTask, expiringTask, eduTask, typeTask, expiringListTask);
                if (IsDisposed) return;

                var dtStats = statsTask.Result;
                if (dtStats.Rows.Count > 0)
                {
                    var row = dtStats.Rows[0];
                    cardTotal.SetCount(SafeInt(row["TotalCount"]));
                    cardActive.SetCount(SafeInt(row["ActiveCount"]));
                    cardInactive.SetCount(SafeInt(row["InactiveCount"]));
                    cardAvg.SetAmount(SafeDecimal(row["AvgSalary"]));
                }

                cardPayroll.SetAmount(payrollTask.Result);
                cardExpiring.SetCount(expiringTask.Result);

                ModernChart.FillDoughnut(chartEducation, ToItems(eduTask.Result, "Education"));
                ModernChart.FillDoughnut(chartPersonnelType, ToItems(typeTask.Result, "PersonnelType"));

                var list = expiringListTask.Result;
                dgvExpiring.DataSource = list;
                expiringCard.Title = $"⚠️ قراردادهای در آستانه انقضا (۳۰ روز آینده) — {StringHelper.ConvertToPersianDigits(list.Rows.Count.ToString())} مورد";

                lblUpdated.Text = "آخرین بروزرسانی: " + StringHelper.ConvertToPersianDigits(DateTime.Now.ToString("HH:mm"));
            }, "در حال بارگذاری داشبورد مدیریتی...");
        }
        catch (ObjectDisposedException) { /* فرم بسته شده */ }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ManagementDashboardForm.LoadAllDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری داشبورد:\n" + ex.Message);
        }
        finally
        {
            if (!IsDisposed) btnRefresh.Enabled = true;
            try { _loadLock.Release(); } catch (ObjectDisposedException) { }
        }
    }

    private static int SafeInt(object v) => v == DBNull.Value || v is null ? 0 : Convert.ToInt32(v);
    private static decimal SafeDecimal(object v) => v == DBNull.Value || v is null ? 0m : Convert.ToDecimal(v);

    private static System.Collections.Generic.IEnumerable<(string, double)> ToItems(DataTable dt, string nameCol)
        => dt.AsEnumerable().Select(r =>
        (
            string.IsNullOrWhiteSpace(r[nameCol]?.ToString()) ? "نامشخص" : r[nameCol].ToString()!,
            (double)SafeInt(r["EmployeeCount"])
        ));
}
