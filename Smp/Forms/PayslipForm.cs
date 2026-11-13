using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
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

            // تنظیم سایز فرم به صورت فشرده تا فضای خالی نماند
            UIHelper.SetupForm(this, $"فیش حقوقی - {payroll.EmployeeName}", new Size(850, 720));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnPrint);

            Shown += async (s, e) => await LoadLogoAsync();
        }

        private void BuildUI()
        {
            var btnPrintPanel = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };
            btnPrint = UIHelper.CreateButton("🖨️ چاپ فیش حقوقی", UIHelper.SectionBlue, 180, 35);
            btnPrint.Dock = DockStyle.Right;
            btnPrint.Click += BtnPrint_Click;
            btnPrintPanel.Controls.Add(btnPrint);

            // استفاده از AutoScroll برای مواقعی که پنجره کوچک می‌شود
            var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Padding = new Padding(20) };
            mainPanel = new Panel { Dock = DockStyle.Top, AutoSize = true, BackColor = Color.White };

            // هدر فیش
            var headerPanel = new Panel { Dock = DockStyle.Top, Height = 80 };
            var lblCompany = new Label { Text = "شرکت مهندسی ستاوند شمس", Font = UIHelper.PersianFont(14f, FontStyle.Bold), Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter, Height = 30, ForeColor = Color.Black };
            var lblDocTitle = new Label { Text = "فیش حقوق و مزایای ماهانه", Font = UIHelper.PersianFont(16f, FontStyle.Bold), ForeColor = UIHelper.SectionBlue, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            headerPanel.Controls.Add(lblDocTitle);
            headerPanel.Controls.Add(lblCompany);

            // جدول اصلی
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                AutoSize = true,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            // ردیف 1: اطلاعات پرسنل
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.Controls.Add(new Label { Text = $"نام: {_payroll.EmployeeName}", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(5, 0, 5, 0) }, 0, 0);
            layout.Controls.Add(new Label { Text = $"کد: {_payroll.PersonnelCode}", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(5, 0, 5, 0) }, 1, 0);
            layout.SetColumnSpan(layout.GetControlFromPosition(1, 0)!, 1);

            layout.Controls.Add(new Label { Text = $"ماه: {TranslationHelper.GetMonthName(_payroll.Month)}", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(5, 0, 5, 0) }, 2, 0);
            layout.Controls.Add(new Label { Text = $"سال: {StringHelper.ConvertToPersianDigits(_payroll.Year.ToString())}", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(5, 0, 5, 0) }, 3, 0);

            // ردیف 2: هدر جدول
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.Controls.Add(new Label { Text = "شرح مزایا", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(11f, FontStyle.Bold), BackColor = Color.FromArgb(245, 245, 245) }, 0, 1);
            layout.Controls.Add(new Label { Text = "مبلغ (ریال)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(11f, FontStyle.Bold), BackColor = Color.FromArgb(245, 245, 245) }, 1, 1);
            layout.Controls.Add(new Label { Text = "شرح کسورات", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(11f, FontStyle.Bold), BackColor = Color.FromArgb(245, 245, 245) }, 2, 1);
            layout.Controls.Add(new Label { Text = "مبلغ (ریال)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(11f, FontStyle.Bold), BackColor = Color.FromArgb(245, 245, 245) }, 3, 1);

            // داده‌ها
            var benefits = new List<(string Name, decimal Amount)>
            {
                ("حقوق پایه", _payroll.BaseSalary),
                ("پایه سنوات", _payroll.YearsBonus),
                ("حق مسکن", _payroll.HousingAllowance),
                ("حق خواربار", _payroll.GroceryAllowance),
                ("حق اولاد", _payroll.ChildAllowance),
                ("حق تاهل", _payroll.MarriageAllowance),
                ("حق جذب", _payroll.AttractionAllowance),
                ("حق مسئولیت", _payroll.ResponsibilityAllowance),
                ("حق مدیریت", _payroll.ManagementAllowance),
                ("حق فنی", _payroll.TechnicalAllowance),
                ("حق ایاب و ذهاب", _payroll.TransportationAllowance),
                ("سایر مزایا", _payroll.OtherAllowances),
                ("اضافه‌کاری", _payroll.OvertimePay)
            };

            var deductions = new List<(string Name, decimal Amount)>
            {
                ("بیمه سهم کارمند", _payroll.InsuranceEmployee),
                ("مالیات بر درآمد", _payroll.IncomeTax),
                ("کسر وام", _payroll.LoanDeduction),
                ("بیمه تکمیلی", _payroll.SupplementaryInsuranceDeduction),
                ("بیمه خودرو", _payroll.CarInsuranceDeduction),
                ("سایر کسورات", _payroll.OtherDeductions)
            };

            int maxRows = Math.Max(benefits.Count, deductions.Count);
            for (int i = 0; i < maxRows; i++)
            {
                layout.RowCount++;
                int row = i + 2;
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));

                if (i < benefits.Count)
                {
                    layout.Controls.Add(new Label { Text = benefits[i].Name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(5, 0, 5, 0) }, 0, row);
                    layout.Controls.Add(new Label { Text = NumberHelper.FormatThousands(benefits[i].Amount, usePersianDigits: true), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 1, row);
                }

                if (i < deductions.Count)
                {
                    layout.Controls.Add(new Label { Text = deductions[i].Name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(5, 0, 5, 0) }, 2, row);
                    layout.Controls.Add(new Label { Text = NumberHelper.FormatThousands(deductions[i].Amount, usePersianDigits: true), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 3, row);
                }
            }

            // جمع کل
            layout.RowCount++;
            int totalRow = maxRows + 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            layout.Controls.Add(new Label { Text = "جمع مزایا", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), BackColor = Color.FromArgb(240, 253, 244), Padding = new Padding(5, 0, 5, 0) }, 0, totalRow);
            layout.Controls.Add(new Label { Text = NumberHelper.FormatThousands(_payroll.TotalEarnings, usePersianDigits: true), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(11f, FontStyle.Bold), BackColor = Color.FromArgb(240, 253, 244) }, 1, totalRow);
            layout.Controls.Add(new Label { Text = "جمع کسورات", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), BackColor = Color.FromArgb(254, 242, 242), Padding = new Padding(5, 0, 5, 0) }, 2, totalRow);
            layout.Controls.Add(new Label { Text = NumberHelper.FormatThousands(_payroll.TotalDeductions, usePersianDigits: true), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(11f, FontStyle.Bold), BackColor = Color.FromArgb(254, 242, 242) }, 3, totalRow);

            // خالص پرداختی
            layout.RowCount++;
            int netRow = totalRow + 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            var lblNet = new Label { Text = $"خالص پرداختی: {NumberHelper.FormatThousands(_payroll.NetPay, usePersianDigits: true)} ریال", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(13f, FontStyle.Bold), ForeColor = Color.White, BackColor = UIHelper.ActionGreen };
            layout.Controls.Add(lblNet, 0, netRow);
            layout.SetColumnSpan(lblNet, 4);

            // مبلغ به حروف
            layout.RowCount++;
            int wordsRow = netRow + 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            var lblWords = new Label { Text = $"مبلغ قابل پرداخت به حروف: {NumberHelper.ToPersianWords(_payroll.NetPay)} ریال", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Padding = new Padding(10, 5, 5, 5) };
            layout.Controls.Add(lblWords, 0, wordsRow);
            layout.SetColumnSpan(lblWords, 4);

            mainPanel.Controls.Add(layout);
            mainPanel.Controls.Add(headerPanel);
            scrollPanel.Controls.Add(mainPanel);

            this.Controls.Add(scrollPanel);
            this.Controls.Add(btnPrintPanel);
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
                        picLogo.Image = ImageHelper.ByteArrayToImage(company.Logo);
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