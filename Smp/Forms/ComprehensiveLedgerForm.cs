using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class ComprehensiveLedgerForm : Form
    {
        private readonly ComprehensiveLedgerRepository _repository;
        private readonly string _connectionString;

        private DataGridView dgvLedger = null!;
        private TextBox txtSearch = null!;
        private System.Windows.Forms.Timer _searchTimer = null!;
        private ComboBox cmbContractFilter = null!, cmbScopeFilter = null!;

        private TextBox txtPartyFilter = null!;
        private Button btnSelectParty = null!, btnClearParty = null!;

        private Label lblTotalAmount = null!, lblTotalInWords = null!;

        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public ComprehensiveLedgerForm(string connectionString)
        {
            _connectionString = connectionString;
            _repository = new ComprehensiveLedgerRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("دفتر روزنامه جامع (چک‌ها و حواله‌ها)", new Size(1400, 800));

            Shown += async (s, e) => await LoadInitialDataAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _loadLock?.Dispose(); } catch { }
                try { _searchTimer?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10), RightToLeft = RightToLeft.Yes };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));

            var tblFilters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 1, RightToLeft = RightToLeft.Yes };

            tblFilters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            tblFilters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            tblFilters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F));
            tblFilters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var lblContract = new Label { Text = "قرارداد:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Anchor = AnchorStyles.Right, Margin = new Padding(5, 15, 5, 5) };
            cmbContractFilter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 12, 15, 5) };
            cmbContractFilter.SelectedIndexChanged += (s, e) => _ = LoadDataAsync();

            var lblScope = new Label { Text = "نوع پرداخت:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Anchor = AnchorStyles.Right, Margin = new Padding(5, 15, 5, 5) };
            cmbScopeFilter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 12, 15, 5) };
            cmbScopeFilter.Items.AddRange(new object[] { "همه", "اداری", "خرید مستقیم پروژه", "پیمانکار بدون قرارداد", "پیمانکار دارای قرارداد" });
            cmbScopeFilter.SelectedIndex = 0;
            cmbScopeFilter.SelectedIndexChanged += (s, e) => _ = LoadDataAsync();

            var lblParty = new Label { Text = "طرف حساب:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Anchor = AnchorStyles.Right, Margin = new Padding(5, 15, 5, 5) };

            var pnlParty = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(5, 12, 15, 5), RightToLeft = RightToLeft.Yes };
            pnlParty.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlParty.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40F));
            pnlParty.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40F));

            txtPartyFilter = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmInputBackground, TextAlign = HorizontalAlignment.Right, Margin = new Padding(0) };

            btnSelectParty = new Button { Text = "🔍", Dock = DockStyle.Fill, BackColor = UIHelper.SectionBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Margin = new Padding(3, 0, 0, 0) };
            btnSelectParty.FlatAppearance.BorderSize = 0;
            btnSelectParty.Click += BtnSelectParty_Click;

            btnClearParty = new Button { Text = "❌", Dock = DockStyle.Fill, BackColor = UIHelper.ActionRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Margin = new Padding(3, 0, 0, 0) };
            btnClearParty.FlatAppearance.BorderSize = 0;
            btnClearParty.Click += (s, e) => { txtPartyFilter.Text = ""; _ = LoadDataAsync(); };

            pnlParty.Controls.Add(txtPartyFilter, 0, 0);
            pnlParty.Controls.Add(btnSelectParty, 1, 0);
            pnlParty.Controls.Add(btnClearParty, 2, 0);

            var lblSearch = new Label { Text = "جستجو:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Anchor = AnchorStyles.Right, Margin = new Padding(5, 15, 5, 5) };
            txtSearch = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f), TextAlign = HorizontalAlignment.Right, Margin = new Padding(5, 12, 5, 5) };

            _searchTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); _ = LoadDataAsync(); };
            txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

            tblFilters.Controls.Add(lblContract, 0, 0);
            tblFilters.Controls.Add(cmbContractFilter, 1, 0);
            tblFilters.Controls.Add(lblScope, 2, 0);
            tblFilters.Controls.Add(cmbScopeFilter, 3, 0);
            tblFilters.Controls.Add(lblParty, 4, 0);
            tblFilters.Controls.Add(pnlParty, 5, 0);
            tblFilters.Controls.Add(lblSearch, 6, 0);
            tblFilters.Controls.Add(txtSearch, 7, 0);

            dgvLedger = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                RightToLeft = RightToLeft.Yes,
                RowHeadersVisible = false,
                Font = UIHelper.PersianFont(10f)
            };
            dgvLedger.EnableHeadersVisualStyles = false;
            dgvLedger.ColumnHeadersDefaultCellStyle.BackColor = UIHelper.WarmPanelBackground;
            dgvLedger.ColumnHeadersDefaultCellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold);
            dgvLedger.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvLedger.ColumnHeadersHeight = 40;
            dgvLedger.CellFormatting += DgvLedger_CellFormatting;

            var pnlFooter = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground };
            pnlFooter.Paint += (s, e) => { using var pen = new Pen(UIHelper.WarmBorder, 1); e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0); };

            var layoutFooter = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, RightToLeft = RightToLeft.Yes };
            layoutFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            layoutFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F));
            layoutFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var lblSumTitle = new Label { Text = "جمع کل مبالغ (بدهکار):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(12f, FontStyle.Bold), ForeColor = UIHelper.WarmText };
            lblTotalAmount = new Label { Text = "0 ریال", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(14f, FontStyle.Bold), ForeColor = UIHelper.ActionRed };
            lblTotalInWords = new Label { Text = "صفر ریال", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.ActionGreen, Padding = new Padding(10, 0, 0, 0) };

            layoutFooter.Controls.Add(lblSumTitle, 0, 0);
            layoutFooter.Controls.Add(lblTotalAmount, 1, 0);
            layoutFooter.Controls.Add(lblTotalInWords, 2, 0);
            pnlFooter.Controls.Add(layoutFooter);

            mainLayout.Controls.Add(tblFilters, 0, 0);
            mainLayout.Controls.Add(dgvLedger, 0, 1);
            mainLayout.Controls.Add(pnlFooter, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void DgvLedger_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvLedger.Columns[e.ColumnIndex].Name;

            if (colName == "Date" && e.Value is DateTime dt)
            {
                e.Value = DateHelper.ToPersianDate(dt);
                e.FormattingApplied = true;
            }
            else if ((colName == "Amount" || colName == "Debit" || colName == "Credit" || colName == "Balance") && e.Value is decimal dec)
            {
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.FormattingApplied = true;
            }
        }

        private void BtnSelectParty_Click(object? sender, EventArgs e)
        {
            using (var form = new TafsiliSelectionForm(_connectionString))
            {
                if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed)
                {
                    txtPartyFilter.Text = form.SelectedName;
                    _ = LoadDataAsync();
                }
            }
        }

        private async Task LoadInitialDataAsync()
        {
            if (IsDisposed) return;

            try
            {
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var contracts = await _repository.GetDistinctContractsAsync();
                    contracts.Insert(0, "همه قراردادها");

                    if (IsDisposed) return;
                    cmbContractFilter.DataSource = contracts;
                }, "در حال بارگذاری فیلترها...");

                if (success && !IsDisposed)
                {
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ComprehensiveLedgerForm.LoadInitialDataAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
            }
        }

        private async Task LoadDataAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                string search = txtSearch.Text.Trim();
                string contract = cmbContractFilter.SelectedItem?.ToString() ?? "همه قراردادها";
                string scope = cmbScopeFilter.SelectedItem?.ToString() ?? "همه";
                string party = txtPartyFilter.Text.Trim();

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var dt = await _repository.GetFilteredLedgerAsync(search, contract, scope, party);
                    if (IsDisposed || dt == null) return;

                    dgvLedger.DataSource = dt;

                    if (dgvLedger.Columns["Id"] != null) dgvLedger.Columns["Id"].Visible = false;
                    if (dgvLedger.Columns["AccountId"] != null) dgvLedger.Columns["AccountId"].Visible = false;
                    if (dgvLedger.Columns["PaymentScope"] != null) dgvLedger.Columns["PaymentScope"].Visible = false;
                    if (dgvLedger.Columns["Amount"] != null) dgvLedger.Columns["Amount"].Visible = false;
                    if (dgvLedger.Columns["RunningTotal"] != null) dgvLedger.Columns["RunningTotal"].Visible = false;
                    if (dgvLedger.Columns["Seq"] != null) dgvLedger.Columns["Seq"].Visible = false;

                    if (dgvLedger.Columns["AccountTitle"] != null) { dgvLedger.Columns["AccountTitle"].HeaderText = "حساب بانکی"; dgvLedger.Columns["AccountTitle"].FillWeight = 12; }
                    if (dgvLedger.Columns["Date"] != null) { dgvLedger.Columns["Date"].HeaderText = "تاریخ"; dgvLedger.Columns["Date"].FillWeight = 10; }
                    if (dgvLedger.Columns["Type"] != null) { dgvLedger.Columns["Type"].HeaderText = "نوع سند"; dgvLedger.Columns["Type"].FillWeight = 10; }
                    if (dgvLedger.Columns["Reference"] != null) { dgvLedger.Columns["Reference"].HeaderText = "شماره مرجع"; dgvLedger.Columns["Reference"].FillWeight = 8; }
                    if (dgvLedger.Columns["PartyName"] != null) { dgvLedger.Columns["PartyName"].HeaderText = "طرف حساب"; dgvLedger.Columns["PartyName"].FillWeight = 15; }
                    if (dgvLedger.Columns["Contract"] != null) { dgvLedger.Columns["Contract"].HeaderText = "پروژه / قرارداد"; dgvLedger.Columns["Contract"].FillWeight = 15; }
                    if (dgvLedger.Columns["CostType"] != null) { dgvLedger.Columns["CostType"].HeaderText = "ماهیت هزینه"; dgvLedger.Columns["CostType"].FillWeight = 8; }

                    if (dgvLedger.Columns["Debit"] != null) { dgvLedger.Columns["Debit"].HeaderText = "بدهکار (دریافتی)"; dgvLedger.Columns["Debit"].FillWeight = 10; }
                    if (dgvLedger.Columns["Credit"] != null) { dgvLedger.Columns["Credit"].HeaderText = "بستانکار (پرداختی)"; dgvLedger.Columns["Credit"].FillWeight = 10; }
                    if (dgvLedger.Columns["Balance"] != null) { dgvLedger.Columns["Balance"].HeaderText = "مانده حساب"; dgvLedger.Columns["Balance"].FillWeight = 10; dgvLedger.Columns["Balance"].DefaultCellStyle.BackColor = UIHelper.WarmWarningBackground; }

                    if (dgvLedger.Columns["Status"] != null) { dgvLedger.Columns["Status"].HeaderText = "وضعیت"; dgvLedger.Columns["Status"].FillWeight = 8; }
                    if (dgvLedger.Columns["Description"] != null) { dgvLedger.Columns["Description"].HeaderText = "توضیحات"; dgvLedger.Columns["Description"].FillWeight = 15; }

                    decimal totalDebit = dt.AsEnumerable().Sum(r => r.Field<decimal>("Debit"));
                    lblTotalAmount.Text = NumberHelper.FormatThousands(totalDebit, usePersianDigits: true) + " ریال";
                    lblTotalInWords.Text = NumberHelper.ToPersianWords(totalDebit) + " ریال";

                }, "در حال دریافت اطلاعات دفتر روزنامه...");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ComprehensiveLedgerForm.LoadDataAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }
    }
}