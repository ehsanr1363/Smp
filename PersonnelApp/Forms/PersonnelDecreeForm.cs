using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class PersonnelDecreeForm : Form
    {
        private readonly Employee _employee;
        private Panel mainPanel = null!;
        private PictureBox picLogo = null!;
        private Button btnPrint = null!;
        private Bitmap? _memoryImage;

        public PersonnelDecreeForm(Employee employee)
        {
            _employee = employee;

            BuildUI();

            // 👈 استفاده از SetupForm برای استایل استاندارد و EnterNavigation
            UIHelper.SetupForm(this, $"حکم پرسنلی - {employee.FullName}", new Size(800, 900));
            ShowInTaskbar = false;

            // فعال‌سازی میانبرهای Ctrl+S (چاپ) و Esc (بستن)
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnPrint);

            // لود لوگو پس از نمایش فرم برای جلوگیری از فریز
            Shown += async (s, e) => await LoadLogoAsync();
        }

        private void BuildUI()
        {
            // ==========================================
            // ۱. پنل بالا (دکمه چاپ)
            // ==========================================
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };

            btnPrint = UIHelper.CreateButton("🖨️ چاپ حکم", UIHelper.SectionBlue, 150, 35);
            btnPrint.Dock = DockStyle.Right;
            btnPrint.Click += BtnPrint_Click;
            topPanel.Controls.Add(btnPrint);

            // ==========================================
            // ۲. پنل اصلی (محتوای حکم)
            // ==========================================
            mainPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30), BackColor = Color.White };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, BackColor = Color.White };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F)); // Header
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));  // Date/Number
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));  // Intro text
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F)); // Employee info
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // Salary details
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F)); // Signatures

            // 1. Header (Logo + Title)
            var headerPanel = new Panel { Dock = DockStyle.Fill };

            picLogo = new PictureBox { Name = "picLogo", Dock = DockStyle.Right, Width = 100, SizeMode = PictureBoxSizeMode.Zoom };

            var lblTitle = new Label
            {
                Text = "بسمه تعالی\nحکم پرسنلی",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(18f, FontStyle.Bold),
                ForeColor = UIHelper.SectionBlue
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(picLogo);
            layout.Controls.Add(headerPanel, 0, 0);

            // 2. Date & Number
            var datePanel = new Panel { Dock = DockStyle.Fill };

            // 👈 تبدیل تاریخ به شمسی
            var lblDate = new Label
            {
                Text = $"تاریخ: {DateHelper.ToPersianDate(DateTime.Now)}",
                Dock = DockStyle.Right,
                AutoSize = true,
                Font = UIHelper.PersianFont(11f)
            };

            var lblNumber = new Label
            {
                Text = $"شماره: {_employee.PersonnelCode}",
                Dock = DockStyle.Left,
                AutoSize = true,
                Font = UIHelper.PersianFont(11f)
            };

            datePanel.Controls.Add(lblDate);
            datePanel.Controls.Add(lblNumber);
            layout.Controls.Add(datePanel, 0, 1);

            // 3. Intro
            var lblIntro = new Label
            {
                Text = "بدینوسیله به اطلاع می‌رساند:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold)
            };
            layout.Controls.Add(lblIntro, 0, 2);

            // 4. Employee Info (Contract & Personal)
            var infoLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4 };
            infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            infoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            AddInfoRow(infoLayout, 0, "نام و نام خانوادگی:", _employee.FullName, "کد ملی:", _employee.NationalCode);
            AddInfoRow(infoLayout, 1, "واحد سازمانی:", _employee.DepartmentName, "سمت:", _employee.Position);

            // 👈 تبدیل تاریخ استخدام، شروع و پایان قرارداد به شمسی
            AddInfoRow(infoLayout, 2, "تاریخ استخدام:", DateHelper.ToPersianDate(_employee.HireDate),
                                             "نوع قرارداد:", _employee.PersonnelType);

            AddInfoRow(infoLayout, 3, "شروع قرارداد:", DateHelper.ToPersianDate(_employee.ContractStartDate),
                                             "پایان قرارداد:", DateHelper.ToPersianDate(_employee.ContractEndDate));

            layout.Controls.Add(infoLayout, 0, 3);

            // 5. Salary Details
            var salaryPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(10) };
            var salaryLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 6 };
            salaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            salaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            salaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            salaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            // 👈 استفاده از NumberHelper برای نمایش مبالغ با ارقام فارسی
            AddInfoRow(salaryLayout, 0, "حقوق پایه:",
                NumberHelper.FormatThousands(_employee.BaseSalary, usePersianDigits: true) + " ریال",
                "حق مسکن:", NumberHelper.FormatThousands(_employee.HousingAllowance, usePersianDigits: true) + " ریال");

            AddInfoRow(salaryLayout, 1, "حق بن خواربار:",
                NumberHelper.FormatThousands(_employee.GroceryAllowance, usePersianDigits: true) + " ریال",
                "حق اولاد:", NumberHelper.FormatThousands(_employee.ChildAllowance, usePersianDigits: true) + " ریال");

            AddInfoRow(salaryLayout, 2, "پایه سنوات:",
                NumberHelper.FormatThousands(_employee.YearsBonus, usePersianDigits: true) + " ریال",
                "حق جذب:", NumberHelper.FormatThousands(_employee.AttractionAllowance, usePersianDigits: true) + " ریال");

            AddInfoRow(salaryLayout, 3, "حق مسئولیت:",
                NumberHelper.FormatThousands(_employee.ResponsibilityAllowance, usePersianDigits: true) + " ریال",
                "حق تخصصی:", NumberHelper.FormatThousands(_employee.TechnicalAllowance, usePersianDigits: true) + " ریال");

            AddInfoRow(salaryLayout, 4, "حق مدیریت:",
                NumberHelper.FormatThousands(_employee.ManagementAllowance, usePersianDigits: true) + " ریال",
                "سایر مزایا:", NumberHelper.FormatThousands(_employee.OtherAllowances, usePersianDigits: true) + " ریال");

            // Total
            decimal total = _employee.BaseSalary + _employee.HousingAllowance + _employee.GroceryAllowance +
                            _employee.ChildAllowance + _employee.YearsBonus + _employee.AttractionAllowance +
                            _employee.ResponsibilityAllowance + _employee.TechnicalAllowance +
                            _employee.ManagementAllowance + _employee.OtherAllowances;

            var lblTotalTitle = new Label { Text = "جمع کل حقوق و مزایا:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(12f, FontStyle.Bold) };
            var lblTotalValue = new Label
            {
                Text = NumberHelper.FormatThousands(total, usePersianDigits: true) + " ریال",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                ForeColor = UIHelper.SectionBlue
            };

            salaryLayout.Controls.Add(lblTotalTitle, 0, 5);
            salaryLayout.SetColumnSpan(lblTotalTitle, 2);
            salaryLayout.Controls.Add(lblTotalValue, 2, 5);
            salaryLayout.SetColumnSpan(lblTotalValue, 2);

            salaryPanel.Controls.Add(salaryLayout);
            layout.Controls.Add(salaryPanel, 0, 4);

            // 6. Signatures
            var signLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            signLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            signLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var lblSign1 = new Label { Text = "امضاء کارمند:\n\n\n_________________________", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(11f) };
            var lblSign2 = new Label { Text = "امضاء مدیریت:\n\n\n_________________________", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(11f) };

            signLayout.Controls.Add(lblSign1, 0, 0);
            signLayout.Controls.Add(lblSign2, 1, 0);
            layout.Controls.Add(signLayout, 0, 5);

            mainPanel.Controls.Add(layout);
            this.Controls.Add(mainPanel);
            this.Controls.Add(topPanel);
        }

        private void AddInfoRow(TableLayoutPanel layout, int row, string label1, string? value1, string? label2, string? value2)
        {
            layout.Controls.Add(new Label { Text = label1, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f) }, 0, row);
            layout.Controls.Add(new Label { Text = string.IsNullOrWhiteSpace(value1) ? "---" : value1, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UIHelper.PersianFont(11f, FontStyle.Bold) }, 1, row);

            if (!string.IsNullOrEmpty(label2))
            {
                layout.Controls.Add(new Label { Text = label2, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f) }, 2, row);
                layout.Controls.Add(new Label { Text = string.IsNullOrWhiteSpace(value2) ? "---" : value2, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UIHelper.PersianFont(11f, FontStyle.Bold) }, 3, row);
            }
        }

        // ==========================================================
        // 🖼️ بارگذاری لوگو از دیتابیس (Async)
        // ==========================================================
        private async Task LoadLogoAsync()
        {
            try
            {
                var repo = new CompanyRepository(AppConfig.ConnectionString);
                var company = await Task.Run(() => repo.GetAll().FirstOrDefault(c => c.Logo != null && c.Logo.Length > 0));

                if (company != null && company.Logo != null && company.Logo.Length > 0)
                {
                    // 👈 استفاده از ImageHelper برای جلوگیری از باگ GDI+ Stream
                    picLogo.Image = ImageHelper.ByteArrayToImage(company.Logo);
                }
            }
            catch (Exception ex)
            {
                // خطای لوگو نباید مانع کار شود، فقط لاگ می‌شود.
                LogHelper.LogWarning("خطا در بارگذاری لوگو در حکم پرسنلی: " + ex.Message, "PersonnelDecreeForm");
            }
        }

        // ==========================================================
        // 🖨️ چاپ و پیش‌نمایش (با مدیریت صحیح منابع)
        // ==========================================================
        private void BtnPrint_Click(object? sender, EventArgs e)
        {
            try
            {
                _memoryImage?.Dispose();
                _memoryImage = new Bitmap(mainPanel.Width, mainPanel.Height);
                mainPanel.DrawToBitmap(_memoryImage, new Rectangle(0, 0, mainPanel.Width, mainPanel.Height));

                // 👈 استفاده از using برای جلوگیری از نشت حافظه
                using var pd = new PrintDocument();
                pd.PrintPage += PrintPage;
                pd.DefaultPageSettings.Landscape = false;

                using var previewDialog = new PrintPreviewDialog
                {
                    Document = pd,
                    Width = 900,
                    Height = 700,
                    StartPosition = FormStartPosition.CenterScreen,
                    Text = "پیش‌نمایش چاپ حکم پرسنلی",
                    RightToLeft = RightToLeft.Yes,
                    RightToLeftLayout = true
                };

                previewDialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PersonnelDecreeForm.BtnPrint");
                ModernMessageBox.ShowError("خطا در آماده‌سازی چاپ:\n" + ex.Message);
            }
        }

        private void PrintPage(object sender, PrintPageEventArgs e)
        {
            if (_memoryImage != null)
            {
                int x = e.MarginBounds.X;
                int y = e.MarginBounds.Y;
                int width = e.MarginBounds.Width;
                int height = (int)((float)width * _memoryImage.Height / _memoryImage.Width);

                e.Graphics?.DrawImage(_memoryImage, x, y, width, height);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _memoryImage?.Dispose();
            base.Dispose(disposing);
        }
    }
}