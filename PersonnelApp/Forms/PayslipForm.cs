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
    public class PayslipForm : Form
    {
        private readonly PayrollResult _payroll;
        private Panel mainPanel = null!;
        private Bitmap? _memoryImage;
        private Button btnPrint = null!;

        public PayslipForm(PayrollResult payroll)
        {
            _payroll = payroll;

            BuildUI();

            UIHelper.SetupForm(this, $"فیش حقوقی - {payroll.EmployeeName} - {payroll.Month} / {payroll.Year}", new Size(780, 960));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnPrint);

            Shown += async (s, e) => await LoadLogoAsync();
        }

        private void BuildUI()
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };

            btnPrint = UIHelper.CreateButton("🖨️ چاپ فیش حقوقی", UIHelper.SectionBlue, 180, 35);
            btnPrint.Dock = DockStyle.Right;
            btnPrint.Click += BtnPrint_Click;
            topPanel.Controls.Add(btnPrint);

            mainPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20), BackColor = Color.White };

            // RowCount برای فضای کافی برای تمام مزایا و کسورات
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 22, BackColor = Color.White };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            var headerPanel = new Panel { Dock = DockStyle.Fill, Height = 70 };

            var lblHeader = new Label
            {
                Text = "فیش حقوقی ماهانه",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(18f, FontStyle.Bold),
                ForeColor = UIHelper.SectionBlue
            };

            var picLogo = new PictureBox
            {
                Name = "picLogo",
                Dock = DockStyle.Right,
                Width = 80,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            headerPanel.Controls.Add(lblHeader);
            headerPanel.Controls.Add(picLogo);

            layout.SetColumnSpan(headerPanel, 4);
            layout.Controls.Add(headerPanel, 0, 0);

            AddRow(layout, 1, "نام و نام خانوادگی:", _payroll.EmployeeName, "کد پرسنلی:", _payroll.PersonnelCode);
            AddRow(layout, 2, "ماه:", TranslationHelper.GetMonthName(_payroll.Month), "سال:", StringHelper.ConvertToPersianDigits(_payroll.Year.ToString()));

            var line = new Label { Text = new string('-', 100), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.LightGray, Font = UIHelper.PersianFont(8f) };
            layout.SetColumnSpan(line, 4);
            layout.Controls.Add(line, 0, 3);

            // ─── بخش مزایا ───
            var lblEarnings = new Label { Text = "مزایا (دریافتی‌ها)", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(12f, FontStyle.Bold), ForeColor = UIHelper.ActionGreen };
            layout.SetColumnSpan(lblEarnings, 4);
            layout.Controls.Add(lblEarnings, 0, 4);

            AddRow(layout, 5, "حقوق پایه:", NumberHelper.FormatThousands(_payroll.BaseSalary, usePersianDigits: true),
                             "پایه سنوات:", NumberHelper.FormatThousands(_payroll.YearsBonus, usePersianDigits: true));

            AddRow(layout, 6, "حق مسکن:", NumberHelper.FormatThousands(_payroll.HousingAllowance, usePersianDigits: true),
                             "حق خواربار:", NumberHelper.FormatThousands(_payroll.GroceryAllowance, usePersianDigits: true));

            AddRow(layout, 7, "حق اولاد:", NumberHelper.FormatThousands(_payroll.ChildAllowance, usePersianDigits: true),
                             "حق تاهل:", NumberHelper.FormatThousands(_payroll.MarriageAllowance, usePersianDigits: true));

            AddRow(layout, 8, "حق جذب:", NumberHelper.FormatThousands(_payroll.AttractionAllowance, usePersianDigits: true),
                             "حق مسئولیت:", NumberHelper.FormatThousands(_payroll.ResponsibilityAllowance, usePersianDigits: true));

            AddRow(layout, 9, "حق مدیریت:", NumberHelper.FormatThousands(_payroll.ManagementAllowance, usePersianDigits: true),
                             "حق فنی:", NumberHelper.FormatThousands(_payroll.TechnicalAllowance, usePersianDigits: true));

            AddRow(layout, 10, "حق ایاب و ذهاب:", NumberHelper.FormatThousands(_payroll.TransportationAllowance, usePersianDigits: true),
                              "سایر مزایا:", NumberHelper.FormatThousands(_payroll.OtherAllowances, usePersianDigits: true));

            AddRow(layout, 11, "اضافه‌کاری:", NumberHelper.FormatThousands(_payroll.OvertimePay, usePersianDigits: true), "", "");

            // ─── بخش کسورات ───
            var lblDeductions = new Label { Text = "کسورات", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(12f, FontStyle.Bold), ForeColor = UIHelper.ActionRed };
            layout.SetColumnSpan(lblDeductions, 4);
            layout.Controls.Add(lblDeductions, 0, 12);

            AddRow(layout, 13, "بیمه سهم کارمند (7%):", NumberHelper.FormatThousands(_payroll.InsuranceEmployee, usePersianDigits: true),
                              "مالیات بر درآمد:", NumberHelper.FormatThousands(_payroll.IncomeTax, usePersianDigits: true));

            int currentRow = 14;

            if (_payroll.SupplementaryInsuranceDeduction > 0)
            {
                AddRow(layout, currentRow++, "بیمه تکمیلی (سهم کارمند):", NumberHelper.FormatThousands(_payroll.SupplementaryInsuranceDeduction, usePersianDigits: true), "", "");
            }
            if (_payroll.CarInsuranceDeduction > 0)
            {
                AddRow(layout, currentRow++, "کسر بیمه خودرو:", NumberHelper.FormatThousands(_payroll.CarInsuranceDeduction, usePersianDigits: true), "", "");
            }
            if (_payroll.LoanDeduction > 0)
            {
                AddRow(layout, currentRow++, "کسر وام:", NumberHelper.FormatThousands(_payroll.LoanDeduction, usePersianDigits: true), "", "");
            }
            if (_payroll.OtherDeductions > 0)
            {
                AddRow(layout, currentRow++, "سایر کسورات:", NumberHelper.FormatThousands(_payroll.OtherDeductions, usePersianDigits: true), "", "");
            }

            // ─── جمع کل ───
            var lblTotals = new Label
            {
                Text = $"جمع کل مزایا: {NumberHelper.FormatThousands(_payroll.TotalEarnings, usePersianDigits: true)} ریال  |  جمع کل کسورات: {NumberHelper.FormatThousands(_payroll.TotalDeductions, usePersianDigits: true)} ریال",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                Padding = new Padding(10)
            };
            layout.SetColumnSpan(lblTotals, 4);
            layout.Controls.Add(lblTotals, 0, 19);

            var lblNet = new Label
            {
                Text = $"خالص پرداختی: {NumberHelper.FormatThousands(_payroll.NetPay, usePersianDigits: true)} ریال",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(16f, FontStyle.Bold),
                ForeColor = UIHelper.SectionBlue,
                Padding = new Padding(10)
            };
            layout.SetColumnSpan(lblNet, 4);
            layout.Controls.Add(lblNet, 0, 20);

            mainPanel.Controls.Add(layout);
            this.Controls.Add(mainPanel);
            this.Controls.Add(topPanel);
        }

        private void AddRow(TableLayoutPanel layout, int row, string label1, string value1, string label2, string value2)
        {
            layout.Controls.Add(new Label { Text = value1, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UIHelper.PersianFont(11f, FontStyle.Bold) }, 0, row);
            layout.Controls.Add(new Label { Text = label1, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f) }, 1, row);

            if (!string.IsNullOrEmpty(label2))
            {
                layout.Controls.Add(new Label { Text = value2, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UIHelper.PersianFont(11f, FontStyle.Bold) }, 2, row);
                layout.Controls.Add(new Label { Text = label2, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f) }, 3, row);
            }
            else
            {
                layout.Controls.Add(new Label { Text = "", Dock = DockStyle.Fill }, 2, row);
                layout.Controls.Add(new Label { Text = "", Dock = DockStyle.Fill }, 3, row);
            }
        }

        private async Task LoadLogoAsync()
        {
            try
            {
                var repo = new CompanyRepository(AppConfig.ConnectionString);
                var company = await Task.Run(() => repo.GetAll().FirstOrDefault());

                if (company != null && company.Logo != null && company.Logo.Length > 0)
                {
                    var picLogo = mainPanel.Controls.Find("picLogo", true).FirstOrDefault() as PictureBox;
                    if (picLogo != null)
                    {
                        picLogo.Image = ImageHelper.ByteArrayToImage(company.Logo);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning("خطا در بارگذاری لوگو در فیش حقوقی: " + ex.Message, "PayslipForm");
            }
        }

        private void BtnPrint_Click(object? sender, EventArgs e)
        {
            try
            {
                _memoryImage?.Dispose();
                _memoryImage = new Bitmap(mainPanel.Width, mainPanel.Height);
                mainPanel.DrawToBitmap(_memoryImage, new Rectangle(0, 0, mainPanel.Width, mainPanel.Height));

                using var pd = new PrintDocument();
                pd.PrintPage += PrintPage;
                pd.DefaultPageSettings.Landscape = false;

                using var previewDialog = new PrintPreviewDialog
                {
                    Document = pd,
                    Width = 900,
                    Height = 700,
                    StartPosition = FormStartPosition.CenterScreen,
                    Text = "پیش‌نمایش چاپ فیش حقوقی",
                    RightToLeft = RightToLeft.Yes,
                    RightToLeftLayout = true
                };

                previewDialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PayslipForm.BtnPrint");
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