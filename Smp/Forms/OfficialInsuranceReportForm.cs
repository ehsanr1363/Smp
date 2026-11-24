using ClosedXML.Excel;
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

namespace Smp.Forms;

public class OfficialInsuranceReportForm : Form
{
    private readonly PayrollRepository _repository;

    private ComboBox cmbYear = null!, cmbMonth = null!;
    private Button btnLoad = null!, btnExport = null!;
    private DataGridView dgvInsurance = null!;
    private DataTable? _currentData;

    private ModernStatCard lblTotalCount = null!, lblTotalEarnings = null!, lblTotalInsuranceEmp = null!, lblTotalInsuranceEmpr = null!, lblTotalInsurance = null!;

    // ✅ FIX P2: جلوگیری از بارگذاری همزمان
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public OfficialInsuranceReportForm(string connectionString)
    {
        _repository = new PayrollRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("گزارش رسمی لیست بیمه تأمین اجتماعی", new Size(1200, 750));
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnExport);

        LoadDefaults();
    }

    // ✅ FIX P2
    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _loadLock?.Dispose(); } catch { } }
        base.Dispose(disposing);
    }

    private void LoadDefaults()
    {
        var pc = new PersianCalendar();
        int currentYear = pc.GetYear(DateTime.Now);
        int currentMonth = pc.GetMonth(DateTime.Now);

        if (cmbYear.Items.Contains(currentYear)) cmbYear.SelectedItem = currentYear;
        else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;

        if (cmbMonth.Items.Contains(currentMonth)) cmbMonth.SelectedItem = currentMonth;
        else if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
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
        load.Margin = new Padding(0, 4, 18, 0);
        load.Click += async (s, e) => await LoadDataSafeAsync();
        btnLoad = load;
        filter.Controls.Add(load);

        var export = ModernButton.Success("📥 خروجی اکسل رسمی", 200, 36);
        export.Margin = new Padding(0, 4, 12, 0);
        export.Enabled = false;
        export.Click += async (s, e) => await ExportDataSafeAsync();
        btnExport = export;
        filter.Controls.Add(export);

        // ── کارت‌های خلاصه ──
        var strip = new StatStrip(5, 76);
        lblTotalCount = strip.Add("تعداد پرسنل", ModernTheme.Dark);
        lblTotalEarnings = strip.Add("جمع مزایا (ریال)", ModernTheme.Primary);
        lblTotalInsuranceEmp = strip.Add("بیمه سهم کارمند", ModernTheme.Orange);
        lblTotalInsuranceEmpr = strip.Add("بیمه سهم کارفرما", ModernTheme.Purple);
        lblTotalInsurance = strip.Add("جمع کل بیمه", ModernTheme.Danger);

        // ── جدول ──
        dgvInsurance = new DataGridView();
        dgvInsurance.ApplyModernStyle();
        dgvInsurance.FillColumns();
        // FillColumns مقدارها را MiddleRight می‌کند که در گرید RTL زیر هدرِ وسط‌چین نمی‌نشیند؛ همه‌چیز وسط‌چین شود
        dgvInsurance.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvInsurance.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        dgvInsurance.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (e.Value is decimal dec)
            {
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.FormattingApplied = true;
            }
            else if (e.Value is int i)
            {
                e.Value = NumberHelper.FormatThousands(i, usePersianDigits: true);
                e.FormattingApplied = true;
            }
        };

        var gridHost = ModernReport.WrapGrid(dgvInsurance);
        gridHost.Margin = new Padding(16, 8, 16, 12);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        strip.Margin = new Padding(10, 4, 10, 4);
        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(strip, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        Controls.Add(root);
    }

    private async Task LoadDataSafeAsync()
    {
        if (cmbYear.SelectedItem is null || cmbMonth.SelectedItem is null || IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            int year = Convert.ToInt32(cmbYear.SelectedItem);
            int month = Convert.ToInt32(cmbMonth.SelectedItem);

            DataTable? result = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                result = await _repository.GetOfficialInsuranceReportAsync(year, month);
            }, "در حال بارگذاری لیست بیمه...");

            // ✅ FIX: بررسی IsDisposed بعد از await
            if (!success || result is null || IsDisposed) return;

            _currentData = result;

            if (_currentData.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("برای این ماه هیچ فیش حقوقی ذخیره نشده است.");
                ResetLabels();
                btnExport.Enabled = false;
                dgvInsurance.DataSource = null;
            }
            else
            {
                dgvInsurance.DataSource = _currentData;

                int count = _currentData.Rows.Count;
                decimal totalEarnings = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("مبلغ مشمول بیمه") ?? 0m);
                decimal totalInsEmp = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("سهم کارمند") ?? 0m);
                decimal totalInsEmpr = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("سهم کارفرما") ?? 0m);
                decimal totalIns = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("جمع بیمه") ?? 0m);

                lblTotalCount.Text = NumberHelper.FormatThousands(count, usePersianDigits: true);
                lblTotalEarnings.Text = NumberHelper.FormatThousands(totalEarnings, usePersianDigits: true);
                lblTotalInsuranceEmp.Text = NumberHelper.FormatThousands(totalInsEmp, usePersianDigits: true);
                lblTotalInsuranceEmpr.Text = NumberHelper.FormatThousands(totalInsEmpr, usePersianDigits: true);
                lblTotalInsurance.Text = NumberHelper.FormatThousands(totalIns, usePersianDigits: true);

                btnExport.Enabled = true;
            }
        }
        catch (ObjectDisposedException) { /* نادیده بگیر */ }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "OfficialInsuranceReportForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری گزارش:\n" + ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch { }
        }
    }

    private void ResetLabels()
    {
        lblTotalCount.Text = "0"; lblTotalEarnings.Text = "0";
        lblTotalInsuranceEmp.Text = "0"; lblTotalInsuranceEmpr.Text = "0"; lblTotalInsurance.Text = "0";
    }

    private async Task ExportDataSafeAsync()
    {
        if (_currentData is null || _currentData.Rows.Count == 0 || IsDisposed) return;

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            FileName = $"Insurance_List_{cmbYear.SelectedItem}_{cmbMonth.SelectedItem}.xlsx",
            Title = "ذخیره لیست رسمی بیمه",
            OverwritePrompt = true
        };

        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("لیست بیمه");
                ws.RightToLeft = true;

                string title = $"لیست بیمه تأمین اجتماعی - {TranslationHelper.GetMonthName(Convert.ToInt32(cmbMonth.SelectedItem))} {cmbYear.SelectedItem}";
                ws.Cell(1, 1).Value = title;
                ws.Range(1, 1, 1, _currentData.Columns.Count).Merge().Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Font.FontSize = 14;
                ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                for (int i = 0; i < _currentData.Columns.Count; i++)
                {
                    var cell = ws.Cell(3, i + 1);
                    cell.Value = _currentData.Columns[i].ColumnName;
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                for (int row = 0; row < _currentData.Rows.Count; row++)
                {
                    for (int col = 0; col < _currentData.Columns.Count; col++)
                    {
                        var cell = ws.Cell(row + 4, col + 1);
                        var value = _currentData.Rows[row][col];
                        if (value is decimal decValue)
                        {
                            cell.Value = decValue;
                            cell.Style.NumberFormat.Format = "#,##0";
                        }
                        else
                        {
                            cell.Value = value?.ToString() ?? "";
                        }
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                }

                int totalRow = _currentData.Rows.Count + 4;
                ws.Cell(totalRow, 1).Value = "جمع کل";
                // ✅ FIX: ردیف جمع قبلاً فقط برچسب داشت؛ حالا جمع ستون‌های عددی را هم می‌نویسد
                for (int totCol = 0; totCol < _currentData.Columns.Count; totCol++)
                {
                    if (_currentData.Columns[totCol].DataType != typeof(decimal)) continue;
                    decimal totSum = 0m;
                    foreach (DataRow totRow in _currentData.Rows)
                        if (totRow[totCol] != DBNull.Value) totSum += Convert.ToDecimal(totRow[totCol]);
                    var totCell = ws.Cell(totalRow, totCol + 1);
                    totCell.Value = totSum;
                    totCell.Style.NumberFormat.Format = "#,##0";
                    totCell.Style.Font.Bold = true;
                    totCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                ws.Range(totalRow, 1, totalRow, _currentData.Columns.Count).Style.Fill.BackgroundColor = XLColor.FromArgb(240, 230, 220);
                ws.Cell(totalRow, 1).Style.Font.Bold = true;

                ws.Columns().AdjustToContents();
                workbook.SaveAs(sfd.FileName);
            });
        }, "در حال تولید فایل اکسل...");

        if (success && !IsDisposed)
        {
            ToastHelper.Show("فایل اکسل لیست بیمه با موفقیت ذخیره شد.", isSuccess: true);
        }
    }
}
