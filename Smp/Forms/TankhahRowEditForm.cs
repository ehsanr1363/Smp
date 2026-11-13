using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class TankhahRowEditForm : Form
    {
        private readonly string _connectionString;
        private readonly int _tankhahAccountId;
        private readonly string _tankhahTitle;
        private readonly int? _transactionId;
        private readonly bool _isEdit;

        private readonly CashTransactionRepository _transRepo;
        private readonly ClientContractRepository _clientContractRepo;
        private readonly ContractorContractRepository _contractorContractRepo;
        private readonly TafsiliRepository _tafsiliRepo;
        private readonly LookupRepository _lookupRepo;

        private RadioButton rbAdmin = null!, rbContractorContract = null!, rbInformalContractor = null!, rbDirectPurchase = null!, rbPayroll = null!;
        private TextBox txtDate = null!, txtDescription = null!;
        private TextBox txtAmount = null!, txtDiscount = null!, txtShipping = null!, txtVat = null!, txtTotal = null!, txtAmountInWords = null!;
        private TextBox txtAutoContractDisplay = null!;
        private ComboBox cmbClientContract = null!, cmbContractorContract = null!, cmbCostType = null!, cmbCategory = null!, cmbTafsili = null!;
        private Button btnSelectTafsili = null!, btnSave = null!;
        private Panel pnlClientContract = null!, pnlContractorContract = null!;
        private TableLayoutPanel formLayout = null!;

        private List<ClientContract> _clientContracts = new();
        private List<ContractorContract> _contractorContracts = new();
        private List<Tafsili> _tafsilis = new();
        private List<string> _projectCostTypes = new();
        private List<string> _adminCostTypes = new();

        private readonly SemaphoreSlim _actionLock = new(1, 1);
        private bool _isInitializing = true;

        private class ComboItem
        {
            public int Id { get; set; }
            public string Display { get; set; } = "";
            public int? ClientContractId { get; set; }
            public int PartyId { get; set; }
        }

        public TankhahRowEditForm(string connectionString, int tankhahAccountId, string tankhahTitle, int? transactionId = null)
        {
            _connectionString = connectionString;
            _tankhahAccountId = tankhahAccountId;
            _tankhahTitle = tankhahTitle;
            _transactionId = transactionId;
            _isEdit = transactionId.HasValue;

            _transRepo = new CashTransactionRepository(connectionString);
            _clientContractRepo = new ClientContractRepository(connectionString);
            _contractorContractRepo = new ContractorContractRepository(connectionString);
            _tafsiliRepo = new TafsiliRepository(connectionString);
            _lookupRepo = new LookupRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader(_isEdit ? "ویرایش ردیف تنخواه" : $"ثبت ردیف جدید در {tankhahTitle}", new Size(800, 830));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            rbAdmin.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };
            rbContractorContract.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };
            rbInformalContractor.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };
            rbDirectPurchase.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };
            rbPayroll.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };

            cmbContractorContract.SelectedIndexChanged += (s, e) => { if (!_isInitializing) OnContractorSelected(); };

            Shown += async (s, e) => await LoadDataAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 10, Padding = new Padding(15, 15, 15, 10) };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 0
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 1
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 2
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 3
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F)); // 4: کادر آبی رنگ (کمی کاهش برای جا شدن)
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));  // 5: اتصال به پروژه (همیشه باز)
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));   // 6: قرارداد پیمانکار
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 7: ماهیت و دسته‌بندی
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 8: طرف حساب
            formLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // 9: توضیحات

            txtDate = new TextBox { TextAlign = HorizontalAlignment.Center };
            DateHelper.ApplyDateMask(txtDate);
            txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);

            var txtTankhahDisplay = new TextBox { Text = _tankhahTitle, ReadOnly = true, BackColor = UIHelper.WarmWarningBackground, Font = UIHelper.PersianFont(11f, FontStyle.Bold), TextAlign = HorizontalAlignment.Center };

            UIHelper.AddField(formLayout, "تاریخ:", txtDate, 0, 0, 1);
            UIHelper.AddField(formLayout, "تنخواه:", txtTankhahDisplay, 0, 2, 3);

            txtAmount = new TextBox { Font = UIHelper.PersianFont(12f, FontStyle.Bold), TextAlign = HorizontalAlignment.Center };
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount);

            txtTotal = new TextBox { ReadOnly = true, BackColor = Color.FromArgb(220, 252, 231), ForeColor = UIHelper.ActionGreen, Font = UIHelper.PersianFont(12f, FontStyle.Bold), TextAlign = HorizontalAlignment.Center };

            txtAmount.TextChanged += (s, e) => CalculateTotal();
            UIHelper.AddField(formLayout, "مبلغ پایه (ریال):", txtAmount, 1, 0, 1);
            UIHelper.AddField(formLayout, "جمع کل (ریال):", txtTotal, 1, 2, 3);

            txtDiscount = new TextBox { TextAlign = HorizontalAlignment.Center };
            SmartInputHelper.EnableSmartAmountTextBox(txtDiscount);
            txtDiscount.TextChanged += (s, e) => CalculateTotal();

            txtShipping = new TextBox { TextAlign = HorizontalAlignment.Center };
            SmartInputHelper.EnableSmartAmountTextBox(txtShipping);
            txtShipping.TextChanged += (s, e) => CalculateTotal();

            UIHelper.AddField(formLayout, "تخفیفات:", txtDiscount, 2, 0, 1);
            UIHelper.AddField(formLayout, "کرایه حمل:", txtShipping, 2, 2, 3);

            txtVat = new TextBox { TextAlign = HorizontalAlignment.Center };
            SmartInputHelper.EnableSmartAmountTextBox(txtVat);
            txtVat.TextChanged += (s, e) => CalculateTotal();

            txtAmountInWords = new TextBox { ReadOnly = true, BackColor = UIHelper.WarmInputBackground, ForeColor = UIHelper.ActionGreen, Font = UIHelper.PersianFont(10f, FontStyle.Bold) };

            UIHelper.AddField(formLayout, "مالیات ارزش افزوده:", txtVat, 3, 0, 1);
            UIHelper.AddField(formLayout, "جمع به حروف:", txtAmountInWords, 3, 2, 3);

            var pnlScope = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(10, 5, 10, 5) };
            pnlScope.Paint += (s, e) => ControlPaint.DrawBorder(e.Graphics, pnlScope.ClientRectangle, UIHelper.SectionBlue, 2, ButtonBorderStyle.Solid, UIHelper.SectionBlue, 2, ButtonBorderStyle.Solid, UIHelper.SectionBlue, 2, ButtonBorderStyle.Solid, UIHelper.SectionBlue, 2, ButtonBorderStyle.Solid);

            var scopeLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
            scopeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            scopeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            scopeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            scopeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            scopeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            scopeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

            var lblScopeTitle = new Label { Text = "📌 این هزینه بابت چیست؟ (نوع پرداخت)", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11.5f, FontStyle.Bold), ForeColor = UIHelper.SectionBlue, TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(0, 0, 0, 5) };
            scopeLayout.Controls.Add(lblScopeTitle, 0, 0);
            scopeLayout.SetColumnSpan(lblScopeTitle, 2);

            void SetupRadio(RadioButton rb) { rb.AutoSize = true; rb.Font = UIHelper.PersianFont(10f); rb.Anchor = AnchorStyles.Right; rb.Margin = new Padding(5, 5, 5, 5); }

            rbAdmin = new RadioButton { Text = "🔹 هزینه‌های اداری" }; SetupRadio(rbAdmin);
            rbDirectPurchase = new RadioButton { Text = "🛒 خرید کالا (فروشندگان)" }; SetupRadio(rbDirectPurchase);
            rbInformalContractor = new RadioButton { Text = "🧾 پیمانکار فاکتوری (خدمات)" }; SetupRadio(rbInformalContractor);
            rbContractorContract = new RadioButton { Text = "📋 پیمانکار قراردادی (علی‌الحساب)" }; SetupRadio(rbContractorContract);
            rbPayroll = new RadioButton { Text = "💵 حقوق و مساعده پرسنل" }; SetupRadio(rbPayroll);

            rbAdmin.Checked = true;

            scopeLayout.Controls.Add(rbAdmin, 0, 1);
            scopeLayout.Controls.Add(rbDirectPurchase, 1, 1);
            scopeLayout.Controls.Add(rbInformalContractor, 0, 2);
            scopeLayout.Controls.Add(rbContractorContract, 1, 2);
            scopeLayout.Controls.Add(rbPayroll, 0, 3);

            pnlScope.Controls.Add(scopeLayout);
            formLayout.Controls.Add(pnlScope, 0, 4);
            formLayout.SetColumnSpan(pnlScope, 4);

            // ✅ بخش قرارداد کارفرما (همیشه باز است)
            pnlClientContract = new Panel { Dock = DockStyle.Fill, Visible = true };
            var ccLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            ccLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            ccLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            cmbClientContract = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Display", ValueMember = "Id", Font = UIHelper.PersianFont(11f) };

            ccLayout.Controls.Add(new Label { Text = "اتصال به پروژه (اختیاری):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmErrorText }, 0, 0);
            ccLayout.Controls.Add(cmbClientContract, 1, 0);
            pnlClientContract.Controls.Add(ccLayout);
            formLayout.Controls.Add(pnlClientContract, 0, 5);
            formLayout.SetColumnSpan(pnlClientContract, 4);

            pnlContractorContract = new Panel { Dock = DockStyle.Fill, Visible = false };
            var conLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            conLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            conLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            cmbContractorContract = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Display", ValueMember = "Id", Font = UIHelper.PersianFont(11f) };

            txtAutoContractDisplay = new TextBox { ReadOnly = true, BackColor = UIHelper.WarmInputBackground, ForeColor = UIHelper.SectionBlue, Font = UIHelper.PersianFont(10.5f, FontStyle.Bold), TextAlign = HorizontalAlignment.Center, Dock = DockStyle.Fill };

            conLayout.Controls.Add(new Label { Text = "قرارداد پیمانکار:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmErrorText }, 0, 0);
            conLayout.Controls.Add(cmbContractorContract, 1, 0);
            conLayout.Controls.Add(new Label { Text = "قرارداد کارفرما:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(10f), ForeColor = Color.Gray }, 0, 1);
            conLayout.Controls.Add(txtAutoContractDisplay, 1, 1);
            pnlContractorContract.Controls.Add(conLayout);
            formLayout.Controls.Add(pnlContractorContract, 0, 6);
            formLayout.SetColumnSpan(pnlContractorContract, 4);

            cmbCostType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f) };
            cmbCategory = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f) };

            UIHelper.AddField(formLayout, "ماهیت هزینه:", cmbCostType, 7, 0, 1);
            UIHelper.AddField(formLayout, "دسته‌بندی:", cmbCategory, 7, 2, 3);

            var pnlTafsili = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            pnlTafsili.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlTafsili.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45F));

            cmbTafsili = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayText", ValueMember = "Code", Font = UIHelper.PersianFont(11f) };

            btnSelectTafsili = new Button { Text = "🔍", Dock = DockStyle.Fill, BackColor = UIHelper.SectionBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(5, 0, 0, 0) };
            btnSelectTafsili.FlatAppearance.BorderSize = 0;
            btnSelectTafsili.Click += BtnSelectTafsili_Click;

            pnlTafsili.Controls.Add(cmbTafsili, 0, 0);
            pnlTafsili.Controls.Add(btnSelectTafsili, 1, 0);

            UIHelper.AddField(formLayout, "طرف حساب (دریافت‌کننده):", pnlTafsili, 8, 0, 1, 3);

            txtDescription = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
            UIHelper.AddField(formLayout, "شرح / توضیحات:", txtDescription, 9, 0, 1, 3);

            btnSave = UIHelper.CreateButton(_isEdit ? "💾 ذخیره تغییرات" : "💾 ثبت ردیف در تنخواه", UIHelper.ActionGreen, 220, 45);
            btnSave.Font = UIHelper.PersianFont(12f, FontStyle.Bold);
            btnSave.Click += async (s, e) => await SaveSafeAsync();

            var btnPanel = new Panel { Dock = DockStyle.Fill };
            btnSave.Anchor = AnchorStyles.None;
            btnPanel.Controls.Add(btnSave);
            btnPanel.Resize += (s, e) => { btnSave.Left = (btnPanel.Width - btnSave.Width) / 2; btnSave.Top = (btnPanel.Height - btnSave.Height) / 2; };

            mainLayout.Controls.Add(formLayout, 0, 0);
            mainLayout.Controls.Add(btnPanel, 0, 1);
            this.Controls.Add(mainLayout);
        }

        private void CalculateTotal()
        {
            if (IsDisposed || _isInitializing) return;

            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            decimal discount = NumberHelper.ParseFormatted(txtDiscount.Text);
            decimal shipping = NumberHelper.ParseFormatted(txtShipping.Text);
            decimal vat = NumberHelper.ParseFormatted(txtVat.Text);

            decimal total = amount - discount + shipping + vat;

            txtTotal.Text = NumberHelper.FormatThousands(total, usePersianDigits: true);
            txtAmountInWords.Text = total > 0 ? NumberHelper.ToPersianWords(total) + " ریال" : "";
        }

        private async Task LoadDataAsync()
        {
            if (IsDisposed) return;

            try
            {
                await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var clientContractsTask = Task.Run(() => _clientContractRepo.GetAll());
                    var contractorContractsTask = Task.Run(() => _contractorContractRepo.GetAll());
                    var tafsilisTask = _tafsiliRepo.GetAllAsync();
                    var projectCostTypesTask = _lookupRepo.GetByCategoryAsync("CostType");
                    var adminCostTypesTask = _lookupRepo.GetByCategoryAsync("AdministrativeCostType");
                    var categoriesTask = _lookupRepo.GetByCategoryAsync("TransactionCategory");

                    await Task.WhenAll(clientContractsTask, contractorContractsTask, tafsilisTask, projectCostTypesTask, adminCostTypesTask, categoriesTask);

                    if (IsDisposed) return;

                    _clientContracts = clientContractsTask.Result;
                    _contractorContracts = contractorContractsTask.Result;
                    _tafsilis = tafsilisTask.Result;

                    _projectCostTypes = projectCostTypesTask.Result.Select(i => i.Value).ToList();
                    if (_projectCostTypes.Count == 0) _projectCostTypes.AddRange(new[] { "خرید کالا", "خرید خدمات", "سایر" });

                    _adminCostTypes = adminCostTypesTask.Result.Select(i => i.Value).ToList();
                    if (_adminCostTypes.Count == 0) _adminCostTypes.AddRange(new[] { "اجاره", "آب و برق", "تلفن و اینترنت", "لوازم مصرفی", "پذیرایی", "حقوق و دستمزد", "حمل و نقل", "تعمیر و نگهداری", "سایر" });

                    var categories = categoriesTask.Result.Select(i => i.Value).Where(c => c.Trim() != "انتقال بین حساب‌ها").ToList();
                    if (categories.Count == 0) categories.AddRange(new[] { "عملیاتی", "اداری", "پرسنلی", "هزینه بانکی", "سایر" });

                    var clientContractItems = new List<ComboItem> { new() { Id = 0, Display = "--- بدون اتصال به پروژه ---" } };
                    clientContractItems.AddRange(_clientContracts.Select(c => new ComboItem { Id = c.Id, Display = $"{c.ContractNumber} - {c.Subject}" }));
                    cmbClientContract.DataSource = clientContractItems;

                    var conItems = new List<ComboItem> { new() { Id = 0, Display = "--- انتخاب کنید ---" } };
                    conItems.AddRange(_contractorContracts.Select(c => new ComboItem { Id = c.Id, Display = $"{c.DisplayName} - قرارداد {c.ContractNumber}", ClientContractId = c.ClientContractId, PartyId = c.PartyId }));
                    cmbContractorContract.DataSource = conItems;

                    var tafsiliItems = new List<Tafsili> { new() { Id = 0, Code = "", FirstName = "---", LastName = "انتخاب کنید ---", PersonType = "شخص حقیقی" } };
                    tafsiliItems.AddRange(_tafsilis);
                    cmbTafsili.DataSource = tafsiliItems;
                    cmbTafsili.DisplayMember = "DisplayText";
                    cmbTafsili.ValueMember = "Code";

                    cmbCategory.DataSource = categories;

                    OnScopeChanged();

                    if (_isEdit && _transactionId.HasValue) await LoadTransactionDataAsync(_transactionId.Value);
                }, "در حال بارگذاری اطلاعات...");
            }
            catch (Exception ex) { LogHelper.LogException(ex, "TankhahRowEditForm.LoadDataAsync"); if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری:\n" + ex.Message); }
            finally { _isInitializing = false; }
        }

        private async Task LoadTransactionDataAsync(int id)
        {
            var trans = await _transRepo.GetByIdAsync(id);
            if (trans == null || IsDisposed) return;

            _isInitializing = true;
            try
            {
                txtDate.Text = DateHelper.ToPersianDate(trans.TransactionDate);
                txtAmount.Text = NumberHelper.FormatThousands(trans.BaseAmount, usePersianDigits: true);
                txtDiscount.Text = NumberHelper.FormatThousands(trans.Discount, usePersianDigits: true);
                txtShipping.Text = NumberHelper.FormatThousands(trans.Shipping, usePersianDigits: true);
                txtVat.Text = NumberHelper.FormatThousands(trans.VAT, usePersianDigits: true);
                txtDescription.Text = trans.Description ?? "";

                if (!string.IsNullOrWhiteSpace(trans.Category) && cmbCategory.Items.Contains(trans.Category)) cmbCategory.SelectedItem = trans.Category;
                if (!string.IsNullOrWhiteSpace(trans.TafsiliCode)) cmbTafsili.SelectedValue = trans.TafsiliCode;

                string scope = trans.PaymentScope ?? PaymentScopes.Administrative;
                string costType = trans.CostType ?? "";

                if (scope == PaymentScopes.ContractorContract)
                {
                    rbContractorContract.Checked = true; SelectContractorContract(trans.ContractorContractId);
                }
                else if (costType == "حقوق و دستمزد")
                {
                    rbPayroll.Checked = true;
                }
                else if (scope == PaymentScopes.DirectPurchase)
                {
                    rbDirectPurchase.Checked = true; SelectClientContract(trans.ContractId);
                }
                else if (scope == PaymentScopes.InformalContractor)
                {
                    rbInformalContractor.Checked = true; SelectClientContract(trans.ContractId);
                }
                else
                {
                    rbAdmin.Checked = true; SelectClientContract(trans.ContractId);
                }
            }
            finally { _isInitializing = false; }

            CalculateTotal();
            OnScopeChanged();
        }

        private void SelectClientContract(int? id)
        {
            if (!id.HasValue || cmbClientContract.Items.Count == 0) { cmbClientContract.SelectedIndex = 0; return; }
            for (int i = 0; i < cmbClientContract.Items.Count; i++)
            {
                if (cmbClientContract.Items[i] is ComboItem item && item.Id == id.Value) { cmbClientContract.SelectedIndex = i; return; }
            }
            cmbClientContract.SelectedIndex = 0;
        }

        private void SelectContractorContract(int? id)
        {
            if (!id.HasValue || cmbContractorContract.Items.Count == 0) return;
            for (int i = 0; i < cmbContractorContract.Items.Count; i++)
            {
                if (cmbContractorContract.Items[i] is ComboItem item && item.Id == id.Value) { cmbContractorContract.SelectedIndex = i; return; }
            }
        }

        private void OnScopeChanged()
        {
            if (IsDisposed || formLayout == null) return;

            bool showContractorContract = rbContractorContract.Checked;

            formLayout.RowStyles[5].Height = 45F; // همیشه باز
            formLayout.RowStyles[6].Height = showContractorContract ? 75F : 0F;

            if (pnlClientContract != null) pnlClientContract.Visible = true;
            if (pnlContractorContract != null) pnlContractorContract.Visible = showContractorContract;

            if (!_isInitializing && cmbCostType != null)
            {
                if (rbAdmin.Checked)
                {
                    cmbCostType.DataSource = _adminCostTypes.ToList();
                }
                else if (rbPayroll.Checked)
                {
                    cmbCostType.DataSource = _adminCostTypes.ToList();
                    if (cmbCostType.Items.Contains("حقوق و دستمزد")) cmbCostType.SelectedItem = "حقوق و دستمزد";
                }
                else
                {
                    cmbCostType.DataSource = _projectCostTypes.ToList();
                    if (rbDirectPurchase.Checked && cmbCostType.Items.Contains("خرید کالا")) cmbCostType.SelectedItem = "خرید کالا";
                    if (rbInformalContractor.Checked && cmbCostType.Items.Contains("خرید خدمات")) cmbCostType.SelectedItem = "خرید خدمات";
                }
            }
            else if (cmbCostType != null && cmbCostType.Items.Count == 0)
            {
                cmbCostType.DataSource = _adminCostTypes.ToList();
            }
        }

        private void OnContractorSelected()
        {
            if (cmbContractorContract.SelectedItem is not ComboItem selected || selected.Id == 0) { txtAutoContractDisplay.Text = ""; return; }

            if (selected.ClientContractId.HasValue)
            {
                var clientContract = _clientContracts.FirstOrDefault(c => c.Id == selected.ClientContractId.Value);
                txtAutoContractDisplay.Text = clientContract != null ? $"{clientContract.ContractNumber} - {clientContract.Subject}" : "--- ارتباط ندارد ---";
            }
            else txtAutoContractDisplay.Text = "--- (بدون ارتباط) ---";

            TryAutoFillTafsiliFromParty(selected.PartyId);
        }

        private void TryAutoFillTafsiliFromParty(int partyId)
        {
            Task.Run(() =>
            {
                try
                {
                    using var conn = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                    conn.Open();
                    using var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT NationalCode, NationalId FROM Parties WHERE Id = @id", conn);
                    cmd.Parameters.AddWithValue("@id", partyId);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        string? nc = reader.IsDBNull(0) ? null : reader.GetString(0);
                        string? nid = reader.IsDBNull(1) ? null : reader.GetString(1);
                        return (nc, nid);
                    }
                    return (null, null);
                }
                catch { return (null, null); }
            }).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && !IsDisposed)
                {
                    var (nc, nid) = t.Result;
                    var match = _tafsilis.FirstOrDefault(x => (!string.IsNullOrEmpty(nc) && x.NationalCode == nc) || (!string.IsNullOrEmpty(nid) && x.NationalId == nid));
                    if (match != null) this.Invoke(new Action(() => cmbTafsili.SelectedValue = match.Code));
                }
            });
        }

        private void BtnSelectTafsili_Click(object? sender, EventArgs e)
        {
            using var form = new TafsiliSelectionForm(_connectionString);
            if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed) cmbTafsili.SelectedValue = form.SelectedCode;
        }

        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
                decimal discount = NumberHelper.ParseFormatted(txtDiscount.Text);
                decimal shipping = NumberHelper.ParseFormatted(txtShipping.Text);
                decimal vat = NumberHelper.ParseFormatted(txtVat.Text);

                decimal totalAmount = amount - discount + shipping + vat;
                txtTotal.Text = NumberHelper.FormatThousands(totalAmount, usePersianDigits: true);

                if (totalAmount <= 0) { ModernMessageBox.ShowWarning("جمع کل باید بزرگتر از صفر باشد."); txtAmount.Focus(); return; }

                DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
                if (date == null) { ModernMessageBox.ShowWarning("تاریخ معتبر نیست."); return; }
                if (string.IsNullOrWhiteSpace(txtDescription.Text)) { ModernMessageBox.ShowWarning("لطفاً شرح ردیف را وارد کنید."); txtDescription.Focus(); return; }

                string paymentScope;
                int? clientContractId = null;
                int? contractorContractId = null;
                string contractTitle = "";
                string category = cmbCategory.Text;

                if (rbAdmin.Checked)
                {
                    paymentScope = PaymentScopes.Administrative;
                    contractTitle = "هزینه‌های اداری";
                    if (cmbClientContract.SelectedItem is ComboItem adminItem && adminItem.Id > 0)
                    {
                        clientContractId = adminItem.Id;
                        contractTitle = adminItem.Display;
                    }
                }
                else if (rbPayroll.Checked)
                {
                    paymentScope = PaymentScopes.Administrative;
                    contractTitle = "حقوق و دستمزد پرسنل";
                    category = "پرسنلی";
                    if (cmbClientContract.SelectedItem is ComboItem payItem && payItem.Id > 0)
                    {
                        clientContractId = payItem.Id;
                        contractTitle = payItem.Display;
                    }
                }
                else if (rbDirectPurchase.Checked || rbInformalContractor.Checked)
                {
                    paymentScope = rbDirectPurchase.Checked ? PaymentScopes.DirectPurchase : PaymentScopes.InformalContractor;
                    if (cmbClientContract.SelectedItem is not ComboItem clientItem || clientItem.Id <= 0) { ModernMessageBox.ShowWarning("لطفاً قرارداد کارفرما را انتخاب کنید."); return; }
                    clientContractId = clientItem.Id;
                    contractTitle = clientItem.Display;
                }
                else
                {
                    paymentScope = PaymentScopes.ContractorContract;
                    if (cmbContractorContract.SelectedItem is not ComboItem conItem || conItem.Id <= 0) { ModernMessageBox.ShowWarning("لطفاً قرارداد پیمانکار را انتخاب کنید."); return; }
                    contractorContractId = conItem.Id;
                    clientContractId = conItem.ClientContractId;
                    contractTitle = conItem.Display;
                }

                string tafsiliCode = cmbTafsili.SelectedValue?.ToString() ?? "";
                string tafsiliTitle = "";
                if (!string.IsNullOrWhiteSpace(tafsiliCode) && cmbTafsili.SelectedItem is Tafsili selectedTaf) tafsiliTitle = selectedTaf.Name;

                var trans = new CashTransaction
                {
                    Id = _transactionId ?? 0,
                    AccountId = _tankhahAccountId,
                    AccountTitle = _tankhahTitle,
                    PaymentScope = paymentScope,
                    ContractId = clientContractId,
                    ContractorContractId = contractorContractId,
                    ContractTitle = contractTitle,
                    TransactionDate = date.Value,
                    Amount = totalAmount,
                    Discount = discount,
                    Shipping = shipping,
                    VAT = vat,
                    TransactionType = "نقدی",
                    Direction = DirectionTypes.Outgoing,
                    Category = category,
                    CostType = cmbCostType.Text,
                    TafsiliCode = string.IsNullOrWhiteSpace(tafsiliCode) ? null : tafsiliCode,
                    TafsiliTitle = string.IsNullOrWhiteSpace(tafsiliTitle) ? null : tafsiliTitle,
                    RecipientName = string.IsNullOrWhiteSpace(tafsiliTitle) ? null : tafsiliTitle,
                    Description = txtDescription.Text.Trim()
                };

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    if (_isEdit && _transactionId.HasValue)
                    {
                        await _transRepo.UpdateAsync(trans);
                    }
                    else
                    {
                        await _transRepo.AddAsync(trans);
                    }
                }, "در حال ذخیره...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show(_isEdit ? "تغییرات ذخیره شد." : "ردیف با موفقیت به تنخواه اضافه شد.", isSuccess: true);
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (Exception ex) { LogHelper.LogException(ex, "TankhahRowEditForm.SaveSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message); }
            finally { try { _actionLock.Release(); } catch { } }
        }
    }
}