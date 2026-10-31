using PersonnelApp.Data;
using PersonnelApp.Helpers;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class AuditLogForm : Form
    {
        private readonly AuditLogRepository _repository;

        private DataGridView dgvLogs = null!;
        private Button btnRefresh = null!;
        private Label lblCount = null!;

        public AuditLogForm(string connectionString)
        {
            _repository = new AuditLogRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("مشاهده لاگ تغییرات سیستم", new Size(1000, 600));
            ShowInTaskbar = false;

            // Esc → بستن، Ctrl+S → بروزرسانی
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);

            // بارگذاری بعد از نمایش فرم (تا overlay جای درست بیفتد)
            Shown += async (s, e) => await LoadLogsAsync();
        }

        // ==========================================================
        // 🎨 رابط کاربری
        // ==========================================================
        private void BuildUI()
        {
            dgvLogs = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = true
            };
            dgvLogs.ApplyModernStyle();   // + Enter-Nav، ترجمه هدرها، ReadOnly

            // نمایش تاریخ به شمسی (به‌جای فرمت میلادی پیش‌فرض)
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

        // ==========================================================
        // 📥 بارگذاری
        // ==========================================================
        private async Task LoadLogsAsync()
        {
            var (success, logs) = await AsyncHelper.RunWithLoading(
                this,
                () => _repository.GetAllLogsAsync(),
                "در حال بارگذاری لاگ‌ها...");

            if (!success) return;

            dgvLogs.DataSource = logs;
            int count = dgvLogs.Rows.Count;

            lblCount.Text = count == 0
                ? "لاگی یافت نشد."
                : $"{NumberHelper.FormatThousands(count)} رکورد";

            ToastHelper.Show(
                count == 0
                    ? "لاگی یافت نشد."
                    : $"{NumberHelper.FormatThousands(count)} لاگ بارگذاری شد.",
                isSuccess: count > 0);
        }
    }
}