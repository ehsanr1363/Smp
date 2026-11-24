using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class ComprehensiveLedgerForm : Form
{
    private readonly ComprehensiveLedgerRepository _repository;
    private readonly string _connectionString;

    private DataGridView dgvLedger = null!;
    private ModernTextBox txtSearch = null!;
    private System.Windows.Forms.Timer _searchTimer = null!;
    private ModernComboBox cmbContractFilter = null!, cmbScopeFilter = null!;

    private ModernTextBox txtPartyFilter = null!;
    private ModernButton btnSelectParty = null!, btnClearParty = null!;

    private ModernStatCard cardTotalAmount = null!, cardTotalInWords = null!;

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public ComprehensiveLedgerForm(string connectionString)
    {
        _connectionString = connectionString;
        _repository = new ComprehensiveLedgerRepository(connectionString);

        BuildUI();

        Shown += async (s, e) => await LoadInitialDataAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ComprehensiveLedgerForm"); }
            try { _searchTimer?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ComprehensiveLedgerForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ═══════════ ۱. نوار فیلتر ═══════════
        cmbContractFilter = ModernUI.Combo();
        cmbContractFilter.SelectedIndexChanged += (s, e) => _ = LoadDataAsync();

        cmbScopeFilter = ModernUI.Combo();
        cmbScopeFilter.Items.AddRange(new object[] { "همه", "اداری", "خرید مستقیم پروژه", "پیمانکار بدون قرارداد", "پیمانکار دارای قرارداد" });
        cmbScopeFilter.SelectedIndex = 0;
        cmbScopeFilter.SelectedIndexChanged += (s, e) => _ = LoadDataAsync();

        txtPartyFilter = ModernUI.Input(ModernInputKind.Text, "همه‌ی طرف حساب‌ها");
        txtPartyFilter.ReadOnly = true;

        btnSelectParty = ModernButton.Soft("🔍 انتخاب", ModernTheme.Primary, 96, 36);
        btnSelectParty.Click += BtnSelectParty_Click;

        btnClearParty = ModernButton.Soft("✖", ModernTheme.Danger, 40, 36);
        btnClearParty.Click += (s, e) => { txtPartyFilter.Text = string.Empty; _ = LoadDataAsync(); };

        txtSearch = ModernUI.Input(ModernInputKind.Text, "جستجو در همه‌ی ستون‌ها...");

        _searchTimer = new System.Windows.Forms.Timer { Interval = 400 };
        _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); _ = LoadDataAsync(); };
        txtSearch.TextBox.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

        var toolbar = ModernListKit.BuildToolbar(
            (ModernListKit.FilterLabel("قرارداد"), 0), (cmbContractFilter, 210),
            (ModernListKit.FilterLabel("نوع پرداخت"), 0), (cmbScopeFilter, 190),
            (ModernListKit.FilterLabel("طرف حساب"), 0), (txtPartyFilter, 200),
            (btnSelectParty, 104), (btnClearParty, 48),
            (ModernListKit.FilterLabel("جستجو"), 0), (txtSearch, -1));

        // ═══════════ ۲. جدول ═══════════
        dgvLedger = ModernGrid.Create();
        dgvLedger.AllowUserToAddRows = false;
        dgvLedger.ReadOnly = true;
        dgvLedger.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvLedger.FillColumns();
        dgvLedger.UseAiryLook(40);
        dgvLedger.UseSelectionAccent(ModernTheme.Primary);
        dgvLedger.EnableThousandsFormattingFor("Debit", "Credit", "Balance");
        dgvLedger.CellFormatting += DgvLedger_CellFormatting;

        // ═══════════ ۳. فوتر: جمع کل و مبلغ به حروف ═══════════
        cardTotalAmount = new ModernStatCard
        {
            Dock = DockStyle.Fill,
            Title = "جمع کل مبالغ (بدهکار)",
            Accent = ModernTheme.Danger,
            TitleFontSize = 10.5f,
            ValueFontSize = 16f,
            Margin = new Padding(0, 0, 6, 0)
        };
        cardTotalAmount.SetAmount(0, "ریال");

        cardTotalInWords = new ModernStatCard
        {
            Dock = DockStyle.Fill,
            Title = "مبلغ به حروف",
            Accent = ModernTheme.Success,
            TitleFontSize = 10.5f,
            ValueFontSize = 12f,
            Margin = new Padding(6, 0, 0, 0),
            Text = "صفر ریال"
        };

        var footer = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        footer.Controls.Add(cardTotalAmount, 0, 0);
        footer.Controls.Add(cardTotalInWords, 1, 0);

        ModernListKit.ComposeRows(this, "دفتر روزنامه جامع (چک‌ها و حواله‌ها)",
            "گردش کامل چک‌ها و حواله‌ها با فیلتر قرارداد، نوع پرداخت و طرف حساب",
            new Size(1400, 800),
            (toolbar, 56), (ModernReport.WrapGrid(dgvLedger), 0), (footer, 84));
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
                if (IsDisposed || dt is null) return;

                dgvLedger.DataSource = dt;

                if (dgvLedger.Columns["Id"] is not null) dgvLedger.Columns["Id"].Visible = false;
                if (dgvLedger.Columns["AccountId"] is not null) dgvLedger.Columns["AccountId"].Visible = false;
                if (dgvLedger.Columns["PaymentScope"] is not null) dgvLedger.Columns["PaymentScope"].Visible = false;
                if (dgvLedger.Columns["Amount"] is not null) dgvLedger.Columns["Amount"].Visible = false;
                if (dgvLedger.Columns["RunningTotal"] is not null) dgvLedger.Columns["RunningTotal"].Visible = false;
                if (dgvLedger.Columns["Seq"] is not null) dgvLedger.Columns["Seq"].Visible = false;

                if (dgvLedger.Columns["AccountTitle"] is not null) { dgvLedger.Columns["AccountTitle"].HeaderText = "حساب بانکی"; dgvLedger.Columns["AccountTitle"].FillWeight = 12; }
                if (dgvLedger.Columns["Date"] is not null) { dgvLedger.Columns["Date"].HeaderText = "تاریخ"; dgvLedger.Columns["Date"].FillWeight = 10; }
                if (dgvLedger.Columns["Type"] is not null) { dgvLedger.Columns["Type"].HeaderText = "نوع سند"; dgvLedger.Columns["Type"].FillWeight = 10; }
                if (dgvLedger.Columns["Reference"] is not null) { dgvLedger.Columns["Reference"].HeaderText = "شماره مرجع"; dgvLedger.Columns["Reference"].FillWeight = 8; }
                if (dgvLedger.Columns["PartyName"] is not null) { dgvLedger.Columns["PartyName"].HeaderText = "طرف حساب"; dgvLedger.Columns["PartyName"].FillWeight = 15; }
                if (dgvLedger.Columns["Contract"] is not null) { dgvLedger.Columns["Contract"].HeaderText = "پروژه / قرارداد"; dgvLedger.Columns["Contract"].FillWeight = 15; }
                if (dgvLedger.Columns["CostType"] is not null) { dgvLedger.Columns["CostType"].HeaderText = "ماهیت هزینه"; dgvLedger.Columns["CostType"].FillWeight = 8; }

                if (dgvLedger.Columns["Debit"] is not null) { dgvLedger.Columns["Debit"].HeaderText = "بدهکار (دریافتی)"; dgvLedger.Columns["Debit"].FillWeight = 10; }
                if (dgvLedger.Columns["Credit"] is not null) { dgvLedger.Columns["Credit"].HeaderText = "بستانکار (پرداختی)"; dgvLedger.Columns["Credit"].FillWeight = 10; }
                if (dgvLedger.Columns["Balance"] is not null) { dgvLedger.Columns["Balance"].HeaderText = "مانده حساب"; dgvLedger.Columns["Balance"].FillWeight = 10; dgvLedger.Columns["Balance"].DefaultCellStyle.BackColor = ModernTheme.WarningSoft; }

                if (dgvLedger.Columns["Status"] is not null) { dgvLedger.Columns["Status"].HeaderText = "وضعیت"; dgvLedger.Columns["Status"].FillWeight = 8; }
                if (dgvLedger.Columns["Description"] is not null) { dgvLedger.Columns["Description"].HeaderText = "توضیحات"; dgvLedger.Columns["Description"].FillWeight = 15; }

                decimal totalDebit = dt.AsEnumerable().Sum(r => r.Field<decimal>("Debit"));
                cardTotalAmount.SetAmount(totalDebit, "ریال");
                cardTotalInWords.Text = NumberHelper.ToPersianWords(totalDebit) + " ریال";

            }, "در حال دریافت اطلاعات دفتر روزنامه...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ComprehensiveLedgerForm.LoadDataAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ComprehensiveLedgerForm"); }
        }
    }
}
