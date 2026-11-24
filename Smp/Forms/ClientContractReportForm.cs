using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Smp.Models;
using Smp.Helpers;
using Smp.Data;

namespace Smp.Forms;

public class ClientContractReportForm : Form
{
    private readonly string _connectionString;
    private readonly ClientContract _contract;
    private readonly CashTransactionRepository _cashRepo;
    private readonly CashCheckRepository _checkRepo;

    private ModernTabHost tabHost = null!;
    private DataGridView dgvStatements = null!;
    private DataGridView dgvPayments = null!;
    private DataGridView dgvContractorContracts = null!;
    private DataGridView dgvExpenses = null!;
    private DataGridView dgvAllProjectPayments = null!;

    private ClientChartPanel chartPanel = null!;

    private ModernStatCard lblSummaryTotalContract = null!, lblSummaryTotalWork = null!, lblSummaryRemaining = null!;
    private ModernStatCard lblSummaryTotalPaid = null!, lblSummaryDebt = null!;
    private ModernStatCard lblSummaryContractorTotal = null!, lblSummaryProfit = null!, lblSummaryExpenses = null!;
    private ModernStatCard lblSummaryTotalAdvance = null!, lblSummaryRecoveredAdvance = null!, lblSummaryRemainingAdvance = null!;
    private ModernStatCard lblSummaryProjectDirect = null!, lblSummaryInformalContractor = null!;

    private ModernStatCard lblSummaryBilled = null!, lblSummaryUnbilled = null!;
    private static readonly Font _boldFont = ModernTheme.GetFont(11f, FontStyle.Bold);
    private static readonly Color _amber = ModernTheme.Warning;
    private decimal _totalBilledExpenses = 0, _totalUnbilledExpenses = 0;

    private ModernProgressBar progressBar = null!;
    private Label lblInfoStatements = null!, lblInfoPayments = null!, lblInfoContractors = null!, lblInfoExpenses = null!, lblInfoAllPayments = null!;
    private TableLayoutPanel? _headerLayout;

    private PictureBox picCompanyLogo = null!;
    private Image? _companyLogo = null;

    private PrintDocument singleStatementPrintDoc = null!;
    private int _printRowIndex = 0;

    private PrintDocument paymentPrintDoc = null!;
    private decimal _paymentAmountForPrint = 0;
    private string _paymentTypeForPrint = "";
    private string _paymentDateForPrint = "";
    private string _paymentDescForPrint = "";

    private List<UnifiedPaymentViewModel> _allPaymentsList = new();
    private List<CashTransaction> _projectCashTransactions = new();

    public ClientContractReportForm(string connectionString, ClientContract contract)
    {
        _connectionString = connectionString;
        _contract = contract;
        _cashRepo = new CashTransactionRepository(connectionString);
        _checkRepo = new CashCheckRepository(connectionString);

        BuildUI();
        LoadCompanyLogo();
        SetupPrintDocument();

        ShortcutHelper.ApplyStandardShortcuts(this);
        Shown += async (s, e) => await LoadDataSafeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { singleStatementPrintDoc?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientContractReportForm"); }
            try { paymentPrintDoc?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientContractReportForm"); }
            try { _companyLogo?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientContractReportForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // فرم قابل تغییر اندازه و Maximize (قبلاً FixedDialog با سقف ۱۶۰۰×۹۵۰ بود) و متناسب با صفحه‌نمایش
        ModernForm.Setup(this, $"گزارش مالی کارفرما - {_contract.ContractNumber}", new Size(1450, 880), isDialog: false);
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(Math.Min(1450, workArea.Width - 40), Math.Min(880, workArea.Height - 40));
        MinimumSize = new Size(Math.Min(1180, Size.Width), Math.Min(720, Size.Height));

        var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));   // کارت اطلاعات قرارداد
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));    // شاخص‌های اصلی
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));   // شاخص‌های تکمیلی (گروه‌بندی‌شده)
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));    // تب‌ها

        // ==========================================
        // ۱. کارت اطلاعات قرارداد: [لوگو] [نام کارفرما + موضوع + نوار پیشرفت] [شماره و تاریخ‌ها]
        // ==========================================
        var headerCard = new ModernCard("", ModernTheme.Primary, new[] { new ColumnStyle(SizeType.Percent, 100F) })
        {
            Margin = new Padding(16, 10, 16, 4)
        };

        _headerLayout = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = Padding.Empty };
        _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8F));     // لوگو؛ با بارگذاری لوگو ۸۴ می‌شود
        _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 440F));
        _headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        picCompanyLogo = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 2, 12, 2),
            BackColor = Color.Transparent
        };

        var titleBlock = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = Padding.Empty };
        titleBlock.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var lblClient = ModernUI.Caption(OrDash(_contract.DisplayName), 15f, true, ModernTheme.TextStrong);
        lblClient.Dock = DockStyle.Fill; lblClient.Margin = Padding.Empty;
        var lblSubject = ModernUI.Caption("موضوع: " + OrDash(_contract.Subject), 10.5f, false, ModernTheme.TextMuted);
        lblSubject.Dock = DockStyle.Fill; lblSubject.Margin = Padding.Empty;

        progressBar = new ModernProgressBar { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0), Caption = "پیشرفت کارکرد: ۰٪" };
        var progressRow = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = Padding.Empty };
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        progressRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        progressRow.Controls.Add(progressBar, 0, 0);

        titleBlock.Controls.Add(lblClient, 0, 0);
        titleBlock.Controls.Add(lblSubject, 0, 1);
        titleBlock.Controls.Add(progressRow, 0, 2);

        var facts = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = Padding.Empty };
        for (int i = 0; i < 3; i++) facts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        facts.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        facts.Controls.Add(MakeFact("شماره قرارداد", OrDash(StringHelper.ConvertToPersianDigits(_contract.ContractNumber ?? "")) ), 0, 0);
        facts.Controls.Add(MakeFact("تاریخ قرارداد", DateOrDash(_contract.ContractDate)), 1, 0);
        facts.Controls.Add(MakeFact("تاریخ پایان", DateOrDash(_contract.EndDate)), 2, 0);

        _headerLayout.Controls.Add(picCompanyLogo, 0, 0);
        _headerLayout.Controls.Add(titleBlock, 1, 0);
        _headerLayout.Controls.Add(facts, 2, 0);
        headerCard.Body.Controls.Add(_headerLayout, 0, 0);

        // ==========================================
        // ۲. شاخص‌های اصلی (۶ کارت درشت)
        // ==========================================
        var heroStrip = new StatStrip(6, 84) { Margin = new Padding(10, 4, 10, 4) };
        lblSummaryTotalContract = heroStrip.Add("مبلغ قرارداد", ModernTheme.Primary, "ریال");
        lblSummaryTotalWork = heroStrip.Add("کارکرد", ModernTheme.Teal, "ریال");
        lblSummaryRemaining = heroStrip.Add("مانده قرارداد", ModernTheme.Orange, "ریال");
        lblSummaryTotalPaid = heroStrip.Add("دریافتی", ModernTheme.Success, "ریال");
        lblSummaryDebt = heroStrip.Add("مانده طلب", ModernTheme.Danger, "ریال");
        lblSummaryProfit = heroStrip.Add("سود ناخالص", ModernTheme.Purple, "ریال");
        foreach (var c in new[] { lblSummaryTotalContract, lblSummaryTotalWork, lblSummaryRemaining, lblSummaryTotalPaid, lblSummaryDebt, lblSummaryProfit })
        {
            c.ValueFontSize = 15f;
            c.TitleFontSize = 9.5f;
            c.DimZero = true;
        }

        // ==========================================
        // ۳. شاخص‌های تکمیلی در سه گروه: پیش‌دریافت | هزینه‌ها | هزینه‌ها در صورت وضعیت
        // ==========================================
        var stripAdvance = new StatStrip(3, 64);
        lblSummaryTotalAdvance = stripAdvance.Add("کل پیش‌دریافت", ModernTheme.Primary);
        lblSummaryRecoveredAdvance = stripAdvance.Add("کسرشده", ModernTheme.Success);
        lblSummaryRemainingAdvance = stripAdvance.Add("مانده", ModernTheme.Danger);

        var stripCosts = new StatStrip(4, 64);
        lblSummaryProjectDirect = stripCosts.Add("خرید مستقیم", ModernTheme.Orange);
        lblSummaryInformalContractor = stripCosts.Add("پیمانکار بدون قرارداد", _amber);
        lblSummaryContractorTotal = stripCosts.Add("پیمانکار رسمی", ModernTheme.Purple);
        lblSummaryExpenses = stripCosts.Add("هزینه فاکتوری", ModernTheme.Danger);

        var stripBilling = new StatStrip(2, 64);
        lblSummaryBilled = stripBilling.Add("منظور شده", ModernTheme.Success);
        lblSummaryUnbilled = stripBilling.Add("منظور نشده", _amber);

        foreach (var c in new[] { lblSummaryTotalAdvance, lblSummaryRecoveredAdvance, lblSummaryRemainingAdvance,
                                  lblSummaryProjectDirect, lblSummaryInformalContractor, lblSummaryContractorTotal, lblSummaryExpenses,
                                  lblSummaryBilled, lblSummaryUnbilled })
        {
            c.ValueFontSize = 10.5f;
            c.DimZero = true;
        }

        var groups = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = new Padding(10, 0, 10, 0) };
        groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29F));
        groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47F));
        groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
        groups.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        groups.Controls.Add(MakeStatGroup("پیش‌دریافت", stripAdvance), 0, 0);
        groups.Controls.Add(MakeStatGroup("هزینه‌های پروژه", stripCosts), 1, 0);
        groups.Controls.Add(MakeStatGroup("هزینه‌ها در صورت وضعیت", stripBilling), 2, 0);

        // ==========================================
        // ۴. تب‌ها (ModernTabHost مثل بقیه‌ی فرم‌های برنامه)
        // ==========================================
        tabHost = new ModernTabHost { Dock = DockStyle.Fill, AccentColor = ModernTheme.Primary };
        var pgStatements = tabHost.AddPage("📄 صورت وضعیت‌های صادره");
        var pgPayments = tabHost.AddPage("💳 دریافتی‌ها");
        var pgContractors = tabHost.AddPage("📝 قراردادهای پیمانکاران");
        var pgExpenses = tabHost.AddPage("🧾 هزینه‌های فاکتوری");
        var pgAllPayments = tabHost.AddPage("📊 همه پرداخت‌های پروژه");
        var pgChart = tabHost.AddPage("📈 نمودار مالی");

        lblInfoStatements = CreateInfoLabel();
        lblInfoPayments = CreateInfoLabel();
        lblInfoContractors = CreateInfoLabel();
        lblInfoExpenses = CreateInfoLabel();
        lblInfoAllPayments = CreateInfoLabel();

        // ═══════════════════════════════════════════
        // تب ۱: صورت وضعیت‌ها
        // ═══════════════════════════════════════════
        dgvStatements = CreateGrid();
        dgvStatements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        dgvStatements.Columns.Add("RowNum", "شماره");
        dgvStatements.Columns.Add("GrossAmount", "مبلغ کارکرد");
        dgvStatements.Columns.Add("FeeAmount", "حق‌الزحمه");
        dgvStatements.Columns.Add("VAT", "ارزش افزوده");
        dgvStatements.Columns.Add("TotalAmount", "جمع کل");
        dgvStatements.Columns.Add("RetentionDeduction", "کسر حسن انجام کار");
        dgvStatements.Columns.Add("InsuranceDeduction", "کسر بیمه");
        dgvStatements.Columns.Add("AdvanceRecovery", "کسر پیش‌دریافت");
        dgvStatements.Columns.Add("OtherDeductions", "سایر کسورات");
        dgvStatements.Columns.Add("Deductions", "جمع کسورات");
        dgvStatements.Columns.Add("NetReceivableAmount", "خالص دریافتی");

        var btnAddSt = ModernButton.Primary("➕ ثبت صورت وضعیت", 170, 34);
        btnAddSt.Click += (s, e) => { using var f = new ClientStatementEditForm(_connectionString, _contract); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };
        var btnEditSt = ModernButton.Soft("✏️ ویرایش", ModernTheme.Primary, 110, 34);
        btnEditSt.Click += EditSelectedStatement;
        var btnDelSt = ModernButton.Soft("🗑️ حذف", ModernTheme.Danger, 100, 34);
        btnDelSt.Click += DeleteSelectedStatement;
        var btnPrintSt = ModernButton.Outline("🖨️ چاپ لیست", ModernTheme.Neutral, 130, 34);
        btnPrintSt.Click += BtnPrintSingleStatement_Click;
        var btnExcelSt = CreateMenuButton("📗 اکسل", ModernTheme.Teal,
            ("📥 دانلود قالب اکسل", new EventHandler(BtnDownloadStTemplate_Click)),
            ("📤 ایمپورت از اکسل", new EventHandler(BtnImportStExcel_Click)));

        BuildTabPage(pgStatements, dgvStatements, lblInfoStatements,
            "هنوز صورت وضعیتی ثبت نشده است", "با «ثبت صورت وضعیت» یا «اکسل ← ایمپورت» اولین مورد را اضافه کنید.",
            btnAddSt, btnEditSt, btnDelSt, btnPrintSt, btnExcelSt);
        BindRowActions(dgvStatements, btnEditSt, btnDelSt);
        dgvStatements.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditSelectedStatement(s, e); };

        // ═══════════════════════════════════════════
        // تب ۲: دریافتی‌ها
        // ═══════════════════════════════════════════
        dgvPayments = CreateGrid();
        dgvPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        dgvPayments.Columns.Add("PaymentDate", "تاریخ دریافت");
        dgvPayments.Columns.Add("Amount", "مبلغ دریافتی");
        dgvPayments.Columns.Add("PaymentType", "روش دریافت");
        dgvPayments.Columns.Add("CostType", "ماهیت هزینه");
        dgvPayments.Columns.Add("Description", "توضیحات");
        dgvPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "TableName", Visible = false });

        var btnAddPay = ModernButton.Success("➕ ثبت دریافتی جدید", 170, 34);
        btnAddPay.Click += (s, e) => { using var f = new ClientPaymentForm(_connectionString, _contract); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };
        var btnDeletePay = ModernButton.Soft("🗑️ حذف", ModernTheme.Danger, 100, 34);
        btnDeletePay.Click += BtnDeletePay_Click;
        var btnPrintPay = ModernButton.Outline("🖨️ چاپ رسید دریافت", ModernTheme.Neutral, 170, 34);
        btnPrintPay.Click += BtnPrintPayment_Click;
        var btnExcelPay = CreateMenuButton("📗 اکسل", ModernTheme.Teal,
            ("📥 دانلود قالب اکسل دریافتی‌ها", new EventHandler(BtnDownloadPayTemplate_Click)),
            ("📤 ایمپورت دریافتی‌ها", new EventHandler(BtnImportPayExcel_Click)));

        BuildTabPage(pgPayments, dgvPayments, lblInfoPayments,
            "هنوز دریافتی‌ای ثبت نشده است", "وجه‌های دریافتی از کارفرما را با «ثبت دریافتی جدید» وارد کنید.",
            btnAddPay, btnDeletePay, btnPrintPay, btnExcelPay);
        BindRowActions(dgvPayments, btnDeletePay, btnPrintPay);

        // ═══════════════════════════════════════════
        // تب ۳: قراردادهای پیمانکاران
        // ═══════════════════════════════════════════
        dgvContractorContracts = CreateGrid();
        dgvContractorContracts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        dgvContractorContracts.Columns.Add("ContractNumber", "شماره قرارداد");
        dgvContractorContracts.Columns.Add("DisplayName", "نام پیمانکار");
        dgvContractorContracts.Columns.Add("Subject", "موضوع");
        dgvContractorContracts.Columns.Add("Amount", "مبلغ قرارداد");
        dgvContractorContracts.Columns.Add("ContractDate", "تاریخ قرارداد");
        dgvContractorContracts.Columns.Add("EndDate", "تاریخ پایان");

        var btnAddContractor = ModernButton.Primary("➕ قرارداد پیمانکار جدید", 200, 34);
        btnAddContractor.Click += (s, e) => { using var f = new ContractorContractEditForm(_connectionString); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };
        var btnOpenContractorReport = ModernButton.Outline("📊 گزارش پیمانکار", ModernTheme.Primary, 170, 34);
        btnOpenContractorReport.Click += async (s, e) => await OpenSelectedContractorReportAsync();

        BuildTabPage(pgContractors, dgvContractorContracts, lblInfoContractors,
            "برای این پروژه قرارداد پیمانکاری ثبت نشده است", "با «قرارداد پیمانکار جدید» شروع کنید.",
            btnAddContractor, btnOpenContractorReport);
        BindRowActions(dgvContractorContracts, btnOpenContractorReport);
        dgvContractorContracts.CellDoubleClick += async (s, e) => { if (e.RowIndex >= 0) await OpenSelectedContractorReportAsync(); };

        // ═══════════════════════════════════════════
        // تب ۴: هزینه‌های فاکتوری
        // ═══════════════════════════════════════════
        dgvExpenses = CreateGrid();
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        dgvExpenses.Columns.Add("InvoiceDate", "تاریخ فاکتور");
        dgvExpenses.Columns.Add("InvoiceNumber", "شماره فاکتور");
        dgvExpenses.Columns.Add("PartyName", "طرف حساب");
        dgvExpenses.Columns.Add("CostType", "ماهیت هزینه");
        dgvExpenses.Columns.Add("Amount", "مبلغ فاکتور");
        dgvExpenses.Columns.Add("Description", "توضیحات");
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "BillingStatus", HeaderText = "وضعیت صورت وضعیت", FillWeight = 15 });

        var btnAddExp = ModernButton.Primary("➕ ثبت فاکتور هزینه", 170, 34);
        btnAddExp.Click += (s, e) => { using var f = new ProjectExpenseEditForm(_connectionString, _contract); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };
        var btnDelExp = ModernButton.Soft("🗑️ حذف", ModernTheme.Danger, 100, 34);
        btnDelExp.Click += DeleteSelectedExpense;

        BuildTabPage(pgExpenses, dgvExpenses, lblInfoExpenses,
            "هنوز فاکتور هزینه‌ای ثبت نشده است", "فاکتورهای خرید و هزینه‌ی این پروژه را با «ثبت فاکتور هزینه» وارد کنید.",
            btnAddExp, btnDelExp);
        BindRowActions(dgvExpenses, btnDelExp);

        // ═══════════════════════════════════════════
        // تب ۵: همه پرداخت‌های پروژه
        // ═══════════════════════════════════════════
        dgvAllProjectPayments = CreateGrid();
        dgvAllProjectPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        dgvAllProjectPayments.Columns.Add("Date", "تاریخ");
        dgvAllProjectPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "PaymentMethod", HeaderText = "روش پرداخت", FillWeight = 15 });
        dgvAllProjectPayments.Columns.Add("Scope", "نوع پرداخت");
        dgvAllProjectPayments.Columns.Add("TafsiliTitle", "طرف حساب");
        dgvAllProjectPayments.Columns.Add("CostType", "ماهیت هزینه");
        dgvAllProjectPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "BillingStatus", HeaderText = "وضعیت صورت وضعیت", FillWeight = 15 });
        dgvAllProjectPayments.Columns.Add("Amount", "مبلغ");
        dgvAllProjectPayments.Columns.Add("Description", "توضیحات");
        dgvAllProjectPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "RawAmount", Visible = false });

        dgvAllProjectPayments.CellDoubleClick += DgvAllProjectPayments_CellDoubleClick;

        var btnAddProjectPay = ModernButton.Success("➕ ثبت پرداخت جدید پروژه", 200, 34);
        btnAddProjectPay.Click += (s, e) => { using var f = new CashTransactionEditForm(_connectionString); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };

        var btnPrintAllPay = ModernButton.Outline("🖨️ چاپ گزارش پرداخت‌ها", ModernTheme.Neutral, 200, 34);
        btnPrintAllPay.Click += (s, e) =>
        {
            string summaryText = $"جمع هزینه‌های صورت وضعیت شده: {NumberHelper.FormatThousands(_totalBilledExpenses, usePersianDigits: true)} ریال  |  جمع هزینه‌های صورت وضعیت نشده: {NumberHelper.FormatThousands(_totalUnbilledExpenses, usePersianDigits: true)} ریال";

            PrintHelper.PrintDataGridView(
                dgvAllProjectPayments,
                $"گزارش پرداخت‌های پروژه {_contract.ContractNumber}",
                $"موضوع: {_contract.Subject}",
                summaryText,
                _companyLogo);
        };

        var btnExportAllPay = ModernButton.Soft("📥 خروجی اکسل", ModernTheme.Teal, 150, 34);
        btnExportAllPay.Click += (s, e) => ExportAllProjectPaymentsToExcel();

        BuildTabPage(pgAllPayments, dgvAllProjectPayments, lblInfoAllPayments,
            "هنوز پرداختی برای این پروژه ثبت نشده است", "خرید مستقیم و پرداخت به پیمانکاران اینجا جمع می‌شود. برای ویرایش، روی ردیف دوبار کلیک کنید.",
            btnAddProjectPay, btnPrintAllPay, btnExportAllPay);

        // ═══════════════════════════════════════════
        // تب ۶: نمودار مالی
        // ═══════════════════════════════════════════
        chartPanel = new ClientChartPanel { Dock = DockStyle.Fill };
        var chartHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = ModernTheme.Border };
        chartHost.Controls.Add(chartPanel);
        pgChart.Padding = new Padding(16, 12, 16, 12);
        pgChart.Controls.Add(chartHost);

        // متن‌های طولانی راست‌چین، مبالغ پررنگ
        RightAlign(dgvPayments, "Description");
        RightAlign(dgvContractorContracts, "DisplayName", "Subject");
        RightAlign(dgvExpenses, "PartyName", "Description");
        RightAlign(dgvAllProjectPayments, "TafsiliTitle", "Description");
        BoldColumns(dgvStatements, "NetReceivableAmount");
        BoldColumns(dgvPayments, "Amount");
        BoldColumns(dgvContractorContracts, "Amount");
        BoldColumns(dgvExpenses, "Amount");
        BoldColumns(dgvAllProjectPayments, "Amount");

        // ApplyModernStyle هدر ستون‌ها را از دیکشنری ترجمه بازنویسی می‌کند (مثلاً «کسر پیش‌پرداخت»)؛
        // اینجا نام‌ها با عنوان کارت‌های خلاصه یکدست می‌شوند
        dgvStatements.TranslateHeaders(new Dictionary<string, string>
        {
            ["RowNum"] = "شماره",
            ["GrossAmount"] = "مبلغ کارکرد",
            ["FeeAmount"] = "حق‌الزحمه",
            ["VAT"] = "ارزش افزوده",
            ["TotalAmount"] = "جمع کل",
            ["RetentionDeduction"] = "کسر حسن انجام کار",
            ["InsuranceDeduction"] = "کسر بیمه",
            ["AdvanceRecovery"] = "کسر پیش‌دریافت",
            ["OtherDeductions"] = "سایر کسورات",
            ["Deductions"] = "جمع کسورات",
            ["NetReceivableAmount"] = "خالص دریافتی"
        });

        // مبلغ‌های صفر کم‌رنگ می‌شوند تا ستون‌های پُر در جدول صورت وضعیت دیده شوند
        dgvStatements.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.CellStyle is null) return;
            string col = dgvStatements.Columns[e.ColumnIndex].Name;
            if (col == "Id" || col == "RowNum") return;
            if (e.Value is string text && IsZeroText(text)) e.CellStyle.ForeColor = ModernTheme.TextDisabled;
        };

        // وضعیت صورت وضعیت به‌صورت قرص رنگی (مقدارهای دیگر خاکستری)
        var billingColors = new Dictionary<string, Color>
        {
            ["🔒 صورت وضعیت شده"] = ModernTheme.Success,
            ["⏳ در انتظار صورت وضعیت"] = _amber
        };
        dgvExpenses.UseBadges("BillingStatus", billingColors);
        dgvAllProjectPayments.UseBadges("BillingStatus", billingColors);

        ModernizeGridRows();

        mainLayout.Controls.Add(headerCard, 0, 0);
        mainLayout.Controls.Add(heroStrip, 0, 1);
        mainLayout.Controls.Add(groups, 0, 2);
        mainLayout.Controls.Add(tabHost, 0, 3);

        Controls.Add(mainLayout);
    }

    // ───────────────────────── کمک‌متدهای ساخت رابط ─────────────────────────

    /// <summary>محتوای جدول‌ها: بج رنگی، مبلغ‌های رنگی/پررنگ و تاریخ‌های کم‌رنگ</summary>
    private void ModernizeGridRows()
    {
        var none = new Dictionary<string, Color>();

        // ── صورت وضعیت: شماره به‌صورت قرص، کسورات قرمز ملایم، خالص سبز ──
        dgvStatements.UseBadges("RowNum", none, ModernTheme.Primary);
        ColorColumns(dgvStatements, PaintDeduction, "RetentionDeduction", "InsuranceDeduction", "AdvanceRecovery", "OtherDeductions", "Deductions");
        ColorColumns(dgvStatements, ModernPaint.Darken(ModernTheme.Success, 70), "NetReceivableAmount");
        ColorColumns(dgvStatements, ModernTheme.TextStrong, "TotalAmount");

        // ── دریافتی‌ها: روش دریافت و ماهیت هزینه قرص؛ مبلغ سبز؛ تاریخ کم‌رنگ ──
        var methodColors = new Dictionary<string, Color>
        {
            ["نقدی"] = ModernTheme.Success,
            ["حواله"] = ModernTheme.Primary,
            ["چک دریافتی"] = ModernTheme.Purple
        };
        dgvPayments.UseBadges("PaymentType", methodColors, ModernTheme.Teal);
        dgvPayments.UseBadges("CostType", none, ModernTheme.Neutral);
        ColorColumns(dgvPayments, ModernPaint.Darken(ModernTheme.Success, 70), "Amount");
        ColorColumns(dgvPayments, ModernTheme.TextMuted, "PaymentDate");

        // ── قراردادهای پیمانکاران ──
        dgvContractorContracts.UseBadges("ContractNumber", none, ModernTheme.Purple);
        ColorColumns(dgvContractorContracts, ModernTheme.TextMuted, "ContractDate", "EndDate");

        // ── فاکتورها ──
        dgvExpenses.UseBadges("CostType", none, ModernTheme.Neutral);
        ColorColumns(dgvExpenses, ModernTheme.TextMuted, "InvoiceDate", "InvoiceNumber");
        ColorColumns(dgvExpenses, ModernPaint.Darken(ModernTheme.Danger, 50), "Amount");

        // ── همه پرداخت‌ها: نوع پرداخت با رنگ معنادار، مبلغ خروجی قرمز ملایم ──
        var scopeColors = new Dictionary<string, Color>
        {
            ["خرید مستقیم پروژه"] = ModernTheme.Orange,
            ["پیمانکار بدون قرارداد"] = _amber,
            ["پیمانکار دارای قرارداد"] = ModernTheme.Purple,
            ["اداری"] = ModernTheme.Neutral
        };
        dgvAllProjectPayments.UseBadges("Scope", scopeColors, ModernTheme.Neutral);
        dgvAllProjectPayments.UseBadges("PaymentMethod", none, ModernTheme.Teal);
        dgvAllProjectPayments.UseBadges("CostType", none, ModernTheme.Neutral);
        ColorColumns(dgvAllProjectPayments, ModernTheme.TextMuted, "Date");
        ColorColumns(dgvAllProjectPayments, ModernPaint.Darken(ModernTheme.Danger, 50), "Amount");
    }

    private static readonly Color PaintDeduction = ModernPaint.Darken(ModernTheme.Danger, 40);

    private static void ColorColumns(DataGridView grid, Color color, params string[] columns)
    {
        foreach (var c in columns)
            if (grid.Columns.Contains(c)) grid.Columns[c].DefaultCellStyle.ForeColor = color;
    }


    private static string OrDash(string? text) => string.IsNullOrWhiteSpace(text) ? "—" : text;

    private static string DateOrDash(DateTime? date)
    {
        var text = DateHelper.ToPersianDate(date);
        return string.IsNullOrEmpty(text) ? "—" : StringHelper.ConvertToPersianDigits(text);
    }

    private static string Num(int n) => StringHelper.ConvertToPersianDigits(n.ToString());
    private static string Money(decimal v) => NumberHelper.FormatThousands(v, usePersianDigits: true);

    /// <summary>«۰» یا «۰٫۰» و مانند آن</summary>
    private static bool IsZeroText(string s) => s.Length > 0 && s.Trim('۰', '0', '٫', '٬', '.', ',', ' ').Length == 0;

    /// <summary>یک «عنوان کوچک + مقدار پررنگ» برای کارت اطلاعات قرارداد</summary>
    private static Control MakeFact(string caption, string value)
    {
        var t = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = new Padding(6, 0, 6, 0) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));

        var cap = ModernUI.Caption(caption, 9.5f, false, ModernTheme.TextMuted);
        cap.Dock = DockStyle.Fill; cap.Margin = Padding.Empty; cap.TextAlign = ContentAlignment.BottomLeft;
        var val = ModernUI.Caption(value, 12f, true, ModernTheme.TextStrong);
        val.Dock = DockStyle.Fill; val.Margin = Padding.Empty; val.TextAlign = ContentAlignment.TopLeft;

        t.Controls.Add(cap, 0, 0);
        t.Controls.Add(val, 0, 1);
        return t;
    }

    /// <summary>گروه شاخص‌ها: عنوان گروه (کم‌رنگ) + ردیف کارت‌ها</summary>
    private static Control MakeStatGroup(string title, StatStrip strip)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 6, 0), BackColor = Color.Transparent };
        var cap = ModernUI.Caption(title, 10f, true, ModernTheme.TextMuted);
        cap.Dock = DockStyle.Top;
        cap.Height = 26;
        cap.Padding = new Padding(0, 0, 6, 0);
        strip.Dock = DockStyle.Fill;
        panel.Controls.Add(strip);   // Fill اول
        panel.Controls.Add(cap);     // Top بعد ⇒ بالاتر
        return panel;
    }

    private static Label CreateInfoLabel() => new Label
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight,   // فرم Mirror: MiddleRight یعنی سمت چپ
        Font = ModernTheme.GetFont(10f),
        ForeColor = ModernTheme.TextMuted,
        BackColor = Color.Transparent
    };

    /// <summary>
    /// صفحه‌ی هر تب: بالا نوار ابزار (دکمه‌ها در راست، خلاصه‌ی تعداد/جمع در چپ)، زیرش جدول.
    /// اولین دکمه = اقدام اصلی و در راست‌ترین جا قرار می‌گیرد.
    /// </summary>
    private void BuildTabPage(Panel page, DataGridView grid, Label info, string emptyTitle, string emptyHint, params Control[] actions)
    {
        page.Padding = new Padding(16, 10, 16, 12);

        var layout = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var toolbar = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = Padding.Empty };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420F));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,   // فرم Mirror: اولین دکمه در راست
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 5, 0, 0)
        };
        foreach (var a in actions) flow.Controls.Add(a);

        toolbar.Controls.Add(flow, 0, 0);
        toolbar.Controls.Add(info, 1, 0);

        var host = WrapTabGrid(grid);
        AttachEmptyState(grid, emptyTitle, emptyHint);

        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(host, 0, 1);
        page.Controls.Add(layout);
    }

    /// <summary>دکمه‌ای که با کلیک، منوی کشویی (مثلاً اکسل) باز می‌کند</summary>
    private static ModernButton CreateMenuButton(string text, Color accent, params (string Text, EventHandler Handler)[] items)
    {
        var menu = new ContextMenuStrip
        {
            RightToLeft = RightToLeft.Yes,
            Font = ModernTheme.GetFont(10.5f),
            ShowImageMargin = false
        };
        foreach (var item in items) menu.Items.Add(item.Text, null, item.Handler);

        var btn = ModernButton.Soft(text + "  ▾", accent, 130, 34);
        btn.Click += (s, e) => menu.Show(btn, new Point(0, btn.Height));
        btn.Disposed += (s, e) => menu.Dispose();
        return btn;
    }

    /// <summary>دکمه‌های وابسته به انتخاب ردیف (ویرایش/حذف/چاپ) تا ردیفی انتخاب نشده غیرفعال می‌مانند</summary>
    private static void BindRowActions(DataGridView grid, params Control[] buttons)
    {
        void Sync()
        {
            bool has = grid.Rows.Count > 0 && grid.CurrentRow is not null;
            foreach (var b in buttons) b.Enabled = has;
        }
        grid.SelectionChanged += (s, e) => Sync();
        grid.RowsAdded += (s, e) => Sync();
        grid.RowsRemoved += (s, e) => Sync();
        Sync();
    }

    /// <summary>وقتی جدول ردیفی ندارد، به‌جای صفحه‌ی خالی یک پیام راهنما نشان می‌دهد</summary>
    private static void AttachEmptyState(DataGridView grid, string title, string hint)
    {
        var lbl = new Label
        {
            Text = title + "\n" + hint,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(11f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = ModernTheme.Background
        };
        grid.Controls.Add(lbl);

        void Sync()
        {
            lbl.Visible = grid.Rows.Count == 0;
            int top = grid.ColumnHeadersVisible ? grid.ColumnHeadersHeight : 0;
            lbl.SetBounds(0, top, grid.ClientSize.Width, Math.Max(0, grid.ClientSize.Height - top));
        }
        grid.RowsAdded += (s, e) => Sync();
        grid.RowsRemoved += (s, e) => Sync();
        grid.Resize += (s, e) => Sync();
        grid.ColumnHeadersHeightChanged += (s, e) => Sync();
        grid.HandleCreated += (s, e) => Sync();
        Sync();
    }

    // ✅ کوچک کردن خودکار فونت در جدول دریافتی‌ها
    private void DgvPayments_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (dgvPayments.Columns[e.ColumnIndex].Name != "PaymentType") return;

        e.PaintBackground(e.CellBounds, true);
        string text = e.FormattedValue?.ToString() ?? "";
        if (string.IsNullOrEmpty(text)) { e.Handled = true; return; }

        Font font = e.CellStyle.Font ?? dgvPayments.DefaultCellStyle.Font;
        int padding = 8;
        Size textSize = TextRenderer.MeasureText(text, font);
        float scale = 1.0f;

        if (textSize.Width > e.CellBounds.Width - padding)
        {
            scale = (float)(e.CellBounds.Width - padding) / textSize.Width;
            if (scale < 0.6f) scale = 0.6f;
        }

        using Font scaledFont = new Font(font.FontFamily, font.Size * scale, font.Style);
        TextRenderer.DrawText(e.Graphics, text, scaledFont, e.CellBounds, e.CellStyle.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        e.Handled = true;
    }

    // ✅ کوچک کردن خودکار فونت در جدول پرداخت‌های پروژه
    private void DgvAllProjectPayments_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (dgvAllProjectPayments.Columns[e.ColumnIndex].Name != "PaymentMethod") return;

        e.PaintBackground(e.CellBounds, true);
        string text = e.FormattedValue?.ToString() ?? "";
        if (string.IsNullOrEmpty(text)) { e.Handled = true; return; }

        Font font = e.CellStyle.Font ?? dgvAllProjectPayments.DefaultCellStyle.Font;
        int padding = 8;
        Size textSize = TextRenderer.MeasureText(text, font);
        float scale = 1.0f;

        if (textSize.Width > e.CellBounds.Width - padding)
        {
            scale = (float)(e.CellBounds.Width - padding) / textSize.Width;
            if (scale < 0.6f) scale = 0.6f;
        }

        using Font scaledFont = new Font(font.FontFamily, font.Size * scale, font.Style);
        TextRenderer.DrawText(e.Graphics, text, scaledFont, e.CellBounds, e.CellStyle.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        e.Handled = true;
    }

    private async void DgvAllProjectPayments_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var row = dgvAllProjectPayments.Rows[e.RowIndex];

        if (row.Cells["Id"].Value is null || row.Cells["Id"].Value == DBNull.Value) return;

        int id = Convert.ToInt32(row.Cells["Id"].Value);

        if (id < 0)
        {
            int checkId = Math.Abs(id);
            using (var form = new CashCheckEditForm(_connectionString, checkId))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    await LoadDataSafeAsync();
                }
            }
        }
        else
        {
            using (var form = new CashTransactionEditForm(_connectionString, id))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    await LoadDataSafeAsync();
                }
            }
        }
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

    /// <summary>جدول تب را داخل کادر مدرن با حاشیه می‌گذارد</summary>
    private static Panel WrapTabGrid(DataGridView grid)
    {
        var host = ModernReport.WrapGrid(grid);
        host.Margin = Padding.Empty;
        return host;
    }

    private static void RightAlign(DataGridView grid, params string[] columns)
    {
        foreach (var c in columns)
            if (grid.Columns.Contains(c)) grid.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
    }

    private static void BoldColumns(DataGridView grid, params string[] columns)
    {
        foreach (var c in columns)
            if (grid.Columns.Contains(c)) grid.Columns[c].DefaultCellStyle.Font = _boldFont;
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
                _companyLogo = new Bitmap(img);

                if (picCompanyLogo is not null && !picCompanyLogo.IsDisposed)
                {
                    picCompanyLogo.Image = _companyLogo;
                    if (_headerLayout is not null) _headerLayout.ColumnStyles[0].Width = 84F;
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogWarning("خطا در بارگذاری لوگو: " + ex.Message, "ClientContractReportForm");
        }
    }

    private async Task LoadDataSafeAsync()
    {
        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var stRepo = new ClientStatementRepository(_connectionString);
                var cRepo = new ContractorContractRepository(_connectionString);
                var eRepo = new ProjectExpenseRepository(_connectionString);
                var tRepo = new CashTransactionRepository(_connectionString);
                var chRepo = new CashCheckRepository(_connectionString);
                var bRepo = new BankAccountRepository(_connectionString);

                var statementsTask = Task.Run(() => stRepo.GetByContractId(_contract.Id));
                var contractsTask = Task.Run(() => cRepo.GetByClientContractId(_contract.Id));
                var expensesTask = Task.Run(() => eRepo.GetByClientContractId(_contract.Id));

                var transTask = tRepo.GetByContractIdAsync(_contract.Id);
                var checksTask = chRepo.GetByContractIdAsync(_contract.Id);

                var accountsTask = Task.Run(() => bRepo.GetAllAsync());

                await Task.WhenAll(statementsTask, contractsTask, expensesTask, transTask, checksTask, accountsTask);

                var statements = statementsTask.Result ?? new List<ClientStatement>();
                var contracts = contractsTask.Result ?? new List<ContractorContract>();
                var expenses = expensesTask.Result ?? new List<ProjectExpense>();
                var allTrans = transTask.Result ?? new List<CashTransaction>();
                var allChecks = checksTask.Result ?? new List<CashCheck>();
                var accounts = accountsTask.Result ?? new List<CompanyBankAccount>();

                var cashDeskDict = accounts.Where(a => a.IsCashDesk).ToDictionary(a => a.Id, a =>
                {
                    string title = a.Title;
                    if (!title.Contains("شماره"))
                    {
                        var match = Regex.Match(title, @"تنخواه\s*(\d+)");
                        string num = match.Success ? match.Groups[1].Value : a.Id.ToString();
                        string namePart = title.Replace("تنخواه", "").Replace(num, "").Trim();
                        return $"تنخواه شماره {num} {namePart}";
                    }
                    return title;
                });

                if (IsDisposed) return;

                _allPaymentsList.Clear();

                // ✅ فقط تراکنش‌های استاندارد (ClientPayments حذف شد)
                var transIn = allTrans.Where(t => t.Direction == DirectionTypes.Incoming);
                foreach (var t in transIn)
                {
                    string method = t.TransactionType;
                    if (t.AccountId.HasValue && cashDeskDict.TryGetValue(t.AccountId.Value, out var cashTitle))
                        method = cashTitle;

                    _allPaymentsList.Add(new UnifiedPaymentViewModel
                    {
                        Id = t.Id,
                        TableName = "CashTransactions",
                        PaymentType = method,
                        PaymentDate = t.TransactionDate,
                        Amount = t.Amount,
                        Description = t.Description,
                        Status = "دریافت شد",
                        CostType = t.CostType
                    });
                }

                var checksIn = allChecks.Where(c => c.CheckType == DirectionTypes.Incoming && c.Status != "باطل شده" && c.Status != "برگشتی");
                foreach (var c in checksIn)
                {
                    string method = "چک دریافتی";
                    if (c.AccountId.HasValue && cashDeskDict.TryGetValue(c.AccountId.Value, out var cashTitle))
                        method = "چک - " + cashTitle;

                    _allPaymentsList.Add(new UnifiedPaymentViewModel
                    {
                        Id = c.Id,
                        TableName = "CashChecks",
                        PaymentType = method,
                        PaymentDate = c.DueDate,
                        Amount = c.Amount,
                        Description = c.Description,
                        Status = c.Status,
                        CostType = c.CostType
                    });
                }

                _allPaymentsList = _allPaymentsList.OrderByDescending(x => x.PaymentDate).ToList();

                _projectCashTransactions = allTrans
                    .Where(t => t.Direction == DirectionTypes.Outgoing &&
                                (t.PaymentScope == PaymentScopes.DirectPurchase || t.PaymentScope == PaymentScopes.InformalContractor || t.PaymentScope == PaymentScopes.ContractorContract))
                    .OrderByDescending(t => t.TransactionDate)
                    .ToList();

                if (IsDisposed) return;

                dgvStatements.Rows.Clear();
                foreach (var st in statements)
                {
                    decimal totalDeductions = st.Deductions;
                    if (totalDeductions == 0)
                        totalDeductions = st.RetentionDeduction + st.InsuranceDeduction + st.AdvanceRecovery + st.OtherDeductions;

                    dgvStatements.Rows.Add(
                        st.Id,
                        st.StatementNumber,
                        NumberHelper.FormatThousands(st.GrossAmount, usePersianDigits: true),
                        NumberHelper.FormatThousands(st.FeeAmount, usePersianDigits: true),
                        NumberHelper.FormatThousands(st.VAT, usePersianDigits: true),
                        NumberHelper.FormatThousands(st.TotalAmount, usePersianDigits: true),
                        NumberHelper.FormatThousands(st.RetentionDeduction, usePersianDigits: true),
                        NumberHelper.FormatThousands(st.InsuranceDeduction, usePersianDigits: true),
                        NumberHelper.FormatThousands(st.AdvanceRecovery, usePersianDigits: true),
                        NumberHelper.FormatThousands(st.OtherDeductions, usePersianDigits: true),
                        NumberHelper.FormatThousands(totalDeductions, usePersianDigits: true),
                        NumberHelper.FormatThousands(st.NetReceivableAmount, usePersianDigits: true));
                }

                dgvPayments.Rows.Clear();
                foreach (var p in _allPaymentsList)
                {
                    dgvPayments.Rows.Add(
                        p.Id,
                        DateHelper.ToPersianDate(p.PaymentDate),
                        NumberHelper.FormatThousands(p.Amount, usePersianDigits: true),
                        p.PaymentType,
                        string.IsNullOrWhiteSpace(p.CostType) ? "---" : p.CostType,
                        p.Description,
                        p.TableName);
                }

                dgvContractorContracts.Rows.Clear();
                foreach (var c in contracts)
                {
                    dgvContractorContracts.Rows.Add(c.Id, c.ContractNumber, c.DisplayName, c.Subject,
                        NumberHelper.FormatThousands(c.Amount, usePersianDigits: true),
                        DateHelper.ToPersianDate(c.ContractDate), DateHelper.ToPersianDate(c.EndDate));
                }

                dgvExpenses.Rows.Clear();
                foreach (var item in expenses)
                {
                    dgvExpenses.Rows.Add(item.Id, DateHelper.ToPersianDate(item.InvoiceDate), item.InvoiceNumber, item.PartyName,
                        string.IsNullOrWhiteSpace(item.CostType) ? "---" : item.CostType,
                        NumberHelper.FormatThousands(item.Amount, usePersianDigits: true), item.Description, item.BillingStatus);
                }

                dgvAllProjectPayments.Rows.Clear();
                foreach (var t in _projectCashTransactions)
                {
                    string descText = string.IsNullOrWhiteSpace(t.Description) ? "---" : t.Description.Replace("\r", " ").Replace("\n", " ");

                    string methodDisplay = t.TransactionType;
                    if (t.AccountId.HasValue && cashDeskDict.TryGetValue(t.AccountId.Value, out var cashTitle))
                    {
                        methodDisplay = cashTitle;
                    }

                    dgvAllProjectPayments.Rows.Add(
                        t.Id,
                        DateHelper.ToPersianDate(t.TransactionDate),
                        methodDisplay,
                        t.PaymentScopeDisplay,
                        t.TafsiliTitle ?? "---",
                        t.CostType ?? "---",
                        t.BillingStatus,
                        NumberHelper.FormatThousands(t.Amount, usePersianDigits: true),
                        descText,
                        t.Amount
                    );
                }

                var paymentChecks = allChecks
                    .Where(c => c.CheckType == DirectionTypes.Outgoing && c.Status != "باطل شده" && c.Status != "برگشتی" &&
                                (c.PaymentScope == PaymentScopes.DirectPurchase || c.PaymentScope == PaymentScopes.InformalContractor || c.PaymentScope == PaymentScopes.ContractorContract))
                    .OrderByDescending(c => c.DueDate)
                    .ToList();

                foreach (var c in paymentChecks)
                {
                    string scopeDisplay = string.IsNullOrWhiteSpace(c.PaymentScope) ? "---" :
                        (c.PaymentScope == PaymentScopes.Administrative ? "اداری" :
                        (c.PaymentScope == PaymentScopes.DirectPurchase ? "خرید مستقیم پروژه" :
                        (c.PaymentScope == PaymentScopes.InformalContractor ? "پیمانکار بدون قرارداد" : "پیمانکار دارای قرارداد")));

                    string billingStatus = c.ClientStatementId.HasValue ? "🔒 صورت وضعیت شده" : "⏳ در انتظار صورت وضعیت";
                    string checkDescText = string.IsNullOrWhiteSpace(c.Description) ? "---" : c.Description.Replace("\r", " ").Replace("\n", " ");

                    string methodDisplay = "چک " + c.Status;
                    if (c.AccountId.HasValue && cashDeskDict.TryGetValue(c.AccountId.Value, out var cashTitle))
                    {
                        methodDisplay = cashTitle;
                    }

                    dgvAllProjectPayments.Rows.Add(
                        -c.Id,
                        DateHelper.ToPersianDate(c.DueDate),
                        methodDisplay,
                        scopeDisplay,
                        c.RecipientName ?? "---",
                        c.CostType ?? "---",
                        billingStatus,
                        NumberHelper.FormatThousands(c.Amount, usePersianDigits: true),
                        checkDescText,
                        c.Amount
                    );
                }

                UpdateSummary(statements, _allPaymentsList, contracts, expenses, _projectCashTransactions, paymentChecks);
            }, "در حال بارگذاری اطلاعات قرارداد...");
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientContractReportForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات:\n" + ex.Message);
        }
    }

    private void UpdateSummary(
        List<ClientStatement> statements,
        List<UnifiedPaymentViewModel> payments,
        List<ContractorContract> contractorContracts,
        List<ProjectExpense> expenses,
        List<CashTransaction> projectPayments,
        List<CashCheck> projectChecks)
    {
        try
        {
            if (IsDisposed) return;

            decimal totalContractAmount = _contract.Amount;
            decimal totalWorkDone = statements.Sum(x => x.GrossAmount);
            decimal totalNetReceivable = statements.Sum(x => x.NetReceivableAmount);
            decimal totalReceived = payments.Sum(p => p.Amount);

            decimal projectDirectExpenses = projectPayments.Where(t => t.PaymentScope == PaymentScopes.DirectPurchase).Sum(t => t.Amount) +
                                            projectChecks.Where(c => c.PaymentScope == PaymentScopes.DirectPurchase).Sum(c => c.Amount);

            decimal informalContractorPayments = projectPayments.Where(t => t.PaymentScope == PaymentScopes.InformalContractor).Sum(t => t.Amount) +
                                                 projectChecks.Where(c => c.PaymentScope == PaymentScopes.InformalContractor).Sum(c => c.Amount);

            decimal formalContractorPayments = projectPayments.Where(t => t.PaymentScope == PaymentScopes.ContractorContract).Sum(t => t.Amount) +
                                               projectChecks.Where(c => c.PaymentScope == PaymentScopes.ContractorContract).Sum(c => c.Amount);

            decimal invoiceExpenses = expenses.Sum(x => x.Amount);

            decimal totalProjectExpenses = projectDirectExpenses + informalContractorPayments
                                          + formalContractorPayments + invoiceExpenses;

            decimal contractAdvance = _contract.TotalAdvanceAmount;
            decimal recoveredAdvance = statements.Sum(x => x.AdvanceRecovery);
            decimal remainingAdvance = contractAdvance - recoveredAdvance;

            decimal remainingAmount = totalContractAmount - totalWorkDone;
            decimal progressPercent = totalContractAmount > 0 ? (totalWorkDone / totalContractAmount) * 100 : 0;
            decimal debt = totalNetReceivable - totalReceived;
            decimal grossProfit = totalContractAmount - totalProjectExpenses;

            _totalBilledExpenses = projectPayments.Where(t => t.ClientStatementId.HasValue).Sum(t => t.Amount) +
                                   projectChecks.Where(c => c.ClientStatementId.HasValue).Sum(c => c.Amount);

            _totalUnbilledExpenses = projectPayments.Where(t => !t.ClientStatementId.HasValue).Sum(t => t.Amount) +
                                     projectChecks.Where(c => !c.ClientStatementId.HasValue).Sum(c => c.Amount);

            lblSummaryTotalContract.SetAmount(totalContractAmount);
            lblSummaryTotalWork.SetAmount(totalWorkDone);
            lblSummaryRemaining.SetAmount(Math.Max(0, remainingAmount));
            string progressText = NumberHelper.FormatThousands(progressPercent, 1, usePersianDigits: true) + "٪";
            lblSummaryTotalWork.SubText = "ریال  •  پیشرفت " + progressText;
            progressBar.Value = progressPercent;
            progressBar.Accent = progressPercent > 100 ? ModernTheme.Danger : ModernTheme.Success;
            progressBar.Caption = "پیشرفت کارکرد: " + progressText;

            lblSummaryTotalAdvance.SetAmount(contractAdvance);
            lblSummaryRecoveredAdvance.SetAmount(recoveredAdvance);
            lblSummaryRemainingAdvance.SetAmount(Math.Max(0, remainingAdvance));
            lblSummaryRemainingAdvance.Accent = remainingAdvance > 0 ? ModernTheme.Danger : ModernTheme.Success;

            lblSummaryTotalPaid.SetAmount(totalReceived);
            // اگر بیشتر از خالص صورت وضعیت‌ها دریافت شده، به‌جای نمایش صفر «مازاد دریافتی» دیده شود
            if (debt >= 0)
            {
                lblSummaryDebt.Title = "مانده طلب";
                lblSummaryDebt.SetAmount(debt);
                lblSummaryDebt.Accent = debt > 0 ? ModernTheme.Danger : ModernTheme.Success;
            }
            else
            {
                lblSummaryDebt.Title = "مازاد دریافتی";
                lblSummaryDebt.SetAmount(-debt);
                lblSummaryDebt.Accent = ModernTheme.Warning;
            }

            lblSummaryProjectDirect.SetAmount(projectDirectExpenses);
            lblSummaryInformalContractor.SetAmount(informalContractorPayments);
            lblSummaryContractorTotal.SetAmount(formalContractorPayments);
            lblSummaryExpenses.SetAmount(invoiceExpenses);

            lblSummaryProfit.SetAmount(grossProfit);
            lblSummaryProfit.Accent = grossProfit >= 0 ? ModernTheme.Purple : ModernTheme.Danger;

            lblSummaryBilled.SetAmount(_totalBilledExpenses);
            lblSummaryUnbilled.SetAmount(_totalUnbilledExpenses);

            // خلاصه‌ی هر تب (تعداد و جمع) کنار دکمه‌ها
            lblInfoStatements.Text = $"{Num(statements.Count)} صورت وضعیت  •  جمع خالص دریافتی: {Money(totalNetReceivable)} ریال";
            lblInfoPayments.Text = $"{Num(payments.Count)} دریافتی  •  جمع: {Money(totalReceived)} ریال";
            lblInfoContractors.Text = $"{Num(contractorContracts.Count)} قرارداد  •  جمع مبالغ: {Money(contractorContracts.Sum(c => c.Amount))} ریال";
            lblInfoExpenses.Text = $"{Num(expenses.Count)} فاکتور  •  جمع: {Money(invoiceExpenses)} ریال";
            lblInfoAllPayments.Text = $"{Num(projectPayments.Count + projectChecks.Count)} پرداخت  •  جمع: {Money(projectPayments.Sum(t => t.Amount) + projectChecks.Sum(c => c.Amount))} ریال";

            decimal chartContractorCosts = formalContractorPayments + informalContractorPayments;
            decimal chartInvoiceCosts = projectDirectExpenses + invoiceExpenses;
            chartPanel?.UpdateData(totalContractAmount, chartContractorCosts, chartInvoiceCosts);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientContractReportForm.UpdateSummary");
        }
    }

    private async Task OpenSelectedContractorReportAsync()
    {
        if (dgvContractorContracts.CurrentRow is null)
        {
            ModernMessageBox.ShowWarning("لطفاً یک قرارداد پیمانکار را از جدول انتخاب کنید.");
            return;
        }

        try
        {
            int contractId = Convert.ToInt32(dgvContractorContracts.CurrentRow.Cells["Id"].Value);

            ContractorContract? contract = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorContractRepository(_connectionString);
                contract = await Task.Run(() => repo.GetById(contractId));
            }, "در حال بارگذاری اطلاعات پیمانکار...");

            if (!success || contract is null || IsDisposed) return;

            using var form = new ContractorStatementReportForm(_connectionString, contract);
            form.ShowDialog(this);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientContractReportForm.OpenSelectedContractorReport");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private void ExportAllProjectPaymentsToExcel()
    {
        if (dgvAllProjectPayments.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("داده‌ای برای خروجی وجود ندارد.");
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            FileName = $"پرداخت‌های_پروژه_{_contract.ContractNumber}_{DateTime.Now:yyyyMMdd}.xlsx",
            Title = "ذخیره خروجی اکسل"
        };

        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("پرداخت‌های پروژه");
            ws.RightToLeft = true;

            string[] headers = { "تاریخ", "روش پرداخت", "نوع پرداخت", "طرف حساب", "ماهیت هزینه", "وضعیت صورت وضعیت", "مبلغ (ریال)", "توضیحات" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            decimal total = 0;
            for (int r = 0; r < dgvAllProjectPayments.Rows.Count; r++)
            {
                var row = dgvAllProjectPayments.Rows[r];
                int er = r + 2;

                ws.Cell(er, 1).Value = row.Cells["Date"].Value?.ToString() ?? "";
                ws.Cell(er, 2).Value = row.Cells["PaymentMethod"].Value?.ToString() ?? "";
                ws.Cell(er, 3).Value = row.Cells["Scope"].Value?.ToString() ?? "";
                ws.Cell(er, 4).Value = row.Cells["TafsiliTitle"].Value?.ToString() ?? "";
                ws.Cell(er, 5).Value = row.Cells["CostType"].Value?.ToString() ?? "";
                ws.Cell(er, 6).Value = row.Cells["BillingStatus"].Value?.ToString() ?? "";
                ws.Cell(er, 7).Value = Convert.ToDecimal(row.Cells["RawAmount"].Value ?? 0);
                ws.Cell(er, 8).Value = row.Cells["Description"].Value?.ToString() ?? "";

                total += Convert.ToDecimal(row.Cells["RawAmount"].Value ?? 0);

                ws.Range(er, 1, er, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(er, 1, er, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int totalRow = dgvAllProjectPayments.Rows.Count + 2;
            ws.Cell(totalRow, 1).Value = "جمع کل";
            ws.Range(totalRow, 1, totalRow, 6).Merge();
            ws.Cell(totalRow, 7).Value = total;
            ws.Cell(totalRow, 7).Style.NumberFormat.Format = "#,##0";
            ws.Range(totalRow, 1, totalRow, 8).Style.Font.Bold = true;
            ws.Range(totalRow, 1, totalRow, 8).Style.Fill.BackgroundColor = XLColor.FromArgb(245, 238, 230);
            ws.Range(totalRow, 1, totalRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Column(7).Style.NumberFormat.Format = "#,##0";
            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);

            workbook.SaveAs(sfd.FileName);

            ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", true);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientContractReportForm.ExportAllProjectPayments");
            ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private async void BtnDeletePay_Click(object? sender, EventArgs e)
    {
        try
        {
            if (dgvPayments.CurrentRow is null)
            {
                ModernMessageBox.ShowWarning("لطفاً ابتدا یک دریافتی را از جدول انتخاب کنید.");
                return;
            }

            int id = Convert.ToInt32(dgvPayments.CurrentRow.Cells["Id"].Value);
            string tableName = dgvPayments.CurrentRow.Cells["TableName"].Value?.ToString() ?? "";
            string typeName = dgvPayments.CurrentRow.Cells["PaymentType"].Value?.ToString() ?? "";
            decimal amount = NumberHelper.ParseFormatted(dgvPayments.CurrentRow.Cells["Amount"].Value?.ToString());

            if (!ModernMessageBox.AskConfirm(
                $"آیا از حذف این دریافتی ({typeName}) به مبلغ {NumberHelper.FormatThousands(amount, usePersianDigits: true)} ریال اطمینان دارید؟",
                "حذف دریافتی"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (tableName == "CashTransactions")
                {
                    await _cashRepo.DeleteBatchAsync(new List<int> { id });
                }
                else if (tableName == "CashChecks")
                {
                    await _checkRepo.DeleteBatchAsync(new List<int> { id });
                }
            }, "در حال حذف دریافتی...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show($"{typeName} با موفقیت حذف شد.", isSuccess: true);
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientContractReportForm.BtnDeletePay_Click");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private async void EditSelectedStatement(object? sender, EventArgs e)
    {
        try
        {
            if (dgvStatements.CurrentRow is null) return;
            int id = Convert.ToInt32(dgvStatements.CurrentRow.Cells["Id"].Value);

            ClientStatement? st = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ClientStatementRepository(_connectionString);
                st = await Task.Run(() => repo.GetById(id));
            }, "در حال دریافت اطلاعات...");

            if (!success || st is null || IsDisposed) return;

            using var f = new ClientStatementEditForm(_connectionString, _contract, st);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                if (!IsDisposed) await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientContractReportForm.EditSelectedStatement");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private async void DeleteSelectedStatement(object? sender, EventArgs e)
    {
        try
        {
            if (dgvStatements.CurrentRow is null) return;
            int id = Convert.ToInt32(dgvStatements.CurrentRow.Cells["Id"].Value);

            if (!ModernMessageBox.AskConfirm("آیا از حذف این صورت وضعیت اطمینان دارید؟", "حذف صورت وضعیت"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ClientStatementRepository(_connectionString);
                await Task.Run(() => repo.Delete(id));
            }, "در حال حذف...");

            if (success && !IsDisposed)
            {
                await LoadDataSafeAsync();
                if (!IsDisposed) ToastHelper.Show("صورت وضعیت حذف شد.", isSuccess: true);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientContractReportForm.DeleteSelectedStatement");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private async void DeleteSelectedExpense(object? sender, EventArgs e)
    {
        try
        {
            if (dgvExpenses.CurrentRow is null) return;
            int id = Convert.ToInt32(dgvExpenses.CurrentRow.Cells["Id"].Value);

            if (!ModernMessageBox.AskConfirm("آیا از حذف این فاکتور اطمینان دارید؟", "حذف فاکتور"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ProjectExpenseRepository(_connectionString);
                await Task.Run(() => repo.Delete(id));
            }, "در حال حذف...");

            if (success && !IsDisposed)
            {
                await LoadDataSafeAsync();
                if (!IsDisposed) ToastHelper.Show("فاکتور حذف شد.", isSuccess: true);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientContractReportForm.DeleteSelectedExpense");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private void BtnDownloadStTemplate_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"ClientStatements_Template_{_contract.ContractNumber}.xlsx",
            Title = "ذخیره قالب اکسل صورت وضعیت‌ها"
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("صورت وضعیت‌ها");
            ws.RightToLeft = true;

            string[] headers = { "شماره صورت وضعیت", "تاریخ (شمسی)", "مبلغ کارکرد", "ارزش افزوده", "حسن انجام کار", "بیمه", "پیش‌دریافت", "سایر کسورات", "جمع کسورات" };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            ws.Cell(2, 1).Value = "ST-1001";
            ws.Cell(2, 2).Value = DateHelper.ToPersianDate(DateTime.Now);
            ws.Cell(2, 3).Value = 50000000;
            ws.Cell(2, 4).Value = 4500000;
            ws.Cell(2, 5).Value = 2500000;
            ws.Cell(2, 6).Value = 1000000;
            ws.Cell(2, 7).Value = 0;
            ws.Cell(2, 8).Value = 0;
            ws.Cell(2, 9).Value = 3500000;

            ws.Columns().AdjustToContents();
            ws.Row(1).Height = 30;
            workbook.SaveAs(sfd.FileName);

            ToastHelper.Show("قالب اکسل با موفقیت ساخته شد.", true);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientContractReportForm.DownloadStTemplate");
            ModernMessageBox.ShowError("خطا در ساخت قالب:\n" + ex.Message);
        }
    }

    private async void BtnImportStExcel_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل صورت وضعیت‌ها" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        DataTable? dt = null;
        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            dt = await ParseStatementsExcelAsync(ofd.FileName);
        }, "در حال خواندن فایل اکسل...");

        if (!success || dt is null || IsDisposed) return;

        if (dt.Rows.Count == 0) { ModernMessageBox.ShowWarning("داده‌ای در فایل اکسل یافت نشد."); return; }

        ShowStatementsPreviewForm(dt);
    }

    private async Task<DataTable> ParseStatementsExcelAsync(string filePath)
    {
        var dt = new DataTable();
        dt.Columns.Add("StatementNumber", typeof(string));
        dt.Columns.Add("DateStr", typeof(string));
        dt.Columns.Add("Date", typeof(DateTime));
        dt.Columns.Add("GrossAmount", typeof(decimal));
        dt.Columns.Add("VAT", typeof(decimal));
        dt.Columns.Add("RetentionDeduction", typeof(decimal));
        dt.Columns.Add("InsuranceDeduction", typeof(decimal));
        dt.Columns.Add("AdvanceRecovery", typeof(decimal));
        dt.Columns.Add("OtherDeductions", typeof(decimal));
        dt.Columns.Add("Deductions", typeof(decimal));
        dt.Columns.Add("NetReceivableAmount", typeof(decimal));
        dt.Columns.Add("Status", typeof(string));

        using var workbook = new XLWorkbook(filePath);
        var ws = workbook.Worksheet(1);
        var rows = ws.RowsUsed().Skip(1);

        var existingStatements = new List<ClientStatement>();
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT StatementNumber FROM ClientStatements WHERE ContractId = @cid", conn);
            cmd.Parameters.AddWithValue("@cid", _contract.Id);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) existingStatements.Add(new ClientStatement { StatementNumber = reader.GetString(0) });
        }

        foreach (var row in rows)
        {
            string stNum = row.Cell(1).GetString().Trim();
            string dateStr = row.Cell(2).GetString().Trim();
            string grossStr = row.Cell(3).GetString().Trim();
            string vatStr = row.Cell(4).GetString().Trim();
            string retStr = row.Cell(5).GetString().Trim();
            string insStr = row.Cell(6).GetString().Trim();
            string advStr = row.Cell(7).GetString().Trim();
            string otherStr = row.Cell(8).GetString().Trim();
            string dedStr = row.Cell(9).GetString().Trim();

            if (string.IsNullOrWhiteSpace(stNum) && string.IsNullOrWhiteSpace(grossStr)) continue;

            DateTime? date = DateHelper.ParsePersianDate(dateStr);
            decimal gross = NumberHelper.ParseFormatted(grossStr);
            decimal vat = NumberHelper.ParseFormatted(vatStr);
            decimal ret = NumberHelper.ParseFormatted(retStr);
            decimal ins = NumberHelper.ParseFormatted(insStr);
            decimal adv = NumberHelper.ParseFormatted(advStr);
            decimal other = NumberHelper.ParseFormatted(otherStr);
            decimal ded = NumberHelper.ParseFormatted(dedStr);

            if (ded == 0) ded = ret + ins + adv + other;

            decimal net = (gross + vat) - ded;

            string status = "معتبر";
            if (string.IsNullOrWhiteSpace(stNum)) status = "خطا: شماره خالی است";
            else if (existingStatements.Any(x => x.StatementNumber == stNum)) status = "خطا: شماره تکراری است";
            else if (date is null) status = "خطا: تاریخ نامعتبر";
            else if (gross <= 0) status = "خطا: مبلغ کارکرد نامعتبر";

            dt.Rows.Add(stNum, dateStr, date ?? DateTime.Now, gross, vat, ret, ins, adv, other, ded, net, status);
        }

        return dt;
    }

    private void ShowStatementsPreviewForm(DataTable dt)
    {
        var previewForm = new Form();
        ModernForm.Setup(previewForm, "پیش‌نمایش صورت وضعیت‌های ایمپورت‌شده", new Size(1100, 600));
        previewForm.Padding = new Padding(15);
        previewForm.ShowInTaskbar = false;

        var dgv = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true, AutoGenerateColumns = false };
        dgv.ApplyModernStyle();

        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StatementNumber", HeaderText = "شماره", FillWeight = 15 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DateStr", HeaderText = "تاریخ", FillWeight = 15 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "GrossAmount", HeaderText = "مبلغ کارکرد", FillWeight = 15 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "VAT", HeaderText = "ارزش افزوده", FillWeight = 10 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RetentionDeduction", HeaderText = "حسن انجام کار", FillWeight = 10 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InsuranceDeduction", HeaderText = "بیمه", FillWeight = 10 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AdvanceRecovery", HeaderText = "پیش‌دریافت", FillWeight = 10 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "OtherDeductions", HeaderText = "سایر کسورات", FillWeight = 10 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Deductions", HeaderText = "جمع کسورات", FillWeight = 10 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NetReceivableAmount", HeaderText = "خالص دریافتی", FillWeight = 15 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "وضعیت", FillWeight = 15 });

        dgv.DataSource = dt;

        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgv.Columns[e.ColumnIndex].DataPropertyName;

            if (colName == "GrossAmount" || colName == "VAT" || colName == "RetentionDeduction" ||
                colName == "InsuranceDeduction" || colName == "AdvanceRecovery" || colName == "OtherDeductions" ||
                colName == "Deductions" || colName == "NetReceivableAmount")
            {
                if (e.Value is decimal d) { e.Value = NumberHelper.FormatThousands(d, usePersianDigits: true); e.FormattingApplied = true; }
            }

            if (colName == "Status")
            {
                string status = e.Value?.ToString() ?? "";
                if (status != "معتبر") { e.CellStyle.BackColor = ModernTheme.DangerSoft; e.CellStyle.ForeColor = ModernTheme.DangerText; }
                else { e.CellStyle.BackColor = ModernTheme.SuccessSoft; }
            }
        };

        var btnSave = ModernButton.Create("انتقال ردیف‌های معتبر به دیتابیس", ModernTheme.Success, 300, 50);
        btnSave.Dock = DockStyle.Bottom;

        btnSave.Click += async (s, ev) =>
        {
            var validRows = dt.Rows.Cast<DataRow>().Where(r => r["Status"].ToString() == "معتبر").ToList();
            if (validRows.Count == 0) { ModernMessageBox.ShowWarning("هیچ ردیف معتبری برای ثبت وجود ندارد."); return; }
            if (!ModernMessageBox.AskConfirm($"آیا از ثبت {validRows.Count} صورت وضعیت مطمئن هستید؟", "تایید ایمپورت")) return;

            bool success = await AsyncHelper.RunWithLoading(previewForm, async () =>
            {
                var repo = new ClientStatementRepository(_connectionString);
                foreach (var row in validRows)
                {
                    var statement = new ClientStatement
                    {
                        ContractId = _contract.Id,
                        StatementNumber = row["StatementNumber"].ToString()!,
                        StatementDate = Convert.ToDateTime(row["Date"]),
                        GrossAmount = Convert.ToDecimal(row["GrossAmount"]),
                        VAT = Convert.ToDecimal(row["VAT"]),
                        RetentionDeduction = Convert.ToDecimal(row["RetentionDeduction"]),
                        InsuranceDeduction = Convert.ToDecimal(row["InsuranceDeduction"]),
                        AdvanceRecovery = Convert.ToDecimal(row["AdvanceRecovery"]),
                        OtherDeductions = Convert.ToDecimal(row["OtherDeductions"]),
                        Deductions = Convert.ToDecimal(row["Deductions"]),
                        NetReceivableAmount = Convert.ToDecimal(row["NetReceivableAmount"]),
                        CreatedAt = DateTime.Now
                    };
                    await Task.Run(() => repo.Add(statement));
                }
            }, "در حال ثبت صورت وضعیت‌ها...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show($"تعداد {validRows.Count} صورت وضعیت با موفقیت ثبت شد.", isSuccess: true);
                previewForm.Close();
                await LoadDataSafeAsync();
            }
        };

        previewForm.Controls.Add(dgv);
        previewForm.Controls.Add(btnSave);
        previewForm.ShowDialog(this);
    }

    private void BtnDownloadPayTemplate_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"ClientPayments_Template_{_contract.ContractNumber}.xlsx",
            Title = "ذخیره قالب اکسل دریافتی‌ها"
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("دریافتی‌ها");
            ws.RightToLeft = true;

            string[] headers = {
                "تاریخ دریافت (شمسی 1403/01/01)",
                "مبلغ دریافتی (ریال)",
                "نوع پرداخت (نقدی/چک/حواله)",
                "توضیحات (مثلاً: علی‌الحساب)"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            ws.Cell(2, 1).Value = DateHelper.ToPersianDate(DateTime.Now);
            ws.Cell(2, 2).Value = 500000000;
            ws.Cell(2, 3).Value = "حواله";
            ws.Cell(2, 4).Value = "علی‌الحساب";

            ws.Range("C2:C1000").CreateDataValidation().List("\"نقدی,چک,حواله,سایر\"");

            ws.Columns().AdjustToContents();
            ws.Row(1).Height = 30;
            workbook.SaveAs(sfd.FileName);

            ToastHelper.Show("قالب اکسل با موفقیت ساخته شد.", true);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientContractReportForm.DownloadPayTemplate");
            ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private async void BtnImportPayExcel_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل دریافتی‌ها" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        DataTable? dt = null;
        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            dt = await ParsePaymentsExcelAsync(ofd.FileName);
        }, "در حال خواندن فایل اکسل...");

        if (!success || dt is null || IsDisposed) return;

        if (dt.Rows.Count == 0) { ModernMessageBox.ShowWarning("داده‌ای در فایل اکسل یافت نشد."); return; }

        ShowPaymentsPreviewForm(dt);
    }

    private async Task<DataTable> ParsePaymentsExcelAsync(string filePath)
    {
        var dt = new DataTable();
        dt.Columns.Add("DateStr", typeof(string));
        dt.Columns.Add("Date", typeof(DateTime));
        dt.Columns.Add("Amount", typeof(decimal));
        dt.Columns.Add("PaymentType", typeof(string));
        dt.Columns.Add("Description", typeof(string));
        dt.Columns.Add("Status", typeof(string));

        using var workbook = new XLWorkbook(filePath);
        var ws = workbook.Worksheet(1);
        var rows = ws.RowsUsed().Skip(1);

        foreach (var row in rows)
        {
            string dateStr = row.Cell(1).GetString().Trim();
            string amountStr = row.Cell(2).GetString().Trim();
            string typeStr = row.Cell(3).GetString().Trim();
            string desc = row.Cell(4).GetString().Trim();

            if (string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(amountStr)) continue;

            DateTime? date = DateHelper.ParsePersianDate(dateStr);
            decimal amount = NumberHelper.ParseFormatted(amountStr);

            string status = "معتبر";
            if (date is null) status = "خطا: تاریخ نامعتبر";
            else if (amount <= 0) status = "خطا: مبلغ نامعتبر";
            else if (string.IsNullOrWhiteSpace(typeStr)) typeStr = "نقدی";

            dt.Rows.Add(dateStr, date ?? DateTime.Now, amount, typeStr, string.IsNullOrWhiteSpace(desc) ? "علی‌الحساب" : desc, status);
        }

        return dt;
    }

    private void ShowPaymentsPreviewForm(DataTable dt)
    {
        var previewForm = new Form();
        ModernForm.Setup(previewForm, "پیش‌نمایش دریافتی‌های ایمپورت‌شده", new Size(800, 600));
        previewForm.Padding = new Padding(15);
        previewForm.ShowInTaskbar = false;

        var dgv = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true, AutoGenerateColumns = false };
        dgv.ApplyModernStyle();

        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DateStr", HeaderText = "تاریخ", FillWeight = 20 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "مبلغ", FillWeight = 30 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PaymentType", HeaderText = "نوع پرداخت", FillWeight = 20 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 30 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "وضعیت", FillWeight = 20 });

        dgv.DataSource = dt;

        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgv.Columns[e.ColumnIndex].DataPropertyName;

            if (colName == "Amount" && e.Value is decimal d)
            {
                e.Value = NumberHelper.FormatThousands(d, usePersianDigits: true);
                e.FormattingApplied = true;
            }

            if (colName == "Status")
            {
                string status = e.Value?.ToString() ?? "";
                if (status != "معتبر") { e.CellStyle.BackColor = ModernTheme.DangerSoft; e.CellStyle.ForeColor = ModernTheme.DangerText; }
                else { e.CellStyle.BackColor = ModernTheme.SuccessSoft; }
            }
        };

        var btnSave = ModernButton.Create("انتقال ردیف‌های معتبر به دیتابیس", ModernTheme.Success, 300, 50);
        btnSave.Dock = DockStyle.Bottom;

        btnSave.Click += async (s, ev) =>
        {
            var validRows = dt.Rows.Cast<DataRow>().Where(r => r["Status"].ToString() == "معتبر").ToList();
            if (validRows.Count == 0) { ModernMessageBox.ShowWarning("هیچ ردیف معتبری برای ثبت وجود ندارد."); return; }
            if (!ModernMessageBox.AskConfirm($"آیا از ثبت {validRows.Count} دریافتی مطمئن هستید؟", "تایید ایمپورت")) return;

            bool success = await AsyncHelper.RunWithLoading(previewForm, async () =>
            {
                // ✅ این بخش هم اصلاح شد تا از جدول استاندارد استفاده کند
                var accounts = await new BankAccountRepository(_connectionString).GetActiveAsync();
                var targetAcc = accounts.FirstOrDefault();

                if (targetAcc is not null)
                {
                    foreach (var row in validRows)
                    {
                        var trans = new CashTransaction
                        {
                            ContractId = _contract.Id,
                            TransactionDate = Convert.ToDateTime(row["Date"]),
                            Amount = Convert.ToDecimal(row["Amount"]),
                            TransactionType = row["PaymentType"].ToString()!,
                            Direction = DirectionTypes.Incoming,
                            AccountId = targetAcc.Id,
                            AccountTitle = targetAcc.Title,
                            Category = "دریافت از کارفرما",
                            CostType = "خدمات",
                            Description = row["Description"].ToString()!
                        };
                        await _cashRepo.AddAsync(trans);
                    }
                }
            }, "در حال ثبت دریافتی‌ها...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show($"تعداد {validRows.Count} دریافتی با موفقیت ثبت شد.", isSuccess: true);
                previewForm.Close();
                await LoadDataSafeAsync();
            }
        };

        previewForm.Controls.Add(dgv);
        previewForm.Controls.Add(btnSave);
        previewForm.ShowDialog(this);
    }

    private void SetupPrintDocument()
    {
        singleStatementPrintDoc = new PrintDocument();
        singleStatementPrintDoc.DefaultPageSettings.Landscape = true;
        singleStatementPrintDoc.BeginPrint += (s, e) => _printRowIndex = 0;
        singleStatementPrintDoc.PrintPage += SingleStatementPrintDoc_PrintPage;

        paymentPrintDoc = new PrintDocument();
        paymentPrintDoc.DefaultPageSettings.Landscape = false;
        paymentPrintDoc.PrintPage += PaymentPrintDoc_PrintPage;
    }

    private void BtnPrintSingleStatement_Click(object? sender, EventArgs e)
    {
        if (dgvStatements.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("هیچ صورت‌وضعیتی برای پرینت وجود ندارد.");
            return;
        }

        using var preview = new PrintPreviewDialog
        {
            Document = singleStatementPrintDoc,
            Width = 1000,
            Height = 800,
            WindowState = FormWindowState.Maximized,
            UseAntiAlias = true,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true
        };
        preview.ShowDialog(this);
    }

    private void SingleStatementPrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        Graphics g = e.Graphics!;
        // ✅ جبران حاشیه‌ی سخت‌افزاری چاپگر (فقط در چاپ واقعی؛ در پیش‌نمایش لازم نیست)
        if (!((sender as PrintDocument)?.PrintController?.IsPreview ?? false))
            g.TranslateTransform(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);
        float pageWidth = e.PageBounds.Width;
        float pageHeight = e.PageBounds.Height;
        float margin = 40f;
        float yPos = margin;

        var fontTitle = ModernTheme.GetFont(20, FontStyle.Bold);
        var fontHeader = ModernTheme.GetFont(9f, FontStyle.Bold);
        var fontBody = ModernTheme.GetFont(9f);
        var fontMeta = ModernTheme.GetFont(10f, FontStyle.Bold);

        using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
        using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
        using StringFormat titleFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

        if (_printRowIndex == 0)
        {
            if (_companyLogo is not null)
            {
                try { g.DrawImage(_companyLogo, pageWidth - margin - 90, yPos, 90, 70); }
                catch (ArgumentException) { }
            }

            var fontDate = ModernTheme.GetFont(9, FontStyle.Regular);
            g.DrawString($"تاریخ چاپ: {DateHelper.ToPersianDate(DateTime.Now)}", fontDate, Brushes.Gray, new RectangleF(margin, yPos + 25, 150, 20), rtlFormatRight);
            g.DrawString("گزارش لیست صورت‌وضعیت‌های کارفرما", fontTitle, Brushes.Black, new RectangleF(margin, yPos + 15, pageWidth - margin * 2, 35), titleFormat);
            yPos += 80;

            string metaLine1 = $"کارفرما: {_contract.DisplayName}   |   شماره قرارداد: {_contract.ContractNumber}   |   تاریخ قرارداد: {DateHelper.ToPersianDate(_contract.ContractDate)}";
            g.DrawString(metaLine1, fontMeta, Brushes.Black, new RectangleF(0, yPos, pageWidth, 20), titleFormat);
            yPos += 25;

            string metaLine2 = $"موضوع: {_contract.Subject}   |   مبلغ کل قرارداد: {NumberHelper.FormatThousands(_contract.Amount, usePersianDigits: true)} ریال";
            g.DrawString(metaLine2, fontMeta, Brushes.Black, new RectangleF(0, yPos, pageWidth, 20), titleFormat);
            yPos += 25;

            decimal printRecoveredAdvance = 0;
            foreach (DataGridViewRow r in dgvStatements.Rows)
            {
                printRecoveredAdvance += NumberHelper.ParseFormatted(r.Cells["AdvanceRecovery"].Value?.ToString());
            }
            decimal printRemainingAdvance = _contract.TotalAdvanceAmount - printRecoveredAdvance;

            string metaLine3 = $"کل پیش‌دریافت: {NumberHelper.FormatThousands(_contract.TotalAdvanceAmount, usePersianDigits: true)}   |   کسر شده: {NumberHelper.FormatThousands(printRecoveredAdvance, usePersianDigits: true)}   |   مانده: {NumberHelper.FormatThousands(Math.Max(0, printRemainingAdvance), usePersianDigits: true)}";
            g.DrawString(metaLine3, fontMeta, Brushes.Black, new RectangleF(0, yPos, pageWidth, 20), titleFormat);
            yPos += 30;

            g.DrawLine(new Pen(Color.Black, 1.5f), margin, yPos, pageWidth - margin, yPos);
            yPos += 15;
        }
        else
        {
            g.DrawString($"ادامه لیست - قرارداد {_contract.ContractNumber}", fontHeader, Brushes.Black, new RectangleF(margin, yPos, pageWidth - margin * 2, 20), rtlFormatRight);
            yPos += 30;
        }

        string[] headers = { "شماره", "کارکرد", "ارزش افزوده", "جمع کل", "حسن انجام کار", "بیمه", "پیش‌دریافت", "سایر کسورات", "جمع کسورات", "خالص دریافتی" };
        float[] colWidths = { 35f, 95f, 75f, 95f, 80f, 70f, 80f, 75f, 85f, 100f };

        float baseWidth = colWidths.Sum();
        float tableWidth = pageWidth - (margin * 2);
        float scaleFactor = tableWidth / baseWidth;
        for (int i = 0; i < colWidths.Length; i++) colWidths[i] *= scaleFactor;

        float rowHeight = 25f;
        float headerRowHeight = 30f;

        g.FillRectangle(Brushes.LightGray, margin, yPos, tableWidth, headerRowHeight);
        g.DrawRectangle(Pens.Black, margin, yPos, tableWidth, headerRowHeight);

        float currentX = margin + tableWidth;

        for (int i = 0; i < headers.Length; i++)
        {
            if (i > 0) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + headerRowHeight);
            currentX -= colWidths[i];
            g.DrawString(headers[i], fontHeader, Brushes.Black, new RectangleF(currentX, yPos, colWidths[i], headerRowHeight), rtlFormatCenter);
        }
        yPos += headerRowHeight;

        decimal sumGross = 0, sumVat = 0, sumTotal = 0, sumRet = 0, sumIns = 0, sumAdv = 0, sumOther = 0, sumDed = 0, sumNet = 0;

        while (_printRowIndex < dgvStatements.Rows.Count)
        {
            if (yPos + rowHeight > pageHeight - 80)
            {
                e.HasMorePages = true;
                return;
            }

            var row = dgvStatements.Rows[_printRowIndex];

            decimal ret = NumberHelper.ParseFormatted(row.Cells["RetentionDeduction"].Value?.ToString());
            decimal ins = NumberHelper.ParseFormatted(row.Cells["InsuranceDeduction"].Value?.ToString());
            decimal adv = NumberHelper.ParseFormatted(row.Cells["AdvanceRecovery"].Value?.ToString());
            decimal oth = NumberHelper.ParseFormatted(row.Cells["OtherDeductions"].Value?.ToString());
            decimal ded = NumberHelper.ParseFormatted(row.Cells["Deductions"].Value?.ToString());

            if (ded == 0) ded = ret + ins + adv + oth;

            sumGross += NumberHelper.ParseFormatted(row.Cells["GrossAmount"].Value?.ToString());
            sumVat += NumberHelper.ParseFormatted(row.Cells["VAT"].Value?.ToString());
            sumTotal += NumberHelper.ParseFormatted(row.Cells["TotalAmount"].Value?.ToString());
            sumRet += ret;
            sumIns += ins;
            sumAdv += adv;
            sumOther += oth;
            sumDed += ded;
            sumNet += NumberHelper.ParseFormatted(row.Cells["NetReceivableAmount"].Value?.ToString());

            currentX = margin + tableWidth;
            g.DrawRectangle(Pens.Black, margin, yPos, tableWidth, rowHeight);

            for (int i = 0; i < 10; i++)
            {
                if (i > 0) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + rowHeight);
                currentX -= colWidths[i];

                string cellValue = "";
                switch (i)
                {
                    case 0: cellValue = row.Cells["RowNum"].Value?.ToString() ?? ""; break;
                    case 1: cellValue = row.Cells["GrossAmount"].Value?.ToString() ?? ""; break;
                    case 2: cellValue = row.Cells["VAT"].Value?.ToString() ?? ""; break;
                    case 3: cellValue = row.Cells["TotalAmount"].Value?.ToString() ?? ""; break;
                    case 4: cellValue = NumberHelper.FormatThousands(ret, usePersianDigits: true); break;
                    case 5: cellValue = NumberHelper.FormatThousands(ins, usePersianDigits: true); break;
                    case 6: cellValue = NumberHelper.FormatThousands(adv, usePersianDigits: true); break;
                    case 7: cellValue = NumberHelper.FormatThousands(oth, usePersianDigits: true); break;
                    case 8: cellValue = NumberHelper.FormatThousands(ded, usePersianDigits: true); break;
                    case 9: cellValue = row.Cells["NetReceivableAmount"].Value?.ToString() ?? ""; break;
                }

                g.DrawString(cellValue, fontBody, Brushes.Black, new RectangleF(currentX, yPos, colWidths[i], rowHeight), rtlFormatCenter);
            }

            yPos += rowHeight;
            _printRowIndex++;
        }

        if (_printRowIndex >= dgvStatements.Rows.Count)
        {
            g.FillRectangle(Brushes.Beige, margin, yPos, tableWidth, rowHeight);
            g.DrawRectangle(Pens.Black, margin, yPos, tableWidth, rowHeight);

            currentX = margin + tableWidth;

            string[] totalValues = {
                "جمع کل",
                StringHelper.ConvertToPersianDigits(sumGross.ToString("N0")),
                StringHelper.ConvertToPersianDigits(sumVat.ToString("N0")),
                StringHelper.ConvertToPersianDigits(sumTotal.ToString("N0")),
                StringHelper.ConvertToPersianDigits(sumRet.ToString("N0")),
                StringHelper.ConvertToPersianDigits(sumIns.ToString("N0")),
                StringHelper.ConvertToPersianDigits(sumAdv.ToString("N0")),
                StringHelper.ConvertToPersianDigits(sumOther.ToString("N0")),
                StringHelper.ConvertToPersianDigits(sumDed.ToString("N0")),
                StringHelper.ConvertToPersianDigits(sumNet.ToString("N0"))
            };

            for (int i = 0; i < 10; i++)
            {
                if (i > 0) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + rowHeight);
                currentX -= colWidths[i];
                g.DrawString(totalValues[i], fontHeader, Brushes.Black, new RectangleF(currentX, yPos, colWidths[i], rowHeight), rtlFormatCenter);
            }
            yPos += rowHeight + 40;

            float signWidth = 150f;
            float signY = pageHeight - margin - 60;

            g.DrawString("مهر و امضای کارفرما", fontHeader, Brushes.Black, new RectangleF(pageWidth - margin - signWidth, signY, signWidth, 20), rtlFormatCenter);
            g.DrawLine(Pens.Black, pageWidth - margin - signWidth, signY + 50, pageWidth - margin, signY + 50);

            g.DrawString("مهر و امضای ناظر", fontHeader, Brushes.Black, new RectangleF(pageWidth / 2 - signWidth / 2, signY, signWidth, 20), rtlFormatCenter);
            g.DrawLine(Pens.Black, pageWidth / 2 - signWidth / 2, signY + 50, pageWidth / 2 + signWidth / 2, signY + 50);

            g.DrawString("مهر و امضای پیمانکار", fontHeader, Brushes.Black, new RectangleF(margin, signY, signWidth, 20), rtlFormatCenter);
            g.DrawLine(Pens.Black, margin, signY + 50, margin + signWidth, signY + 50);
        }

        e.HasMorePages = false;
    }

    private void BtnPrintPayment_Click(object? sender, EventArgs e)
    {
        if (dgvPayments.CurrentRow is null) { ModernMessageBox.ShowWarning("لطفاً یک ردیف از دریافتی‌ها را انتخاب کنید."); return; }

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

        if (_companyLogo is not null)
        {
            try { g.DrawImage(_companyLogo, pageWidth - margin - 100, yPos + 10, 100, 80); }
            catch (ArgumentException) { }
        }

        g.DrawString("رسید دریافت وجه", fontTitle, Brushes.Black, new RectangleF(margin, yPos + 30, pageWidth - margin * 2, 40), rtlFormatCenter);
        g.DrawLine(Pens.Black, margin, yPos + 90, pageWidth - margin, yPos + 90);
        yPos += 110;

        g.DrawString($"تاریخ: {_paymentDateForPrint}", fontHeader, Brushes.Black, new RectangleF(margin + 20, yPos, 200, 25), rtlFormatRight);
        g.DrawString($"شماره قرارداد: {_contract.ContractNumber}", fontHeader, Brushes.Black, new RectangleF(pageWidth / 2, yPos, pageWidth / 2 - margin - 20, 25), rtlFormatRight);
        yPos += 40;

        g.DrawString($"از طرف: {_contract.DisplayName} (کارفرما)", fontBody, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 35;

        g.DrawString($"مبلغ: {NumberHelper.FormatThousands(_paymentAmountForPrint, usePersianDigits: true)} ریال", fontBody, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 35;

        g.DrawString($"به حروف: {NumberHelper.ToPersianWords(_paymentAmountForPrint)} ریال", fontHeader, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 35;

        g.DrawString($"بابت: دریافت وجه بابت قرارداد فوق ({_paymentTypeForPrint}) {_paymentDescForPrint}", fontBody, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 35;

        g.DrawString("مبلغ فوق دریافت گردید.", fontBody, Brushes.Black, new RectangleF(margin + 20, yPos, pageWidth - margin * 2 - 40, 25), rtlFormatRight);
        yPos += 60;

        float signY = pageHeight - margin - 100;
        float signWidth = 150f;

        g.DrawString("مهر و امضای دریافت‌کننده (پیمانکار)", fontHeader, Brushes.Black, new RectangleF(pageWidth - margin - signWidth - 20, signY, signWidth + 20, 20), rtlFormatCenter);
        g.DrawLine(Pens.Black, pageWidth - margin - signWidth - 20, signY + 60, pageWidth - margin - 20, signY + 60);

        g.DrawString("مهر و امضای پرداخت‌کننده (کارفرما)", fontHeader, Brushes.Black, new RectangleF(margin + 20, signY, signWidth + 20, 20), rtlFormatCenter);
        g.DrawLine(Pens.Black, margin + 20, signY + 60, margin + 20 + signWidth + 20, signY + 60);
    }

    private class UnifiedPaymentViewModel
    {
        public int Id { get; set; }
        public string TableName { get; set; } = "";
        public string PaymentType { get; set; } = "";
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = "";
        public string Status { get; set; } = "";
        public string? CostType { get; set; }
    }
}
