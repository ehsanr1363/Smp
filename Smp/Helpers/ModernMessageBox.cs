using System;
using System.Drawing;
using System.Windows.Forms;

namespace SMP.Helpers
{
    public static class ModernMessageBox
    {
        public static DialogResult Show(string message, string title = "سیستم مدیریت", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            // اطمینان از اجرا در ترد اصلی رابط کاربری
            if (Application.OpenForms.Count > 0 && Application.OpenForms[0].InvokeRequired)
            {
                return (DialogResult)Application.OpenForms[0].Invoke(new Func<DialogResult>(() => Show(message, title, buttons, icon)));
            }

            using var form = new Form
            {
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                Text = title,
                BackColor = Color.White,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                ShowInTaskbar = false,
                Size = new Size(450, 200) // اندازه پایه
            };

            Font messageFont = UIHelper.PersianFont(11f, FontStyle.Regular);

            // محاسبه دقیق ارتفاع متن
            Size textSize = TextRenderer.MeasureText(message, messageFont, new Size(350, int.MaxValue), TextFormatFlags.WordBreak);
            int neededHeight = Math.Max(80, textSize.Height + 20);

            // ✅ استفاده از TableLayoutPanel برای جلوگیری از بیرون زدن دکمه‌ها
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(20)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, neededHeight));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F)); // ردیف دکمه‌ها

            // --- ردیف اول: پیام و آیکون ---
            var pnlMsg = new Panel { Dock = DockStyle.Fill };
            var lblMessage = new Label
            {
                Text = message,
                Font = messageFont,
                ForeColor = Color.FromArgb(40, 40, 40),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(10, 0, 0, 0)
            };

            var picIcon = new PictureBox
            {
                Size = new Size(32, 32),
                Dock = DockStyle.Left,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            switch (icon)
            {
                case MessageBoxIcon.Error: picIcon.Image = SystemIcons.Error.ToBitmap(); break;
                case MessageBoxIcon.Warning: picIcon.Image = SystemIcons.Warning.ToBitmap(); break;
                case MessageBoxIcon.Question: picIcon.Image = SystemIcons.Question.ToBitmap(); break;
                default: picIcon.Image = SystemIcons.Information.ToBitmap(); break;
            }

            pnlMsg.Controls.Add(lblMessage);
            pnlMsg.Controls.Add(picIcon);

            // --- ردیف دوم: دکمه‌ها ---
            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 0)
            };

            int btnWidth = 100;
            int btnHeight = 35;

            Color primaryColor = icon == MessageBoxIcon.Error ? Color.FromArgb(220, 53, 69) :
                                 icon == MessageBoxIcon.Warning ? Color.FromArgb(234, 124, 105) :
                                 Color.FromArgb(59, 130, 246);

            var btnPrimary = new Button
            {
                Text = (buttons == MessageBoxButtons.OKCancel || buttons == MessageBoxButtons.OK) ? "تایید" : "بله",
                DialogResult = buttons == MessageBoxButtons.OK ? DialogResult.OK : DialogResult.Yes,
                Size = new Size(btnWidth, btnHeight),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPrimary.FlatAppearance.BorderSize = 0;

            if (buttons == MessageBoxButtons.OKCancel || buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel)
            {
                var btnSecondary = new Button
                {
                    Text = buttons == MessageBoxButtons.OKCancel ? "انصراف" : "خیر",
                    DialogResult = DialogResult.Cancel,
                    Size = new Size(btnWidth, btnHeight),
                    BackColor = Color.FromArgb(240, 240, 240),
                    FlatStyle = FlatStyle.Flat,
                    Font = UIHelper.PersianFont(11f),
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 0, 10, 0)
                };
                btnSecondary.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
                pnlButtons.Controls.Add(btnSecondary);
            }

            pnlButtons.Controls.Add(btnPrimary);

            mainLayout.Controls.Add(pnlMsg, 0, 0);
            mainLayout.Controls.Add(pnlButtons, 0, 1);

            form.Controls.Add(mainLayout);

            // تنظیم دقیق ارتفاع فرم بر اساس محتوا
            form.ClientSize = new Size(450, neededHeight + 70);

            form.AcceptButton = btnPrimary;
            if (buttons != MessageBoxButtons.OK && pnlButtons.Controls.Count > 1)
                form.CancelButton = (Button)pnlButtons.Controls[0];
            else
                form.CancelButton = btnPrimary;

            return form.ShowDialog();
        }

        public static void ShowError(string message, string title = "خطا")
        {
            Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void ShowWarning(string message, string title = "هشدار")
        {
            Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void ShowInfo(string message, string title = "اطلاع")
        {
            Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void ShowSuccess(string message, string title = "موفقیت")
        {
            Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static bool AskConfirm(string message, string title = "تایید")
        {
            return Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        public static bool ShowQuestion(string message, string title = "سوال")
        {
            return Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
    }
}