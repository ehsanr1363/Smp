using System;
using System.Drawing;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class ModernLayoutHelper
    {
        // ==========================================================
        // ۱. ساخت هدر بالای فرم (دقیقاً مثل عکس اول)
        // ==========================================================
        public static Panel CreateModernHeader(string title, string subTitle, Image logo)
        {
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.White,
                Padding = new Padding(10, 0, 10, 0)
            };

            // رسم خط نازک در پایین هدر
            headerPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(230, 230, 230), 1);
                e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            // --- سمت راست هدر (لوگو و عناوین) ---
            var rightPanel = new Panel { Dock = DockStyle.Right, Width = 380 };

            var picLogo = new PictureBox
            {
                Image = logo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Right,
                Width = 60,
                Margin = new Padding(10)
            };

            var titlePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 10, 0) };

            var lblTitle = new Label
            {
                Text = title,
                Font = UIHelper.PersianFont(14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleRight
            };

            var lblSubTitle = new Label
            {
                Text = subTitle,
                Font = UIHelper.PersianFont(9f, FontStyle.Regular),
                ForeColor = Color.Gray,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopRight
            };

            titlePanel.Controls.Add(lblSubTitle);
            titlePanel.Controls.Add(lblTitle);
            rightPanel.Controls.Add(titlePanel);
            rightPanel.Controls.Add(picLogo);

            // --- سمت چپ هدر (منو، چک‌باکس، جستجو) ---
            var leftPanel = new Panel { Dock = DockStyle.Left, Width = 450 };

            var btnMenu = new Button
            {
                Text = "☰",
                Font = new Font("Arial", 16f, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(40, 40),
                Dock = DockStyle.Left,
                Cursor = Cursors.Hand
            };
            btnMenu.FlatAppearance.BorderSize = 0;

            var chkInactive = new CheckBox
            {
                Text = "نمایش افراد غیرفعال",
                Font = UIHelper.PersianFont(10f),
                ForeColor = Color.FromArgb(80, 80, 80),
                Dock = DockStyle.Left,
                Width = 140,
                TextAlign = ContentAlignment.MiddleRight
            };

            var txtSearch = new TextBox
            {
                PlaceholderText = "جستجو...",
                Font = UIHelper.PersianFont(10f),
                Dock = DockStyle.Left,
                Width = 200
            };
            StyleModernInput(txtSearch);

            // ایجاد پنل‌های واسط برای تنظیم فاصله عمودی
            var searchWrap = new Panel { Dock = DockStyle.Left, Width = 220, Padding = new Padding(0, 18, 10, 0) };
            searchWrap.Controls.Add(txtSearch);

            var chkWrap = new Panel { Dock = DockStyle.Left, Width = 140, Padding = new Padding(0, 20, 0, 0) };
            chkWrap.Controls.Add(chkInactive);

            leftPanel.Controls.Add(btnMenu);
            leftPanel.Controls.Add(searchWrap);
            leftPanel.Controls.Add(chkWrap);

            headerPanel.Controls.Add(leftPanel);
            headerPanel.Controls.Add(rightPanel);

            return headerPanel;
        }

        // ==========================================================
        // ۲. استایل‌دهی به تکست‌باکس و کمبوباکس (دقیقاً مثل عکس دوم)
        // ==========================================================
        public static void StyleModernInput(Control input)
        {
            if (input == null) return;

            input.Font = UIHelper.PersianFont(11f);
            input.BackColor = Color.White;
            input.ForeColor = Color.FromArgb(50, 50, 50);

            if (input is TextBox txt)
            {
                txt.AutoSize = false;
                txt.Height = 32;
                txt.TextAlign = HorizontalAlignment.Center; // وسط‌چین بودن متن
                txt.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (input is ComboBox cb)
            {
                cb.Height = 32;
                cb.DropDownStyle = ComboBoxStyle.DropDownList;
                cb.FlatStyle = FlatStyle.Flat;
            }
            else if (input is DateTimePicker dtp)
            {
                dtp.Height = 32;
            }
        }

        // ==========================================================
        // ۳. افزودن لیبل و کنترول به جدول (با چسبندگی دقیق)
        // ==========================================================
        public static void AddModernField(TableLayoutPanel tlp, string labelText, Control control, int row, int labelCol, int controlCol)
        {
            var lbl = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight, // لیبل دقیقاً به سمت راست می‌چسبد
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80),
                Margin = new Padding(0, 0, 0, 0), // بدون هیچ فاصله‌ای
                AutoSize = false
            };

            StyleModernInput(control);
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(5, 5, 0, 5); // فقط ۵ پیکسل فاصله از لیبل

            tlp.Controls.Add(lbl, labelCol, row);
            tlp.Controls.Add(control, controlCol, row);
        }

        // ==========================================================
        // ۴. استایل‌دهی به TabControl (تب‌های سفید و فلت)
        // ==========================================================
        public static void StyleModernTabControl(TabControl tabControl)
        {
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.ItemSize = new Size(160, 40);
            tabControl.Font = UIHelper.PersianFont(11f, FontStyle.Bold);
            tabControl.Padding = new Point(15, 5);

            tabControl.DrawItem += (sender, e) =>
            {
                TabControl tc = (TabControl)sender;
                TabPage page = tc.TabPages[e.Index];
                Rectangle rect = tc.GetTabRect(e.Index);

                Color backColor = (e.Index == tc.SelectedIndex) ? Color.White : Color.FromArgb(240, 240, 240);
                Color textColor = (e.Index == tc.SelectedIndex) ? Color.FromArgb(30, 41, 59) : Color.Gray;

                using (SolidBrush brush = new SolidBrush(backColor))
                    e.Graphics.FillRectangle(brush, rect);

                if (e.Index == tc.SelectedIndex)
                {
                    using Pen pen = new Pen(Color.FromArgb(59, 130, 246), 3); // خط آبی زیر تب فعال
                    e.Graphics.DrawLine(pen, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
                }
                else
                {
                    using Pen pen = new Pen(Color.LightGray, 1); // خط جداکننده تب‌های غیرفعال
                    e.Graphics.DrawLine(pen, rect.Right - 1, rect.Top + 10, rect.Right - 1, rect.Bottom - 10);
                }

                TextRenderer.DrawText(e.Graphics, page.Text, tc.Font, rect, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
        }
    }
}