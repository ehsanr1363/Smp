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
using SMP.Models;
using SMP.Helpers;
using SMP.Data;

namespace SMP.Forms
{
    public class ClientContractReportForm : Form
    {
        private readonly string _connectionString;
        private readonly ClientContract _contract;
        private readonly CashTransactionRepository _cashRepo;
        private readonly CashCheckRepository _checkRepo;

        private TabControl tabControl = null!;
        private DataGridView dgvStatements = null!;
        private DataGridView dgvPayments = null!;
        private DataGridView dgvContractorContracts = null!;
        private DataGridView dgvExpenses = null!;
        private DataGridView dgvAllProjectPayments = null!;

        private ClientChartPanel chartPanel = null!;

        private Label lblSummaryTotalContract = null!, lblSummaryTotalWork = null!, lblSummaryRemaining = null!, lblSummaryProgress = null!;
        private Label lblSummaryTotalPaid = null!, lblSummaryDebt = null!;
        private Label lblSummaryContractorTotal = null!, lblSummaryProfit = null!, lblSummaryExpenses = null!;
        private Label lblSummaryTotalAdvance = null!, lblSummaryRecoveredAdvance = null!, lblSummaryRemainingAdvance = null!;
        private Label lblSummaryProjectDirect = null!, lblSummaryInformalContractor = null!;

        private Label lblSummaryBilled = null!, lblSummaryUnbilled = null!;
        private decimal _totalBilledExpenses = 0, _totalUnbilledExpenses = 0;

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
                try { singleStatementPrintDoc?.Dispose(); } catch { }
                try { paymentPrintDoc?.Dispose(); } catch { }
                try { _companyLogo?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, $"گزارش مالی کارفرما - {_contract.ContractNumber}", new Size(1450, 880));
            this.MaximumSize = new Size(1600, 950);

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // ==========================================
            // ۱. هدر: اطلاعات قرارداد
            // ==========================================
            var groupBoxHeader = new GroupBox
            {
                Text = "اطلاعات قرارداد کارفرما",
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                Padding = new Padding(10)
            };

            var headerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            picCompanyLogo = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(10)
            };
            headerLayout.Controls.Add(picCompanyLogo, 0, 0);

            var infoLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 3 };
            infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            for (int i = 0; i < 3; i++) infoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));

            void AddInfoLabel(TableLayoutPanel layout, string text, int col, int row, bool isBold, int rightMargin = 5)
            {
                var lbl = new Label
                {
                    Text = text,
                    Font = UIHelper.PersianFont(11f, isBold ? FontStyle.Bold : FontStyle.Regular),
                    ForeColor = isBold ? UIHelper.WarmText : Color.Black,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(5, 0, rightMargin, 0)
                };
                layout.Controls.Add(lbl, col, row);
            }

            AddInfoLabel(infoLayout, "کارفرما:", 0, 0, true);
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

            headerLayout.Controls.Add(infoLayout, 1, 0);
            groupBoxHeader.Controls.Add(headerLayout);

            // ==========================================
            // ۲. خلاصه وضعیت مالی
            // ==========================================
            var groupBoxSummary = new GroupBox
            {
                Text = "خلاصه وضعیت مالی",
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(10f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                Padding = new Padding(10, 5, 10, 5)
            };

            var summaryLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 2, Padding = new Padding(2) };
            for (int i = 0; i < 8; i++) summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            lblSummaryTotalContract = CreateSummaryLabel("مبلغ قرارداد: ");
            lblSummaryTotalWork = CreateSummaryLabel("کارکرد: ");
            lblSummaryRemaining = CreateSummaryLabel("مانده قرارداد: ");
            lblSummaryProgress = CreateSummaryLabel("پیشرفت: ");
            lblSummaryTotalAdvance = CreateSummaryLabel("کل پیش‌دریافت: ");
            lblSummaryRecoveredAdvance = CreateSummaryLabel("پیش‌دریافت کسرشده: ");
            lblSummaryRemainingAdvance = CreateSummaryLabel("مانده پیش‌دریافت: ");
            lblSummaryBilled = CreateSummaryLabel("صورت وضعیت شده: ");

            lblSummaryTotalPaid = CreateSummaryLabel("دریافتی: ");
            lblSummaryDebt = CreateSummaryLabel("مانده طلب: ");
            lblSummaryProjectDirect = CreateSummaryLabel("خرید مستقیم: ");
            lblSummaryInformalContractor = CreateSummaryLabel("پیمانکار بدون قرارداد: ");
            lblSummaryContractorTotal = CreateSummaryLabel("پیمانکار رسمی: ");
            lblSummaryExpenses = CreateSummaryLabel("هزینه فاکتوری: ");
            lblSummaryProfit = CreateSummaryLabel("سود ناخالص: ");
            lblSummaryUnbilled = CreateSummaryLabel("صورت وضعیت نشده: ");

            summaryLayout.Controls.Add(lblSummaryTotalContract, 0, 0);
            summaryLayout.Controls.Add(lblSummaryTotalWork, 1, 0);
            summaryLayout.Controls.Add(lblSummaryRemaining, 2, 0);
            summaryLayout.Controls.Add(lblSummaryProgress, 3, 0);
            summaryLayout.Controls.Add(lblSummaryTotalAdvance, 4, 0);
            summaryLayout.Controls.Add(lblSummaryRecoveredAdvance, 5, 0);
            summaryLayout.Controls.Add(lblSummaryRemainingAdvance, 6, 0);
            summaryLayout.Controls.Add(lblSummaryBilled, 7, 0);

            summaryLayout.Controls.Add(lblSummaryTotalPaid, 0, 1);
            summaryLayout.Controls.Add(lblSummaryDebt, 1, 1);
            summaryLayout.Controls.Add(lblSummaryProjectDirect, 2, 1);
            summaryLayout.Controls.Add(lblSummaryInformalContractor, 3, 1);
            summaryLayout.Controls.Add(lblSummaryContractorTotal, 4, 1);
            summaryLayout.Controls.Add(lblSummaryExpenses, 5, 1);
            summaryLayout.Controls.Add(lblSummaryProfit, 6, 1);
            summaryLayout.Controls.Add(lblSummaryUnbilled, 7, 1);

            groupBoxSummary.Controls.Add(summaryLayout);

            // ==========================================
            // ۳. تب‌ها
            // ==========================================
            tabControl = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
            ModernLayoutHelper.StyleModernTabControl(tabControl);

            TabPage tabStatements = new TabPage("📄 صورت وضعیت‌های صادره");
            TabPage tabPayments = new TabPage("💳 دریافتی‌ها");
            TabPage tabContractors = new TabPage("📝 قراردادهای پیمانکاران");
            TabPage tabExpenses = new TabPage("🧾 هزینه‌های فاکتوری");
            TabPage tabAllPayments = new TabPage("📊 همه پرداخت‌های پروژه");
            TabPage tabChart = new TabPage("📈 نمودار مالی");

            // ═══════════════════════════════════════════
            // تب ۱: صورت وضعیت‌ها
            // ═══════════════════════════════════════════
            dgvStatements = CreateGrid();
            dgvStatements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            dgvStatements.Columns.Add("RowNum", "شماره");
            dgvStatements.Columns.Add("GrossAmount", "مبلغ کارکرد");
            dgvStatements.Columns.Add("VAT", "ارزش افزوده");
            dgvStatements.Columns.Add("TotalAmount", "جمع کل");
            dgvStatements.Columns.Add("RetentionDeduction", "حسن انجام کار");
            dgvStatements.Columns.Add("InsuranceDeduction", "بیمه");
            dgvStatements.Columns.Add("AdvanceRecovery", "پیش‌دریافت");
            dgvStatements.Columns.Add("OtherDeductions", "سایر کسورات");
            dgvStatements.Columns.Add("Deductions", "جمع کسورات");
            dgvStatements.Columns.Add("NetReceivableAmount", "خالص دریافتی");

            var tabStLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tabStLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabStLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));

            var stFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 15, 0, 0), WrapContents = false, AutoScroll = true };

            var btnAddSt = UIHelper.CreateButton("➕ ثبت صورت وضعیت", UIHelper.WarmPrimary, 160, 35);
            btnAddSt.Click += (s, e) => { using var f = new ClientStatementEditForm(_connectionString, _contract); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };
            var btnEditSt = UIHelper.CreateButton("✏️ ویرایش", UIHelper.WarmSecondary, 120, 35);
            btnEditSt.Click += EditSelectedStatement;
            var btnDelSt = UIHelper.CreateButton("🗑️ حذف", UIHelper.WarmDanger, 120, 35);
            btnDelSt.Click += DeleteSelectedStatement;
            var btnPrintSt = UIHelper.CreateButton("🖨️ پرینت لیست", UIHelper.SectionBlue, 180, 35);
            btnPrintSt.Click += BtnPrintSingleStatement_Click;
            var btnDownloadStTemplate = UIHelper.CreateButton("📥 دانلود قالب اکسل", UIHelper.SectionTeal, 160, 35);
            btnDownloadStTemplate.Click += BtnDownloadStTemplate_Click;
            var btnImportStExcel = UIHelper.CreateButton("📤 ایمپورت از اکسل", UIHelper.ActionGreen, 160, 35);
            btnImportStExcel.Click += BtnImportStExcel_Click;

            stFlow.Controls.Add(btnAddSt);
            stFlow.Controls.Add(btnEditSt);
            stFlow.Controls.Add(btnDelSt);
            stFlow.Controls.Add(btnPrintSt);
            stFlow.Controls.Add(btnDownloadStTemplate);
            stFlow.Controls.Add(btnImportStExcel);

            tabStLayout.Controls.Add(dgvStatements, 0, 0);
            tabStLayout.Controls.Add(stFlow, 0, 1);
            tabStatements.Controls.Add(tabStLayout);

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

            dgvPayments.CellPainting += DgvPayments_CellPainting;

            var tabPayLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tabPayLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabPayLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));

            var payFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 15, 0, 0), WrapContents = false, AutoScroll = true };

            var btnAddPay = UIHelper.CreateButton("➕ ثبت دریافتی جدید", UIHelper.ActionGreen, 180, 35);
            btnAddPay.Click += (s, e) => { using var f = new ClientPaymentForm(_connectionString, _contract); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };
            var btnDeletePay = UIHelper.CreateButton("🗑️ حذف دریافتی", UIHelper.WarmDanger, 150, 35);
            btnDeletePay.Click += BtnDeletePay_Click;
            var btnPrintPay = UIHelper.CreateButton("🖨️ پرینت رسید دریافت", UIHelper.SectionBlue, 180, 35);
            btnPrintPay.Click += BtnPrintPayment_Click;
            var btnDownloadPayTemplate = UIHelper.CreateButton("📥 قالب اکسل دریافتی‌ها", UIHelper.SectionTeal, 180, 35);
            btnDownloadPayTemplate.Click += BtnDownloadPayTemplate_Click;
            var btnImportPayExcel = UIHelper.CreateButton("📤 ایمپورت دریافتی‌ها", UIHelper.ActionGreen, 180, 35);
            btnImportPayExcel.Click += BtnImportPayExcel_Click;

            payFlow.Controls.Add(btnAddPay);
            payFlow.Controls.Add(btnDeletePay);
            payFlow.Controls.Add(btnPrintPay);
            payFlow.Controls.Add(btnDownloadPayTemplate);
            payFlow.Controls.Add(btnImportPayExcel);

            tabPayLayout.Controls.Add(dgvPayments, 0, 0);
            tabPayLayout.Controls.Add(payFlow, 0, 1);
            tabPayments.Controls.Add(tabPayLayout);

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

            var tabConLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tabConLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabConLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            var conFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 10, 0, 0), WrapContents = false, AutoScroll = true };
            var btnAddContractor = UIHelper.CreateButton("➕ افزودن قرارداد پیمانکار جدید", UIHelper.WarmPrimary, 220, 35);
            btnAddContractor.Click += (s, e) => { using var f = new ContractorContractEditForm(_connectionString); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };
            var btnOpenContractorReport = UIHelper.CreateButton("📊 مشاهده گزارش پیمانکار", UIHelper.SectionBlue, 200, 35);
            btnOpenContractorReport.Click += async (s, e) => await OpenSelectedContractorReportAsync();
            conFlow.Controls.Add(btnAddContractor);
            conFlow.Controls.Add(btnOpenContractorReport);
            tabConLayout.Controls.Add(dgvContractorContracts, 0, 0);
            tabConLayout.Controls.Add(conFlow, 0, 1);
            tabContractors.Controls.Add(tabConLayout);

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

            var tabExpLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tabExpLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabExpLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            var expFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 10, 0, 0), WrapContents = false, AutoScroll = true };
            var btnAddExp = UIHelper.CreateButton("➕ ثبت فاکتور هزینه", UIHelper.WarmPrimary, 180, 35);
            btnAddExp.Click += (s, e) => { using var f = new ProjectExpenseEditForm(_connectionString, _contract); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };
            var btnDelExp = UIHelper.CreateButton("🗑️ حذف فاکتور", UIHelper.WarmDanger, 120, 35);
            btnDelExp.Click += DeleteSelectedExpense;
            expFlow.Controls.Add(btnAddExp);
            expFlow.Controls.Add(btnDelExp);
            tabExpLayout.Controls.Add(dgvExpenses, 0, 0);
            tabExpLayout.Controls.Add(expFlow, 0, 1);
            tabExpenses.Controls.Add(tabExpLayout);

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

            dgvAllProjectPayments.CellPainting += DgvAllProjectPayments_CellPainting;
            dgvAllProjectPayments.CellDoubleClick += DgvAllProjectPayments_CellDoubleClick;

            dgvAllProjectPayments.RowPrePaint += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var scopeCell = dgvAllProjectPayments.Rows[e.RowIndex].Cells["Scope"].Value?.ToString() ?? "";
                if (scopeCell.Contains("خرید مستقیم"))
                    dgvAllProjectPayments.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(240, 249, 255);
                else if (scopeCell.Contains("بدون قرارداد"))
                    dgvAllProjectPayments.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 251, 235);
                else if (scopeCell.Contains("رسمی"))
                    dgvAllProjectPayments.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(245, 255, 245);
            };

            var tabAllPayLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tabAllPayLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabAllPayLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));

            var allPayFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 15, 0, 0), WrapContents = false, AutoScroll = true };
            var btnAddProjectPay = UIHelper.CreateButton("➕ ثبت پرداخت جدید پروژه", UIHelper.ActionGreen, 200, 35);
            btnAddProjectPay.Click += (s, e) => { using var f = new CashTransactionEditForm(_connectionString); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };

            var btnPrintAllPay = UIHelper.CreateButton("🖨️ پرینت گزارش پرداخت‌ها", UIHelper.SectionBlue, 200, 35);
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

            var btnExportAllPay = UIHelper.CreateButton("📥 خروجی اکسل", UIHelper.SectionTeal, 150, 35);
            btnExportAllPay.Click += (s, e) => ExportAllProjectPaymentsToExcel();
            allPayFlow.Controls.Add(btnAddProjectPay);
            allPayFlow.Controls.Add(btnPrintAllPay);
            allPayFlow.Controls.Add(btnExportAllPay);

            tabAllPayLayout.Controls.Add(dgvAllProjectPayments, 0, 0);
            tabAllPayLayout.Controls.Add(allPayFlow, 0, 1);
            tabAllPayments.Controls.Add(tabAllPayLayout);

            // ═══════════════════════════════════════════
            // تب ۶: نمودار مالی
            // ═══════════════════════════════════════════
            chartPanel = new ClientChartPanel { Dock = DockStyle.Fill, Margin = new Padding(10) };
            tabChart.Controls.Add(chartPanel);

            tabControl.TabPages.Add(tabStatements);
            tabControl.TabPages.Add(tabPayments);
            tabControl.TabPages.Add(tabContractors);
            tabControl.TabPages.Add(tabExpenses);
            tabControl.TabPages.Add(tabAllPayments);
            tabControl.TabPages.Add(tabChart);

            mainLayout.Controls.Add(groupBoxHeader, 0, 0);
            mainLayout.Controls.Add(groupBoxSummary, 0, 1);
            mainLayout.Controls.Add(tabControl, 0, 2);

            Controls.Add(mainLayout);
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

            if (row.Cells["Id"].Value == null || row.Cells["Id"].Value == DBNull.Value) return;

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
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None
            };
            dgv.ApplyModernStyle();
            return dgv;
        }

        private Label CreateSummaryLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(9.5f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                BackColor = UIHelper.WarmPanelBackground,
                Margin = new Padding(2)
            };
        }

        private void LoadCompanyLogo()
        {
            try
            {
                var companyRepo = new CompanyRepository(_connectionString);
                var company = companyRepo.GetAll().FirstOrDefault();

                if (company != null && company.Logo != null && company.Logo.Length > 0)
                {
                    using var ms = new MemoryStream(company.Logo);
                    using var img = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false);
                    _companyLogo = new Bitmap(img);

                    if (picCompanyLogo != null && !picCompanyLogo.IsDisposed)
                    {
                        picCompanyLogo.Image = _companyLogo;
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

                lblSummaryTotalContract.Text = $"مبلغ قرارداد: {StringHelper.ConvertToPersianDigits(totalContractAmount.ToString("N0"))}";
                lblSummaryTotalWork.Text = $"کارکرد: {StringHelper.ConvertToPersianDigits(totalWorkDone.ToString("N0"))}";
                lblSummaryRemaining.Text = $"مانده قرارداد: {StringHelper.ConvertToPersianDigits(Math.Max(0, remainingAmount).ToString("N0"))}";
                lblSummaryProgress.Text = $"پیشرفت: {StringHelper.ConvertToPersianDigits(progressPercent.ToString("N1"))}%";
                lblSummaryProgress.ForeColor = progressPercent > 100 ? UIHelper.WarmDanger : UIHelper.ActionGreen;

                lblSummaryTotalAdvance.Text = $"کل پیش‌دریافت: {StringHelper.ConvertToPersianDigits(contractAdvance.ToString("N0"))}";
                lblSummaryTotalAdvance.ForeColor = UIHelper.SectionBlue;
                lblSummaryRecoveredAdvance.Text = $"پیش‌دریافت کسرشده: {StringHelper.ConvertToPersianDigits(recoveredAdvance.ToString("N0"))}";
                lblSummaryRecoveredAdvance.ForeColor = UIHelper.ActionGreen;
                lblSummaryRemainingAdvance.Text = $"مانده پیش‌دریافت: {StringHelper.ConvertToPersianDigits(Math.Max(0, remainingAdvance).ToString("N0"))}";
                lblSummaryRemainingAdvance.ForeColor = remainingAdvance > 0 ? UIHelper.WarmDanger : UIHelper.ActionGreen;

                lblSummaryTotalPaid.Text = $"دریافتی: {StringHelper.ConvertToPersianDigits(totalReceived.ToString("N0"))}";
                lblSummaryDebt.Text = $"مانده طلب: {StringHelper.ConvertToPersianDigits(Math.Max(0, debt).ToString("N0"))}";
                lblSummaryDebt.ForeColor = debt > 0 ? UIHelper.WarmDanger : UIHelper.ActionGreen;

                lblSummaryProjectDirect.Text = $"خرید مستقیم: {StringHelper.ConvertToPersianDigits(projectDirectExpenses.ToString("N0"))}";
                lblSummaryProjectDirect.ForeColor = UIHelper.SectionOrange;

                lblSummaryInformalContractor.Text = $"پیمانکار بدون قرارداد: {StringHelper.ConvertToPersianDigits(informalContractorPayments.ToString("N0"))}";
                lblSummaryInformalContractor.ForeColor = UIHelper.ActionAmber;

                lblSummaryContractorTotal.Text = $"پیمانکار رسمی: {StringHelper.ConvertToPersianDigits(formalContractorPayments.ToString("N0"))}";
                lblSummaryContractorTotal.ForeColor = UIHelper.SectionPurple;

                lblSummaryExpenses.Text = $"هزینه فاکتوری: {StringHelper.ConvertToPersianDigits(invoiceExpenses.ToString("N0"))}";
                lblSummaryExpenses.ForeColor = UIHelper.WarmDanger;

                lblSummaryProfit.Text = $"سود ناخالص: {StringHelper.ConvertToPersianDigits(grossProfit.ToString("N0"))}";
                lblSummaryProfit.ForeColor = grossProfit >= 0 ? UIHelper.ActionGreen : UIHelper.WarmDanger;

                lblSummaryBilled.Text = $"صورت وضعیت شده: {StringHelper.ConvertToPersianDigits(_totalBilledExpenses.ToString("N0"))}";
                lblSummaryBilled.ForeColor = UIHelper.ActionGreen;

                lblSummaryUnbilled.Text = $"صورت وضعیت نشده: {StringHelper.ConvertToPersianDigits(_totalUnbilledExpenses.ToString("N0"))}";
                lblSummaryUnbilled.ForeColor = UIHelper.ActionAmber;

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
            if (dgvContractorContracts.CurrentRow == null)
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

                if (!success || contract == null || IsDisposed) return;

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
                if (dgvPayments.CurrentRow == null)
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
                if (dgvStatements.CurrentRow == null) return;
                int id = Convert.ToInt32(dgvStatements.CurrentRow.Cells["Id"].Value);

                ClientStatement? st = null;
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var repo = new ClientStatementRepository(_connectionString);
                    st = await Task.Run(() => repo.GetById(id));
                }, "در حال دریافت اطلاعات...");

                if (!success || st == null || IsDisposed) return;

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
                if (dgvStatements.CurrentRow == null) return;
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
                if (dgvExpenses.CurrentRow == null) return;
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

            if (!success || dt == null || IsDisposed) return;

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
                else if (date == null) status = "خطا: تاریخ نامعتبر";
                else if (gross <= 0) status = "خطا: مبلغ کارکرد نامعتبر";

                dt.Rows.Add(stNum, dateStr, date ?? DateTime.Now, gross, vat, ret, ins, adv, other, ded, net, status);
            }

            return dt;
        }

        private void ShowStatementsPreviewForm(DataTable dt)
        {
            var previewForm = new Form();
            UIHelper.SetupForm(previewForm, "پیش‌نمایش صورت وضعیت‌های ایمپورت‌شده", new Size(1100, 600));
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
                    if (status != "معتبر") { e.CellStyle.BackColor = UIHelper.WarmErrorBackground; e.CellStyle.ForeColor = UIHelper.WarmErrorText; }
                    else { e.CellStyle.BackColor = UIHelper.WarmSuccessBackground; }
                }
            };

            var btnSave = UIHelper.CreateButton("انتقال ردیف‌های معتبر به دیتابیس", UIHelper.ActionGreen, 300, 50);
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

            if (!success || dt == null || IsDisposed) return;

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
                if (date == null) status = "خطا: تاریخ نامعتبر";
                else if (amount <= 0) status = "خطا: مبلغ نامعتبر";
                else if (string.IsNullOrWhiteSpace(typeStr)) typeStr = "نقدی";

                dt.Rows.Add(dateStr, date ?? DateTime.Now, amount, typeStr, string.IsNullOrWhiteSpace(desc) ? "علی‌الحساب" : desc, status);
            }

            return dt;
        }

        private void ShowPaymentsPreviewForm(DataTable dt)
        {
            var previewForm = new Form();
            UIHelper.SetupForm(previewForm, "پیش‌نمایش دریافتی‌های ایمپورت‌شده", new Size(800, 600));
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
                    if (status != "معتبر") { e.CellStyle.BackColor = UIHelper.WarmErrorBackground; e.CellStyle.ForeColor = UIHelper.WarmErrorText; }
                    else { e.CellStyle.BackColor = UIHelper.WarmSuccessBackground; }
                }
            };

            var btnSave = UIHelper.CreateButton("انتقال ردیف‌های معتبر به دیتابیس", UIHelper.ActionGreen, 300, 50);
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

                    if (targetAcc != null)
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
            float pageWidth = e.PageBounds.Width;
            float pageHeight = e.PageBounds.Height;
            float margin = 40f;
            float yPos = margin;

            var fontTitle = UIHelper.PersianFont(20, FontStyle.Bold);
            var fontHeader = UIHelper.PersianFont(9f, FontStyle.Bold);
            var fontBody = UIHelper.PersianFont(9f);
            var fontMeta = UIHelper.PersianFont(10f, FontStyle.Bold);

            using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
            using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
            using StringFormat titleFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

            if (_printRowIndex == 0)
            {
                if (_companyLogo != null)
                {
                    try { g.DrawImage(_companyLogo, pageWidth - margin - 90, yPos, 90, 70); }
                    catch (ArgumentException) { }
                }

                var fontDate = UIHelper.PersianFont(9, FontStyle.Regular);
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
            if (dgvPayments.CurrentRow == null) { ModernMessageBox.ShowWarning("لطفاً یک ردیف از دریافتی‌ها را انتخاب کنید."); return; }

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
            float pageWidth = e.PageBounds.Width;
            float pageHeight = e.PageBounds.Height;
            float margin = 50f;
            float yPos = margin;

            var fontTitle = UIHelper.PersianFont(18, FontStyle.Bold);
            var fontHeader = UIHelper.PersianFont(12, FontStyle.Bold);
            var fontBody = UIHelper.PersianFont(11f);

            using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
            using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };

            g.DrawRectangle(Pens.Black, margin, margin, pageWidth - margin * 2, pageHeight - margin * 2);

            if (_companyLogo != null)
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
}