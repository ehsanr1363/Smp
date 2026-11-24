using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Models;
using Smp.Helpers;
using Smp.Data;

namespace Smp.Forms;

public class ContractorStatementReportForm : Form
{
    private readonly string _connectionString;
    private readonly ContractorContract _contract;

    private TabControl tabControl = null!;
    private DataGridView dgvStatements = null!;
    private DataGridView dgvPayments = null!;
    private DataGridView dgvGuarantees = null!;

    private ModernStatCard lblSummaryTotalContract = null!, lblSummaryTotalWork = null!, lblSummaryRemaining = null!, lblSummaryProgress = null!;
    private ModernStatCard lblSummaryTotalPaid = null!, lblSummaryDebt = null!;
    private ModernStatCard lblSummaryFormalPaid = null!, lblSummaryCostTypeSplit = null!;
    private static readonly Font _boldFont = ModernTheme.GetFont(11f, FontStyle.Bold);

    private Button btnAddStatement = null!, btnEditStatement = null!, btnDeleteStatement = null!;
    private Button btnPrintList = null!, btnPrintSingle = null!, btnExportExcel = null!;
    private Button btnAddPayment = null!, btnPrintPayment = null!;
    private Button btnAddGuarantee = null!, btnEditGuarantee = null!, btnDeleteGuarantee = null!;

    private PictureBox pbLogo = null!;
    private Label lblLogoPlaceholder = null!;

    private PrintDocument printDoc = null!;
    private int printRowIndex = 0;
    private bool isPrintingHeader = true;

    private PrintDocument paymentPrintDoc = null!;
    private decimal _paymentAmountForPrint = 0;
    private string _paymentTypeForPrint = "";
    private string _paymentDateForPrint = "";
    private string _paymentDescForPrint = "";

    private PrintDocument singleStatementPrintDoc = null!;
    private string _sglStatementNumber = "";
    private decimal _sglGross = 0;
    private decimal _sglVat = 0;
    private decimal _sglTotal = 0;
    private decimal _sglRetention = 0;
    private decimal _sglInsurance = 0;
    private decimal _sglAdvance = 0;
    private decimal _sglNet = 0;

    // ═══ ViewModel یکپارچه پرداخت‌ها ═══
    private class UnifiedPayment
    {
        public int Id { get; set; }
        public string Source { get; set; } = "";
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = "";
        public string CostType { get; set; } = "";
        public string Description { get; set; } = "";
    }

    private List<UnifiedPayment> _allPayments = new();

    public ContractorStatementReportForm(string connectionString, ContractorContract contract)
    {
        _connectionString = connectionString;
        _contract = contract;

        BuildUI();
        SetupPrintDocument();

        ShortcutHelper.ApplyStandardShortcuts(this);
        ShowInTaskbar = false;

        Shown += async (s, e) =>
        {
            LoadCompanyLogo();
            await LoadDataSafeAsync();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { printDoc?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorStatementReportForm"); }
            try { paymentPrintDoc?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorStatementReportForm"); }
            try { singleStatementPrintDoc?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorStatementReportForm"); }
            try { pbLogo?.Image?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorStatementReportForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        ModernForm.Setup(this, $"گزارش مالی قرارداد - {_contract.ContractNumber}", new Size(1250, 880));
        this.Padding = new Padding(15);
        this.MaximizeBox = true;

        var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 178F));   // کارت اطلاعات قرارداد
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));    // کارت‌های خلاصه ردیف ۱
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));    // کارت‌های خلاصه ردیف ۲
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));    // تب‌ها
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));    // دکمه‌های پایین

        // ==========================================
        // ۱. کارت اطلاعات قرارداد
        // ==========================================
        var headerCard = ModernUI.SurfacePanel();
        headerCard.BackColor = ModernTheme.Surface;
        headerCard.Margin = new Padding(16, 10, 16, 4);

        var headerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        pbLogo = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(10), BackColor = Color.Transparent };
        lblLogoPlaceholder = new Label { Text = "لوگو شرکت", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = ModernTheme.TextMuted, BackColor = Color.Transparent };
        pbLogo.Controls.Add(lblLogoPlaceholder);
        headerLayout.Controls.Add(pbLogo, 0, 0);

        var infoLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 5, BackColor = Color.Transparent };
        infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 5; i++) infoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

        // عنوان‌ها کم‌رنگ و کوچک، مقدارها پررنگ؛ تا اطلاعات قرارداد در یک نگاه خوانده شود
        void AddInfoLabel(TableLayoutPanel layout, string text, int col, int row, bool isBold, int rightMargin = 5)
        {
            var lbl = ModernUI.Caption(text, isBold ? 10f : 11f, true, isBold ? ModernTheme.TextMuted : ModernTheme.TextStrong);
            lbl.AutoSize = true;
            lbl.Anchor = AnchorStyles.Left;
            lbl.Margin = new Padding(5, 0, rightMargin, 0);
            layout.Controls.Add(lbl, col, row);
        }

        AddInfoLabel(infoLayout, "پیمانکار:", 0, 0, true);
        AddInfoLabel(infoLayout, _contract.DisplayName, 1, 0, false, 40);
        AddInfoLabel(infoLayout, "شماره قرارداد:", 3, 0, true);
        AddInfoLabel(infoLayout, _contract.ContractNumber, 4, 0, false, 0);

        AddInfoLabel(infoLayout, "موضوع:", 0, 1, true);
        AddInfoLabel(infoLayout, _contract.Subject, 1, 1, false, 40);
        AddInfoLabel(infoLayout, "مبلغ کل:", 3, 1, true);
        AddInfoLabel(infoLayout, NumberHelper.FormatThousands(_contract.Amount, usePersianDigits: true) + " ریال", 4, 1, false, 0);

        AddInfoLabel(infoLayout, "تاریخ قرارداد:", 0, 2, true);
        AddInfoLabel(infoLayout, DateHelper.ToPersianDate(_contract.ContractDate), 1, 2, false, 40);
        AddInfoLabel(infoLayout, "تاریخ پایان:", 3, 2, true);
        AddInfoLabel(infoLayout, DateHelper.ToPersianDate(_contract.EndDate), 4, 2, false, 0);

        AddInfoLabel(infoLayout, "پیش پرداخت:", 0, 3, true);
        AddInfoLabel(infoLayout, NumberHelper.FormatThousands(_contract.TotalAdvanceAmount, usePersianDigits: true) + " ریال", 1, 3, false, 40);
        AddInfoLabel(infoLayout, "حسن انجام کار:", 3, 3, true);
        AddInfoLabel(infoLayout, StringHelper.ConvertToPersianDigits(_contract.RetentionPercentage.ToString("0.##")) + "%", 4, 3, false, 0);

        AddInfoLabel(infoLayout, "بیمه:", 0, 4, true);
        AddInfoLabel(infoLayout, StringHelper.ConvertToPersianDigits(_contract.InsurancePercentage.ToString("0.##")) + "%", 1, 4, false, 40);
        AddInfoLabel(infoLayout, "ارزش افزوده:", 3, 4, true);
        AddInfoLabel(infoLayout, StringHelper.ConvertToPersianDigits(_contract.VatPercentage.ToString("0.##")) + "%", 4, 4, false, 0);

        headerLayout.Controls.Add(infoLayout, 1, 0);
        headerCard.Controls.Add(headerLayout);

        // ==========================================
        // ۲. خلاصه وضعیت مالی (۸ کارت در دو ردیف)
        // ==========================================
        var strip1 = new StatStrip(4, 76);
        lblSummaryTotalContract = strip1.Add("مبلغ قرارداد (ریال)", ModernTheme.Primary);
        lblSummaryTotalWork = strip1.Add("کارکرد (ریال)", ModernTheme.Teal);
        lblSummaryRemaining = strip1.Add("مانده قرارداد (ریال)", ModernTheme.Orange);
        lblSummaryProgress = strip1.Add("پیشرفت", ModernTheme.Success);
        strip1.Margin = new Padding(10, 2, 10, 2);

        var strip2 = new StatStrip(4, 76);
        lblSummaryTotalPaid = strip2.Add("پرداخت شده (ریال)", ModernTheme.Success);
        lblSummaryDebt = strip2.Add("مانده بدهی (ریال)", ModernTheme.Danger);
        lblSummaryFormalPaid = strip2.Add("پرداخت‌های رسمی (ریال)", ModernTheme.Purple);
        lblSummaryCostTypeSplit = strip2.Add("تفکیک کالا / خدمات", ModernTheme.Orange);
        strip2.Margin = new Padding(10, 2, 10, 2);

        // ==========================================
        // ۳. تب‌ها
        // ==========================================
        tabControl = new TabControl { Dock = DockStyle.Fill, Font = ModernTheme.GetFont(11f, FontStyle.Bold), Margin = new Padding(16, 4, 16, 4) };
        ModernForm.StyleTabControl(tabControl);

        TabPage tabStatements = new TabPage("📄 صورت وضعیت‌ها");
        TabPage tabPayments = new TabPage("💳 پرداخت‌ها");
        TabPage tabGuarantees = new TabPage("🏦 ضمانت‌نامه‌ها");
        foreach (var tp in new[] { tabStatements, tabPayments, tabGuarantees }) tp.BackColor = ModernTheme.Background;

        // ═══════════════════════════════════════════
        // تب ۱: صورت وضعیت‌ها
        // ═══════════════════════════════════════════
        dgvStatements = CreateGrid();
        dgvStatements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        dgvStatements.Columns.Add("RowNum", "شماره");
        dgvStatements.Columns.Add("CumulativeAmount", "مبلغ انباشته");
        dgvStatements.Columns.Add("GrossAmount", "مبلغ جاری");
        dgvStatements.Columns.Add("VAT", "ارزش افزوده");
        dgvStatements.Columns.Add("TotalAmount", "جمع کل");
        dgvStatements.Columns.Add("RetentionDeduction", "حسن انجام کار");
        dgvStatements.Columns.Add("InsuranceDeduction", "بیمه");
        dgvStatements.Columns.Add("OtherDeductions", "سایر");
        dgvStatements.Columns.Add("AdvanceRecovery", "پیش پرداخت");
        dgvStatements.Columns.Add("TotalDeductions", "جمع کسورات");
        dgvStatements.Columns.Add("NetPayableAmount", "خالص");
        dgvStatements.Columns["NetPayableAmount"].DefaultCellStyle.Font = _boldFont;   // مبلغ خالص برجسته

        dgvStatements.CellDoubleClick += DgvStatements_CellDoubleClick;
        dgvStatements.SelectionChanged += (s, e) => UpdateStatementButtons();
        tabStatements.Padding = new Padding(12, 10, 12, 6);
        tabStatements.Controls.Add(ModernReport.WrapGrid(dgvStatements));

        // ═══════════════════════════════════════════
        // تب ۲: پرداخت‌ها
        // ═══════════════════════════════════════════
        dgvPayments = CreateGrid();
        dgvPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        dgvPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "منبع پرداخت", FillWeight = 12 });
        dgvPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "PaymentDate", HeaderText = "تاریخ پرداخت", FillWeight = 12 });
        dgvPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "CostType", HeaderText = "ماهیت هزینه", FillWeight = 15 });
        dgvPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "مبلغ پرداختی", FillWeight = 15 });
        dgvPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "PaymentType", HeaderText = "روش پرداخت", FillWeight = 12 });
        dgvPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "توضیحات", FillWeight = 34 });
        dgvPayments.Columns["Description"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        dgvPayments.Columns["Amount"].DefaultCellStyle.Font = _boldFont;
        dgvPayments.UseBadges("CostType", new Dictionary<string, Color>
        {
            ["خرید کالا"] = ModernTheme.Orange,
            ["خرید خدمات"] = ModernTheme.Teal
        });
        dgvPayments.SelectionChanged += (s, e) => btnPrintPayment.Enabled = dgvPayments.CurrentRow is not null;

        var tabPaymentsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        tabPaymentsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tabPaymentsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

        btnAddPayment = ModernButton.Success("➕ ثبت پرداخت جدید (حواله/نقدی)", 250, 36);
        btnAddPayment.Click += async (s, e) => await AddPaymentViaCashTransactionAsync();

        btnPrintPayment = ModernButton.Soft("🖨️ پرینت رسید پرداخت", ModernTheme.Primary, 180, 36);
        btnPrintPayment.Enabled = false;
        btnPrintPayment.Click += BtnPrintPayment_Click;

        var paymentGridHost = ModernReport.WrapGrid(dgvPayments);
        paymentGridHost.Margin = new Padding(12, 10, 12, 6);

        tabPaymentsLayout.Controls.Add(paymentGridHost, 0, 0);
        tabPaymentsLayout.Controls.Add(ModernForm.CreateButtonBar(btnAddPayment, btnPrintPayment), 0, 1);
        tabPayments.Controls.Add(tabPaymentsLayout);

        // ═══════════════════════════════════════════
        // تب ۳: ضمانت‌نامه‌ها
        // ═══════════════════════════════════════════
        dgvGuarantees = CreateGrid();
        dgvGuarantees.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        dgvGuarantees.Columns.Add("GuaranteeNumber", "شماره ضمانت‌نامه");
        dgvGuarantees.Columns.Add("BankName", "نام بانک");
        dgvGuarantees.Columns.Add("GuaranteeType", "نوع ضمانت");
        dgvGuarantees.Columns.Add("Amount", "مبلغ");
        dgvGuarantees.Columns.Add("IssueDate", "تاریخ صدور");
        dgvGuarantees.Columns.Add("MaturityDate", "تاریخ سررسید");
        dgvGuarantees.Columns.Add("Status", "وضعیت");

        dgvGuarantees.Columns["Amount"].DefaultCellStyle.Font = _boldFont;
        dgvGuarantees.UseBadges("Status", new Dictionary<string, Color> { ["فعال"] = ModernTheme.Success });
        dgvGuarantees.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditSelectedGuarantee(); };
        dgvGuarantees.SelectionChanged += (s, e) => UpdateGuaranteeButtons();

        var tabGuaranteesLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        tabGuaranteesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tabGuaranteesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

        btnAddGuarantee = ModernButton.Primary("➕ ثبت ضمانت‌نامه", 160, 36);
        btnAddGuarantee.Click += (s, e) =>
        {
            try
            {
                using var gForm = new ContractorGuaranteeEditForm(_connectionString, _contract);
                if (gForm.ShowDialog(this) == DialogResult.OK && !IsDisposed) _ = LoadDataSafeAsync();
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ContractorStatementReportForm.AddGuarantee");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
            }
        };

        btnEditGuarantee = ModernButton.Soft("✏️ ویرایش ردیف", ModernTheme.Primary, 140, 36);
        btnEditGuarantee.Enabled = false;
        btnEditGuarantee.Click += (s, e) => EditSelectedGuarantee();

        btnDeleteGuarantee = ModernButton.Soft("🗑️ حذف ردیف", ModernTheme.Danger, 140, 36);
        btnDeleteGuarantee.Enabled = false;
        btnDeleteGuarantee.Click += DeleteSelectedGuarantee;

        var guaranteeGridHost = ModernReport.WrapGrid(dgvGuarantees);
        guaranteeGridHost.Margin = new Padding(12, 10, 12, 6);

        tabGuaranteesLayout.Controls.Add(guaranteeGridHost, 0, 0);
        tabGuaranteesLayout.Controls.Add(ModernForm.CreateButtonBar(btnAddGuarantee, btnEditGuarantee, btnDeleteGuarantee), 0, 1);
        tabGuarantees.Controls.Add(tabGuaranteesLayout);

        tabControl.TabPages.Add(tabStatements);
        tabControl.TabPages.Add(tabPayments);
        tabControl.TabPages.Add(tabGuarantees);

        // ==========================================
        // ۴. دکمه‌های پایین (صورت وضعیت)
        // ==========================================
        btnAddStatement = ModernButton.Primary("➕ افزودن صورت وضعیت", 180, 38);
        btnAddStatement.Click += (s, e) =>
        {
            try
            {
                using var editForm = new ContractorStatementEditForm(_connectionString, _contract);
                if (editForm.ShowDialog(this) == DialogResult.OK && !IsDisposed) _ = LoadDataSafeAsync();
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ContractorStatementReportForm.AddStatement");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
            }
        };

        btnEditStatement = ModernButton.Soft("✏️ ویرایش ردیف", ModernTheme.Primary, 150, 38);
        btnEditStatement.Enabled = false;
        btnEditStatement.Click += (s, e) => OpenEditFormForSelectedRow();

        btnDeleteStatement = ModernButton.Soft("🗑️ حذف ردیف", ModernTheme.Danger, 150, 38);
        btnDeleteStatement.Enabled = false;
        btnDeleteStatement.Click += DeleteSelectedRow;

        btnExportExcel = ModernButton.Success("📥 خروجی اکسل", 150, 38);
        btnExportExcel.Click += BtnExportExcel_Click;

        btnPrintList = ModernButton.Outline("🖨️ پرینت لیست", ModernTheme.Primary, 150, 38);
        btnPrintList.Click += BtnPrint_Click;

        btnPrintSingle = ModernButton.Outline("🖨️ پرینت تک صورت‌وضعیت", ModernTheme.Neutral, 190, 38);
        btnPrintSingle.Enabled = false;
        btnPrintSingle.Click += BtnPrintSingleStatement_Click;

        var bottomBar = ModernForm.CreateButtonBar(btnAddStatement, btnEditStatement, btnDeleteStatement, btnExportExcel, btnPrintList, btnPrintSingle);

        var tabHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = ModernTheme.Background, Padding = new Padding(16, 4, 16, 4) };
        tabControl.Margin = Padding.Empty;
        tabHost.Controls.Add(tabControl);

        mainLayout.Controls.Add(headerCard, 0, 0);
        mainLayout.Controls.Add(strip1, 0, 1);
        mainLayout.Controls.Add(strip2, 0, 2);
        mainLayout.Controls.Add(tabHost, 0, 3);
        mainLayout.Controls.Add(bottomBar, 0, 4);

        Controls.Add(mainLayout);
    }

    private DataGridView CreateGrid()
    {
        var dgv = new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        dgv.ApplyModernStyle();
        dgv.FillColumns();
        // FillColumns مقدارها را MiddleRight می‌کند که در گرید RTL زیر هدر وسط‌چین نمی‌نشیند
        dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        return dgv;
    }

    private void LoadCompanyLogo()
    {
        try
        {
            var companyRepo = new CompanyRepository(_connectionString);
            var company = companyRepo.GetAll().FirstOrDefault();

            if (company is not null && company.Logo is not null && company.Logo.Length > 0)
            {
                using var ms = new MemoryStream(company.Logo);
                using var img = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false);
                var bmp = new Bitmap(img);

                if (!IsDisposed && !pbLogo.IsDisposed)
                {
                    pbLogo.Image = bmp;
                    lblLogoPlaceholder.Visible = false;
                }
                else bmp.Dispose();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogWarning("خطا در بارگذاری لوگو: " + ex.Message, "ContractorStatementReportForm");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ✅ بارگذاری داده‌ها (نسخه بهینه‌شده - فقط اطلاعات این قرارداد)
    // ═══════════════════════════════════════════════════════════════
    private async Task LoadDataSafeAsync()
    {
        try
        {
            List<ContractorStatement>? statements = null;
            List<UnifiedPayment>? unifiedPayments = null;
            List<ContractorGuarantee>? guarantees = null;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var sRepo = new ContractorStatementRepository(_connectionString);
                var pRepo = new ContractorPaymentRepository(_connectionString);
                var gRepo = new ContractorGuaranteeRepository(_connectionString);
                var tRepo = new CashTransactionRepository(_connectionString);
                var cRepo = new CashCheckRepository(_connectionString);

                var sTask = Task.Run(() => sRepo.GetByContractId(_contract.Id));
                var pTask = Task.Run(() => pRepo.GetByContractId(_contract.Id));
                var gTask = Task.Run(() => gRepo.GetByContractId(_contract.Id));

                // ✅ استفاده از متدهای بهینه (فقط تراکنش‌ها و چک‌های این قرارداد)
                var tTask = tRepo.GetByContractorContractIdAsync(_contract.Id);
                var cTask = cRepo.GetByContractorContractIdAsync(_contract.Id);

                await Task.WhenAll(sTask, pTask, gTask, tTask, cTask);

                statements = sTask.Result;
                var legacyPayments = pTask.Result;
                guarantees = gTask.Result;

                var formalTrans = tTask.Result ?? new List<CashTransaction>();
                var formalChecks = cTask.Result ?? new List<CashCheck>();

                // ═══ ساخت لیست یکپارچه ═══
                unifiedPayments = new List<UnifiedPayment>();

                // ۱. پرداخت‌های رسمی جدید (حواله/نقدی)
                foreach (var t in formalTrans.Where(x => x.Direction == DirectionTypes.Outgoing))
                {
                    unifiedPayments.Add(new UnifiedPayment
                    {
                        Id = t.Id,
                        Source = "حواله/نقدی رسمی",
                        Date = t.TransactionDate,
                        Amount = t.Amount,
                        Type = t.TransactionType,
                        CostType = string.IsNullOrWhiteSpace(t.CostType) ? "---" : t.CostType,
                        Description = t.Description ?? ""
                    });
                }

                // ۲. چک‌های پرداختی
                foreach (var c in formalChecks.Where(x => x.CheckType == DirectionTypes.Outgoing && x.Status != "باطل شده" && x.Status != "برگشتی"))
                {
                    unifiedPayments.Add(new UnifiedPayment
                    {
                        Id = c.Id,
                        Source = "چک پرداختی",
                        Date = c.DueDate,
                        Amount = c.Amount,
                        Type = "چک",
                        CostType = string.IsNullOrWhiteSpace(c.CostType) ? "---" : c.CostType,
                        Description = c.Description ?? ""
                    });
                }

                // ۳. رسیدهای دستی قدیمی (اگر هنوز وجود دارند)
                foreach (var p in legacyPayments)
                {
                    unifiedPayments.Add(new UnifiedPayment
                    {
                        Id = p.Id,
                        Source = "رسید دستی قدیمی",
                        Date = p.PaymentDate,
                        Amount = p.Amount,
                        Type = p.PaymentType,
                        CostType = string.IsNullOrWhiteSpace(p.CostType) ? "---" : p.CostType,
                        Description = p.Description ?? ""
                    });
                }

                unifiedPayments = unifiedPayments.OrderByDescending(x => x.Date).ToList();
            }, "در حال بارگذاری گزارش...");

            if (!success || statements is null || unifiedPayments is null || guarantees is null || IsDisposed) return;

            _allPayments = unifiedPayments;

            PopulateStatementsGrid(statements);
            PopulatePaymentsGrid(unifiedPayments);
            PopulateGuaranteesGrid(guarantees);
            UpdateSummary(statements, unifiedPayments);

            UpdateStatementButtons();
            UpdateGuaranteeButtons();
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractorStatementReportForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری گزارش:\n" + ex.Message);
        }
    }

    private void PopulateStatementsGrid(List<ContractorStatement> statements)
    {
        if (IsDisposed) return;

        dgvStatements.Rows.Clear();
        decimal cumulative = 0;

        foreach (var st in statements)
        {
            cumulative += st.GrossAmount;
            dgvStatements.Rows.Add(
                st.Id, st.StatementNumber,
                NumberHelper.FormatThousands(cumulative, usePersianDigits: true),
                NumberHelper.FormatThousands(st.GrossAmount, usePersianDigits: true),
                NumberHelper.FormatThousands(st.VAT, usePersianDigits: true),
                NumberHelper.FormatThousands(st.TotalAmount, usePersianDigits: true),
                NumberHelper.FormatThousands(st.RetentionDeduction, usePersianDigits: true),
                NumberHelper.FormatThousands(st.InsuranceDeduction, usePersianDigits: true),
                NumberHelper.FormatThousands(st.OtherDeductions, usePersianDigits: true),
                NumberHelper.FormatThousands(st.AdvanceRecovery, usePersianDigits: true),
                NumberHelper.FormatThousands(st.TotalDeductions, usePersianDigits: true),
                NumberHelper.FormatThousands(st.NetPayableAmount, usePersianDigits: true)
            );
        }

        if (statements.Count > 0)
        {
            var sumGross = statements.Sum(x => x.GrossAmount);
            var sumVat = statements.Sum(x => x.VAT);
            var sumTotal = statements.Sum(x => x.TotalAmount);
            var sumRetention = statements.Sum(x => x.RetentionDeduction);
            var sumInsurance = statements.Sum(x => x.InsuranceDeduction);
            var sumOther = statements.Sum(x => x.OtherDeductions);
            var sumAdvance = statements.Sum(x => x.AdvanceRecovery);
            var sumDeductions = statements.Sum(x => x.TotalDeductions);
            var sumNet = statements.Sum(x => x.NetPayableAmount);

            int rowIndex = dgvStatements.Rows.Add("", "جمع کل",
                NumberHelper.FormatThousands(cumulative, usePersianDigits: true),
                NumberHelper.FormatThousands(sumGross, usePersianDigits: true),
                NumberHelper.FormatThousands(sumVat, usePersianDigits: true),
                NumberHelper.FormatThousands(sumTotal, usePersianDigits: true),
                NumberHelper.FormatThousands(sumRetention, usePersianDigits: true),
                NumberHelper.FormatThousands(sumInsurance, usePersianDigits: true),
                NumberHelper.FormatThousands(sumOther, usePersianDigits: true),
                NumberHelper.FormatThousands(sumAdvance, usePersianDigits: true),
                NumberHelper.FormatThousands(sumDeductions, usePersianDigits: true),
                NumberHelper.FormatThousands(sumNet, usePersianDigits: true));

            dgvStatements.Rows[rowIndex].DefaultCellStyle.BackColor = ModernPaint.Tint(ModernTheme.Primary, 8);
            dgvStatements.Rows[rowIndex].DefaultCellStyle.Font = _boldFont;
            dgvStatements.Rows[rowIndex].Tag = "Total";
        }
    }

    private void PopulatePaymentsGrid(List<UnifiedPayment> payments)
    {
        if (IsDisposed) return;

        dgvPayments.Rows.Clear();
        foreach (var p in payments)
        {
            dgvPayments.Rows.Add(
                p.Id,
                p.Source,
                DateHelper.ToPersianDate(p.Date),
                p.CostType,
                NumberHelper.FormatThousands(p.Amount, usePersianDigits: true),
                p.Type,
                p.Description
            );
        }
    }

    private void PopulateGuaranteesGrid(List<ContractorGuarantee> guarantees)
    {
        if (IsDisposed) return;

        dgvGuarantees.Rows.Clear();
        foreach (var g in guarantees)
        {
            int rowIndex = dgvGuarantees.Rows.Add(
                g.Id, g.GuaranteeNumber, g.BankName, g.GuaranteeType,
                NumberHelper.FormatThousands(g.Amount, usePersianDigits: true),
                DateHelper.ToPersianDate(g.IssueDate),
                DateHelper.ToPersianDate(g.MaturityDate), g.Status
            );

            if (g.Status == "فعال" && g.MaturityDate >= DateTime.Today)
            {
                int daysLeft = (g.MaturityDate - DateTime.Today).Days;
                if (daysLeft <= 30)
                {
                    dgvGuarantees.Rows[rowIndex].DefaultCellStyle.BackColor = ModernTheme.WarningSoft;
                    dgvGuarantees.Rows[rowIndex].DefaultCellStyle.ForeColor = ModernTheme.WarningText;
                }
            }
            else if (g.Status == "فعال" && g.MaturityDate < DateTime.Today)
            {
                dgvGuarantees.Rows[rowIndex].DefaultCellStyle.BackColor = ModernTheme.DangerSoft;
                dgvGuarantees.Rows[rowIndex].DefaultCellStyle.ForeColor = ModernTheme.DangerText;
            }
        }
    }

    private void UpdateSummary(List<ContractorStatement> statements, List<UnifiedPayment> payments)
    {
        if (IsDisposed) return;

        decimal totalContractAmount = _contract.Amount;
        decimal totalWorkDone = statements.Sum(x => x.GrossAmount);
        decimal totalNetPayable = statements.Sum(x => x.NetPayableAmount);
        decimal totalPaid = payments.Sum(x => x.Amount);

        decimal formalPayments = payments.Where(p => p.Source.Contains("رسمی") || p.Source.Contains("چک")).Sum(p => p.Amount);
        decimal legacyPayments = payments.Where(p => p.Source.Contains("دستی")).Sum(p => p.Amount);

        decimal purchaseOfGoods = payments.Where(p => p.CostType == "خرید کالا").Sum(p => p.Amount);
        decimal purchaseOfServices = payments.Where(p => p.CostType == "خرید خدمات").Sum(p => p.Amount);

        decimal remainingAmount = totalContractAmount - totalWorkDone;
        decimal progressPercent = totalContractAmount > 0 ? (totalWorkDone / totalContractAmount) * 100 : 0;
        decimal debt = totalNetPayable - totalPaid;

        lblSummaryTotalContract.SetAmount(totalContractAmount);
        lblSummaryTotalWork.SetAmount(totalWorkDone);
        lblSummaryRemaining.SetAmount(Math.Max(0, remainingAmount));

        lblSummaryProgress.Text = StringHelper.ConvertToPersianDigits(Math.Round(progressPercent).ToString("N0")) + "%";
        lblSummaryProgress.Accent = progressPercent > 100 ? ModernTheme.Danger : ModernTheme.Success;

        lblSummaryTotalPaid.SetAmount(totalPaid);
        lblSummaryDebt.SetAmount(Math.Max(0, debt));
        lblSummaryDebt.Accent = debt > 0 ? ModernTheme.Danger : ModernTheme.Success;

        lblSummaryFormalPaid.SetAmount(formalPayments);
        lblSummaryFormalPaid.SubText = "دستی: " + NumberHelper.FormatThousands(legacyPayments, usePersianDigits: true);

        lblSummaryCostTypeSplit.Text = "کالا: " + NumberHelper.FormatThousands(purchaseOfGoods, usePersianDigits: true);
        lblSummaryCostTypeSplit.SubText = "خدمات: " + NumberHelper.FormatThousands(purchaseOfServices, usePersianDigits: true);
    }

    private void UpdateStatementButtons()
    {
        if (IsDisposed) return;
        bool hasSelection = dgvStatements.CurrentRow is not null && dgvStatements.CurrentRow.Index != dgvStatements.Rows.Count - 1;
        btnEditStatement.Enabled = hasSelection;
        btnDeleteStatement.Enabled = hasSelection;
        btnPrintSingle.Enabled = hasSelection;
        btnPrintList.Enabled = dgvStatements.Rows.Count > 0;
    }

    private void UpdateGuaranteeButtons()
    {
        if (IsDisposed) return;
        bool hasSelection = dgvGuarantees.CurrentRow is not null;
        btnEditGuarantee.Enabled = hasSelection;
        btnDeleteGuarantee.Enabled = hasSelection;
    }

    private void DgvStatements_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && e.RowIndex < dgvStatements.Rows.Count - 1) OpenEditFormForSelectedRow();
    }

    private async void OpenEditFormForSelectedRow()
    {
        try
        {
            if (IsDisposed) return;
            if (dgvStatements.CurrentRow is null || dgvStatements.CurrentRow.Index == dgvStatements.Rows.Count - 1) return;
            int statementId = Convert.ToInt32(dgvStatements.CurrentRow.Cells["Id"].Value);

            ContractorStatement? statement = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorStatementRepository(_connectionString);
                statement = await Task.Run(() => repo.GetById(statementId));
            }, "در حال دریافت اطلاعات...");

            if (!success || statement is null || IsDisposed) return;

            using var editForm = new ContractorStatementEditForm(_connectionString, _contract, statement);
            if (editForm.ShowDialog(this) == DialogResult.OK && !IsDisposed) _ = LoadDataSafeAsync();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractorStatementReportForm.OpenEditFormForSelectedRow");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private async void DeleteSelectedRow(object? sender, EventArgs e)
    {
        try
        {
            if (IsDisposed) return;
            if (dgvStatements.CurrentRow is null || dgvStatements.CurrentRow.Index == dgvStatements.Rows.Count - 1) return;

            int statementId = Convert.ToInt32(dgvStatements.CurrentRow.Cells["Id"].Value);
            string stNumber = dgvStatements.CurrentRow.Cells["RowNum"].Value?.ToString() ?? "";

            if (!ModernMessageBox.AskConfirm($"آیا از حذف صورت وضعیت شماره «{stNumber}» اطمینان دارید؟", "حذف صورت وضعیت"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorStatementRepository(_connectionString);
                await Task.Run(() => repo.Delete(statementId));
            }, "در حال حذف...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("صورت وضعیت حذف شد.", isSuccess: true);
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractorStatementReportForm.DeleteSelectedRow");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private async void EditSelectedGuarantee()
    {
        try
        {
            if (IsDisposed) return;
            if (dgvGuarantees.CurrentRow is null) return;
            int guaranteeId = Convert.ToInt32(dgvGuarantees.CurrentRow.Cells["Id"].Value);

            ContractorGuarantee? guarantee = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorGuaranteeRepository(_connectionString);
                guarantee = await Task.Run(() => repo.GetById(guaranteeId));
            }, "در حال دریافت اطلاعات...");

            if (!success || guarantee is null || IsDisposed) return;

            using var form = new ContractorGuaranteeEditForm(_connectionString, _contract, guarantee);
            if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed) _ = LoadDataSafeAsync();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractorStatementReportForm.EditSelectedGuarantee");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private async void DeleteSelectedGuarantee(object? sender, EventArgs e)
    {
        try
        {
            if (IsDisposed) return;
            if (dgvGuarantees.CurrentRow is null) return;
            int guaranteeId = Convert.ToInt32(dgvGuarantees.CurrentRow.Cells["Id"].Value);
            string gNumber = dgvGuarantees.CurrentRow.Cells["GuaranteeNumber"].Value?.ToString() ?? "";

            if (!ModernMessageBox.AskConfirm($"آیا از حذف ضمانت‌نامه «{gNumber}» اطمینان دارید؟", "حذف ضمانت‌نامه"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorGuaranteeRepository(_connectionString);
                await Task.Run(() => repo.Delete(guaranteeId));
            }, "در حال حذف...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("ضمانت‌نامه حذف شد.", isSuccess: true);
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractorStatementReportForm.DeleteSelectedGuarantee");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private async Task AddPaymentViaCashTransactionAsync()
    {
        try
        {
            using var form = new CashTransactionEditForm(_connectionString);
            var result = form.ShowDialog(this);
            if (result == DialogResult.OK && !IsDisposed)
            {
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractorStatementReportForm.AddPaymentViaCashTransaction");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private void BtnExportExcel_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (dgvStatements.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("داده‌ای برای خروجی وجود ندارد.");
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            FileName = $"گزارش صورت وضعیت - {_contract.ContractNumber}.xlsx",
            Title = "ذخیره فایل اکسل"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                ExcelExportHelper.ExportDataGridViewToExcel(dgvStatements, sfd.FileName, "صورت وضعیت");
                ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ContractorStatementReportForm.ExportExcel");
                ModernMessageBox.ShowError("خطا در ذخیره فایل اکسل:\n" + ex.Message);
            }
        }
    }

    private void SetupPrintDocument()
    {
        printDoc = new PrintDocument();
        printDoc.DefaultPageSettings.Landscape = true;
        printDoc.PrintPage += PrintDoc_PrintPage;

        paymentPrintDoc = new PrintDocument();
        paymentPrintDoc.DefaultPageSettings.Landscape = false;
        paymentPrintDoc.PrintPage += PaymentPrintDoc_PrintPage;

        singleStatementPrintDoc = new PrintDocument();
        singleStatementPrintDoc.DefaultPageSettings.Landscape = false;
        singleStatementPrintDoc.PrintPage += SingleStatementPrintDoc_PrintPage;
    }

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (dgvStatements.Rows.Count == 0) { ModernMessageBox.ShowWarning("داده‌ای برای پرینت وجود ندارد."); return; }
        printRowIndex = 0;
        isPrintingHeader = true;

        using var preview = new PrintPreviewDialog
        {
            Document = printDoc,
            Width = 1100,
            Height = 800,
            WindowState = FormWindowState.Maximized,
            UseAntiAlias = true,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true
        };
        preview.ShowDialog(this);
    }

    private void BtnPrintPayment_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (dgvPayments.CurrentRow is null) return;

        _paymentDateForPrint = dgvPayments.CurrentRow.Cells["PaymentDate"].Value?.ToString() ?? "";
        _paymentAmountForPrint = NumberHelper.ParseFormatted(dgvPayments.CurrentRow.Cells["Amount"].Value?.ToString());
        _paymentTypeForPrint = dgvPayments.CurrentRow.Cells["PaymentType"].Value?.ToString() ?? "";
        _paymentDescForPrint = dgvPayments.CurrentRow.Cells["Description"].Value?.ToString() ?? "";

        using var preview = new PrintPreviewDialog
        {
            Document = paymentPrintDoc,
            Width = 800,
            Height = 900,
            WindowState = FormWindowState.Maximized,
            UseAntiAlias = true,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true
        };
        preview.ShowDialog(this);
    }

    private void BtnPrintSingleStatement_Click(object? sender, EventArgs e)
    {
        if (dgvStatements.CurrentRow is null || dgvStatements.CurrentRow.Index == dgvStatements.Rows.Count - 1)
        {
            ModernMessageBox.ShowWarning("لطفاً یک صورت وضعیت را از لیست انتخاب کنید.");
            return;
        }

        int id = Convert.ToInt32(dgvStatements.CurrentRow.Cells["Id"].Value);

        var repo = new ContractorStatementRepository(_connectionString);
        var statement = repo.GetById(id);

        if (statement is null) return;

        using (var printForm = new StatementPrintForm(_connectionString, statement, _contract, isClient: false))
        {
            printForm.ShowDialog(this);
        }
    }

    private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        Graphics g = e.Graphics!;
        float pageWidth = e.MarginBounds.Width;
        float startXBase = e.MarginBounds.Left;
        float yPos = e.MarginBounds.Top;

        float[] proportions = { 4, 10, 9, 8, 10, 10, 8, 6, 10, 10, 10 };
        float totalProp = proportions.Sum();
        float[] colWidths = proportions.Select(p => (p / totalProp) * pageWidth).ToArray();
        float tableWidth = colWidths.Sum();
        float startX = startXBase;

        var fontTitle = ModernTheme.GetFont(16, FontStyle.Bold);
        var fontHeader = ModernTheme.GetFont(10f, FontStyle.Bold);
        var fontBody = ModernTheme.GetFont(9f);
        var fontTotal = ModernTheme.GetFont(7.5f, FontStyle.Bold);

        using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
        using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
        using StringFormat rtlFormatLeft = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
        using StringFormat titleFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

        if (isPrintingHeader)
        {
            float logoWidth = 80; float logoHeight = 60;
            if (pbLogo.Image is not null)
            {
                try { g.DrawImage(pbLogo.Image, e.MarginBounds.Right - logoWidth, yPos, logoWidth, logoHeight); }
                catch (ArgumentException) { }
            }

            g.DrawString("گزارش صورت وضعیت پیمانکار", fontTitle, Brushes.Black, new RectangleF(startXBase, yPos, pageWidth, 30), titleFormat);
            yPos += 30;

            g.DrawString($"پیمانکار: {_contract.DisplayName}", fontHeader, Brushes.Black, new RectangleF(startXBase, yPos, pageWidth, 25), titleFormat);
            yPos += 60;

            float col1W = pageWidth * 0.30f;
            float col2W = pageWidth * 0.40f;
            float col3W = pageWidth * 0.30f;

            float col1X = startXBase;
            float col2X = col1X + col1W;
            float col3X = col2X + col2W;

            g.DrawString($"حسن انجام کار: {StringHelper.ConvertToPersianDigits(_contract.RetentionPercentage.ToString("0.##"))}%", fontHeader, Brushes.Black, new RectangleF(col1X, yPos, col1W, 25), rtlFormatLeft);
            g.DrawString($"تاریخ قرارداد: {DateHelper.ToPersianDate(_contract.ContractDate)}", fontHeader, Brushes.Black, new RectangleF(col2X, yPos, col2W, 25), rtlFormatCenter);
            g.DrawString($"شماره قرارداد: {_contract.ContractNumber}", fontHeader, Brushes.Black, new RectangleF(col3X, yPos, col3W, 25), rtlFormatRight);
            yPos += 25;

            g.DrawString($"ارزش افزوده: {StringHelper.ConvertToPersianDigits(_contract.VatPercentage.ToString("0.##"))}%", fontHeader, Brushes.Black, new RectangleF(col1X, yPos, col1W, 25), rtlFormatLeft);
            g.DrawString($"تاریخ پایان: {DateHelper.ToPersianDate(_contract.EndDate)}", fontHeader, Brushes.Black, new RectangleF(col2X, yPos, col2W, 25), rtlFormatCenter);
            g.DrawString($"مبلغ کل: {NumberHelper.FormatThousands(_contract.Amount, usePersianDigits: true)} ریال", fontHeader, Brushes.Black, new RectangleF(col3X, yPos, col3W, 25), rtlFormatRight);
            yPos += 25;

            g.DrawString($"بیمه: {StringHelper.ConvertToPersianDigits(_contract.InsurancePercentage.ToString("0.##"))}%", fontHeader, Brushes.Black, new RectangleF(col1X, yPos, col1W, 25), rtlFormatLeft);
            g.DrawString($"موضوع: {_contract.Subject}", fontHeader, Brushes.Black, new RectangleF(col2X, yPos, col2W, 25), rtlFormatCenter);
            g.DrawString($"پیش پرداخت: {NumberHelper.FormatThousands(_contract.TotalAdvanceAmount, usePersianDigits: true)} ریال", fontHeader, Brushes.Black, new RectangleF(col3X, yPos, col3W, 25), rtlFormatRight);
            yPos += 40;

            isPrintingHeader = false;
        }
        else
        {
            g.DrawString($"ادامه گزارش - قرارداد {_contract.ContractNumber}", fontHeader, Brushes.Black, new RectangleF(startXBase, yPos, pageWidth, 20), rtlFormatRight);
            yPos += 30;
        }

        float headerRowHeight = 35f;
        float rowHeight = 30f;

        string[] headers = { "شماره", "مبلغ انباشته", "مبلغ جاری", "ارزش افزوده", "جمع کل", "حسن انجام کار", "بیمه", "سایر", "پیش پرداخت", "جمع کسورات", "خالص" };

        float currentX = startX + tableWidth;
        g.FillRectangle(Brushes.LightGray, startX, yPos, tableWidth, headerRowHeight);

        for (int i = 0; i < headers.Length; i++)
        {
            currentX -= colWidths[i];
            RectangleF rect = new RectangleF(currentX, yPos, colWidths[i], headerRowHeight);
            g.DrawRectangle(Pens.Black, rect.X, rect.Y, rect.Width, rect.Height);
            g.DrawString(headers[i], fontHeader, Brushes.Black, rect, rtlFormatCenter);
        }
        yPos += headerRowHeight;

        while (printRowIndex < dgvStatements.Rows.Count)
        {
            if (yPos + rowHeight > e.MarginBounds.Bottom - 100) { e.HasMorePages = true; return; }

            var row = dgvStatements.Rows[printRowIndex];
            currentX = startX + tableWidth;

            for (int i = 1; i < dgvStatements.Columns.Count; i++)
            {
                currentX -= colWidths[i - 1];
                string cellValue = row.Cells[i].Value?.ToString() ?? "";
                RectangleF rect = new RectangleF(currentX, yPos, colWidths[i - 1], rowHeight);
                g.DrawRectangle(Pens.Black, rect.X, rect.Y, rect.Width, rect.Height);

                if (printRowIndex == dgvStatements.Rows.Count - 1)
                {
                    g.FillRectangle(Brushes.Beige, rect);
                    g.DrawString(cellValue, fontTotal, Brushes.Black, rect, rtlFormatCenter);
                }
                else g.DrawString(cellValue, fontBody, Brushes.Black, rect, rtlFormatCenter);
            }
            yPos += rowHeight;
            printRowIndex++;
        }

        if (printRowIndex >= dgvStatements.Rows.Count)
        {
            yPos += 40;
            float signWidth = 150f; float gap = (tableWidth - (signWidth * 3)) / 2;
            float signStartX = startX;

            g.DrawString("مهر و امضای پیمانکار", fontHeader, Brushes.Black, new RectangleF(signStartX + tableWidth - signWidth, yPos, signWidth, 20), rtlFormatCenter);
            g.DrawLine(Pens.Black, signStartX + tableWidth - signWidth, yPos + 50, signStartX + tableWidth, yPos + 50);

            g.DrawString("مهر و امضای ناظر", fontHeader, Brushes.Black, new RectangleF(signStartX + tableWidth - signWidth - gap - signWidth, yPos, signWidth, 20), rtlFormatCenter);
            g.DrawLine(Pens.Black, signStartX + tableWidth - signWidth - gap - signWidth, yPos + 50, signStartX + tableWidth - gap - signWidth, yPos + 50);

            g.DrawString("مهر و امضای کارفرما", fontHeader, Brushes.Black, new RectangleF(signStartX, yPos, signWidth, 20), rtlFormatCenter);
            g.DrawLine(Pens.Black, signStartX, yPos + 50, signStartX + signWidth, yPos + 50);
        }
        e.HasMorePages = false;
    }

    private void PaymentPrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        Graphics g = e.Graphics!;
        // ✅ جبران حاشیه‌ی سخت‌افزاری چاپگر (فقط در چاپ واقعی؛ در پیش‌نمایش لازم نیست)
        if (!((sender as PrintDocument)?.PrintController?.IsPreview ?? false))
            g.TranslateTransform(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);
        float pageWidth = e.PageBounds.Width;
        float pageHeight = e.PageBounds.Height;
        float margin = 50f;
        float yPos = margin;

        var fontTitle = ModernTheme.GetFont(18, FontStyle.Bold);
        var fontHeader = ModernTheme.GetFont(12, FontStyle.Bold);
        var fontBody = ModernTheme.GetFont(11f);

        using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
        using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };

        g.DrawRectangle(Pens.Black, margin, margin, pageWidth - margin * 2, pageHeight - margin * 2);

        if (pbLogo.Image is not null)
        {
            try { g.DrawImage(pbLogo.Image, pageWidth - margin - 110, yPos + 10, 100, 80); }
            catch (ArgumentException) { }
        }

        g.DrawString("رسید پرداخت وجه", fontTitle, Brushes.Black, new RectangleF(margin, yPos + 30, pageWidth - margin * 2, 40), rtlFormatCenter);
        g.DrawLine(Pens.Black, margin, yPos + 90, pageWidth - margin, yPos + 90);
        yPos += 110;

        g.DrawString($"تاریخ: {_paymentDateForPrint}", fontHeader, Brushes.Black, new RectangleF(margin + 20, yPos, 200, 25), rtlFormatRight);
        g.DrawString($"شماره قرارداد: {_contract.ContractNumber}", fontHeader, Brushes.Black, new RectangleF(pageWidth / 2, yPos, pageWidth / 2 - margin - 20, 25), rtlFormatRight);
        yPos += 40;

        g.DrawString($"اینجانب / شرکت: {_contract.DisplayName}", fontBody, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 35;

        g.DrawString($"مبلغ: {NumberHelper.FormatThousands(_paymentAmountForPrint, usePersianDigits: true)} ریال", fontBody, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 35;

        g.DrawString($"به حروف: {NumberHelper.ToPersianWords(_paymentAmountForPrint)} ریال", fontHeader, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 35;

        g.DrawString($"بابت: پرداخت بابت قرارداد فوق ({_paymentTypeForPrint}) {_paymentDescForPrint}", fontBody, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 35;

        g.DrawString("در وجه / به حساب پیمانکار مربوطه پرداخت گردید.", fontBody, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 60;

        float signY = pageHeight - margin - 100;
        float signWidth = 150f;

        g.DrawString("مهر و امضای پرداخت‌کننده", fontHeader, Brushes.Black, new RectangleF(pageWidth - margin - signWidth - 20, signY, signWidth, 20), rtlFormatCenter);
        g.DrawLine(Pens.Black, pageWidth - margin - signWidth - 20, signY + 60, pageWidth - margin - 20, signY + 60);

        g.DrawString("مهر و امضای دریافت‌کننده", fontHeader, Brushes.Black, new RectangleF(margin + 20, signY, signWidth, 20), rtlFormatCenter);
        g.DrawLine(Pens.Black, margin + 20, signY + 60, margin + 20 + signWidth, signY + 60);
    }

    private void SingleStatementPrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        Graphics g = e.Graphics!;
        // ✅ جبران حاشیه‌ی سخت‌افزاری چاپگر (فقط در چاپ واقعی؛ در پیش‌نمایش لازم نیست)
        if (!((sender as PrintDocument)?.PrintController?.IsPreview ?? false))
            g.TranslateTransform(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);
        float pageWidth = e.PageBounds.Width;
        float pageHeight = e.PageBounds.Height;
        float margin = 50f;
        float yPos = margin;

        var fontTitle = ModernTheme.GetFont(18, FontStyle.Bold);
        var fontHeader = ModernTheme.GetFont(12, FontStyle.Bold);
        var fontBody = ModernTheme.GetFont(11f);

        using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
        using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };

        g.DrawRectangle(Pens.Black, margin, margin, pageWidth - margin * 2, pageHeight - margin * 2);

        if (pbLogo.Image is not null)
        {
            try { g.DrawImage(pbLogo.Image, pageWidth - margin - 110, yPos + 10, 100, 80); }
            catch (ArgumentException) { }
        }

        g.DrawString($"صورت وضعیت شماره {_sglStatementNumber}", fontTitle, Brushes.Black, new RectangleF(margin, yPos + 30, pageWidth - margin * 2, 40), rtlFormatCenter);
        g.DrawLine(Pens.Black, margin, yPos + 90, pageWidth - margin, yPos + 90);
        yPos += 110;

        g.DrawString($"پیمانکار: {_contract.DisplayName}", fontHeader, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 30;
        g.DrawString($"شماره قرارداد: {_contract.ContractNumber}", fontHeader, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 30;
        g.DrawString($"موضوع قرارداد: {_contract.Subject}", fontHeader, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 40;

        float tableWidth = pageWidth - margin * 2 - 40;
        float startX = margin + 20;
        float rowHeight = 30f;

        g.FillRectangle(Brushes.LightGray, startX, yPos, tableWidth, rowHeight);
        g.DrawRectangle(Pens.Black, startX, yPos, tableWidth, rowHeight);
        g.DrawString("شرح", fontHeader, Brushes.Black, new RectangleF(startX, yPos, tableWidth * 0.6f, rowHeight), rtlFormatCenter);
        g.DrawString("مبلغ (ریال)", fontHeader, Brushes.Black, new RectangleF(startX + tableWidth * 0.6f, yPos, tableWidth * 0.4f, rowHeight), rtlFormatCenter);
        yPos += rowHeight;

        void DrawRow(string title, decimal amount, bool isBold = false)
        {
            Font currentFont = isBold ? fontHeader : fontBody;
            g.DrawRectangle(Pens.Black, startX, yPos, tableWidth, rowHeight);
            g.DrawString(title, currentFont, Brushes.Black, new RectangleF(startX, yPos, tableWidth * 0.6f, rowHeight), rtlFormatRight);
            g.DrawString(NumberHelper.FormatThousands(amount, usePersianDigits: true), currentFont, Brushes.Black, new RectangleF(startX + tableWidth * 0.6f, yPos, tableWidth * 0.4f, rowHeight), rtlFormatCenter);
            yPos += rowHeight;
        }

        DrawRow("مبلغ کارکرد (ناخالص)", _sglGross);
        DrawRow("ارزش افزوده", _sglVat);
        DrawRow("جمع کل", _sglTotal, true);
        DrawRow("کسر حسن انجام کار", _sglRetention);
        DrawRow("کسر بیمه", _sglInsurance);
        DrawRow("کسر پیش‌پرداخت", _sglAdvance);
        DrawRow("مبلغ خالص قابل پرداخت", _sglNet, true);

        yPos += 30;

        g.DrawString($"مبلغ خالص به حروف: {NumberHelper.ToPersianWords(_sglNet)} ریال", fontHeader, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 60;

        float signY = pageHeight - margin - 100;
        float signWidth = 150f;

        g.DrawString("مهر و امضای پیمانکار", fontHeader, Brushes.Black, new RectangleF(pageWidth - margin - signWidth - 20, signY, signWidth, 20), rtlFormatCenter);
        g.DrawLine(Pens.Black, pageWidth - margin - signWidth - 20, signY + 60, pageWidth - margin - 20, signY + 60);

        g.DrawString("مهر و امضای ناظر", fontHeader, Brushes.Black, new RectangleF(pageWidth / 2 - signWidth / 2, signY, signWidth, 20), rtlFormatCenter);
        g.DrawLine(Pens.Black, pageWidth / 2 - signWidth / 2, signY + 60, pageWidth / 2 + signWidth / 2, signY + 60);

        g.DrawString("مهر و امضای کارفرما", fontHeader, Brushes.Black, new RectangleF(margin + 20, signY, signWidth, 20), rtlFormatCenter);
        g.DrawLine(Pens.Black, margin + 20, signY + 60, margin + 20 + signWidth, signY + 60);
    }
}
