using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System.Text.RegularExpressions;

namespace Smp.Forms;

public class TankhahPerformanceReportForm : Form
{
    private readonly string _connectionString;
    private ModernComboBox cmbOwnerFilter = null!;
    private ModernButton btnRefresh = null!, btnExport = null!, btnPrint = null!;
    private ModernTabHost tabMain = null!;

    private DataGridView dgvSummary = null!;
    private ModernStatCard cardTotalCharged = null!, cardTotalExpenses = null!, cardTotalBalance = null!;
    private DataGridView dgvCharges = null!;
    private ModernStatCard cardChargesTotal = null!;
    private DataGridView dgvExpenses = null!;
    private ModernStatCard cardExpensesTotal = null!;

    // متن‌های جمع برای چاپ (قبلاً از Text لیبل‌ها خوانده می‌شد)
    private string _summaryPrintText = string.Empty, _chargesPrintText = string.Empty, _expensesPrintText = string.Empty;

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public TankhahPerformanceReportForm(string connectionString)
    {
        _connectionString = connectionString;
        BuildUI();

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnExport);

        Shown += async (s, e) =>
        {
            await LoadOwnersAsync();
            await LoadReportAsync();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "TankhahPerformanceReportForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ═══════════ ۱. نوار بالا: انتخاب تنخواه‌دار + دکمه‌ها ═══════════
        cmbOwnerFilter = ModernUI.Combo();
        cmbOwnerFilter.SelectedIndexChanged += async (s, e) => await LoadReportAsync();

        btnRefresh = ModernButton.Soft("🔄 بروزرسانی", ModernTheme.Neutral, 130, 36);
        btnRefresh.Click += async (s, e) => { await LoadOwnersAsync(); await LoadReportAsync(); };

        btnExport = ModernButton.Success("📥 خروجی اکسل (تب فعال)", 200, 36);
        btnExport.Click += BtnExport_Click;

        btnPrint = ModernButton.Soft("🖨️ چاپ (تب فعال)", ModernTheme.Primary, 160, 36);
        btnPrint.Click += BtnPrint_Click;

        var toolbar = ModernListKit.BuildToolbar(
            (ModernListKit.FilterLabel("تنخواه‌دار (شخص)"), 0), (cmbOwnerFilter, 260),
            (btnRefresh, 138), (btnExport, 208), (btnPrint, 168));

        // ═══════════ ۲. تب‌ها ═══════════
        tabMain = new ModernTabHost { Dock = DockStyle.Fill, AccentColor = ModernTheme.Primary };

        // ── تب خلاصه عملکرد ──
        dgvSummary = CreateGrid();
        dgvSummary.AutoGenerateColumns = false;
        dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "AccountId", DataPropertyName = "AccountId", Visible = false });
        dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", DataPropertyName = "Title", HeaderText = "عنوان لیست", FillWeight = 20 });
        dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "InitialBalance", DataPropertyName = "InitialBalance", HeaderText = "موجودی اولیه", FillWeight = 12 });
        dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalCharged", DataPropertyName = "TotalCharged", HeaderText = "کل پول گرفته شده", FillWeight = 15 });
        dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalExpenses", DataPropertyName = "TotalExpenses", HeaderText = "جمع هزینه‌ها", FillWeight = 15 });
        dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "TrueBalance", DataPropertyName = "TrueBalance", HeaderText = "موجودی واقعی", FillWeight = 15 });
        dgvSummary.CellFormatting += Dgv_CellFormatting;

        dgvSummary.DataBindingComplete += (s, e) => {
            if (dgvSummary.Columns["TotalCharged"] is not null) dgvSummary.Columns["TotalCharged"].HeaderText = "کل پول گرفته شده (شارژ)";
            if (dgvSummary.Columns["TotalExpenses"] is not null) dgvSummary.Columns["TotalExpenses"].HeaderText = "جمع هزینه‌ها";
            if (dgvSummary.Columns["TrueBalance"] is not null) dgvSummary.Columns["TrueBalance"].HeaderText = "موجودی واقعی";
        };

        cardTotalCharged = NewTotalCard("کل شارژ", ModernTheme.Primary);
        cardTotalExpenses = NewTotalCard("کل هزینه‌ها", ModernTheme.Danger);
        cardTotalBalance = NewTotalCard("مانده کل", ModernTheme.Success);
        AddTab("📊 خلاصه عملکرد تنخواه‌ها", dgvSummary, cardTotalCharged, cardTotalExpenses, cardTotalBalance);

        // ── تب ریز شارژها ──
        dgvCharges = CreateGrid();
        dgvCharges.AutoGenerateColumns = false;
        dgvCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", DataPropertyName = "Date", HeaderText = "تاریخ واریز", FillWeight = 15 });
        dgvCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "TankhahName", DataPropertyName = "TankhahName", HeaderText = "تنخواه مقصد", FillWeight = 25 });
        dgvCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "SourceBank", DataPropertyName = "SourceBank", HeaderText = "بانک / حساب مبدا", FillWeight = 25 });
        dgvCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", DataPropertyName = "Amount", HeaderText = "مبلغ شارژ (ریال)", FillWeight = 20 });
        dgvCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 15 });
        dgvCharges.CellFormatting += Dgv_CellFormatting;

        dgvCharges.DataBindingComplete += (s, e) => {
            if (dgvCharges.Columns["SourceBank"] is not null) dgvCharges.Columns["SourceBank"].HeaderText = "بانک / حساب مبدا";
            if (dgvCharges.Columns["TankhahName"] is not null) dgvCharges.Columns["TankhahName"].HeaderText = "تنخواه مقصد";
        };

        cardChargesTotal = NewTotalCard("جمع کل شارژهای نمایش داده شده", ModernTheme.Success);
        AddTab("💵 ریز شارژهای دریافتی (با تاریخ)", dgvCharges, cardChargesTotal);

        // ── تب ریز هزینه‌ها ──
        dgvExpenses = CreateGrid();
        dgvExpenses.AutoGenerateColumns = false;
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", DataPropertyName = "Date", HeaderText = "تاریخ هزینه", FillWeight = 15 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "TankhahName", DataPropertyName = "TankhahName", HeaderText = "تنخواه", FillWeight = 20 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", DataPropertyName = "Type", HeaderText = "نوع سند", FillWeight = 12 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tafsili", DataPropertyName = "Tafsili", HeaderText = "طرف حساب / دریافت‌کننده", FillWeight = 25 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 15 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 15 });
        dgvExpenses.CellFormatting += Dgv_CellFormatting;

        dgvExpenses.DataBindingComplete += (s, e) => {
            if (dgvExpenses.Columns["Tafsili"] is not null) dgvExpenses.Columns["Tafsili"].HeaderText = "طرف حساب";
        };

        cardExpensesTotal = NewTotalCard("جمع کل هزینه‌های نمایش داده شده", ModernTheme.Danger);
        AddTab("📉 ریز هزینه‌ها (با تاریخ)", dgvExpenses, cardExpensesTotal);

        ModernListKit.ComposeRows(this, "گزارش عملکرد و ریز تراکنش‌های تنخواه",
            "خلاصه‌ی شارژ، هزینه و مانده‌ی هر تنخواه‌دار به‌همراه ریز تراکنش‌ها",
            new Size(1350, 850),
            (toolbar, 50), (tabMain, 0));
    }

    /// <summary>تب جدید: جدول در کارت + ردیف کارت‌های جمع پایین آن</summary>
    private void AddTab(string title, DataGridView grid, params ModernStatCard[] footerCards)
    {
        var page = tabMain.AddPage(title);
        page.Padding = new Padding(0, 12, 0, 4);

        var layout = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));

        var footer = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 8, 0, 0) };
        for (int i = 0; i < 3; i++) footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        for (int i = 0; i < footerCards.Length && i < 3; i++) footer.Controls.Add(footerCards[i], i, 0);

        layout.Controls.Add(ModernReport.WrapGrid(grid), 0, 0);
        layout.Controls.Add(footer, 0, 1);
        page.Controls.Add(layout);
    }

    private static ModernStatCard NewTotalCard(string title, Color accent)
    {
        var c = new ModernStatCard
        {
            Dock = DockStyle.Fill,
            Title = title,
            Accent = accent,
            TitleFontSize = 10.5f,
            ValueFontSize = 15f,
            Margin = new Padding(4, 0, 4, 0)
        };
        c.SetAmount(0, "ریال");
        return c;
    }

    private string ExtractOwnerName(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "نامشخص";
        string clean = StringHelper.NormalizePersianText(title);
        clean = Regex.Replace(clean, @"[0-9]", "");
        string[] wordsToRemove = { "تنخواه", "لیست", "شماره", "آقای", "خانم", "جناب", "مهندس", "دکتر", "شرکت" };
        foreach (var word in wordsToRemove) clean = clean.Replace(word, "");
        clean = clean.Trim();
        if (string.IsNullOrWhiteSpace(clean)) return title;
        return clean;
    }

    private DataGridView CreateGrid()
    {
        var dgv = ModernGrid.Create();
        dgv.AllowUserToAddRows = false;
        dgv.ReadOnly = true;
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.RowHeadersVisible = false;
        dgv.FillColumns();
        dgv.UseAiryLook(40);
        dgv.UseSelectionAccent(ModernTheme.Primary);
        return dgv;
    }

    private async Task LoadOwnersAsync()
    {
        try
        {
            var bankRepo = new BankAccountRepository(_connectionString);
            var accounts = await bankRepo.GetActiveAsync();
            var tankhahs = accounts.Where(a => a.IsCashDesk).ToList();

            // ✅ استفاده از تابع جدید برای استخراج نام شخص
            var owners = tankhahs.Select(t => ExtractOwnerName(t.Title)).ToList();
            // تنخواه‌دارهای ثبت‌شده در سیستم لیست‌های تنخواه (بدون نیاز به حساب صندوق)
            owners.AddRange((await new TankhahRepository(_connectionString).GetAllTankhahsAsync()).Select(o => o.OwnerName));
            owners = owners.Where(o => !string.IsNullOrWhiteSpace(o)).Distinct().OrderBy(o => o).ToList();

            var items = new List<string> { "همه افراد" };
            items.AddRange(owners);

            cmbOwnerFilter.DataSource = items;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahPerformanceReportForm.LoadOwnersAsync");
        }
    }

    private async Task LoadReportAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            string selectedOwner = cmbOwnerFilter.SelectedItem?.ToString() ?? "همه افراد";

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string querySummary = @"
                        SELECT 
                            a.Id AS AccountId,
                            a.Title,
                            a.InitialBalance,
                            ISNULL((SELECT SUM(Amount) FROM AccountTransfers WHERE DestinationAccountId = a.Id), 0) AS TotalCharged,
                            ISNULL((SELECT SUM(Amount) FROM CashTransactions WHERE AccountId = a.Id AND Direction = N'پرداختی' AND ISNULL(Category, '') <> N'انتقال بین حساب‌ها'), 0) +
                            ISNULL((SELECT SUM(Amount) FROM CashChecks WHERE AccountId = a.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی')), 0) AS TotalExpenses,
                            ISNULL((SELECT SUM(Amount) FROM CashTransactions WHERE AccountId = a.Id AND Direction = N'دریافتی' AND ISNULL(Category, '') <> N'انتقال بین حساب‌ها'), 0) +
                            ISNULL((SELECT SUM(Amount) FROM CashChecks WHERE AccountId = a.Id AND CheckType = N'دریافتی' AND Status NOT IN (N'باطل شده', N'برگشتی')), 0) AS TotalIncomes,
                            ISNULL((SELECT SUM(Amount) FROM AccountTransfers WHERE SourceAccountId = a.Id), 0) AS TotalSettled
                        FROM CompanyBankAccounts a
                        WHERE a.IsCashDesk = 1";

                if (selectedOwner != "همه افراد") querySummary += " AND a.Title LIKE @OwnerName";
                querySummary += " ORDER BY a.Id DESC";

                var dtSummary = new DataTable();
                using (var cmd = new SqlCommand(querySummary, conn))
                {
                    if (selectedOwner != "همه افراد")
                        cmd.Parameters.AddWithValue("@OwnerName", $"%{selectedOwner}%");

                    using var adapter = new SqlDataAdapter(cmd);
                    await Task.Run(() => adapter.Fill(dtSummary));
                }

                if (IsDisposed) return;

                // ✅ تنخواه‌دارهای سیستم لیست‌ها: شارژ = شارژهای لیست‌ها + شارژهای ثبت‌شده بدون لیست، هزینه = مصرف لیست‌ها
                var tkRepo = new TankhahRepository(_connectionString);
                foreach (var owner in await tkRepo.GetAllTankhahsAsync())
                {
                    if (selectedOwner != "همه افراد" && !owner.OwnerName.Contains(selectedOwner)) continue;
                    var ownerLists = await tkRepo.GetListsByTankhahIdAsync(owner.Id);
                    var ownerCharges = await tkRepo.GetChargesAsync(tankhahId: owner.Id);
                    if (ownerLists.Count == 0 && ownerCharges.Count == 0) continue;
                    var nr = dtSummary.NewRow();
                    nr["AccountId"] = -owner.Id;
                    nr["Title"] = owner.OwnerName;
                    nr["InitialBalance"] = 0m;
                    nr["TotalCharged"] = ownerCharges.Sum(c => c.Amount);
                    nr["TotalExpenses"] = ownerLists.Sum(l => l.ChargeAmount - l.CurrentBalance);
                    nr["TotalIncomes"] = 0m;
                    nr["TotalSettled"] = await tkRepo.GetSettledTotalAsync(owner.OwnerName); // تسویه‌های برگشتی به بانک (سند مجزا از لیست‌ها)
                    dtSummary.Rows.Add(nr);
                }

                dtSummary.Columns.Add("TrueBalance", typeof(decimal));
                foreach (DataRow row in dtSummary.Rows)
                {
                    decimal initial = Convert.ToDecimal(row["InitialBalance"]);
                    decimal charged = Convert.ToDecimal(row["TotalCharged"]);
                    decimal incomes = Convert.ToDecimal(row["TotalIncomes"]);
                    decimal expenses = Convert.ToDecimal(row["TotalExpenses"]);
                    decimal settled = Convert.ToDecimal(row["TotalSettled"]);
                    row["TrueBalance"] = (initial + charged + incomes) - (expenses + settled);
                }

                dgvSummary.DataSource = dtSummary;

                decimal sumCharged = dtSummary.AsEnumerable().Sum(r => r.Field<decimal>("TotalCharged"));
                decimal sumExpenses = dtSummary.AsEnumerable().Sum(r => r.Field<decimal>("TotalExpenses"));
                decimal sumBalance = dtSummary.AsEnumerable().Sum(r => r.Field<decimal>("TrueBalance"));

                cardTotalCharged.SetAmount(sumCharged, "ریال");
                cardTotalExpenses.SetAmount(sumExpenses, "ریال");
                cardTotalBalance.SetAmount(sumBalance, "ریال");
                cardTotalBalance.Accent = sumBalance >= 0 ? ModernTheme.Success : ModernTheme.Danger;

                _summaryPrintText = $"کل شارژ: {NumberHelper.FormatThousands(sumCharged, usePersianDigits: true)} ریال | کل هزینه‌ها: {NumberHelper.FormatThousands(sumExpenses, usePersianDigits: true)} ریال";

                string queryCharges = @"
                        SELECT 
                            t.TransferDate AS [Date],
                            d.Title AS [TankhahName],
                            ISNULL(s.Title, '---') AS [SourceBank],
                            t.Amount AS [Amount],
                            ISNULL(t.Description, '---') AS [Description]
                        FROM AccountTransfers t
                        INNER JOIN CompanyBankAccounts d ON t.DestinationAccountId = d.Id
                        LEFT JOIN CompanyBankAccounts s ON t.SourceAccountId = s.Id
                        WHERE d.IsCashDesk = 1";

                if (selectedOwner != "همه افراد") queryCharges += " AND d.Title LIKE @OwnerName";
                // ✅ شارژ لیست‌های جدید تنخواه (ثبت‌شده در CashTransactions با TankhahListId)
                queryCharges += @"
                        UNION ALL
                        SELECT
                            t.TransactionDate,
                            ISNULL(o.Value, N'نامشخص') + N' - ' + l.Title,
                            ISNULL(b.Title, '---'),
                            t.Amount,
                            ISNULL(t.Description, '---')
                        FROM CashTransactions t
                        INNER JOIN TankhahLists l ON t.TankhahListId = l.Id
                        LEFT JOIN LookupItems o ON l.TankhahId = o.Id AND o.Category = N'TankhahOwner'
                        LEFT JOIN CompanyBankAccounts b ON t.AccountId = b.Id
                        WHERE t.Category = N'انتقال بین حساب‌ها' AND t.Direction = N'پرداختی'";
                if (selectedOwner != "همه افراد") queryCharges += " AND o.Value LIKE @OwnerName";
                queryCharges += " ORDER BY [Date] DESC";

                var dtCharges = new DataTable();
                using (var cmd = new SqlCommand(queryCharges, conn))
                {
                    if (selectedOwner != "همه افراد")
                        cmd.Parameters.AddWithValue("@OwnerName", $"%{selectedOwner}%");

                    using var adapter = new SqlDataAdapter(cmd);
                    await Task.Run(() => adapter.Fill(dtCharges));
                }

                // ✅ شارژهای ایمپورت‌شده که به هیچ لیستی وصل نیستند (تراکنش نقدی «شارژ تنخواه ...» بدون TankhahListId)
                var seen = new HashSet<string>(dtCharges.AsEnumerable().Select(r => $"{r.Field<DateTime>("Date"):yyyyMMdd}|{r.Field<decimal>("Amount")}|{r.Field<string>("Description")}"));
                var tkRepo2 = new TankhahRepository(_connectionString);
                foreach (var owner in await tkRepo2.GetAllTankhahsAsync())
                {
                    if (selectedOwner != "همه افراد" && !owner.OwnerName.Contains(selectedOwner)) continue;
                    foreach (var c in (await tkRepo2.GetChargesAsync(tankhahId: owner.Id)).Where(c => c.ListId == 0))
                    {
                        string desc = string.IsNullOrWhiteSpace(c.Description) ? "---" : c.Description;
                        if (!seen.Add($"{c.Date:yyyyMMdd}|{c.Amount}|{desc}")) continue;
                        dtCharges.Rows.Add(c.Date, owner.OwnerName + " - " + c.ListTitle, c.BankTitle, c.Amount, desc);
                    }
                }
                dtCharges.DefaultView.Sort = "[Date] DESC";
                dtCharges = dtCharges.DefaultView.ToTable();

                dgvCharges.DataSource = dtCharges;
                decimal totalChargeList = dtCharges.AsEnumerable().Sum(r => r.Field<decimal>("Amount"));
                cardChargesTotal.SetAmount(totalChargeList, "ریال");
                _chargesPrintText = $"جمع کل شارژهای نمایش داده شده: {NumberHelper.FormatThousands(totalChargeList, usePersianDigits: true)} ریال";

                string queryExpenses = @"
                        SELECT 
                            t.TransactionDate AS [Date],
                            a.Title AS [TankhahName],
                            N'نقدی / حواله' AS [Type],
                            ISNULL(t.TafsiliTitle, ISNULL(t.RecipientName, '---')) AS [Tafsili],
                            t.Amount AS [Amount],
                            ISNULL(t.Description, '---') AS [Description]
                        FROM CashTransactions t
                        INNER JOIN CompanyBankAccounts a ON t.AccountId = a.Id
                        WHERE a.IsCashDesk = 1 AND t.Direction = N'پرداختی' AND ISNULL(t.Category, '') <> N'انتقال بین حساب‌ها'";

                if (selectedOwner != "همه افراد") queryExpenses += " AND a.Title LIKE @OwnerName";

                queryExpenses += @"
                        UNION ALL
                        SELECT 
                            k.DueDate,
                            a.Title,
                            N'چک',
                            ISNULL(k.RecipientName, '---'),
                            k.Amount,
                            ISNULL(k.Description, '---')
                        FROM CashChecks k
                        INNER JOIN CompanyBankAccounts a ON k.AccountId = a.Id
                        WHERE a.IsCashDesk = 1 AND k.CheckType = N'پرداختی' AND k.Status NOT IN (N'باطل شده', N'برگشتی')";

                if (selectedOwner != "همه افراد") queryExpenses += " AND a.Title LIKE @OwnerName";

                // ✅ هزینه‌های ثبت‌شده در لیست‌های تنخواه (ردیف‌هایی با TankhahListId)
                queryExpenses += @"
                        UNION ALL
                        SELECT
                            t.TransactionDate,
                            ISNULL(o.Value, N'نامشخص') + N' - ' + l.Title,
                            N'نقدی / حواله',
                            ISNULL(t.TafsiliTitle, ISNULL(t.RecipientName, '---')),
                            t.Amount,
                            ISNULL(t.Description, '---')
                        FROM CashTransactions t
                        INNER JOIN TankhahLists l ON t.TankhahListId = l.Id
                        LEFT JOIN LookupItems o ON l.TankhahId = o.Id AND o.Category = N'TankhahOwner'
                        WHERE t.Direction = N'پرداختی' AND ISNULL(t.Category, '') <> N'انتقال بین حساب‌ها'";
                if (selectedOwner != "همه افراد") queryExpenses += " AND o.Value LIKE @OwnerName";

                queryExpenses += " ORDER BY [Date] DESC";

                var dtExpenses = new DataTable();
                using (var cmd = new SqlCommand(queryExpenses, conn))
                {
                    if (selectedOwner != "همه افراد")
                        cmd.Parameters.AddWithValue("@OwnerName", $"%{selectedOwner}%");

                    using var adapter = new SqlDataAdapter(cmd);
                    await Task.Run(() => adapter.Fill(dtExpenses));
                }

                dgvExpenses.DataSource = dtExpenses;
                decimal totalExpList = dtExpenses.AsEnumerable().Sum(r => r.Field<decimal>("Amount"));
                cardExpensesTotal.SetAmount(totalExpList, "ریال");
                _expensesPrintText = $"جمع کل هزینه‌های نمایش داده شده: {NumberHelper.FormatThousands(totalExpList, usePersianDigits: true)} ریال";

            }, "در حال استخراج گزارش...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahPerformanceReportForm.LoadReportAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری گزارش:\n" + ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "TankhahPerformanceReportForm"); }
        }
    }

    private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || sender is not DataGridView dgv) return;
        string propName = dgv.Columns[e.ColumnIndex].DataPropertyName;

        if (e.Value is decimal dec)
        {
            e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
            e.FormattingApplied = true;

            if (propName == "TrueBalance" || propName == "Amount")
            {
                if (dec < 0)
                    e.CellStyle.ForeColor = ModernTheme.Danger;
                else if (dec > 0)
                    e.CellStyle.ForeColor = ModernTheme.Success;
            }
        }
        else if (propName == "Date" && e.Value is DateTime dt)
        {
            e.Value = DateHelper.ToPersianDate(dt);
            e.FormattingApplied = true;
        }
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        DataGridView? activeGrid = null;
        string sheetName = "گزارش";
        string title = "";

        if (tabMain.SelectedIndex == 0)
        {
            activeGrid = dgvSummary;
            sheetName = "خلاصه تنخواه‌ها";
            title = "گزارش خلاصه عملکرد تنخواه‌ها";
        }
        else if (tabMain.SelectedIndex == 1)
        {
            activeGrid = dgvCharges;
            sheetName = "ریز شارژها";
            title = "گزارش ریز شارژهای تنخواه (با تاریخ)";
        }
        else if (tabMain.SelectedIndex == 2)
        {
            activeGrid = dgvExpenses;
            sheetName = "ریز هزینه‌ها";
            title = "گزارش ریز هزینه‌های تنخواه (با تاریخ)";
        }

        if (activeGrid is null || activeGrid.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("داده‌ای در این تب برای خروجی وجود ندارد.");
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            FileName = $"Tankhah_{sheetName}_{DateTime.Now:yyyyMMdd}.xlsx",
            Title = "ذخیره گزارش"
        };

        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add(sheetName);
            ws.RightToLeft = true;

            ws.Cell(1, 1).Value = title;
            ws.Range(1, 1, 1, activeGrid.Columns.Count).Merge().Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            for (int i = 0; i < activeGrid.Columns.Count; i++)
            {
                var cell = ws.Cell(3, i + 1);
                cell.Value = activeGrid.Columns[i].HeaderText;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (int r = 0; r < activeGrid.Rows.Count; r++)
            {
                for (int c = 0; c < activeGrid.Columns.Count; c++)
                {
                    var val = activeGrid.Rows[r].Cells[c].Value;
                    var cell = ws.Cell(r + 4, c + 1);

                    if (val is decimal dec)
                    {
                        cell.Value = dec;
                        cell.Style.NumberFormat.Format = "#,##0";
                    }
                    else
                    {
                        cell.Value = val?.ToString() ?? "";
                    }
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(3);
            workbook.SaveAs(sfd.FileName);

            ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", true);
        }
        catch (Exception ex)
        {
            ModernMessageBox.ShowError("خطا در تولید فایل اکسل:\n" + ex.Message);
        }
    }

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        if (tabMain.SelectedIndex == 0 && dgvSummary.Rows.Count > 0)
        {
            PrintHelper.PrintDataGridView(dgvSummary, "گزارش خلاصه عملکرد تنخواه‌ها", $"تاریخ: {DateHelper.ToPersianDate(DateTime.Now)}", _summaryPrintText);
        }
        else if (tabMain.SelectedIndex == 1 && dgvCharges.Rows.Count > 0)
        {
            PrintHelper.PrintDataGridView(dgvCharges, "ریز شارژهای تنخواه", $"تاریخ: {DateHelper.ToPersianDate(DateTime.Now)}", _chargesPrintText);
        }
        else if (tabMain.SelectedIndex == 2 && dgvExpenses.Rows.Count > 0)
        {
            PrintHelper.PrintDataGridView(dgvExpenses, "ریز هزینه‌های تنخواه", $"تاریخ: {DateHelper.ToPersianDate(DateTime.Now)}", _expensesPrintText);
        }
    }
}
