using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Models;
using PersonnelApp.Helpers;
using PersonnelApp.Data;

namespace PersonnelApp.Forms
{
    public class ClientContractReportForm : Form
    {
        private readonly string _connectionString;
        private readonly ClientContract _contract;

        private TabControl tabControl = null!;
        private DataGridView dgvStatements = null!;
        private DataGridView dgvPayments = null!;
        private DataGridView dgvContractorContracts = null!;
        private DataGridView dgvExpenses = null!;

        private ClientChartPanel chartPanel = null!;

        private Label lblSummaryTotalContract = null!, lblSummaryTotalWork = null!, lblSummaryRemaining = null!, lblSummaryProgress = null!;
        private Label lblSummaryTotalPaid = null!, lblSummaryDebt = null!;
        private Label lblSummaryContractorTotal = null!, lblSummaryProfit = null!, lblSummaryExpenses = null!;

        private Image? _companyLogo = null;

        private PrintDocument singleStatementPrintDoc = null!;
        private string _sglStatementNumber = "";
        private decimal _sglGross = 0;
        private decimal _sglVat = 0;
        private decimal _sglTotal = 0;
        private decimal _sglDeductions = 0;
        private decimal _sglNet = 0;

        private PrintDocument paymentPrintDoc = null!;
        private decimal _paymentAmountForPrint = 0;
        private string _paymentTypeForPrint = "";
        private string _paymentDateForPrint = "";
        private string _paymentDescForPrint = "";

        public ClientContractReportForm(string connectionString, ClientContract contract)
        {
            _connectionString = connectionString;
            _contract = contract;

            BuildUI();
            LoadCompanyLogo();
            SetupPrintDocument();

            ShortcutHelper.ApplyStandardShortcuts(this);
            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, $"گزارش مالی کارفرما - {_contract.ContractNumber}", new Size(1250, 850));
            this.MaximumSize = new Size(1400, 900);

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var groupBoxHeader = new GroupBox
            {
                Text = "اطلاعات قرارداد کارفرما",
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                Padding = new Padding(10)
            };

            var headerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            var lblContractNumber = CreateHeaderLabel($"شماره قرارداد: {_contract.ContractNumber}");
            var lblContractDate = CreateHeaderLabel($"تاریخ قرارداد: {DateHelper.ToPersianDate(_contract.ContractDate)}");
            var lblEndDate = CreateHeaderLabel($"تاریخ پایان: {DateHelper.ToPersianDate(_contract.EndDate)}");
            var lblClientName = CreateHeaderLabel($"کارفرما: {_contract.DisplayName}");
            var lblSubject = CreateHeaderLabel($"موضوع: {_contract.Subject}");
            var lblTotalAmount = CreateHeaderLabel($"مبلغ کل: {NumberHelper.FormatThousands(_contract.Amount, usePersianDigits: true)} ریال");

            headerLayout.Controls.Add(lblClientName, 0, 0);
            headerLayout.Controls.Add(lblContractNumber, 1, 0);
            headerLayout.Controls.Add(lblContractDate, 2, 0);
            headerLayout.Controls.Add(lblEndDate, 3, 0);
            headerLayout.Controls.Add(lblSubject, 0, 1);
            headerLayout.SetColumnSpan(lblSubject, 2);
            headerLayout.Controls.Add(lblTotalAmount, 2, 1);

            groupBoxHeader.Controls.Add(headerLayout);

            var groupBoxSummary = new GroupBox
            {
                Text = "خلاصه وضعیت مالی",
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(10f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                Padding = new Padding(10, 5, 10, 5)
            };

            var summaryLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 9, RowCount = 1 };
            for (int i = 0; i < 9; i++) summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.11F));

            lblSummaryTotalContract = CreateSummaryLabel("مبلغ قرارداد: ");
            lblSummaryTotalWork = CreateSummaryLabel("کارکرد: ");
            lblSummaryRemaining = CreateSummaryLabel("مانده قرارداد: ");
            lblSummaryProgress = CreateSummaryLabel("پیشرفت: ");
            lblSummaryTotalPaid = CreateSummaryLabel("دریافتی: ");
            lblSummaryDebt = CreateSummaryLabel("مانده طلب: ");
            lblSummaryContractorTotal = CreateSummaryLabel("هزینه پیمانکاران: ");
            lblSummaryExpenses = CreateSummaryLabel("هزینه فاکتوری: ");
            lblSummaryProfit = CreateSummaryLabel("سود ناخالص: ");

            summaryLayout.Controls.Add(lblSummaryTotalContract, 0, 0);
            summaryLayout.Controls.Add(lblSummaryTotalWork, 1, 0);
            summaryLayout.Controls.Add(lblSummaryRemaining, 2, 0);
            summaryLayout.Controls.Add(lblSummaryProgress, 3, 0);
            summaryLayout.Controls.Add(lblSummaryTotalPaid, 4, 0);
            summaryLayout.Controls.Add(lblSummaryDebt, 5, 0);
            summaryLayout.Controls.Add(lblSummaryContractorTotal, 6, 0);
            summaryLayout.Controls.Add(lblSummaryExpenses, 7, 0);
            summaryLayout.Controls.Add(lblSummaryProfit, 8, 0);

            groupBoxSummary.Controls.Add(summaryLayout);

            tabControl = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
            ModernLayoutHelper.StyleModernTabControl(tabControl);

            TabPage tabStatements = new TabPage("📄 صورت وضعیت‌های صادره");
            TabPage tabPayments = new TabPage("💳 دریافتی‌ها");
            TabPage tabContractors = new TabPage("📝 قراردادهای پیمانکاران جزء");
            TabPage tabExpenses = new TabPage("🧾 هزینه‌های فاکتوری");
            TabPage tabChart = new TabPage("📊 نمودار مالی");

            dgvStatements = CreateGrid();
            dgvStatements.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            dgvStatements.Columns.Add("RowNum", "شماره");
            dgvStatements.Columns.Add("GrossAmount", "مبلغ کارکرد");
            dgvStatements.Columns.Add("VAT", "ارزش افزوده");
            dgvStatements.Columns.Add("TotalAmount", "جمع کل");
            dgvStatements.Columns.Add("Deductions", "کسورات");
            dgvStatements.Columns.Add("NetReceivableAmount", "خالص دریافتی");

            var tabStLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tabStLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabStLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            var stFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10, 5, 0, 0) };
            var btnAddSt = UIHelper.CreateButton("➕ ثبت صورت وضعیت", UIHelper.WarmPrimary, 160, 35);
            btnAddSt.Click += (s, e) => { using var f = new ClientStatementEditForm(_connectionString, _contract); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };
            var btnEditSt = UIHelper.CreateButton("✏️ ویرایش", UIHelper.WarmSecondary, 120, 35);
            btnEditSt.Click += EditSelectedStatement;
            var btnDelSt = UIHelper.CreateButton("🗑️ حذف", UIHelper.WarmDanger, 120, 35);
            btnDelSt.Click += DeleteSelectedStatement;
            var btnPrintSt = UIHelper.CreateButton("🖨️ پرینت صورت‌وضعیت", UIHelper.SectionBlue, 180, 35);
            btnPrintSt.Click += BtnPrintSingleStatement_Click;

            stFlow.Controls.Add(btnAddSt); stFlow.Controls.Add(btnEditSt); stFlow.Controls.Add(btnDelSt); stFlow.Controls.Add(btnPrintSt);
            tabStLayout.Controls.Add(dgvStatements, 0, 0);
            tabStLayout.Controls.Add(stFlow, 0, 1);
            tabStatements.Controls.Add(tabStLayout);

            dgvPayments = CreateGrid();
            dgvPayments.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            dgvPayments.Columns.Add("PaymentDate", "تاریخ دریافت");
            dgvPayments.Columns.Add("Amount", "مبلغ دریافتی");
            dgvPayments.Columns.Add("PaymentType", "نوع پرداخت");
            dgvPayments.Columns.Add("Description", "توضیحات");

            var tabPayLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tabPayLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabPayLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            var payFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10, 5, 0, 0) };
            var btnAddPay = UIHelper.CreateButton("➕ ثبت دریافتی جدید", UIHelper.ActionGreen, 180, 35);
            btnAddPay.Click += (s, e) => { using var f = new ClientPaymentForm(_connectionString, _contract); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };
            var btnPrintPay = UIHelper.CreateButton("🖨️ پرینت رسید دریافت", UIHelper.SectionBlue, 180, 35);
            btnPrintPay.Click += BtnPrintPayment_Click;

            payFlow.Controls.Add(btnAddPay);
            payFlow.Controls.Add(btnPrintPay);
            tabPayLayout.Controls.Add(dgvPayments, 0, 0);
            tabPayLayout.Controls.Add(payFlow, 0, 1);
            tabPayments.Controls.Add(tabPayLayout);

            dgvContractorContracts = CreateGrid();
            dgvContractorContracts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            dgvContractorContracts.Columns.Add("ContractNumber", "شماره قرارداد پیمانکار");
            dgvContractorContracts.Columns.Add("DisplayName", "نام پیمانکار");
            dgvContractorContracts.Columns.Add("Subject", "موضوع");
            dgvContractorContracts.Columns.Add("Amount", "مبلغ قرارداد");
            dgvContractorContracts.Columns.Add("ContractDate", "تاریخ قرارداد");
            dgvContractorContracts.Columns.Add("EndDate", "تاریخ پایان");

            var tabConLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tabConLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabConLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            var conFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10, 5, 0, 0) };
            var btnAddContractor = UIHelper.CreateButton("➕ افزودن قرارداد پیمانکار جدید", UIHelper.WarmPrimary, 220, 35);
            btnAddContractor.Click += (s, e) => { using var f = new ContractorContractEditForm(_connectionString); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };
            conFlow.Controls.Add(btnAddContractor);
            tabConLayout.Controls.Add(dgvContractorContracts, 0, 0);
            tabConLayout.Controls.Add(conFlow, 0, 1);
            tabContractors.Controls.Add(tabConLayout);

            dgvExpenses = CreateGrid();
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            dgvExpenses.Columns.Add("InvoiceDate", "تاریخ فاکتور");
            dgvExpenses.Columns.Add("InvoiceNumber", "شماره فاکتور");
            dgvExpenses.Columns.Add("PartyName", "طرف حساب");
            dgvExpenses.Columns.Add("Amount", "مبلغ فاکتور");
            dgvExpenses.Columns.Add("Description", "توضیحات");

            var tabExpLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            tabExpLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabExpLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            var expFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10, 5, 0, 0) };
            var btnAddExp = UIHelper.CreateButton("➕ ثبت فاکتور هزینه", UIHelper.WarmPrimary, 180, 35);
            btnAddExp.Click += (s, e) => { using var f = new ProjectExpenseEditForm(_connectionString, _contract); if (f.ShowDialog() == DialogResult.OK) _ = LoadDataSafeAsync(); };
            var btnDelExp = UIHelper.CreateButton("🗑️ حذف فاکتور", UIHelper.WarmDanger, 120, 35);
            btnDelExp.Click += DeleteSelectedExpense;
            expFlow.Controls.Add(btnAddExp);
            expFlow.Controls.Add(btnDelExp);
            tabExpLayout.Controls.Add(dgvExpenses, 0, 0);
            tabExpLayout.Controls.Add(expFlow, 0, 1);
            tabExpenses.Controls.Add(tabExpLayout);

            chartPanel = new ClientChartPanel { Dock = DockStyle.Fill, Margin = new Padding(10) };
            tabChart.Controls.Add(chartPanel);

            tabControl.TabPages.Add(tabStatements);
            tabControl.TabPages.Add(tabPayments);
            tabControl.TabPages.Add(tabContractors);
            tabControl.TabPages.Add(tabExpenses);
            tabControl.TabPages.Add(tabChart);

            mainLayout.Controls.Add(groupBoxHeader, 0, 0);
            mainLayout.Controls.Add(groupBoxSummary, 0, 1);
            mainLayout.Controls.Add(tabControl, 0, 2);

            Controls.Add(mainLayout);
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

        private Label CreateHeaderLabel(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(10.5f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(5), AutoSize = false };
        }

        private Label CreateSummaryLabel(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(9.5f, FontStyle.Bold), ForeColor = UIHelper.WarmText, BackColor = UIHelper.WarmPanelBackground, Margin = new Padding(2) };
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
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning("خطا در بارگذاری لوگو: " + ex.Message, "ClientContractReportForm");
            }
        }

        private async Task LoadDataSafeAsync()
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var stRepo = new ClientStatementRepository(_connectionString);
                var pRepo = new ClientPaymentRepository(_connectionString);
                var cRepo = new ContractorContractRepository(_connectionString);
                var eRepo = new ProjectExpenseRepository(_connectionString);

                var statementsTask = Task.Run(() => stRepo.GetByContractId(_contract.Id));
                var paymentsTask = Task.Run(() => pRepo.GetByContractId(_contract.Id));
                var contractsTask = Task.Run(() => cRepo.GetByClientContractId(_contract.Id));
                var expensesTask = Task.Run(() => eRepo.GetByClientContractId(_contract.Id));

                await Task.WhenAll(statementsTask, paymentsTask, contractsTask, expensesTask);

                var statements = statementsTask.Result;
                var payments = paymentsTask.Result;
                var contracts = contractsTask.Result;
                var expenses = expensesTask.Result;

                dgvStatements.Rows.Clear();
                foreach (var st in statements)
                {
                    dgvStatements.Rows.Add(st.Id, st.StatementNumber,
                        NumberHelper.FormatThousands(st.GrossAmount, usePersianDigits: true),
                        NumberHelper.FormatThousands(st.VAT, usePersianDigits: true),
                        NumberHelper.FormatThousands(st.TotalAmount, usePersianDigits: true),
                        NumberHelper.FormatThousands(st.Deductions, usePersianDigits: true),
                        NumberHelper.FormatThousands(st.NetReceivableAmount, usePersianDigits: true));
                }

                dgvPayments.Rows.Clear();
                foreach (var p in payments)
                {
                    dgvPayments.Rows.Add(p.Id, DateHelper.ToPersianDate(p.PaymentDate),
                        NumberHelper.FormatThousands(p.Amount, usePersianDigits: true),
                        p.PaymentType, p.Description);
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
                        NumberHelper.FormatThousands(item.Amount, usePersianDigits: true), item.Description);
                }

                UpdateSummary(statements, payments, contracts, expenses);
            }, "در حال بارگذاری اطلاعات قرارداد...");
        }

        private void UpdateSummary(
            List<ClientStatement> statements,
            List<ClientPayment> payments,
            List<ContractorContract> contractorContracts,
            List<ProjectExpense> expenses)
        {
            decimal totalContractAmount = _contract.Amount;
            decimal totalWorkDone = statements.Sum(x => x.GrossAmount);
            decimal totalNetReceivable = statements.Sum(x => x.NetReceivableAmount);
            decimal totalReceived = payments.Sum(x => x.Amount);
            decimal totalContractorAmount = contractorContracts.Sum(x => x.Amount);
            decimal totalExpenses = expenses.Sum(x => x.Amount);

            decimal remainingAmount = totalContractAmount - totalWorkDone;
            decimal progressPercent = totalContractAmount > 0 ? (totalWorkDone / totalContractAmount) * 100 : 0;
            decimal debt = totalNetReceivable - totalReceived;
            decimal grossProfit = totalContractAmount - (totalContractorAmount + totalExpenses);

            lblSummaryTotalContract.Text = $"مبلغ قرارداد: {NumberHelper.FormatThousands(totalContractAmount, usePersianDigits: true)}";
            lblSummaryTotalWork.Text = $"کارکرد: {NumberHelper.FormatThousands(totalWorkDone, usePersianDigits: true)}";
            lblSummaryRemaining.Text = $"مانده قرارداد: {NumberHelper.FormatThousands(Math.Max(0, remainingAmount), usePersianDigits: true)}";
            lblSummaryProgress.Text = $"پیشرفت: {StringHelper.ConvertToPersianDigits(progressPercent.ToString("N1"))}%";
            lblSummaryProgress.ForeColor = progressPercent > 100 ? UIHelper.WarmDanger : UIHelper.ActionGreen;

            lblSummaryTotalPaid.Text = $"دریافتی: {NumberHelper.FormatThousands(totalReceived, usePersianDigits: true)}";
            lblSummaryDebt.Text = $"مانده طلب: {NumberHelper.FormatThousands(Math.Max(0, debt), usePersianDigits: true)}";
            lblSummaryDebt.ForeColor = debt > 0 ? UIHelper.WarmDanger : UIHelper.ActionGreen;

            lblSummaryContractorTotal.Text = $"هزینه پیمانکاران: {NumberHelper.FormatThousands(totalContractorAmount, usePersianDigits: true)}";
            lblSummaryContractorTotal.ForeColor = UIHelper.WarmDanger;

            lblSummaryExpenses.Text = $"هزینه فاکتوری: {NumberHelper.FormatThousands(totalExpenses, usePersianDigits: true)}";
            lblSummaryExpenses.ForeColor = UIHelper.WarmDanger;

            lblSummaryProfit.Text = $"سود ناخالص: {NumberHelper.FormatThousands(grossProfit, usePersianDigits: true)}";
            lblSummaryProfit.ForeColor = grossProfit >= 0 ? UIHelper.ActionGreen : UIHelper.WarmDanger;

            chartPanel?.UpdateData(totalContractAmount, totalContractorAmount, totalExpenses);
        }

        private async void EditSelectedStatement(object? sender, EventArgs e)
        {
            if (dgvStatements.CurrentRow == null) return;
            int id = Convert.ToInt32(dgvStatements.CurrentRow.Cells["Id"].Value);

            ClientStatement? st = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ClientStatementRepository(_connectionString);
                st = await Task.Run(() => repo.GetById(id));
            }, "در حال دریافت اطلاعات صورت وضعیت...");

            if (!success || st == null) return;

            using var f = new ClientStatementEditForm(_connectionString, _contract, st);
            if (f.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync();
        }

        private async void DeleteSelectedStatement(object? sender, EventArgs e)
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

            if (success)
            {
                _ = LoadDataSafeAsync();
                ToastHelper.Show("صورت وضعیت حذف شد.", isSuccess: true);
            }
        }

        private async void DeleteSelectedExpense(object? sender, EventArgs e)
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

            if (success)
            {
                _ = LoadDataSafeAsync();
                ToastHelper.Show("فاکتور حذف شد.", isSuccess: true);
            }
        }

        private void SetupPrintDocument()
        {
            singleStatementPrintDoc = new PrintDocument();
            singleStatementPrintDoc.DefaultPageSettings.Landscape = false;
            singleStatementPrintDoc.PrintPage += SingleStatementPrintDoc_PrintPage;

            paymentPrintDoc = new PrintDocument();
            paymentPrintDoc.DefaultPageSettings.Landscape = false;
            paymentPrintDoc.PrintPage += PaymentPrintDoc_PrintPage;

            FormClosed += (s, e) =>
            {
                singleStatementPrintDoc?.Dispose();
                paymentPrintDoc?.Dispose();
                _companyLogo?.Dispose();
            };
        }

        private void BtnPrintSingleStatement_Click(object? sender, EventArgs e)
        {
            if (dgvStatements.CurrentRow == null)
            {
                ModernMessageBox.ShowWarning("لطفاً یک ردیف صورت‌وضعیت را برای پرینت انتخاب کنید.");
                return;
            }

            _sglStatementNumber = dgvStatements.CurrentRow.Cells["RowNum"].Value?.ToString() ?? "";
            _sglGross = NumberHelper.ParseFormatted(dgvStatements.CurrentRow.Cells["GrossAmount"].Value?.ToString());
            _sglVat = NumberHelper.ParseFormatted(dgvStatements.CurrentRow.Cells["VAT"].Value?.ToString());
            _sglTotal = NumberHelper.ParseFormatted(dgvStatements.CurrentRow.Cells["TotalAmount"].Value?.ToString());
            _sglDeductions = NumberHelper.ParseFormatted(dgvStatements.CurrentRow.Cells["Deductions"].Value?.ToString());
            _sglNet = NumberHelper.ParseFormatted(dgvStatements.CurrentRow.Cells["NetReceivableAmount"].Value?.ToString());

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

        private void SingleStatementPrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics!;
            float pageWidth = e.PageBounds.Width;
            float pageHeight = e.PageBounds.Height;
            float margin = 50f;
            float yPos = margin;

            // ✅ FIX: فونت‌های کش‌شده نباید Dispose شوند
            var fontTitle = UIHelper.PersianFont(18, FontStyle.Bold);
            var fontHeader = UIHelper.PersianFont(12, FontStyle.Bold);
            var fontBody = UIHelper.PersianFont(11f);

            using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
            using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };

            if (_companyLogo != null)
            {
                g.DrawImage(_companyLogo, pageWidth - margin - 100, yPos, 100, 80);
            }

            g.DrawString($"صورت وضعیت شماره {_sglStatementNumber}", fontTitle, Brushes.Black, new RectangleF(margin, yPos + 20, pageWidth - margin * 2, 40), rtlFormatCenter);
            yPos += 100;

            g.DrawLine(Pens.Black, margin, yPos, pageWidth - margin, yPos);
            yPos += 20;

            g.DrawString($"کارفرما: {_contract.DisplayName}", fontHeader, Brushes.Black, new RectangleF(margin, yPos, pageWidth / 2, 25), rtlFormatRight);
            g.DrawString($"شماره قرارداد: {_contract.ContractNumber}", fontHeader, Brushes.Black, new RectangleF(pageWidth / 2, yPos, pageWidth / 2 - margin, 25), rtlFormatRight);
            yPos += 30;

            g.DrawString($"موضوع قرارداد: {_contract.Subject}", fontHeader, Brushes.Black, new RectangleF(margin, yPos, pageWidth - margin * 2, 25), rtlFormatRight);
            yPos += 40;

            float tableWidth = pageWidth - margin * 2;
            float startX = margin;
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
            DrawRow("کسورات", _sglDeductions);
            DrawRow("مبلغ خالص قابل دریافت", _sglNet, true);

            yPos += 30;

            g.DrawString($"مبلغ خالص به حروف: {NumberHelper.ToPersianWords(_sglNet)} ریال", fontHeader, Brushes.Black, new RectangleF(margin, yPos, pageWidth - margin * 2, 25), rtlFormatRight);
            yPos += 60;

            float signY = pageHeight - margin - 100;
            float signWidth = 150f;

            g.DrawString("مهر و امضای کارفرما", fontHeader, Brushes.Black, new RectangleF(pageWidth - margin - signWidth, signY, signWidth, 20), rtlFormatCenter);
            g.DrawLine(Pens.Black, pageWidth - margin - signWidth, signY + 60, pageWidth - margin, signY + 60);

            g.DrawString("مهر و امضای ناظر", fontHeader, Brushes.Black, new RectangleF(pageWidth / 2 - signWidth / 2, signY, signWidth, 20), rtlFormatCenter);
            g.DrawLine(Pens.Black, pageWidth / 2 - signWidth / 2, signY + 60, pageWidth / 2 + signWidth / 2, signY + 60);

            g.DrawString("مهر و امضای پیمانکار", fontHeader, Brushes.Black, new RectangleF(margin, signY, signWidth, 20), rtlFormatCenter);
            g.DrawLine(Pens.Black, margin, signY + 60, margin + signWidth, signY + 60);
        }

        private void BtnPrintPayment_Click(object? sender, EventArgs e)
        {
            if (dgvPayments.CurrentRow == null)
            {
                ModernMessageBox.ShowWarning("لطفاً یک ردیف از دریافتی‌ها را برای پرینت انتخاب کنید.");
                return;
            }

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

            // ✅ FIX: فونت‌های کش‌شده نباید Dispose شوند
            var fontTitle = UIHelper.PersianFont(18, FontStyle.Bold);
            var fontHeader = UIHelper.PersianFont(12, FontStyle.Bold);
            var fontBody = UIHelper.PersianFont(11f);

            using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
            using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };

            g.DrawRectangle(Pens.Black, margin, margin, pageWidth - margin * 2, pageHeight - margin * 2);

            if (_companyLogo != null)
            {
                g.DrawImage(_companyLogo, pageWidth - margin - 100, yPos + 10, 100, 80);
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
    }
}