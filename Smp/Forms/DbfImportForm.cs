using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Helpers;

namespace SMP.Forms
{
    public class DbfImportForm : Form
    {
        private TextBox txtHeaderPath = null!;
        private TextBox txtDetailsPath = null!;
        private Button btnBrowseHeader = null!, btnBrowseDetails = null!;
        private Button btnImport = null!;
        private Label lblStatus = null!;

        // ✅ FIX P2: جلوگیری از تداخل
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public DbfImportForm()
        {
            BuildUI();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, "ایمپورت اطلاعات بیمه (فایل‌های DBF)", new Size(750, 400));
            ShowInTaskbar = false;

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(20) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            var pnlHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));
            pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            txtHeaderPath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = UIHelper.PersianFont(11f), BackColor = Color.White };
            btnBrowseHeader = UIHelper.CreateButton("📁 انتخاب فایل ۱ (هدر)", UIHelper.SectionBlue, 150, 35);
            btnBrowseHeader.Dock = DockStyle.Fill;
            btnBrowseHeader.Click += (s, e) => BrowseFile(txtHeaderPath);
            pnlHeader.Controls.Add(txtHeaderPath, 0, 0);
            pnlHeader.Controls.Add(btnBrowseHeader, 1, 0);

            var pnlDetails = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            pnlDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));
            pnlDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            txtDetailsPath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = UIHelper.PersianFont(11f), BackColor = Color.White };
            btnBrowseDetails = UIHelper.CreateButton("📁 انتخاب فایل ۲ (جزئیات)", UIHelper.SectionBlue, 150, 35);
            btnBrowseDetails.Dock = DockStyle.Fill;
            btnBrowseDetails.Click += (s, e) => BrowseFile(txtDetailsPath);
            pnlDetails.Controls.Add(txtDetailsPath, 0, 0);
            pnlDetails.Controls.Add(btnBrowseDetails, 1, 0);

            var lblHint = new Label
            {
                Text = "💡 راهنما: ابتدا فایل هدر (DSK) و سپس فایل جزئیات (DSW) را انتخاب کنید.\nتوجه: قبل از ایمپورت، مطمئن شوید کارگاه‌ها و کدهای ملی پرسنل در سیستم ثبت شده باشند.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UIHelper.PersianFont(10f),
                ForeColor = Color.Gray
            };

            lblStatus = new Label
            {
                Text = "آماده برای ایمپورت...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText
            };

            btnImport = UIHelper.CreateButton("📥 شروع ایمپورت به دیتابیس", UIHelper.ActionGreen, 250, 45);
            btnImport.Dock = DockStyle.Right;
            btnImport.Click += async (s, e) => await BtnImport_Click(s, e);

            var pnlFooter = new Panel { Dock = DockStyle.Fill };
            pnlFooter.Controls.Add(btnImport);

            mainLayout.Controls.Add(pnlHeader, 0, 0);
            mainLayout.Controls.Add(pnlDetails, 0, 1);
            mainLayout.Controls.Add(lblHint, 0, 2);
            mainLayout.Controls.Add(lblStatus, 0, 3);
            mainLayout.Controls.Add(pnlFooter, 0, 4);

            this.Controls.Add(mainLayout);
        }

        private void BrowseFile(TextBox targetTextBox)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "DBF Files|*.dbf|All Files|*.*",
                Title = "انتخاب فایل DBF"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                targetTextBox.Text = ofd.FileName;
            }
        }

        private async Task BtnImport_Click(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (string.IsNullOrWhiteSpace(txtHeaderPath.Text) || !File.Exists(txtHeaderPath.Text))
                {
                    ModernMessageBox.ShowWarning("لطفاً فایل هدر (فایل ۱) را انتخاب کنید.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtDetailsPath.Text) || !File.Exists(txtDetailsPath.Text))
                {
                    ModernMessageBox.ShowWarning("لطفاً فایل جزئیات (فایل ۲) را انتخاب کنید.");
                    return;
                }

                btnImport.Enabled = false;
                lblStatus.Text = "در حال پردازش فایل‌ها... لطفاً صبر کنید.";
                lblStatus.ForeColor = UIHelper.WarmWarningText;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    await Task.Run(() =>
                    {
                        var result = DbfImportHelper.ImportCombined(AppConfig.ConnectionString, txtHeaderPath.Text, txtDetailsPath.Text);

                        string msg = $"✅ ایمپورت با موفقیت انجام شد!\n\n" +
                                     $"📥 هدرهای جدید: {NumberHelper.FormatThousands(result.HeaderInsertCount, usePersianDigits: true)}\n" +
                                     $"🔄 هدرهای آپدیت شده: {NumberHelper.FormatThousands(result.HeaderUpdateCount, usePersianDigits: true)}\n" +
                                     $"📥 جزئیات جدید: {NumberHelper.FormatThousands(result.DetailsInsertCount, usePersianDigits: true)}\n" +
                                     $"🔄 جزئیات آپدیت شده: {NumberHelper.FormatThousands(result.DetailsUpdateCount, usePersianDigits: true)}\n";

                        if (result.ErrorCount > 0 || result.MissingPersonnel.Count > 0)
                        {
                            msg += $"\n⚠️ خطاها: {result.ErrorCount}\n";
                            if (result.MissingPersonnel.Count > 0)
                            {
                                msg += $"پرسنل یافت نشده: {result.MissingPersonnel.Count} نفر\n";
                                msg += "اسامی: " + string.Join(", ", result.MissingPersonnel.Take(5)) + (result.MissingPersonnel.Count > 5 ? "..." : "");
                            }
                        }

                        // ✅ FIX: استفاده از Invoke با بررسی IsDisposed
                        if (!IsDisposed)
                        {
                            try
                            {
                                this.Invoke(new Action(() =>
                                {
                                    if (!IsDisposed) ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                                }));
                            }
                            catch (ObjectDisposedException) { /* نادیده بگیر */ }
                        }
                    });
                }, "در حال ایمپورت اطلاعات به دیتابیس...");

                if (IsDisposed) return;

                btnImport.Enabled = true;
                if (success)
                {
                    lblStatus.Text = "✅ ایمپورت با موفقیت به پایان رسید.";
                    lblStatus.ForeColor = UIHelper.ActionGreen;
                    ToastHelper.Show("ایمپورت با موفقیت انجام شد.", isSuccess: true);
                }
                else
                {
                    lblStatus.Text = "❌ خطا در ایمپورت. جزئیات در فایل لاگ ثبت شد.";
                    lblStatus.ForeColor = UIHelper.WarmDanger;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "DbfImportForm.BtnImport_Click");
                if (!IsDisposed)
                {
                    lblStatus.Text = "❌ خطا در ایمپورت. جزئیات در فایل لاگ ثبت شد.";
                    lblStatus.ForeColor = UIHelper.WarmDanger;
                }
            }
            finally
            {
                if (!IsDisposed) btnImport.Enabled = true;
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}