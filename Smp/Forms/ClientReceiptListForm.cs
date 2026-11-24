using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using ClosedXML.Excel;
using Smp.Helpers;
using Smp.Data;
using Smp.Models;

namespace Smp.Forms;

public class ClientReceiptListForm : Form
{
    private readonly string _connectionString;
    private readonly CashTransactionRepository _cashRepo;
    private readonly CashCheckRepository _checkRepo;

    private DataGridView dgvReceipts = null!;
    private ComboBox cmbContract = null!;
    private TextBox txtSearch = null!, txtFromDate = null!, txtToDate = null!;

    private Button btnFilter = null!, btnDelete = null!, btnRefresh = null!, btnEdit = null!, btnPrint = null!;
    private Button btnImportExcel = null!, btnDownloadTemplate = null!;

    private ModernStatCard cardCount = null!, cardTotal = null!, cardContracts = null!;
    private static readonly Font _boldFont = ModernTheme.GetFont(11f, FontStyle.Bold);

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    private Image? _companyLogo = null;
    private PrintDocument _printDoc = null!;
    private List<PrintGroup> _printGroups = new();
    private int _currentGroupIndex = 0;
    private int _currentRowIndex = 0;

    private class PrintGroup
    {
        public string ContractDisplayName { get; set; } = "";
        public List<DataGridViewRow> Rows { get; set; } = new();
        public decimal TotalAmount { get; set; }
    }

    public ClientReceiptListForm(string connectionString)
    {
        _connectionString = connectionString;
        _cashRepo = new CashTransactionRepository(connectionString);
        _checkRepo = new CashCheckRepository(connectionString);

        BuildUI();
        LoadCompanyLogo();
        SetupPrintDocument();

        this.ApplyStandardHeader("لیست دریافتی‌های کارفرمایان", new Size(1200, 750));
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);

        Shown += async (s, e) =>
        {
            await LoadContractsAsync();
            await LoadDataSafeAsync();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientReceiptListForm"); }
            try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientReceiptListForm"); }
            try { _printDoc?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientReceiptListForm"); }
            try { _companyLogo?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientReceiptListForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── نوار فیلتر (هم‌سبک گزارش‌های مدرن) ──
        var filter = ModernReport.CreateFilterBar();

        cmbContract = ModernUI.Combo();
        cmbContract.SelectedIndexChanged += async (s, e) => await LoadDataSafeAsync();
        ModernReport.AddFilter(filter, "قرارداد", cmbContract, 340);

        txtFromDate = new TextBox { TextAlign = HorizontalAlignment.Center, PlaceholderText = "۱۴۰۵/۰۱/۰۱" };
        DateHelper.ApplyDateMask(txtFromDate);
        ModernReport.AddFilter(filter, "از تاریخ", txtFromDate, 130);

        txtToDate = new TextBox { TextAlign = HorizontalAlignment.Center, PlaceholderText = "۱۴۰۵/۱۲/۲۹" };
        DateHelper.ApplyDateMask(txtToDate);
        ModernReport.AddFilter(filter, "تا تاریخ", txtToDate, 130);

        txtSearch = new TextBox { PlaceholderText = "توضیحات، شماره قرارداد..." };
        txtSearch.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await LoadDataSafeAsync(); }
        };
        ModernReport.AddFilter(filter, "جستجو", txtSearch, 230);

        // در چیدمان راست‌به‌چپ فاصله‌ی دکمه از کنترل قبلی را Margin «راست» دکمه می‌دهد
        btnFilter = ModernButton.Primary("🔍 اعمال فیلتر", 130, 36);
        btnFilter.Margin = new Padding(0, 4, 18, 0);
        btnFilter.Click += async (s, e) => await LoadDataSafeAsync();
        filter.Controls.Add(btnFilter);

        btnRefresh = ModernButton.Outline("🔄 بروزرسانی", ModernTheme.Neutral, 120, 36);
        btnRefresh.Margin = new Padding(0, 4, 8, 0);
        btnRefresh.Click += async (s, e) => await LoadDataSafeAsync();
        filter.Controls.Add(btnRefresh);

        btnDownloadTemplate = ModernButton.Soft("📥 دانلود قالب اکسل", ModernTheme.Teal, 170, 36);
        btnDownloadTemplate.Margin = new Padding(0, 4, 24, 0);
        btnDownloadTemplate.Click += BtnDownloadTemplate_Click;
        filter.Controls.Add(btnDownloadTemplate);

        btnImportExcel = ModernButton.Success("📤 ایمپورت از اکسل", 170, 36);
        btnImportExcel.Margin = new Padding(0, 4, 8, 0);
        btnImportExcel.Click += async (s, e) => await BtnImportExcel_Click(s, e);
        filter.Controls.Add(btnImportExcel);

        // ── کارت‌های خلاصه ──
        var strip = new StatStrip(3, 76);
        cardCount = strip.Add("تعداد دریافتی‌ها", ModernTheme.Dark);
        cardTotal = strip.Add("جمع دریافتی‌ها (ریال)", ModernTheme.Success);
        cardContracts = strip.Add("تعداد قرارداد / پروژه", ModernTheme.Primary);
        cardCount.SetCount(0);
        cardTotal.SetAmount(0m);
        cardContracts.SetCount(0);

        // ── جدول ──
        dgvReceipts = new DataGridView { AutoGenerateColumns = false, AllowUserToAddRows = false, ReadOnly = true };
        dgvReceipts.ApplyModernStyle();
        dgvReceipts.FillColumns();
        dgvReceipts.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvReceipts.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvReceipts.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnEdit_Click(null, EventArgs.Empty); };

        dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "TableName", DataPropertyName = "TableName", Visible = false });
        dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "AccountId", DataPropertyName = "AccountId", Visible = false });
        dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", DataPropertyName = "Category", Visible = false });
        dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsSummary", DataPropertyName = "IsSummary", Visible = false });

        dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractDisplayName", DataPropertyName = "ContractDisplayName", HeaderText = "قرارداد / پروژه", FillWeight = 24,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
        dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "AccountTitle", DataPropertyName = "AccountTitle", HeaderText = "حساب / بانک واریز", FillWeight = 17 });
        dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReceiptDate", DataPropertyName = "ReceiptDate", HeaderText = "تاریخ دریافت", FillWeight = 12 });
        dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReceiptType", DataPropertyName = "ReceiptType", HeaderText = "روش دریافت", FillWeight = 13 });
        dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 15 });
        dgvReceipts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 22,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });

        // روش دریافت به‌صورت قرص رنگی (مقدارهای دیگر خاکستری)
        dgvReceipts.UseBadges("ReceiptType", new Dictionary<string, Color>
        {
            ["چک دریافتی"] = ModernTheme.Orange,
            ["جمع کل"] = ModernTheme.Primary
        });

        // ردیف «جمع کل» هر قرارداد: پس‌زمینه‌ی ملایم آبی و متن پررنگ
        dgvReceipts.RowPrePaint += (s, e) =>
        {
            if (e.RowIndex >= 0 && dgvReceipts.Rows[e.RowIndex].Cells["IsSummary"].Value is bool isSum && isSum)
            {
                var st = dgvReceipts.Rows[e.RowIndex].DefaultCellStyle;
                st.BackColor = ModernPaint.Tint(ModernTheme.Primary, 8);
                st.Font = _boldFont;
                st.ForeColor = ModernTheme.TextStrong;
            }
        };

        dgvReceipts.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvReceipts.Columns[e.ColumnIndex].DataPropertyName;

            bool isSum = false;
            if (dgvReceipts.Columns.Contains("IsSummary") && dgvReceipts.Rows[e.RowIndex].Cells["IsSummary"].Value != DBNull.Value && dgvReceipts.Rows[e.RowIndex].Cells["IsSummary"].Value is not null)
                isSum = Convert.ToBoolean(dgvReceipts.Rows[e.RowIndex].Cells["IsSummary"].Value);

            if (colName == "ReceiptDate")
            {
                if (isSum) { e.Value = "—"; e.FormattingApplied = true; }
                else if (e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
            }
            else if (colName == "Amount" && e.Value is decimal dec)
            {
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.FormattingApplied = true;
                // مبلغ دریافتی سبز و پررنگ (ردیف جمع کل، رنگ خودش را دارد)
                if (!isSum && e.CellStyle is not null)
                {
                    e.CellStyle.ForeColor = ModernPaint.Darken(ModernTheme.Success, 35);
                    e.CellStyle.Font = _boldFont;
                }
            }
            else if (colName == "AccountTitle" && isSum)
            {
                e.Value = "—";
                e.FormattingApplied = true;
            }
        };

        var gridHost = ModernReport.WrapGrid(dgvReceipts);
        gridHost.Margin = new Padding(16, 8, 16, 8);

        // ── دکمه‌های پایین ──
        btnEdit = ModernButton.Soft("✏️ ویرایش دریافتی", ModernTheme.Primary, 150, 38);
        btnEdit.Click += BtnEdit_Click;

        btnPrint = ModernButton.Outline("🖨️ چاپ لیست", ModernTheme.Orange, 130, 38);
        btnPrint.Click += BtnPrint_Click;

        btnDelete = ModernButton.Soft("🗑️ حذف دریافتی", ModernTheme.Danger, 150, 38);
        btnDelete.Click += BtnDelete_Click;

        var bottomBar = ModernForm.CreateButtonBar(btnEdit, btnPrint, btnDelete);

        // ── چیدمان کلی ──
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        strip.Margin = new Padding(10, 4, 10, 4);
        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(strip, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        root.Controls.Add(bottomBar, 0, 3);
        Controls.Add(root);
    }

    private class ContractComboItem
    {
        public int Id { get; set; }
        public string Display { get; set; } = "";
        public override string ToString() => Display;
    }

    private void LoadCompanyLogo()
    {
        try
        {
            var companyRepo = new CompanyRepository(_connectionString);
            var company = companyRepo.GetAll().FirstOrDefault();
            if (company?.Logo is not null && company.Logo.Length > 0)
            {
                _companyLogo = ImageHelper.ByteArrayToImage(company.Logo);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogWarning("خطا در بارگذاری لوگو: " + ex.Message, "ClientReceiptListForm");
        }
    }

    private async Task LoadContractsAsync()
    {
        try
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT Id, ContractNumber, Subject FROM ClientContracts ORDER BY Id DESC", conn);
            using var adapter = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            adapter.Fill(dt);

            cmbContract.Items.Clear();
            cmbContract.Items.Add(new ContractComboItem { Id = 0, Display = "--- همه قراردادها ---" });

            foreach (DataRow row in dt.Rows)
            {
                cmbContract.Items.Add(new ContractComboItem
                {
                    Id = Convert.ToInt32(row["Id"]),
                    Display = $"{row["ContractNumber"]} - {row["Subject"]}"
                });
            }

            if (cmbContract.Items.Count > 0)
                cmbContract.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientReceiptListForm.LoadContracts");
            ModernMessageBox.ShowError("خطا در لود قراردادها: " + ex.Message);
        }
    }

    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            int? contractId = null;
            if (cmbContract.SelectedItem is ContractComboItem selectedContract && selectedContract.Id > 0)
            {
                contractId = selectedContract.Id;
            }

            DateTime? fromDate = DateHelper.ParsePersianDate(txtFromDate.Text);
            DateTime? toDate = DateHelper.ParsePersianDate(txtToDate.Text);
            string search = txtSearch.Text.Trim();

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                        SELECT 
                            r.Id, 
                            r.TableName, 
                            r.ContractId,
                            r.AccountId,
                            CASE 
                                WHEN c.Id IS NOT NULL THEN ISNULL(c.ContractNumber, '') + CASE WHEN ISNULL(c.Subject, '') <> '' THEN ' - ' + c.Subject ELSE '' END
                                ELSE ISNULL(NULLIF(r.ContractTitle, ''), '---')
                            END AS ContractDisplayName,
                            r.ReceiptDate, 
                            r.Amount, 
                            r.ReceiptType, 
                            r.Description,
                            r.Category,
                            ISNULL(r.AccountTitle, '---') AS AccountTitle
                        FROM (
                            SELECT t.Id, 'CashTransactions' AS TableName, t.ContractId, t.AccountId, 
                                   t.TransactionDate AS ReceiptDate, t.Amount, t.TransactionType AS ReceiptType, 
                                   t.Description, ISNULL(t.ContractTitle, '') AS ContractTitle, ISNULL(t.Category, '') AS Category,
                                   ISNULL(a.Title, '---') AS AccountTitle
                            FROM CashTransactions t 
                            LEFT JOIN CompanyBankAccounts a ON t.AccountId = a.Id
                            WHERE (LTRIM(RTRIM(t.Direction)) = N'دریافتی' OR LTRIM(RTRIM(t.Direction)) = N'دریافت')
                              AND LTRIM(RTRIM(ISNULL(t.Category, ''))) NOT IN (N'انتقال بین حساب‌ها', N'سود بانکی', N'سود', N'شارژ تنخواه')
                              
                            UNION ALL
                            
                            SELECT k.Id, 'CashChecks' AS TableName, k.ContractId, k.AccountId, 
                                   k.DueDate AS ReceiptDate, k.Amount, N'چک دریافتی' AS ReceiptType, 
                                   k.Description, ISNULL(k.ContractTitle, '') AS ContractTitle, N'چک' AS Category,
                                   ISNULL(a.Title, '---') AS AccountTitle
                            FROM CashChecks k 
                            LEFT JOIN CompanyBankAccounts a ON k.AccountId = a.Id
                            WHERE LTRIM(RTRIM(k.CheckType)) = N'دریافتی'
                        ) r
                        LEFT JOIN ClientContracts c ON r.ContractId = c.Id
                        WHERE 1=1";

                if (contractId.HasValue) query += " AND r.ContractId = @cid";
                if (fromDate.HasValue) query += " AND CAST(r.ReceiptDate AS DATE) >= CAST(@from AS DATE)";
                if (toDate.HasValue) query += " AND CAST(r.ReceiptDate AS DATE) <= CAST(@to AS DATE)";
                if (!string.IsNullOrWhiteSpace(search)) query += " AND (r.Description LIKE @search OR c.ContractNumber LIKE @search OR r.ContractTitle LIKE @search)";

                query += " ORDER BY r.ReceiptDate DESC, r.Id DESC";

                using var cmd = new SqlCommand(query, conn);
                if (contractId.HasValue) cmd.Parameters.AddWithValue("@cid", contractId.Value);
                if (fromDate.HasValue) cmd.Parameters.AddWithValue("@from", fromDate.Value);
                if (toDate.HasValue) cmd.Parameters.AddWithValue("@to", toDate.Value);
                if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", $"%{search}%");

                using var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                await Task.Run(() => adapter.Fill(dt));

                if (IsDisposed) return;

                var groupedDt = dt.Clone();
                groupedDt.Columns.Add("IsSummary", typeof(bool));

                var sortedRows = dt.AsEnumerable()
                    .OrderBy(r => r.Field<string>("ContractDisplayName") ?? "---")
                    .ThenByDescending(r => r.Field<DateTime>("ReceiptDate"));

                foreach (var group in sortedRows.GroupBy(r => r.Field<string>("ContractDisplayName") ?? "---"))
                {
                    decimal total = 0;
                    foreach (var row in group)
                    {
                        var newRow = groupedDt.NewRow();
                        newRow.ItemArray = row.ItemArray;
                        newRow["IsSummary"] = false;
                        groupedDt.Rows.Add(newRow);
                        total += row.Field<decimal>("Amount");
                    }

                    var sumRow = groupedDt.NewRow();
                    sumRow["ContractDisplayName"] = group.Key;
                    sumRow["ReceiptType"] = "جمع کل";
                    sumRow["Amount"] = total;
                    sumRow["Description"] = $"جمع کل واریزی بابت {group.Key}";
                    sumRow["IsSummary"] = true;
                    if (groupedDt.Columns.Contains("Id")) sumRow["Id"] = 0;
                    if (groupedDt.Columns.Contains("TableName")) sumRow["TableName"] = "Summary";
                    if (groupedDt.Columns.Contains("AccountTitle")) sumRow["AccountTitle"] = "---";
                    if (groupedDt.Columns.Contains("Category")) sumRow["Category"] = "";
                    if (groupedDt.Columns.Contains("AccountId")) sumRow["AccountId"] = DBNull.Value;
                    if (groupedDt.Columns.Contains("ContractId")) sumRow["ContractId"] = DBNull.Value;
                    if (groupedDt.Columns.Contains("ReceiptDate")) sumRow["ReceiptDate"] = DBNull.Value;

                    groupedDt.Rows.Add(sumRow);
                }

                // کارت‌های خلاصه از ردیف‌های واقعی (بدون ردیف‌های «جمع کل»)
                int realCount = 0; decimal realTotal = 0m;
                foreach (DataRow gr in groupedDt.Rows)
                {
                    if (gr["IsSummary"] is bool sm && sm) continue;
                    realCount++;
                    if (gr["Amount"] != DBNull.Value) realTotal += Convert.ToDecimal(gr["Amount"]);
                }
                cardCount.SetCount(realCount);
                cardTotal.SetAmount(realTotal);
                cardContracts.SetCount(groupedDt.AsEnumerable()
                    .Where(r => !(r["IsSummary"] is bool sm2 && sm2))
                    .Select(r => r["ContractDisplayName"]?.ToString() ?? "---").Distinct().Count());

                dgvReceipts.DataSource = groupedDt;
            }, "در حال بارگذاری لیست دریافتی‌ها...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientReceiptListForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری لیست:\n" + ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientReceiptListForm"); }
        }
    }

    private void SetupPrintDocument()
    {
        _printDoc = new PrintDocument();
        _printDoc.DefaultPageSettings.Landscape = true;
        _printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
        _printDoc.BeginPrint += (s, e) => { _currentGroupIndex = 0; _currentRowIndex = 0; };
        _printDoc.PrintPage += PrintDoc_PrintPage;
    }

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        if (IsDisposed || dgvReceipts.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("داده‌ای برای چاپ وجود ندارد.");
            return;
        }

        _printGroups.Clear();
        string currentContract = "";
        PrintGroup? currentGroup = null;

        foreach (DataGridViewRow row in dgvReceipts.Rows)
        {
            if (row.IsNewRow) continue;

            string contract = row.Cells["ContractDisplayName"].Value?.ToString() ?? "---";
            bool isSummary = row.Cells["IsSummary"].Value is not null && Convert.ToBoolean(row.Cells["IsSummary"].Value);

            if (currentGroup is null || currentContract != contract)
            {
                currentContract = contract;
                currentGroup = new PrintGroup { ContractDisplayName = contract };
                _printGroups.Add(currentGroup);
            }

            if (!isSummary)
            {
                currentGroup.Rows.Add(row);
            }
            else
            {
                currentGroup.TotalAmount = NumberHelper.ParseFormatted(row.Cells["Amount"].Value?.ToString());
            }
        }

        using var preview = new PrintPreviewDialog
        {
            Document = _printDoc,
            Width = 1100,
            Height = 800,
            WindowState = FormWindowState.Maximized,
            UseAntiAlias = true,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true,
            Text = "پیش‌نمایش چاپ گزارش دریافتی‌ها"
        };
        preview.ShowDialog(this);
    }

    private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        Graphics g = e.Graphics!;
        // ✅ جبران حاشیه‌ی سخت‌افزاری چاپگر (فقط در چاپ واقعی؛ در پیش‌نمایش لازم نیست)
        if (!((sender as PrintDocument)?.PrintController?.IsPreview ?? false))
            g.TranslateTransform(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);
        float pageWidth = e.PageBounds.Width;
        float pageHeight = e.PageBounds.Height;
        float margin = 40f;

        var fontMainTitle = ModernTheme.GetFont(16f, FontStyle.Bold);
        var fontGroupTitle = ModernTheme.GetFont(12f, FontStyle.Bold);
        var fontHeader = ModernTheme.GetFont(10f, FontStyle.Bold);
        var fontBody = ModernTheme.GetFont(10f);
        var fontTotal = ModernTheme.GetFont(10f, FontStyle.Bold);

        using StringFormat rtlCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
        using StringFormat rtlRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
        using StringFormat titleFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

        float yPos = margin;
        float topMarginOffset = 0;

        if (_companyLogo is not null)
        {
            try
            {
                float logoWidth = 90f;
                float logoHeight = 80f;
                float logoX = pageWidth - margin - logoWidth;
                g.DrawImage(_companyLogo, logoX, margin - 10, logoWidth, logoHeight);
                topMarginOffset = 85f;
            }
            catch (ArgumentException) { }
        }

        yPos = margin + topMarginOffset;

        if (_currentGroupIndex == 0)
        {
            g.DrawString("گزارش لیست دریافتی‌های کارفرمایان", fontMainTitle, Brushes.Black, new RectangleF(0, yPos, pageWidth, 30), titleFormat);
            g.DrawString($"تاریخ چاپ: {DateHelper.ToPersianDate(DateTime.Now)}", fontBody, Brushes.Gray, new RectangleF(margin, yPos + 30, 200, 20), rtlRight);
            yPos += 60;
        }

        float tableWidth = pageWidth - (margin * 2);
        float[] proportions = { 15f, 20f, 15f, 15f, 35f };
        float[] colWidths = proportions.Select(p => (p / 100f) * tableWidth).ToArray();
        float rowHeight = 25f;
        float headerRowHeight = 30f;

        while (_currentGroupIndex < _printGroups.Count)
        {
            var group = _printGroups[_currentGroupIndex];

            if (yPos > margin + 60) yPos += 20;

            string headerText = $"دریافتی‌های بابت {group.ContractDisplayName}";
            g.DrawString(headerText, fontGroupTitle, Brushes.DarkBlue, new RectangleF(margin, yPos, tableWidth, 25), rtlRight);
            yPos += 30;

            string[] headers = { "تاریخ دریافت", "حساب / بانک واریز", "روش دریافت", "مبلغ (ریال)", "توضیحات" };
            g.FillRectangle(Brushes.LightGray, margin, yPos, tableWidth, headerRowHeight);
            g.DrawRectangle(Pens.Black, margin, yPos, tableWidth, headerRowHeight);

            float currentX = pageWidth - margin;
            for (int i = 0; i < headers.Length; i++)
            {
                currentX -= colWidths[i];
                // ✅ اصلاح شد: رسم خطوط عمودی برای تمام ستون‌ها به جز آخرین ستون (لبه سمت چپ)
                if (i < headers.Length - 1) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + headerRowHeight);
                g.DrawString(headers[i], fontHeader, Brushes.Black, new RectangleF(currentX, yPos, colWidths[i], headerRowHeight), rtlCenter);
            }
            yPos += headerRowHeight;

            while (_currentRowIndex < group.Rows.Count)
            {
                if (yPos + rowHeight > pageHeight - margin)
                {
                    e.HasMorePages = true;
                    return;
                }

                var row = group.Rows[_currentRowIndex];
                string dateVal = row.Cells["ReceiptDate"].FormattedValue?.ToString() ?? "";
                string accVal = row.Cells["AccountTitle"].FormattedValue?.ToString() ?? "";
                string methodVal = row.Cells["ReceiptType"].FormattedValue?.ToString() ?? "";
                string amountVal = row.Cells["Amount"].FormattedValue?.ToString() ?? "";
                string descVal = row.Cells["Description"].FormattedValue?.ToString() ?? "";

                string[] rowData = { dateVal, accVal, methodVal, amountVal, descVal };

                g.DrawRectangle(Pens.Black, margin, yPos, tableWidth, rowHeight);
                currentX = pageWidth - margin;
                for (int i = 0; i < rowData.Length; i++)
                {
                    currentX -= colWidths[i];
                    // ✅ اصلاح شد
                    if (i < rowData.Length - 1) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + rowHeight);
                    g.DrawString(rowData[i], fontBody, Brushes.Black, new RectangleF(currentX, yPos, colWidths[i], rowHeight), rtlCenter);
                }

                yPos += rowHeight;
                _currentRowIndex++;
            }

            if (yPos + rowHeight > pageHeight - margin)
            {
                e.HasMorePages = true;
                return;
            }

            g.FillRectangle(Brushes.Beige, margin, yPos, tableWidth, rowHeight);
            g.DrawRectangle(Pens.Black, margin, yPos, tableWidth, rowHeight);

            currentX = pageWidth - margin;
            string[] totalRowData = { "جمع کل", "---", "---", NumberHelper.FormatThousands(group.TotalAmount, usePersianDigits: true), $"جمع کل دریافتی‌های این قرارداد" };

            for (int i = 0; i < totalRowData.Length; i++)
            {
                currentX -= colWidths[i];
                // ✅ اصلاح شد
                if (i < totalRowData.Length - 1) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + rowHeight);
                g.DrawString(totalRowData[i], fontTotal, Brushes.Black, new RectangleF(currentX, yPos, colWidths[i], rowHeight), rtlCenter);
            }

            yPos += rowHeight + 10;
            _currentGroupIndex++;
            _currentRowIndex = 0;
        }

        e.HasMorePages = false;
    }

    private async void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (dgvReceipts.CurrentRow is null || IsDisposed) return;

        if (dgvReceipts.CurrentRow.Cells["IsSummary"].Value is bool isSum && isSum)
        {
            ModernMessageBox.ShowWarning("این ردیف مربوط به جمع کل است و قابل ویرایش نیست.");
            return;
        }

        try
        {
            string category = dgvReceipts.CurrentRow.Cells["Category"].Value?.ToString() ?? "";
            if (category == "انتقال بین حساب‌ها")
            {
                ModernMessageBox.ShowWarning("این رکورد مربوط به انتقال بین حساب‌هاست. برای ویرایش، از منوی «انتقال بین حساب‌ها» استفاده کنید.");
                return;
            }

            int id = Convert.ToInt32(dgvReceipts.CurrentRow.Cells["Id"].Value);
            string tableName = dgvReceipts.CurrentRow.Cells["TableName"].Value?.ToString() ?? "";
            bool isUpdated = false;

            if (tableName == "CashTransactions")
            {
                // این لیست فقط «دریافتی»ها را نشان می‌دهد؛ فرم «ویرایش حواله» مخصوص پرداخت است و
                // ذخیره با آن، دریافتی را به «پرداختی از نوع حواله» تبدیل می‌کرد. پس فرم دریافت باز می‌شود.
                using var form = new ClientPaymentForm(_connectionString, id);
                if (form.ShowDialog(this) == DialogResult.OK) isUpdated = true;
            }
            else if (tableName == "CashChecks")
            {
                using var form = new CashCheckEditForm(_connectionString, id);
                if (form.ShowDialog(this) == DialogResult.OK) isUpdated = true;
            }

            if (isUpdated && !IsDisposed)
            {
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientReceiptListForm.BtnEdit_Click");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ویرایش:\n" + ex.Message);
        }
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (dgvReceipts.CurrentRow is null || IsDisposed) return;

        if (dgvReceipts.CurrentRow.Cells["IsSummary"].Value is bool isSum && isSum)
        {
            ModernMessageBox.ShowWarning("این ردیف مربوط به جمع کل است و قابل حذف نیست.");
            return;
        }

        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            string category = dgvReceipts.CurrentRow.Cells["Category"].Value?.ToString() ?? "";
            if (category == "انتقال بین حساب‌ها")
            {
                ModernMessageBox.ShowWarning("این رکورد مربوط به انتقال بین حساب‌هاست. برای حذف، از منوی «انتقال بین حساب‌ها» استفاده کنید.");
                return;
            }

            int id = Convert.ToInt32(dgvReceipts.CurrentRow.Cells["Id"].Value);
            string tableName = dgvReceipts.CurrentRow.Cells["TableName"].Value?.ToString() ?? "";
            decimal amount = NumberHelper.ParseFormatted(dgvReceipts.CurrentRow.Cells["Amount"].Value?.ToString());

            if (!ModernMessageBox.AskConfirm($"آیا از حذف این دریافتی به مبلغ {NumberHelper.FormatThousands(amount, usePersianDigits: true)} ریال مطمئن هستید؟\n\nتوجه: اگر حواله یا نقدی باشد، مبلغ از موجودی حساب بانکی کسر می‌شود.", "حذف دریافتی"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (tableName == "CashChecks")
                {
                    await _checkRepo.DeleteBatchAsync(new List<int> { id });
                }
                else if (tableName == "CashTransactions")
                {
                    await _cashRepo.DeleteBatchAsync(new List<int> { id });
                }
            }, "در حال حذف دریافتی...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("دریافتی با موفقیت حذف شد.", isSuccess: true);
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientReceiptListForm.BtnDelete_Click");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در حذف:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientReceiptListForm"); }
        }
    }

    private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = "ClientReceipts_Import_Template.xlsx",
            Title = "ذخیره قالب اکسل دریافتی‌ها"
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("دریافتی‌ها");
            ws.RightToLeft = true;

            string[] headers = {
                "تاریخ دریافت (1403/01/01)",
                "مبلغ (ریال)",
                "روش دریافت (نقدی/حواله بانکی/چک)",
                "توضیحات",
                "شماره قرارداد (اختیاری - اگر خالی باشد از فیلتر بالای صفحه استفاده می‌شود)"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.WrapText = true;
            }

            ws.Cell(2, 1).Value = DateHelper.ToPersianDate(DateTime.Now);
            ws.Cell(2, 2).Value = 10000000;
            ws.Cell(2, 3).Value = "نقدی";
            ws.Cell(2, 4).Value = "بابت دریافت وجه";
            ws.Cell(2, 5).Value = "";

            ws.Range("C2:C1000").CreateDataValidation().List("\"نقدی,حواله بانکی,چک,سایر\"");

            ws.Columns().AdjustToContents();
            ws.Row(1).Height = 40;
            workbook.SaveAs(sfd.FileName);

            ToastHelper.Show("قالب اکسل با موفقیت ساخته شد.", true);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientReceiptListForm.DownloadTemplate");
            ModernMessageBox.ShowError("خطا در ساخت قالب:\n" + ex.Message);
        }
    }

    private async Task BtnImportExcel_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل دریافتی‌ها" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        var accounts = await new BankAccountRepository(_connectionString).GetActiveAsync();
        if (accounts is null || accounts.Count == 0)
        {
            ModernMessageBox.ShowWarning("هیچ حساب بانکی فعالی یافت نشد. ابتدا یک حساب تعریف کنید.");
            return;
        }

        using var selectForm = new Form();
        selectForm.Text = "انتخاب حساب بانکی";
        selectForm.Size = new Size(400, 200);
        selectForm.StartPosition = FormStartPosition.CenterParent;
        selectForm.FormBorderStyle = FormBorderStyle.FixedDialog;
        selectForm.MaximizeBox = false;
        selectForm.MinimizeBox = false;
        selectForm.RightToLeft = RightToLeft.Yes;
        selectForm.RightToLeftLayout = true;
        selectForm.Font = ModernTheme.GetFont(11f);

        var lbl = new Label { Text = "این دریافتی‌ها به کدام حساب بانکی واریز شده‌اند؟", Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter, Font = ModernTheme.GetFont(11f, FontStyle.Bold) };
        var cmb = new ModernComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id", DataSource = new List<CompanyBankAccount>(accounts) };
        var btnOk = ModernButton.Create("تایید و ادامه", ModernTheme.Success, 120, 35);
        btnOk.Dock = DockStyle.Bottom;
        btnOk.Click += (s, ev) => { selectForm.DialogResult = DialogResult.OK; selectForm.Close(); };

        selectForm.Controls.Add(btnOk); selectForm.Controls.Add(cmb); selectForm.Controls.Add(lbl);

        if (selectForm.ShowDialog(this) != DialogResult.OK || cmb.SelectedItem is not CompanyBankAccount targetAcc) return;

        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            int defaultContractId = 0;
            if (cmbContract.SelectedItem is ContractComboItem selectedContract)
            {
                defaultContractId = selectedContract.Id;
            }

            int successCount = 0;
            int errorCount = 0;
            List<string> errors = new();

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                using var workbook = new XLWorkbook(ofd.FileName);
                var ws = workbook.Worksheet(1);
                var rows = ws.RowsUsed().Skip(1);

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                int rowNum = 1;
                foreach (var row in rows)
                {
                    rowNum++;
                    try
                    {
                        string dateStr = row.Cell(1).GetString().Trim();
                        string amountStr = row.Cell(2).GetString().Trim();
                        string method = row.Cell(3).GetString().Trim();
                        string desc = row.Cell(4).GetString().Trim();
                        string contractNum = row.Cell(5).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(amountStr)) continue;

                        DateTime? date = DateHelper.ParsePersianDate(dateStr);
                        if (date is null) throw new Exception("تاریخ دریافت نامعتبر است.");

                        decimal amount = NumberHelper.ParseFormatted(amountStr);
                        if (amount <= 0) throw new Exception("مبلغ نامعتبر است.");

                        if (string.IsNullOrWhiteSpace(method)) method = "نقدی";

                        int contractId = defaultContractId;
                        if (!string.IsNullOrWhiteSpace(contractNum))
                        {
                            using var cmdC = new SqlCommand("SELECT TOP 1 Id FROM ClientContracts WHERE ContractNumber = @num", conn);
                            cmdC.Parameters.AddWithValue("@num", contractNum);
                            var res = await cmdC.ExecuteScalarAsync();
                            if (res is not null && res != DBNull.Value) contractId = Convert.ToInt32(res);
                            else throw new Exception($"قرارداد با شماره «{contractNum}» یافت نشد.");
                        }

                        if (contractId == 0) throw new Exception("شماره قرارداد مشخص نیست (نه در اکسل و نه در فیلتر بالای صفحه).");

                        var trans = new CashTransaction
                        {
                            ContractId = contractId,
                            TransactionDate = date.Value,
                            Amount = amount,
                            TransactionType = method,
                            Direction = DirectionTypes.Incoming,
                            AccountId = targetAcc.Id,
                            AccountTitle = targetAcc.Title,
                            Category = "دریافت از کارفرما",
                            CostType = "خدمات",
                            Description = string.IsNullOrWhiteSpace(desc) ? "ایمپورت از اکسل" : desc
                        };

                        await _cashRepo.AddAsync(trans);

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"ردیف {rowNum}: {ex.Message}");
                    }
                }
            }, "در حال ایمپورت دریافتی‌ها از اکسل...");

            if (success && !IsDisposed)
            {
                string msg = $"✅ تعداد {successCount} ردیف دریافتی با موفقیت ثبت و به موجودی حساب اضافه شد.";
                if (errorCount > 0)
                {
                    msg += $"\n\n⚠️ {errorCount} ردیف ثبت نشد:\n" + string.Join("\n", errors.Take(5));
                    if (errors.Count > 5) msg += "\n... و موارد دیگر";
                    ModernMessageBox.ShowWarning(msg, "نتیجه ایمپورت");
                }
                else
                {
                    ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                }
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientReceiptListForm.BtnImportExcel_Click");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ایمپورت:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientReceiptListForm"); }
        }
    }
}
