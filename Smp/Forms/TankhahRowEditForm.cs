using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class TankhahRowEditForm : Form
{
    private readonly string _connectionString;
    private readonly int _tankhahListId;   // شناسه‌ی لیست تنخواه (TankhahLists.Id)
    private readonly string _tankhahTitle;
    private readonly int? _transactionId;
    private readonly bool _isEdit;

    private readonly CashTransactionRepository _transRepo;
    private readonly TankhahRepository _tankhahRepo;
    private readonly ClientContractRepository _clientContractRepo;
    private readonly ContractorContractRepository _contractorContractRepo;
    private readonly TafsiliRepository _tafsiliRepo;
    private readonly LookupRepository _lookupRepo;

    private RadioButton rbAdmin = null!, rbContractorContract = null!, rbInformalContractor = null!, rbDirectPurchase = null!, rbPayroll = null!, rbProjectExpense = null!;
    private TextBox txtDate = null!, txtDescription = null!;
    private TextBox txtAmount = null!, txtDiscount = null!, txtShipping = null!, txtVat = null!, txtTotal = null!, txtAmountInWords = null!;
    private TextBox txtAutoContractDisplay = null!;
    private ComboBox cmbClientContract = null!, cmbContractorContract = null!, cmbCostType = null!, cmbCategory = null!, cmbTafsili = null!;
    private Button btnSelectTafsili = null!, btnSave = null!;
    private Panel pnlClientContract = null!, pnlContractorContract = null!;
    private ModernPage formLayout = null!;
    private TableLayoutPanel linkBody = null!;
    private int _contentHeight;

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

    public TankhahRowEditForm(string connectionString, int tankhahListId, string tankhahTitle, int? transactionId = null)
    {
        _connectionString = connectionString;
        _tankhahListId = tankhahListId;
        _tankhahTitle = tankhahTitle;
        _transactionId = transactionId;
        _isEdit = transactionId.HasValue;

        _transRepo = new CashTransactionRepository(connectionString);
        _tankhahRepo = new TankhahRepository(connectionString);
        _clientContractRepo = new ClientContractRepository(connectionString);
        _contractorContractRepo = new ContractorContractRepository(connectionString);
        _tafsiliRepo = new TafsiliRepository(connectionString);
        _lookupRepo = new LookupRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader(_isEdit ? "ویرایش ردیف تنخواه" : $"ثبت ردیف جدید در {tankhahTitle}", new Size(880, 900));
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

        Shown += async (s, e) => await LoadDataAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "TankhahRowEditForm"); } }
        base.Dispose(disposing);
    }

    private const int HeaderHeight = 50;
    private const int FooterHeight = 66;
    private const int CardGap = 12;

    /// <summary>ارتفاع فرم به اندازه‌ی محتوا؛ اگر صفحه کوچک بود محتوا اسکرول می‌خورد</summary>
    private void ApplyLayoutHeight()
    {
        if (formLayout is null) return;
        ClientSize = new Size(ClientSize.Width, HeaderHeight + FooterHeight + _contentHeight);
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        int maxHeight = wa.Height - 10;
        if (Height > maxHeight) Height = maxHeight;
    }

    private void BuildUI()
    {
        int FH = ModernUI.FieldHeight;
        Color okColor = ModernPaint.Darken(ModernTheme.Success, 40);

        // ───────────── کارت ۱: تاریخ و مبلغ ─────────────
        var cardMain = new ModernCard("💵 تاریخ و مبلغ", ModernTheme.Primary, 4, FH, FH, FH);

        var dateBox = ModernUI.Input();
        dateBox.TextAlign = HorizontalAlignment.Center;
        txtDate = dateBox.TextBox;
        DateHelper.ApplyDateMask(txtDate);
        txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);

        var tankhahBox = ModernUI.Input();
        tankhahBox.ReadOnly = true;
        tankhahBox.Text = _tankhahTitle;
        tankhahBox.TextAlign = HorizontalAlignment.Center;
        tankhahBox.Font = ModernTheme.GetFont(11f, FontStyle.Bold);

        var amountBox = ModernUI.Money(null);
        amountBox.Font = ModernTheme.GetFont(12f, FontStyle.Bold);
        txtAmount = amountBox.TextBox;
        txtAmount.TextChanged += (s, e) => CalculateTotal();

        var discountBox = ModernUI.Money(null);
        txtDiscount = discountBox.TextBox;
        txtDiscount.TextChanged += (s, e) => CalculateTotal();

        var shippingBox = ModernUI.Money(null);
        txtShipping = shippingBox.TextBox;
        txtShipping.TextChanged += (s, e) => CalculateTotal();

        var vatBox = ModernUI.Money(null);
        txtVat = vatBox.TextBox;
        txtVat.TextChanged += (s, e) => CalculateTotal();

        var totalBox = ModernUI.Input();
        totalBox.ReadOnly = true;
        totalBox.TextAlign = HorizontalAlignment.Center;
        totalBox.Font = ModernTheme.GetFont(12f, FontStyle.Bold);
        txtTotal = totalBox.TextBox;
        txtTotal.ForeColor = okColor;
        txtTotal.TabStop = false;

        var wordsBox = ModernUI.Input();
        wordsBox.ReadOnly = true;
        wordsBox.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
        txtAmountInWords = wordsBox.TextBox;
        txtAmountInWords.ForeColor = okColor;
        txtAmountInWords.TabStop = false;

        cardMain.Body.Controls.Add(ModernUI.Field("تاریخ", dateBox), 0, 0);
        var fldTankhah = ModernUI.Field("تنخواه", tankhahBox);
        cardMain.Body.Controls.Add(fldTankhah, 1, 0);
        cardMain.Body.SetColumnSpan(fldTankhah, 3);
        cardMain.Body.Controls.Add(ModernUI.Field("مبلغ پایه (ریال)", amountBox), 0, 1);
        cardMain.Body.Controls.Add(ModernUI.Field("تخفیفات", discountBox), 1, 1);
        cardMain.Body.Controls.Add(ModernUI.Field("کرایه حمل", shippingBox), 2, 1);
        cardMain.Body.Controls.Add(ModernUI.Field("مالیات ارزش افزوده", vatBox), 3, 1);
        cardMain.Body.Controls.Add(ModernUI.Field("جمع کل (ریال)", totalBox), 0, 2);
        var fldWords = ModernUI.Field("جمع به حروف", wordsBox);
        cardMain.Body.Controls.Add(fldWords, 1, 2);
        cardMain.Body.SetColumnSpan(fldWords, 3);

        // ───────────── کارت ۲: نوع پرداخت ─────────────
        var cardScope = new ModernCard("📌 این هزینه بابت چیست؟ (نوع پرداخت)", ModernTheme.Orange, 3, 46, 46);
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
        var cardLink = new ModernCard("🔗 اتصال و طرف حساب", ModernTheme.Teal, 2, FH, 0, FH, FH);
        linkBody = cardLink.Body;

        cmbClientContract = ModernUI.Combo();
        cmbClientContract.DisplayMember = "Display"; cmbClientContract.ValueMember = "Id";
        var pnlClient = ModernUI.Field("اتصال به پروژه", cmbClientContract, ModernTheme.DangerText);
        pnlClientContract = pnlClient;
        linkBody.Controls.Add(pnlClient, 0, 0);
        linkBody.SetColumnSpan(pnlClient, 2);

        // پیمانکار با قرارداد: قرارداد پیمانکار + قرارداد کارفرمای مربوطه (خودکار)
        cmbContractorContract = ModernUI.Combo();
        cmbContractorContract.DisplayMember = "Display"; cmbContractorContract.ValueMember = "Id";
        var autoBox = ModernUI.Input();
        autoBox.ReadOnly = true;
        autoBox.TextAlign = HorizontalAlignment.Center;
        autoBox.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
        txtAutoContractDisplay = autoBox.TextBox;
        txtAutoContractDisplay.ForeColor = ModernTheme.Primary;
        txtAutoContractDisplay.TabStop = false;

        var pnlCon = new BufferedTable { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Color.Transparent, Visible = false, ColumnCount = 2, RowCount = 1 };
        pnlCon.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        pnlCon.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        pnlCon.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        pnlCon.Controls.Add(ModernUI.Field("قرارداد پیمانکار", cmbContractorContract, ModernTheme.DangerText), 0, 0);
        pnlCon.Controls.Add(ModernUI.Field("قرارداد کارفرما (خودکار)", autoBox, ModernTheme.TextMuted), 1, 0);
        pnlContractorContract = pnlCon;
        linkBody.Controls.Add(pnlCon, 0, 1);
        linkBody.SetColumnSpan(pnlCon, 2);

        cmbCostType = ModernUI.Combo();
        cmbCategory = ModernUI.Combo();
        linkBody.Controls.Add(ModernUI.Field("ماهیت هزینه", cmbCostType), 0, 2);
        linkBody.Controls.Add(ModernUI.Field("دسته‌بندی", cmbCategory), 1, 2);

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
        var fldTafsili = ModernUI.Field("طرف حساب (دریافت‌کننده)", pnlTafsili);
        linkBody.Controls.Add(fldTafsili, 0, 3);
        linkBody.SetColumnSpan(fldTafsili, 2);

        // ───────────── کارت ۴: توضیحات ─────────────
        var cardDesc = new ModernCard("📝 شرح / توضیحات", ModernTheme.Neutral, 1, 92);
        var memo = ModernUI.Memo();
        memo.Dock = DockStyle.Fill;
        memo.Margin = new Padding(6, 2, 6, 2);
        txtDescription = memo.TextBox;
        cardDesc.Body.Controls.Add(memo, 0, 0);

        // ───────────── صفحه + نوار دکمه‌ها ─────────────
        var cards = new Control[] { cardMain, cardScope, cardLink, cardDesc };
        formLayout = new ModernPage();
        foreach (var c in cards) formLayout.AddCard(c, CardGap);
        _contentHeight = cards.Sum(c => c.Height + CardGap) + formLayout.Padding.Vertical;

        var save = ModernButton.Success(_isEdit ? "💾 ذخیره تغییرات" : "💾 ثبت ردیف در تنخواه", 230, 42);
        save.Click += async (s, e) => await SaveSafeAsync();
        btnSave = save;

        var cancel = ModernButton.Cancel("انصراف", 110, 42);
        cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var bar = ModernForm.CreateButtonBar(save, cancel);
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
                if (_projectCostTypes.Count == 0) _projectCostTypes.AddRange(new[] { "خرید کالا", "خرید خدمات", "حمل و نقل", "سایر" });
                else if (!_projectCostTypes.Contains("حمل و نقل")) _projectCostTypes.Add("حمل و نقل");

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
        if (trans is null || IsDisposed) return;

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
            else if (scope == PaymentScopes.ProjectExpense) // ✅ اسکوپ جدید هزینه پروژه
            {
                rbProjectExpense.Checked = true; SelectClientContract(trans.ContractId);
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
        if (IsDisposed || formLayout is null) return;

        bool isContractorContract = rbContractorContract.Checked;

        // ✅ تعیین وضعیت فعال/غیرفعال بودن فیلدها طبق درخواست شما
        // 1. اداری و 6. پروژه -> ماهیت هزینه فعال، قرارداد کارفرما غیرفعال
        // 2، 3، 5 -> قرارداد کارفرما فعال، ماهیت هزینه غیرفعال
        // 4 -> (قرارداد پیمانکار نمایش داده می‌شود و بقیه غیرفعال)

        bool isAdminOrProject = rbAdmin.Checked || rbProjectExpense.Checked;

        // تنظیم ارتفاع ردیف‌ها
        // ردیف ۰: اتصال به پروژه | ردیف ۱: قرارداد پیمانکار (مجموع ارتفاع کارت ثابت می‌ماند)
        linkBody.RowStyles[0].Height = isContractorContract ? 0F : ModernUI.FieldHeight;
        linkBody.RowStyles[1].Height = isContractorContract ? ModernUI.FieldHeight : 0F;

        if (pnlClientContract is not null) pnlClientContract.Visible = !isContractorContract;
        if (pnlContractorContract is not null) pnlContractorContract.Visible = isContractorContract;

        if (!_isInitializing && cmbCostType is not null)
        {
            // فعال یا غیرفعال کردن فیلدها
            cmbCostType.Enabled = isAdminOrProject;
            cmbClientContract.Enabled = !isAdminOrProject && !isContractorContract;

            // بارگذاری لیست کشویی ماهیت هزینه
            if (isAdminOrProject)
            {
                if (rbAdmin.Checked) cmbCostType.DataSource = _adminCostTypes.ToList();
                else cmbCostType.DataSource = _projectCostTypes.ToList();
            }
            else
            {
                // وقتی غیرفعال است، یک مقدار پیش‌فرض منطقی به آن می‌دهیم
                cmbCostType.DataSource = _projectCostTypes.ToList();
                if (rbDirectPurchase.Checked && cmbCostType.Items.Contains("خرید کالا")) cmbCostType.SelectedItem = "خرید کالا";
                else if (rbInformalContractor.Checked && cmbCostType.Items.Contains("خرید خدمات")) cmbCostType.SelectedItem = "خرید خدمات";
                else if (rbPayroll.Checked && cmbCostType.Items.Contains("حقوق و دستمزد")) cmbCostType.SelectedItem = "حقوق و دستمزد";
                else if (rbContractorContract.Checked && cmbCostType.Items.Contains("خدمات پیمانکاری")) cmbCostType.SelectedItem = "خدمات پیمانکاری";
            }

            // اگر قرارداد کارفرما غیرفعال شد، انتخاب آن را پاک کن
            if (!cmbClientContract.Enabled && cmbClientContract.Items.Count > 0)
            {
                cmbClientContract.SelectedIndex = 0;
            }
        }
        else if (cmbCostType is not null && cmbCostType.Items.Count == 0)
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
            txtAutoContractDisplay.Text = clientContract is not null ? $"{clientContract.ContractNumber} - {clientContract.Subject}" : "--- ارتباط ندارد ---";
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
                if (match is not null) this.Invoke(new Action(() => cmbTafsili.SelectedValue = match.Code));
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
            if (date is null) { ModernMessageBox.ShowWarning("تاریخ معتبر نیست."); return; }
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
                // قرارداد کارفرما غیرفعال است، پس نال می‌ماند
            }
            else if (rbProjectExpense.Checked) // ✅ هزینه‌های پروژه
            {
                paymentScope = PaymentScopes.ProjectExpense;
                contractTitle = "هزینه‌های پروژه";
                // قرارداد کارفرما غیرفعال است، پس نال می‌ماند
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
                AccountId = null,   // پول هنگام شارژ از بانک خارج شده؛ ردیف هزینه به حساب بانکی وصل نیست
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
                    await _tankhahRepo.UpdateListExpenseAsync(_tankhahListId, trans);
                }
                else
                {
                    await _tankhahRepo.AddListExpenseAsync(_tankhahListId, trans);
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
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "TankhahRowEditForm"); } }
    }
}
