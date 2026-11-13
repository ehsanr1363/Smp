using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Helpers
{
    public static class ToastHelper
    {
        private static readonly object _lock = new object();
        private static readonly List<Form> _activeToasts = new List<Form>();

        public static void Show(string message, bool isSuccess = true)
        {
            var form = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(350, 70), // کمی بزرگتر برای متن‌های طولانی
                BackColor = isSuccess ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68), // سبز یا قرمز
                ShowInTaskbar = false,
                TopMost = true,
                Opacity = 0.95
            };

            // رسم حاشیه و گوشه‌های نرم (اختیاری)
            form.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, form.ClientRectangle, Color.White, 1, ButtonBorderStyle.Solid, Color.White, 1, ButtonBorderStyle.Solid, Color.White, 1, ButtonBorderStyle.Solid, Color.White, 1, ButtonBorderStyle.Solid);
            };

            var lbl = new Label
            {
                Text = message,
                ForeColor = Color.White,
                Font = UIHelper.PersianFont(10.5f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(10),
                RightToLeft = RightToLeft.Yes
            };
            form.Controls.Add(lbl);

            // اضافه کردن به لیست و مرتب‌سازی موقعیت‌ها
            lock (_lock)
            {
                _activeToasts.Add(form);
                RepositionToasts();
            }

            // وقتی پیام بسته شد، از لیست حذف شود و بقیه جابجا شوند
            form.FormClosed += (s, e) =>
            {
                lock (_lock)
                {
                    _activeToasts.Remove(form);
                    RepositionToasts();
                }
            };

            // نمایش پیام و شروع تایمر بستن خودکار
            form.Show();
            AutoCloseAsync(form);
        }

        // بستن خودکار بعد از ۳ ثانیه (بدون استفاده از Timer که پاک شود)
        private static async void AutoCloseAsync(Form form)
        {
            await Task.Delay(3000);

            if (!form.IsDisposed)
            {
                form.Invoke(new Action(() => form.Close()));
            }
        }

        // چیدمان پیام‌ها از پایین به بالا
        private static void RepositionToasts()
        {
            var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
            int bottom = screen.Bottom - 20; // فاصله از پایین صفحه

            // چون جدیدترین پیام آخر لیست است، از آخر به اول می‌رویم تا از پایین بچینیم
            for (int i = _activeToasts.Count - 1; i >= 0; i--)
            {
                var t = _activeToasts[i];
                if (t.IsDisposed) continue;

                t.Location = new Point(screen.Right - t.Width - 20, bottom - t.Height);
                bottom -= (t.Height + 10); // ۱۰ پیکسل فاصله بین پیام‌ها
            }
        }
    }
}