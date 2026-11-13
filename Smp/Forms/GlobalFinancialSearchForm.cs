using SMP.Data;
using SMP.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace SMP.Forms
{
    public class GlobalFinancialSearchForm : Form
    {
        private readonly string _connectionString;
        private TextBox txtSearch = null!;
        private Button btnSearch = null!;
        private TabControl tabControl = null!;

        private DataGridView dgvTransactions = null!;
        private DataGridView dgvChecks = null!;
        private DataGridView dgvContracts = null!;
        private DataGridView dgvExpenses = null!;

        private readonly SemaphoreSlim _searchLock = new(1, 1);

        public GlobalFinancialSearchForm(string connectionString)
        {
            _connectionString = connectionString;

            BuildUI();
            this.ApplyStandardHeader("جستجوی سراسری مالی", new Size(1200, 800));
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnSearch);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _searchLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // ═══════ پنل جستجو ═══════
            var pnlTop = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(20, 15, 20, 15) };

            var searchLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            searchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));

            txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(14f, FontStyle.Bold),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "جستجو در کل سیستم (نام پیمانکار، شماره چک، موضوع قرارداد، کالا/خدمات و...)",
                TextAlign = HorizontalAlignment.Center,
                Margin = new Padding(0, 5, 10, 5)
            };
            txtSearch.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await PerformSearchAsync(); } };

            btnSearch = UIHelper.CreateButton("🔍 جستجو", UIHelper.ActionGreen, 120, 45);
            btnSearch.Dock = DockStyle.Fill;
            btnSearch.Margin = new Padding(0, 3, 10, 3);
            btnSearch.Click += async (s, e) => await PerformSearchAsync();

            var btnClear = UIHelper.CreateButton("پاک کردن", UIHelper.SectionGray, 90, 45);
            btnClear.Dock = DockStyle.Fill;
            btnClear.Margin = new Padding(0, 3, 0, 3);
            btnClear.Click += (s, e) => { txtSearch.Clear(); ClearGrids(); };

            searchLayout.Controls.Add(txtSearch, 0, 0);
            searchLayout.Controls.Add(btnSearch, 1, 0);
            searchLayout.Controls.Add(btnClear, 2, 0);

            pnlTop.Controls.Add(searchLayout);

            // ═══════ تب‌های نتایج ═══════
            tabControl = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
            ModernLayoutHelper.StyleModernTabControl(tabControl);

            var tabTx = new TabPage("💸 تراکنش‌های نقدی و حواله");
            dgvTransactions = CreateGrid();
            tabTx.Controls.Add(dgvTransactions);

            var tabChecks = new TabPage("🧾 چک‌ها");
            dgvChecks = CreateGrid();
            tabChecks.Controls.Add(dgvChecks);

            var tabContracts = new TabPage("📄 قراردادها");
            dgvContracts = CreateGrid();
            tabContracts.Controls.Add(dgvContracts);

            var tabExpenses = new TabPage("🧮 فاکتورها و هزینه‌ها");
            dgvExpenses = CreateGrid();
            tabExpenses.Controls.Add(dgvExpenses);

            tabControl.TabPages.Add(tabTx);
            tabControl.TabPages.Add(tabChecks);
            tabControl.TabPages.Add(tabContracts);
            tabControl.TabPages.Add(tabExpenses);

            mainLayout.Controls.Add(pnlTop, 0, 0);
            mainLayout.Controls.Add(tabControl, 0, 1);
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
                RightToLeft = RightToLeft.Yes
            };
            dgv.ApplyModernStyle();
            return dgv;
        }

        private void ClearGrids()
        {
            dgvTransactions.DataSource = null;
            dgvChecks.DataSource = null;
            dgvContracts.DataSource = null;
            dgvExpenses.DataSource = null;
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

                    FormatGrid(dgvTransactions, "مبلغ");
                    FormatGrid(dgvChecks, "مبلغ");
                    FormatGrid(dgvContracts, "مبلغ");
                    FormatGrid(dgvExpenses, "مبلغ");

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

                        if (dgvTransactions.Rows.Count > 0) tabControl.SelectedTab = tabControl.TabPages[0];
                        else if (dgvChecks.Rows.Count > 0) tabControl.SelectedTab = tabControl.TabPages[1];
                        else if (dgvContracts.Rows.Count > 0) tabControl.SelectedTab = tabControl.TabPages[2];
                        else if (dgvExpenses.Rows.Count > 0) tabControl.SelectedTab = tabControl.TabPages[3];
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
                try { _searchLock.Release(); } catch { }
            }
        }

        private void FormatGrid(DataGridView dgv, string columnName)
        {
            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgv.Columns[e.ColumnIndex].Name == columnName && e.Value is decimal d)
                {
                    e.Value = NumberHelper.FormatThousands(d, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
                if (dgv.Columns[e.ColumnIndex].Name.Contains("تاریخ") && e.Value is DateTime dt)
                {
                    e.Value = DateHelper.ToPersianDate(dt);
                    e.FormattingApplied = true;
                }
            };
        }
    }
}