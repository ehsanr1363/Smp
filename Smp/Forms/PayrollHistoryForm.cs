using SMP.Data;
using SMP.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class PayrollHistoryForm : Form
    {
        private readonly PayrollRepository _repository;

        private ComboBox cmbYear = null!, cmbMonth = null!;
        private Button btnLoad = null!, btnExport = null!, btnDeleteRow = null!, btnDeleteMonth = null!;
        private DataGridView dgvHistory = null!;
        private DataTable? _currentData;

        // ✅ FIX P2: جلوگیری از تداخل و کرش
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public PayrollHistoryForm(string connectionString)
        {
            _repository = new PayrollRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("تاریخچه و گزارش حقوق ماهانه", new Size(1000, 600));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnExport);

            LoadDefaults();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _loadLock?.Dispose(); } catch { }
                try { _actionLock?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void LoadDefaults()
        {
            var pc = new PersianCalendar();
            int currentYear = pc.GetYear(DateTime.Now);
            int currentMonth = pc.GetMonth(DateTime.Now);

            if (cmbYear.Items.Contains(currentYear)) cmbYear.SelectedItem = currentYear;
            else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;

            if (cmbMonth.Items.Contains(currentMonth)) cmbMonth.SelectedItem = currentMonth;
            else if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
        }

        private void BuildUI()
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };

            var topFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            cmbYear = new ComboBox
            {
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UIHelper.PersianFont(11f),
                Margin = new Padding(5, 8, 15, 5)
            };
            for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);

            cmbMonth = new ComboBox
            {
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UIHelper.PersianFont(11f),
                Margin = new Padding(5, 8, 15, 5)
            };
            for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);

            btnLoad = UIHelper.CreateButton("نمایش لیست", UIHelper.SectionBlue, 120, 35);
            btnLoad.Margin = new Padding(5, 3, 15, 5);
            btnLoad.Click += async (s, e) => await LoadDataSafeAsync();

            btnExport = UIHelper.CreateButton("📥 خروجی اکسل", UIHelper.ActionGreen, 160, 35);
            btnExport.Margin = new Padding(5, 3, 15, 5);
            btnExport.Enabled = false;
            btnExport.Click += async (s, e) => await ExportDataSafeAsync();

            btnDeleteMonth = UIHelper.CreateButton("🗑️ حذف فیش‌های این ماه", UIHelper.ActionRed, 180, 35);
            btnDeleteMonth.Margin = new Padding(5, 3, 5, 5);
            btnDeleteMonth.Enabled = false;
            btnDeleteMonth.Click += async (s, e) => await BtnDeleteMonth_Click(s, e);

            topFlow.Controls.Add(new Label { Text = "سال:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbYear);
            topFlow.Controls.Add(new Label { Text = "ماه:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbMonth);
            topFlow.Controls.Add(btnLoad);
            topFlow.Controls.Add(btnExport);
            topFlow.Controls.Add(btnDeleteMonth);

            topPanel.Controls.Add(topFlow);

            dgvHistory = new DataGridView { Dock = DockStyle.Fill };
            dgvHistory.ApplyModernStyle();
            dgvHistory.SelectionChanged += (s, e) => btnDeleteRow.Enabled = dgvHistory.CurrentRow != null;

            dgvHistory.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                string colName = dgvHistory.Columns[e.ColumnIndex].Name;

                if (e.Value is DateTime dt)
                {
                    e.Value = DateHelper.ToPersianDate(dt);
                    e.FormattingApplied = true;
                }
                else if (e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
                else if (e.Value is int i)
                {
                    e.Value = NumberHelper.FormatThousands(i, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            };

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(10) };

            btnDeleteRow = UIHelper.CreateButton("🗑️ حذف فیش انتخاب‌شده", UIHelper.ActionRed, 200, 40);
            btnDeleteRow.Dock = DockStyle.Left;
            btnDeleteRow.Enabled = false;
            btnDeleteRow.Click += async (s, e) => await BtnDeleteRow_Click(s, e);

            bottomPanel.Controls.Add(btnDeleteRow);

            this.Controls.Add(dgvHistory);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(topPanel);
        }

        private async Task LoadDataSafeAsync()
        {
            if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null || IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                int year = Convert.ToInt32(cmbYear.SelectedItem);
                int month = Convert.ToInt32(cmbMonth.SelectedItem);

                DataTable? result = null;
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    result = await _repository.GetSavedPayrollsAsync(year, month);
                }, "در حال بارگذاری لیست حقوق...");

                if (!success || result == null || IsDisposed) return;

                _currentData = result;

                if (_currentData.Rows.Count == 0)
                {
                    ModernMessageBox.ShowWarning("برای این ماه هیچ فیش حقوقی ذخیره نشده است.");
                    btnExport.Enabled = false;
                    btnDeleteMonth.Enabled = false;
                    dgvHistory.DataSource = null;
                }
                else
                {
                    dgvHistory.DataSource = _currentData;
                    btnExport.Enabled = true;
                    btnDeleteMonth.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PayrollHistoryForm.LoadDataSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }

        private async Task ExportDataSafeAsync()
        {
            if (_currentData == null || _currentData.Rows.Count == 0 || IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    FileName = $"Payroll_{cmbYear.SelectedItem}_{cmbMonth.SelectedItem}.xlsx",
                    Title = "ذخیره فایل اکسل حقوق",
                    OverwritePrompt = true
                };

                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    await Task.Yield();
                    ExcelExportHelper.ExportDataGridViewToExcel(dgvHistory, sfd.FileName, "لیست حقوق");
                }, "در حال تولید فایل اکسل...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PayrollHistoryForm.ExportDataSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private async Task BtnDeleteRow_Click(object? sender, EventArgs e)
        {
            if (dgvHistory.CurrentRow == null || IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                var payrollId = Convert.ToInt32(dgvHistory.CurrentRow.Cells["Id"].Value);
                string empName = dgvHistory.CurrentRow.Cells["EmployeeName"].Value?.ToString() ?? "";

                if (!ModernMessageBox.AskConfirm(
                    $"آیا از حذف فیش حقوقی «{empName}» مطمئن هستید؟\n\n" +
                    "توجه: با حذف این فیش، مبالغ کسر شده بابت وام و بیمه مجدداً به مانده بدهی کارمند اضافه می‌شوند.",
                    "حذف فیش حقوقی"))
                    return;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    await _repository.DeletePayrollAsync(payrollId);
                }, "در حال حذف فیش...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("فیش حقوقی با موفقیت حذف شد.", isSuccess: true);
                    await LoadDataSafeAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PayrollHistoryForm.BtnDeleteRow_Click");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private async Task BtnDeleteMonth_Click(object? sender, EventArgs e)
        {
            if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null || IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                int year = Convert.ToInt32(cmbYear.SelectedItem);
                int month = Convert.ToInt32(cmbMonth.SelectedItem);
                string monthName = TranslationHelper.GetMonthName(month);

                if (!ModernMessageBox.AskConfirm(
                    $"⚠️ هشدار جدی!\n\n" +
                    $"آیا از حذف کلیه فیش‌های حقوقی {monthName} {year} مطمئن هستید؟\n\n" +
                    "توجه: با حذف این فیش‌ها، مبالغ کسر شده بابت وام و بیمه مجدداً به مانده بدهی کارمندان اضافه می‌شوند.",
                    "حذف گروهی فیش‌ها"))
                    return;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    await _repository.DeletePayrollsForMonthAsync(year, month);
                }, "در حال حذف گروهی فیش‌ها...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("تمام فیش‌های این ماه با موفقیت حذف شدند.", isSuccess: true);
                    await LoadDataSafeAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PayrollHistoryForm.BtnDeleteMonth_Click");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}