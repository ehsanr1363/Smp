using System;
using System.Drawing;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class ModernMessageBox
    {
        public static void ShowInfo(string message, string title = "اطلاعیه")
            => Show(message, title, MessageBoxIcon.Information);

        public static void ShowError(string message, string title = "خطا")
            => Show(message, title, MessageBoxIcon.Error);

        public static void ShowWarning(string message, string title = "هشدار")
            => Show(message, title, MessageBoxIcon.Warning);

        public static bool AskConfirm(string message, string title = "تایید")
        {
            using var form = CreateBaseForm(title, message, MessageBoxIcon.Question, true);
            return form.ShowDialog() == DialogResult.Yes;
        }

        private static void Show(string message, string title, MessageBoxIcon icon)
        {
            using var form = CreateBaseForm(title, message, icon, false);
            form.ShowDialog();
        }

        private static Form CreateBaseForm(string title, string message, MessageBoxIcon icon, bool isConfirm)
        {
            const int minWidth = 450;
            const int minHeight = 220;
            const int messageMaxWidth = 350;

            // اندازه‌گیری داینامیک متن
            Font msgFont = UIHelper.PersianFont(11f);
            Size measured = TextRenderer.MeasureText(
                message ?? "", msgFont,
                new Size(messageMaxWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.RightToLeft);

            int messageHeight = Math.Max(80, measured.Height);
            int formWidth = Math.Max(minWidth, Math.Min(650, measured.Width + 130));
            int formHeight = Math.Max(minHeight, 160 + messageHeight);

            var form = new Form
            {
                Text = title,
                Size = new Size(formWidth, formHeight),
                MinimumSize = new Size(minWidth, minHeight),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.None,
                BackColor = Color.White,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Font = msgFont,
                ShowInTaskbar = false
            };

            // حاشیه و خط بالای فرم
            form.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, form.ClientRectangle,
                    Color.FromArgb(220, 220, 220), ButtonBorderStyle.Solid);
                using var pen = new Pen(Color.FromArgb(59, 130, 246), 4);
                e.Graphics.DrawLine(pen, 0, 0, form.Width, 0);
            };

            // هدر
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(248, 249, 250) };
            pnlHeader.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(form.Handle, 0xA1, 0x2, 0);
                }
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 50, 50),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(10, 0, 10, 0)
            };
            pnlHeader.Controls.Add(lblTitle);

            // آیکون
            var picIcon = new PictureBox
            {
                Size = new Size(32, 32),
                Location = new Point(20, 65),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            switch (icon)
            {
                case MessageBoxIcon.Error: picIcon.Image = SystemIcons.Error.ToBitmap(); break;
                case MessageBoxIcon.Warning: picIcon.Image = SystemIcons.Warning.ToBitmap(); break;
                case MessageBoxIcon.Question: picIcon.Image = SystemIcons.Question.ToBitmap(); break;
                default: picIcon.Image = SystemIcons.Information.ToBitmap(); break;
            }

            // متن
            var lblMessage = new Label
            {
                Text = message,
                Font = msgFont,
                ForeColor = Color.FromArgb(80, 80, 80),
                Location = new Point(60, 70),
                Size = new Size(form.Width - 100, messageHeight),
                TextAlign = ContentAlignment.TopRight,
                AutoSize = false
            };

            // دکمه‌ها
            var pnlButtons = new Panel { Dock = DockStyle.Bottom, Height = 50 };

            var btnNo = new Button
            {
                Text = "خیر",
                Size = new Size(100, 35),
                Location = new Point(20, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(80, 80, 80),
                Cursor = Cursors.Hand,
                Visible = isConfirm,
                DialogResult = DialogResult.No
            };
            btnNo.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);

            var btnYes = new Button
            {
                Text = isConfirm ? "بله" : "تایید",
                Size = new Size(100, 35),
                Location = new Point(130, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = isConfirm ? DialogResult.Yes : DialogResult.OK
            };
            btnYes.FlatAppearance.BorderSize = 0;

            pnlButtons.Controls.Add(btnNo);
            pnlButtons.Controls.Add(btnYes);

            form.Controls.Add(pnlButtons);
            form.Controls.Add(lblMessage);
            form.Controls.Add(picIcon);
            form.Controls.Add(pnlHeader);

            form.AcceptButton = btnYes;
            form.CancelButton = btnNo;

            return form;
        }
    }

    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    }
}