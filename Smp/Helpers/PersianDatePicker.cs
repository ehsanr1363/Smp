using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace SMP.Helpers
{
    public static class PersianDatePicker
    {
        private static readonly PersianCalendar _pc = new PersianCalendar();
        private static int _currentYear;
        private static int _currentMonth;
        private static Form _popupForm;
        private static TextBox _targetTextBox;

        /// <summary>
        /// به یک TextBox متصل می‌شود و هنگام کلیک، تقویم شمسی باز می‌شود.
        /// </summary>
        public static void AttachTo(TextBox txt)
        {
            if (txt == null) return;

            // یک دکمه کوچک کنار تکست‌باکس
            txt.ReadOnly = true;
            txt.Cursor = Cursors.Hand;
            txt.Click += (s, e) => ShowCalendar(txt);
        }

        private static void ShowCalendar(TextBox target)
        {
            _targetTextBox = target;

            // تعیین ماه و سال جاری از متن فعلی
            DateTime baseDate = DateHelper.ParsePersianDate(target.Text) ?? DateTime.Now;
            _currentYear = _pc.GetYear(baseDate);
            _currentMonth = _pc.GetMonth(baseDate);

            _popupForm = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(300, 320),
                BackColor = Color.White,
                TopMost = true,
                ShowInTaskbar = false
            };

            // موقعیت زیر تکست‌باکس
            var screenPoint = target.PointToScreen(new Point(0, target.Height));
            _popupForm.Location = screenPoint;

            // رسم حاشیه
            _popupForm.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, _popupForm.ClientRectangle, Color.FromArgb(200, 200, 200), ButtonBorderStyle.Solid);
            };

            // ساخت هدر (ماه و سال با دکمه‌های قبل و بعد)
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(59, 130, 246) };

            var btnPrev = new Button { Text = "◀", Dock = DockStyle.Left, Width = 40, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            btnPrev.FlatAppearance.BorderSize = 0;
            btnPrev.Click += (s, e) => { _currentMonth--; if (_currentMonth < 1) { _currentMonth = 12; _currentYear--; } RefreshCalendar(); };

            var btnNext = new Button { Text = "▶", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            btnNext.FlatAppearance.BorderSize = 0;
            btnNext.Click += (s, e) => { _currentMonth++; if (_currentMonth > 12) { _currentMonth = 1; _currentYear++; } RefreshCalendar(); };

            var lblMonthYear = new Label
            {
                Text = $"{TranslationHelper.MonthNames[_currentMonth]} {_currentYear}",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            pnlHeader.Controls.Add(lblMonthYear);
            pnlHeader.Controls.Add(btnNext);
            pnlHeader.Controls.Add(btnPrev);

            // جدول روزها
            var tblDays = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 7,
                RightToLeft = RightToLeft.Yes,
                BackColor = Color.White,
                Padding = new Padding(5)
            };

            for (int i = 0; i < 7; i++)
                tblDays.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.28f));
            for (int i = 0; i < 7; i++)
                tblDays.RowStyles.Add(new RowStyle(SizeType.Percent, 14.28f));

            // روزهای هفته
            string[] weekDays = { "ش", "ی", "د", "س", "چ", "پ", "ج" };
            for (int i = 0; i < 7; i++)
            {
                var lbl = new Label
                {
                    Text = weekDays[i],
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = UIHelper.PersianFont(10f, FontStyle.Bold),
                    ForeColor = Color.Gray
                };
                tblDays.Controls.Add(lbl, i, 0);
            }

            _popupForm.Controls.Add(tblDays);
            _popupForm.Controls.Add(pnlHeader);

            RefreshCalendar(tblDays);

            _popupForm.Deactivate += (s, e) => _popupForm.Close();
            _popupForm.Show();
        }

        private static void RefreshCalendar(TableLayoutPanel tblDays = null)
        {
            if (tblDays == null)
                tblDays = _popupForm.Controls[0] as TableLayoutPanel;

            // به‌روزرسانی عنوان ماه
            var lblMonthYear = _popupForm.Controls[1].Controls[0] as Label;
            lblMonthYear.Text = $"{TranslationHelper.MonthNames[_currentMonth]} {_currentYear}";

            // پاک کردن روزهای قبلی
            for (int r = 1; r < 7; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    var oldCtrl = tblDays.GetControlFromPosition(c, r);
                    if (oldCtrl != null) tblDays.Controls.Remove(oldCtrl);
                }
            }

            // محاسبه روز اول ماه
            int daysInMonth = _pc.GetDaysInMonth(_currentYear, _currentMonth);
            DateTime firstDay = _pc.ToDateTime(_currentYear, _currentMonth, 1, 0, 0, 0, 0);
            int startCol = (int)_pc.GetDayOfWeek(firstDay);
            // تبدیل شنبه=0 به شنبه=6 (چون تقویم فارسی از شنبه شروع می‌شود)
            int col = (startCol + 1) % 7;
            int row = 1;

            for (int day = 1; day <= daysInMonth; day++)
            {
                var btnDay = new Button
                {
                    Text = day.ToString(),
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                    Font = UIHelper.PersianFont(10f),
                    BackColor = Color.White,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(1)
                };
                btnDay.FlatAppearance.BorderSize = 0;
                btnDay.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 248, 255);

                // رنگ جمعه‌ها
                if (col == 6) btnDay.ForeColor = Color.Red;

                int selectedDay = day;
                btnDay.Click += (s, e) =>
                {
                    _targetTextBox.Text = $"{_currentYear:0000}/{_currentMonth:00}/{selectedDay:00}";
                    _popupForm.Close();
                };

                tblDays.Controls.Add(btnDay, col, row);
                col++;
                if (col > 6) { col = 0; row++; }
            }
        }
    }
}