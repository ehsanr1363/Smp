using PersonnelApp.Data;
using PersonnelApp.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class BankPaymentForm : Form
    {
        private readonly PayrollRepository _repository;

        private ComboBox cmbYear = null!;
        private ComboBox cmbMonth = null!;
        private Button btnLoad = null!;
        private Button btnExport = null!;
        private DataGridView dgvBankList = null!;
        private Label lblCount = null!;

        private DataTable? _currentData;

        public BankPaymentForm(string connectionString)
        {
            _repository = new PayrollRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("فایل واریز حقوق به بانک", new Size(900, 600));
            ShowInTaskbar = false;

            LoadDefaults();
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnLoad); // Ctrl+S → Load
        }

        // ==========================================================
        // 🎯 مقدار پیش‌فرض (سال و ماه جاری شمسی)
        // ==========================================================
        private void LoadDefaults()
        {
            try
            {
                var pc = new PersianCalendar();
                int y = pc.GetYear(DateTime.Now);
                int m = pc.GetMonth(DateTime.Now);

                if (cmbYear.Items.Contains(y)) cmbYear.SelectedItem = y;
                else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;

                if (cmbMonth.Items.Contains(m)) cmbMonth.SelectedItem = m;
                else if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
            }
            catch
            {
                if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;
                if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
            }
        }

        // ==========================================================
        // 🎨 رابط کاربری
        // ==========================================================
        private void BuildUI()
        {
            // ---- پنل بالا ----
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };

            var topFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(5)
            };

            cmbYear = new ComboBox
            {
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UIHelper.PersianFont(11f),
                Margin = new Padding(5, 8, 15, 5)
            };
            for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);

            cmbMonth = new ComboBox
            {
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UIHelper.PersianFont(11f),
                Margin = new Padding(5, 8, 15, 5)
            };
            for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);

            btnLoad = UIHelper.CreateButton("نمایش لیست", UIHelper.SectionBlue, 130, 35);
            btnLoad.Margin = new Padding(5, 3, 5, 5);
            btnLoad.Click += BtnLoad_Click;

            btnExport = UIHelper.CreateButton("📥 خروجی اکسل بانک", UIHelper.ActionGreen, 190, 35);
            btnExport.Margin = new Padding(5, 3, 5, 5);
            btnExport.Enabled = false;
            btnExport.Click += BtnExport_Click;

            topFlow.Controls.Add(new Label
            {
                Text = "سال:",
                AutoSize = true,
                Font = UIHelper.PersianFont(11f),
                Margin = new Padding(5, 13, 5, 5)
            });
            topFlow.Controls.Add(cmbYear);
            topFlow.Controls.Add(new Label
            {
                Text = "ماه:",
                AutoSize = true,
                Font = UIHelper.PersianFont(11f),
                Margin = new Padding(5, 13, 5, 5)
            });
            topFlow.Controls.Add(cmbMonth);
            topFlow.Controls.Add(btnLoad);
            topFlow.Controls.Add(btnExport);

            topPanel.Controls.Add(topFlow);

            // ---- گرید ----
            dgvBankList = new DataGridView { Dock = DockStyle.Fill };
            dgvBankList.ApplyModernStyle(); // + Enter-Nav، ترجمه هدرها

            // ---- پنل پایین (شمارشگر) ----
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(10) };

            lblCount = new Label
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = UIHelper.PersianFont(9.5f),
                Padding = new Padding(5, 6, 0, 0)
            };
            bottomPanel.Controls.Add(lblCount);

            Controls.Add(dgvBankList);
            Controls.Add(topPanel);
            Controls.Add(bottomPanel);
        }

        // ==========================================================
        // 📥 بارگذاری
        // ==========================================================
        private async void BtnLoad_Click(object? sender, EventArgs e)
        {
            if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null)
            {
                ModernMessageBox.ShowWarning("لطفاً سال و ماه را انتخاب کنید.");
                return;
            }

            int year = Convert.ToInt32(cmbYear.SelectedItem);
            int month = Convert.ToInt32(cmbMonth.SelectedItem);

            DataTable? data = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                data = await _repository.GetBankPaymentListAsync(year, month);
            }, "در حال بارگذاری لیست واریز...");

            if (!success) return;

            _currentData = data;

            if (_currentData == null || _currentData.Rows.Count == 0)
            {
                dgvBankList.DataSource = null;
                btnExport.Enabled = false;
                lblCount.Text = "هیچ رکوردی یافت نشد.";
                ModernMessageBox.ShowWarning("برای این ماه هیچ فیش حقوقی ذخیره نشده است.");
                return;
            }

            dgvBankList.DataSource = _currentData;
            btnExport.Enabled = true;

            int count = _currentData.Rows.Count;
            lblCount.Text = $"{NumberHelper.FormatThousands(count)} رکورد";
            ToastHelper.Show(
                $"{NumberHelper.FormatThousands(count)} رکورد بارگذاری شد.",
                isSuccess: true);
        }

        // ==========================================================
        // 📤 خروجی اکسل (روی thread جداگانه)
        // ==========================================================
        private async void BtnExport_Click(object? sender, EventArgs e)
        {
            if (_currentData == null || _currentData.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("ابتدا لیست را بارگذاری کنید.");
                return;
            }

            // نام فایل ایمن (اگر SelectedItem خالی بود، پیش‌فرض)
            string yearStr = cmbYear.SelectedItem?.ToString() ?? "0000";
            string monthStr = cmbMonth.SelectedItem?.ToString() ?? "00";

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = $"BankPayment_{yearStr}_{monthStr}.xlsx",
                Title = "ذخیره فایل واریز بانک",
                OverwritePrompt = true
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                // ExportDataGridViewToExcel هم UI را لمس می‌کند هم I/O می‌کند.
                // برای سادگی و امنیت، کل کار را روی UI thread نگه می‌داریم
                // ولی با overlay تا کاربر ببیند چه اتفاقی می‌افتد.
                await Task.Yield(); // اجازه بده overlay رسم شود
                ExcelExportHelper.ExportDataGridViewToExcel(dgvBankList, sfd.FileName, "واریز حقوق");
            }, "در حال تولید فایل اکسل...");

            if (!success) return;

            ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true);

            // پرسش باز کردن فایل
            if (ModernMessageBox.AskConfirm(
                "فایل اکسل ذخیره شد.\nآیا می‌خواهید آن را باز کنید؟", "باز کردن فایل"))
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "BankPaymentForm.OpenFile");
                    ModernMessageBox.ShowWarning("فایل ذخیره شد اما باز کردن آن ناموفق بود:\n" + ex.Message);
                }
            }
        }
    }
}