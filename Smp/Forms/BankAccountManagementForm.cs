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

public class BankAccountManagementForm : Form
{
    private readonly string _connectionString;
    private readonly BankAccountRepository _repo;

    private DataGridView dgvAccounts = null!;
    private ModernTextBox txtSearch = null!;
    private ModernStatCard cardBalance = null!, cardPendingOut = null!, cardPendingIn = null!;
    private Label lblCount = null!;
    private Button btnAdd = null!, btnEdit = null!, btnDelete = null!, btnCheckbook = null!, btnRefresh = null!;

    private List<CompanyBankAccount> _allAccounts = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public BankAccountManagementForm(string connectionString)
    {
        _connectionString = connectionString;
        _repo = new BankAccountRepository(connectionString);

                    BuildUI();
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this);

        Shown += async (s, e) => await LoadDataSafeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "BankAccountManagementForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── کارت‌های خلاصه (مقدار درشت‌تر) ──
        var strip = ModernReport.CreateStatStrip(3, 92);
        cardBalance = strip.Add("جمع موجودی واقعی", ModernTheme.Primary);
        cardPendingOut = strip.Add("جمع چک پرداختی در جریان", ModernTheme.Success);
        cardPendingIn = strip.Add("جمع چک دریافتی در انتظار وصول", ModernTheme.Warning);
        foreach (var c in new[] { cardBalance, cardPendingOut, cardPendingIn }) c.ValueFontSize = 15f;
        strip.Dock = DockStyle.Fill;

        // ── نوار جستجو: کادر جستجو (راست) + شمارنده (چپ) ──
        txtSearch = ModernUI.Input(placeholder: "جستجوی نام حساب، بانک یا شماره حساب...");
        txtSearch.TextBox.TextChanged += (s, e) => ApplyFilter();
        txtSearch.Dock = DockStyle.Fill;
        var searchHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8), BackColor = ModernTheme.Background };
        searchHost.Controls.Add(txtSearch);

        lblCount = ModernUI.Caption("", 10f, false, ModernTheme.TextMuted);
        lblCount.Dock = DockStyle.Fill;
        lblCount.TextAlign = ContentAlignment.MiddleRight;

        var filterBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            RightToLeft = RightToLeft.Yes, BackColor = ModernTheme.Background, Padding = new Padding(6, 0, 6, 0)
        };
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420F));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        filterBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        filterBar.Controls.Add(searchHost, 0, 0);
        filterBar.Controls.Add(lblCount, 1, 0);

        // ── جدول ──
        dgvAccounts = ModernGrid.Create("Id");
        dgvAccounts.AutoGenerateColumns = false;
        dgvAccounts.FillColumns();
        dgvAccounts.UseAiryLook(54);
        dgvAccounts.UseSelectionAccent(ModernTheme.Primary);
        dgvAccounts.SelectionChanged += (s, e) => UpdateButtonsState();
        dgvAccounts.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnEdit_Click(null, EventArgs.Empty); };

        dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "BankTitle", DataPropertyName = "BankTitle", HeaderText = "حساب", FillWeight = 30 });
        dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "AccountSubtitle", DataPropertyName = "AccountSubtitle", HeaderText = "", Visible = false });
        dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "AccountNumber", DataPropertyName = "AccountNumber", HeaderText = "شماره حساب", FillWeight = 18 });
        dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "InitialBalance", DataPropertyName = "InitialBalance", HeaderText = "موجودی اولیه", FillWeight = 14 });
        dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentBalance", DataPropertyName = "CurrentBalance", HeaderText = "مانده واقعی بانک", FillWeight = 16 });
        dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "PendingChecksAmount", DataPropertyName = "PendingChecksAmount", HeaderText = "چک پرداختی در جریان", FillWeight = 15 });
        dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "PendingIncomingChecksAmount", DataPropertyName = "PendingIncomingChecksAmount", HeaderText = "چک دریافتی در انتظار", FillWeight = 15 });
        dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "CheckbookText", DataPropertyName = "CheckbookText", HeaderText = "دسته چک", FillWeight = 10 });
        dgvAccounts.Columns.Add(new DataGridViewTextBoxColumn { Name = "StatusText", DataPropertyName = "StatusText", HeaderText = "وضعیت", FillWeight = 9 });

        dgvAccounts.Columns["BankTitle"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        foreach (var c in new[] { "AccountNumber", "InitialBalance", "CurrentBalance", "PendingChecksAmount", "PendingIncomingChecksAmount", "CheckbookText", "StatusText" })
            dgvAccounts.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvAccounts.Columns["AccountNumber"].DefaultCellStyle.ForeColor = ModernTheme.TextMuted;

        // آواتار + نام بانک + «نوع · شعبه» زیر آن؛ وضعیت به‌صورت قرص رنگی
        dgvAccounts.UsePersonColumn("BankTitle", "AccountSubtitle", "");
        dgvAccounts.UseBadges("StatusText", new Dictionary<string, Color>
        {
            ["فعال"] = ModernTheme.Success,
            ["غیرفعال"] = ModernTheme.Neutral
        });
        dgvAccounts.UseBadges("CheckbookText", new Dictionary<string, Color>
        {
            ["دارد"] = ModernTheme.Warning,
            ["ندارد"] = ModernTheme.Neutral
        });
        dgvAccounts.PinHeaderTexts();
        dgvAccounts.CellFormatting += DgvAccounts_CellFormatting;

        var gridHost = ModernReport.WrapGrid(dgvAccounts);

        // ── دکمه‌های پایین ──
        btnAdd = ModernButton.Success("افزودن حساب", 140);
        btnAdd.Click += BtnAdd_Click;
        btnEdit = ModernButton.Primary("ویرایش", 110);
        btnEdit.Click += BtnEdit_Click;
        btnDelete = ModernButton.Danger("حذف", 100);
        btnDelete.Click += BtnDelete_Click;
        btnCheckbook = ModernButton.Soft("دسته چک‌ها", ModernTheme.Warning, 130);
        btnCheckbook.Click += BtnCheckbook_Click;
        btnRefresh = ModernButton.Outline("بروزرسانی", ModernTheme.Neutral, 120);
        btnRefresh.Click += async (s, e) => await LoadDataSafeAsync();
        var bar = ModernForm.CreateButtonBar(btnAdd, btnEdit, btnDelete, btnCheckbook, btnRefresh);

        // ── چیدمان صفحه ──
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12, 12, 12, 0), BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.Controls.Add(strip, 0, 0);
        root.Controls.Add(filterBar, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        root.Controls.Add(bar, 0, 3);
        Controls.Add(root);

        this.ApplyStandardHeader("مدیریت حساب‌های بانکی", new Size(1150, 700), isDialog: false);
        UpdateButtonsState();
    }

    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                _allAccounts = await _repo.GetBankAccountsOnlyAsync();
                if (IsDisposed) return;

                ApplyFilter();
            }, "در حال بارگذاری حساب‌ها...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "BankAccountManagementForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری:\n" + ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "BankAccountManagementForm"); }
        }
    }

    private void ApplyFilter()
    {
        if (IsDisposed) return;

        string search = StringHelper.NormalizePersianText(txtSearch.Text.Trim());
        var filtered = _allAccounts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(a =>
                StringHelper.NormalizePersianText(a.DisplayTitle ?? "").Contains(search) ||
                StringHelper.NormalizePersianText(a.BankTitle ?? "").Contains(search) ||
                StringHelper.NormalizePersianText(a.AccountNumber ?? "").Contains(search));
        }

        var list = filtered.OrderBy(a => a.BankName).ThenBy(a => a.AccountType).ToList();

        dgvAccounts.DataSource = null;
        dgvAccounts.DataSource = list;

        lblCount.Text = StringHelper.ConvertToPersianDigits(list.Count.ToString()) + " حساب";

        cardBalance.SetAmount(list.Sum(a => a.CurrentBalance), "ریال");
        cardPendingOut.SetAmount(list.Sum(a => a.PendingChecksAmount), "ریال");
        cardPendingIn.SetAmount(list.Sum(a => a.PendingIncomingChecksAmount), "ریال");

        UpdateButtonsState();
    }

    private void DgvAccounts_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        string colName = dgvAccounts.Columns[e.ColumnIndex].Name;

        if (e.Value is decimal dec)
        {
            e.Value = dec == 0 && colName != "CurrentBalance" ? "—" : NumberHelper.FormatThousands(dec, usePersianDigits: true);
            e.FormattingApplied = true;

            if (colName == "CurrentBalance")
            {
                e.CellStyle.Font = ModernTheme.GetFont(11f, FontStyle.Bold);
                e.CellStyle.ForeColor = dec < 0 ? ModernTheme.Danger : dec > 0 ? ModernTheme.Success : ModernTheme.TextMuted;
            }
            else if (dec == 0)
            {
                e.CellStyle.ForeColor = ModernTheme.TextDisabled;
            }
            else if (colName == "PendingChecksAmount")
            {
                e.CellStyle.ForeColor = ModernPaint.Darken(ModernTheme.Warning, 40);
            }
            else if (colName == "PendingIncomingChecksAmount")
            {
                e.CellStyle.ForeColor = ModernPaint.Darken(ModernTheme.Teal, 30);
            }
        }
        else if (e.Value is int i)
        {
            e.Value = NumberHelper.FormatThousands(i, usePersianDigits: true);
            e.FormattingApplied = true;
        }

        // حساب غیرفعال: کم‌رنگ
        if (dgvAccounts.Rows[e.RowIndex].DataBoundItem is CompanyBankAccount acc && !acc.IsActive)
        {
            e.CellStyle.BackColor = ModernTheme.SurfaceMuted;
            if (colName != "CurrentBalance") e.CellStyle.ForeColor = ModernTheme.TextDisabled;
        }
    }

    private void UpdateButtonsState()
    {
        if (IsDisposed) return;
        bool hasSelection = dgvAccounts.CurrentRow is not null;
        btnEdit.Enabled = hasSelection;
        btnDelete.Enabled = hasSelection;
        // دکمه «دسته چک‌ها» فقط برای حسابی که دسته چک دارد فعال است
        btnCheckbook.Enabled = hasSelection &&
                               dgvAccounts.CurrentRow?.DataBoundItem is CompanyBankAccount acc &&
                               acc.HasCheckbook;
    }

    private async void BtnAdd_Click(object? sender, EventArgs e)
    {
        using var form = new BankAccountEditForm(_connectionString);
        if (form.ShowDialog(this) == DialogResult.OK) await LoadDataSafeAsync();
    }

    private async void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (dgvAccounts.CurrentRow?.DataBoundItem is not CompanyBankAccount acc) return;
        using var form = new BankAccountEditForm(_connectionString, acc);
        if (form.ShowDialog(this) == DialogResult.OK) await LoadDataSafeAsync();
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (dgvAccounts.CurrentRow?.DataBoundItem is not CompanyBankAccount acc || IsDisposed) return;

        if (!ModernMessageBox.AskConfirm($"آیا از حذف حساب «{acc.DisplayTitle}» مطمئن هستید؟\n\nتوجه: اگر این حساب دارای تراکنش، چک یا دسته چک باشد، سیستم اجازه حذف نمی‌دهد.", "حذف حساب"))
            return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await _repo.DeleteAsync(acc.Id);
        }, "در حال حذف...");

        if (success && !IsDisposed)
        {
            ToastHelper.Show("حساب بانکی حذف شد.", isSuccess: true);
            await LoadDataSafeAsync();
        }
    }

    private void BtnCheckbook_Click(object? sender, EventArgs e)
    {
        if (dgvAccounts.CurrentRow?.DataBoundItem is not CompanyBankAccount acc || !acc.HasCheckbook) return;
        using var form = new CheckbookManagementForm(_connectionString, acc);
        form.ShowDialog(this);
        _ = LoadDataSafeAsync();
    }
}

// ═══════════════════════════════════════════════════════════════
// کلاس کمکی برای جلوگیری از پریدن نام بانک در ComboBox
// ═══════════════════════════════════════════════════════════════
public class BankComboBoxItem
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}

// ═══════════════════════════════════════════════════════════════
// فرم ثبت و ویرایش حساب بانکی
// ═══════════════════════════════════════════════════════════════
public class BankAccountEditForm : Form
{
    private readonly BankAccountRepository _repo;
    private readonly CompanyBankAccount _account;
    private readonly bool _isEdit;

    private TextBox txtAccountNumber = null!, txtInitialBalance = null!;
    private TextBox txtBranch = null!; // فیلد شعبه
    private ComboBox cmbBank = null!, cmbAccountType = null!;
    private CheckBox chkHasCheckbook = null!, chkIsActive = null!;

    public BankAccountEditForm(string connectionString, CompanyBankAccount? account = null)
    {
        _repo = new BankAccountRepository(connectionString);
        _account = account ?? new CompanyBankAccount();
        _isEdit = account is not null;

        BuildUI();
        this.ShowInTaskbar = false;
        ShortcutHelper.ApplyStandardShortcuts(this);

        if (_isEdit) LoadData();
    }

    private void BuildUI()
    {
        cmbBank = ModernUI.Combo();
        var bankList = new List<BankComboBoxItem> { new BankComboBoxItem { Code = "", Name = "--- انتخاب کنید ---" } };
        foreach (var kvp in BankHelper.BankList.OrderBy(x => x.Value))
        {
            bankList.Add(new BankComboBoxItem { Code = kvp.Key, Name = kvp.Value });
        }

        // ✅ استفاده از Items.AddRange برای جلوگیری از خطای SelectedIndex
        cmbBank.Items.AddRange(bankList.ToArray());
        if (cmbBank.Items.Count > 0) cmbBank.SelectedIndex = 0;

        txtAccountNumber = new TextBox();
        cmbAccountType = ModernUI.Combo();
        cmbAccountType.Items.AddRange(new object[] { "جاری", "کوتاه مدت", "بلند مدت", "سپرده", "قرض الحسنه" });
        cmbAccountType.SelectedIndex = 0;

        txtBranch = new TextBox();
        txtInitialBalance = new TextBox();
        SmartInputHelper.EnableSmartAmountTextBox(txtInitialBalance);

        chkHasCheckbook = ModernUI.Check("این حساب دارای دسته چک است");
        chkIsActive = ModernUI.Check("حساب فعال است");
        chkIsActive.Checked = true;

        // ── کارت فرم ──
        int rh = ModernUI.RowHeight;
        var card = new ModernCard("مشخصات حساب", ModernTheme.Primary, 2, rh, rh, rh, rh);
        card.Body.Controls.Add(ModernUI.Row("نام بانک", cmbBank), 0, 0);
        card.Body.Controls.Add(ModernUI.Row("نوع حساب", cmbAccountType), 1, 0);
        card.Body.Controls.Add(ModernUI.Row("شماره حساب", txtAccountNumber), 0, 1);
        card.Body.Controls.Add(ModernUI.Row("نام شعبه (اختیاری)", txtBranch), 1, 1);
        card.Body.Controls.Add(ModernUI.Row("موجودی اولیه (ریال)", txtInitialBalance), 0, 2);
        card.Body.Controls.Add(ModernUI.CheckRow("دارای دسته چک", chkHasCheckbook), 1, 2);
        card.Body.Controls.Add(ModernUI.CheckRow("حساب فعال", chkIsActive), 1, 3);   // زیر «دارای دسته چک»

        var btnSave = ModernButton.Save("ذخیره", 160, 44);
        btnSave.Click += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(cmbAccountType.Text))
            {
                ModernMessageBox.ShowWarning("نوع حساب را انتخاب کنید.");
                return;
            }

            // ✅ استخراج کد بانک از آیتم انتخاب شده
            var bankItem = cmbBank.SelectedItem as BankComboBoxItem;
            _account.BankCode = bankItem?.Code ?? "";

            // عنوان دیگر از کاربر گرفته نمی‌شود؛ خودکار از «نوع - بانک - شعبه - شماره حساب» ساخته می‌شود
            // (ستون Title برای صندوق/تنخواه و گزارش‌ها لازم است و خالی نمی‌ماند)
            _account.Title = BuildAutoTitle(cmbAccountType.Text, string.IsNullOrEmpty(bankItem?.Code) ? "" : bankItem!.Name,
                                            txtBranch.Text, txtAccountNumber.Text);

            _account.AccountNumber = txtAccountNumber.Text.Trim();
            _account.AccountType = cmbAccountType.Text;
            _account.Branch = txtBranch.Text.Trim();
            _account.InitialBalance = NumberHelper.ParseFormatted(txtInitialBalance.Text);
            _account.HasCheckbook = chkHasCheckbook.Checked;
            _account.IsActive = chkIsActive.Checked;
            _account.IsCashDesk = false;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_isEdit) await _repo.UpdateAsync(_account);
                else await _repo.AddAsync(_account);
            }, "در حال ذخیره...");

            if (success)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        var btnCancel = ModernButton.Cancel("انصراف", 110, 44);
        btnCancel.Click += (s, e) => Close();
        var bar = ModernForm.CreateButtonBar(btnSave, btnCancel);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12, 10, 12, 0), BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, card.Height + 8));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        card.Dock = DockStyle.Fill;
        root.Controls.Add(card, 0, 0);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);

        this.ApplyStandardHeader(_isEdit ? "ویرایش حساب بانکی" : "افزودن حساب بانکی جدید", new Size(860, 440));
    }

    private static string BuildAutoTitle(string? type, string? bank, string? branch, string? accountNumber)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(type)) parts.Add(type.Trim());
        if (!string.IsNullOrWhiteSpace(bank)) parts.Add(bank.Trim());
        if (!string.IsNullOrWhiteSpace(branch)) parts.Add("شعبه " + branch.Trim());
        if (!string.IsNullOrWhiteSpace(accountNumber)) parts.Add(accountNumber.Trim());
        return parts.Count == 0 ? "حساب بانکی" : string.Join(" - ", parts);
    }

    private void LoadData()
    {
        // ✅ پیدا کردن دقیق بانک برای جلوگیری از پریدن
        if (!string.IsNullOrEmpty(_account.BankCode))
        {
            string cleanCode = _account.BankCode.Trim();
            for (int i = 0; i < cmbBank.Items.Count; i++)
            {
                if (cmbBank.Items[i] is BankComboBoxItem item && item.Code == cleanCode)
                {
                    cmbBank.SelectedIndex = i;
                    break;
                }
            }
        }

        txtAccountNumber.Text = _account.AccountNumber;
        if (!string.IsNullOrEmpty(_account.AccountType)) cmbAccountType.SelectedItem = _account.AccountType;
        txtBranch.Text = _account.Branch;

        if (_account.InitialBalance > 0)
            txtInitialBalance.Text = NumberHelper.FormatThousands(_account.InitialBalance, usePersianDigits: true);

        chkHasCheckbook.Checked = _account.HasCheckbook;
        chkIsActive.Checked = _account.IsActive;
    }
}
