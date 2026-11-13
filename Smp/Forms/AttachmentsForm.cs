using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;

namespace SMP.Forms
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
            ShortcutHelper.ApplyStandardShortcuts(this);

            // ✅ تغییر به Async برای لود اولیه
            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void BuildUI(string formTitle)
        {
            var (topRightPanel, topLeftPanel, grid, bottomPanel) = StandardFormHelper.SetupStandardListLayout(
                this, $"پیوست‌های {formTitle}", new Size(800, 500));

            dgvAttachments = grid;
            dgvAttachments.AutoGenerateColumns = false;

            dgvAttachments.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            dgvAttachments.Columns.Add(new DataGridViewTextBoxColumn { Name = "FileName", HeaderText = "نام فایل", FillWeight = 50 });
            dgvAttachments.Columns.Add(new DataGridViewTextBoxColumn { Name = "FileSize", HeaderText = "حجم (KB)", FillWeight = 15 });
            dgvAttachments.Columns.Add(new DataGridViewTextBoxColumn { Name = "UploadedAt", HeaderText = "تاریخ آپلود", FillWeight = 20 });

            dgvAttachments.SelectionChanged += (s, e) => UpdateButtonsState();
            dgvAttachments.CellDoubleClick += async (s, e) => await BtnDownload_ClickAsync();
            dgvAttachments.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Delete) { e.Handled = true; await BtnDelete_ClickAsync(); } };

            btnUpload = UIHelper.CreateButton("📤 آپلود فایل جدید", UIHelper.WarmPrimary, 180, 40);
            btnUpload.Click += async (s, e) => await BtnUpload_ClickAsync();
            topRightPanel.Controls.Add(btnUpload);

            btnDownload = UIHelper.CreateButton("💾 دانلود / ذخیره فایل", UIHelper.ActionGreen, 200, 40);
            btnDownload.Click += async (s, e) => await BtnDownload_ClickAsync();

            btnDelete = UIHelper.CreateButton("🗑️ حذف فایل", UIHelper.WarmDanger, 150, 40);
            btnDelete.Click += async (s, e) => await BtnDelete_ClickAsync();

            bottomPanel.Controls.Add(btnDownload);
            bottomPanel.Controls.Add(btnDelete);
        }

        // ✅ تغییر نام و تبدیل به Async
        private async Task LoadDataSafeAsync()
        {
            try
            {
                var list = await new AttachmentRepository(_connectionString).GetByEntityAsync(_entityType, _entityId);
                dgvAttachments.Rows.Clear();
                foreach (var a in list)
                {
                    dgvAttachments.Rows.Add(a.Id, a.FileName, NumberHelper.FormatThousands(a.FileSize / 1024), DateHelper.ToPersianDate(a.UploadedAt));
                }
                UpdateButtonsState();
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, nameof(AttachmentsForm));
                ModernMessageBox.ShowError("خطا در بارگذاری پیوست‌ها:\n" + ex.Message);
            }
        }

        private void UpdateButtonsState()
        {
            bool hasRow = dgvAttachments.CurrentRow != null;
            if (btnDownload != null) btnDownload.Enabled = hasRow;
            if (btnDelete != null) btnDelete.Enabled = hasRow;
        }

        private async Task BtnUpload_ClickAsync()
        {
            using var ofd = new OpenFileDialog { Title = "انتخاب فایل برای پیوست", Filter = "همه فایل‌ها|*.*", CheckFileExists = true };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            FileInfo fi;
            try { fi = new FileInfo(ofd.FileName); }
            catch (Exception ex) { ModernMessageBox.ShowError("دسترسی به فایل امکان‌پذیر نیست:\n" + ex.Message); return; }

            if (fi.Length > MaxFileSizeBytes) { ModernMessageBox.ShowWarning("حجم فایل نباید بیشتر از ۲۰ مگابایت باشد."); return; }

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                byte[] data = await File.ReadAllBytesAsync(ofd.FileName);
                var attachment = new Attachment { EntityType = _entityType, EntityId = _entityId, FileName = fi.Name, FileData = data, FileSize = fi.Length };
                DateStampHelper.StampCreated(attachment);
                // ✅ تغییر به متد Async
                await new AttachmentRepository(_connectionString).AddAsync(attachment);
            }, "در حال آپلود فایل...");

            if (!success) return;
            await LoadDataSafeAsync();
            ToastHelper.Show("فایل با موفقیت پیوست شد.", isSuccess: true);
        }

        private async Task BtnDownload_ClickAsync()
        {
            if (!TryGetCurrent(out int id, out string fileName)) return;

            using var sfd = new SaveFileDialog { FileName = fileName, Title = "ذخیره فایل پیوست", OverwritePrompt = true };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new AttachmentRepository(_connectionString);
                // ✅ تغییر به متد Async
                byte[]? data = await repo.GetFileDataAsync(id);
                if (data == null || data.Length == 0) throw new InvalidOperationException("محتوای فایل در سیستم یافت نشد.");
                await File.WriteAllBytesAsync(sfd.FileName, data);
            }, "در حال دانلود فایل...");

            if (!success) return;

            if (ModernMessageBox.AskConfirm("فایل با موفقیت ذخیره شد.\nآیا می‌خواهید آن را باز کنید؟", "باز کردن فایل"))
            {
                try { Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true }); }
                catch (Exception ex) { ModernMessageBox.ShowWarning("فایل ذخیره شد اما باز کردن آن ناموفق بود:\n" + ex.Message); }
            }
        }

        private async Task BtnDelete_ClickAsync()
        {
            if (!TryGetCurrent(out int id, out string fileName)) return;
            if (!ModernMessageBox.AskConfirm($"آیا از حذف فایل «{fileName}» اطمینان دارید؟", "حذف فایل")) return;

            try
            {
                // ✅ تغییر به متد Async
                await new AttachmentRepository(_connectionString).DeleteAsync(id);
                await LoadDataSafeAsync();
                ToastHelper.Show("فایل حذف شد.", isSuccess: true);
            }
            catch (Exception ex) { ModernMessageBox.ShowError("خطا در حذف فایل:\n" + ex.Message); }
        }

        private bool TryGetCurrent(out int id, out string fileName)
        {
            id = 0; fileName = "";
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