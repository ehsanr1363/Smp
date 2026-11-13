using SMP.Data;
using SMP.Helpers;
using SMP.Models;
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
    public class AttendanceForm : Form
    {
        private readonly AttendanceRepository _repository;
        private readonly SalarySettingsRepository _settingsRepo;
        private SalarySettings? _settings;

        private ComboBox cmbYear = null!;
        private ComboBox cmbMonth = null!;
        private Button btnLoad = null!;
        private Button btnSave = null!;
        private TabControl tabControl = null!;

        private DataGridView dgvContractual = null!;
        private DataGridView dgvDaily = null!;
        private DataGridView dgvHourly = null!;

        private List<Attendance> _allAttendances = new();

        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public AttendanceForm(string connectionString)
        {
            _repository = new AttendanceRepository(connectionString);
            _settingsRepo = new SalarySettingsRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("ثبت کارکرد ماهانه", new Size(1300, 750));
            LoadDefaults();
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

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

        private void BuildUI()
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
            var topFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(5) };

            cmbYear = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 15, 5) };
            for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);

            cmbMonth = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 15, 5) };
            for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);

            btnLoad = UIHelper.CreateButton("بارگذاری لیست", UIHelper.SectionBlue, 130, 35);
            btnLoad.Margin = new Padding(5, 3, 5, 5);
            btnLoad.Click += async (s, e) => await BtnLoad_Click(s, e);

            topFlow.Controls.Add(new Label { Text = "سال:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbYear);
            topFlow.Controls.Add(new Label { Text = "ماه:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbMonth);
            topFlow.Controls.Add(btnLoad);

            topPanel.Controls.Add(topFlow);

            tabControl = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(12f, FontStyle.Bold), SizeMode = TabSizeMode.Fixed, ItemSize = new Size(200, 40) };
            ModernLayoutHelper.StyleModernTabControl(tabControl);

            // ─── تب ۱: قراردادی / ماهانه ───
            var pageContractual = new TabPage("پرسنل قراردادی / ماهانه") { BackColor = UIHelper.WarmBackground, Padding = new Padding(5) };
            dgvContractual = CreateGrid();
            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonnelCode", DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", ReadOnly = true, Width = 90 });
            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام و نام خانوادگی", ReadOnly = true, Width = 180 });
            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "WorkingDays", DataPropertyName = "WorkingDays", HeaderText = "کل کارکرد (روز)", Width = 100 });

            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalPresenceStr", DataPropertyName = "TotalPresenceStr", HeaderText = "حضور کل (ساعت)", Width = 110 });
            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "GrossOvertimeStr", DataPropertyName = "GrossOvertimeStr", HeaderText = "اضافه کار ناخالص", Width = 110 });
            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "AbsenceHoursStr", DataPropertyName = "AbsenceHoursStr", HeaderText = "کسر کار (ساعت)", Width = 100 });
            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "EarlyOvertimeStr", DataPropertyName = "EarlyOvertimeStr", HeaderText = "اضافه کار اول وقت", Width = 110 });
            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "OtherOvertimeStr", DataPropertyName = "OtherOvertimeStr", HeaderText = "سایر اضافه کار", Width = 100 });
            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "NetOvertimeStr", DataPropertyName = "NetOvertimeStr", HeaderText = "اضافه کار (کسر کار)", ReadOnly = true, Width = 110 });
            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "PayableOvertimeStr", DataPropertyName = "PayableOvertimeStr", HeaderText = "اضافه کار قابل پرداخت", Width = 110 });

            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "HourlyLeave", DataPropertyName = "HourlyLeave", HeaderText = "مرخصی ساعتی", Width = 100 });
            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "DailyLeave", DataPropertyName = "DailyLeave", HeaderText = "مرخصی روزانه", Width = 100 });
            dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "LunchDays", DataPropertyName = "LunchDays", HeaderText = "روزهای نهاری", Width = 100 });

            dgvContractual.CellEndEdit += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvContractual.Rows[e.RowIndex].DataBoundItem is Attendance att)
                {
                    // محاسبه خودکار اضافه کار (کسر کار) در لحظه تایپ
                    att.NetOvertime = att.GrossOvertime - att.AbsenceHours + att.EarlyOvertime + att.OtherOvertime;
                    dgvContractual.InvalidateRow(e.RowIndex);
                }
            };

            pageContractual.Controls.Add(dgvContractual);

            // ─── تب ۲: روزمزد ───
            var pageDaily = new TabPage("پرسنل روزمزد") { BackColor = UIHelper.WarmBackground, Padding = new Padding(5) };
            dgvDaily = CreateGrid();
            dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonnelCode", DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", ReadOnly = true, Width = 80 });
            dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام و نام خانوادگی", ReadOnly = true, Width = 160 });
            dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "WorkingDays", DataPropertyName = "WorkingDays", HeaderText = "روزهای عادی", Width = 80 });
            dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "HolidayDays", DataPropertyName = "HolidayDays", HeaderText = "روزهای تعطیل", Width = 80 });
            dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalDaysStr", DataPropertyName = "TotalDaysStr", HeaderText = "جمع کارکرد", ReadOnly = true, Width = 80 });
            dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "ExpectedHoursStr", DataPropertyName = "ExpectedHoursStr", HeaderText = "موظفی", ReadOnly = true, Width = 100 });
            dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalPresenceStr", DataPropertyName = "TotalPresenceStr", HeaderText = "حضور کل", Width = 120 });
            dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "EarlyOvertimeStr", DataPropertyName = "EarlyOvertimeStr", HeaderText = "اضافه کار اول وقت", Width = 110 });
            dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "NetOvertimeStr", DataPropertyName = "NetOvertimeStr", HeaderText = "اضافه کار (کسر کار)", ReadOnly = true, Width = 110 });
            dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "NormalDayRate", DataPropertyName = "NormalDayRate", HeaderText = "روز عادی", ReadOnly = true, Width = 80 });
            dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "HolidayDayRate", DataPropertyName = "HolidayDayRate", HeaderText = "روز تعطیل", ReadOnly = true, Width = 80 });

            dgvDaily.CellEndEdit += (s, e) =>
            {
                if (dgvDaily.Rows[e.RowIndex].DataBoundItem is Attendance att && _settings != null)
                {
                    decimal expectedHours = (att.WorkingDays * _settings.NormalDayHours) + (att.HolidayDays * _settings.HolidayDayHours);
                    att.NetOvertime = att.TotalPresence - expectedHours - att.EarlyOvertime;
                    dgvDaily.InvalidateRow(e.RowIndex);
                }
            };
            pageDaily.Controls.Add(dgvDaily);

            // ─── تب ۳: ساعتی ───
            var pageHourly = new TabPage("پرسنل ساعتی") { BackColor = UIHelper.WarmBackground, Padding = new Padding(5) };
            dgvHourly = CreateGrid();
            dgvHourly.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonnelCode", DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", ReadOnly = true, Width = 120 });
            dgvHourly.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام و نام خانوادگی", ReadOnly = true, Width = 250 });
            dgvHourly.Columns.Add(new DataGridViewTextBoxColumn { Name = "HoursWorked", DataPropertyName = "HoursWorked", HeaderText = "تعداد ساعات کارکرد", Width = 150 });
            pageHourly.Controls.Add(dgvHourly);

            tabControl.TabPages.Add(pageContractual);
            tabControl.TabPages.Add(pageDaily);
            tabControl.TabPages.Add(pageHourly);

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(10) };

            btnSave = UIHelper.CreateButton("💾 ذخیره کارکردها", UIHelper.ActionGreen, 200, 40);
            btnSave.Dock = DockStyle.Right;
            btnSave.Click += async (s, e) => await BtnSave_Click(s, e);
            bottomPanel.Controls.Add(btnSave);

            Controls.Add(tabControl);
            Controls.Add(topPanel);
            Controls.Add(bottomPanel);
        }

        private DataGridView CreateGrid()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            dgv.ApplyModernStyle();
            dgv.ReadOnly = false;
            return dgv;
        }

        private async Task BtnLoad_Click(object? sender, EventArgs e)
        {
            if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null || IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                int year = Convert.ToInt32(cmbYear.SelectedItem);
                int month = Convert.ToInt32(cmbMonth.SelectedItem);

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    _settings = await _settingsRepo.GetByYearAsync(year);
                    if (_settings == null)
                        throw new InvalidOperationException($"تنظیمات حقوق سال {year} یافت نشد. لطفاً ابتدا تنظیمات را ذخیره کنید.");

                    _allAttendances = await _repository.GetAttendanceForMonthAsync(year, month);

                    if (IsDisposed) return;

                    var contractualList = _allAttendances.Where(a => a.PersonnelType != "روزمزد" && a.PersonnelType != "ساعتی").ToList();
                    var dailyList = _allAttendances.Where(a => a.PersonnelType == "روزمزد").ToList();
                    var hourlyList = _allAttendances.Where(a => a.PersonnelType == "ساعتی").ToList();

                    foreach (var att in contractualList)
                    {
                        att.NetOvertime = att.GrossOvertime - att.AbsenceHours + att.EarlyOvertime + att.OtherOvertime;
                    }

                    foreach (var att in dailyList)
                    {
                        decimal expectedHours = (att.WorkingDays * _settings.NormalDayHours) + (att.HolidayDays * _settings.HolidayDayHours);
                        att.ExpectedHoursStr = TimeHelper.FormatToTimeString(expectedHours);
                        att.NormalDayRate = TimeHelper.FormatToTimeString(_settings.NormalDayHours);
                        att.HolidayDayRate = TimeHelper.FormatToTimeString(_settings.HolidayDayHours);

                        if (att.TotalPresence > 0 || att.EarlyOvertime > 0)
                        {
                            att.NetOvertime = att.TotalPresence - expectedHours - att.EarlyOvertime;
                        }
                    }

                    dgvContractual.DataSource = new System.ComponentModel.BindingList<Attendance>(contractualList);
                    dgvDaily.DataSource = new System.ComponentModel.BindingList<Attendance>(dailyList);
                    dgvHourly.DataSource = new System.ComponentModel.BindingList<Attendance>(hourlyList);
                }, "در حال بارگذاری لیست...");

                if (success && !IsDisposed)
                    ToastHelper.Show("لیست کارکردها بارگذاری شد.", true);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AttendanceForm.BtnLoad_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private async Task BtnSave_Click(object? sender, EventArgs e)
        {
            if (_allAttendances.Count == 0 || IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                // ثبت نهایی مقادیر در حال ویرایش
                dgvContractual.EndEdit();
                dgvDaily.EndEdit();
                dgvHourly.EndEdit();

                // ═══════════════════════════════════════════════════════════════
                // ✅ FIX بحرانی: همگام‌سازی OvertimeHours با NetOvertime
                //
                //   ⚠️ چرا این لازم است؟
                //   ─ PayrollCalculatorService برای محاسبه OvertimePay از att.OvertimeHours استفاده می‌کند
                //   ─ ولی در UI، کاربر فقط GrossOvertimeStr و PayableOvertimeStr را می‌بیند
                //   ─ در نتیجه OvertimeHours همیشه 0 می‌ماند و اضافه‌کاری در حقوق صفر می‌شود
                //
                //   اولویت‌بندی:
                //   ۱. PayableOvertime (اگر کاربر دستی وارد کرده) — دقیق‌ترین
                //   ۲. NetOvertime (محاسبه‌شده از ناخالص - کسر کار + اول وقت + سایر)
                //   ۳. GrossOvertime (اگر NetOvertime صفر بود)
                // ═══════════════════════════════════════════════════════════════
                foreach (var att in _allAttendances)
                {
                    if (att.PayableOvertime > 0)
                        att.OvertimeHours = att.PayableOvertime;
                    else if (att.NetOvertime > 0)
                        att.OvertimeHours = att.NetOvertime;
                    else if (att.GrossOvertime > 0)
                        att.OvertimeHours = att.GrossOvertime;
                    // else: OvertimeHours صفر می‌ماند (طبیعی)
                }

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    await _repository.SaveAttendanceBatchAsync(_allAttendances);
                }, "در حال ذخیره کارکردها...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("کارکردها با موفقیت ذخیره شد.", true);

                    // 👈 بارگذاری مجدد لیست بعد از ذخیره برای نمایش صحیح اطلاعات
                    await BtnLoad_Click(null, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AttendanceForm.BtnSave_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}