using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class YearEndBonusForm : Form
    {
        private readonly string _connectionString;
        private readonly EmployeeRepository _empRepository;
        private readonly SalarySettingsRepository _settingsRepository;
        private readonly YearEndBonusRepository _bonusRepository;
        private readonly YearEndBonusService _bonusService;

        private ComboBox cmbYear = null!;
        private Button btnCalculate = null!, btnSave = null!;
        private DataGridView dgvResults = null!;
        private List<YearEndBonusRecord>? _currentResults;

        private readonly SemaphoreSlim _actionLock = new(1, 1);

        // ✅ FIX: نگه‌داری سال قبلی برای بازگردانی در صورت انصراف
        private int _lastSelectedYear = 0;
        private bool _suppressYearChange = false;

        public YearEndBonusForm(string connectionString)
        {
            _connectionString = connectionString;
            _empRepository = new EmployeeRepository(connectionString);
            _settingsRepository = new SalarySettingsRepository(connectionString);
            _bonusRepository = new YearEndBonusRepository(connectionString);
            _bonusService = new YearEndBonusService();

            BuildUI();

            this.ApplyStandardHeader("محاسبه عیدی و پاداش پایان سال", new Size(1000, 600));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            LoadDefaults();

            // ✅ FIX: هندل کردن تغییر سال با هشدار از دست دادن نتایج
            cmbYear.SelectedIndexChanged += CmbYear_SelectedIndexChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void LoadDefaults()
        {
            var pc = new PersianCalendar();
            int currentYear = pc.GetYear(DateTime.Now);

            if (cmbYear.Items.Contains(currentYear)) cmbYear.SelectedItem = currentYear;
            else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;

            _lastSelectedYear = cmbYear.SelectedItem != null ? (int)cmbYear.SelectedItem : 0;
        }

        // ✅ FIX: با تغییر سال، نتایج قبلی پاک می‌شوند و Save غیرفعال می‌شود
        private void CmbYear_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || _suppressYearChange) return;
            if (cmbYear.SelectedItem == null) return;

            int newYear = (int)cmbYear.SelectedItem;
            if (newYear == _lastSelectedYear) return;

            // اگر نتایج ذخیره‌نشده وجود دارد، از کاربر تأیید بگیر
            if (_currentResults != null && _currentResults.Count > 0)
            {
                var result = ModernMessageBox.AskConfirm(
                    "سال مالی را تغییر می‌دهید.\n\n" +
                    "نتایج محاسبه‌شده فعلی (که هنوز در دیتابیس ذخیره نشده‌اند) پاک خواهند شد.\n\n" +
                    "آیا ادامه می‌دهید؟",
                    "تغییر سال مالی");
                if (!result)
                {
                    // بازگردانی به سال قبلی بدون فعال کردن مجدد هندلر
                    _suppressYearChange = true;
                    try { cmbYear.SelectedItem = _lastSelectedYear; }
                    finally { _suppressYearChange = false; }
                    return;
                }
            }

            // پاک‌سازی نتایج و غیرفعال کردن Save
            _lastSelectedYear = newYear;
            _currentResults = null;
            dgvResults.DataSource = null;
            btnSave.Enabled = false;
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

            btnCalculate = UIHelper.CreateButton("محاسبه عیدی همه پرسنل", UIHelper.ActionGreen, 200, 35);
            btnCalculate.Margin = new Padding(5, 3, 15, 5);
            btnCalculate.Click += async (s, e) => await CalculateSafeAsync();

            btnSave = UIHelper.CreateButton("💾 ذخیره در دیتابیس", UIHelper.SectionBlue, 160, 35);
            btnSave.Margin = new Padding(5, 3, 5, 5);
            btnSave.Enabled = false;
            btnSave.Click += async (s, e) => await SaveSafeAsync();

            topFlow.Controls.Add(new Label { Text = "سال مالی:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbYear);
            topFlow.Controls.Add(btnCalculate);
            topFlow.Controls.Add(btnSave);

            topPanel.Controls.Add(topFlow);

            dgvResults = new DataGridView { Dock = DockStyle.Fill };
            dgvResults.ApplyModernStyle("EmployeeId", "Year");

            dgvResults.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                if (e.Value is decimal dec)
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

            this.Controls.Add(dgvResults);
            this.Controls.Add(topPanel);
        }

        private async Task CalculateSafeAsync()
        {
            if (cmbYear.SelectedItem == null || IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                int year = Convert.ToInt32(cmbYear.SelectedItem);
                btnSave.Enabled = false;

                // ✅ اگر نتایج قبلی برای سال دیگری وجود دارد، پاک شود
                if (_currentResults != null && _currentResults.Count > 0 && _currentResults[0].Year != year)
                {
                    _currentResults = null;
                    dgvResults.DataSource = null;
                }

                List<YearEndBonusRecord>? calculatedResults = null;
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var settings = await _settingsRepository.GetByYearAsync(year);
                    if (settings == null)
                        throw new InvalidOperationException($"تنظیمات حقوق برای سال {year} یافت نشد.");

                    var employees = await _empRepository.GetEmployeesWithDetailsAsync("", false);

                    calculatedResults = await Task.Run(() =>
                    {
                        var list = new List<YearEndBonusRecord>();
                        foreach (var emp in employees)
                        {
                            var result = _bonusService.CalculateBonus(emp, year, settings);
                            if (result.MonthsWorked > 0)
                            {
                                list.Add(result);
                            }
                        }
                        return list;
                    });
                }, "در حال محاسبه عیدی و پاداش...");

                if (!success || calculatedResults == null || IsDisposed) return;

                _currentResults = calculatedResults;
                _lastSelectedYear = year;
                dgvResults.DataSource = null;
                dgvResults.DataSource = _currentResults;

                if (dgvResults.Columns.Contains("NetBonus")) dgvResults.Columns["NetBonus"].HeaderText = "خالص عیدی";
                if (dgvResults.Columns.Contains("BonusAmount")) dgvResults.Columns["BonusAmount"].HeaderText = "مبلغ عیدی";

                btnSave.Enabled = true;
                ToastHelper.Show("محاسبات انجام شد. حالا می‌توانید ذخیره کنید.", isSuccess: true);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "YearEndBonusForm.CalculateSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در محاسبه:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private async Task SaveSafeAsync()
        {
            if (_currentResults == null || _currentResults.Count == 0 || IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                // ✅ اطمینان از تطابق سال نتایج با سال انتخاب‌شده
                if (cmbYear.SelectedItem == null) return;
                int selectedYear = Convert.ToInt32(cmbYear.SelectedItem);
                if (_currentResults.Any(r => r.Year != selectedYear))
                {
                    ModernMessageBox.ShowWarning(
                        "نتایج محاسبه‌شده با سال انتخاب‌شده تطابق ندارد.\n" +
                        "لطفاً ابتدا برای این سال «محاسبه» را انجام دهید.");
                    return;
                }

                if (!ModernMessageBox.AskConfirm("آیا از ذخیره عیدی و پاداش برای این پرسنل مطمئن هستید؟", "ذخیره"))
                    return;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    await _bonusRepository.SaveBonusesBatchAsync(_currentResults);
                }, "در حال ذخیره در دیتابیس...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("عیدی و پاداش پایان سال با موفقیت ذخیره شد.", isSuccess: true);
                    btnSave.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "YearEndBonusForm.SaveSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}