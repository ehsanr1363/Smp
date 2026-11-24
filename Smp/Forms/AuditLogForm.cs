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

public class AuditLogForm : Form
{
    private readonly AuditLogRepository _repository;

    private DataGridView dgvLogs = null!;
    private Button btnRefresh = null!;
    private Label lblCount = null!;
    private ModernTextBox txtSearch = null!;
    private System.Windows.Forms.Timer _searchTimer = null!;

    private List<AuditLog> _allLogs = new();
    private readonly Dictionary<string, Color> _actionTones = new();

    // جلوگیری از بارگذاری همزمان
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public AuditLogForm(string connectionString)
    {
        _repository = new AuditLogRepository(connectionString);

        BuildUI();

        // Esc → بستن، Ctrl+S → بروزرسانی
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);

        // بارگذاری بعد از نمایش فرم
        Shown += async (s, e) => await LoadLogsAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AuditLogForm"); }
            try { _searchTimer?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AuditLogForm"); }
        }
        base.Dispose(disposing);
    }

    private static Color ActionTone(string? action)
    {
        string a = action ?? string.Empty;
        if (a.Contains("حذف")) return ModernTheme.Danger;
        if (a.Contains("ویرایش") || a.Contains("تغییر")) return ModernTheme.Warning;
        if (a.Contains("ثبت") || a.Contains("افزودن") || a.Contains("ایجاد")) return ModernTheme.Success;
        if (a.Contains("ورود")) return ModernTheme.Primary;
        return ModernTheme.Neutral;
    }

    private void BuildUI()
    {
        // ── جدول ──
        dgvLogs = ModernGrid.Create();
        dgvLogs.AutoGenerateColumns = true;
        dgvLogs.FillColumns();
        dgvLogs.UseAiryLook(44);
        dgvLogs.UseSelectionAccent(ModernTheme.Primary);
        dgvLogs.UseBadges("ActionType", _actionTones);

        dgvLogs.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string col = dgvLogs.Columns[e.ColumnIndex].Name;

            if (col == "LogDate" && e.Value is DateTime dt)
            {
                e.Value = DateHelper.ToPersianDateTime(dt);
                e.FormattingApplied = true;
            }
            else if (col == "Description") e.CellStyle.ForeColor = ModernTheme.TextStrong;
            else if (col == "ModuleName" || col == "Username") e.CellStyle.ForeColor = ModernTheme.TextMuted;
        };

        // ستون‌ها خودکار ساخته می‌شوند؛ بعد از هر بایند عرض و ترازشان را مرتب می‌کنیم
        dgvLogs.DataBindingComplete += (s, e) =>
        {
            void Tune(string name, float weight, int min, bool center)
            {
                if (!dgvLogs.Columns.Contains(name)) return;
                var c = dgvLogs.Columns[name];
                c.FillWeight = weight;
                c.MinimumWidth = min;
                c.DefaultCellStyle.Alignment = center ? DataGridViewContentAlignment.MiddleCenter : DataGridViewContentAlignment.MiddleRight;
            }
            Tune("Username", 12, 110, false);
            Tune("ActionType", 12, 110, true);
            Tune("ModuleName", 14, 120, false);
            Tune("Description", 44, 220, false);
            Tune("LogDate", 18, 150, true);
        };

        // ── جستجو و تعداد ──
        txtSearch = ModernUI.Input(placeholder: "جستجو در کاربر، بخش، نوع عملیات و توضیحات...");
        _searchTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); ApplyFilter(); };
        txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

        lblCount = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ModernTheme.TextMuted,
            Font = ModernTheme.GetFont(10f),
            BackColor = Color.Transparent
        };
        var toolbar = ModernListKit.BuildToolbar((txtSearch, 420), (lblCount, 220));

        // ── دکمه ──
        btnRefresh = ModernButton.Primary("بروزرسانی لاگ‌ها", 180, 36);
        btnRefresh.Click += async (s, e) => await LoadLogsAsync();
        var actions = ModernListKit.BuildActionBar(new Control[] { btnRefresh }, new Control[0]);

        ModernListKit.ComposeRows(this, "مشاهده لاگ تغییرات سیستم", "تاریخچه‌ی ثبت، ویرایش و حذف‌های انجام‌شده توسط کاربران", new Size(1000, 600),
            (toolbar, 56), (ModernReport.WrapGrid(dgvLogs), 0), (actions, 64));
    }

    private void ApplyFilter()
    {
        if (IsDisposed) return;
        string q = txtSearch.Text.Trim();

        IEnumerable<AuditLog> view = _allLogs;
        if (q.Length > 0)
        {
            view = _allLogs.Where(l =>
                (l.Username ?? "").Contains(q) ||
                (l.ActionType ?? "").Contains(q) ||
                (l.ModuleName ?? "").Contains(q) ||
                (l.Description ?? "").Contains(q));
        }
        var list = view.ToList();

        ModernListKit.FillTones(_actionTones, list.Select(l => (string?)l.ActionType), t => ActionTone(t));
        dgvLogs.DataSource = list;

        lblCount.Text = list.Count == 0
            ? (q.Length > 0 ? "موردی پیدا نشد." : "لاگی یافت نشد.")
            : $"{NumberHelper.FormatThousands(list.Count, usePersianDigits: true)} رکورد";
    }

    private async Task LoadLogsAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            var (success, logs) = await AsyncHelper.RunWithLoading(
                this,
                () => _repository.GetAllLogsAsync(),
                "در حال بارگذاری لاگ‌ها...");

            if (!success || logs is null || IsDisposed) return;

            _allLogs = logs.ToList();
            ApplyFilter();

            int count = _allLogs.Count;
            ToastHelper.Show(
                count == 0
                    ? "لاگی یافت نشد."
                    : $"{NumberHelper.FormatThousands(count, usePersianDigits: true)} لاگ بارگذاری شد.",
                isSuccess: count > 0);
        }
        catch (ObjectDisposedException) { /* نادیده بگیر */ }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AuditLogForm.LoadLogsAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AuditLogForm"); }
        }
    }
}
