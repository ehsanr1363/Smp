using Smp.Data;
using Smp.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Smp.Forms;

public class GlobalFinancialSearchForm : Form
{
    private readonly string _connectionString;
    private ModernTextBox txtSearch = null!;
    private ModernButton btnSearch = null!;
    private ModernTabHost tabHost = null!;
    private Label lblStatus = null!;

    private DataGridView dgvTransactions = null!;
    private DataGridView dgvChecks = null!;
    private DataGridView dgvContracts = null!;
    private DataGridView dgvExpenses = null!;

    // برای هر تب: جدول + پیام «حالت خالی» (راهنما / نتیجه‌ای یافت نشد)
    private readonly Label[] _emptyLabels = new Label[4];
    private const string HintInitial = "عبارت مورد نظر را در کادر بالا بنویسید و Enter بزنید";
    private const string HintNoResult = "در این بخش نتیجه‌ای یافت نشد";

    private readonly SemaphoreSlim _searchLock = new(1, 1);

    public GlobalFinancialSearchForm(string connectionString)
    {
        _connectionString = connectionString;

        BuildUI();
        this.ApplyStandardHeader("جستجوی سراسری مالی", new Size(1200, 800), isDialog: false);
        this.MinimumSize = new Size(900, 600);
        this.Shown += (s, e) => txtSearch.Focus();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnSearch);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _searchLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "GlobalFinancialSearchForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16, 14, 16, 10),
            BackColor = ModernTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));   // کارت جستجو
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));   // خلاصه‌ی نتایج
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // تب‌ها

        // ═══════ کارت جستجو ═══════
        var searchCard = new ModernCard("", ModernTheme.Primary,
            new[]
            {
                new ColumnStyle(SizeType.Percent, 100F),
                new ColumnStyle(SizeType.Absolute, 130F),
                new ColumnStyle(SizeType.Absolute, 110F)
            }, 52)
        { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };

        txtSearch = new ModernTextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "جستجو در کل سیستم (نام پیمانکار، شماره چک، موضوع قرارداد، کالا/خدمات و...)",
            Margin = new Padding(0, 4, 10, 4)
        };
        txtSearch.TextBox.Font = ModernTheme.GetFont(12.5f);
        txtSearch.TextBox.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await PerformSearchAsync(); } };

        btnSearch = ModernButton.Primary("🔍  جستجو", 120, 44);
        btnSearch.Dock = DockStyle.Fill;
        btnSearch.Margin = new Padding(0, 4, 10, 4);
        btnSearch.Click += async (s, e) => await PerformSearchAsync();

        var btnClear = ModernButton.Outline("پاک کردن", ModernTheme.Neutral, 100, 44);
        btnClear.Dock = DockStyle.Fill;
        btnClear.Margin = new Padding(0, 4, 0, 4);
        btnClear.Click += (s, e) => { txtSearch.Text = string.Empty; ClearGrids(); txtSearch.Focus(); };

        searchCard.Body.Controls.Add(txtSearch, 0, 0);
        searchCard.Body.Controls.Add(btnSearch, 1, 0);
        searchCard.Body.Controls.Add(btnClear, 2, 0);

        // ═══════ خلاصه‌ی نتایج ═══════
        lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,   // فرم Mirror: متن در سمت راست
            Font = ModernTheme.GetFont(10f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent,
            Padding = new Padding(6, 0, 6, 0),
            Text = "نتایج در چهار بخش نمایش داده می‌شوند: تراکنش‌ها، چک‌ها، قراردادها و فاکتورها"
        };

        // ═══════ تب‌های نتایج ═══════
        tabHost = new ModernTabHost { Dock = DockStyle.Fill, AccentColor = ModernTheme.Primary };

        dgvTransactions = AddResultTab("💸 تراکنش‌های نقدی و حواله", 0, ModernTheme.Primary);
        dgvChecks = AddResultTab("🧾 چک‌ها", 1, ModernTheme.Orange);
        dgvContracts = AddResultTab("📄 قراردادها", 2, ModernTheme.Purple);
        dgvExpenses = AddResultTab("🧮 فاکتورها و هزینه‌ها", 3, ModernTheme.Teal);

        root.Controls.Add(searchCard, 0, 0);
        root.Controls.Add(lblStatus, 0, 1);
        root.Controls.Add(tabHost, 0, 2);
        this.Controls.Add(root);
    }

    /// <summary>یک تب نتیجه می‌سازد: کارت سفید گرد + جدول + پیام حالت خالی</summary>
    private DataGridView AddResultTab(string title, int index, Color accent)
    {
        var page = tabHost.AddPage(title);
        page.Padding = new Padding(0, 14, 0, 4);

        var card = new ModernCard("", accent, new[] { new ColumnStyle(SizeType.Percent, 100F) });

        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = Padding.Empty };

        var dgv = CreateGrid();
        dgv.Visible = false;

        var empty = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(12f),
            ForeColor = ModernTheme.Placeholder,
            BackColor = Color.White,
            Text = "🔎\r\n\r\n" + HintInitial
        };
        _emptyLabels[index] = empty;

        host.Controls.Add(dgv);
        host.Controls.Add(empty);
        card.Body.Controls.Add(host, 0, 0);
        page.Controls.Add(card);
        return dgv;
    }

    private DataGridView CreateGrid()
    {
        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RightToLeft = RightToLeft.Yes
        };
        dgv.ApplyModernStyle();

        // فرمت فقط یک‌بار وصل می‌شود (قبلاً با هر جستجو یک هندلر جدید اضافه می‌شد)
        dgv.CellFormatting += Grid_CellFormatting;
        dgv.DataBindingComplete += Grid_DataBindingComplete;
        return dgv;
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (sender is not DataGridView dgv || e.RowIndex < 0 || e.ColumnIndex < 0) return;
        string name = dgv.Columns[e.ColumnIndex].Name;

        if (name == "مبلغ" && (e.Value is decimal || e.Value is double))
        {
            e.Value = NumberHelper.FormatThousands(Convert.ToDecimal(e.Value), usePersianDigits: true);
            e.FormattingApplied = true;
        }
        else if (name.Contains("تاریخ") && e.Value is DateTime dt)
        {
            e.Value = DateHelper.ToPersianDate(dt);
            e.FormattingApplied = true;
        }
    }

    private void Grid_DataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e)
    {
        if (sender is not DataGridView dgv) return;
        foreach (DataGridViewColumn col in dgv.Columns)
        {
            if (col.Name == "مبلغ")
            {
                col.DefaultCellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.FillWeight = 90;
            }
            else if (col.Name == "توضیحات" || col.Name == "موضوع")
            {
                col.FillWeight = 220;   // ستون‌های متنی بلند جای بیشتری بگیرند
            }
        }
    }

    private void ClearGrids()
    {
        dgvTransactions.DataSource = null;
        dgvChecks.DataSource = null;
        dgvContracts.DataSource = null;
        dgvExpenses.DataSource = null;
        foreach (var g in new[] { dgvTransactions, dgvChecks, dgvContracts, dgvExpenses }) g.Visible = false;
        foreach (var l in _emptyLabels) l.Text = "🔎\r\n\r\n" + HintInitial;
        foreach (var l in _emptyLabels) l.Visible = true;
        lblStatus.Text = "نتایج در چهار بخش نمایش داده می‌شوند: تراکنش‌ها، چک‌ها، قراردادها و فاکتورها";
    }

    /// <summary>بعد از هر جستجو: جدول یا پیام خالی هر تب را نشان می‌دهد و خلاصه‌ی نتایج را می‌نویسد</summary>
    private void UpdateResultViews()
    {
        var grids = new[] { dgvTransactions, dgvChecks, dgvContracts, dgvExpenses };
        var names = new[] { "تراکنش‌ها", "چک‌ها", "قراردادها", "فاکتورها" };
        var parts = new System.Collections.Generic.List<string>();
        int total = 0;

        for (int i = 0; i < grids.Length; i++)
        {
            int n = grids[i].Rows.Count;
            total += n;
            grids[i].Visible = n > 0;
            _emptyLabels[i].Visible = n == 0;
            _emptyLabels[i].Text = "📭\r\n\r\n" + HintNoResult;
            parts.Add($"{names[i]}: {NumberHelper.FormatThousands(n, usePersianDigits: true)}");
        }

        lblStatus.Text = total == 0
            ? "نتیجه‌ای یافت نشد"
            : $"{NumberHelper.FormatThousands(total, usePersianDigits: true)} سند یافت شد   ·   " + string.Join("   ·   ", parts);
    }

    private async Task PerformSearchAsync()
    {
        if (IsDisposed) return;
        string term = txtSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            ClearGrids();
            return;
        }

        if (!await _searchLock.WaitAsync(0)) return;

        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                string searchParam = $"%{term}%";

                // ✅ اضافه کردن TOP 100 به تمام کوئری‌ها برای جلوگیری از فریز شدن
                string sqlTx = @"
                        SELECT TOP 100 TransactionDate AS [تاریخ], Direction AS [جهت], PaymentScope AS [نوع پرداخت], 
                               CostType AS [ماهیت], ContractTitle AS [پروژه], TafsiliTitle AS [طرف حساب], 
                               Amount AS [مبلغ], Description AS [توضیحات]
                        FROM CashTransactions 
                        WHERE Description LIKE @q OR TafsiliTitle LIKE @q OR ContractTitle LIKE @q OR Amount LIKE @q
                        ORDER BY TransactionDate DESC";
                var dtTx = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, sqlTx, new SqlParameter("@q", searchParam)));

                string sqlCheck = @"
                        SELECT TOP 100 CheckNumber AS [شماره چک], DueDate AS [تاریخ سررسید], CheckType AS [نوع چک], 
                               Status AS [وضعیت], RecipientName AS [نام گیرنده], ContractTitle AS [پروژه], 
                               Amount AS [مبلغ], Description AS [توضیحات]
                        FROM CashChecks 
                        WHERE CheckNumber LIKE @q OR RecipientName LIKE @q OR Description LIKE @q OR Amount LIKE @q
                        ORDER BY DueDate DESC";
                var dtCheck = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, sqlCheck, new SqlParameter("@q", searchParam)));

                string sqlContracts = @"
                        SELECT TOP 100 * FROM (
                            SELECT 'کارفرما' AS [نوع], ContractNumber AS [شماره قرارداد], Subject AS [موضوع], 
                                   Amount AS [مبلغ], ContractDate AS [تاریخ] 
                            FROM ClientContracts WHERE ContractNumber LIKE @q OR Subject LIKE @q
                            UNION ALL
                            SELECT 'پیمانکار' AS [نوع], ContractNumber AS [شماره قرارداد], Subject AS [موضوع], 
                                   Amount AS [مبلغ], ContractDate AS [تاریخ] 
                            FROM ContractorContracts WHERE ContractNumber LIKE @q OR Subject LIKE @q
                        ) AS C
                        ORDER BY [تاریخ] DESC";
                var dtContracts = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, sqlContracts, new SqlParameter("@q", searchParam)));

                string sqlExpenses = @"
                        SELECT TOP 100 InvoiceDate AS [تاریخ فاکتور], InvoiceNumber AS [شماره فاکتور], 
                               CostType AS [ماهیت], Amount AS [مبلغ], Description AS [توضیحات]
                        FROM ProjectExpenses 
                        WHERE InvoiceNumber LIKE @q OR Description LIKE @q OR CostType LIKE @q
                        ORDER BY InvoiceDate DESC";
                var dtExpenses = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, sqlExpenses, new SqlParameter("@q", searchParam)));

                if (IsDisposed) return;

                dgvTransactions.DataSource = dtTx;
                dgvChecks.DataSource = dtCheck;
                dgvContracts.DataSource = dtContracts;
                dgvExpenses.DataSource = dtExpenses;

                UpdateResultViews();

            }, "در حال جستجو در کل پایگاه داده...");

            if (success)
            {
                int totalResults = (dgvTransactions.Rows.Count + dgvChecks.Rows.Count + dgvContracts.Rows.Count + dgvExpenses.Rows.Count);
                if (totalResults == 0)
                {
                    ToastHelper.Show("نتیجه‌ای برای این جستجو یافت نشد.", false);
                }
                else
                {
                    string warning = totalResults >= 100 ? " (ممکن است نتایج بیشتری وجود داشته باشد، لطفاً جستجو را دقیق‌تر کنید)" : "";
                    ToastHelper.Show($"مجموعاً {NumberHelper.FormatThousands(totalResults, usePersianDigits: true)} سند مرتبط یافت شد.{warning}", true);

                    if (dgvTransactions.Rows.Count > 0) tabHost.SelectedIndex = 0;
                    else if (dgvChecks.Rows.Count > 0) tabHost.SelectedIndex = 1;
                    else if (dgvContracts.Rows.Count > 0) tabHost.SelectedIndex = 2;
                    else if (dgvExpenses.Rows.Count > 0) tabHost.SelectedIndex = 3;
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "GlobalFinancialSearchForm.PerformSearchAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در جستجو:\n" + ex.Message);
        }
        finally
        {
            try { _searchLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "GlobalFinancialSearchForm"); }
        }
    }
}
