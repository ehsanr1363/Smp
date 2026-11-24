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

/// <summary>
/// مدیریت دسته چک‌های یک حساب بانکی — طراحی مدرن:
/// کارت‌های خلاصه ← کارت ثبت دسته چک (با پیش‌نمایش زندهٔ محدوده‌ها) ← جدول دسته چک‌ها ← نوار دکمه‌ها
/// </summary>
public class CheckbookManagementForm : Form
{
    private readonly string _connectionString;
    private readonly CompanyBankAccount _account;
    private readonly CheckbookRepository _repo;

    private ModernTextBox txtSerial = null!, txtStartSayad = null!, txtStartCheck = null!;
    private NumericUpDown numPageCount = null!;
    private Label lblPreview = null!;
    private DataGridView dgvCheckbooks = null!;
    private ModernStatCard cardCount = null!, cardPages = null!, cardLast = null!;
    private Button btnAdd = null!, btnDelete = null!;

    private List<Checkbook> _current = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public CheckbookManagementForm(string connectionString, CompanyBankAccount account)
    {
        _connectionString = connectionString;
        _account = account;
        _repo = new CheckbookRepository(connectionString);

        BuildUI();
        ShowInTaskbar = false;
        ShortcutHelper.ApplyStandardShortcuts(this);

        Shown += async (s, e) =>
        {
            txtSerial.Focus();
            await LoadDataSafeAsync();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CheckbookManagementForm"); }
            try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CheckbookManagementForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── کارت‌های خلاصه ──
        var strip = ModernReport.CreateStatStrip(3, 84);
        cardCount = strip.Add("تعداد دسته چک‌ها", ModernTheme.Primary);
        cardPages = strip.Add("جمع کل برگ‌ها", ModernTheme.Success);
        cardLast = strip.Add("آخرین دسته چک ثبت‌شده", ModernTheme.Warning);
        foreach (var c in new[] { cardCount, cardPages, cardLast }) c.ValueFontSize = 15f;
        strip.Dock = DockStyle.Fill;
        cardCount.SetCount(0);
        cardPages.SetCount(0, "برگ");
        cardLast.Text = "—";

        // ── فیلدهای ثبت ──
        txtSerial = ModernUI.Input(placeholder: "مثلاً ۹۹۲۶۶");
        txtStartSayad = ModernUI.Input(ModernInputKind.Digits, "۱۲ رقم، مثلاً 050038890221");
        txtStartSayad.MaxLength = 12;
        txtStartCheck = ModernUI.Input(ModernInputKind.Digits, "مثلاً 95901");

        numPageCount = new NumericUpDown
        {
            Minimum = 1, Maximum = 500, Value = 100,
            TextAlign = HorizontalAlignment.Center,
            Font = ModernTheme.GetFont(11f)
        };

        txtStartCheck.TextBox.TextChanged += (s, e) => UpdatePreview();
        txtStartSayad.TextBox.TextChanged += (s, e) => UpdatePreview();
        numPageCount.ValueChanged += (s, e) => UpdatePreview();

        // پیش‌نمایش زنده‌ی محدوده‌ها (نوار ملایم رنگی)
        lblPreview = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,   // فرم Mirror: متن سمت راست
            Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
            ForeColor = ModernPaint.Darken(ModernTheme.Primary, 30),
            BackColor = ModernPaint.Tint(ModernTheme.Primary, 8),
            Padding = new Padding(0, 0, 14, 0),
            Margin = new Padding(8, 6, 8, 2)
        };

        int rh = ModernUI.RowHeight;
        var card = new ModernCard("ثبت دسته چک جدید", ModernTheme.Success, 2, rh, rh, 46);
        card.Body.Controls.Add(ModernUI.Row("سریال دسته چک", txtSerial), 0, 0);
        card.Body.Controls.Add(ModernUI.Row("صیاد چک اول", txtStartSayad), 1, 0);
        card.Body.Controls.Add(ModernUI.Row("شماره چک اول", txtStartCheck), 0, 1);
        card.Body.Controls.Add(ModernUI.Row("تعداد برگ", numPageCount), 1, 1);
        card.Body.Controls.Add(lblPreview, 0, 2);
        card.Body.SetColumnSpan(lblPreview, 2);
        card.Dock = DockStyle.Fill;
        UpdatePreview();

        // ── جدول ──
        dgvCheckbooks = ModernGrid.Create("Id");
        dgvCheckbooks.AutoGenerateColumns = false;
        dgvCheckbooks.FillColumns();
        dgvCheckbooks.UseAiryLook(50);
        dgvCheckbooks.UseSelectionAccent(ModernTheme.Warning);

        dgvCheckbooks.Columns.Add(new DataGridViewTextBoxColumn { Name = "SerialNumber", DataPropertyName = "SerialNumber", HeaderText = "سریال دسته چک", FillWeight = 20 });
        dgvCheckbooks.Columns.Add(new DataGridViewTextBoxColumn { Name = "SayadRangeDisplay", DataPropertyName = "SayadRangeDisplay", HeaderText = "محدوده صیاد", FillWeight = 34 });
        dgvCheckbooks.Columns.Add(new DataGridViewTextBoxColumn { Name = "RangeDisplay", DataPropertyName = "RangeDisplay", HeaderText = "محدوده شماره چک‌ها", FillWeight = 28 });
        dgvCheckbooks.Columns.Add(new DataGridViewTextBoxColumn { Name = "PageCount", DataPropertyName = "PageCount", HeaderText = "تعداد برگ", FillWeight = 12 });
        foreach (DataGridViewColumn c in dgvCheckbooks.Columns)
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvCheckbooks.Columns["SerialNumber"].DefaultCellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);

        dgvCheckbooks.UseBadges("PageCount", new Dictionary<string, Color>(), ModernTheme.Primary);
        dgvCheckbooks.PinHeaderTexts();
        dgvCheckbooks.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvCheckbooks.Columns[e.ColumnIndex].Name == "PageCount" && e.Value is int n)
            {
                e.Value = StringHelper.ConvertToPersianDigits(n.ToString());
                e.FormattingApplied = true;
            }
        };
        dgvCheckbooks.SelectionChanged += (s, e) => UpdateButtons();
        var gridHost = ModernReport.WrapGrid(dgvCheckbooks);

        // ── دکمه‌ها ──
        btnAdd = ModernButton.Success("ثبت دسته چک", 160, 42);
        btnAdd.Click += async (s, e) => await SaveSafeAsync();
        btnDelete = ModernButton.Soft("حذف انتخاب‌شده", ModernTheme.Danger, 150, 42);
        btnDelete.Click += async (s, e) => await DeleteSafeAsync();
        var btnClose = ModernButton.Cancel("بستن", 110, 42);
        btnClose.Click += (s, e) => Close();
        var bar = ModernForm.CreateButtonBar(btnAdd, btnDelete, btnClose);

        // ── چیدمان صفحه ──
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12, 12, 12, 0), BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, card.Height + 10F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.Controls.Add(strip, 0, 0);
        root.Controls.Add(card, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        root.Controls.Add(bar, 0, 3);
        Controls.Add(root);

        this.ApplyStandardHeader($"مدیریت دسته چک‌ها - {_account.AutoTitle}", new Size(980, 760));
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        if (IsDisposed || btnDelete is null) return;
        btnDelete.Enabled = dgvCheckbooks.CurrentRow?.DataBoundItem is Checkbook;
    }

    private void UpdatePreview()
    {
        if (lblPreview is null) return;
        if (long.TryParse(txtStartCheck.Text.Trim(), out long start))
        {
            long end = start + (long)numPageCount.Value - 1;
            string text = $"شماره چک: از {start} تا {end}";
            if (long.TryParse(txtStartSayad.Text.Trim(), out long sayad))
                text += $"      |      صیاد: از {sayad:D12} تا {(sayad + (long)numPageCount.Value - 1):D12}";
            lblPreview.Text = StringHelper.ConvertToPersianDigits(text);
        }
        else
        {
            lblPreview.Text = "با وارد کردن شماره چک اول و تعداد برگ، محدوده‌ها اینجا نمایش داده می‌شود";
        }
    }

    private void UpdateSummary(List<Checkbook> list)
    {
        cardCount.SetCount(list.Count);
        cardPages.SetCount(list.Sum(c => c.PageCount), "برگ");
        cardLast.Text = list.Count > 0 ? StringHelper.ConvertToPersianDigits(list[0].SerialNumber) : "—";
    }

    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            var (success, list) = await AsyncHelper.RunWithLoading(this, () => _repo.GetByAccountIdAsync(_account.Id), "در حال بارگذاری دسته چک‌ها...");
            if (!success || list is null || IsDisposed) return;
            _current = list;
            dgvCheckbooks.DataSource = list;
            UpdateSummary(list);
            UpdateButtons();
        }
        finally { try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CheckbookManagementForm"); } }
    }

    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            string serial = txtSerial.Text.Trim();
            string sayad = txtStartSayad.Text.Trim();
            string startCheck = txtStartCheck.Text.Trim();

            if (serial.Length == 0 || sayad.Length == 0 || startCheck.Length == 0)
            {
                ModernMessageBox.ShowWarning("پر کردن سریال، شناسه صیاد و شماره چک اول الزامی است."); return;
            }
            if (!long.TryParse(startCheck, out long start)) { ModernMessageBox.ShowWarning("شماره چک اول باید عددی باشد."); return; }
            if (sayad.Length != 12 || !long.TryParse(sayad, out _)) { ModernMessageBox.ShowWarning("شناسه صیاد باید دقیقاً ۱۲ رقم باشد."); return; }

            int pages = (int)numPageCount.Value;
            long end = start + pages - 1;

            // جلوگیری از ثبت دسته چک با شماره‌های تکراری/هم‌پوشان
            var overlap = _current.FirstOrDefault(c =>
                long.TryParse(c.StartCheckNumber, out long s2) && c.PageCount > 0 &&
                start <= s2 + c.PageCount - 1 && s2 <= end);
            if (overlap is not null)
            {
                ModernMessageBox.ShowWarning($"محدوده شماره چک با دسته چک «{overlap.SerialNumber}» ({StringHelper.ConvertToPersianDigits(overlap.RangeDisplay)}) هم‌پوشانی دارد.");
                return;
            }

            var checkbook = new Checkbook
            {
                AccountId = _account.Id,
                SerialNumber = serial,
                StartSayadNumber = sayad,
                StartCheckNumber = startCheck,
                PageCount = pages
            };

            bool success = await AsyncHelper.RunWithLoading(this, async () => { await _repo.AddAsync(checkbook); }, "در حال ثبت...");
            if (success && !IsDisposed)
            {
                ToastHelper.Show("دسته چک ثبت شد.", true);
                txtSerial.Text = ""; txtStartSayad.Text = ""; txtStartCheck.Text = ""; numPageCount.Value = 100;
                UpdatePreview();
                txtSerial.Focus();
                await LoadDataSafeAsync();
            }
        }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CheckbookManagementForm"); } }
    }

    private async Task DeleteSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (dgvCheckbooks.CurrentRow?.DataBoundItem is not Checkbook selected) return;
            if (!ModernMessageBox.AskConfirm($"آیا از حذف دسته چک با سریال «{selected.SerialNumber}» مطمئن هستید؟", "تأیید حذف")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () => { await _repo.DeleteAsync(selected.Id); }, "در حال حذف...");
            if (success && !IsDisposed) { ToastHelper.Show("حذف شد.", true); await LoadDataSafeAsync(); }
        }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CheckbookManagementForm"); } }
    }
}
