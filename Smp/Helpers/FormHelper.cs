using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SMP.Data;

namespace SMP.Helpers
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // ─── ساخت هدر ───
            var headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            headerPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(UIHelper.WarmBorder, 1);
                e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            var headerFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                // 🔴 اصلاح شد: کاهش پدینگ تا لوگو در ارتفاع ۷۰ پیکسل جا شود
                Padding = new Padding(15, 5, 15, 5),
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = UIHelper.PersianFont(16f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                AutoSize = true,
                Margin = new Padding(10, 12, 5, 0)
            };

            var picLogo = new PictureBox
            {
                Size = new Size(55, 55),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                // 🔴 اصلاح شد: حاشیه بالا صفر شد تا کل ارتفاع ۵۵ بماند
                Margin = new Padding(0, 0, 0, 0)
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

            headerFlow.Controls.Add(lblTitle);
            headerFlow.Controls.Add(picLogo);
            headerPanel.Controls.Add(headerFlow);

            // ─── پنل محتوا ───
            var contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };
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
    }
}