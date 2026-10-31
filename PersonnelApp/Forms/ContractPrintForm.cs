using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Data;
using PersonnelApp.Models;
using PersonnelApp.Helpers;

namespace PersonnelApp.Forms
{
    public partial class ContractPrintForm : Form
    {
        private readonly Employee _employee;
        private readonly PrintDocument _printDocument;
        private int _pageNumber = 1;

        private Company? _company;

        public ContractPrintForm(Employee employee)
        {
            InitializeComponent();
            _employee = employee;

            _printDocument = new PrintDocument();
            _printDocument.DefaultPageSettings.Landscape = false;
            _printDocument.DefaultPageSettings.Margins = new Margins(70, 70, 70, 70);
            _printDocument.BeginPrint += (s, ev) => { _pageNumber = 1; };
            _printDocument.PrintPage += PrintDocument_PrintPage;

            UIHelper.SetupForm(this, "پیش‌نمایش چاپ قرارداد", new Size(900, 700));
            this.ShowInTaskbar = false;

            this.FormClosed += (s, e) => _printDocument?.Dispose();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // 👈 اصلاح شد: به جای مینیمایز کردن فرم، فقط پنجره پیش‌نمایش را باز می‌کنیم
            try
            {
                var companyRepo = new CompanyRepository(AppConfig.ConnectionString);
                var companies = await Task.Run(() => companyRepo.GetAll());

                if (_employee.CompanyId.HasValue)
                {
                    _company = companies.FirstOrDefault(c => c.Id == _employee.CompanyId.Value);
                }

                // اگر کارمند به شرکت خاصی وصل نبود، اولین شرکت سیستم را به عنوان پیش‌فرض در نظر بگیر
                if (_company == null)
                {
                    _company = companies.FirstOrDefault();
                }

                using var preview = new PrintPreviewDialog
                {
                    Document = _printDocument,
                    WindowState = FormWindowState.Maximized,
                    BackColor = UIHelper.WarmBackground,
                    ForeColor = UIHelper.WarmText,
                    Font = UIHelper.PersianFont(11f),
                    RightToLeft = RightToLeft.Yes,
                    RightToLeftLayout = true,
                    Text = "پیش‌نمایش قرارداد کار"
                };

                preview.ShowDialog(this);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ContractPrintForm.ShowPreview");
                ModernMessageBox.ShowError("خطا در باز کردن پیش‌نمایش چاپ:\n" + ex.Message);
            }
            finally
            {
                this.Close();
            }
        }

        private void ContractPrintForm_Load(object sender, EventArgs e)
        {
            // متد خالی برای رفع خطای Designer
        }

        private float DrawParagraph(Graphics g, string text, Font font, float left, float y, float width, StringFormat format)
        {
            SizeF size = g.MeasureString(text, font, (int)width, format);
            g.DrawString(text, font, Brushes.Black, new RectangleF(left, y, width, size.Height), format);
            return y + size.Height + 1;
        }

        private float DrawSegments(Graphics g, List<(string Text, bool IsBold)> segments,
            Font bodyFont, Font boldFont, float rightX, float startY, float maxWidth)
        {
            float currentX = rightX;
            float currentY = startY;
            float lineHeight = bodyFont.GetHeight(g) + 1;

            using StringFormat format = new StringFormat(StringFormatFlags.DirectionRightToLeft);

            foreach (var seg in segments)
            {
                Font font = seg.IsBold ? boldFont : bodyFont;
                string[] words = seg.Text.Split(' ');
                for (int i = 0; i < words.Length; i++)
                {
                    string word = words[i];
                    if (string.IsNullOrEmpty(word)) continue;

                    string wordWithSpace = word + (i < words.Length - 1 ? " " : "");
                    SizeF size = g.MeasureString(wordWithSpace, font);

                    if (currentX - size.Width < rightX - maxWidth && currentX != rightX)
                    {
                        currentY += lineHeight;
                        currentX = rightX;
                    }

                    g.DrawString(wordWithSpace, font, Brushes.Black, new PointF(currentX, currentY), format);
                    currentX -= size.Width;
                }
            }
            return currentY + lineHeight + 1;
        }

        private void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics!;
            float yPos = e.MarginBounds.Top;
            float leftMargin = e.MarginBounds.Left;
            float rightMargin = e.MarginBounds.Right;
            float width = e.MarginBounds.Width;

            float padding = 15f;
            float topExtra = 40f;
            float bottomExtra = 20f;

            RectangleF outerBorder = new RectangleF(
                e.MarginBounds.Left - padding,
                e.MarginBounds.Top - topExtra,
                e.MarginBounds.Width + (padding * 2),
                e.MarginBounds.Height + topExtra + bottomExtra);

            using (Pen outerPen = new Pen(Color.Black, 1.5f))
            {
                g.DrawRectangle(outerPen, outerBorder.X, outerBorder.Y, outerBorder.Width, outerBorder.Height);
            }

            using (Pen innerPen = new Pen(Color.Black, 1f))
            {
                RectangleF innerBorder = new RectangleF(
                    outerBorder.X + 4,
                    outerBorder.Y + 4,
                    outerBorder.Width - 8,
                    outerBorder.Height - 8);
                g.DrawRectangle(innerPen, innerBorder.X, innerBorder.Y, innerBorder.Width, innerBorder.Height);
            }

            var titleFont = UIHelper.PersianFont(15, FontStyle.Bold);
            var headerFont = UIHelper.PersianFont(11.5f, FontStyle.Bold);
            var bodyFont = UIHelper.PersianFont(10f, FontStyle.Regular);
            var boldFont = UIHelper.PersianFont(10f, FontStyle.Bold);
            var bulletFont = UIHelper.PersianFont(9f, FontStyle.Regular);
            var tableFont = UIHelper.PersianFont(10.5f, FontStyle.Bold);
            var tableBoldFont = UIHelper.PersianFont(11f, FontStyle.Bold);
            var pageNumFont = UIHelper.PersianFont(8, FontStyle.Regular);

            using StringFormat rtlFormat = new StringFormat
            {
                FormatFlags = StringFormatFlags.DirectionRightToLeft,
                Alignment = StringAlignment.Near
            };
            using StringFormat centerFormat = new StringFormat
            {
                FormatFlags = StringFormatFlags.DirectionRightToLeft,
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            using StringFormat leftRtlFormat = new StringFormat
            {
                FormatFlags = StringFormatFlags.DirectionRightToLeft,
                Alignment = StringAlignment.Far
            };
            using Pen pen = new Pen(Color.Black, 1);

            g.DrawString($"صفحه {_pageNumber} از 2", pageNumFont, Brushes.Black, new RectangleF(rightMargin - 60, e.MarginBounds.Top - 30, 60, 20), rtlFormat);

            if (_pageNumber == 1)
            {
                string title = "\u200Fقرارداد کار (مدت موقت)\u200F";
                g.DrawString(title, titleFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 30), centerFormat);
                yPos += 35;

                float topLeftX = leftMargin + 60;
                float topLeftWidth = 200;

                g.DrawString("شماره:                           ", bodyFont, Brushes.Black, new RectangleF(topLeftX, yPos, topLeftWidth, 20), leftRtlFormat);
                yPos += 20;
                g.DrawString("تاریخ:                           ", bodyFont, Brushes.Black, new RectangleF(topLeftX, yPos, topLeftWidth, 20), leftRtlFormat);
                yPos += 25;

                // 👈 اصلاح شد: استفاده از اطلاعات شرکت از دیتابیس
                string companyName = _company?.Name ?? "شرکت ثبت نشده";
                string companyNationalId = _company?.NationalId ?? "................";
                string companyAddress = _company?.Address ?? "آدرس ثبت نشده";

                var introSegs = new List<(string, bool)>
                {
                    ("این قرارداد بین ", false),
                    ($"{companyName}", true),
                    (" به شناسه ملی ", false),
                    ($"{companyNationalId}", true),
                    (" و آدرس: ", false),
                    ($"{companyAddress}", true),
                    (" که به اختصار کارفرما نامیده می‌شود و خانم / آقای: ", false),

                    ($"{_employee.FirstName} {_employee.LastName}", true),
                    (" فرزند: ", false),
                    ($"{_employee.FatherName ?? ".........."}", true),
                    (" کد ملی: ", false),
                    ($"{_employee.NationalCode ?? ".........."}", true),
                    (" به شماره شناسنامه: ", false),
                    ($"{_employee.BirthCertificateNumber ?? ".........."}", true),
                    (" صادره از: ", false),
                    ($"{_employee.IssuancePlace ?? ".........."}", true),
                    (" متولد: ", false),

                    ($"{DateHelper.ToPersianDate(_employee.BirthDate)}", true),

                    (" با وضعیت تأهل: ", false),
                    ($"{_employee.MaritalStatus ?? ".........."}", true),
                    (" دارای مدرک تحصیلی: ", false),
                    ($"{_employee.Education ?? ".........."}", true),
                    (" رشته: ", false),
                    ($"{_employee.SpecializedField ?? ".........."}", true),
                    (" به نشانی: ", false),
                    ($"{_employee.Address ?? "...................................................."}", false),
                    (" تلفن: ", false),
                    ($"{_employee.PhoneNumber ?? ".........."}", true),
                    (" که از این پس کارپذیر نامیده می‌شود، منعقد می‌گردد.", false)
                };
                yPos = DrawSegments(g, introSegs, bodyFont, boldFont, rightMargin, yPos, width);
                yPos += 2;

                yPos = DrawParagraph(g, "تبصره 1: این قرارداد جهت مدت موقت با توجه به بند (و) از ماده 10 قانون کار بین طرفین تنظیم گردیده است.", boldFont, leftMargin, yPos, width, rtlFormat);
                yPos += 2;

                g.DrawString("ماده 1) موضوع قرارداد:", headerFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), rtlFormat);
                yPos += 22;

                var clause1Segs = new List<(string, bool)>
                {
                    ("خدمت در واحد سازمانی ", false),
                    ($"{_employee.DepartmentName ?? "........"}", true),
                    (" با عنوان ", false),
                    ($"{_employee.Position ?? "........"}", true),
                    (" به آدرس ", false),
                    ($"{_employee.DepartmentAddress ?? companyAddress}", true),
                    (" می باشد.", false)
                };
                yPos = DrawSegments(g, clause1Segs, bodyFont, boldFont, rightMargin, yPos, width);

                yPos = DrawParagraph(g, "تبصره 2: کارپذیر متعهد است وظایف محوله از سوی کارفرما را با رعایت مقررات و طبق شرح وظایف ابلاغ شده به ایشان انجام دهد.", boldFont, leftMargin, yPos, width, rtlFormat);
                yPos += 2;

                g.DrawString("ماده 2) مدت قرارداد:", headerFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), rtlFormat);
                yPos += 22;

                var clause2Segs = new List<(string, bool)>
                {
                    ("مدت این قرارداد از تاریخ ", false),
                    ($"{DateHelper.ToPersianDate(_employee.ContractStartDate)}", true),
                    (" لغایت ", false),
                    ($"{DateHelper.ToPersianDate(_employee.ContractEndDate)}", true),
                    (" می‌باشد که در پایان مدت، در صورت تایید عملکرد و نیاز کارفرما و رضایت طرفین قابل تمدید می‌باشد در غیر این صورت، عدم نیاز به کارپذیر و خاتمه قرارداد تلقی می‌گردد.", false)
                };
                yPos = DrawSegments(g, clause2Segs, bodyFont, boldFont, rightMargin, yPos, width);

                yPos = DrawParagraph(g, "تبصره 3: این قرارداد از ابتدا به مدت 3 ماه جنبه آزمایشی دارد و در حین و یا در پایان مدت آزمایشی چنانچه نحوه خدمت کارپذیر مورد تایید کارفرما نباشد به این قرارداد خاتمه داده خواهد شد و کارپذیر حق هیچگونه اعتراضی نخواهد داشت.", boldFont, leftMargin, yPos, width, rtlFormat);
                yPos += 2;

                g.DrawString("ماده 3) ساعت کاری:", headerFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), rtlFormat);
                yPos += 22;
                yPos = DrawParagraph(g, "ساعت حضور و فعالیت در محل کار برابر یک شیفت می‌باشد. (طبق ضوابط و مقررات اداره کار معادل 44 ساعت در هفته و 176 ساعت در ماه می باشد) که در صورت کاهش به همان نسبت از حقوق و مزایای ماهیانه کسر خواهد شد.", bodyFont, leftMargin, yPos, width, rtlFormat);
                yPos += 2;

                g.DrawString("ماده 4) حقوق و مزایا:", headerFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), rtlFormat);
                yPos += 22;

                decimal total = _employee.BaseSalary + _employee.YearsBonus + _employee.ManagementAllowance + _employee.ResponsibilityAllowance + _employee.TechnicalAllowance + _employee.AttractionAllowance + _employee.GroceryAllowance + _employee.HousingAllowance + _employee.ChildAllowance + _employee.MarriageAllowance + _employee.OtherAllowances;

                decimal dailyWage = _employee.BaseSalary / 30;

                var clause4Segs = new List<(string, bool)>
                {
                    ("حقوق و مزایای مندرج در این ماده طبق جدول زیر جمعاً به مبلغ ", false),
                    ($"{NumberHelper.FormatThousands(total, usePersianDigits: true)} ریال", true),
                    (" که پس از تایید گزارش کار توسط امور اداری و کسر کسورات قانونی و با لحاظ نمودن مفاد ماده 3، در صورت تامین منابع مالی در پایان هر ماه قابل پرداخت می باشد.", false)
                };
                yPos = DrawSegments(g, clause4Segs, bodyFont, boldFont, rightMargin, yPos, width);
                yPos += 5;

                g.DrawString($"دستمزد روزانه: {NumberHelper.FormatThousands(dailyWage, usePersianDigits: true)} ریال", boldFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), leftRtlFormat);
                yPos += 25;

                float[] colWidths = { width * 0.35f, width * 0.15f, width * 0.35f, width * 0.15f };
                float rowHeight = 24;

                float midX = rightMargin - colWidths[0] - colWidths[1];

                g.DrawLine(pen, rightMargin, yPos, rightMargin, yPos + (6 * rowHeight));
                g.DrawLine(pen, midX, yPos, midX, yPos + (6 * rowHeight));
                g.DrawLine(pen, leftMargin, yPos, leftMargin, yPos + (6 * rowHeight));

                for (int i = 0; i <= 6; i++)
                    g.DrawLine(pen, leftMargin, yPos + (i * rowHeight), rightMargin, yPos + (i * rowHeight));

                string[,] tableData = {
                    { "1 - حقوق مبنا (30 روزه)", $"{NumberHelper.FormatThousands(_employee.BaseSalary, usePersianDigits: true)} ریال", "7 - کمک هزینه خوار و بار و بن", $"{NumberHelper.FormatThousands(_employee.GroceryAllowance, usePersianDigits: true)} ریال" },
                    { "2 - پایه سنوات", $"{NumberHelper.FormatThousands(_employee.YearsBonus, usePersianDigits: true)} ریال", "8 - کمک هزینه مسکن", $"{NumberHelper.FormatThousands(_employee.HousingAllowance, usePersianDigits: true)} ریال" },
                    { "3 - حق مدیریت", $"{NumberHelper.FormatThousands(_employee.ManagementAllowance, usePersianDigits: true)} ریال", "9 - کمک هزینه عائله مندی", $"{NumberHelper.FormatThousands(_employee.ChildAllowance, usePersianDigits: true)} ریال" },
                    { "4 - حق مسئولیت", $"{NumberHelper.FormatThousands(_employee.ResponsibilityAllowance, usePersianDigits: true)} ریال", "10 - حق تاهل", $"{NumberHelper.FormatThousands(_employee.MarriageAllowance, usePersianDigits: true)} ریال" },
                    { "5 - حق فنی", $"{NumberHelper.FormatThousands(_employee.TechnicalAllowance, usePersianDigits: true)} ریال", "11 - سایر مزایا", $"{NumberHelper.FormatThousands(_employee.OtherAllowances, usePersianDigits: true)} ریال" },
                    { "6 - حق جذب", $"{NumberHelper.FormatThousands(_employee.AttractionAllowance, usePersianDigits: true)} ریال", "جمع حقوق و مزایا", $"{NumberHelper.FormatThousands(total, usePersianDigits: true)} ریال" }
                };

                for (int row = 0; row < 6; row++)
                {
                    float rowY = yPos + (row * rowHeight);
                    Font currentFont = (row == 5) ? tableBoldFont : tableFont;

                    g.DrawString(tableData[row, 0], currentFont, Brushes.Black, new RectangleF(rightMargin - colWidths[0], rowY, colWidths[0], rowHeight), rtlFormat);
                    g.DrawString(tableData[row, 1], currentFont, Brushes.Black, new RectangleF(midX, rowY, colWidths[1], rowHeight), rtlFormat);
                    g.DrawString(tableData[row, 2], currentFont, Brushes.Black, new RectangleF(midX - colWidths[2], rowY, colWidths[2], rowHeight), rtlFormat);
                    g.DrawString(tableData[row, 3], currentFont, Brushes.Black, new RectangleF(leftMargin, rowY, colWidths[3], rowHeight), rtlFormat);
                }

                yPos += (6 * rowHeight) + 8;

                g.DrawString("ماده 5) تعهدات کارپذیر:", headerFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), rtlFormat);
                yPos += 22;

                yPos = DrawParagraph(g, "1- با توجه به ماده 10 قانون مدنی کارپذیر متعهد می‌شود، ضمن رعایت مقررات انضباطی محیط کار، کلیه تعهدات مندرج در این قرارداد و شرح وظایف ابلاغ شده به ایشان و همچنین قوانین مرتبط با موضوع فعالیت شرکت را به انجام رساند.", bodyFont, leftMargin, yPos, width, rtlFormat);

                yPos = DrawParagraph(g, "تبصره 4: در صورت بروز هرگونه تخلف و یا عدم اجرای هر یک از مفاد این قرارداد بخصوص موضوع تبصره 2 (شرح وظایف) و یا غیبت‌های مکرر و یا تاخیر چه عمداً چه سهواً که موجب ایجاد هرگونه خسارت و یا ضرر و زیان ناشی از سوء عملکرد کارپذیر باشد، می‌بایستی نسبت به پاسخگویی به کارفرما تا رفع مشکل آن باشد. بدیهی است در غیر اینصورت کارفرما مجاز به فسخ یک طرفه قرارداد بوده و کارپذیر حق هیچگونه اعتراضی نخواهد داشت.", boldFont, leftMargin, yPos, width, rtlFormat);

                yPos = DrawParagraph(g, "2- چنانچه کارپذیر به هر دلیل تمایل به ادامه همکاری با کارفرما را نداشته باشد مکلف می‌باشد (حداقل یک ماه) قبل استعفای کتبی خود را به شرکت تقدیم دارد، تا در صورت موافقت، کارفرما برای تعیین جانشین مناسب اقدامات لازم را به انجام برساند. همچنین موظف است قبل از قطع رابطه کاری با رعایت حفظ منابع و منافع شرکت امور مورد نظر در قرارداد (موضوع تبصره ماده یک) را به روز رسانیده و طی صورتجلسه‌ای نسبت به تحویل و تحول کارها، اطلاعات و امور و تجهیزات در اختیار اقدام نماید.", bodyFont, leftMargin, yPos, width, rtlFormat);

                _pageNumber++;
                e.HasMorePages = true;
            }
            else if (_pageNumber == 2)
            {
                yPos = DrawParagraph(g, "3- کارپذیر اقرار می‌نماید دارای صلاحیت‌های قضایی و انتظامی و اخلاقی، دینی و سلامت جسمی و روانی بوده و برای انجام این قرارداد تجربه، تخصص، فن و اطلاعات کامل را تماماً دارا بوده و هیچگونه ابهام و ناآگاهی از مفاد این قرارداد یا عدم آگاهی با نوع کار واگذار شده فوق را نداشته و قادر و صالح و آماده به کار می‌باشد و اعتراف می‌نماید که همچنان بدون هیچگونه عذری ملزم به اجرای کلیه مسئولیت‌های واگذاری به وی می‌باشد.", bodyFont, leftMargin, yPos, width, rtlFormat);

                yPos = DrawParagraph(g, "تبصره 5: کارپذیر به هیچ وجه حق واگذاری تمام یا قسمتی از کار را به غیر ندارد.", boldFont, leftMargin, yPos, width, rtlFormat);

                yPos = DrawParagraph(g, "4- کارپذیر شخصاً مسئول حفظ و نگهداری کلیه اسناد، مدارک، اموال و تجهیزاتی می‌باشد که در طول مدت همکاری در اختیار داشته و یا چنانچه در اثر ناآگاهی مفقود یا انتقال به اشخاص یا واحدهای دیگر و یا دچار خسارت به کارفرما شود متعهد می‌گردد تا میزان واقعی آن هزینه‌های مربوطه را پاسخگو باشد.", bodyFont, leftMargin, yPos, width, rtlFormat);
                yPos += 2;

                g.DrawString("ماده 6) بیمه:", headerFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), rtlFormat);
                yPos += 22;
                yPos = DrawParagraph(g, "به موجب ماده 148 قانون کار کارفرما مکلف است کارپذیر را نزد سازمان تامین اجتماعی یا سایر دستگاه‌های بیمه‌گذار بیمه نماید.", bodyFont, leftMargin, yPos, width, rtlFormat);
                yPos += 2;

                g.DrawString("ماده 7) موارد فسخ قرارداد:", headerFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), rtlFormat);
                yPos += 22;
                yPos = DrawParagraph(g, "این قرارداد در موارد زیر و سایر موارد پیش‌بینی نشده توسط کارفرما بصورت یک‌طرفه قابل فسخ است و فسخ قرارداد 5 روز قبل به کارپذیر اعلام می‌شود:", bodyFont, leftMargin, yPos, width, rtlFormat);

                string bullets = "• هرگونه تخلف از آیین‌نامه‌های شرکت و افشاء اسرار و اطلاعات محرمانه شرکت.\n" +
                                 "• خرید و فروش، حمل و نگهداری، مصرف و توزیع مواد مخدر و روان گردان و استعمال دخانیات در داخل یا خارج از محیط کار.\n" +
                                 "• کاهش نقدینگی شرکت و رکود در اثر شرایط اقتصادی و اجتماعی و سیاسی یا تغییرات ساختاری و فن‌آوری در سطح شرکت.\n" +
                                 "• تمرد از اجرای دستورات سازمانی، عدم رعایت شئونات اخلاقی، نزاع، درگیری و هرنوع توهین و هتک حرمت فیزیکی یا لفظی به مافوق و همکاران.\n" +
                                 "• کم‌کاری، سهل‌انگاری و بی‌دقتی در انجام وظایف و غیبت غیرموجه بیش از 5 روز در یک ماه و تاخیرهای مکرر و پی در پی.\n" +
                                 "• تعارض و سوء استفاده از مرخصی استعلاجی و خوابیدن در محل کار و ساعات موظف کاری.\n" +
                                 "• عدم استفاده از وسایل و ابزار ایمنی و انجام اعمال ناایمن و پرخطر در محیط کار.\n" +
                                 "• اعمال و رفتار مغایر با شئونات اسلامی که به اعتبار و جایگاه شرکت در خارج از سازمان لطمه وارد کند.\n" +
                                 "• استفاده از تلفن همراه در محیط کار به منظور مکالمه، ارسال پیامک، بلوتوث، فیلم و عکسبرداری و هرنوع استفاده متعارف یا غیرمتعارف دیگر.\n" +
                                 "• اخذ رشوه، جعل اسناد و امضاء و مخدوش نمودن اوراق، اسناد و مدارک شرکت و یا ارائه مدارک تقلبی جهت استخدام یا در حین خدمت.\n" +
                                 "• تهدید، ارعاب و اعتصاب فردی و گروهی و یا اقدام و ترغیب کارکنان به کم‌کاری.\n" +
                                 "• عدم توانایی و کارایی لازم جهت انجام وظایف محوله طبق ارزیابی‌های انجام شده و عدم تبعیت از برنامه‌های شرکت.\n" +
                                 "• سرقت و ایراد خسارت عمدی، سوء استفاده از اموال و دارایی‌های شرکت و استفاده از وسایل و تجهیزات شرکت جهت مصارف شخصی.\n" +
                                 "• تعدیل نیرو به هر علت و دلیل که از طرف کارفرما اتخاذ می‌گردد.";

                yPos = DrawParagraph(g, bullets, bulletFont, leftMargin, yPos, width, rtlFormat);
                yPos += 2;

                yPos = DrawParagraph(g, "تبصره 6: احراز تحقق هریک از موارد فوق، بمنظور فسخ قرارداد صرفاً بنا به تشخیص کارفرما می‌باشد و کارپذیر حق هیچگونه اعتراضی را نخواهد داشت.", boldFont, leftMargin, yPos, width, rtlFormat);
                yPos += 2;

                g.DrawString("ماده 8) حل اختلاف:", headerFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), rtlFormat);
                yPos += 22;
                yPos = DrawParagraph(g, "اختلافات ناشی از اجرای این قرارداد، بدواً از طریق سازش بین طرفین و در صورت عدم سازش در مراجع حل اختلاف مندرج در قانون کار حل و فصل خواهد شد.", bodyFont, leftMargin, yPos, width, rtlFormat);
                yPos += 2;

                g.DrawString("ماده 9) سایر:", headerFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), rtlFormat);
                yPos += 22;
                yPos = DrawParagraph(g, "سایر موضوعات مندرج در قانون کار و مقررات تبعی از جمله مرخصی استحقاقی، نسبت به این قرارداد اعمال خواهد شد و همچنین بقیه مواردی که در این قرارداد پیش‌بینی نشده بر اساس قانون کار و سایر قوانین و مقررات مربوطه خواهد بود.", bodyFont, leftMargin, yPos, width, rtlFormat);
                yPos += 2;

                g.DrawString("ماده 10) تعداد نسخ:", headerFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), rtlFormat);
                yPos += 22;
                yPos = DrawParagraph(g, "این قرارداد در 10 ماده و 6 تبصره و در 4 نسخه که در هر کدام حکم واحد را دارا می‌باشد تنظیم که پس از امضای طرفین و مهر کارفرما معتبر خواهد بود.", bodyFont, leftMargin, yPos, width, rtlFormat);
                yPos += 2;

                var finalSegs = new List<(string, bool)>
                {
                    ("اینجانب ", false),
                    ($"{_employee.FirstName} {_employee.LastName}", true),
                    (" با توجه به قبول تصدی امور فوق کلیه مسئولیت‌های قانونی مرتبط را در طول مدت همکاری به عهده می‌گیرم و با علم کامل بر مفاد این قرارداد خود را ملزم به انجام بندبند موارد مندرج در قرارداد به نحو مطلوب در حین و پایان کار می‌دانم و آنرا امضاء نموده و خارج از مواد مندرج در قرارداد حق طرح هیچگونه اعتراضی را در آینده، چه در شرکت و یا سایر مراجع نخواهم داشت.", false)
                };
                yPos = DrawSegments(g, finalSegs, bodyFont, boldFont, rightMargin, yPos, width);
                yPos += 10;

                g.DrawString("امضاء کارفرما                                                                         امضاء کارپذیر", boldFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), rtlFormat);
                yPos += 20;

                yPos = DrawParagraph(g, "این قرارداد بنا به درخواست کارپذیر و تایید کارفرما از تاریخ ................ لغایت ................ تمدید میگردد. ضمناً کلیه مفاد قرارداد فوق کماکان به قوت خود باقی است.", bodyFont, leftMargin, yPos, width, rtlFormat);
                yPos += 20;

                g.DrawString("امضاء کارفرما                                                                         امضاء کارپذیر", boldFont, Brushes.Black, new RectangleF(leftMargin, yPos, width, 20), rtlFormat);

                e.HasMorePages = false;
            }
        }
    }
}