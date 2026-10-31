using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using PersonnelApp.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
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

            // فعال‌سازی میانبرهای Ctrl+S (ذخیره) و Esc (بستن)
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            // مقداردهی اولیه سال جاری شمسی
            LoadDefaults();
        }

        private void LoadDefaults()
        {
            var pc = new PersianCalendar();
            int currentYear = pc.GetYear(DateTime.Now);

            if (cmbYear.Items.Contains(currentYear)) cmbYear.SelectedItem = currentYear;
            else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;
        }

        private void BuildUI()
        {
            // ==========================================
            // ۱. پنل بالا (فیلتر و دکمه‌ها)
            // ==========================================
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
            btnCalculate.Click += BtnCalculate_Click;

            btnSave = UIHelper.CreateButton("💾 ذخیره در دیتابیس", UIHelper.SectionBlue, 160, 35);
            btnSave.Margin = new Padding(5, 3, 5, 5);
            btnSave.Enabled = false;
            btnSave.Click += BtnSave_Click;

            topFlow.Controls.Add(new Label { Text = "سال مالی:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbYear);
            topFlow.Controls.Add(btnCalculate);
            topFlow.Controls.Add(btnSave);

            topPanel.Controls.Add(topFlow);

            // ==========================================
            // ۲. جدول (DataGridView)
            // ==========================================
            dgvResults = new DataGridView { Dock = DockStyle.Fill };
            dgvResults.ApplyModernStyle("EmployeeId", "Year");

            // فرمت‌دهی اعداد با ارقام فارسی
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

        // ==========================================================
        // 🧮 محاسبه عیدی (Async + موازی)
        // ==========================================================
        private async void BtnCalculate_Click(object? sender, EventArgs e)
        {
            if (cmbYear.SelectedItem == null)
            {
                ModernMessageBox.ShowWarning("لطفاً سال مالی را انتخاب کنید.");
                return;
            }

            int year = Convert.ToInt32(cmbYear.SelectedItem);
            btnSave.Enabled = false;

            List<YearEndBonusRecord>? calculatedResults = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                // ۱. دریافت تنظیمات
                var settings = await _settingsRepository.GetByYearAsync(year);
                if (settings == null)
                    throw new InvalidOperationException($"تنظیمات حقوق برای سال {year} یافت نشد. ابتدا از منوی «تنظیمات حقوق سالانه» مبالغ را وارد کنید.");

                // ۲. دریافت پرسنل فعال
                var employees = await _empRepository.GetEmployeesWithDetailsAsync("", false);

                // ۳. محاسبات سنگین در پس‌زمینه
                calculatedResults = await Task.Run(() =>
                {
                    var list = new List<YearEndBonusRecord>();
                    foreach (var emp in employees)
                    {
                        var result = _bonusService.CalculateBonus(emp, year, settings);
                        if (result.MonthsWorked > 0) // اگر در آن سال کارکرد داشته
                        {
                            list.Add(result);
                        }
                    }
                    return list;
                });
            }, "در حال محاسبه عیدی و پاداش...");

            if (!success || calculatedResults == null) return;

            _currentResults = calculatedResults;
            dgvResults.DataSource = null;
            dgvResults.DataSource = _currentResults;

            if (dgvResults.Columns.Contains("NetBonus")) dgvResults.Columns["NetBonus"].HeaderText = "خالص عیدی";
            if (dgvResults.Columns.Contains("BonusAmount")) dgvResults.Columns["BonusAmount"].HeaderText = "مبلغ عیدی";

            btnSave.Enabled = true;
            ToastHelper.Show("محاسبات انجام شد. حالا می‌توانید ذخیره کنید.", isSuccess: true);
        }

        // ==========================================================
        // 💾 ذخیره در دیتابیس (Async)
        // ==========================================================
        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_currentResults == null || _currentResults.Count == 0) return;

            if (!ModernMessageBox.AskConfirm("آیا از ذخیره عیدی و پاداش برای این پرسنل مطمئن هستید؟", "ذخیره"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _bonusRepository.SaveBonusesBatchAsync(_currentResults);
            }, "در حال ذخیره در دیتابیس...");

            if (success)
            {
                ToastHelper.Show("عیدی و پاداش پایان سال با موفقیت ذخیره شد.", isSuccess: true);
                btnSave.Enabled = false; // جلوگیری از ذخیره مجدد
            }
        }
    }
}