using Smp.Data;
using Smp.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;

namespace Smp.Forms;

public class ContractsManagementDashboardForm : Form
{
    private readonly ContractsDashboardRepository _repository;

    private ModernStatCard cardClient = null!, cardContractor = null!, cardReceivables = null!, cardPayables = null!, cardExpiring = null!;
    private Chart chartStatus = null!, chartCompare = null!;
    private DataGridView dgvExpiring = null!, dgvAtRisk = null!;
    private ModernButton btnRefresh = null!;
    private Label lblUpdated = null!;

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public ContractsManagementDashboardForm(string connectionString)
    {
        _repository = new ContractsDashboardRepository(connectionString);

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);

        Shown += async (s, e) => await LoadDataSafeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception ex) { LogHelper.LogException(ex, "ContractsManagementDashboardForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── نوار بالا ──
        btnRefresh = ModernButton.Soft("🔄 بروزرسانی", ModernTheme.Primary, 140, 34);
        btnRefresh.Click += async (s, e) => await LoadDataSafeAsync();
        lblUpdated = new Label { AutoSize = true, Font = ModernTheme.GetFont(9.5f), ForeColor = ModernTheme.TextMuted, BackColor = Color.Transparent, Margin = new Padding(12, 8, 0, 0) };
        var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
        bar.Controls.Add(btnRefresh);
        bar.Controls.Add(lblUpdated);

        // ── KPI ──
        var strip = ModernReport.CreateStatStrip(5, 92);
        cardClient = strip.Add("مبلغ قراردادهای کارفرما", ModernTheme.Primary);
        cardContractor = strip.Add("مبلغ قراردادهای پیمانکار", ModernTheme.Orange);
        cardReceivables = strip.Add("مانده مطالبات (طلب)", ModernTheme.Success);
        cardPayables = strip.Add("مانده بدهی‌ها (بدهی)", ModernTheme.Danger);
        cardExpiring = strip.Add("در آستانه انقضا (۳۰ روز)", ModernTheme.Warning);
        foreach (var c in new[] { cardClient, cardContractor, cardReceivables, cardPayables })
        { c.TitleFontSize = 9.5f; c.ValueFontSize = 13f; c.SetAmount(0); c.NegativeRed = false; c.DimZero = true; }
        cardExpiring.TitleFontSize = 9.5f; cardExpiring.ValueFontSize = 13f; cardExpiring.SetCount(0, "قرارداد");

        // ── نمودارها: وضعیت قراردادها + مقایسه‌ی مبالغ ──
        chartStatus = ModernChart.CreateDoughnut();

        chartCompare = ModernChart.CreateCartesian("#,#,, 'M'");
        chartCompare.Legends.Clear();
        var cmp = ModernChart.AddSeries(chartCompare, "مبلغ", ModernTheme.Primary);
        cmp["PointWidth"] = "0.5";

        var statusCard = ModernChart.InCard("وضعیت قراردادهای کارفرما", ModernTheme.Primary, chartStatus);
        var compareCard = ModernChart.InCard("مقایسه‌ی مبالغ (میلیون ریال)", ModernTheme.Teal, chartCompare);
        var charts = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        statusCard.Margin = new Padding(0, 0, 6, 0);
        compareCard.Margin = new Padding(6, 0, 0, 0);
        charts.Controls.Add(statusCard, 0, 0);
        charts.Controls.Add(compareCard, 1, 0);

        // ── جدول‌ها (دو صفحه با تب مدرن) ──
        dgvExpiring = CreateGrid(ModernTheme.Warning);
        AddCol(dgvExpiring, "ContractNumber", "شماره قرارداد", 14, true);
        AddCol(dgvExpiring, "PartyName", "کارفرما", 30);
        AddCol(dgvExpiring, "EndDate", "تاریخ پایان", 14, true);
        AddCol(dgvExpiring, "Status", "وضعیت", 14, true);
        AddCol(dgvExpiring, "Amount", "مبلغ قرارداد", 20, true);
        ModernListKit.PinHeaders(dgvExpiring);

        dgvAtRisk = CreateGrid(ModernTheme.Danger);
        AddCol(dgvAtRisk, "ContractNumber", "شماره قرارداد", 14, true);
        AddCol(dgvAtRisk, "PartyName", "کارفرما", 26);
        AddCol(dgvAtRisk, "ClientAmount", "مبلغ قرارداد", 20, true);
        AddCol(dgvAtRisk, "TotalCosts", "جمع هزینه‌ها (پیمانکار + فاکتور)", 26, true);
        ModernListKit.PinHeaders(dgvAtRisk);

        var tabs = new ModernTabHost { Dock = DockStyle.Fill };
        var pageExpiring = tabs.AddPage("⏳ در آستانه انقضا (۳۰ روز آینده)");
        var pageAtRisk = tabs.AddPage("⚠️ قراردادهای ضررده (ریسک مالی)");
        pageExpiring.Controls.Add(ModernReport.WrapGrid(dgvExpiring));
        pageAtRisk.Controls.Add(ModernReport.WrapGrid(dgvAtRisk));

        var tablesCard = new ModernCard("", ModernTheme.Danger, 1);
        tablesCard.Body.Controls.Add(tabs, 0, 0);

        ModernListKit.ComposeRows(this, "داشبورد مدیریت قراردادها", "پایش مطالبات، بدهی‌ها، قراردادهای در آستانه‌ی انقضا و قراردادهای ضررده",
            new Size(1400, 950),
            (bar, 46), (strip, 92), (charts, 300), (tablesCard, 0));
    }

    private static DataGridView CreateGrid(Color accent)
    {
        var dgv = ModernGrid.Create();
        dgv.AutoGenerateColumns = false;
        dgv.AllowUserToAddRows = false;
        dgv.ReadOnly = true;
        dgv.FillColumns();
        dgv.UseAiryLook(42);
        dgv.UseSelectionAccent(accent);
        dgv.CellFormatting += Grid_CellFormatting;
        return dgv;
    }

    private static void AddCol(DataGridView dgv, string name, string header, float weight, bool center = false)
    {
        var col = new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = header, FillWeight = weight, MinimumWidth = 90 };
        col.DefaultCellStyle.Alignment = center ? DataGridViewContentAlignment.MiddleCenter : DataGridViewContentAlignment.MiddleRight;
        dgv.Columns.Add(col);
    }

    private static void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || sender is not DataGridView dgv) return;
        string colName = dgv.Columns[e.ColumnIndex].Name;

        if (colName == "EndDate" && e.Value is DateTime dt)
        {
            e.Value = DateHelper.ToPersianDate(dt);
            e.FormattingApplied = true;
        }
        else if ((colName == "Amount" || colName == "ClientAmount" || colName == "TotalCosts") && e.Value is decimal dec)
        {
            e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
            e.FormattingApplied = true;
        }

        // هزینه‌ها بیشتر از مبلغ قرارداد ⇒ قرمز
        if (colName == "TotalCosts" && dgv.Columns.Contains("ClientAmount"))
        {
            decimal clientAmt = ToDec(dgv.Rows[e.RowIndex].Cells["ClientAmount"].Value);
            decimal costs = ToDec(dgv.Rows[e.RowIndex].Cells["TotalCosts"].Value);
            if (costs > clientAmt)
            {
                e.CellStyle.BackColor = ModernTheme.DangerSoft;
                e.CellStyle.ForeColor = ModernTheme.DangerText;
                e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
            }
        }
    }

    private static decimal ToDec(object? v) => v is null || v == DBNull.Value ? 0m : Convert.ToDecimal(v);

    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            btnRefresh.Enabled = false;
            DataSet? ds = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                ds = await Task.Run(() => _repository.GetDashboardData());
            }, "در حال بارگذاری داشبورد قراردادها...");

            if (!success || ds is null || IsDisposed) return;

            // ۱. KPI
            decimal clientVal = 0, contractorVal = 0, receivables = 0, payables = 0;
            int expiring = 0;
            if (ds.Tables.Contains("KPI") && ds.Tables["KPI"]!.Rows.Count > 0)
            {
                var row = ds.Tables["KPI"]!.Rows[0];
                clientVal = ToDec(row["TotalClientValue"]);
                contractorVal = ToDec(row["TotalContractorValue"]);
                receivables = ToDec(row["TotalReceivables"]);
                payables = ToDec(row["TotalPayables"]);
                expiring = row["ExpiringContractsCount"] == DBNull.Value ? 0 : Convert.ToInt32(row["ExpiringContractsCount"]);
            }
            cardClient.SetAmount(clientVal);
            cardContractor.SetAmount(contractorVal);
            cardReceivables.SetAmount(Math.Max(0, receivables));
            cardPayables.SetAmount(Math.Max(0, payables));
            cardExpiring.SetCount(expiring, "قرارداد");

            // ۲. نمودار وضعیت
            var dtStatus = ds.Tables["StatusDistribution"]!;
            ModernChart.FillDoughnut(chartStatus, dtStatus.AsEnumerable().Select(r =>
            (
                string.IsNullOrWhiteSpace(r["Status"]?.ToString()) ? "نامشخص" : r["Status"].ToString()!,
                r["RecordCount"] == DBNull.Value ? 0d : Convert.ToDouble(r["RecordCount"])
            )));

            // ۳. نمودار مقایسه‌ی مبالغ (همان داده‌ی KPI؛ بدون کوئری اضافه)
            var s = chartCompare.Series[0];
            s.Points.Clear();
            chartCompare.Annotations.Clear();
            AddBar(s, "قراردادهای کارفرما", clientVal, ModernTheme.Primary);
            AddBar(s, "قراردادهای پیمانکار", contractorVal, ModernTheme.Orange);
            AddBar(s, "مطالبات", Math.Max(0, receivables), ModernTheme.Success);
            AddBar(s, "بدهی‌ها", Math.Max(0, payables), ModernTheme.Danger);
            if (clientVal + contractorVal + receivables + payables == 0) ModernChart.ShowEmpty(chartCompare);

            // ۴. جدول‌ها
            dgvExpiring.DataSource = ds.Tables["ExpiringContracts"];
            dgvAtRisk.DataSource = ds.Tables["AtRiskContracts"];
            lblUpdated.Text = "آخرین بروزرسانی: " + StringHelper.ConvertToPersianDigits(DateTime.Now.ToString("HH:mm"));
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractsManagementDashboardForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری داشبورد:\n" + ex.Message);
        }
        finally
        {
            if (!IsDisposed) btnRefresh.Enabled = true;
            try { _loadLock.Release(); } catch (ObjectDisposedException) { }
        }
    }

    private static void AddBar(Series s, string label, decimal value, Color color)
    {
        int i = s.Points.AddXY(label, value);
        s.Points[i].Color = color;
    }
}
