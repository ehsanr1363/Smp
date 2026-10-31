using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;

namespace PersonnelApp.Forms
{
    public class AttachmentsForm : Form
    {
        private const long MaxFileSizeBytes = 20L * 1024 * 1024;

        private readonly string _connectionString;
        private readonly string _entityType;
        private readonly int _entityId;

        private DataGridView dgvAttachments = null!;
        private Button btnUpload = null!;
        private Button btnDownload = null!;
        private Button btnDelete = null!;

        public AttachmentsForm(string connectionString, string entityType, int entityId, string formTitle)
        {
            _connectionString = connectionString;
            _entityType = entityType;
            _entityId = entityId;

            BuildUI(formTitle);
            ShortcutHelper.ApplyStandardShortcuts(this); // Esc → Close
            LoadDataSafe();
        }

        // ==========================================================
        // 🎨 رابط کاربری
        // ==========================================================
        private void BuildUI(string formTitle)
        {
            UIHelper.SetupForm(this, $"پیوست‌های {formTitle}", new Size(800, 500));
            ShowInTaskbar = false;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            // ---- پنل بالا: آپلود ----
            var topFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(10),
                WrapContents = false
            };
            btnUpload = UIHelper.CreateButton("📤 آپلود فایل جدید", UIHelper.WarmPrimary, 180, 40);
            btnUpload.Click += BtnUpload_Click;
            topFlow.Controls.Add(btnUpload);

            // ---- گرید فایل‌ها ----
            dgvAttachments = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            dgvAttachments.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            dgvAttachments.Columns.Add(new DataGridViewTextBoxColumn { Name = "FileName", HeaderText = "نام فایل", FillWeight = 50 });
            dgvAttachments.Columns.Add(new DataGridViewTextBoxColumn { Name = "FileSize", HeaderText = "حجم (KB)", FillWeight = 15 });
            dgvAttachments.Columns.Add(new DataGridViewTextBoxColumn { Name = "UploadedAt", HeaderText = "تاریخ آپلود", FillWeight = 20 });

            dgvAttachments.ApplyModernStyle();   // + Enter-Nav خودکار
            dgvAttachments.SelectionChanged += (s, e) => UpdateButtonsState();
            dgvAttachments.CellDoubleClick += (s, e) => BtnDownload_Click(null, EventArgs.Empty);
            dgvAttachments.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    e.Handled = true;
                    BtnDelete_Click(null, EventArgs.Empty);
                }
            };

            // ---- پنل پایین: دانلود / حذف ----
            var bottomFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(10),
                WrapContents = false
            };
            btnDownload = UIHelper.CreateButton("💾 دانلود / ذخیره فایل", UIHelper.ActionGreen, 200, 40);
            btnDownload.Click += BtnDownload_Click;

            btnDelete = UIHelper.CreateButton("🗑️ حذف فایل", UIHelper.WarmDanger, 150, 40);
            btnDelete.Click += BtnDelete_Click;

            bottomFlow.Controls.Add(btnDownload);
            bottomFlow.Controls.Add(btnDelete);

            mainLayout.Controls.Add(topFlow, 0, 0);
            mainLayout.Controls.Add(dgvAttachments, 0, 1);
            mainLayout.Controls.Add(bottomFlow, 0, 2);

            Controls.Add(mainLayout);
        }

        // ==========================================================
        // 📥 بارگذاری
        // ==========================================================
        private void LoadDataSafe()
        {
            try { LoadData(); }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, nameof(AttachmentsForm));
                ModernMessageBox.ShowError("خطا در بارگذاری پیوست‌ها:\n" + ex.Message);
            }
        }

        private void LoadData()
        {
            var list = new AttachmentRepository(_connectionString)
                .GetByEntity(_entityType, _entityId);

            dgvAttachments.Rows.Clear();
            foreach (var a in list)
            {
                dgvAttachments.Rows.Add(
                    a.Id,
                    a.FileName,
                    NumberHelper.FormatThousands(a.FileSize / 1024),
                    DateHelper.ToPersianDate(a.UploadedAt));
            }

            UpdateButtonsState();
        }

        private void UpdateButtonsState()
        {
            bool hasRow = dgvAttachments.CurrentRow != null;
            if (btnDownload != null) btnDownload.Enabled = hasRow;
            if (btnDelete != null) btnDelete.Enabled = hasRow;
        }

        // ==========================================================
        // 📤 آپلود
        // ==========================================================
        private async void BtnUpload_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "انتخاب فایل برای پیوست",
                Filter = "همه فایل‌ها|*.*",
                CheckFileExists = true
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            FileInfo fi;
            try { fi = new FileInfo(ofd.FileName); }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AttachmentsForm.Upload.FileInfo");
                ModernMessageBox.ShowError("دسترسی به فایل امکان‌پذیر نیست:\n" + ex.Message);
                return;
            }

            if (fi.Length > MaxFileSizeBytes)
            {
                ModernMessageBox.ShowWarning("حجم فایل نباید بیشتر از ۲۰ مگابایت باشد.");
                return;
            }

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                byte[] data = await File.ReadAllBytesAsync(ofd.FileName);

                var attachment = new Attachment
                {
                    EntityType = _entityType,
                    EntityId = _entityId,
                    FileName = fi.Name,
                    FileData = data,
                    FileSize = fi.Length
                };
                DateStampHelper.StampCreated(attachment); // ★ UploadedAt خودکار

                new AttachmentRepository(_connectionString).Add(attachment);
            }, "در حال آپلود فایل...");

            if (!success) return;

            LoadDataSafe();
            ToastHelper.Show("فایل با موفقیت پیوست شد.", isSuccess: true);
        }

        // ==========================================================
        // 💾 دانلود
        // ==========================================================
        private async void BtnDownload_Click(object? sender, EventArgs e)
        {
            if (!TryGetCurrent(out int id, out string fileName)) return;

            using var sfd = new SaveFileDialog
            {
                FileName = fileName,
                Title = "ذخیره فایل پیوست",
                OverwritePrompt = true
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new AttachmentRepository(_connectionString);
                byte[]? data = await Task.Run(() => repo.GetFileData(id));
                if (data == null || data.Length == 0)
                    throw new InvalidOperationException("محتوای فایل در سیستم یافت نشد.");

                await File.WriteAllBytesAsync(sfd.FileName, data);
            }, "در حال دانلود فایل...");

            if (!success) return;

            if (ModernMessageBox.AskConfirm(
                "فایل با موفقیت ذخیره شد.\nآیا می‌خواهید آن را باز کنید؟", "باز کردن فایل"))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "AttachmentsForm.OpenFile");
                    ModernMessageBox.ShowWarning("فایل ذخیره شد اما باز کردن آن ناموفق بود:\n" + ex.Message);
                }
            }
        }

        // ==========================================================
        // 🗑️ حذف
        // ==========================================================
        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (!TryGetCurrent(out int id, out string fileName)) return;

            if (!ModernMessageBox.AskConfirm($"آیا از حذف فایل «{fileName}» اطمینان دارید؟", "حذف فایل"))
                return;

            try
            {
                new AttachmentRepository(_connectionString).Delete(id);
                LoadDataSafe();
                ToastHelper.Show("فایل حذف شد.", isSuccess: true);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AttachmentsForm.Delete");
                ModernMessageBox.ShowError("خطا در حذف فایل:\n" + ex.Message);
            }
        }

        // ==========================================================
        // 🔧 کمکی
        // ==========================================================
        private bool TryGetCurrent(out int id, out string fileName)
        {
            id = 0;
            fileName = "";
            var row = dgvAttachments.CurrentRow;
            if (row == null) return false;

            var idVal = row.Cells["Id"].Value;
            if (idVal == null || idVal == DBNull.Value) return false;

            id = Convert.ToInt32(idVal);
            fileName = row.Cells["FileName"].Value?.ToString() ?? "file";
            return true;
        }
    }
}