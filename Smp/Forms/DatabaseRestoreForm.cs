using Microsoft.Data.SqlClient;
using Microsoft.VisualBasic;
using Smp.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class DatabaseRestoreForm : Form
{
    private readonly string _connectionString;
    private string _databaseName = "";
    private string _dataSource = "";

    private ModernTextBox txtBackupPath = null!;
    private Button btnBrowse = null!, btnRestore = null!, btnCancel = null!;
    private Label lblBackupFileInfo = null!;
    private ModernStatCard cardDb = null!, cardEmp = null!, cardUser = null!, cardPayroll = null!;

    // ✅ FIX P2: جلوگیری از تداخل و کرش
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public DatabaseRestoreForm(string connectionString)
    {
        _connectionString = connectionString;
        ExtractDatabaseInfo();

        BuildUI();

        ShortcutHelper.ApplyStandardShortcuts(this);

        Shown += async (s, e) => await LoadCurrentDatabaseInfoAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "DatabaseRestoreForm"); } }
        base.Dispose(disposing);
    }

    private void ExtractDatabaseInfo()
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            _databaseName = builder.InitialCatalog;
            _dataSource = builder.DataSource;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "DatabaseRestoreForm.ExtractDatabaseInfo");
        }
    }

    private void BuildUI()
    {
        var banner = ModernListKit.Banner(
            "هشدار جدی:\n" +
            "با انجام این عملیات، تمام داده‌های فعلی دیتابیس (شامل تمام پرسنل، حقوق‌ها، قراردادها، کاربران و...) پاک می‌شود\n" +
            "و اطلاعات فایل پشتیبان جایگزین آن خواهد شد. این عملیات غیرقابل بازگشت است!",
            ModernTheme.Danger);

        // ── وضعیت دیتابیس فعلی ──
        var strip = ModernReport.CreateStatStrip(4, 84);
        cardDb = strip.Add("دیتابیس فعلی", ModernTheme.Primary);
        cardEmp = strip.Add("پرسنل", ModernTheme.Teal);
        cardUser = strip.Add("کاربران", ModernTheme.Purple);
        cardPayroll = strip.Add("فیش‌های حقوقی", ModernTheme.Orange);
        cardDb.Text = string.IsNullOrWhiteSpace(_databaseName) ? "در حال بارگذاری..." : _databaseName;
        cardEmp.Text = "—"; cardUser.Text = "—"; cardPayroll.Text = "—";

        // ── انتخاب فایل ──
        txtBackupPath = ModernUI.Input(placeholder: "فایلی انتخاب نشده است");
        txtBackupPath.ReadOnly = true;
        btnBrowse = ModernButton.Soft("انتخاب فایل...", ModernTheme.Primary, 150, 36);
        btnBrowse.Click += BtnBrowse_Click;
        var pickBar = ModernListKit.BuildToolbar((txtBackupPath, -1), (btnBrowse, 160));

        lblBackupFileInfo = new Label
        {
            Text = "لطفاً فایل پشتیبان را انتخاب کنید...",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ModernTheme.GetFont(10f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent
        };

        var fileCard = new ModernCard("فایل پشتیبان (.bak)", ModernTheme.Primary, 1, 50, 78);
        fileCard.Body.Controls.Add(pickBar, 0, 0);
        fileCard.Body.Controls.Add(lblBackupFileInfo, 0, 1);

        // ── دکمه‌ها ──
        btnCancel = ModernButton.Cancel("انصراف", 110, 36);
        btnCancel.Click += (s, e) => this.Close();
        btnRestore = ModernButton.Danger("بازیابی دیتابیس", 190, 36);
        btnRestore.Enabled = false;
        btnRestore.Click += BtnRestore_Click;
        var actions = ModernListKit.BuildActionBar(new Control[] { btnCancel }, new Control[] { btnRestore });

        ModernListKit.ComposeRows(this, "بازیابی دیتابیس (Restore)", "جایگزینی دیتابیس فعلی با یک فایل پشتیبان", new Size(800, 680),
            (banner, 96), (strip, 100), (fileCard, fileCard.Height + 12), (new Panel(), 0), (actions, 64));
    }

    private async Task LoadCurrentDatabaseInfoAsync()
    {
        if (IsDisposed) return;

        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                        SELECT 
                            (SELECT COUNT(*) FROM Employees) AS EmpCount,
                            (SELECT COUNT(*) FROM Users) AS UserCount,
                            (SELECT COUNT(*) FROM Payrolls) AS PayrollCount";

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    int empCount = reader.GetInt32(0);
                    int userCount = reader.GetInt32(1);
                    int payrollCount = reader.GetInt32(2);

                    if (IsDisposed) return;

                    cardDb.Text = _databaseName;
                    cardEmp.SetCount(empCount, "نفر");
                    cardUser.SetCount(userCount, "نفر");
                    cardPayroll.SetCount(payrollCount, "ردیف");
                }
            }, "در حال خواندن اطلاعات دیتابیس...");

            if (!success && !IsDisposed)
            {
                cardDb.Text = "خطا در خواندن اطلاعات (جزئیات در فایل لاگ)";
                cardDb.Accent = ModernTheme.Danger;
            }
        }
        catch (ObjectDisposedException) { /* نادیده بگیر */ }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "DatabaseRestoreForm.LoadCurrentDatabaseInfoAsync");
            if (!IsDisposed)
            {
                cardDb.Text = "خطا: " + ex.Message;
                cardDb.Accent = ModernTheme.Danger;
            }
        }
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "SQL Backup Files (*.bak)|*.bak|All Files (*.*)|*.*",
            Title = "انتخاب فایل پشتیبان دیتابیس"
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            txtBackupPath.Text = ofd.FileName;
            btnRestore.Enabled = true;

            try
            {
                var fileInfo = new FileInfo(ofd.FileName);
                double sizeInMb = fileInfo.Length / (1024.0 * 1024.0);
                string formattedSize = NumberHelper.FormatThousands((decimal)sizeInMb, decimals: 2, usePersianDigits: true);
                string formattedDate = DateHelper.ToPersianDateTime(fileInfo.LastWriteTime);

                lblBackupFileInfo.Text = $"فایل: {fileInfo.Name}\n" +
                                         $"حجم فایل: {formattedSize} مگابایت\n" +
                                         $"تاریخ فایل: {formattedDate}";
                lblBackupFileInfo.ForeColor = ModernTheme.Text;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "DatabaseRestoreForm.BrowseFile");
                lblBackupFileInfo.Text = "خطا در خواندن اطلاعات فایل.";
                lblBackupFileInfo.ForeColor = ModernTheme.Danger;
            }
        }
    }

    private async void BtnRestore_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtBackupPath.Text) || !File.Exists(txtBackupPath.Text) || IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return; // ✅ FIX

        try
        {
            if (!ModernMessageBox.AskConfirm("⚠️ هشدار بسیار جدی:\n\n" +
                "با این عملیات، تمام داده‌های فعلی دیتابیس شما پاک خواهد شد و داده‌های فایل پشتیبان جایگزین آن می‌شود.\n\n" +
                "آیا از انجام این عملیات مطمئن هستید؟", "تأیید اول"))
                return;

            var result = ModernMessageBox.Show(
                "این آخرین هشدار است!\n\n" +
                "لطفاً عبارت «تایید» را در کادر بعدی تایپ کنید تا عملیات بازیابی انجام شود.",
                "تأیید نهایی", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

            if (result != DialogResult.OK) return;

            string userInput = Interaction.InputBox("برای تأیید، عبارت «تایید» را تایپ کنید:", "تأیید نهایی", "");

            if (userInput.Trim() != "تایید")
            {
                ModernMessageBox.ShowWarning("عبارت تأیید صحیح نیست. عملیات لغو شد.");
                return;
            }

            await PerformRestoreAsync();
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "DatabaseRestoreForm"); }
        }
    }

    private async Task PerformRestoreAsync()
    {
        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() => DatabaseBackupHelper.RestoreDatabase(_connectionString, txtBackupPath.Text));
            }, "در حال بازیابی دیتابیس (لطفاً برنامه را نبندید)...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("دیتابیس با موفقیت بازیابی شد.", isSuccess: true);
                ModernMessageBox.ShowInfo("✅ دیتابیس با موفقیت بازیابی شد!\n\n" +
                    "برنامه باید مجدداً باز شود تا تغییرات اعمال شوند.\n" +
                    "برنامه به صورت خودکار بسته خواهد شد.", "بازیابی موفق");

                Application.Exit();
            }
            else if (!IsDisposed)
            {
                ModernMessageBox.ShowError(
                    "خطا در بازیابی دیتابیس!\n\n" +
                    "دلایل احتمالی:\n" +
                    "• فایل پشتیبان معتبر نیست\n" +
                    "• کاربر SQL دسترسی کافی ندارد\n" +
                    "• دیتابیس هنوز در حال استفاده توسط برنامه‌های دیگر است\n" +
                    "• برنامه در حال اجرای عملیات دیگری روی دیتابیس است\n\n" +
                    "جزئیات کامل خطا در فایل Errors.log ثبت شد.", "خطا در بازیابی");
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "DatabaseRestoreForm.PerformRestoreAsync");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message, "خطا");
        }
    }
}
