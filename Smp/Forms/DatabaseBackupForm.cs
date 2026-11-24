using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Helpers;

namespace Smp.Forms;

public class DatabaseBackupForm : Form
{
    private readonly string _connectionString;

    private Label lblStatus = null!;
    private Button btnBackup = null!;
    private Button btnOpenFolder = null!;
    private Button btnRestore = null!;

    private string _lastBackupFolder = "";

    // ✅ FIX P2: جلوگیری از تداخل در عملیات سنگین
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public DatabaseBackupForm(string connectionString)
    {
        _connectionString = connectionString;

        BuildUI();

        ShortcutHelper.ApplyStandardShortcuts(this);
        ShowInTaskbar = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "DatabaseBackupForm"); } }
        base.Dispose(disposing);
    }

    private static Label Desc(string text, float size, bool bold, Color color)
    {
        var l = ModernUI.Caption(text, size, bold, color);
        l.Dock = DockStyle.Fill;
        return l;
    }

    private void BuildUI()
    {
        var banner = ModernListKit.Banner(
            "توجه: قبل از انجام عملیات بازیابی، مطمئن شوید که فایل پشتیبان معتبر است.\n" +
            "عملیات بازیابی تمام اطلاعات فعلی را پاک کرده و اطلاعات فایل پشتیبان را جایگزین می‌کند.",
            ModernTheme.Danger);

        // ── پشتیبان‌گیری ──
        btnBackup = ModernButton.Success("تهیه نسخه پشتیبان (Backup)", 280, 38);
        btnBackup.Click += BtnBackup_Click;
        btnOpenFolder = ModernButton.Outline("باز کردن پوشه بک‌آپ", ModernTheme.Neutral, 200, 38);
        btnOpenFolder.Enabled = false;
        btnOpenFolder.Click += (s, e) =>
        {
            if (Directory.Exists(_lastBackupFolder))
                Process.Start(new ProcessStartInfo("explorer.exe", _lastBackupFolder) { UseShellExecute = true });
        };

        var backupCard = new ModernCard("پشتیبان‌گیری", ModernTheme.Success, 1, 40, 52);
        backupCard.Body.Controls.Add(Desc("یک نسخه‌ی کامل از دیتابیس در پوشه‌ی پیش‌فرض پشتیبان‌گیری ذخیره می‌شود.", 10.5f, false, ModernTheme.TextMuted), 0, 0);
        backupCard.Body.Controls.Add(ModernListKit.BuildActionBar(new Control[] { btnBackup, btnOpenFolder }, new Control[0]), 0, 1);

        // ── بازیابی ──
        btnRestore = ModernButton.Soft("بازیابی از نسخه پشتیبان (Restore)", ModernTheme.Danger, 320, 38);
        btnRestore.Click += BtnRestore_Click;

        var restoreCard = new ModernCard("بازیابی", ModernTheme.Danger, 1, 40, 52);
        restoreCard.Body.Controls.Add(Desc("دیتابیس از روی یک فایل پشتیبان (.bak) برمی‌گردد و اطلاعات فعلی جایگزین می‌شود.", 10.5f, false, ModernTheme.TextMuted), 0, 0);
        restoreCard.Body.Controls.Add(ModernListKit.BuildActionBar(new Control[] { btnRestore }, new Control[0]), 0, 1);

        // ── وضعیت ──
        lblStatus = new Label
        {
            Text = "سیستم آماده است.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ModernTheme.GetFont(11f, FontStyle.Bold),
            ForeColor = ModernTheme.Text,
            BackColor = Color.Transparent
        };
        var statusCard = new ModernCard("وضعیت", ModernTheme.Neutral, 1, 40);
        statusCard.Body.Controls.Add(lblStatus, 0, 0);

        ModernListKit.ComposeRows(this, "پشتیبان‌گیری و بازیابی دیتابیس", "تهیه‌ی نسخه‌ی پشتیبان و بازگرداندن اطلاعات", new Size(680, 700),
            (banner, 84), (backupCard, backupCard.Height + 12), (restoreCard, restoreCard.Height + 12), (statusCard, statusCard.Height + 12), (new Panel(), 0));
    }

    private async void BtnBackup_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            lblStatus.Text = "در حال تهیه نسخه پشتیبان... لطفاً صبر کنید.";
            lblStatus.ForeColor = ModernTheme.WarningText;

            string fullPath = "";

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                _lastBackupFolder = DatabaseBackupHelper.GetDefaultBackupPath(_connectionString);

                string fileName = $"PersonnelDB_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                fullPath = Path.Combine(_lastBackupFolder, fileName);

                await Task.Run(() => DatabaseBackupHelper.BackupDatabase(_connectionString, fullPath));
            }, "در حال تهیه نسخه پشتیبان...");

            if (success && !IsDisposed)
            {
                lblStatus.Text = "نسخه پشتیبان با موفقیت ذخیره شد.";
                lblStatus.ForeColor = ModernPaint.Darken(ModernTheme.Success, 70);
                btnOpenFolder.Enabled = true;

                ToastHelper.Show("نسخه پشتیبان با موفقیت ذخیره شد.", isSuccess: true);
                ModernMessageBox.ShowInfo($"نسخه پشتیبان با موفقیت ذخیره شد.\n\nمسیر ذخیره:\n{fullPath}\n\nمی‌توانید با دکمه «باز کردن پوشه»، فایل را کپی کرده و در جای امن نگهداری کنید.", "موفقیت");
            }
            else if (!IsDisposed)
            {
                lblStatus.Text = "خطا در تهیه نسخه پشتیبان!";
                lblStatus.ForeColor = ModernTheme.Danger;
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "DatabaseBackupForm.BtnBackup_Click");
            if (!IsDisposed) lblStatus.Text = "خطا در تهیه نسخه پشتیبان!";
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "DatabaseBackupForm"); }
        }
    }

    private async void BtnRestore_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (!ModernMessageBox.AskConfirm("آیا مطمئن هستید؟\n\nتمام اطلاعات فعلی پاک شده و اطلاعات فایل پشتیبان جایگزین می‌شود.\nتوصیه می‌شود قبل از این کار یک بک‌آپ جدید بگیرید.", "هشدار بازیابی"))
                return;

            using var ofd = new OpenFileDialog
            {
                Filter = "SQL Backup Files|*.bak",
                Title = "انتخاب فایل پشتیبان برای بازیابی"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            lblStatus.Text = "در حال بازیابی اطلاعات... لطفاً صبر کنید.";
            lblStatus.ForeColor = ModernTheme.WarningText;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() => DatabaseBackupHelper.RestoreDatabase(_connectionString, ofd.FileName));
            }, "در حال بازیابی اطلاعات...");

            if (success && !IsDisposed)
            {
                lblStatus.Text = "بازیابی با موفقیت انجام شد. لطفاً برنامه را ببندید.";
                lblStatus.ForeColor = ModernPaint.Darken(ModernTheme.Success, 70);

                ModernMessageBox.ShowInfo("اطلاعات با موفقیت بازیابی شد.\nلطفاً برنامه را بسته و دوباره باز کنید.", "موفقیت");
            }
            else if (!IsDisposed)
            {
                lblStatus.Text = "خطا در بازیابی اطلاعات!";
                lblStatus.ForeColor = ModernTheme.Danger;

                ModernMessageBox.ShowWarning("ممکن است برنامه دیگری به دیتابیس متصل باشد. لطفاً سایر اتصالات را قطع کرده و دوباره تلاش کنید.", "خطای بازیابی");
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "DatabaseBackupForm.BtnRestore_Click");
            if (!IsDisposed) lblStatus.Text = "خطا در بازیابی اطلاعات!";
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "DatabaseBackupForm"); }
        }
    }
}
