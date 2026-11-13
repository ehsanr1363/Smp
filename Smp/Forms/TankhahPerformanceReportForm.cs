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
using SMP.Data;
using SMP.Helpers;
using SMP.Models;

namespace SMP.Forms
{
    public class TankhahPerformanceReportForm : Form
    {
        private readonly string _connectionString;

        private ComboBox cmbOwnerFilter = null!;
        private Button btnRefresh = null!, btnExport = null!, btnPrint = null!;

        private TabControl tabMain = null!;

        // تب ۱: خلاصه
        private DataGridView dgvSummary = null!;
        private Label lblTotalCharged = null!, lblTotalExpenses = null!, lblTotalBalance = null!;

        // تب ۲: ریز شارژها
        private DataGridView dgvCharges = null!;
        private Label lblChargesTotal = null!;

        // تب ۳: ریز هزینه‌ها
        private DataGridView dgvExpenses = null!;
        private Label lblExpensesTotal = null!;

        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public TankhahPerformanceReportForm(string connectionString)
        {
            _connectionString = connectionString;
            BuildUI();
            this.ApplyStandardHeader("گزارش عملکرد و ریز تراکنش‌های تنخواه", new Size(1350, 850));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnExport);

            Shown += async (s, e) =>
            {
                await LoadOwnersAsync();
                await LoadReportAsync();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _loadLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));  // فیلترها
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // تب‌ها

            // ═══════ ۱. فیلترها ═══════
            var pnlTop = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground };
            var topFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 12, 10, 10) };

            var lblOwner = new Label { Text = "انتخاب تنخواه‌دار (شخص):", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Margin = new Padding(10, 8, 5, 5) };
            cmbOwnerFilter = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 6, 20, 5) };
            cmbOwnerFilter.SelectedIndexChanged += async (s, e) => await LoadReportAsync();

            btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی", UIHelper.SectionGray, 120, 35);
            btnRefresh.Margin = new Padding(5, 3, 5, 5);
            btnRefresh.Click += async (s, e) => { await LoadOwnersAsync(); await LoadReportAsync(); };

            btnExport = UIHelper.CreateButton("📥 خروجی اکسل (تب فعال)", UIHelper.ActionGreen, 180, 35);
            btnExport.Margin = new Padding(5, 3, 5, 5);
            btnExport.Click += BtnExport_Click;

            btnPrint = UIHelper.CreateButton("🖨️ چاپ (تب فعال)", UIHelper.SectionBlue, 150, 35);
            btnPrint.Margin = new Padding(5, 3, 5, 5);
            btnPrint.Click += BtnPrint_Click;

            topFlow.Controls.Add(lblOwner);
            topFlow.Controls.Add(cmbOwnerFilter);
            topFlow.Controls.Add(btnRefresh);
            topFlow.Controls.Add(btnExport);
            topFlow.Controls.Add(btnPrint);
            pnlTop.Controls.Add(topFlow);

            // ═══════ ۲. تب‌ها ═══════
            tabMain = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
            ModernLayoutHelper.StyleModernTabControl(tabMain);

            // --- تب ۱: خلاصه ---
            var tabSummary = new TabPage("📊 خلاصه عملکرد تنخواه‌ها") { BackColor = UIHelper.WarmBackground, Padding = new Padding(5) };
            var layoutSummary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layoutSummary.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F)); // افزایش ارتفاع فوتر

            dgvSummary = CreateGrid();
            dgvSummary.AutoGenerateColumns = false;
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "AccountId", DataPropertyName = "AccountId", Visible = false });
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", DataPropertyName = "Title", HeaderText = "عنوان لیست", FillWeight = 20 });
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "InitialBalance", DataPropertyName = "InitialBalance", HeaderText = "موجودی اولیه", FillWeight = 12 });
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalCharged", DataPropertyName = "TotalCharged", HeaderText = "کل پول گرفته شده", FillWeight = 15 });
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalExpenses", DataPropertyName = "TotalExpenses", HeaderText = "جمع هزینه‌ها", FillWeight = 15 });
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "TrueBalance", DataPropertyName = "TrueBalance", HeaderText = "موجودی واقعی", FillWeight = 15 });
            dgvSummary.CellFormatting += Dgv_CellFormatting;

            // جلوگیری از انگلیسی شدن هدرها توسط GridHelper
            dgvSummary.DataBindingComplete += (s, e) => {
                if (dgvSummary.Columns["TotalCharged"] != null) dgvSummary.Columns["TotalCharged"].HeaderText = "کل پول گرفته شده (شارژ)";
                if (dgvSummary.Columns["TotalExpenses"] != null) dgvSummary.Columns["TotalExpenses"].HeaderText = "جمع هزینه‌ها";
                if (dgvSummary.Columns["TrueBalance"] != null) dgvSummary.Columns["TrueBalance"].HeaderText = "موجودی واقعی";
            };

            var pnlSumFooter = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(5) };
            pnlSumFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            pnlSumFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            pnlSumFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4F));

            lblTotalCharged = CreateFooterLabel("کل شارژ: 0", UIHelper.SectionBlue);
            lblTotalExpenses = CreateFooterLabel("کل هزینه‌ها: 0", UIHelper.ActionRed);
            lblTotalBalance = CreateFooterLabel("مانده کل: 0", UIHelper.ActionGreen);

            pnlSumFooter.Controls.Add(lblTotalCharged, 0, 0);
            pnlSumFooter.Controls.Add(lblTotalExpenses, 1, 0);
            pnlSumFooter.Controls.Add(lblTotalBalance, 2, 0);

            layoutSummary.Controls.Add(dgvSummary, 0, 0);
            layoutSummary.Controls.Add(pnlSumFooter, 0, 1);
            tabSummary.Controls.Add(layoutSummary);

            // --- تب ۲: ریز شارژها (لیست تاریخ‌دار) ---
            var tabCharges = new TabPage("💵 ریز شارژهای دریافتی (با تاریخ)") { BackColor = UIHelper.WarmBackground, Padding = new Padding(5) };
            var layoutCharges = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layoutCharges.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutCharges.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            dgvCharges = CreateGrid();
            dgvCharges.AutoGenerateColumns = false;
            dgvCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", DataPropertyName = "Date", HeaderText = "تاریخ واریز", FillWeight = 15 });
            dgvCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "TankhahName", DataPropertyName = "TankhahName", HeaderText = "تنخواه مقصد", FillWeight = 25 });
            dgvCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "SourceBank", DataPropertyName = "SourceBank", HeaderText = "بانک / حساب مبدا", FillWeight = 25 });
            dgvCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", DataPropertyName = "Amount", HeaderText = "مبلغ شارژ (ریال)", FillWeight = 20 });
            dgvCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 15 });
            dgvCharges.CellFormatting += Dgv_CellFormatting;

            dgvCharges.DataBindingComplete += (s, e) => {
                if (dgvCharges.Columns["SourceBank"] != null) dgvCharges.Columns["SourceBank"].HeaderText = "بانک / حساب مبدا";
                if (dgvCharges.Columns["TankhahName"] != null) dgvCharges.Columns["TankhahName"].HeaderText = "تنخواه مقصد";
            };

            lblChargesTotal = CreateFooterLabel("جمع کل شارژها در این لیست: 0 ریال", UIHelper.ActionGreen);
            lblChargesTotal.Dock = DockStyle.Fill;

            layoutCharges.Controls.Add(dgvCharges, 0, 0);
            layoutCharges.Controls.Add(lblChargesTotal, 0, 1);
            tabCharges.Controls.Add(layoutCharges);

            // --- تب ۳: ریز هزینه‌ها ---
            var tabExpenses = new TabPage("📉 ریز هزینه‌ها (با تاریخ)") { BackColor = UIHelper.WarmBackground, Padding = new Padding(5) };
            var layoutExpenses = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layoutExpenses.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutExpenses.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

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
                if (dgvExpenses.Columns["Tafsili"] != null) dgvExpenses.Columns["Tafsili"].HeaderText = "طرف حساب";
            };

            lblExpensesTotal = CreateFooterLabel("جمع کل هزینه‌ها در این لیست: 0 ریال", UIHelper.ActionRed);
            lblExpensesTotal.Dock = DockStyle.Fill;

            layoutExpenses.Controls.Add(dgvExpenses, 0, 0);
            layoutExpenses.Controls.Add(lblExpensesTotal, 0, 1);
            tabExpenses.Controls.Add(layoutExpenses);

            tabMain.TabPages.Add(tabSummary);
            tabMain.TabPages.Add(tabCharges);
            tabMain.TabPages.Add(tabExpenses);

            mainLayout.Controls.Add(pnlTop, 0, 0);
            mainLayout.Controls.Add(tabMain, 0, 1);
            this.Controls.Add(mainLayout);
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
                RightToLeft = RightToLeft.Yes,
                RowHeadersVisible = false
            };
            dgv.ApplyModernStyle();
            return dgv;
        }

        private Label CreateFooterLabel(string text, Color color)
        {
            return new Label
            {
                Text = text,
                AutoSize = false, // جلوگیری از بریده شدن
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                ForeColor = color,
                BackColor = Color.Transparent,
                RightToLeft = RightToLeft.Yes
            };
        }

        private async Task LoadOwnersAsync()
        {
            try
            {
                var bankRepo = new BankAccountRepository(_connectionString);
                var accounts = await bankRepo.GetActiveAsync();
                var tankhahs = accounts.Where(a => a.IsCashDesk).ToList();

                var owners = tankhahs.Select(t =>
                {
                    string clean = System.Text.RegularExpressions.Regex.Replace(t.Title, @"[0-9]", "").Replace("تنخواه", "").Replace("لیست", "").Replace("شماره", "").Trim();
                    return StringHelper.NormalizePersianText(clean);
                }).Distinct().OrderBy(o => o).ToList();

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

                    // ==========================================
                    // ۱. داده‌های خلاصه
                    // ==========================================
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

                    lblTotalCharged.Text = $"کل شارژ: {NumberHelper.FormatThousands(sumCharged, usePersianDigits: true)} ریال";
                    lblTotalExpenses.Text = $"کل هزینه‌ها: {NumberHelper.FormatThousands(sumExpenses, usePersianDigits: true)} ریال";
                    lblTotalBalance.Text = $"مانده کل: {NumberHelper.FormatThousands(sumBalance, usePersianDigits: true)} ریال";
                    lblTotalBalance.ForeColor = sumBalance >= 0 ? UIHelper.ActionGreen : UIHelper.ActionRed;

                    // ==========================================
                    // ۲. ریز شارژها (با تاریخ)
                    // ==========================================
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
                    queryCharges += " ORDER BY t.TransferDate DESC";

                    var dtCharges = new DataTable();
                    using (var cmd = new SqlCommand(queryCharges, conn))
                    {
                        if (selectedOwner != "همه افراد")
                            cmd.Parameters.AddWithValue("@OwnerName", $"%{selectedOwner}%");

                        using var adapter = new SqlDataAdapter(cmd);
                        await Task.Run(() => adapter.Fill(dtCharges));
                    }

                    dgvCharges.DataSource = dtCharges;
                    decimal totalChargeList = dtCharges.AsEnumerable().Sum(r => r.Field<decimal>("Amount"));
                    lblChargesTotal.Text = $"جمع کل شارژهای نمایش داده شده: {NumberHelper.FormatThousands(totalChargeList, usePersianDigits: true)} ریال";

                    // ==========================================
                    // ۳. ریز هزینه‌ها (با تاریخ)
                    // ==========================================
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
                    lblExpensesTotal.Text = $"جمع کل هزینه‌های نمایش داده شده: {NumberHelper.FormatThousands(totalExpList, usePersianDigits: true)} ریال";

                }, "در حال استخراج گزارش...");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "TankhahPerformanceReportForm.LoadReportAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری گزارش:\n" + ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
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
                        e.CellStyle.ForeColor = UIHelper.ActionRed;
                    else if (dec > 0)
                        e.CellStyle.ForeColor = UIHelper.ActionGreen;
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

            if (activeGrid == null || activeGrid.Rows.Count == 0)
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

                // عنوان
                ws.Cell(1, 1).Value = title;
                ws.Range(1, 1, 1, activeGrid.Columns.Count).Merge().Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Font.FontSize = 14;
                ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // هدرها
                for (int i = 0; i < activeGrid.Columns.Count; i++)
                {
                    var cell = ws.Cell(3, i + 1);
                    cell.Value = activeGrid.Columns[i].HeaderText;
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // داده‌ها
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
                PrintHelper.PrintDataGridView(dgvSummary, "گزارش خلاصه عملکرد تنخواه‌ها", $"تاریخ: {DateHelper.ToPersianDate(DateTime.Now)}", $"کل شارژ: {lblTotalCharged.Text} | کل هزینه‌ها: {lblTotalExpenses.Text}");
            }
            else if (tabMain.SelectedIndex == 1 && dgvCharges.Rows.Count > 0)
            {
                PrintHelper.PrintDataGridView(dgvCharges, "ریز شارژهای تنخواه", $"تاریخ: {DateHelper.ToPersianDate(DateTime.Now)}", lblChargesTotal.Text);
            }
            else if (tabMain.SelectedIndex == 2 && dgvExpenses.Rows.Count > 0)
            {
                PrintHelper.PrintDataGridView(dgvExpenses, "ریز هزینه‌های تنخواه", $"تاریخ: {DateHelper.ToPersianDate(DateTime.Now)}", lblExpensesTotal.Text);
            }
        }
    }
}