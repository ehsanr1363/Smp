using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Models;
using PersonnelApp.Helpers;
using PersonnelApp.Data;

namespace PersonnelApp.Forms
{
    public class ContractorStatementReportForm : Form
    {
        private readonly string _connectionString;
        private readonly ContractorContract _contract;

        private TabControl tabControl = null!;
        private DataGridView dgvStatements = null!;
        private DataGridView dgvPayments = null!;
        private DataGridView dgvGuarantees = null!;

        private Label lblContractNumber = null!, lblContractDate = null!, lblEndDate = null!, lblContractorName = null!;
        private Label lblSubject = null!, lblTotalAmount = null!, lblAdvanceAmount = null!;
        private Label lblRetentionPercent = null!, lblInsurancePercent = null!, lblVatPercent = null!;
        private PictureBox pbLogo = null!;
        private Label lblLogoPlaceholder = null!;

        private Label lblSummaryTotalContract = null!, lblSummaryTotalWork = null!, lblSummaryRemaining = null!, lblSummaryProgress = null!;
        private Label lblSummaryTotalPaid = null!, lblSummaryDebt = null!;

        private Button btnAddStatement = null!, btnEditStatement = null!, btnDeleteStatement = null!;
        private Button btnPrintList = null!, btnPrintSingle = null!, btnExportExcel = null!;
        private Button btnAddPayment = null!, btnPrintPayment = null!;
        private Button btnAddGuarantee = null!, btnEditGuarantee = null!, btnDeleteGuarantee = null!;

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

            FormClosed += (s, e) =>
            {
                printDoc?.Dispose();
                paymentPrintDoc?.Dispose();
                singleStatementPrintDoc?.Dispose();
                pbLogo?.Image?.Dispose();
            };
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, $"گزارش مالی قرارداد - {_contract.ContractNumber}", new Size(1200, 850));
            this.MaximizeBox = true;

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            var groupBoxHeader = new GroupBox
            {
                Text = "اطلاعات قرارداد",
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                Padding = new Padding(10)
            };

            var headerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 3 };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            pbLogo = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0)
            };

            lblLogoPlaceholder = new Label { Text = "لوگو شرکت", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gray, BackColor = Color.Transparent };
            pbLogo.Controls.Add(lblLogoPlaceholder);

            headerLayout.Controls.Add(pbLogo, 0, 0);
            headerLayout.SetRowSpan(pbLogo, 3);

            lblContractNumber = CreateHeaderLabel();
            lblContractDate = CreateHeaderLabel();
            lblEndDate = CreateHeaderLabel();
            lblContractorName = CreateHeaderLabel();
            lblSubject = CreateHeaderLabel();
            lblTotalAmount = CreateHeaderLabel();
            lblAdvanceAmount = CreateHeaderLabel();
            lblRetentionPercent = CreateHeaderLabel();
            lblInsurancePercent = CreateHeaderLabel();
            lblVatPercent = CreateHeaderLabel();

            headerLayout.Controls.Add(lblContractorName, 1, 0);
            headerLayout.Controls.Add(lblContractNumber, 2, 0);
            headerLayout.Controls.Add(lblContractDate, 3, 0);
            headerLayout.Controls.Add(lblEndDate, 4, 0);

            headerLayout.Controls.Add(lblSubject, 1, 1);
            headerLayout.Controls.Add(lblTotalAmount, 2, 1);
            headerLayout.Controls.Add(lblAdvanceAmount, 3, 1);

            headerLayout.Controls.Add(lblRetentionPercent, 1, 2);
            headerLayout.Controls.Add(lblInsurancePercent, 2, 2);
            headerLayout.Controls.Add(lblVatPercent, 3, 2);

            groupBoxHeader.Controls.Add(headerLayout);

            var groupBoxSummary = new GroupBox
            {
                Text = "خلاصه وضعیت مالی",
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                Padding = new Padding(10, 5, 10, 5)
            };

            var summaryLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1 };
            for (int i = 0; i < 6; i++) summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66F));

            lblSummaryTotalContract = CreateSummaryLabel("مبلغ کل قرارداد: ");
            lblSummaryTotalWork = CreateSummaryLabel("مجموع کارکرد: ");
            lblSummaryRemaining = CreateSummaryLabel("مانده قرارداد: ");
            lblSummaryProgress = CreateSummaryLabel("درصد پیشرفت: ");
            lblSummaryTotalPaid = CreateSummaryLabel("مجموع پرداخت شده: ");
            lblSummaryDebt = CreateSummaryLabel("مانده بدهی: ");

            summaryLayout.Controls.Add(lblSummaryTotalContract, 0, 0);
            summaryLayout.Controls.Add(lblSummaryTotalWork, 1, 0);
            summaryLayout.Controls.Add(lblSummaryRemaining, 2, 0);
            summaryLayout.Controls.Add(lblSummaryProgress, 3, 0);
            summaryLayout.Controls.Add(lblSummaryTotalPaid, 4, 0);
            summaryLayout.Controls.Add(lblSummaryDebt, 5, 0);

            groupBoxSummary.Controls.Add(summaryLayout);

            tabControl = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
            ModernLayoutHelper.StyleModernTabControl(tabControl);

            TabPage tabStatements = new TabPage("📄 صورت وضعیت‌ها");
            TabPage tabPayments = new TabPage("💳 پرداخت‌ها");
            TabPage tabGuarantees = new TabPage("🏦 ضمانت‌نامه‌ها");

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

            dgvStatements.CellDoubleClick += DgvStatements_CellDoubleClick;
            dgvStatements.SelectionChanged += (s, e) => UpdateStatementButtons();
            tabStatements.Controls.Add(dgvStatements);

            dgvPayments = CreateGrid();
            dgvPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            dgvPayments.Columns.Add("PaymentDate", "تاریخ پرداخت");
            dgvPayments.Columns.Add("Amount", "مبلغ پرداختی");
            dgvPayments.Columns.Add("PaymentType", "نوع پرداخت");
            dgvPayments.Columns.Add("Description", "توضیحات");
            dgvPayments.SelectionChanged += (s, e) => btnPrintPayment.Enabled = dgvPayments.CurrentRow != null;

            var tabPaymentsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tabPaymentsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabPaymentsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            var paymentBtnFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10, 5, 0, 0) };

            btnAddPayment = UIHelper.CreateButton("➕ ثبت پرداخت جدید", UIHelper.ActionGreen, 180, 35);
            btnAddPayment.Click += (s, e) => { using var pForm = new ContractorPaymentForm(_connectionString, _contract); if (pForm.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync(); };

            btnPrintPayment = UIHelper.CreateButton("🖨️ پرینت رسید پرداخت", UIHelper.SectionBlue, 180, 35);
            btnPrintPayment.Enabled = false;
            btnPrintPayment.Click += BtnPrintPayment_Click;

            paymentBtnFlow.Controls.Add(btnAddPayment);
            paymentBtnFlow.Controls.Add(btnPrintPayment);

            tabPaymentsLayout.Controls.Add(dgvPayments, 0, 0);
            tabPaymentsLayout.Controls.Add(paymentBtnFlow, 0, 1);
            tabPayments.Controls.Add(tabPaymentsLayout);

            dgvGuarantees = CreateGrid();
            dgvGuarantees.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            dgvGuarantees.Columns.Add("GuaranteeNumber", "شماره ضمانت‌نامه");
            dgvGuarantees.Columns.Add("BankName", "نام بانک");
            dgvGuarantees.Columns.Add("GuaranteeType", "نوع ضمانت");
            dgvGuarantees.Columns.Add("Amount", "مبلغ");
            dgvGuarantees.Columns.Add("IssueDate", "تاریخ صدور");
            dgvGuarantees.Columns.Add("MaturityDate", "تاریخ سررسید");
            dgvGuarantees.Columns.Add("Status", "وضعیت");

            dgvGuarantees.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditSelectedGuarantee(); };
            dgvGuarantees.SelectionChanged += (s, e) => UpdateGuaranteeButtons();

            var tabGuaranteesLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tabGuaranteesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabGuaranteesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            var guaranteeFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(10, 5, 0, 0) };

            btnAddGuarantee = UIHelper.CreateButton("➕ ثبت ضمانت‌نامه", UIHelper.WarmPrimary, 160, 35);
            btnAddGuarantee.Click += (s, e) => { using var gForm = new ContractorGuaranteeEditForm(_connectionString, _contract); if (gForm.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync(); };

            btnEditGuarantee = UIHelper.CreateButton("✏️ ویرایش ردیف", UIHelper.WarmSecondary, 140, 35);
            btnEditGuarantee.Enabled = false;
            btnEditGuarantee.Click += (s, e) => EditSelectedGuarantee();

            btnDeleteGuarantee = UIHelper.CreateButton("🗑️ حذف ردیف", UIHelper.WarmDanger, 140, 35);
            btnDeleteGuarantee.Enabled = false;
            btnDeleteGuarantee.Click += DeleteSelectedGuarantee;

            guaranteeFlow.Controls.Add(btnAddGuarantee);
            guaranteeFlow.Controls.Add(btnEditGuarantee);
            guaranteeFlow.Controls.Add(btnDeleteGuarantee);

            tabGuaranteesLayout.Controls.Add(dgvGuarantees, 0, 0);
            tabGuaranteesLayout.Controls.Add(guaranteeFlow, 0, 1);
            tabGuarantees.Controls.Add(tabGuaranteesLayout);

            tabControl.TabPages.Add(tabStatements);
            tabControl.TabPages.Add(tabPayments);
            tabControl.TabPages.Add(tabGuarantees);

            var pnlBottom = new Panel { Dock = DockStyle.Fill };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };

            btnAddStatement = UIHelper.CreateButton("➕ افزودن صورت وضعیت", UIHelper.WarmPrimary, 180, 40);
            btnAddStatement.Click += (s, e) => { using var editForm = new ContractorStatementEditForm(_connectionString, _contract); if (editForm.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync(); };

            btnEditStatement = UIHelper.CreateButton("✏️ ویرایش ردیف", UIHelper.WarmSecondary, 150, 40);
            btnEditStatement.Enabled = false;
            btnEditStatement.Click += (s, e) => OpenEditFormForSelectedRow();

            btnDeleteStatement = UIHelper.CreateButton("🗑️ حذف ردیف", UIHelper.WarmDanger, 150, 40);
            btnDeleteStatement.Enabled = false;
            btnDeleteStatement.Click += DeleteSelectedRow;

            btnExportExcel = UIHelper.CreateButton("📥 خروجی اکسل", UIHelper.ActionGreen, 150, 40);
            btnExportExcel.Click += BtnExportExcel_Click;

            btnPrintList = UIHelper.CreateButton("🖨️ پرینت لیست", UIHelper.SectionBlue, 150, 40);
            btnPrintList.Click += BtnPrint_Click;

            btnPrintSingle = UIHelper.CreateButton("🖨️ پرینت تک صورت‌وضعیت", UIHelper.WarmSecondary, 180, 40);
            btnPrintSingle.Enabled = false;
            btnPrintSingle.Click += BtnPrintSingleStatement_Click;

            flow.Controls.Add(btnAddStatement);
            flow.Controls.Add(btnEditStatement);
            flow.Controls.Add(btnDeleteStatement);
            flow.Controls.Add(btnExportExcel);
            flow.Controls.Add(btnPrintList);
            flow.Controls.Add(btnPrintSingle);

            pnlBottom.Controls.Add(flow);

            mainLayout.Controls.Add(groupBoxHeader, 0, 0);
            mainLayout.Controls.Add(groupBoxSummary, 0, 1);
            mainLayout.Controls.Add(tabControl, 0, 2);
            mainLayout.Controls.Add(pnlBottom, 0, 3);

            Controls.Add(mainLayout);
            UpdateHeaderLabels();
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

        private Label CreateHeaderLabel() => new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(10.5f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(5), AutoSize = false };
        private Label CreateSummaryLabel(string text) => new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(10.5f, FontStyle.Bold), ForeColor = UIHelper.WarmText, BackColor = UIHelper.WarmPanelBackground, Margin = new Padding(3) };

        private void UpdateHeaderLabels()
        {
            lblContractNumber.Text = $"شماره قرارداد: {_contract.ContractNumber}";
            lblContractDate.Text = $"تاریخ قرارداد: {DateHelper.ToPersianDate(_contract.ContractDate)}";
            lblEndDate.Text = $"تاریخ پایان: {DateHelper.ToPersianDate(_contract.EndDate)}";
            lblContractorName.Text = $"پیمانکار: {_contract.DisplayName}";
            lblSubject.Text = $"موضوع: {_contract.Subject}";
            lblTotalAmount.Text = $"مبلغ کل: {NumberHelper.FormatThousands(_contract.Amount, usePersianDigits: true)} ریال";
            lblAdvanceAmount.Text = $"پیش پرداخت: {NumberHelper.FormatThousands(_contract.TotalAdvanceAmount, usePersianDigits: true)} ریال";
            lblRetentionPercent.Text = $"حسن انجام کار: {StringHelper.ConvertToPersianDigits(_contract.RetentionPercentage.ToString("0.##"))}%";
            lblInsurancePercent.Text = $"بیمه: {StringHelper.ConvertToPersianDigits(_contract.InsurancePercentage.ToString("0.##"))}%";
            lblVatPercent.Text = $"ارزش افزوده: {StringHelper.ConvertToPersianDigits(_contract.VatPercentage.ToString("0.##"))}%";
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
                    pbLogo.Image = new Bitmap(img);
                    lblLogoPlaceholder.Visible = false;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning("خطا در بارگذاری لوگو: " + ex.Message, "ContractorStatementReportForm");
            }
        }

        private async Task LoadDataSafeAsync()
        {
            List<ContractorStatement>? statements = null;
            List<ContractorPayment>? payments = null;
            List<ContractorGuarantee>? guarantees = null;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var sRepo = new ContractorStatementRepository(_connectionString);
                var pRepo = new ContractorPaymentRepository(_connectionString);
                var gRepo = new ContractorGuaranteeRepository(_connectionString);

                var sTask = Task.Run(() => sRepo.GetByContractId(_contract.Id));
                var pTask = Task.Run(() => pRepo.GetByContractId(_contract.Id));
                var gTask = Task.Run(() => gRepo.GetByContractId(_contract.Id));

                await Task.WhenAll(sTask, pTask, gTask);

                statements = sTask.Result;
                payments = pTask.Result;
                guarantees = gTask.Result;
            }, "در حال بارگذاری گزارش...");

            if (!success || statements == null || payments == null || guarantees == null) return;

            PopulateStatementsGrid(statements);
            PopulatePaymentsGrid(payments);
            PopulateGuaranteesGrid(guarantees);
            UpdateSummary(statements, payments);

            UpdateStatementButtons();
            UpdateGuaranteeButtons();
        }

        private void PopulateStatementsGrid(List<ContractorStatement> statements)
        {
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

                dgvStatements.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(240, 230, 220);
                dgvStatements.Rows[rowIndex].DefaultCellStyle.Font = UIHelper.PersianFont(11f, FontStyle.Bold);
                dgvStatements.Rows[rowIndex].Tag = "Total";
            }
        }

        private void PopulatePaymentsGrid(List<ContractorPayment> payments)
        {
            dgvPayments.Rows.Clear();
            foreach (var p in payments)
            {
                dgvPayments.Rows.Add(
                    p.Id, DateHelper.ToPersianDate(p.PaymentDate),
                    NumberHelper.FormatThousands(p.Amount, usePersianDigits: true),
                    p.PaymentType, p.Description
                );
            }
        }

        private void PopulateGuaranteesGrid(List<ContractorGuarantee> guarantees)
        {
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
                        dgvGuarantees.Rows[rowIndex].DefaultCellStyle.BackColor = UIHelper.WarmWarningBackground;
                        dgvGuarantees.Rows[rowIndex].DefaultCellStyle.ForeColor = UIHelper.WarmWarningText;
                    }
                }
                else if (g.Status == "فعال" && g.MaturityDate < DateTime.Today)
                {
                    dgvGuarantees.Rows[rowIndex].DefaultCellStyle.BackColor = UIHelper.WarmErrorBackground;
                    dgvGuarantees.Rows[rowIndex].DefaultCellStyle.ForeColor = UIHelper.WarmErrorText;
                }
            }
        }

        private void UpdateSummary(List<ContractorStatement> statements, List<ContractorPayment> payments)
        {
            decimal totalContractAmount = _contract.Amount;
            decimal totalWorkDone = statements.Sum(x => x.GrossAmount);
            decimal totalNetPayable = statements.Sum(x => x.NetPayableAmount);
            decimal totalPaid = payments.Sum(x => x.Amount);

            decimal remainingAmount = totalContractAmount - totalWorkDone;
            decimal progressPercent = totalContractAmount > 0 ? (totalWorkDone / totalContractAmount) * 100 : 0;
            decimal debt = totalNetPayable - totalPaid;

            lblSummaryTotalContract.Text = $"مبلغ کل قرارداد: {NumberHelper.FormatThousands(totalContractAmount, usePersianDigits: true)} ریال";
            lblSummaryTotalWork.Text = $"مجموع کارکرد: {NumberHelper.FormatThousands(totalWorkDone, usePersianDigits: true)} ریال";
            lblSummaryRemaining.Text = $"مانده قرارداد: {NumberHelper.FormatThousands(Math.Max(0, remainingAmount), usePersianDigits: true)} ریال";
            lblSummaryProgress.Text = $"پیشرفت: {StringHelper.ConvertToPersianDigits(progressPercent.ToString("N1"))}%";
            lblSummaryProgress.ForeColor = progressPercent > 100 ? UIHelper.WarmDanger : UIHelper.ActionGreen;

            lblSummaryTotalPaid.Text = $"پرداخت شده: {NumberHelper.FormatThousands(totalPaid, usePersianDigits: true)} ریال";
            lblSummaryDebt.Text = $"مانده بدهی: {NumberHelper.FormatThousands(Math.Max(0, debt), usePersianDigits: true)} ریال";
            lblSummaryDebt.ForeColor = debt > 0 ? UIHelper.WarmDanger : UIHelper.ActionGreen;
        }

        private void UpdateStatementButtons()
        {
            bool hasSelection = dgvStatements.CurrentRow != null && dgvStatements.CurrentRow.Index != dgvStatements.Rows.Count - 1;
            btnEditStatement.Enabled = hasSelection;
            btnDeleteStatement.Enabled = hasSelection;
            btnPrintSingle.Enabled = hasSelection;
            btnPrintList.Enabled = dgvStatements.Rows.Count > 0;
        }

        private void UpdateGuaranteeButtons()
        {
            bool hasSelection = dgvGuarantees.CurrentRow != null;
            btnEditGuarantee.Enabled = hasSelection;
            btnDeleteGuarantee.Enabled = hasSelection;
        }

        private void DgvStatements_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < dgvStatements.Rows.Count - 1) OpenEditFormForSelectedRow();
        }

        private async void OpenEditFormForSelectedRow()
        {
            if (dgvStatements.CurrentRow == null || dgvStatements.CurrentRow.Index == dgvStatements.Rows.Count - 1) return;
            int statementId = Convert.ToInt32(dgvStatements.CurrentRow.Cells["Id"].Value);

            ContractorStatement? statement = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorStatementRepository(_connectionString);
                statement = await Task.Run(() => repo.GetById(statementId));
            }, "در حال دریافت اطلاعات...");

            if (!success || statement == null) return;

            using var editForm = new ContractorStatementEditForm(_connectionString, _contract, statement);
            if (editForm.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync();
        }

        private async void DeleteSelectedRow(object? sender, EventArgs e)
        {
            if (dgvStatements.CurrentRow == null || dgvStatements.CurrentRow.Index == dgvStatements.Rows.Count - 1) return;

            int statementId = Convert.ToInt32(dgvStatements.CurrentRow.Cells["Id"].Value);
            string stNumber = dgvStatements.CurrentRow.Cells["RowNum"].Value?.ToString() ?? "";

            if (!ModernMessageBox.AskConfirm($"آیا از حذف صورت وضعیت شماره «{stNumber}» اطمینان دارید؟", "حذف صورت وضعیت"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorStatementRepository(_connectionString);
                await Task.Run(() => repo.Delete(statementId));
            }, "در حال حذف...");

            if (success)
            {
                ToastHelper.Show("صورت وضعیت حذف شد.", isSuccess: true);
                _ = LoadDataSafeAsync();
            }
        }

        private async void EditSelectedGuarantee()
        {
            if (dgvGuarantees.CurrentRow == null) return;
            int guaranteeId = Convert.ToInt32(dgvGuarantees.CurrentRow.Cells["Id"].Value);

            ContractorGuarantee? guarantee = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorGuaranteeRepository(_connectionString);
                guarantee = await Task.Run(() => repo.GetById(guaranteeId));
            }, "در حال دریافت اطلاعات...");

            if (!success || guarantee == null) return;

            using var form = new ContractorGuaranteeEditForm(_connectionString, _contract, guarantee);
            if (form.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync();
        }

        private async void DeleteSelectedGuarantee(object? sender, EventArgs e)
        {
            if (dgvGuarantees.CurrentRow == null) return;
            int guaranteeId = Convert.ToInt32(dgvGuarantees.CurrentRow.Cells["Id"].Value);
            string gNumber = dgvGuarantees.CurrentRow.Cells["GuaranteeNumber"].Value?.ToString() ?? "";

            if (!ModernMessageBox.AskConfirm($"آیا از حذف ضمانت‌نامه «{gNumber}» اطمینان دارید؟", "حذف ضمانت‌نامه"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorGuaranteeRepository(_connectionString);
                await Task.Run(() => repo.Delete(guaranteeId));
            }, "در حال حذف...");

            if (success)
            {
                ToastHelper.Show("ضمانت‌نامه حذف شد.", isSuccess: true);
                _ = LoadDataSafeAsync();
            }
        }

        private void BtnExportExcel_Click(object? sender, EventArgs e)
        {
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
            if (dgvPayments.CurrentRow == null) return;

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
            if (dgvStatements.CurrentRow == null || dgvStatements.CurrentRow.Index == dgvStatements.Rows.Count - 1) return;

            _sglStatementNumber = dgvStatements.CurrentRow.Cells["RowNum"].Value?.ToString() ?? "";
            _sglGross = NumberHelper.ParseFormatted(dgvStatements.CurrentRow.Cells["GrossAmount"].Value?.ToString());
            _sglVat = NumberHelper.ParseFormatted(dgvStatements.CurrentRow.Cells["VAT"].Value?.ToString());
            _sglTotal = NumberHelper.ParseFormatted(dgvStatements.CurrentRow.Cells["TotalAmount"].Value?.ToString());
            _sglRetention = NumberHelper.ParseFormatted(dgvStatements.CurrentRow.Cells["RetentionDeduction"].Value?.ToString());
            _sglInsurance = NumberHelper.ParseFormatted(dgvStatements.CurrentRow.Cells["InsuranceDeduction"].Value?.ToString());
            _sglAdvance = NumberHelper.ParseFormatted(dgvStatements.CurrentRow.Cells["AdvanceRecovery"].Value?.ToString());
            _sglNet = NumberHelper.ParseFormatted(dgvStatements.CurrentRow.Cells["NetPayableAmount"].Value?.ToString());

            using var preview = new PrintPreviewDialog
            {
                Document = singleStatementPrintDoc,
                Width = 800,
                Height = 900,
                WindowState = FormWindowState.Maximized,
                UseAntiAlias = true,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true
            };
            preview.ShowDialog(this);
        }

        private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics!;
            float pageWidth = e.PageBounds.Width;
            float pageHeight = e.PageBounds.Height;
            float margin = 40f;
            float yPos = margin;

            // ✅ FIX: فونت‌های کش‌شده نباید Dispose شوند
            var fontTitle = UIHelper.PersianFont(16, FontStyle.Bold);
            var fontHeader = UIHelper.PersianFont(10, FontStyle.Bold);
            var fontBody = UIHelper.PersianFont(9.5f);

            using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
            using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
            using StringFormat titleFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

            if (isPrintingHeader)
            {
                float logoWidth = 100; float logoHeight = 80;
                if (pbLogo.Image != null) g.DrawImage(pbLogo.Image, pageWidth - margin - logoWidth, yPos, logoWidth, logoHeight);

                g.DrawString("گزارش صورت وضعیت پیمانکار", fontTitle, Brushes.Black, new RectangleF(margin, yPos + 30, pageWidth - margin * 2, 40), titleFormat);
                yPos += 110;

                float contentWidth = pageWidth - margin * 2; float colWidth = contentWidth / 4; float rightEdge = pageWidth - margin;
                float rowY = yPos;
                g.DrawString($"پیمانکار: {_contract.DisplayName}", fontHeader, Brushes.Black, new RectangleF(rightEdge - colWidth, rowY, colWidth, 25), rtlFormatRight);
                g.DrawString($"شماره قرارداد: {_contract.ContractNumber}", fontHeader, Brushes.Black, new RectangleF(rightEdge - colWidth * 2, rowY, colWidth, 25), rtlFormatRight);
                g.DrawString($"تاریخ قرارداد: {DateHelper.ToPersianDate(_contract.ContractDate)}", fontHeader, Brushes.Black, new RectangleF(rightEdge - colWidth * 3, rowY, colWidth, 25), rtlFormatRight);
                g.DrawString($"تاریخ پایان: {DateHelper.ToPersianDate(_contract.EndDate)}", fontHeader, Brushes.Black, new RectangleF(rightEdge - colWidth * 4, rowY, colWidth, 25), rtlFormatRight);

                rowY += 30;
                g.DrawString($"موضوع: {_contract.Subject}", fontHeader, Brushes.Black, new RectangleF(rightEdge - colWidth * 2, rowY, colWidth * 2, 25), rtlFormatRight);
                g.DrawString($"مبلغ کل: {NumberHelper.FormatThousands(_contract.Amount, usePersianDigits: true)} ریال", fontHeader, Brushes.Black, new RectangleF(rightEdge - colWidth * 3, rowY, colWidth, 25), rtlFormatRight);
                g.DrawString($"پیش پرداخت: {NumberHelper.FormatThousands(_contract.TotalAdvanceAmount, usePersianDigits: true)} ریال", fontHeader, Brushes.Black, new RectangleF(rightEdge - colWidth * 4, rowY, colWidth, 25), rtlFormatRight);

                rowY += 30;
                g.DrawString($"حسن انجام کار: {StringHelper.ConvertToPersianDigits(_contract.RetentionPercentage.ToString("0.##"))}%", fontHeader, Brushes.Black, new RectangleF(rightEdge - colWidth, rowY, colWidth, 25), rtlFormatRight);
                g.DrawString($"بیمه: {StringHelper.ConvertToPersianDigits(_contract.InsurancePercentage.ToString("0.##"))}%", fontHeader, Brushes.Black, new RectangleF(rightEdge - colWidth * 2, rowY, colWidth, 25), rtlFormatRight);
                g.DrawString($"ارزش افزوده: {StringHelper.ConvertToPersianDigits(_contract.VatPercentage.ToString("0.##"))}%", fontHeader, Brushes.Black, new RectangleF(rightEdge - colWidth * 3, rowY, colWidth, 25), rtlFormatRight);

                yPos = rowY + 40;
                isPrintingHeader = false;
            }
            else
            {
                g.DrawString($"ادامه گزارش صورت وضعیت - قرارداد {_contract.ContractNumber}", fontHeader, Brushes.Black, new RectangleF(margin, yPos, pageWidth - margin * 2, 20), rtlFormatRight);
                yPos += 30;
            }

            float[] colWidths = { 45, 90, 90, 80, 90, 95, 75, 65, 85, 90, 90 };
            float tableWidth = colWidths.Sum(); float startX = (pageWidth - tableWidth) / 2; float rowHeight = 28f;
            string[] headers = { "شماره", "مبلغ انباشته", "مبلغ جاری", "ارزش افزوده", "جمع کل", "حسن انجام کار", "بیمه", "سایر", "پیش پرداخت", "جمع کسورات", "خالص" };

            float currentX = startX + tableWidth;
            g.FillRectangle(Brushes.LightGray, startX, yPos, tableWidth, rowHeight);

            for (int i = 0; i < headers.Length; i++)
            {
                currentX -= colWidths[i];
                RectangleF rect = new RectangleF(currentX, yPos, colWidths[i], rowHeight);
                g.DrawRectangle(Pens.Black, rect.X, rect.Y, rect.Width, rect.Height);
                g.DrawString(headers[i], fontHeader, Brushes.Black, rect, rtlFormatCenter);
            }
            yPos += rowHeight;

            while (printRowIndex < dgvStatements.Rows.Count)
            {
                if (yPos + rowHeight > pageHeight - 120) { e.HasMorePages = true; return; }
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
                        g.DrawString(cellValue, fontHeader, Brushes.Black, rect, rtlFormatCenter);
                    }
                    else g.DrawString(cellValue, fontBody, Brushes.Black, rect, rtlFormatCenter);
                }
                yPos += rowHeight;
                printRowIndex++;
            }

            if (printRowIndex >= dgvStatements.Rows.Count)
            {
                yPos += 40;
                float signWidth = 150f; float gap = (pageWidth - (signWidth * 3) - margin * 2) / 2;

                g.DrawString("مهر و امضای پیمانکار", fontHeader, Brushes.Black, new RectangleF(pageWidth - margin - signWidth, yPos, signWidth, 20), rtlFormatCenter);
                g.DrawLine(Pens.Black, pageWidth - margin - signWidth, yPos + 50, pageWidth - margin, yPos + 50);

                g.DrawString("مهر و امضای ناظر", fontHeader, Brushes.Black, new RectangleF(pageWidth - margin - signWidth - gap - signWidth, yPos, signWidth, 20), rtlFormatCenter);
                g.DrawLine(Pens.Black, pageWidth - margin - signWidth - gap - signWidth, yPos + 50, pageWidth - margin - gap - signWidth, yPos + 50);

                g.DrawString("مهر و امضای کارفرما", fontHeader, Brushes.Black, new RectangleF(margin, yPos, signWidth, 20), rtlFormatCenter);
                g.DrawLine(Pens.Black, margin, yPos + 50, margin + signWidth, yPos + 50);
            }
            e.HasMorePages = false;
        }

        private void PaymentPrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics!;
            float pageWidth = e.PageBounds.Width;
            float pageHeight = e.PageBounds.Height;
            float margin = 50f;
            float yPos = margin;

            // ✅ FIX
            var fontTitle = UIHelper.PersianFont(18, FontStyle.Bold);
            var fontHeader = UIHelper.PersianFont(12, FontStyle.Bold);
            var fontBody = UIHelper.PersianFont(11f);

            using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
            using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };

            g.DrawRectangle(Pens.Black, margin, margin, pageWidth - margin * 2, pageHeight - margin * 2);

            if (pbLogo.Image != null)
            {
                g.DrawImage(pbLogo.Image, pageWidth - margin - 110, yPos + 10, 100, 80);
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
            float pageWidth = e.PageBounds.Width;
            float pageHeight = e.PageBounds.Height;
            float margin = 50f;
            float yPos = margin;

            // ✅ FIX
            var fontTitle = UIHelper.PersianFont(18, FontStyle.Bold);
            var fontHeader = UIHelper.PersianFont(12, FontStyle.Bold);
            var fontBody = UIHelper.PersianFont(11f);

            using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
            using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };

            g.DrawRectangle(Pens.Black, margin, margin, pageWidth - margin * 2, pageHeight - margin * 2);

            if (pbLogo.Image != null)
            {
                g.DrawImage(pbLogo.Image, pageWidth - margin - 110, yPos + 10, 100, 80);
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
}