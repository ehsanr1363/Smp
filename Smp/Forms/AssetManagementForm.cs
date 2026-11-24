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

/// <summary>لیست و مدیریت اموال: جستجو، فیلتر وضعیت، ثبت / ویرایش / حذف، پیوست‌ها و تاریخچه‌ی جابه‌جایی</summary>
public class AssetManagementForm : Form
{
    private readonly string _connectionString;
    private readonly AssetRepository _repo;

    private DataGridView dgv = null!;
    private ModernTextBox txtSearch = null!;
    private ComboBox cmbStatus = null!;
    private Label lblInfo = null!;
    private System.Windows.Forms.Timer _searchTimer = null!;
    private ModernStatCard cardCount = null!, cardAmount = null!, cardActive = null!, cardRepair = null!;

    private Button btnImport = null!, btnTemplate = null!;
    private Button btnAdd = null!, btnEdit = null!, btnDelete = null!, btnAttach = null!, btnHistory = null!, btnRefresh = null!;

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _isInitializing = true;

    private readonly Dictionary<string, string> _headers = new();
    private const string AllStatuses = "همه وضعیت‌ها";

    private static readonly Dictionary<string, Color> _statusTones = new()
    {
        [AssetStatuses.Active] = ModernTheme.Success,
        [AssetStatuses.InRepair] = ModernTheme.Warning,
        [AssetStatuses.Scrapped] = ModernTheme.Neutral,
        [AssetStatuses.Sold] = ModernTheme.Primary
    };

    private class AssetRow
    {
        public int Id { get; set; }
        public string کد { get; set; } = "";
        public string نام { get; set; } = "";
        public string دسته { get; set; } = "";
        public string سریال { get; set; } = "";
        public string واحد { get; set; } = "";
        public string تحویل_گیرنده { get; set; } = "";
        public string محل { get; set; } = "";
        public string تاریخ_خرید { get; set; } = "";
        public decimal مبلغ { get; set; }
        public string وضعیت { get; set; } = "";
        public string اسناد { get; set; } = "";
    }

    public AssetManagementForm(string connectionString)
    {
        _connectionString = connectionString;
        _repo = new AssetRepository(connectionString);

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this);
        Load += async (s, e) =>
        {
            _isInitializing = false;
            await LoadDataAsync();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception ex) { LogHelper.LogException(ex, "AssetManagementForm"); }
            try { _searchTimer?.Dispose(); } catch (Exception ex) { LogHelper.LogException(ex, "AssetManagementForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── جدول ──
        dgv = ModernGrid.Create();
        dgv.AutoGenerateColumns = false;
        dgv.MultiSelect = true;
        dgv.FillColumns();
        dgv.UseAiryLook(46);
        dgv.UseSelectionAccent(ModernTheme.Primary);
        dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        AddCol("کد", "کد اموال", 8, true, 90);
        AddCol("نام", "نام مال", 18, false, 150);
        AddCol("دسته", "دسته‌بندی", 9, true, 90);
        AddCol("سریال", "شماره سریال", 9, true, 90);
        AddCol("واحد", "واحد / بخش", 10, true, 90);
        AddCol("تحویل_گیرنده", "تحویل‌گیرنده", 11, true, 100);
        AddCol("محل", "محل نگهداری", 10, true, 90);
        AddCol("تاریخ_خرید", "تاریخ خرید", 8, true, 90);
        AddCol("مبلغ", "مبلغ خرید (ریال)", 10, true, 110);
        AddCol("وضعیت", "وضعیت", 8, true, 90);
        AddCol("اسناد", "اسناد خرید", 6, true, 60);

        dgv.UseBadges("وضعیت", _statusTones);
        dgv.CellFormatting += Dgv_CellFormatting;
        dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenEdit(); };

        void AddCol(string name, string header, float weight, bool center, int min)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name, DataPropertyName = name, HeaderText = header,
                FillWeight = weight, MinimumWidth = min, SortMode = DataGridViewColumnSortMode.Automatic
            };
            col.DefaultCellStyle.Alignment = center ? DataGridViewContentAlignment.MiddleCenter : DataGridViewContentAlignment.MiddleRight;
            dgv.Columns.Add(col);
            _headers[name] = header;
        }
        // ModernGrid هدرها را از نام ستون می‌نویسد (با زیرخط)؛ عنوان فارسی را دوباره می‌گذاریم
        dgv.DataBindingComplete += (s, e) =>
        {
            foreach (var kv in _headers)
                if (dgv.Columns.Contains(kv.Key)) dgv.Columns[kv.Key].HeaderText = kv.Value;
        };

        // ── کارت‌های خلاصه ──
        var strip = ModernReport.CreateStatStrip(4, 84);
        cardCount = strip.Add("تعداد کل اموال", ModernTheme.Primary);
        cardAmount = strip.Add("جمع مبلغ خرید", ModernTheme.Teal);
        cardActive = strip.Add("فعال", ModernTheme.Success);
        cardRepair = strip.Add("در تعمیر", ModernTheme.Warning);
        cardCount.SetCount(0, "مورد");
        cardAmount.SetAmount(0m, "ریال");
        cardActive.SetCount(0, "مورد");
        cardRepair.SetCount(0, "مورد");

        // ── جستجو و فیلتر ──
        txtSearch = ModernUI.Input(placeholder: "جستجو در کد، نام، سریال، واحد، تحویل‌گیرنده...");
        _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await LoadDataAsync(); };
        txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

        cmbStatus = ModernUI.Combo();
        cmbStatus.Items.Add(AllStatuses);
        cmbStatus.Items.AddRange(AssetStatuses.All.Cast<object>().ToArray());
        cmbStatus.SelectedIndex = 0;
        cmbStatus.SelectedIndexChanged += async (s, e) => { if (!_isInitializing) await LoadDataAsync(); };

        var toolbar = ModernListKit.BuildToolbar(
            (txtSearch, 380),
            (ModernListKit.FilterLabel("وضعیت:"), 56),
            (cmbStatus, 200));

        // ── نوار اطلاعات (جای صفحه‌بندی) ──
        lblInfo = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = ModernTheme.GetFont(10f),
            ForeColor = ModernTheme.Text,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes
        };

        // ── دکمه‌ها ──
        btnAdd = ModernButton.Success("➕ ثبت مال جدید", 160, 36);
        btnAdd.Click += (s, e) => OpenAdd();
        btnEdit = ModernButton.Soft("ویرایش", ModernTheme.Primary, 100, 36);
        btnEdit.Click += (s, e) => OpenEdit();
        btnDelete = ModernButton.Soft("حذف", ModernTheme.Danger, 100, 36);
        btnDelete.Click += async (s, e) => await DeleteSelectedAsync();
        btnAttach = ModernButton.Outline("📎 پیوست‌ها", ModernTheme.Neutral, 120, 36);
        btnAttach.Click += (s, e) => OpenAttachments();
        btnHistory = ModernButton.Outline("🔁 تاریخچه جابه‌جایی", ModernTheme.Neutral, 160, 36);
        btnHistory.Click += async (s, e) => await ShowHistoryAsync();
        btnRefresh = ModernButton.Outline("بروزرسانی", ModernTheme.Neutral, 120, 36);
        btnRefresh.Click += async (s, e) => { txtSearch.Text = ""; cmbStatus.SelectedIndex = 0; await LoadDataAsync(); };

        btnImport = ModernButton.Outline("ایمپورت اکسل", ModernTheme.Neutral, 130, 36);
        btnImport.Click += async (s, e) => await ImportExcelAsync();
        btnTemplate = ModernButton.Outline("دانلود قالب", ModernTheme.Neutral, 130, 36);
        btnTemplate.Click += (s, e) => DownloadTemplate();

        var actions = ModernListKit.BuildActionBar(
            new Control[] { btnAdd, btnEdit, btnDelete, btnAttach, btnHistory },
            new Control[] { btnImport, btnTemplate, btnRefresh });

        ModernListKit.Compose(this, "مدیریت اموال", "ثبت، پیگیری و تحویل اموال شرکت به همراه اسناد خرید",
            new Size(1500, 800), strip, 84, toolbar, ModernReport.WrapGrid(dgv), lblInfo, actions);
    }

    private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.RowIndex >= dgv.RowCount) return;
        string col = dgv.Columns[e.ColumnIndex].Name;
        if (col == "مبلغ" && e.Value is decimal m)
        {
            e.Value = m == 0m ? "—" : NumberHelper.FormatThousands(m, usePersianDigits: true);
            e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
            e.FormattingApplied = true;
        }
        else if (col == "نام")
            e.CellStyle.ForeColor = ModernTheme.TextStrong;
    }

    // ───────────── بارگذاری ─────────────
    private async Task LoadDataAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;   // درخواست هم‌زمان نادیده گرفته می‌شود
        try
        {
            string search = txtSearch.Text.Trim();
            string? status = cmbStatus.SelectedItem as string;
            if (status == AllStatuses) status = null;

            List<Asset> list = new();
            bool ok = await AsyncHelper.RunWithLoading(this, async () =>
            {
                list = await _repo.GetAllAsync(search, status);
            }, "در حال بارگذاری اموال...");
            if (!ok || IsDisposed) return;

            var rows = list.Select(a => new AssetRow
            {
                Id = a.Id,
                کد = a.AssetCode,
                نام = a.Name,
                دسته = a.Category ?? "",
                سریال = a.SerialNumber ?? "",
                واحد = a.DepartmentName ?? "—",
                تحویل_گیرنده = a.EmployeeName ?? "—",
                محل = a.Location ?? "",
                تاریخ_خرید = a.PurchaseDate.HasValue ? DateHelper.ToPersianDate(a.PurchaseDate.Value) : "",
                مبلغ = a.PurchaseAmount,
                وضعیت = a.Status,
                اسناد = a.DocCount > 0 ? a.DocCount.ToString() : "—"
            }).ToList();

            dgv.DataSource = null;
            dgv.DataSource = rows;
            dgv.ClearSelection();

            cardCount.SetCount(list.Count, "مورد");
            cardAmount.SetAmount(list.Sum(a => a.PurchaseAmount), "ریال");
            cardActive.SetCount(list.Count(a => a.Status == AssetStatuses.Active), "مورد");
            cardRepair.SetCount(list.Count(a => a.Status == AssetStatuses.InRepair), "مورد");
            lblInfo.Text = $"{list.Count} مال نمایش داده می‌شود";
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AssetManagementForm.LoadDataAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در دریافت اموال:\n" + ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (ObjectDisposedException) { }
        }
    }

    private List<int> SelectedIds() => dgv.SelectedRows.Cast<DataGridViewRow>()
        .Select(r => (r.DataBoundItem as AssetRow)?.Id ?? 0).Where(i => i > 0).ToList();

    private string SelectedCode() => (dgv.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault()?.DataBoundItem as AssetRow)?.کد ?? "";

    // ───────────── عملیات ─────────────
    private async void OpenAdd()
    {
        using (var f = new AssetEditForm(_connectionString))
            if (f.ShowDialog(this) != DialogResult.OK) return;
        await LoadDataAsync();
    }

    private async void OpenEdit()
    {
        var ids = SelectedIds();
        if (ids.Count != 1) { ModernMessageBox.ShowWarning("یک مال را برای ویرایش انتخاب کنید."); return; }
        using (var f = new AssetEditForm(_connectionString, ids[0]))
            if (f.ShowDialog(this) != DialogResult.OK) return;
        await LoadDataAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        var ids = SelectedIds();
        if (ids.Count == 0) { ModernMessageBox.ShowWarning("حداقل یک مال را انتخاب کنید."); return; }
        if (!ModernMessageBox.AskConfirm($"آیا از حذف {ids.Count} مال انتخاب‌شده مطمئن هستید؟\nلینک اسناد، پیوست‌ها و تاریخچه‌ی آن هم حذف می‌شود.", "تایید حذف"))
            return;
        try
        {
            bool ok = await AsyncHelper.RunWithLoading(this, async () => await _repo.DeleteAsync(ids), "در حال حذف...");
            if (!ok) return;
            AuditHelper.Log("حذف", "اموال", $"حذف {ids.Count} مال");
            ToastHelper.Show("حذف شد.", true);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AssetManagementForm.DeleteSelectedAsync");
            ModernMessageBox.ShowError("خطا در حذف:\n" + ex.Message);
        }
    }

    private void OpenAttachments()
    {
        var ids = SelectedIds();
        if (ids.Count != 1) { ModernMessageBox.ShowWarning("یک مال را انتخاب کنید."); return; }
        using var f = new AttachmentsForm(_connectionString, "Asset", ids[0], $"پیوست‌های مال {SelectedCode()}");
        f.ShowDialog(this);
    }

    private void DownloadTemplate()
    {
        using var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = "Assets_Import_Template.xlsx", Title = "ذخیره قالب اکسل اموال" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            AssetExcelHelper.SaveTemplate(sfd.FileName);
            ToastHelper.Show("قالب ذخیره شد.", true);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AssetManagementForm.DownloadTemplate");
            ModernMessageBox.ShowError("خطا در ذخیره‌ی قالب (شاید فایل باز است):\n" + ex.Message);
        }
    }

    private async Task ImportExcelAsync()
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx", Title = "انتخاب فایل اکسل اموال" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            AssetImportResult? res = null;
            bool ok = await AsyncHelper.RunWithLoading(this, async () => { res = await AssetExcelHelper.ParseAsync(ofd.FileName, _repo); }, "در حال خواندن فایل...");
            if (!ok || res is null || IsDisposed) return;

            string Preview(List<string> l) => string.Join("\n", l.Take(10)) + (l.Count > 10 ? $"\n... و {l.Count - 10} مورد دیگر" : "");

            if (res.Valid.Count == 0)
            {
                ModernMessageBox.ShowWarning("هیچ ردیف معتبری برای ثبت پیدا نشد." + (res.Errors.Count > 0 ? "\n\n" + Preview(res.Errors) : ""));
                return;
            }

            string msg = $"{res.Valid.Count} مال آماده‌ی ثبت است.";
            if (res.Errors.Count > 0) msg += $"\n\n{res.Errors.Count} ردیف رد شد:\n" + Preview(res.Errors);
            if (res.Warnings.Count > 0) msg += $"\n\nهشدارها:\n" + Preview(res.Warnings);
            msg += "\n\nثبت انجام شود؟";
            if (!ModernMessageBox.AskConfirm(msg, "تایید ایمپورت")) return;

            int count = 0;
            bool saved = await AsyncHelper.RunWithLoading(this, async () => { count = await _repo.ImportAsync(res.Valid); }, "در حال ثبت اموال...");
            if (!saved) return;

            AuditHelper.Log("ایمپورت", "اموال", $"ایمپورت {count} مال از اکسل");
            ToastHelper.Show($"{count} مال ثبت شد.", true);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AssetManagementForm.ImportExcelAsync");
            ModernMessageBox.ShowError("خطا در ایمپورت:\n" + ex.Message);
        }
    }

    private async Task ShowHistoryAsync()
    {
        var ids = SelectedIds();
        if (ids.Count != 1) { ModernMessageBox.ShowWarning("یک مال را انتخاب کنید."); return; }
        try
        {
            var list = await _repo.GetTransfersAsync(ids[0]);
            if (list.Count == 0) { ModernMessageBox.ShowWarning("برای این مال جابه‌جایی ثبت نشده است."); return; }

            string Part(string? d, string? e, string? l) =>
                string.Join(" / ", new[] { d, e, l }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } s ? s : "—";

            var text = string.Join("\n\n", list.Select(t =>
                $"{DateHelper.ToPersianDate(t.TransferDate)}\nاز: {Part(t.FromDepartment, t.FromEmployee, t.FromLocation)}\nبه: {Part(t.ToDepartment, t.ToEmployee, t.ToLocation)}"));
            MessageBox.Show(text, $"تاریخچه‌ی جابه‌جایی {SelectedCode()}", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AssetManagementForm.ShowHistoryAsync");
            ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }
}
