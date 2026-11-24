using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class BatchPayslipPrintForm : Form
{
    private readonly string _connectionString;

    private ComboBox cmbYear = null!, cmbMonth = null!;
    private Button btnLoad = null!, btnPrint = null!;
    private CheckBox chkSelectAll = null!;
    private Label lblCount = null!;

    // سه گروه: قراردادی/ماهانه، روزمزد، ساعتی (هرکدام جدول و لیست جدا)
    private static readonly string[] GroupTitles = { "قراردادی / ماهانه", "روزمزد", "ساعتی" };
    private PillSwitcher tabGroups = null!;
    private readonly DataGridView[] _grids = new DataGridView[3];
    private readonly BindingList<PayrollPrintRow>[] _groupRows =
    {
        new BindingList<PayrollPrintRow>(), new BindingList<PayrollPrintRow>(), new BindingList<PayrollPrintRow>()
    };
    private IEnumerable<PayrollPrintRow> AllRows => _groupRows.SelectMany(g => g);
    private BindingList<PayrollPrintRow> ActiveRows => _groupRows[Math.Max(0, tabGroups.SelectedIndex)];
    private DataTable? _payrollsDt;          // داده ذخیره‌شده‌ی ماه، برای چاپ فیش کامل
    private string _loadedYearMonth = "";    // دوره‌ای که واقعاً بارگذاری شده

    private PrintDocument? _printDoc;
    private int _printIndex;
    private List<PayrollPrintRow> _printQueue = new();

    // ✅ FIX P2: جلوگیری از تداخل و کرش
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public BatchPayslipPrintForm(string connectionString)
    {
        _connectionString = connectionString;

        BuildUI();
        this.ApplyStandardHeader("چاپ گروهی فیش‌های حقوقی", new Size(1050, 720));
        ShowInTaskbar = false;

        LoadDefaults();
        ShortcutHelper.ApplyStandardShortcuts(this);

        FormClosed += (s, e) => _printDoc?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "BatchPayslipPrintForm"); } }
        base.Dispose(disposing);
    }

    private void LoadDefaults()
    {
        try
        {
            var pc = new PersianCalendar();
            int y = pc.GetYear(DateTime.Now);
            int m = pc.GetMonth(DateTime.Now);

            if (cmbYear.Items.Contains(y)) cmbYear.SelectedItem = y;
            else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;

            if (cmbMonth.Items.Contains(m)) cmbMonth.SelectedItem = m;
            else if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
        }
        catch
        {
            if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;
            if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
        }
    }

    private void BuildUI()
    {
        // ── نوار انتخاب دوره ──
        var filter = ModernReport.CreateFilterBar();

        cmbYear = ModernUI.Combo();
        for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);
        ModernReport.AddFilter(filter, "سال", cmbYear, 120);

        cmbMonth = ModernUI.Combo();
        for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);
        ModernReport.AddFilter(filter, "ماه", cmbMonth, 120);

        var load = ModernButton.Primary("نمایش لیست", 140, 36);
        load.Margin = new Padding(12, 4, 0, 0);
        load.Click += async (s, e) => await BtnLoad_Click(s, e);
        btnLoad = load;
        filter.Controls.Add(load);

        var chkAll = ModernUI.Check("انتخاب / لغو همه");
        chkAll.Width = 180;
        chkAll.Margin = new Padding(16, 8, 4, 0);
        chkAll.CheckedChanged += ChkSelectAll_CheckedChanged;
        chkSelectAll = chkAll;
        filter.Controls.Add(chkAll);

        // ── جدول‌ها (سه گروه) ──
        tabGroups = new PillSwitcher(GroupTitles, ModernTheme.Success) { Margin = new Padding(16, 4, 16, 8) };
        for (int i = 0; i < 3; i++)
        {
            _grids[i] = CreateGrid();
            tabGroups.AddToView(i, _grids[i]);
        }
        tabGroups.SelectedIndexChanged += (s, e) =>
        {
            UpdateCountLabel();
            if (_groupRows is not null && chkSelectAll is not null)
            {
                var rows = ActiveRows;
                chkSelectAll.CheckedChanged -= ChkSelectAll_CheckedChanged;
                chkSelectAll.Checked = rows.Count > 0 && rows.All(r => r.Selected);
                chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;
            }
        };
        var gridHost = tabGroups;
        gridHost.Dock = DockStyle.Fill;

        // ── نوار پایین ──
        var print = ModernButton.Success("🖨️ چاپ فیش‌های انتخاب شده (گروه فعال)", 320, 40);
        print.Enabled = false;
        print.Click += BtnPrint_Click;
        btnPrint = print;

        lblCount = ModernUI.Caption("", 10f, false, ModernTheme.TextMuted);
        lblCount.AutoSize = true;
        lblCount.Margin = new Padding(16, 12, 0, 0);

        var bar = ModernForm.CreateButtonBar(print, lblCount);
        bar.WrapContents = false;
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(gridHost, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
    }

    private DataGridView CreateGrid()
    {
        var dgv = new DataGridView
        {
            AllowUserToAddRows = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Color.White
        };
        dgv.ApplyModernStyle();
        dgv.FillColumns();
        dgv.ReadOnly = false;

        dgv.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", DataPropertyName = "Selected", HeaderText = "چاپ", FillWeight = 10 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonnelCode", DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", ReadOnly = true, FillWeight = 20 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام و نام خانوادگی", ReadOnly = true, FillWeight = 40 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "NetPay", DataPropertyName = "NetPay", HeaderText = "خالص پرداختی (ریال)", ReadOnly = true, FillWeight = 30 });
        dgv.Columns["NetPay"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgv.Columns[e.ColumnIndex].Name == "NetPay" && e.Value is decimal d)
            {
                e.Value = NumberHelper.FormatThousands(d, usePersianDigits: true);
                e.FormattingApplied = true;
            }
        };

        dgv.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (dgv.IsCurrentCellDirty && dgv.CurrentCell is DataGridViewCheckBoxCell)
                dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        dgv.CellValueChanged += (s, e) => { if (e.RowIndex >= 0) UpdateCountLabel(); };
        return dgv;
    }

    private async Task BtnLoad_Click(object? sender, EventArgs e)
    {
        if (cmbYear.SelectedItem is null || cmbMonth.SelectedItem is null || IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            int year = Convert.ToInt32(cmbYear.SelectedItem);
            int month = Convert.ToInt32(cmbMonth.SelectedItem);

            var payrollRepo = new PayrollRepository(_connectionString);
            bool hasSaved = await payrollRepo.HasPayrollsForMonthAsync(year, month);
            if (!hasSaved)
            {
                ModernMessageBox.ShowWarning(
                    "برای این ماه هیچ فیش حقوقی ذخیره نشده است.\n\n" +
                    "ابتدا از منوی «پردازش حقوق»، فیش‌ها را محاسبه و ذخیره کنید، سپس دوباره تلاش کنید.");
                return;
            }

            List<PayrollPrintRow>[]? newGroups = null;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var payrollsDt = await payrollRepo.GetSavedPayrollsAsync(year, month);
                _payrollsDt = payrollsDt;
                _loadedYearMonth = $"{TranslationHelper.GetMonthName(month)} {year}";

                if (IsDisposed) return; // ✅ FIX: جلوگیری از کرش

                var groups = new[] { new List<PayrollPrintRow>(), new List<PayrollPrintRow>(), new List<PayrollPrintRow>() };
                bool hasType = payrollsDt.Columns.Contains("PersonnelType");
                foreach (DataRow row in payrollsDt.Rows)
                {
                    // نوع پرسنل با همان منطق محاسبه حقوق تعیین می‌شود (ماهانه/قراردادی/رسمی/پیمانی ← گروه اول)
                    var kind = PayrollCalculatorService.ResolveKind(hasType ? row["PersonnelType"]?.ToString() : null);
                    int g = kind == PayrollKind.Daily ? 1 : kind == PayrollKind.Hourly ? 2 : 0;
                    groups[g].Add(new PayrollPrintRow
                    {
                        Selected = true,
                        EmployeeId = Convert.ToInt32(row["EmployeeId"]),
                        PersonnelCode = row["PersonnelCode"].ToString() ?? string.Empty,
                        EmployeeName = row["EmployeeName"].ToString() ?? string.Empty,
                        NetPay = Convert.ToDecimal(row["NetPay"]),
                        PayrollId = Convert.ToInt32(row["Id"])
                    });
                }

                newGroups = groups;
            }, "در حال بارگذاری اطلاعات...");

            // ✅ FIX: بررسی IsDisposed بعد از await
            if (success && newGroups is not null && !IsDisposed)
            {
                int firstNonEmpty = -1;
                for (int i = 0; i < 3; i++)
                {
                    _groupRows[i] = new BindingList<PayrollPrintRow>(newGroups[i]);
                    _grids[i].DataSource = _groupRows[i];
                    tabGroups.SetCount(i, newGroups[i].Count);
                    if (firstNonEmpty < 0 && newGroups[i].Count > 0) firstNonEmpty = i;
                }
                if (firstNonEmpty >= 0) tabGroups.SelectedIndex = firstNonEmpty;
                chkSelectAll.CheckedChanged -= ChkSelectAll_CheckedChanged;
                chkSelectAll.Checked = true;
                chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;
                UpdateCountLabel();
                btnPrint.Enabled = AllRows.Any();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "BatchPayslipPrintForm.BtnLoad_Click");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "BatchPayslipPrintForm"); }
        }
    }

    private void ChkSelectAll_CheckedChanged(object? sender, EventArgs e)
    {
        var rows = ActiveRows;
        if (rows.Count == 0 || IsDisposed) return;

        // فقط گروه فعال (تب جاری) انتخاب / لغو می‌شود
        foreach (var row in rows) row.Selected = chkSelectAll.Checked;

        _grids[Math.Max(0, tabGroups.SelectedIndex)].Refresh();
        UpdateCountLabel();
    }

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        var selectedRows = ActiveRows.Where(r => r.Selected).ToList();
        if (selectedRows.Count == 0)
        {
            ModernMessageBox.ShowWarning($"در گروه «{tabGroups.SelectedTitle}» هیچ کارمندی برای چاپ انتخاب نشده است.");
            return;
        }
        if (_payrollsDt is null || _payrollsDt.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("ابتدا لیست را بارگذاری کنید.");
            return;
        }

        // ✅ FIX: چاپ گروهی حالا همان فیش کامل (PayslipPrinter) را چاپ می‌کند، نه فقط نام و خالص پرداختی
        var ids = new HashSet<int>(selectedRows.Select(r => r.EmployeeId));
        var filtered = _payrollsDt.Clone();
        foreach (DataRow row in _payrollsDt.Rows)
            if (ids.Contains(Convert.ToInt32(row["EmployeeId"]))) filtered.ImportRow(row);

        var company = new CompanyRepository(AppConfig.ConnectionString).GetAll().FirstOrDefault();
        Image? logo = ImageHelper.ByteArrayToImage(company?.Logo);
        new PayslipPrinter(filtered, _loadedYearMonth, company?.Name ?? "شرکت", logo).ShowPreview();
    }

    private void PrintDocument_PrintPage(object? sender, PrintPageEventArgs e)
    {
        if (_printIndex >= _printQueue.Count) return;

        var row = _printQueue[_printIndex];
        var g = e.Graphics;
        if (g is null) return;

        float yPos = 50f;
        float leftMargin = 50f;
        float rightMargin = (float)e.MarginBounds.Right - 50f;
        float centerX = (float)e.MarginBounds.Width / 2f;

        // فونت‌های ModernTheme کش‌شده‌اند؛ Dispose نشوند
        var fontTitle = ModernTheme.GetFont(16f, FontStyle.Bold);
        var fontText = ModernTheme.GetFont(12f);

        var format = new StringFormat { Alignment = StringAlignment.Near, FormatFlags = StringFormatFlags.DirectionRightToLeft };

        var titlePoint = new PointF(centerX - 40f, yPos);
        g.DrawString("فیش حقوقی", fontTitle, Brushes.Black, titlePoint);

        yPos += 40f;

        g.DrawString($"کد پرسنلی: {row.PersonnelCode}", fontText, Brushes.Black, new PointF(rightMargin, yPos), format);
        g.DrawString($"نام و نام خانوادگی: {row.EmployeeName}", fontText, Brushes.Black, new PointF(rightMargin - 200f, yPos), format);

        yPos += 30f;
        g.DrawString($"خالص پرداختی: {NumberHelper.FormatThousands(row.NetPay, usePersianDigits: true)} ریال", fontText, Brushes.Black, new PointF(rightMargin, yPos), format);

        g.DrawLine(Pens.Gray, leftMargin, yPos + 20f, rightMargin, yPos + 20f);

        _printIndex++;
        e.HasMorePages = _printIndex < _printQueue.Count;
    }

    private void UpdateCountLabel()
    {
        if (lblCount is null || tabGroups is null || IsDisposed) return;
        var rows = ActiveRows;
        int sel = rows.Count(r => r.Selected);
        int total = AllRows.Count();
        lblCount.Text = $"{tabGroups.SelectedTitle}: {NumberHelper.FormatThousands(rows.Count, usePersianDigits: true)} نفر | انتخاب شده: {NumberHelper.FormatThousands(sel, usePersianDigits: true)}   ·   کل گروه‌ها: {NumberHelper.FormatThousands(total, usePersianDigits: true)}";
    }
}

public class PayrollPrintRow : INotifyPropertyChanged
{
    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected))); }
    }

    public int EmployeeId { get; set; }
    public int PayrollId { get; set; }
    public string PersonnelCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public decimal NetPay { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
