using SMP.Data;
using SMP.Helpers;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class AuditLogForm : Form
    {
        private readonly AuditLogRepository _repository;

        private DataGridView dgvLogs = null!;
        private Button btnRefresh = null!;
        private Label lblCount = null!;

        // ✅ FIX P2: جلوگیری از تداخل
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public AuditLogForm(string connectionString)
        {
            _repository = new AuditLogRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("مشاهده لاگ تغییرات سیستم", new Size(1000, 600));
            ShowInTaskbar = false;

            // Esc → بستن، Ctrl+S → بروزرسانی
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);

            // بارگذاری بعد از نمایش فرم
            Shown += async (s, e) => await LoadLogsAsync();
        }

        // ✅ FIX P2: آزادسازی SemaphoreSlim
        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _loadLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            dgvLogs = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = true
            };
            dgvLogs.ApplyModernStyle();

            dgvLogs.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (!string.Equals(dgvLogs.Columns[e.ColumnIndex].Name, "LogDate", StringComparison.Ordinal))
                    return;

                if (e.Value is DateTime dt)
                {
                    e.Value = DateHelper.ToPersianDateTime(dt);
                    e.FormattingApplied = true;
                }
            };

            // ---- پنل پایین ----
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(10) };

            btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی لاگ‌ها", UIHelper.SectionBlue, 200, 40);
            btnRefresh.Dock = DockStyle.Right;
            btnRefresh.Click += async (s, e) => await LoadLogsAsync();
            bottomPanel.Controls.Add(btnRefresh);

            lblCount = new Label
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = UIHelper.PersianFont(9.5f),
                Padding = new Padding(5, 12, 0, 0)
            };
            bottomPanel.Controls.Add(lblCount);

            Controls.Add(dgvLogs);
            Controls.Add(bottomPanel);
        }

        private async Task LoadLogsAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return; // ✅ FIX: جلوگیری از بارگذاری همزمان

            try
            {
                var (success, logs) = await AsyncHelper.RunWithLoading(
                    this,
                    () => _repository.GetAllLogsAsync(),
                    "در حال بارگذاری لاگ‌ها...");

                // ✅ FIX: بررسی IsDisposed بعد از await
                if (!success || logs == null || IsDisposed) return;

                dgvLogs.DataSource = logs;
                int count = dgvLogs.Rows.Count;

                lblCount.Text = count == 0
                    ? "لاگی یافت نشد."
                    : $"{NumberHelper.FormatThousands(count, usePersianDigits: true)} رکورد";

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
                try { _loadLock.Release(); } catch { }
            }
        }
    }
}