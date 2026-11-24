using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Helpers;

namespace Smp.Forms;

public class InsuranceSummaryReportForm : Form
{
    private readonly string _connectionString;

    private DataGridView dgvReport = null!;
    private ComboBox cmbContractFilter = null!;
    private ComboBox cmbYearFilter = null!;
    private Label lblSummary = null!;
    private Button btnExport = null!, btnRefresh = null!, btnDeleteRow = null!;

    private DataTable? _dataTable;
    private bool _isLoading;
    private int _deletedCount = 0;

    public bool IsImportAllowed { get; private set; } = true;

    private const string ContractTableName = "ClientContracts";
    private const string HeaderTableName = "WorkshopRecords";
    private const string DetailsTableName = "InsuranceRecords";

    public InsuranceSummaryReportForm(string connectionString)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? throw new ArgumentException("رشته اتصال نامعتبر است.", nameof(connectionString))
            : connectionString;

        BuildUI();

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnExport);
        ShowInTaskbar = false;

        Shown += async (s, e) =>
        {
            await LoadFiltersSafeAsync();
            await LoadReportSafeAsync();
        };
    }

    // ✅ FIX: CleanNum فقط برای پاکسازی اولیه — ولی حالا در CTE هر فیلد یک بار محاسبه می‌شود
    private static string CleanNum(string field)
    {
        return $@"REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ISNULL({field}, N''))), 
                N'۰', N'0'), N'۱', N'1'), N'۲', N'2'), N'۳', N'3'), N'۴', N'4'), 
                N'۵', N'5'), N'۶', N'6'), N'۷', N'7'), N'۸', N'8'), N'۹', N'9'),
                N'٠', N'0'), N'١', N'1'), N'٢', N'2'), N'٣', N'3'), N'٤', N'4'),
                N'٥', N'5'), N'٦', N'6'), N'٧', N'7'), N'٨', N'8'), N'٩', N'9'),
                N',', N''), N'٬', N''), N' ', N'')";
    }

    private void BuildUI()
    {
        // ── نوار فیلتر ──
        var filterPanel = ModernReport.CreateFilterBar();

        cmbContractFilter = ModernUI.Combo();
        cmbContractFilter.SelectedIndexChanged += async (s, e) => { if (!_isLoading) await LoadReportSafeAsync(); };
        ModernReport.AddFilter(filterPanel, "کد پیمان", cmbContractFilter, 320);

        cmbYearFilter = ModernUI.Combo();
        cmbYearFilter.SelectedIndexChanged += async (s, e) => { if (!_isLoading) await LoadReportSafeAsync(); };
        ModernReport.AddFilter(filterPanel, "سال", cmbYearFilter, 120);

        var refresh = ModernButton.Soft("🔄 بروزرسانی", ModernTheme.Neutral, 140, 36);
        refresh.Margin = new Padding(12, 4, 0, 0);
        refresh.Click += async (s, e) => { await LoadFiltersSafeAsync(); await LoadReportSafeAsync(); };
        btnRefresh = refresh;
        filterPanel.Controls.Add(refresh);

        dgvReport = new DataGridView { AutoGenerateColumns = false };
        dgvReport.ApplyModernStyle();
        dgvReport.FillColumns();
        dgvReport.ShowCellToolTips = true;

        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "WorkshopCode", Name = "WorkshopCode", HeaderText = "کد کارگاه", FillWeight = 8 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ListNumber", Name = "ListNumber", HeaderText = "شماره لیست", FillWeight = 7 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractRow", Name = "ContractRow", HeaderText = "ردیف پیمان", FillWeight = 7 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ClientContractNumber", Name = "ClientContractNumber", HeaderText = "شماره قرارداد", FillWeight = 9 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Year", Name = "Year", HeaderText = "سال", FillWeight = 5 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Month", Name = "Month", HeaderText = "ماه", FillWeight = 5 });

        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "H10", Name = "H10", HeaderText = "تعداد افراد", FillWeight = 6 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "H11", Name = "H11", HeaderText = "مجموع روزهای کارکرد", FillWeight = 9 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "H12", Name = "H12", HeaderText = "مجموع دستمزد روزانه", FillWeight = 10 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "H13", Name = "H13", HeaderText = "مجموع دستمزد ماهانه", FillWeight = 10 });

        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "H15", Name = "H15", HeaderText = "مجموع حقوق و مزایای مشمول", FillWeight = 12 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "H16", Name = "H16", HeaderText = "جمع کل حقوق و مزایا", FillWeight = 12 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "H17", Name = "H17", HeaderText = "بیمه سهم بیمه شده", FillWeight = 9 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "H18", Name = "H18", HeaderText = "بیمه سهم کارفرما", FillWeight = 9 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "H19", Name = "H19", HeaderText = "بیمه بیکاری", FillWeight = 8 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "H30", Name = "H30", HeaderText = "جمع بیمه ۳۰٪", FillWeight = 10 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "H24", Name = "H24", HeaderText = "پایه سنواتی ماهانه", FillWeight = 9 });
        dgvReport.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "H25", Name = "H25", HeaderText = "حق تاهل ماهانه", FillWeight = 9 });

        // ✅ عنوان فارسی ستون‌های H10..H30 با نام انگلیسی جایگزین نشود
        dgvReport.PinHeaderTexts();

        foreach (DataGridViewColumn col in dgvReport.Columns)
        {
            if (col.Name.StartsWith("H"))
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        dgvReport.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _dataTable is null) return;

            string colName = dgvReport.Columns[e.ColumnIndex].Name;

            if (colName.StartsWith("H") && e.Value is double dVal)
            {
                e.Value = NumberHelper.FormatThousands((decimal)dVal, usePersianDigits: true);
                e.FormattingApplied = true;
            }
            else if (colName.StartsWith("H") && e.Value is decimal decVal)
            {
                e.Value = NumberHelper.FormatThousands(decVal, usePersianDigits: true);
                e.FormattingApplied = true;
            }
            else if (colName.StartsWith("H") && e.Value is int iVal)
            {
                e.Value = NumberHelper.FormatThousands(iVal, usePersianDigits: true);
                e.FormattingApplied = true;
            }

            string diffColName = GetDiffColumnName(colName);
            if (!string.IsNullOrEmpty(diffColName))
            {
                var diffObj = _dataTable.Rows[e.RowIndex][diffColName];
                if (diffObj != DBNull.Value && diffObj is not null)
                {
                    decimal diff = Convert.ToDecimal(diffObj);
                    if (Math.Abs(diff) >= 1000)
                    {
                        e.CellStyle.BackColor = ModernTheme.DangerSoft;
                        e.CellStyle.ForeColor = ModernTheme.DangerText;
                        e.CellStyle.SelectionBackColor = ModernTheme.DangerSoft;
                        e.CellStyle.SelectionForeColor = ModernTheme.DangerText;
                        e.CellStyle.Font = ModernTheme.GetFont(11f, FontStyle.Bold);
                    }
                    else if (Math.Abs(diff) > 0)
                    {
                        e.CellStyle.BackColor = ModernTheme.WarningSoft;
                        e.CellStyle.ForeColor = ModernTheme.WarningText;
                        e.CellStyle.SelectionBackColor = ModernTheme.WarningSoft;
                        e.CellStyle.SelectionForeColor = ModernTheme.WarningText;
                    }
                }
            }
        };

        dgvReport.CellToolTipTextNeeded += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _dataTable is null) return;

            string colName = dgvReport.Columns[e.ColumnIndex].Name;
            string diffColName = GetDiffColumnName(colName);

            if (!string.IsNullOrEmpty(diffColName))
            {
                var diffObj = _dataTable.Rows[e.RowIndex][diffColName];
                if (diffObj != DBNull.Value && diffObj is not null)
                {
                    decimal diff = Convert.ToDecimal(diffObj);
                    if (Math.Abs(diff) >= 1000)
                        e.ToolTipText = $"❌ مغایرت: {NumberHelper.FormatThousands(Math.Abs(diff), usePersianDigits: true)} ریال\nمبلغ هدر با جمع جزئیات هم‌خوانی ندارد!";
                    else if (Math.Abs(diff) > 0)
                        e.ToolTipText = $"⚠️ اختلاف جزئی: {NumberHelper.FormatThousands(Math.Abs(diff), usePersianDigits: true)} ریال (ناشی از گرد کردن)";
                }
            }
        };

        var ctxMenu = new ContextMenuStrip { Font = ModernTheme.GetFont(11f), RightToLeft = RightToLeft.Yes };
        var miDeleteRow = new ToolStripMenuItem("🗑 حذف ردیف انتخاب‌شده");
        miDeleteRow.Click += (s, e) => DeleteSelectedRow();
        ctxMenu.Items.Add(miDeleteRow);

        ctxMenu.Items.Add(new ToolStripSeparator());

        var miDeleteErrors = new ToolStripMenuItem("⛔ حذف همه‌ی ردیف‌های دارای مغایرت (≥ ۱۰۰۰ ریال)");
        miDeleteErrors.Click += (s, e) => DeleteAllErrorRows();
        ctxMenu.Items.Add(miDeleteErrors);

        ctxMenu.Items.Add(new ToolStripSeparator());

        var miReset = new ToolStripMenuItem("🔄 بازگردانی (Reload از دیتابیس)");
        miReset.Click += async (s, e) => { await LoadFiltersSafeAsync(); await LoadReportSafeAsync(); };
        ctxMenu.Items.Add(miReset);

        dgvReport.ContextMenuStrip = ctxMenu;

        dgvReport.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete) { DeleteSelectedRow(); e.Handled = true; }
        };

        var gridHost = ModernReport.WrapGrid(dgvReport);
        gridHost.Margin = new Padding(16, 4, 16, 8);

        lblSummary = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,   // فرم Mirror شده: متن سمت راست
            Font = ModernTheme.GetFont(11f, FontStyle.Bold),
            ForeColor = ModernTheme.Text,
            BackColor = Color.White,
            Padding = new Padding(14, 0, 14, 0),
            Margin = new Padding(16, 0, 16, 8),
            Text = "—"
        };

        var export = ModernButton.Create("📊 خروجی اکسل", ModernTheme.Brand, 160, 40);
        export.Click += BtnExport_Click;
        btnExport = export;

        var del = ModernButton.Danger("🗑 حذف ردیف انتخاب‌شده", 220, 40);
        del.Click += (s, e) => DeleteSelectedRow();
        btnDeleteRow = del;

        var close = ModernButton.Cancel("✖ بستن", 120, 40);
        close.Click += (s, e) => Close();

        var bar = ModernForm.CreateButtonBar(export, del, close);
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(filterPanel, 0, 0);
        root.Controls.Add(gridHost, 0, 1);
        root.Controls.Add(lblSummary, 0, 2);
        root.Controls.Add(bar, 0, 3);
        Controls.Add(root);

        this.ApplyStandardHeader("گزارش تطبیق و اعتبارسنجی بیمه (فایل ۱ و ۲)", new Size(1850, 850), isDialog: false);
        this.MinimumSize = new Size(1100, 600);
    }

    private string GetDiffColumnName(string colName)
    {
        return colName switch
        {
            "H10" => "Diff10",
            "H11" => "Diff11",
            "H12" => "Diff12",
            "H13" => "Diff13",
            "H15" => "Diff15",
            "H16" => "Diff16",
            "H17" => "Diff17",
            "H18" => "Diff18",
            "H19" => "Diff19",
            "H30" => "Diff30",
            "H24" => "Diff24",
            "H25" => "Diff25",
            _ => ""
        };
    }

    private async Task LoadFiltersSafeAsync()
    {
        _isLoading = true;
        try
        {
            var (success, tupleResult) = await AsyncHelper.RunWithLoading(
                this,
                () => Task.Run(() =>
                {
                    string sqlContracts = $"SELECT c.Id, COALESCE(c.ContractNumber, '') + N' — ' + COALESCE(c.Subject, '') AS Display FROM {ContractTableName} c ORDER BY c.Id";
                    var dtContracts = DbHelper.ExecuteQuery(_connectionString, sqlContracts);

                    string sqlYears = $"SELECT DISTINCT DSK_YY FROM {HeaderTableName} WHERE DSK_YY IS NOT NULL ORDER BY DSK_YY DESC";
                    var dtYears = DbHelper.ExecuteQuery(_connectionString, sqlYears);

                    return (dtContracts, dtYears);
                }),
                "در حال بارگذاری فیلترها...");

            if (!success) return;

            cmbContractFilter.Items.Clear();
            cmbContractFilter.Items.Add(new ComboItem(0, "همه"));
            foreach (DataRow row in tupleResult.dtContracts.Rows)
            {
                int id = Convert.ToInt32(row[0]);
                string display = row[1].ToString() ?? $"#{id}";
                cmbContractFilter.Items.Add(new ComboItem(id, display));
            }
            cmbContractFilter.SelectedIndex = 0;

            cmbYearFilter.Items.Clear();
            cmbYearFilter.Items.Add("همه");
            foreach (DataRow row in tupleResult.dtYears.Rows)
            {
                cmbYearFilter.Items.Add(row[0].ToString());
            }
            cmbYearFilter.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "InsuranceSummaryReportForm.LoadFilters");
            ModernMessageBox.ShowError("خطا در بارگذاری فیلترها:\n" + ex.Message);
        }
        finally { _isLoading = false; }
    }

    private async Task LoadReportSafeAsync()
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            _dataTable = null;
            _deletedCount = 0;
            if (dgvReport.DataSource is not null) dgvReport.DataSource = null;

            IsImportAllowed = true;

            int contractId = GetSelectedContractId();
            int? year = GetSelectedYear();

            string sql = BuildReportQuery(contractId, year);
            var parameters = new List<SqlParameter>();
            if (contractId > 0) parameters.Add(new SqlParameter("@contractId", contractId));
            if (year.HasValue) parameters.Add(new SqlParameter("@year", year.Value));

            var (success, dt) = await AsyncHelper.RunWithLoading(
                this,
                () => Task.Run(() => DbHelper.ExecuteQuery(_connectionString, sql, parameters.ToArray())),
                "در حال بارگذاری گزارش...");

            if (!success || dt is null) return;

            _dataTable = dt;
            dgvReport.DataSource = _dataTable;

            ValidateImportConditions();
            UpdateSummaryLabel();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "InsuranceSummaryReportForm.LoadReport");
            ModernMessageBox.ShowError("خطا در بارگذاری گزارش:\n" + ex.Message);
            if (lblSummary is not null) lblSummary.Text = "— خطا در بارگذاری —";
        }
        finally { _isLoading = false; }
    }

    private void DeleteSelectedRow()
    {
        if (_dataTable is null || _dataTable.Rows.Count == 0) return;

        int idx = dgvReport.CurrentRow?.Index ?? -1;
        if (idx < 0 || idx >= _dataTable.Rows.Count)
        {
            ModernMessageBox.ShowWarning("لطفاً ابتدا یک ردیف را انتخاب کنید.");
            return;
        }

        var row = _dataTable.Rows[idx];
        string info = $"کد کارگاه: {row["WorkshopCode"]}\r\nشماره لیست: {row["ListNumber"]}\r\nردیف پیمان: {row["ContractRow"]}\r\nشماره قرارداد: {row["ClientContractNumber"]}";

        if (!ModernMessageBox.AskConfirm($"این ردیف از نمای گزارش حذف شود؟\r\n\r\n{info}\r\n\r\nتوجه: داده در دیتابیس حذف نمی‌شود؛ فقط از این گزارش و خروجی اکسل کنار گذاشته می‌شود.", "حذف ردیف")) return;

        _dataTable.Rows.RemoveAt(idx);
        _deletedCount++;
        RebindAndValidate();
    }

    private void DeleteAllErrorRows()
    {
        if (_dataTable is null || _dataTable.Rows.Count == 0) return;

        string[] diffColumns = { "Diff10", "Diff11", "Diff12", "Diff13", "Diff15", "Diff16", "Diff17", "Diff18", "Diff19", "Diff30", "Diff24", "Diff25" };
        var toRemove = new List<DataRow>();

        foreach (DataRow row in _dataTable.Rows)
        {
            foreach (var col in diffColumns)
            {
                if (row[col] != DBNull.Value && Math.Abs(Convert.ToDecimal(row[col])) >= 1000)
                {
                    toRemove.Add(row);
                    break;
                }
            }
        }

        if (toRemove.Count == 0)
        {
            ModernMessageBox.ShowWarning("هیچ ردیف دارای مغایرت (≥ ۱۰۰۰ ریال) یافت نشد.");
            return;
        }

        if (!ModernMessageBox.AskConfirm($"{NumberHelper.FormatThousands(toRemove.Count, usePersianDigits: true)} ردیف دارای مغایرت از نمای گزارش حذف شوند؟\r\n\r\nتوجه: داده در دیتابیس حذف نمی‌شود.", "حذف ردیف‌های خطادار")) return;

        foreach (var r in toRemove)
        {
            _dataTable.Rows.Remove(r);
            _deletedCount++;
        }
        RebindAndValidate();
    }

    private void RebindAndValidate()
    {
        dgvReport.DataSource = null;
        dgvReport.DataSource = _dataTable;
        ValidateImportConditions();
        UpdateSummaryLabel();
    }

    private void ValidateImportConditions()
    {
        IsImportAllowed = true;
        if (_dataTable is null || _dataTable.Rows.Count == 0) return;

        string[] diffColumns = { "Diff10", "Diff11", "Diff12", "Diff13", "Diff15", "Diff16", "Diff17", "Diff18", "Diff19", "Diff30", "Diff24", "Diff25" };
        foreach (DataRow row in _dataTable.Rows)
        {
            foreach (var col in diffColumns)
            {
                if (row[col] != DBNull.Value && Math.Abs(Convert.ToDecimal(row[col])) >= 1000)
                {
                    IsImportAllowed = false;
                    return;
                }
            }
        }
    }

    private void UpdateSummaryLabel()
    {
        if (lblSummary is null) return;

        string deletedInfo = _deletedCount > 0 ? $"  |  🗑 حذف‌شده از نما: {NumberHelper.FormatThousands(_deletedCount, usePersianDigits: true)}" : "";

        if (_dataTable is null || _dataTable.Rows.Count == 0)
        {
            lblSummary.Text = "داده‌ای برای نمایش وجود ندارد." + deletedInfo;
            lblSummary.ForeColor = ModernTheme.Text;
            return;
        }

        string rowCountStr = NumberHelper.FormatThousands(_dataTable.Rows.Count, usePersianDigits: true);

        if (IsImportAllowed)
        {
            lblSummary.Text = $"تعداد ردیف‌ها: {rowCountStr}{deletedInfo}  |  وضعیت: ✅ مبالغ تطبیق دارند (ایمپورت مجاز است)";
            lblSummary.ForeColor = ModernTheme.Secondary;
        }
        else
        {
            string firstErrorCol = "نامشخص"; decimal maxDiff = 0;
            string[] diffColumns = { "Diff10", "Diff11", "Diff12", "Diff13", "Diff15", "Diff16", "Diff17", "Diff18", "Diff19", "Diff30", "Diff24", "Diff25" };

            foreach (DataRow row in _dataTable.Rows)
            {
                foreach (var col in diffColumns)
                {
                    if (row[col] != DBNull.Value)
                    {
                        decimal diff = Math.Abs(Convert.ToDecimal(row[col]));
                        if (diff >= 1000 && diff > maxDiff)
                        {
                            maxDiff = diff;
                            firstErrorCol = col switch
                            {
                                "Diff10" => "تعداد افراد",
                                "Diff11" => "مجموع روزهای کارکرد",
                                "Diff12" => "مجموع دستمزد روزانه",
                                "Diff13" => "مجموع دستمزد ماهانه",
                                "Diff15" => "حقوق و مزایای مشمول",
                                "Diff16" => "جمع کل حقوق و مزایا",
                                "Diff17" => "بیمه سهم بیمه شده",
                                "Diff18" => "بیمه سهم کارفرما",
                                "Diff19" => "بیمه بیکاری",
                                "Diff30" => "جمع بیمه ۳۰٪",
                                "Diff24" => "پایه سنواتی ماهانه",
                                "Diff25" => "حق تاهل ماهانه",
                                _ => "نامشخص"
                            };
                        }
                    }
                }
            }
            lblSummary.Text = $"تعداد ردیف‌ها: {rowCountStr}{deletedInfo}  |  ❌ مغایرت در ستون «{firstErrorCol}» به مبلغ {NumberHelper.FormatThousands(maxDiff, usePersianDigits: true)} ریال (ایمپورت ممنوع)";
            lblSummary.ForeColor = ModernTheme.Danger;
        }
    }

    private int GetSelectedContractId() => cmbContractFilter.SelectedItem is ComboItem item ? item.Id : 0;
    private int? GetSelectedYear() => cmbYearFilter.SelectedItem is string s && int.TryParse(s, out int y) ? y : null;

    private async void BtnExport_Click(object? sender, EventArgs e)
    {
        if (_dataTable is null || _dataTable.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("داده‌ای برای خروجی نیست.");
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"InsuranceValidation_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
            Title = "ذخیره خروجی اکسل",
            OverwritePrompt = true
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("اعتبارسنجی");

                for (int i = 0; i < dgvReport.Columns.Count; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = dgvReport.Columns[i].HeaderText;
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(248, 237, 227);
                    cell.Style.Font.FontColor = XLColor.FromArgb(90, 60, 40);
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                for (int r = 0; r < _dataTable.Rows.Count; r++)
                {
                    for (int c = 0; c < dgvReport.Columns.Count; c++)
                    {
                        string propName = dgvReport.Columns[c].DataPropertyName;
                        var cell = ws.Cell(r + 2, c + 1);
                        object? val = _dataTable.Rows[r][propName];

                        if (val is null || val == DBNull.Value) cell.Value = "";
                        else if (val is decimal or long or int or double) cell.Value = Convert.ToDecimal(val);
                        else cell.Value = val.ToString();

                        string diffColName = GetDiffColumnName(propName);
                        if (!string.IsNullOrEmpty(diffColName))
                        {
                            var diffObj = _dataTable.Rows[r][diffColName];
                            if (diffObj != DBNull.Value && diffObj is not null)
                            {
                                decimal diff = Convert.ToDecimal(diffObj);
                                if (Math.Abs(diff) >= 1000)
                                {
                                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(254, 226, 226);
                                    cell.Style.Font.FontColor = XLColor.FromArgb(153, 27, 27);
                                }
                                else if (Math.Abs(diff) > 0)
                                {
                                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(255, 243, 205);
                                    cell.Style.Font.FontColor = XLColor.FromArgb(133, 100, 4);
                                }
                            }
                        }
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }
                }

                ws.Columns().AdjustToContents();
                ws.RightToLeft = true;
                workbook.SaveAs(sfd.FileName);
            });
        }, "در حال تولید فایل اکسل...");

        if (success)
        {
            ToastHelper.Show("خروجی اکسل با موفقیت ذخیره شد.", isSuccess: true);
        }
    }

    // ✅ FIX: کوئری بهینه با CTE
    // هر فیلد (CleanNum + TRY_CAST) حالا فقط یک بار در CTE محاسبه می‌شود،
    // نه ۲-۳ بار در SELECT/GROUP BY. این باعث کاهش چشمگیر زمان اجرا می‌شود.
    private string BuildReportQuery(int contractId, int? year)
    {
        var sb = new StringBuilder();

        // ─── HeaderCalc: محاسبه یک‌بارهٔ تمام مقادیر هدر ───
        sb.AppendLine($@"
                WITH HeaderCalc AS (
                    SELECT 
                        h.WorkshopCode, h.DSK_LISTNO, h.MON_PYM, h.DSK_YY, h.DSK_MM,
                        {CleanNum("h.MON_PYM")} AS CleanMonPym,
                        TRY_CAST({CleanNum("h.DSK_NUM")} AS FLOAT) AS NumH10,
                        TRY_CAST({CleanNum("h.DSK_TDD")} AS FLOAT) AS NumH11,
                        TRY_CAST({CleanNum("h.DSK_TROOZ")} AS FLOAT) AS NumH12,
                        TRY_CAST({CleanNum("h.DSK_TMAH")} AS FLOAT) AS NumH13,
                        TRY_CAST({CleanNum("h.DSK_TMASH")} AS FLOAT) AS NumH15,
                        TRY_CAST({CleanNum("h.DSK_TTOTL")} AS FLOAT) AS NumH16,
                        TRY_CAST({CleanNum("h.DSK_TBIME")} AS FLOAT) AS NumH17,
                        TRY_CAST({CleanNum("h.DSK_TKOSO")} AS FLOAT) AS NumH18,
                        TRY_CAST({CleanNum("h.DSK_BIC")} AS FLOAT) AS NumH19,
                        TRY_CAST({CleanNum("h.DSK_INC")} AS FLOAT) AS NumH24,
                        TRY_CAST({CleanNum("h.DSK_SPOUSE")} AS FLOAT) AS NumH25
                    FROM {HeaderTableName} h
                ),

                DetailsCalc AS (
                    SELECT 
                        d.Id, d.WorkshopCode, d.Year, d.Month, d.ListNumber,
                        {CleanNum("d.ContractRow")} AS CleanContractRow,
                        TRY_CAST({CleanNum("d.DaysWorked")} AS FLOAT) AS NumD11,
                        TRY_CAST({CleanNum("d.Wage")} AS FLOAT) AS NumD12,
                        TRY_CAST({CleanNum("d.MonthlyWage")} AS FLOAT) AS NumD13,
                        TRY_CAST({CleanNum("d.WageAndBenefitsIncluded")} AS FLOAT) AS NumD15,
                        TRY_CAST({CleanNum("d.WageAndBenefitsTotal")} AS FLOAT) AS NumD16,
                        TRY_CAST({CleanNum("d.WorkerInsurance")} AS FLOAT) AS NumD17,
                        TRY_CAST({CleanNum("d.DSW_INC")} AS FLOAT) AS NumDInc,
                        TRY_CAST({CleanNum("d.DSW_SPOUSE")} AS FLOAT) AS NumD25
                    FROM {DetailsTableName} d
                ),

                ContractsCalc AS (
                    SELECT 
                        c.Id, c.ContractNumber,
                        TRY_CAST({CleanNum("c.ContractRow")} AS INT) AS NumContractRow
                    FROM {ContractTableName} c
                )
            ");

        // ─── کوئری نهایی: از ستون‌های CTE استفاده می‌کند (بدون تکرار CleanNum) ───
        sb.AppendLine(@"
                SELECT 
                    h.WorkshopCode AS WorkshopCode, h.DSK_LISTNO AS ListNumber,
                    CASE WHEN LTRIM(RTRIM(COALESCE(h.MON_PYM, ''))) = '' THEN '' 
                         ELSE CAST(h.CleanMonPym AS NVARCHAR(50)) END AS ContractRow,
                    COALESCE(c.ContractNumber, '') AS ClientContractNumber, 
                    h.DSK_YY AS Year, h.DSK_MM AS Month,

                    COALESCE(MAX(h.NumH10), 0) AS H10, 
                    COUNT(d.Id) AS D10, 
                    (COALESCE(MAX(h.NumH10), 0) - COUNT(d.Id)) AS Diff10,

                    COALESCE(MAX(h.NumH11), 0) AS H11, 
                    COALESCE(SUM(d.NumD11), 0) AS D11, 
                    (COALESCE(MAX(h.NumH11), 0) - COALESCE(SUM(d.NumD11), 0)) AS Diff11,

                    COALESCE(MAX(h.NumH12), 0) AS H12, 
                    COALESCE(SUM(d.NumD12), 0) AS D12, 
                    (COALESCE(MAX(h.NumH12), 0) - COALESCE(SUM(d.NumD12), 0)) AS Diff12,

                    COALESCE(MAX(h.NumH13), 0) AS H13, 
                    COALESCE(SUM(d.NumD13), 0) AS D13, 
                    (COALESCE(MAX(h.NumH13), 0) - COALESCE(SUM(d.NumD13), 0)) AS Diff13,

                    COALESCE(MAX(h.NumH15), 0) AS H15, 
                    COALESCE(SUM(d.NumD15), 0) AS D15, 
                    (COALESCE(MAX(h.NumH15), 0) - COALESCE(SUM(d.NumD15), 0)) AS Diff15,

                    COALESCE(MAX(h.NumH16), 0) AS H16, 
                    COALESCE(SUM(d.NumD16), 0) AS D16, 
                    (COALESCE(MAX(h.NumH16), 0) - COALESCE(SUM(d.NumD16), 0)) AS Diff16,

                    COALESCE(MAX(h.NumH17), 0) AS H17, 
                    COALESCE(SUM(d.NumD17), 0) AS D17, 
                    (COALESCE(MAX(h.NumH17), 0) - COALESCE(SUM(d.NumD17), 0)) AS Diff17,

                    COALESCE(MAX(h.NumH18), 0) AS H18, 
                    COALESCE(SUM(d.NumD15 * 0.20), 0) AS D18, 
                    (COALESCE(MAX(h.NumH18), 0) - COALESCE(SUM(d.NumD15 * 0.20), 0)) AS Diff18,

                    COALESCE(MAX(h.NumH19), 0) AS H19, 
                    COALESCE(SUM(d.NumD15 * 0.03), 0) AS D19, 
                    (COALESCE(MAX(h.NumH19), 0) - COALESCE(SUM(d.NumD15 * 0.03), 0)) AS Diff19,

                    (COALESCE(MAX(h.NumH17), 0) + COALESCE(MAX(h.NumH18), 0) + COALESCE(MAX(h.NumH19), 0)) AS H30, 
                    (COALESCE(SUM(d.NumD17), 0) + COALESCE(SUM(d.NumD15 * 0.20), 0) + COALESCE(SUM(d.NumD15 * 0.03), 0)) AS D30, 
                    ((COALESCE(MAX(h.NumH17), 0) + COALESCE(MAX(h.NumH18), 0) + COALESCE(MAX(h.NumH19), 0)) 
                     - (COALESCE(SUM(d.NumD17), 0) + COALESCE(SUM(d.NumD15 * 0.20), 0) + COALESCE(SUM(d.NumD15 * 0.03), 0))) AS Diff30,

                    COALESCE(MAX(h.NumH24), 0) AS H24, 
                    COALESCE(SUM(d.NumDInc * d.NumD11), 0) AS D24, 
                    (COALESCE(MAX(h.NumH24), 0) - COALESCE(SUM(d.NumDInc * d.NumD11), 0)) AS Diff24,

                    COALESCE(MAX(h.NumH25), 0) AS H25, 
                    COALESCE(SUM(d.NumD25), 0) AS D25, 
                    (COALESCE(MAX(h.NumH25), 0) - COALESCE(SUM(d.NumD25), 0)) AS Diff25

                FROM HeaderCalc h
                LEFT JOIN DetailsCalc d 
                    ON LTRIM(RTRIM(h.WorkshopCode)) = LTRIM(RTRIM(d.WorkshopCode)) 
                   AND LTRIM(RTRIM(h.DSK_YY)) = LTRIM(RTRIM(d.Year)) 
                   AND LTRIM(RTRIM(h.DSK_MM)) = LTRIM(RTRIM(d.Month)) 
                   AND LTRIM(RTRIM(h.DSK_LISTNO)) = LTRIM(RTRIM(d.ListNumber)) 
                   AND h.CleanMonPym = d.CleanContractRow
                LEFT JOIN ContractsCalc c 
                    ON h.CleanMonPym <> '' 
                   AND c.NumContractRow = TRY_CAST(h.CleanMonPym AS INT)
                WHERE 1 = 1");

        if (contractId > 0) sb.AppendLine("  AND c.Id = @contractId");
        if (year.HasValue) sb.AppendLine("  AND h.DSK_YY = @year");

        sb.AppendLine(@"
                GROUP BY h.WorkshopCode, h.DSK_LISTNO, h.MON_PYM, h.CleanMonPym, h.DSK_YY, h.DSK_MM, c.ContractNumber
                ORDER BY h.DSK_YY DESC, h.DSK_MM DESC, h.DSK_LISTNO DESC");

        return sb.ToString();
    }

    private sealed class ComboItem
    {
        public int Id { get; }
        public string Display { get; }
        public ComboItem(int id, string display) { Id = id; Display = display; }
        public override string ToString() => Display;
    }
}
