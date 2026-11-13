using Microsoft.Data.SqlClient;
using Microsoft.VisualBasic;
using SMP.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class DatabaseRestoreForm : Form
    {
        private readonly string _connectionString;
        private string _databaseName = "";
        private string _dataSource = "";

        private TextBox txtBackupPath = null!;
        private Button btnBrowse = null!, btnRestore = null!, btnCancel = null!;
        private Label lblCurrentDbInfo = null!, lblBackupFileInfo = null!;

        // ✅ FIX P2: جلوگیری از تداخل و کرش
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public DatabaseRestoreForm(string connectionString)
        {
            _connectionString = connectionString;
            ExtractDatabaseInfo();

            BuildUI();
            this.ApplyStandardHeader("بازیابی دیتابیس (Restore)", new Size(750, 520));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this);

            Shown += async (s, e) => await LoadCurrentDatabaseInfoAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
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
            var mainPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            var warningPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(255, 235, 235) };
            warningPanel.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, warningPanel.ClientRectangle,
                    Color.Red, 2, ButtonBorderStyle.Solid, Color.Red, 2, ButtonBorderStyle.Solid,
                    Color.Red, 2, ButtonBorderStyle.Solid, Color.Red, 2, ButtonBorderStyle.Solid);
            };

            var lblWarning = new Label
            {
                Text = "⚠️ هشدار جدی:\n" +
                       "با انجام این عملیات، تمام داده‌های فعلی دیتابیس (شامل تمام پرسنل، حقوق‌ها، قراردادها، کاربران و...) پاک می‌شود\n" +
                       "و اطلاعات فایل پشتیبان جایگزین آن خواهد شد. این عملیات غیرقابل بازگشت است!",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(153, 27, 27)
            };
            warningPanel.Controls.Add(lblWarning);

            var currentInfoPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            lblCurrentDbInfo = new Label
            {
                Text = "در حال بارگذاری...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(10),
                Font = UIHelper.PersianFont(10f),
                ForeColor = UIHelper.WarmText
            };
            currentInfoPanel.Controls.Add(lblCurrentDbInfo);

            var filePanel = new Panel { Dock = DockStyle.Fill };
            var fileLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            fileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            fileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var lblBrowse = new Label { Text = "فایل پشتیبان (.bak):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f) };
            txtBackupPath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = UIHelper.PersianFont(10f) };

            var fileRight = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            fileRight.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            fileRight.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));

            btnBrowse = UIHelper.CreateButton("📁 انتخاب فایل...", UIHelper.SectionBlue, 100, 35);
            btnBrowse.Dock = DockStyle.Fill;
            btnBrowse.Click += BtnBrowse_Click;

            fileRight.Controls.Add(txtBackupPath, 0, 0);
            fileRight.Controls.Add(btnBrowse, 1, 0);

            fileLayout.Controls.Add(lblBrowse, 0, 0);
            fileLayout.Controls.Add(fileRight, 1, 0);
            filePanel.Controls.Add(fileLayout);

            var fileInfoPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            lblBackupFileInfo = new Label
            {
                Text = "لطفاً فایل پشتیبان را انتخاب کنید...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(10),
                Font = UIHelper.PersianFont(10f),
                ForeColor = Color.Gray
            };
            fileInfoPanel.Controls.Add(lblBackupFileInfo);

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };

            btnRestore = UIHelper.CreateButton("⚠️ بازیابی دیتابیس", UIHelper.ActionRed, 180, 45);
            btnRestore.Enabled = false;
            btnRestore.Click += BtnRestore_Click;

            btnCancel = UIHelper.CreateButton("انصراف", UIHelper.SectionGray, 100, 45);
            btnCancel.Click += (s, e) => this.Close();

            btnPanel.Controls.Add(btnRestore);
            btnPanel.Controls.Add(btnCancel);

            layout.Controls.Add(warningPanel, 0, 0);
            layout.Controls.Add(currentInfoPanel, 0, 1);
            layout.Controls.Add(filePanel, 0, 2);
            layout.Controls.Add(fileInfoPanel, 0, 3);
            layout.Controls.Add(new Panel(), 0, 4);
            layout.Controls.Add(btnPanel, 0, 5);

            mainPanel.Controls.Add(layout);
            this.Controls.Add(mainPanel);
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

                        lblCurrentDbInfo.Text = $"📊 دیتابیس فعلی: «{_databaseName}» | " +
                                                $"👥 پرسنل: {NumberHelper.FormatThousands(empCount, usePersianDigits: true)} نفر | " +
                                                $"👤 کاربران: {NumberHelper.FormatThousands(userCount, usePersianDigits: true)} نفر | " +
                                                $"💰 فیش حقوقی: {NumberHelper.FormatThousands(payrollCount, usePersianDigits: true)} ردیف";
                    }
                }, "در حال خواندن اطلاعات دیتابیس...");

                if (!success && !IsDisposed)
                {
                    lblCurrentDbInfo.Text = $"⚠️ خطا در خواندن اطلاعات دیتابیس (جزئیات در فایل لاگ ثبت شد).";
                    lblCurrentDbInfo.ForeColor = UIHelper.WarmDanger;
                }
            }
            catch (ObjectDisposedException) { /* نادیده بگیر */ }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "DatabaseRestoreForm.LoadCurrentDatabaseInfoAsync");
                if (!IsDisposed)
                {
                    lblCurrentDbInfo.Text = $"⚠️ خطا: {ex.Message}";
                    lblCurrentDbInfo.ForeColor = UIHelper.WarmDanger;
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

                    lblBackupFileInfo.Text = $"📁 فایل: {fileInfo.Name}\n" +
                                             $"📦 حجم فایل: {formattedSize} مگابایت\n" +
                                             $"📅 تاریخ فایل: {formattedDate}";
                    lblBackupFileInfo.ForeColor = UIHelper.WarmText;
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "DatabaseRestoreForm.BrowseFile");
                    lblBackupFileInfo.Text = $"⚠️ خطا در خواندن اطلاعات فایل.";
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

                var result = MessageBox.Show(
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
                try { _actionLock.Release(); } catch { }
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
}