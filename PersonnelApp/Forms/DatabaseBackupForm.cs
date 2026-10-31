using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Helpers;

namespace PersonnelApp.Forms
{
    public class DatabaseBackupForm : Form
    {
        private readonly string _connectionString;

        // 🌟 تعریف کنترل‌ها به عنوان فیلد کلاس
        private Label lblStatus = null!;
        private Button btnBackup = null!;
        private Button btnOpenFolder = null!;
        private Button btnRestore = null!;

        private string _lastBackupFolder = "";

        public DatabaseBackupForm(string connectionString)
        {
            _connectionString = connectionString;

            BuildUI();

            ShortcutHelper.ApplyStandardShortcuts(this); // Esc → Close
            ShowInTaskbar = false;
        }

        private void BuildUI()
        {
            // تنظیمات کلی فرم
            UIHelper.SetupForm(this, "پشتیبان‌گیری و بازیابی دیتابیس", new Size(620, 520));

            // چیدمان اصلی (یک ستون و ۵ ردیف)
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(25) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));  // هشدار
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));  // دکمه بک‌آپ
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));  // دکمه پوشه
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));  // دکمه ریستور
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // وضعیت

            // --- ۱. هشدار ---
            var lblWarning = new Label
            {
                Text = "⚠️ توجه: قبل از انجام عملیات بازیابی، مطمئن شوید که فایل پشتیبان معتبر است.\nعملیات بازیابی تمام اطلاعات فعلی را پاک کرده و اطلاعات فایل پشتیبان را جایگزین می‌کند.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(10f, FontStyle.Bold),
                ForeColor = UIHelper.WarmErrorText,
                BackColor = UIHelper.WarmErrorBackground,
                Padding = new Padding(10)
            };

            // --- ۲. دکمه بک‌آپ ---
            btnBackup = UIHelper.CreateButton("💾 تهیه نسخه پشتیبان (Backup)", UIHelper.ActionGreen, 320, 45);
            btnBackup.Anchor = AnchorStyles.None;
            btnBackup.Margin = new Padding(0, 10, 0, 10);
            btnBackup.Click += BtnBackup_Click;

            // --- ۳. دکمه باز کردن پوشه ---
            btnOpenFolder = UIHelper.CreateButton("📂 باز کردن پوشه بک‌آپ", UIHelper.SectionBlue, 320, 45);
            btnOpenFolder.Anchor = AnchorStyles.None;
            btnOpenFolder.Margin = new Padding(0, 10, 0, 10);
            btnOpenFolder.Enabled = false; // در ابتدا غیرفعال است
            btnOpenFolder.Click += (s, e) =>
            {
                if (Directory.Exists(_lastBackupFolder))
                    Process.Start(new ProcessStartInfo("explorer.exe", _lastBackupFolder) { UseShellExecute = true });
            };

            // --- ۴. دکمه ریستور ---
            btnRestore = UIHelper.CreateButton("♻️ بازیابی از نسخه پشتیبان (Restore)", UIHelper.WarmPrimary, 320, 45);
            btnRestore.Anchor = AnchorStyles.None;
            btnRestore.Margin = new Padding(0, 10, 0, 10);
            btnRestore.Click += BtnRestore_Click;

            // --- ۵. نوار وضعیت ---
            lblStatus = new Label
            {
                Text = "سیستم آماده است.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText
            };

            // اضافه کردن به چیدمان
            mainLayout.Controls.Add(lblWarning, 0, 0);
            mainLayout.Controls.Add(btnBackup, 0, 1);
            mainLayout.Controls.Add(btnOpenFolder, 0, 2);
            mainLayout.Controls.Add(btnRestore, 0, 3);
            mainLayout.Controls.Add(lblStatus, 0, 4);

            Controls.Add(mainLayout);
        }

        // ==========================================================
        // 💾 عملیات بک‌آپ
        // ==========================================================
        private async void BtnBackup_Click(object? sender, EventArgs e)
        {
            lblStatus.Text = "در حال تهیه نسخه پشتیبان... لطفاً صبر کنید.";
            lblStatus.ForeColor = UIHelper.WarmWarningText;

            string fullPath = "";

            // 👈 استفاده از AsyncHelper به جای Application.DoEvents
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                _lastBackupFolder = DatabaseBackupHelper.GetDefaultBackupPath(_connectionString);

                string fileName = $"PersonnelDB_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                fullPath = Path.Combine(_lastBackupFolder, fileName);

                await Task.Run(() => DatabaseBackupHelper.BackupDatabase(_connectionString, fullPath));
            }, "در حال تهیه نسخه پشتیبان...");

            if (success)
            {
                lblStatus.Text = "✓ نسخه پشتیبان با موفقیت ذخیره شد.";
                lblStatus.ForeColor = UIHelper.ActionGreen;
                btnOpenFolder.Enabled = true;

                ToastHelper.Show("نسخه پشتیبان با موفقیت ذخیره شد.", isSuccess: true);
                ModernMessageBox.ShowInfo($"نسخه پشتیبان با موفقیت ذخیره شد.\n\nمسیر ذخیره:\n{fullPath}\n\nمی‌توانید با دکمه «باز کردن پوشه»، فایل را کپی کرده و در جای امن نگهداری کنید.", "موفقیت");
            }
            else
            {
                lblStatus.Text = "❌ خطا در تهیه نسخه پشتیبان!";
                lblStatus.ForeColor = UIHelper.WarmDanger;
            }
        }

        // ==========================================================
        // ♻️ عملیات ریستور (بازیابی)
        // ==========================================================
        private async void BtnRestore_Click(object? sender, EventArgs e)
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
            lblStatus.ForeColor = UIHelper.WarmWarningText;

            // 👈 استفاده از AsyncHelper به جای Application.DoEvents
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() => DatabaseBackupHelper.RestoreDatabase(_connectionString, ofd.FileName));
            }, "در حال بازیابی اطلاعات...");

            if (success)
            {
                lblStatus.Text = "✓ بازیابی با موفقیت انجام شد. لطفاً برنامه را ببندید.";
                lblStatus.ForeColor = UIHelper.ActionGreen;

                ModernMessageBox.ShowInfo("اطلاعات با موفقیت بازیابی شد.\nلطفاً برنامه را بسته و دوباره باز کنید.", "موفقیت");
            }
            else
            {
                lblStatus.Text = "❌ خطا در بازیابی اطلاعات!";
                lblStatus.ForeColor = UIHelper.WarmDanger;

                // خطا توسط AsyncHelper نمایش داده شده، اما اینجا می‌توانیم راهنمایی بیشتری بدهیم
                ModernMessageBox.ShowWarning("ممکن است برنامه دیگری به دیتابیس متصل باشد. لطفاً سایر اتصالات را قطع کرده و دوباره تلاش کنید.", "خطای بازیابی");
            }
        }
    }
}