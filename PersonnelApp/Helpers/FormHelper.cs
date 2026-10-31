using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PersonnelApp.Data;

namespace PersonnelApp.Helpers
{
    public static class FormHelper
    {
        public static void ApplyStandardHeader(this Form form, string title, Size size, bool isDialog = true)
        {
            form.SuspendLayout();

            form.Text = title;
            form.Size = size;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.RightToLeft = RightToLeft.Yes;
            form.RightToLeftLayout = true;
            form.BackColor = UIHelper.WarmBackground;
            form.Font = UIHelper.PersianFont(11f);
            form.Padding = new Padding(0);

            if (isDialog)
            {
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
            }
            else
            {
                form.FormBorderStyle = FormBorderStyle.Sizable;
                form.MaximizeBox = true;
            }

            var existingControls = form.Controls.Cast<Control>().ToList();
            form.Controls.Clear();

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F)); // ارتفاع هدر
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // ─── ساخت هدر ───
            var headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground };
            headerPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(UIHelper.WarmBorder, 1);
                e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            // ✅ استفاده از TableLayoutPanel برای وسط‌چین کردن کامل لوگو و عنوان
            var headerTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            // ستون چپ و راست را 50-50 می‌کنیم تا ستون وسط (محتوا) دقیقاً در مرکز قرار بگیرد
            headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var centerFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft, // عنوان سمت راست، لوگو سمت چپ آن
                WrapContents = false,
                Anchor = AnchorStyles.None, // این خط باعث می‌شود کل پنل در ستون وسط، دقیقاً وسط‌چین شود
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = UIHelper.PersianFont(15f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                AutoSize = true,
                Margin = new Padding(10, 22, 10, 0)
            };

            var picLogo = new PictureBox
            {
                Size = new Size(60, 60),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Margin = new Padding(10, 10, 10, 10)
            };

            try
            {
                var repo = new CompanyRepository(AppConfig.ConnectionString);
                var company = repo.GetAll().FirstOrDefault();
                if (company != null && company.Logo != null && company.Logo.Length > 0)
                {
                    using var ms = new MemoryStream(company.Logo);
                    picLogo.Image = Image.FromStream(ms);
                }
            }
            catch { /* اگر لوگو لود نشد، برنامه متوقف نشود */ }

            centerFlow.Controls.Add(lblTitle);
            centerFlow.Controls.Add(picLogo);

            headerTable.Controls.Add(centerFlow, 1, 0);
            headerPanel.Controls.Add(headerTable);

            // ─── پنل محتوا ───
            var contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };

            foreach (var ctrl in existingControls)
            {
                contentPanel.Controls.Add(ctrl);
            }

            mainLayout.Controls.Add(headerPanel, 0, 0);
            mainLayout.Controls.Add(contentPanel, 0, 1);

            form.Controls.Add(mainLayout);
            EnterNavigationHelper.EnableForForm(form);
            form.ResumeLayout();
        }

        /// <summary>
        /// ⚠️ منسوخ — از EnterNavigationHelper.EnableForForm استفاده کنید.
        /// </summary>
        public static bool HandleEnterKeyNavigation(this Form form, ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                Control activeControl = form.ActiveControl;
                bool isInputControl = activeControl is TextBox || activeControl is ComboBox || activeControl is NumericUpDown;

                if (isInputControl)
                {
                    if (activeControl is TextBox tb && tb.Multiline) return false;
                    if (activeControl is ComboBox cb && cb.DroppedDown) return false;

                    if (form.SelectNextControl(activeControl, true, true, true, true))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}