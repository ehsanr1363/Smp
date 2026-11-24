using ClosedXML.Excel;
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

public class TankhahChargeForm : Form
{
    private readonly string _connectionString;
    private readonly TankhahRepository _tankhahRepo;
    private readonly BankAccountRepository _bankRepo;

    private ComboBox cmbSourceAccount = null!, cmbDestinationList = null!;
    private TextBox txtAmount = null!, txtDate = null!, txtDescription = null!;
    private ModernButton btnSave = null!; private Button btnSavePrint = null!, btnPrintReceipt = null!, btnDownloadTemplate = null!, btnImportExcel = null!;
    private ModernTextBox txtSearch = null!;
    private Label lblCount = null!;
    private Label lblWords = null!;
    private ModernButton btnCancelEdit = null!;
    private TankhahCharge? _editingCharge;   // شارژی که در حال ویرایش است (null = ثبت جدید)
    private ModernStatCard statCount = null!, statTotal = null!, statOwner = null!;
    private DataGridView dgvRecentCharges = null!;

    private List<CompanyBankAccount> _accounts = new();
    private List<Tankhah> _owners = new();
    private List<TankhahList> _tankhahLists = new();
    private List<TankhahCharge> _charges = new();

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public TankhahChargeForm(string connectionString)
    {
        _connectionString = connectionString;
        _tankhahRepo = new TankhahRepository(connectionString);
        _bankRepo = new BankAccountRepository(connectionString);

        BuildUI();
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
        Shown += async (s, e) => await LoadDataAsync();
    }

    private void BuildUI()
    {
        // ═══════════ ۱. کارت ورودی ═══════════
        cmbSourceAccount = ModernUI.Combo();
        cmbSourceAccount.DisplayMember = "DisplayTitle";
        cmbSourceAccount.ValueMember = "Id";
        cmbDestinationList = ModernUI.Combo();
        cmbDestinationList.DisplayMember = "OwnerName";   // فقط تنخواه‌دارها
        cmbDestinationList.ValueMember = "Id";
        cmbDestinationList.SelectedIndexChanged += (s, e) => UpdateStats();

        txtAmount = new TextBox();
        SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
        txtAmount.TextChanged += (s, e) =>
        {
            lblWords.Text = NumberHelper.TryParseFormatted(txtAmount.Text, out var v) && v > 0
                ? "مبلغ به حروف: " + NumberHelper.ToPersianWordsWithUnit(v, "ریال")
                : "مبلغ به حروف: —";
        };

        txtDate = new TextBox { TextAlign = HorizontalAlignment.Center };
        DateHelper.ApplyDateMask(txtDate);
        txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);

        txtDescription = new TextBox { PlaceholderText = "توضیحات اختیاری (مثلاً بابت چه هزینه‌ای)..." };

        lblWords = ModernUI.Caption("مبلغ به حروف: —", 10f, false, ModernTheme.TextMuted);
        lblWords.Dock = DockStyle.Fill;
        lblWords.TextAlign = ContentAlignment.MiddleCenter;

        var cols = new[]
        {
            new ColumnStyle(SizeType.Percent, 50F),
            new ColumnStyle(SizeType.Percent, 50F)
        };
        var card = new ModernCard("اطلاعات شارژ تنخواه (انتقال از بانک به تنخواه‌دار)", ModernTheme.Success, cols, 50, 50, 50, 28);
        card.Body.Controls.Add(ModernUI.Row("از حساب بانکی", cmbSourceAccount), 0, 0);
        card.Body.Controls.Add(ModernUI.Row("به تنخواه‌دار", cmbDestinationList), 1, 0);
        card.Body.Controls.Add(ModernUI.Row("مبلغ (ریال)", txtAmount), 0, 1);
        card.Body.Controls.Add(ModernUI.Row("تاریخ", txtDate), 1, 1);
        var descRow = ModernUI.Row("توضیحات", txtDescription);
        card.Body.Controls.Add(descRow, 0, 2);
        card.Body.SetColumnSpan(descRow, 2);
        card.Body.Controls.Add(lblWords, 0, 3);
        card.Body.SetColumnSpan(lblWords, 2);

        // ═══════════ ۲. کارت‌های خلاصه ═══════════
        var strip = ModernReport.CreateStatStrip(3, 76);
        statCount = strip.Add("تعداد شارژهای ثبت‌شده", ModernTheme.Primary);
        statTotal = strip.Add("جمع مبلغ شارژها", ModernTheme.Success);
        statOwner = strip.Add("جمع شارژ تنخواه‌دار انتخاب‌شده", ModernTheme.Warning);
        statCount.SetCount(0, "شارژ");
        statTotal.SetAmount(0, "ریال");
        statOwner.SetAmount(0, "ریال");
        strip.Dock = DockStyle.Fill;

        // ═══════════ ۳. نوار جستجو ═══════════
        txtSearch = ModernUI.Input(placeholder: "جستجو در شارژها (تنخواه‌دار، بانک، توضیحات، تاریخ، مبلغ)...");
        txtSearch.TextChanged += (s, e) => RenderCharges();

        lblCount = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ModernTheme.TextMuted,
            Font = ModernTheme.GetFont(10f),
            BackColor = Color.Transparent
        };
        var toolbar = ModernListKit.BuildToolbar((txtSearch, 460), (lblCount, 220));

        btnDownloadTemplate = ModernButton.Soft("قالب اکسل", ModernTheme.Primary, 120, 40);
        btnDownloadTemplate.Click += BtnDownloadTemplate_Click;
        btnImportExcel = ModernButton.Soft("ایمپورت از اکسل", ModernTheme.Secondary, 150, 40);
        btnImportExcel.Click += async (s, e) => await BtnImportExcel_Click(s, e);

        // ═══════════ ۴. جدول ═══════════
        dgvRecentCharges = ModernGrid.Create();
        dgvRecentCharges.AllowUserToAddRows = false;
        dgvRecentCharges.AllowUserToDeleteRows = false;
        dgvRecentCharges.ReadOnly = true;
        dgvRecentCharges.AutoGenerateColumns = false;
        dgvRecentCharges.MultiSelect = true;
        dgvRecentCharges.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvRecentCharges.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        dgvRecentCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "تاریخ", FillWeight = 12 });
        dgvRecentCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bank", HeaderText = "از بانک", FillWeight = 27 });
        dgvRecentCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "List", HeaderText = "به تنخواه‌دار / لیست", FillWeight = 29 });
        dgvRecentCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 15 });
        dgvRecentCharges.Columns.Add(new DataGridViewTextBoxColumn { Name = "Desc", HeaderText = "توضیحات", FillWeight = 30 });

        dgvRecentCharges.UseAiryLook(56);
        dgvRecentCharges.UseSelectionAccent();
        dgvRecentCharges.UseTextAlignment("Bank", "List", "Desc");
        foreach (var wrapCol in new[] { "Bank", "List", "Desc" })
        {
            dgvRecentCharges.Columns[wrapCol].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvRecentCharges.Columns[wrapCol].DefaultCellStyle.Padding = new Padding(2, 4, 2, 4);
        }
        dgvRecentCharges.ShowCellToolTips = true;

        dgvRecentCharges.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvRecentCharges.Columns[e.ColumnIndex].Name == "Amount" && e.Value is decimal dec)
            {
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.FormattingApplied = true;
            }
        };
        // دابل‌کلیک روی هر شارژ → ویرایش همان شارژ (چاپ: دکمه‌ی «چاپ دستور پرداخت ردیف‌های انتخاب‌شده»)
        dgvRecentCharges.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            if (dgvRecentCharges.Rows[e.RowIndex].Tag is TankhahCharge ch) BeginEdit(ch);
        };

        // ═══════════ ۵. دکمه‌های پایین ═══════════
        btnSave = ModernButton.Save("ثبت شارژ تنخواه", 170, 40);
        btnSave.Click += async (s, e) => await SaveSafeAsync(printAfter: false);

        btnSavePrint = ModernButton.Success("ثبت و چاپ دستور پرداخت", 230, 40);
        btnSavePrint.Click += async (s, e) => await SaveSafeAsync(printAfter: true);

        btnPrintReceipt = ModernButton.Soft("چاپ دستور پرداخت ردیف‌های انتخاب‌شده", ModernTheme.Primary, 300, 40);
        btnPrintReceipt.Click += (s, e) => PrintSelectedReceipts();

        btnCancelEdit = ModernButton.Cancel("انصراف از ویرایش", 160, 40);
        btnCancelEdit.Visible = false;
        btnCancelEdit.Click += (s, e) => ExitEditMode();

        // دکمه‌های اصلی از راست، اکسل در انتهای چپ (استاندارد ModernListKit)
        var actions = ModernListKit.BuildActionBar(
            new Control[] { btnSave, btnSavePrint, btnPrintReceipt, btnCancelEdit },
            new Control[] { btnDownloadTemplate, btnImportExcel });

        // ═══════════ چیدمان صفحه ═══════════
        ModernListKit.ComposeRows(this, "شارژ تنخواه (انتقال از بانک به تنخواه)",
            "انتقال وجه از حساب بانکی به تنخواه‌دار و چاپ دستور پرداخت", new Size(1200, 860),
            (strip, 88), (card, card.Height + 12), (toolbar, 56), (ModernReport.WrapGrid(dgvRecentCharges), 0), (actions, 64));
    }

    private async Task LoadDataAsync()
    {
        try
        {
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                _accounts = await _bankRepo.GetBankAccountsOnlyAsync();
                _owners = await _tankhahRepo.GetAllTankhahsAsync();
                _tankhahLists = await _tankhahRepo.GetAllListsAsync();

                cmbSourceAccount.DataSource = _accounts;
                cmbDestinationList.DataSource = _owners;   // فقط تنخواه‌دارها؛ شارژ در جدیدترین لیست همان تنخواه‌دار ثبت می‌شود

                if (cmbSourceAccount.Items.Count > 0) cmbSourceAccount.SelectedIndex = 0;
                if (cmbDestinationList.Items.Count > 0) cmbDestinationList.SelectedIndex = 0;

                await LoadRecentChargesAsync();
            }, "در حال بارگذاری اطلاعات...");
        }
        catch (Exception ex) { if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
    }

    private async Task LoadRecentChargesAsync()
    {
        // همه‌ی شارژها (لیست‌های جدید + شارژهای قدیمی/ایمپورت‌شده بدون لیست)؛ جدیدترین بالا
        _charges = (await _tankhahRepo.GetAllChargesEverywhereAsync())
            .OrderByDescending(c => c.Date).ThenByDescending(c => c.Id)
            .Take(300).ToList();
        RenderCharges();
    }

    private static string SearchKey(string? t)
        => StringHelper.ConvertToEnglishDigits(StringHelper.NormalizePersianText(t ?? "")).Replace(" ", "");

    private static string OwnerOf(TankhahCharge c)
        => string.IsNullOrWhiteSpace(c.OwnerName) ? c.TankhahAccountTitle : c.OwnerName;

    /// <summary>
    /// متن ستون «به تنخواه‌دار / لیست». در شارژهای قدیمی ثبت‌شده با تراکنش نقدی، «عنوان حساب» همان بانک مبدأ است؛
    /// پس نباید به‌عنوان تنخواه‌دار یا دوباره داخل عنوان لیست نمایش داده شود.
    /// </summary>
    private static string DestinationOf(TankhahCharge c)
    {
        string bank = (c.BankTitle ?? "").Trim();
        string acct = (c.TankhahAccountTitle ?? "").Trim();
        string owner = OwnerOf(c)?.Trim() ?? "";
        string list = (c.ListTitle ?? "").Trim();

        bool acctIsBank = bank.Length > 0 && bank != "---" && acct == bank;
        if (acctIsBank)
        {
            if (string.IsNullOrWhiteSpace(c.OwnerName)) owner = "";
            string suffix = " — " + bank;
            if (list.EndsWith(suffix, StringComparison.Ordinal))
                list = list.Substring(0, list.Length - suffix.Length).Trim();
        }

        if (owner.Length == 0) return list;
        if (list.Length == 0) return owner;
        return owner + " - " + list;
    }

    private void RenderCharges()
    {
        if (IsDisposed || dgvRecentCharges is null) return;

        string q = SearchKey(txtSearch?.Text);
        var shown = _charges.Where(c => q.Length == 0 || SearchKey(
            $"{OwnerOf(c)} {c.ListTitle} {c.BankTitle} {c.Description} {DateHelper.ToPersianDate(c.Date)} {c.Amount:0}").Contains(q)).ToList();

        dgvRecentCharges.Rows.Clear();
        foreach (var c in shown)
        {
            int i = dgvRecentCharges.Rows.Add(
                DateHelper.ToPersianDate(c.Date),
                c.BankTitle,
                DestinationOf(c),
                c.Amount,
                string.IsNullOrWhiteSpace(c.Description) ? "---" : c.Description);
            dgvRecentCharges.Rows[i].Tag = c;
        }
        lblCount.Text = shown.Count == 0
            ? (q.Length > 0 ? "موردی پیدا نشد." : "شارژی ثبت نشده است.")
            : $"{NumberHelper.FormatThousands(shown.Count, usePersianDigits: true)} شارژ";
        UpdateStats();
    }

    private void UpdateStats()
    {
        if (IsDisposed || statCount is null) return;
        statCount.SetCount(_charges.Count, "شارژ");
        statTotal.SetAmount(_charges.Sum(c => c.Amount), "ریال");

        string? owner = (cmbDestinationList?.SelectedItem as Tankhah)?.OwnerName;
        decimal ownerTotal = string.IsNullOrEmpty(owner) ? 0 : _charges.Where(c => OwnerOf(c) == owner).Sum(c => c.Amount);
        statOwner.SetAmount(ownerTotal, "ریال");
    }

    // ───────────────────────── چاپ دستور پرداخت خزانه‌داری ─────────────────────────
    private void PrintSelectedReceipts()
    {
        var selected = dgvRecentCharges.SelectedRows.Cast<DataGridViewRow>()
            .OrderBy(r => r.Index)
            .Select(r => r.Tag as TankhahCharge)
            .Where(c => c is not null).Select(c => c!).ToList();

        if (selected.Count == 0)
        {
            ModernMessageBox.ShowWarning("یک یا چند ردیف شارژ را از جدول انتخاب کنید (یا روی ردیف دابل‌کلیک کنید).");
            return;
        }
        PrintReceipts(selected);
    }

    /// <summary>حساب بانکی مبدأ یک شارژ را از روی عنوان بانک پیدا می‌کند</summary>
    private CompanyBankAccount FindAccountOf(TankhahCharge c)
    {
        string bt = (c.BankTitle ?? "").Replace("🏦", "").Trim();
        var acc = _accounts.FirstOrDefault(a =>
                   a.AutoTitle == bt || a.DisplayTitle.Replace("🏦", "").Trim() == bt || (a.Title ?? "") == bt)
               ?? _accounts.FirstOrDefault(a => bt.Length > 0 && (bt.Contains(a.AutoTitle) || a.AutoTitle.Contains(bt)));
        return acc ?? new CompanyBankAccount { Title = bt, BankName = bt };
    }

    /// <summary>
    /// «فرم دستور پرداخت خزانه‌داری» (همان فرم چک‌ها و تراکنش‌ها) برای شارژهای انتخاب‌شده.
    /// شارژهایی که از یک حساب بانکی‌اند در یک دستور پرداخت می‌آیند (هر حساب یک دستور جدا).
    /// </summary>
    private void PrintReceipts(IEnumerable<TankhahCharge> charges)
    {
        var list = charges.ToList();
        if (list.Count == 0) return;

        try
        {
            var groups = list.Select(c => (Charge: c, Account: FindAccountOf(c)))
                             .GroupBy(x => x.Account.Id != 0 ? "id:" + x.Account.Id : "t:" + x.Account.Title)
                             .ToList();

            foreach (var g in groups)
            {
                var account = g.First().Account;
                var items = g.Select(x => new PaymentOrderItem
                {
                    Description = string.IsNullOrWhiteSpace(x.Charge.Description)
                        ? $"شارژ تنخواه {OwnerOf(x.Charge)}" + (string.IsNullOrWhiteSpace(x.Charge.ListTitle) ? "" : $" ({x.Charge.ListTitle})")
                        : x.Charge.Description,
                    ReferenceNumber = "---",
                    Date = x.Charge.Date,
                    Amount = x.Charge.Amount,
                    RecipientName = OwnerOf(x.Charge)
                }).ToList();

                using var form = new PaymentOrderForm(items, account);
                form.ShowDialog(this);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahChargeForm.PrintReceipts");
            ModernMessageBox.ShowError("خطا در چاپ دستور پرداخت:\n" + ex.Message);
        }
    }

    /// <summary>جدیدترین لیست یک تنخواه‌دار (بیشترین شماره‌ی لیست؛ لیست‌های فعال در اولویت)</summary>
    private TankhahList? GetLatestListOf(int ownerId)
    {
        var lists = _tankhahLists.Where(l => l.TankhahId == ownerId).ToList();
        var active = lists.Where(l => l.IsActive).ToList();
        if (active.Count > 0) lists = active;
        return lists
            .OrderByDescending(l => TankhahRepository.ParseListNumber(l.Title) ?? int.MinValue)
            .ThenByDescending(l => l.Id)
            .FirstOrDefault();
    }

    // ───────────────────────── ویرایش شارژ با دابل‌کلیک ─────────────────────────
    private const int CashTransactionChargeIdLimit = 1_000_000_000;

    /// <summary>فقط شارژهای ثبت‌شده با همین فرم (ردیف CashTransactions با لیست) قابل ویرایش‌اند، نه ردیف‌های «قدیمی»</summary>
    private static bool IsEditable(TankhahCharge c) => c.Id > 0 && c.Id < CashTransactionChargeIdLimit && c.ListId > 0;

    private void BeginEdit(TankhahCharge ch)
    {
        if (!IsEditable(ch))
        {
            ModernMessageBox.ShowInfo("این ردیف از شارژهای «قدیمی» است و از اینجا قابل ویرایش نیست.\nاز همان صفحه‌ای که ثبت شده (انتقال وجه یا تراکنش نقدی) ویرایشش کنید.");
            return;
        }

        var acc = FindAccountOf(ch);
        if (acc.Id <= 0)
        {
            ModernMessageBox.ShowWarning("حساب بانکی این شارژ در فهرست حساب‌های فعال پیدا نشد؛ ویرایش ممکن نیست.");
            return;
        }

        var list = _tankhahLists.FirstOrDefault(l => l.Id == ch.ListId);
        if (list is null)
        {
            ModernMessageBox.ShowWarning("لیست تنخواه این شارژ پیدا نشد.");
            return;
        }

        _editingCharge = ch;
        cmbSourceAccount.SelectedValue = acc.Id;
        cmbDestinationList.SelectedValue = list.TankhahId;
        cmbDestinationList.Enabled = false;   // لیست ثابت می‌ماند

        txtAmount.Text = NumberHelper.FormatThousands(ch.Amount, usePersianDigits: true);
        txtDate.Text = DateHelper.ToPersianDate(ch.Date);

        string desc = ch.Description ?? "";
        string prefix = $"شارژ تنخواه {ch.ListTitle} - ";
        if (desc.StartsWith(prefix, StringComparison.Ordinal)) desc = desc.Substring(prefix.Length);
        txtDescription.Text = desc.Trim();

        btnSave.Text = "ذخیره تغییرات";
        btnSave.Accent = ModernTheme.Primary;
        btnSavePrint.Text = "ذخیره و چاپ دستور پرداخت";
        btnCancelEdit.Visible = true;
        txtAmount.Focus();
        ToastHelper.Show("در حال ویرایش شارژ؛ پس از اصلاح، «ذخیره تغییرات» را بزنید.", true);
    }

    private void ExitEditMode()
    {
        _editingCharge = null;
        cmbDestinationList.Enabled = true;
        txtAmount.Clear();
        txtDescription.Clear();
        txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);
        btnSave.Text = "ثبت شارژ تنخواه";
        btnSave.Accent = ModernTheme.Brand;
        btnSavePrint.Text = "ثبت و چاپ دستور پرداخت";
        btnCancelEdit.Visible = false;
    }

    private async Task SaveSafeAsync(bool printAfter = false)
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (cmbSourceAccount.SelectedValue is null || cmbDestinationList.SelectedValue is null)
            {
                ModernMessageBox.ShowWarning("لطفاً حساب بانکی و تنخواه‌دار را انتخاب کنید.");
                return;
            }

            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            if (amount <= 0) { ModernMessageBox.ShowWarning("مبلغ باید بزرگتر از صفر باشد."); return; }

            DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
            if (date is null) { ModernMessageBox.ShowWarning("تاریخ معتبر نیست."); return; }

            int bankId = (int)cmbSourceAccount.SelectedValue;
            int ownerId = (int)cmbDestinationList.SelectedValue;

            var editing = _editingCharge;
            var targetList = editing is not null
                ? _tankhahLists.FirstOrDefault(l => l.Id == editing.ListId)
                : GetLatestListOf(ownerId);
            if (targetList is null)
            {
                ModernMessageBox.ShowWarning("برای این تنخواه‌دار هنوز هیچ لیستی ساخته نشده است. ابتدا در صفحه‌ی تنخواه یک لیست بسازید.");
                return;
            }
            int listId = targetList.Id;
            string bankTitle = (cmbSourceAccount.SelectedItem as CompanyBankAccount)?.DisplayTitle ?? cmbSourceAccount.Text;
            string ownerName = (cmbDestinationList.SelectedItem as Tankhah)?.OwnerName ?? cmbDestinationList.Text;
            string desc = txtDescription.Text.Trim();

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (editing is not null)
                    await _tankhahRepo.UpdateChargeAsync(editing.Id, bankId, amount, date.Value, txtDescription.Text.Trim());
                else
                    await _tankhahRepo.ChargeTankhahAsync(bankId, listId, amount, date.Value, txtDescription.Text.Trim());
            }, editing is not null ? "در حال ذخیره تغییرات..." : "در حال ثبت انتقال...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show(editing is not null
                    ? $"شارژ «{targetList.Title}» ویرایش شد."
                    : $"شارژ تنخواه در «{targetList.Title}» ثبت شد.", true);
                if (editing is not null) ExitEditMode();
                txtAmount.Clear();
                txtDescription.Clear();
                await LoadRecentChargesAsync();

                if (printAfter && !IsDisposed)
                {
                    // همان شارژ تازه‌ثبت‌شده را پیدا می‌کنیم؛ اگر در ۳۰۰ ردیف آخر نبود، از اطلاعات فرم رسید می‌سازیم
                    var charge = (editing is not null ? _charges.FirstOrDefault(c => c.Id == editing.Id) : null)
                        ?? _charges
                        .Where(c => c.ListId == listId && c.Amount == amount && c.Date.Date == date.Value.Date)
                        .OrderByDescending(c => c.Id).FirstOrDefault()
                        ?? new TankhahCharge
                        {
                            ListId = listId, ListTitle = targetList.Title, OwnerName = ownerName,
                            Date = date.Value, Amount = amount, BankTitle = bankTitle, Description = desc
                        };
                    PrintReceipts(new[] { charge });
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahChargeForm.SaveSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ثبت:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "TankhahChargeForm"); }
        }
    }

    private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = "Tankhah_Charge_Template.xlsx", Title = "ذخیره قالب اکسل شارژ تنخواه" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("شارژ تنخواه");
            ws.RightToLeft = true;

            string[] headers = { "تاریخ (میلادی یا شمسی)", "حساب بانکی (مبدا)", "لیست تنخواه (مقصد)", "مبلغ (ریال)", "توضیحات" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            if (_accounts.Count > 0 && _tankhahLists.Count > 0)
            {
                ws.Cell(2, 1).Value = DateHelper.ToPersianDate(DateTime.Now);
                ws.Cell(2, 2).Value = _accounts[0].Title;
                ws.Cell(2, 3).Value = _tankhahLists[0].Title;
                ws.Cell(2, 4).Value = 10000000;
                ws.Cell(2, 5).Value = "شارژ اولیه لیست";
            }

            ws.Columns().AdjustToContents();
            ws.Row(1).Height = 35;
            workbook.SaveAs(sfd.FileName);
            ToastHelper.Show("قالب اکسل ساخته شد.", true);
        }
        catch (Exception ex) { ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
    }

    private async Task BtnImportExcel_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل شارژ تنخواه" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        if (!await _actionLock.WaitAsync(0)) return;
        btnImportExcel.Enabled = false;

        try
        {
            int successCount = 0;
            List<string> errors = new();

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                using var workbook = new XLWorkbook(ofd.FileName);
                var ws = workbook.Worksheet(1);
                var rows = ws.RowsUsed().Skip(1);

                int rowNum = 1;
                foreach (var row in rows)
                {
                    rowNum++;
                    try
                    {
                        string dateStr = row.Cell(1).GetFormattedString().Trim();
                        string bankStr = row.Cell(2).GetFormattedString().Trim();
                        string listStr = row.Cell(3).GetFormattedString().Trim();
                        string amountStr = row.Cell(4).GetFormattedString().Trim();
                        string descStr = row.Cell(5).GetFormattedString().Trim();

                        if (string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(amountStr)) continue;

                        // ✅ اصلاح ۱: پذیرش تاریخ شمسی، میلادی و فرمت Date اکسل
                        DateTime? date = DateHelper.ParsePersianDate(dateStr);
                        if (date is null)
                        {
                            if (row.Cell(1).DataType == XLDataType.DateTime) date = row.Cell(1).GetDateTime();
                            else if (DateTime.TryParse(dateStr, out DateTime gDate)) date = gDate;
                        }
                        if (date is null) throw new Exception($"تاریخ «{dateStr}» معتبر نیست.");

                        decimal amount = NumberHelper.ParseFormatted(amountStr);
                        if (amount <= 0) throw new Exception("مبلغ نامعتبر است.");

                        // ✅ اصلاح ۲: تطبیق هوشمند نام بانک
                        var bank = _accounts.FirstOrDefault(a =>
                        {
                            string cleanDb = StringHelper.NormalizePersianText(a.Title).Replace(" ", "").Replace("-", "");
                            string cleanExcel = StringHelper.NormalizePersianText(bankStr).Replace(" ", "").Replace("-", "");
                            return cleanDb.Contains(cleanExcel) || cleanExcel.Contains(cleanDb);
                        });
                        if (bank is null) throw new Exception($"حساب بانکی «{bankStr}» یافت نشد.");

                        // ✅ اصلاح ۳: تطبیق هوشمند نام لیست تنخواه (با نادیده گرفتن آقای/خانم/تنخواه/لیست)
                        var tankhahList = _tankhahLists.FirstOrDefault(l =>
                        {
                            string dbTitle = StringHelper.NormalizePersianText(l.Title).Replace(" ", "").Replace("-", "");
                            string excelTitle = StringHelper.NormalizePersianText(listStr).Replace(" ", "").Replace("-", "");

                            if (dbTitle == excelTitle) return true;

                            string[] wordsToRemove = { "آقای", "خانم", "تنخواه", "لیست", "شماره" };
                            foreach (var w in wordsToRemove)
                            {
                                dbTitle = dbTitle.Replace(w, "");
                                excelTitle = excelTitle.Replace(w, "");
                            }

                            // مرتب‌سازی حروف برای اینکه "ظریف1" با "1ظریف" برابر شود
                            var sortedDb = new string(dbTitle.OrderBy(c => c).ToArray());
                            var sortedExcel = new string(excelTitle.OrderBy(c => c).ToArray());

                            return sortedDb == sortedExcel;
                        });

                        if (tankhahList is null) throw new Exception($"لیست تنخواه «{listStr}» یافت نشد.");

                        await _tankhahRepo.ChargeTankhahAsync(bank.Id, tankhahList.Id, amount, date.Value, string.IsNullOrWhiteSpace(descStr) ? "ایمپورت از اکسل" : descStr);
                        successCount++;
                    }
                    catch (Exception ex) { errors.Add($"ردیف {rowNum}: {ex.Message}"); }
                }
            }, "در حال ایمپورت از اکسل...");

            if (success && !IsDisposed)
            {
                string msg = $"✅ تعداد {successCount} شارژ با موفقیت ثبت شد.";
                if (errors.Count > 0)
                {
                    msg += $"\n\n⚠️ {errors.Count} خطا:\n" + string.Join("\n", errors.Take(5));
                    ModernMessageBox.ShowWarning(msg, "نتیجه ایمپورت");
                }
                else ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");

                await LoadRecentChargesAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahChargeForm.BtnImportExcel_Click");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ایمپورت:\n" + ex.Message);
        }
        finally
        {
            btnImportExcel.Enabled = true;
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "TankhahChargeForm"); }
        }
    }
}
