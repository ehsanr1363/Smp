using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class CashTransactionEditForm : Form
{
    private readonly string _connectionString;
    private readonly int? _transactionId;
    private readonly bool _isEdit;

    private readonly CashTransactionRepository _transRepo;
    private readonly BankAccountRepository _bankRepo;
    private readonly ClientContractRepository _clientContractRepo;
    private readonly ContractorContractRepository _contractorContractRepo;
    private readonly TafsiliRepository _tafsiliRepo;
    private readonly LookupRepository _lookupRepo;

    private ComboBox cmbAccount = null!;
    private TextBox txtDate = null!, txtDescription = null!, txtAmount = null!;
    private RadioButton rbAdmin = null!, rbContractorContract = null!, rbInformalContractor = null!, rbDirectPurchase = null!, rbPayroll = null!, rbProjectExpense = null!;
    private ComboBox cmbClientContract = null!, cmbContractorContract = null!, cmbCostType = null!, cmbTafsili = null!;
    private Label lblAmountWords = null!;
    private string? _loadedCategory;
    private string _loadedScopeKey = "";
    private TextBox txtAutoContractDisplay = null!;
    private Button btnSave = null!, btnSelectTafsili = null!, btnPrintOrder = null!;
    private TableLayoutPanel pnlClientContract = null!, pnlContractorContract = null!;
    private TableLayoutPanel linkBody = null!;
    private ModernPage formLayout = null!;

    private List<CompanyBankAccount> _accounts = new();
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

    public CashTransactionEditForm(string connectionString, int? transactionId = null)
    {
        _connectionString = connectionString;
        _transactionId = transactionId;
        _isEdit = transactionId.HasValue;

        _transRepo = new CashTransactionRepository(connectionString);
        _bankRepo = new BankAccountRepository(connectionString);
        _clientContractRepo = new ClientContractRepository(connectionString);
        _contractorContractRepo = new ContractorContractRepository(connectionString);
        _tafsiliRepo = new TafsiliRepository(connectionString);
        _lookupRepo = new LookupRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader(_isEdit ? "ویرایش حواله" : "ثبت حواله / نقدی جدید", new Size(800, 700));
        ApplyLayoutHeight();
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        rbAdmin.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };
        rbContractorContract.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };
        rbInformalContractor.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };
        rbDirectPurchase.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };
        rbPayroll.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };
        rbProjectExpense.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };

        cmbContractorContract.SelectedIndexChanged += (s, e) => { if (!_isInitializing) OnContractorSelected(); };
        cmbTafsili.SelectedIndexChanged += (s, e) => { if (!_isInitializing) OnTafsiliChanged(); };

        Shown += async (s, e) => await LoadDataAsync();
    }

    // ───────────── ابعاد ثابت چیدمان ─────────────
    private const int FieldHeight = ModernUI.FieldHeight;   // لیبل + ورودی
    private const int AmountWordsHeight = 30;   // ردیف «مبلغ به حروف»
    private const int HeaderHeight = 50;        // هدر مدرن فرم (ModernForm.CreateHeader)
    private const int FooterHeight = 64;
    private const int CardGap = 8;
    private int _contentHeight;

    /// <summary>ارتفاع فرم را به اندازه‌ی محتوا می‌کند (اگر صفحه کوچک بود، محتوا اسکرول می‌خورد)</summary>
    private void ApplyLayoutHeight()
    {
        if (formLayout is null) return;
        ClientSize = new Size(ClientSize.Width, HeaderHeight + FooterHeight + _contentHeight);

        var wa = (IsHandleCreated ? Screen.FromControl(this) : Screen.PrimaryScreen)?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        int maxHeight = wa.Height - 10;
        if (Height > maxHeight) Height = maxHeight;
        if (IsHandleCreated && Bottom > wa.Bottom) Top = Math.Max(wa.Top, wa.Bottom - Height);
    }

    private void BuildUI()
    {
        int FH = FieldHeight;

        // ───────────── کارت ۱: اطلاعات اصلی ─────────────
        var cardMain = new ModernCard("💳 اطلاعات اصلی پرداخت", ModernTheme.Primary, 2, FH, FH, AmountWordsHeight);

        cmbAccount = ModernUI.Combo();
        cmbAccount.DisplayMember = "DisplayTitle"; cmbAccount.ValueMember = "Id";

        var dateBox = ModernUI.Input();
        dateBox.TextAlign = HorizontalAlignment.Center;
        txtDate = dateBox.TextBox;
        DateHelper.ApplyDateMask(txtDate);
        txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);

        var amountBox = ModernUI.Money();
        amountBox.Font = ModernTheme.GetFont(13f, FontStyle.Bold);
        txtAmount = amountBox.TextBox;
        txtAmount.ForeColor = ModernTheme.Dark;

        lblAmountWords = ModernUI.Caption("", 10.5f, true, ModernTheme.Primary);
        lblAmountWords.Dock = DockStyle.Fill;
        lblAmountWords.AutoEllipsis = true;
        lblAmountWords.Margin = new Padding(8, 0, 8, 0);
        txtAmount.TextChanged += (s, e) => UpdateAmountWords();

        cardMain.Body.Controls.Add(ModernUI.Field("حساب / صندوق", cmbAccount), 0, 0);
        cardMain.Body.Controls.Add(ModernUI.Field("تاریخ", dateBox), 1, 0);
        var fldAmount = ModernUI.Field("مبلغ", amountBox);
        cardMain.Body.Controls.Add(fldAmount, 0, 1);
        cardMain.Body.SetColumnSpan(fldAmount, 2);
        cardMain.Body.Controls.Add(lblAmountWords, 0, 2);
        cardMain.Body.SetColumnSpan(lblAmountWords, 2);

        // ───────────── کارت ۲: نوع پرداخت (کاشی‌های انتخابی) ─────────────
        var cardScope = new ModernCard("📌 این هزینه بابت چیست؟ (نوع پرداخت)", ModernTheme.Orange, 3, 40, 40);
        ModernChoiceTile Tile(string text) => new ModernChoiceTile { Text = text, Dock = DockStyle.Fill, Margin = new Padding(5, 4, 5, 4) };

        rbAdmin = Tile("🔹 هزینه‌های اداری");
        rbPayroll = Tile("💵 هزینه حقوق و مساعده");
        rbProjectExpense = Tile("🏗️ هزینه‌های پروژه");
        rbDirectPurchase = Tile("🛒 خرید مستقیم برای پروژه");
        rbInformalContractor = Tile("🧾 پیمانکار بدون قرارداد");
        rbContractorContract = Tile("📋 پیمانکار با قرارداد");
        rbAdmin.Checked = true;

        cardScope.Body.Controls.Add(rbAdmin, 0, 0);
        cardScope.Body.Controls.Add(rbPayroll, 1, 0);
        cardScope.Body.Controls.Add(rbProjectExpense, 2, 0);
        cardScope.Body.Controls.Add(rbDirectPurchase, 0, 1);
        cardScope.Body.Controls.Add(rbInformalContractor, 1, 1);
        cardScope.Body.Controls.Add(rbContractorContract, 2, 1);

        // ───────────── کارت ۳: اتصال و طرف حساب ─────────────
        // ردیف ۱: پروژه (مخفی در حالت پیمانکار با قرارداد) | ردیف ۲: ماهیت هزینه + طرف حساب | ردیف ۳: قرارداد پیمانکار + پروژه‌ی خودکار (فقط پیمانکار با قرارداد)
        var cardLink = new ModernCard("🔗 اتصال و طرف حساب", ModernTheme.Teal, 2, FH, FH, 0);
        linkBody = cardLink.Body;

        cmbClientContract = ModernUI.Combo();
        cmbClientContract.DisplayMember = "Display"; cmbClientContract.ValueMember = "Id";
        pnlClientContract = ModernUI.Field("اتصال به پروژه", cmbClientContract, ModernTheme.DangerText);
        linkBody.Controls.Add(pnlClientContract, 0, 0);
        linkBody.SetColumnSpan(pnlClientContract, 2);

        cmbCostType = ModernUI.Combo();
        linkBody.Controls.Add(ModernUI.Field("ماهیت هزینه", cmbCostType), 0, 1);

        var pnlTafsili = new BufferedTable { Margin = Padding.Empty, BackColor = Color.Transparent, ColumnCount = 2, RowCount = 1 };
        pnlTafsili.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pnlTafsili.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));
        pnlTafsili.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        cmbTafsili = ModernUI.Combo();
        cmbTafsili.DisplayMember = "DisplayText"; cmbTafsili.ValueMember = "Code";
        cmbTafsili.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        cmbTafsili.Margin = Padding.Empty;
        var btnTaf = ModernButton.Primary("🔍", 44, 36);
        btnTaf.Dock = DockStyle.Fill;
        btnTaf.Margin = new Padding(6, 0, 0, 0);
        btnTaf.Click += BtnSelectTafsili_Click;
        btnSelectTafsili = btnTaf;
        pnlTafsili.Controls.Add(cmbTafsili, 0, 0);
        pnlTafsili.Controls.Add(btnTaf, 1, 0);
        linkBody.Controls.Add(ModernUI.Field("طرف حساب (پیمانکار / دریافت‌کننده)", pnlTafsili), 1, 1);

        // پیمانکار با قرارداد: با انتخاب طرف حساب، فقط قراردادهای همان پیمانکار در این کمبو می‌آید
        cmbContractorContract = ModernUI.Combo();
        cmbContractorContract.DisplayMember = "Display"; cmbContractorContract.ValueMember = "Id";
        var autoBox = ModernUI.Input();
        autoBox.ReadOnly = true;
        autoBox.TextAlign = HorizontalAlignment.Center;
        autoBox.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
        txtAutoContractDisplay = autoBox.TextBox;
        txtAutoContractDisplay.ForeColor = ModernTheme.Primary;
        txtAutoContractDisplay.TabStop = false;

        pnlContractorContract = new BufferedTable { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Color.Transparent, Visible = false, ColumnCount = 2, RowCount = 1 };
        pnlContractorContract.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        pnlContractorContract.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        pnlContractorContract.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        pnlContractorContract.Controls.Add(ModernUI.Field("قرارداد پیمانکار", cmbContractorContract, ModernTheme.DangerText), 0, 0);
        pnlContractorContract.Controls.Add(ModernUI.Field("پروژه‌ی قرارداد (خودکار)", autoBox, ModernTheme.TextMuted), 1, 0);
        linkBody.Controls.Add(pnlContractorContract, 0, 2);
        linkBody.SetColumnSpan(pnlContractorContract, 2);

        // ───────────── کارت ۴: توضیحات ─────────────
        var cardDesc = new ModernCard("📝 شرح / توضیحات", ModernTheme.Neutral, 1, ModernUI.InputHeight + 4);
        var memo = ModernUI.Input();   // توضیحات یک خط کافی است
        memo.Dock = DockStyle.Fill;
        memo.Margin = new Padding(6, 2, 6, 2);
        txtDescription = memo.TextBox;
        cardDesc.Body.Controls.Add(memo, 0, 0);

        // ───────────── صفحه‌ی اسکرول‌دار + نوار دکمه‌ها ─────────────
        var cards = new Control[] { cardMain, cardScope, cardLink, cardDesc };
        formLayout = new ModernPage();
        foreach (var c in cards) formLayout.AddCard(c, CardGap);
        _contentHeight = cards.Sum(c => c.Height + CardGap) + formLayout.Padding.Vertical;

        var save = ModernButton.Success(_isEdit ? "💾 ذخیره تغییرات" : "💾 ثبت حواله", 200, 40);
        save.Click += async (s, e) => await SaveSafeAsync();
        btnSave = save;

        var cancel = ModernButton.Cancel();
        cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        // 🖨️ ذخیره‌ی حواله و بلافاصله چاپ دستور پرداخت
        var printOrder = ModernButton.Primary("🖨️ چاپ دستور پرداخت", 190, 40);
        printOrder.Click += async (s, e) => await SaveSafeAsync(printAfterSave: true);
        btnPrintOrder = printOrder;

        var bar = ModernForm.CreateButtonBar(save, printOrder, cancel);
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight));
        root.Controls.Add(formLayout, 0, 0);
        root.Controls.Add(bar, 0, 1);
        Controls.Add(root);
    }

    private async Task LoadDataAsync()
    {
        if (IsDisposed) return;
        try
        {
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                var accountsTask = _bankRepo.GetActiveAsync();
                var clientContractsTask = Task.Run(() => _clientContractRepo.GetAll());
                var contractorContractsTask = Task.Run(() => _contractorContractRepo.GetAll());
                var tafsilisTask = _tafsiliRepo.GetAllAsync();
                var projectCostTypesTask = _lookupRepo.GetByCategoryAsync("CostType");
                var adminCostTypesTask = _lookupRepo.GetByCategoryAsync("AdministrativeCostType");

                await Task.WhenAll(accountsTask, clientContractsTask, contractorContractsTask, tafsilisTask, projectCostTypesTask, adminCostTypesTask);

                if (IsDisposed) return;

                _accounts = accountsTask.Result;
                _clientContracts = clientContractsTask.Result;
                _contractorContracts = contractorContractsTask.Result;
                _tafsilis = tafsilisTask.Result;

                _projectCostTypes = projectCostTypesTask.Result.Select(i => i.Value).ToList();
                if (_projectCostTypes.Count == 0) _projectCostTypes.AddRange(new[] { "خرید کالا", "خرید خدمات", "حمل و نقل", "سایر" });
                else if (!_projectCostTypes.Contains("حمل و نقل")) _projectCostTypes.Add("حمل و نقل");

                _adminCostTypes = adminCostTypesTask.Result.Select(i => i.Value).ToList();
                if (_adminCostTypes.Count == 0) _adminCostTypes.AddRange(new[] { "اجاره", "آب و برق", "تلفن و اینترنت", "لوازم مصرفی", "پذیرایی", "حقوق و دستمزد", "حمل و نقل", "تعمیر و نگهداری", "سایر" });

                cmbAccount.DataSource = _accounts;

                var clientContractItems = new List<ComboItem> { new() { Id = 0, Display = "--- بدون اتصال به پروژه ---" } };
                clientContractItems.AddRange(_clientContracts.Select(c => new ComboItem { Id = c.Id, Display = $"{c.ContractNumber} - {c.Subject}" }));
                cmbClientContract.DataSource = clientContractItems;

                RebindTafsili();
                RefreshContractorContracts(null);

                OnScopeChanged(applyDefaults: true);   // پر شدن ماهیت هزینه + فعال/غیرفعال شدن فیلدها در بارگذاری اولیه

                if (_isEdit && _transactionId.HasValue) await LoadTransactionDataAsync(_transactionId.Value);
                else if (cmbAccount.Items.Count > 0) cmbAccount.SelectedIndex = 0;

            }, "در حال بارگذاری اطلاعات...");
        }
        catch (Exception ex) { LogHelper.LogException(ex, "CashTransactionEditForm.LoadDataAsync"); if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
        finally { _isInitializing = false; }
    }

    private async Task LoadTransactionDataAsync(int id)
    {
        var trans = await _transRepo.GetByIdAsync(id);
        if (trans is null || IsDisposed) return;

        _isInitializing = true;
        try
        {
            if (trans.AccountId.HasValue) cmbAccount.SelectedValue = trans.AccountId.Value;
            txtDate.Text = DateHelper.ToPersianDate(trans.TransactionDate);
            txtAmount.Text = NumberHelper.FormatThousands(trans.Amount, usePersianDigits: true);
            txtDescription.Text = trans.Description ?? "";
            _loadedCategory = trans.Category;

            // ۱) نوع پرداخت  ۲) چیدمان/فهرست‌ها  ۳) مقادیر ذخیره‌شده (تا بازنویسی نشوند)
            string scope = trans.PaymentScope ?? PaymentScopes.Administrative;
            string costType = trans.CostType ?? "";

            if (scope == PaymentScopes.ContractorContract) rbContractorContract.Checked = true;
            else if (scope == PaymentScopes.DirectPurchase) rbDirectPurchase.Checked = true;
            else if (scope == PaymentScopes.InformalContractor) rbInformalContractor.Checked = true;
            else if (scope == PaymentScopes.ProjectExpense) rbProjectExpense.Checked = true;
            else if (costType == "حقوق و دستمزد") rbPayroll.Checked = true;
            else rbAdmin.Checked = true;

            OnScopeChanged(applyDefaults: false);

            if (!string.IsNullOrWhiteSpace(costType))
            {
                if (!cmbCostType.Items.Contains(costType) && cmbCostType.DataSource is List<string> costList)
                {
                    costList.Add(costType);
                    cmbCostType.DataSource = null; cmbCostType.DataSource = costList;
                }
                cmbCostType.SelectedItem = costType;
            }

            if (!string.IsNullOrWhiteSpace(trans.TafsiliCode)) TrySelectTafsili(0, trans.TafsiliCode!);

            if (scope == PaymentScopes.ContractorContract) RefreshContractorContracts(trans.ContractorContractId);
            else if (cmbClientContract.Enabled) SelectClientContract(trans.ContractId);

            _loadedScopeKey = ScopeKey();
        }
        finally { _isInitializing = false; }
    }

    private string ScopeKey() =>
        rbAdmin.Checked ? "admin" : rbPayroll.Checked ? "payroll" : rbProjectExpense.Checked ? "project" :
        rbDirectPurchase.Checked ? "direct" : rbInformalContractor.Checked ? "informal" : "contractor";

    private void UpdateAmountWords()
    {
        if (lblAmountWords is null) return;
        lblAmountWords.Text = NumberHelper.TryParseFormatted(txtAmount.Text, out decimal v) && v > 0
            ? "مبلغ به حروف: " + NumberHelper.ToPersianWords(v) + " ریال"
            : "";
    }

    private void SelectClientContract(int? id)
    {
        if (!id.HasValue || cmbClientContract.Items.Count == 0) { cmbClientContract.SelectedIndex = 0; return; }
        for (int i = 0; i < cmbClientContract.Items.Count; i++)
            if (cmbClientContract.Items[i] is ComboItem item && item.Id == id.Value) { cmbClientContract.SelectedIndex = i; return; }
        cmbClientContract.SelectedIndex = 0;
    }

    private void SelectContractorContract(int? id)
    {
        if (!id.HasValue || cmbContractorContract.Items.Count == 0) return;
        for (int i = 0; i < cmbContractorContract.Items.Count; i++)
            if (cmbContractorContract.Items[i] is ComboItem item && item.Id == id.Value) { cmbContractorContract.SelectedIndex = i; return; }
    }

    private void OnScopeChanged() => OnScopeChanged(true);

    private void OnScopeChanged(bool applyDefaults)
    {
        if (IsDisposed || formLayout is null) return;

        bool isContractorContract = rbContractorContract.Checked;
        bool isAdmin = rbAdmin.Checked;
        bool isProject = rbProjectExpense.Checked;
        bool isAdminOrProject = isAdmin || isProject;

        // پیمانکار با قرارداد: ردیف پروژه مخفی و ردیف «قرارداد پیمانکار» نمایش داده می‌شود (ارتفاع کل ثابت می‌ماند)
        if (linkBody is not null)
        {
            linkBody.RowStyles[0].Height = isContractorContract ? 0F : FieldHeight;
            linkBody.RowStyles[2].Height = isContractorContract ? FieldHeight : 0F;
        }
        if (pnlClientContract is not null) pnlClientContract.Visible = !isContractorContract;
        if (pnlContractorContract is not null) pnlContractorContract.Visible = isContractorContract;

        if (cmbCostType is null || cmbClientContract is null) return;

        // هزینه اداری/پروژه: ماهیت هزینه فعال؛ هزینه اداری: اتصال به پروژه غیرفعال، هزینه پروژه: باید به پروژه وصل شود
        cmbCostType.Enabled = isAdminOrProject;
        cmbClientContract.Enabled = !isAdmin && !isContractorContract;

        // اداری: فقط لیست اداری | پروژه: لیست هزینه‌های پروژه + لیست هزینه‌های اداری
        cmbCostType.DataSource = (isAdmin ? _adminCostTypes
            : isProject ? _projectCostTypes.Union(_adminCostTypes).ToList()
            : _projectCostTypes).ToList();

        if (!isAdminOrProject)
        {
            if (rbDirectPurchase.Checked && cmbCostType.Items.Contains("خرید کالا")) cmbCostType.SelectedItem = "خرید کالا";
            else if (rbInformalContractor.Checked && cmbCostType.Items.Contains("خرید خدمات")) cmbCostType.SelectedItem = "خرید خدمات";
            else if (rbPayroll.Checked && cmbCostType.Items.Contains("حقوق و دستمزد")) cmbCostType.SelectedItem = "حقوق و دستمزد";
            else if (isContractorContract && cmbCostType.Items.Contains("خدمات پیمانکاری")) cmbCostType.SelectedItem = "خدمات پیمانکاری";
            else if (isContractorContract && cmbCostType.Items.Contains("خرید خدمات")) cmbCostType.SelectedItem = "خرید خدمات";
        }

        if (applyDefaults && !cmbClientContract.Enabled && cmbClientContract.Items.Count > 0) cmbClientContract.SelectedIndex = 0;
        if (applyDefaults && isContractorContract) RefreshContractorContracts(null);
    }

    // ───────────── پیمانکار با قرارداد: قراردادهای همان پیمانکار ─────────────
    private static string NormId(string? s) => PersianText.NormalizeIdentifier(s);

    /// <summary>آیا این قرارداد متعلق به طرف حساب انتخاب‌شده است؟ (تطبیق با کد ملی / شناسه ملی)</summary>
    private static bool BelongsTo(ContractorContract c, Tafsili t)
    {
        string tc = NormId(t.NationalCode), tn = NormId(t.NationalId);
        string cc = NormId(c.NationalCode), cn = NormId(c.NationalId);
        return (tc != "" && (tc == cc || tc == cn)) || (tn != "" && (tn == cn || tn == cc));
    }

    private static string ContractLabel(ContractorContract c)
    {
        string status = !string.IsNullOrWhiteSpace(c.Status) && c.Status.Trim() != ContractStatuses.Active ? $" ({c.Status})" : "";
        return $"قرارداد {c.ContractNumber} - {c.Subject}{status}";
    }

    /// <summary>
    /// فهرست قراردادهای پیمانکار را بر اساس طرف حساب انتخاب‌شده می‌سازد.
    /// includeId: قراردادی که حتماً باید در فهرست بماند و انتخاب شود (مثلاً در ویرایش).
    /// </summary>
    private void RefreshContractorContracts(int? includeId)
    {
        if (cmbContractorContract is null || cmbTafsili is null) return;

        var tafsili = cmbTafsili.SelectedItem as Tafsili;
        bool hasTafsili = tafsili is not null && tafsili.Id != 0;

        var list = hasTafsili ? _contractorContracts.Where(c => BelongsTo(c, tafsili!)).ToList() : new List<ContractorContract>();
        if (includeId.HasValue && list.All(c => c.Id != includeId.Value))
        {
            var saved = _contractorContracts.FirstOrDefault(c => c.Id == includeId.Value);
            if (saved is not null) list.Add(saved);
        }

        string placeholder = list.Count > 0 ? "--- انتخاب قرارداد ---"
                           : hasTafsili ? "--- این پیمانکار قراردادی ندارد ---"
                           : "--- ابتدا طرف حساب (پیمانکار) را انتخاب کنید ---";

        var items = new List<ComboItem> { new() { Id = 0, Display = placeholder } };
        items.AddRange(list.Select(c => new ComboItem { Id = c.Id, Display = ContractLabel(c), ClientContractId = c.ClientContractId, PartyId = c.PartyId }));

        bool prev = _isInitializing; _isInitializing = true;
        try
        {
            cmbContractorContract.DisplayMember = "Display"; cmbContractorContract.ValueMember = "Id";
            cmbContractorContract.DataSource = items;
            cmbContractorContract.Enabled = list.Count > 0;

            int idx = includeId.HasValue ? items.FindIndex(i => i.Id == includeId.Value) : -1;
            if (idx < 0 && list.Count == 1) idx = 1;   // فقط یک قرارداد دارد → خودکار انتخاب می‌شود
            cmbContractorContract.SelectedIndex = idx >= 0 ? idx : 0;
        }
        finally { _isInitializing = prev; }

        UpdateAutoContractDisplay();
    }

    private void OnTafsiliChanged()
    {
        if (!rbContractorContract.Checked) return;

        // اگر قرارداد انتخاب‌شده متعلق به همین طرف حساب است، حفظ شود
        int? keep = null;
        if (cmbContractorContract.SelectedItem is ComboItem cur && cur.Id > 0 && cmbTafsili.SelectedItem is Tafsili t && t.Id != 0)
        {
            var c = _contractorContracts.FirstOrDefault(x => x.Id == cur.Id);
            if (c is not null && BelongsTo(c, t)) keep = cur.Id;
        }
        RefreshContractorContracts(keep);
    }

    private void UpdateAutoContractDisplay()
    {
        if (cmbContractorContract.SelectedItem is not ComboItem selected || selected.Id == 0) { txtAutoContractDisplay.Text = ""; return; }
        if (selected.ClientContractId.HasValue)
        {
            var clientContract = _clientContracts.FirstOrDefault(c => c.Id == selected.ClientContractId.Value);
            txtAutoContractDisplay.Text = clientContract is not null ? $"{clientContract.ContractNumber} - {clientContract.Subject}" : "--- ارتباط ندارد ---";
        }
        else txtAutoContractDisplay.Text = "--- (بدون ارتباط) ---";
    }

    private void OnContractorSelected()
    {
        UpdateAutoContractDisplay();
        // قراردادهای قدیمی بدون طرف حساب: طرف حساب از روی پیمانکار قرارداد پر می‌شود
        if (cmbContractorContract.SelectedItem is ComboItem selected && selected.Id > 0 && !(cmbTafsili.SelectedItem is Tafsili { Id: > 0 }))
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
                if (match is not null) this.Invoke(new Action(() => TrySelectTafsili(match.Id, match.Code)));
            }
        });
    }

    // ───────────── طرف حساب ─────────────
    private void RebindTafsili()
    {
        bool prev = _isInitializing; _isInitializing = true;
        try
        {
            var items = new List<Tafsili> { new() { Id = 0, Code = "", FirstName = "", LastName = "--- انتخاب کنید ---", PersonType = "شخص حقیقی" } };
            items.AddRange(_tafsilis);
            cmbTafsili.DisplayMember = "DisplayText"; cmbTafsili.ValueMember = "Code";
            cmbTafsili.DataSource = items;
        }
        finally { _isInitializing = prev; }
    }

    /// <summary>انتخاب طرف حساب بر اساس شناسه/کد (بدون وابستگی به SelectedValue)</summary>
    private bool TrySelectTafsili(int id, string code)
    {
        for (int i = 0; i < cmbTafsili.Items.Count; i++)
        {
            if (cmbTafsili.Items[i] is Tafsili t && t.Id != 0 && ((id != 0 && t.Id == id) || (!string.IsNullOrEmpty(code) && t.Code == code)))
            {
                if (cmbTafsili.SelectedIndex != i) cmbTafsili.SelectedIndex = i;
                else if (!_isInitializing) OnTafsiliChanged();
                return true;
            }
        }
        return false;
    }

    private async void BtnSelectTafsili_Click(object? sender, EventArgs e)
    {
        using var form = new TafsiliSelectionForm(_connectionString);
        if (form.ShowDialog(this) != DialogResult.OK || IsDisposed) return;
        int id = form.SelectedId; string code = form.SelectedCode;

        if (TrySelectTafsili(id, code)) return;
        try
        {   // طرف حساب تازه اضافه شده؛ فهرست دوباره خوانده می‌شود
            _tafsilis = await _tafsiliRepo.GetAllAsync();
            if (IsDisposed) return;
            RebindTafsili();
            TrySelectTafsili(id, code);
        }
        catch (Exception ex) { LogHelper.LogException(ex, "CashTransactionEditForm.BtnSelectTafsili_Click"); }
    }

    private async Task SaveSafeAsync(bool printAfterSave = false)
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (cmbAccount.SelectedValue is null) { ModernMessageBox.ShowWarning("لطفاً حساب را انتخاب کنید."); return; }
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            if (amount <= 0) { ModernMessageBox.ShowWarning("مبلغ نامعتبر است."); return; }
            DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
            if (date is null) { ModernMessageBox.ShowWarning("تاریخ نامعتبر است."); return; }

            string paymentScope;
            int? clientContractId = null;
            int? contractorContractId = null;
            string contractTitle = "";
            string category = rbPayroll.Checked ? "پرسنلی" : rbAdmin.Checked ? "اداری" : "عملیاتی";
            if (_isEdit && !string.IsNullOrWhiteSpace(_loadedCategory) && ScopeKey() == _loadedScopeKey) category = _loadedCategory!;

            if (rbAdmin.Checked) { paymentScope = PaymentScopes.Administrative; contractTitle = "هزینه‌های اداری"; }
            else if (rbProjectExpense.Checked)
            {
                paymentScope = PaymentScopes.ProjectExpense;
                if (cmbClientContract.SelectedItem is not ComboItem prjItem || prjItem.Id <= 0) { ModernMessageBox.ShowWarning("برای هزینه‌های پروژه، انتخاب پروژه (قرارداد کارفرما) الزامی است."); return; }
                clientContractId = prjItem.Id; contractTitle = prjItem.Display;
            }
            else if (rbPayroll.Checked)
            {
                paymentScope = PaymentScopes.Administrative;
                contractTitle = "حقوق و دستمزد پرسنل";
                category = "پرسنلی";
                if (cmbClientContract.SelectedItem is ComboItem payItem && payItem.Id > 0) { clientContractId = payItem.Id; contractTitle = payItem.Display; }
            }
            else if (rbDirectPurchase.Checked || rbInformalContractor.Checked)
            {
                paymentScope = rbDirectPurchase.Checked ? PaymentScopes.DirectPurchase : PaymentScopes.InformalContractor;
                if (cmbClientContract.SelectedItem is not ComboItem clientItem || clientItem.Id <= 0) { ModernMessageBox.ShowWarning("لطفاً قرارداد کارفرما را انتخاب کنید."); return; }
                clientContractId = clientItem.Id; contractTitle = clientItem.Display;
            }
            else
            {
                paymentScope = PaymentScopes.ContractorContract;
                if (cmbContractorContract.SelectedItem is not ComboItem conItem || conItem.Id <= 0) { ModernMessageBox.ShowWarning("لطفاً طرف حساب (پیمانکار) و سپس قرارداد او را انتخاب کنید."); return; }
                contractorContractId = conItem.Id; clientContractId = conItem.ClientContractId; contractTitle = conItem.Display;
            }

            string tafsiliCode = cmbTafsili.SelectedValue?.ToString() ?? "";
            string tafsiliTitle = "";
            if (!string.IsNullOrWhiteSpace(tafsiliCode) && cmbTafsili.SelectedItem is Tafsili selectedTaf) tafsiliTitle = selectedTaf.Name;

            var trans = new CashTransaction
            {
                Id = _transactionId ?? 0,
                AccountId = (int)cmbAccount.SelectedValue,
                AccountTitle = _accounts.FirstOrDefault(a => a.Id == (int)cmbAccount.SelectedValue)?.Title,
                PaymentScope = paymentScope,
                ContractId = clientContractId,
                ContractorContractId = contractorContractId,
                ContractTitle = contractTitle,
                TransactionDate = date.Value,
                Amount = amount,
                TransactionType = "حواله",
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
                if (_isEdit) await _transRepo.UpdateAsync(trans);
                else await _transRepo.AddAsync(trans);
            }, "در حال ذخیره...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("حواله با موفقیت ذخیره شد.", true);

                if (printAfterSave)
                {
                    var account = _accounts.FirstOrDefault(a => a.Id == trans.AccountId);
                    if (account is null) ModernMessageBox.ShowWarning("حساب بانکی این حواله پیدا نشد؛ چاپ دستور پرداخت انجام نشد.");
                    else
                    {
                        using var printForm = new PaymentOrderForm(new List<CashTransaction> { trans }, account);
                        printForm.ShowDialog(this);
                    }
                }

                DialogResult = DialogResult.OK; Close();
            }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "CashTransactionEditForm.SaveSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CashTransactionEditForm"); } }
    }
}
