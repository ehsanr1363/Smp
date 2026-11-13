using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using Microsoft.Data.SqlClient; // 👈 برای خواندن مستقیم آدرس از دیتابیس

namespace SMP.Forms
{
    public partial class ContractPrintForm : Form
    {
        private Employee _employee;
        private WebBrowser browser = null!;

        public ContractPrintForm(Employee employee)
        {
            _employee = employee;
            BuildUI();
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Maximized;
            this.Text = $"پیش‌نمایش و چاپ قرارداد - {employee.FullName}";
        }

        private void BuildUI()
        {
            browser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                IsWebBrowserContextMenuEnabled = false,
                WebBrowserShortcutsEnabled = false,
                AllowWebBrowserDrop = false,
                ScriptErrorsSuppressed = true
            };

            this.Controls.Add(browser);
            this.Shown += async (s, e) => await GenerateHtmlAsync();
        }

        private async Task GenerateHtmlAsync()
        {
            // ─── ۱. به‌روزرسانی اطلاعات کارمند از دیتابیس ───
            try
            {
                var empRepo = new EmployeeRepository(AppConfig.ConnectionString);
                var dbEmp = empRepo.GetById(_employee.Id);
                if (dbEmp != null) _employee = dbEmp;
            }
            catch { }

            // ─── ۲. خواندن مستقیم آدرس واحد سازمانی در صورت خالی بودن ───
            if (string.IsNullOrWhiteSpace(_employee.DepartmentAddress) && _employee.DepartmentId.HasValue)
            {
                try
                {
                    using var conn = new SqlConnection(AppConfig.ConnectionString);
                    await conn.OpenAsync();
                    using var cmd = new SqlCommand("SELECT Address FROM Departments WHERE Id = @Id", conn);
                    cmd.Parameters.AddWithValue("@Id", _employee.DepartmentId.Value);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res != null && res != DBNull.Value)
                    {
                        _employee.DepartmentAddress = res.ToString();
                    }
                }
                catch { }
            }

            // ─── ۳. خواندن قرارداد فعال و تنظیمات سال ───
            EmployeeContract? activeContract = null;
            SalarySettings? settings = null;
            try
            {
                var contractRepo = new EmployeeContractRepository(AppConfig.ConnectionString);
                activeContract = await contractRepo.GetActiveContractByEmployeeIdAsync(_employee.Id);

                int year = _employee.ContractYear ?? new System.Globalization.PersianCalendar().GetYear(DateTime.Now);
                var settingsRepo = new SalarySettingsRepository(AppConfig.ConnectionString);
                settings = await settingsRepo.GetByYearAsync(year);
            }
            catch { }

            // ─── ۴. استخراج دقیق مبالغ ───
            decimal baseSalary = _employee.BaseSalary > 0 ? _employee.BaseSalary : (activeContract?.BaseSalary ?? 0);
            decimal yearsBonus = _employee.YearsBonus > 0 ? _employee.YearsBonus : (activeContract?.YearsBonus ?? 0);
            decimal management = _employee.ManagementAllowance > 0 ? _employee.ManagementAllowance : (activeContract?.ManagementAllowance ?? 0);
            decimal responsibility = _employee.ResponsibilityAllowance > 0 ? _employee.ResponsibilityAllowance : (activeContract?.ResponsibilityAllowance ?? 0);
            decimal technical = _employee.TechnicalAllowance > 0 ? _employee.TechnicalAllowance : (activeContract?.TechnicalAllowance ?? 0);
            decimal attraction = _employee.AttractionAllowance > 0 ? _employee.AttractionAllowance : (activeContract?.AttractionAllowance ?? 0);
            decimal other = _employee.OtherAllowances > 0 ? _employee.OtherAllowances : (activeContract?.OtherAllowances ?? 0);

            decimal housing = _employee.HousingAllowance > 0 ? _employee.HousingAllowance : (activeContract?.HousingAllowance ?? 0);
            if (housing == 0 && settings != null) housing = settings.HousingAllowance;

            decimal grocery = _employee.GroceryAllowance > 0 ? _employee.GroceryAllowance : (activeContract?.GroceryAllowance ?? 0);
            if (grocery == 0 && settings != null) grocery = settings.FoodAllowance;

            decimal marriage = _employee.MarriageAllowance > 0 ? _employee.MarriageAllowance : (activeContract?.MarriageAllowance ?? 0);
            if (marriage == 0 && settings != null)
            {
                string marital = StringHelper.NormalizePersianText(_employee.MaritalStatus ?? "");
                if (marital.Contains("متاهل") || marital.Contains("متأهل"))
                    marriage = settings.MarriageAllowance;
            }

            decimal child = _employee.ChildAllowance > 0 ? _employee.ChildAllowance : (activeContract?.ChildAllowance ?? 0);
            if (child == 0 && settings != null && _employee.ChildrenCount > 0)
            {
                decimal childRate = settings.ChildAllowanceRate > 1 ? settings.ChildAllowanceRate / 100m : settings.ChildAllowanceRate;
                child = Math.Round(settings.MinimumWage * childRate * _employee.ChildrenCount, 0);
            }

            // ─── ۵. محاسبه جمع کل ───
            decimal totalSalary = baseSalary + yearsBonus + management + responsibility + technical + attraction + other + grocery + housing + child + marriage;
            decimal dailyWage = baseSalary > 0 ? Math.Round(baseSalary / 30m, 0) : 0;

            var templateRepo = new ContractTemplateRepository(AppConfig.ConnectionString);
            string customText = await templateRepo.GetActiveTemplateContentAsync() ?? "";

            string logoBase64 = "";
            try
            {
                var companyRepo = new CompanyRepository(AppConfig.ConnectionString);
                var company = companyRepo.GetAll().FirstOrDefault();
                if (company != null && company.Logo != null && company.Logo.Length > 0)
                    logoBase64 = Convert.ToBase64String(company.Logo);
            }
            catch { }

            string logoHtml = string.IsNullOrEmpty(logoBase64) ? "" : $"<img src='data:image/png;base64,{logoBase64}' style='max-height: 60px;' />";

            string additionalTextHtml = "";
            if (!string.IsNullOrWhiteSpace(customText))
            {
                string formattedText = customText.Replace("\r\n", "<br>").Replace("\n", "<br>");
                additionalTextHtml = "<div style='margin-top: 10px; color: #c00000; font-weight: bold;'>شرایط و توضیحات اضافی:</div><div style='text-align: justify;'>" + formattedText + "</div>";
            }

            // آدرس در صورت خالی بودن، با خط تیره نمایش داده می‌شود
            string deptAddress = string.IsNullOrWhiteSpace(_employee.DepartmentAddress) ? "دفتر مرکزی" : _employee.DepartmentAddress;
            string workLocation = string.IsNullOrWhiteSpace(_employee.WorkLocation) ? "دفتر مرکزی" : _employee.WorkLocation;

            // ─── ۶. ساخت قالب HTML ───
            string html = $@"
            <html>
            <head>
                <meta http-equiv='X-UA-Compatible' content='IE=edge' />
                <style>
                    @page {{ size: A4; margin: 8mm; }}
                    
                    html, body {{ 
                        font-family: 'B Nazanin', 'Tahoma', sans-serif; 
                        font-size: 12px; 
                        direction: rtl; 
                        margin: 0; padding: 0; 
                    }}
                    
                    .page-container {{ 
                        width: 100%; 
                        border: 2px solid black; 
                        padding: 10px;
                        box-sizing: border-box;
                    }}

                    .page-break {{ 
                        page-break-before: always; 
                    }}
                    
                    p, li {{ 
                        margin: 4px 0; 
                        padding: 0;
                        text-align: justify; 
                        line-height: 1.4; 
                        word-wrap: break-word; 
                    }}
                    ul {{ margin: 4px 0; padding-right: 15px; }}

                    .no-border {{ border: none !important; }}
                    .no-border td {{ border: none !important; padding: 2px; }}
                    .blue-text {{ color: #1F497D; font-weight: bold; margin-top: 8px; margin-bottom: 2px; }}
                    
                    .salary-table {{ 
                        width: 100%; 
                        border-collapse: collapse; 
                        table-layout: fixed; 
                        margin: 8px 0;
                    }}
                    .salary-table td {{ 
                        border: 1px solid black; 
                        padding: 4px 6px; 
                        font-size: 11.5px; 
                    }}
                    
                    .signature-table {{ width: 100%; margin-top: 20px; }}
                    .signature-table td {{ border: none; text-align: center; font-weight: bold; padding-top: 10px; }}
                </style>
            </head>
            <body>

                <!-- ═══════════ صفحه ۱ ═══════════ -->
                <div class='page-container'>
                    
                    <table style='width: 100%; border-bottom: 2px solid black; margin-bottom: 15px;'>
                        <tr>
                            <td style='width: 20%; text-align: right; vertical-align: top;'>
                                {logoHtml}
                            </td>
                            <td style='width: 60%; text-align: center; vertical-align: middle;'>
                                <div style='font-size: 16px; font-weight: bold;'>قرارداد کار (مدت موقت)</div>
                            </td>
                            <td style='width: 20%; text-align: left; vertical-align: top; padding-top: 5px;'>
                                <div style='font-size: 11px; line-height: 1.6;'>
                                    صفحه 1 از 2<br>
                                    شماره: .........................<br>
                                    تاریخ: .........................
                                </div>
                            </td>
                        </tr>
                    </table>

                    <p>این قرارداد بین <b>شرکت مهندسی ستاوند شمس (سهامی خاص)</b> به شماره ثبت: 76723 و آدرس: مشهد - بلوار سجاد 3 - نبش میلاد 2 - پلاک 72 که به اختصار کارفرما نامیده می‌شود و خانم / آقای: <b>{_employee.FirstName} {_employee.LastName}</b> فرزند: <b>{_employee.FatherName}</b> کد ملی: <b>{_employee.NationalCode}</b> به شماره شناسنامه: <b>{_employee.BirthCertificateNumber}</b> صادره از: <b>{_employee.IssuancePlace}</b> متولد: <b>{DateHelper.ToPersianDate(_employee.BirthDate)}</b> با وضعیت تأهل: <b>{_employee.MaritalStatus}</b> دارای مدرک تحصیلی: <b>{_employee.Education}</b> رشته: <b>{_employee.SpecializedField}</b> به نشانی: <b>{_employee.Address}</b> تلفن: <b>{_employee.PhoneNumber}</b> که از این پس کارپذیر نامیده می‌شود، منعقد می‌گردد.</p>

                    <div class='blue-text'>تبصره1 : این قرارداد جهت مدت موقت با توجه به بند (و) از ماده 10 قانون کار بین طرفین تنظیم گردیده است.</div>

                    <div class='blue-text'>ماده 1) موضوع قرارداد:</div>
                    <!-- 🔴 آدرس واحد سازمانی اینجا جایگذاری شد -->
                    <p style='text-align: right;'>خدمت در واحد سازمانی <b>{_employee.DepartmentName}</b> با عنوان <b>{_employee.Position}</b> در <b>{workLocation}</b> به آدرس <b>{deptAddress}</b> می باشد.</p>
                    <div class='blue-text'>تبصره2 : کارپذیر متعهد است وظایف محوله از سوی کارفرما را با رعایت مقررات و طبق شرح وظایف ابلاغ شده به ایشان انجام دهد.</div>

                    <div class='blue-text'>ماده 2) مدت قرارداد:</div>
                    <p>مدت این قرارداد از تاریخ <b>{DateHelper.ToPersianDate(_employee.ContractStartDate)}</b> لغایت <b>{DateHelper.ToPersianDate(_employee.ContractEndDate)}</b> می‌باشد که در پایان مدت، در صورت تایید عملکرد و نیاز کارفرما و رضایت طرفین قابل تمدید می‌باشد در غیر این صورت، عدم نیاز به کارپذیر و خاتمه قرارداد تلقی می‌گردد.</p>
                    <div class='blue-text'>تبصره3 : این قرارداد از ابتدا به مدت 3 ماه جنبه آزمایشی دارد و در حین و یا در پایان مدت آزمایشی چنانچه نحوه خدمت کارپذیر مورد تایید کارفرما نباشد به این قرارداد خاتمه داده خواهد شد و کارپذیر حق هیچگونه اعتراضی نخواهد داشت.</div>

                    <div class='blue-text'>ماده 3) ساعت کاری:</div>
                    <p>ساعت حضور و فعالیت در محل کار برابر یک شیفت می‌باشد.(طبق ضوابط و مقررات اداره کار معادل 44 ساعت در هفته و 176 ساعت در ماه می باشد) که در صورت کاهش به همان نسبت از حقوق و مزایای ماهیانه کسر خواهد شد.</p>

                    <div class='blue-text'>ماده 4) حقوق و مزایا:</div>
                    <p>حقوق و مزایای مندرج در این ماده طبق جدول زیر جمعاً به مبلغ <b>{NumberHelper.FormatThousands(totalSalary, usePersianDigits: true)}</b> ریال که پس از تایید گزارش کار توسط امور اداری و کسر کسورات قانونی و با لحاظ نمودن مفاد ماده 3، در صورت تامین منابع مالی در پایان هر ماه قابل پرداخت می باشد.</p>

                    <div style='text-align: left; margin-bottom: 4px; font-weight: bold;'>
                        دستمزد روزانه : {NumberHelper.FormatThousands(dailyWage, usePersianDigits: true)} ریال
                    </div>

                    <table class='salary-table'>
                        <tr>
                            <td style='text-align:right; width:30%;'>1 - حقوق مبنا ( 30 روزه )</td>
                            <td style='text-align:center; width:20%;'>{NumberHelper.FormatThousands(baseSalary, usePersianDigits: true)} ریال</td>
                            <td style='text-align:right; width:30%;'>7 - کمک هزینه خوار و بار و بن</td>
                            <td style='text-align:center; width:20%;'>{NumberHelper.FormatThousands(grocery, usePersianDigits: true)} ریال</td>
                        </tr>
                        <tr>
                            <td style='text-align:right;'>2 - پایه سنوات</td>
                            <td style='text-align:center;'>{NumberHelper.FormatThousands(yearsBonus, usePersianDigits: true)} ریال</td>
                            <td style='text-align:right;'>8 - کمک هزینه مسکن</td>
                            <td style='text-align:center;'>{NumberHelper.FormatThousands(housing, usePersianDigits: true)} ریال</td>
                        </tr>
                        <tr>
                            <td style='text-align:right;'>3 - حق مدیریت</td>
                            <td style='text-align:center;'>{NumberHelper.FormatThousands(management, usePersianDigits: true)} ریال</td>
                            <td style='text-align:right;'>9 - کمک هزینه عائله مندی</td>
                            <td style='text-align:center;'>{NumberHelper.FormatThousands(child, usePersianDigits: true)} ریال</td>
                        </tr>
                        <tr>
                            <td style='text-align:right;'>4 - حق مسئولیت</td>
                            <td style='text-align:center;'>{NumberHelper.FormatThousands(responsibility, usePersianDigits: true)} ریال</td>
                            <td style='text-align:right;'>10 - حق تاهل</td>
                            <td style='text-align:center;'>{NumberHelper.FormatThousands(marriage, usePersianDigits: true)} ریال</td>
                        </tr>
                        <tr>
                            <td style='text-align:right;'>5 - حق فنی</td>
                            <td style='text-align:center;'>{NumberHelper.FormatThousands(technical, usePersianDigits: true)} ریال</td>
                            <td style='text-align:right;'>11 - سایر مزایا</td>
                            <td style='text-align:center;'>{NumberHelper.FormatThousands(other, usePersianDigits: true)} ریال</td>
                        </tr>
                        <tr>
                            <td style='text-align:right;'>6 - حق جذب</td>
                            <td style='text-align:center;'>{NumberHelper.FormatThousands(attraction, usePersianDigits: true)} ریال</td>
                            <td style='text-align:right; font-weight:bold;'>جمع حقوق و مزایا :</td>
                            <td style='text-align:center; font-weight:bold;'>{NumberHelper.FormatThousands(totalSalary, usePersianDigits: true)} ریال</td>
                        </tr>
                    </table>

                    <div class='blue-text'>ماده 5) تعهدات کارپذیر:</div>
                    <p>1- با توجه به ماده 10 قانون مدنی کارپذیر متعهد می‌شود، ضمن رعایت مقررات انضباطی محیط کار، کلیه تعهدات مندرج در این قرارداد و شرح وظایف ابلاغ شده به ایشان و همچنین قوانین مرتبط با موضوع فعالیت شرکت را به انجام رساند.</p>
                    <p class='blue-text' style='text-align: justify;'>تبصره4 : در صورت بروز هرگونه تخلف و یا عدم اجرای هر یک از مفاد این قرارداد بخصوص موضوع تبصره 2 (شرح وظایف) و یا غیبت های مکرر و یا تاخیر چه عمداً چه سهواً که موجب ایجاد هرگونه خسارت و یا ضرر و زیان ناشی از سوء عملکرد کارپذیر باشد، می‌بایستی نسبت به پاسخگویی به کارفرما تا رفع مشکل آن باشد. بدیهی است در غیر اینصورت کارفرما مجاز به فسخ یک طرفه قرارداد بوده و کارپذیر حق هیچگونه اعتراضی نخواهد داشت.</p>
                    <p>2- چنانچه کارپذیر به هر دلیل تمایل به ادامه همکاری با کارفرما را نداشته باشد مکلف می‌باشد (حداقل یک ماه) قبل استعفای کتبی خود را به شرکت تقدیم دارد، تا در صورت موافقت، کارفرما برای تعیین جانشین مناسب اقدامات لازم را به انجام برساند. همچنین موظف است قبل از قطع رابطه کاری با رعایت حفظ منابع و منافع شرکت امور مورد نظر در قرارداد (موضوع تبصره ماده یک) را به روز رسانیده و طی صورتجلسه‌ای نسبت به تحویل و تحول کارها، اطلاعات و امور و تجهیزات در اختیار اقدام نماید.</p>
                    <p>3- کارپذیر اقرار می‌نماید دارای صلاحیت‌های قضایی و انتظامی و اخلاقی، دینی و سلامت جسمی و روانی بوده و برای انجام این قرارداد تجربه، تخصص، فن و اطلاعات کامل را تماماً دارا بوده و هیچگونه ابهام و ناآگاهی از مفاد این قرارداد یا عدم آگاهی با نوع کار واگذار شده فوق را نداشته و قادر و صالح و آماده به کار می‌باشد و اعتراف می‌نماید که همچنان بدون هیچگونه عذری ملزم به اجرای کلیه مسئولیت های واگذاری به وی می‌باشد.</p>
                    <p>4- کارپذیر شخصاً مسئول حفظ و نگهداری کلیه اسناد، مدارک، اموال و تجهیزاتی می‌باشد که در طول مدت همکاری در اختیار داشته و یا چنانچه در اثر ناآگاهی مفقود یا انتقال به اشخاص یا واحدهای دیگر و یا دچار خسارت به کارفرما شود متعهد میگردد تا میزان واقعی آن هزینه‌های مربوطه را پاسخگو باشد.</p>
                </div>

                <!-- ═══════════ صفحه ۲ ═══════════ -->
                <div class='page-container page-break'>
                    
                    <table style='width: 100%; border-bottom: 2px solid black; margin-bottom: 15px;'>
                        <tr>
                            <td style='width: 20%; text-align: right; vertical-align: top;'>
                                {logoHtml}
                            </td>
                            <td style='width: 60%; text-align: center; vertical-align: middle;'>
                                <div style='font-size: 16px; font-weight: bold;'>قرارداد کار (مدت موقت)</div>
                            </td>
                            <td style='width: 20%; text-align: left; vertical-align: top; padding-top: 5px;'>
                                <div style='font-size: 11px;'>صفحه 2 از 2</div>
                            </td>
                        </tr>
                    </table>

                    <div class='blue-text'>تبصره5 : کارپذیر به هیچ وجه حق واگذاری تمام یا قسمتی از کار را به غیر ندارد.</div>

                    <div class='blue-text'>ماده 6) بیمه:</div>
                    <p>به موجب ماده 148 قانون کار کارفرما مکلف است کارپذیر را نزد سازمان تامین اجتماعی یا سایر دستگاه های بیمه گذار بیمه نماید.</p>

                    <div class='blue-text'>ماده 7) : موارد فسخ قرارداد:</div>
                    <p>این قرارداد در موارد زیر و سایر موارد پیش‌بینی نشده توسط کارفرما بصورت یک‌طرفه قابل فسخ است و فسخ قرارداد 5 روز قبل به کارپذیر اعلام می شود:</p>
                    <ul>
                        <li>هرگونه تخلف از آیین نامه های شرکت و افشاء اسرار و اطلاعات محرمانه شرکت.</li>
                        <li>خرید و فروش، حمل و نگهداری، مصرف و توزیع مواد مخدر و روان گردان و استعمال دخانیات در داخل یا خارج از محیط کار.</li>
                        <li>کاهش نقدینگی شرکت و رکود در اثر شرایط اقتصادی و اجتماعی و سیاسی یا تغییرات ساختاری و فن آوری در سطح شرکت.</li>
                        <li>تمرد از اجرای دستورات سازمانی، عدم رعایت شئونات اخلاقی، نزاع، درگیری و هرنوع توهین و هتک حرمت فیزیکی یا لفظی به مافوق و همکاران.</li>
                        <li>کم کاری، سهل انگاری و بی دقتی در انجام وظایف و غیبت غیرموجه بیش از 5 روز در یک ماه و تاخیرهای مکرر و پی در پی.</li>
                        <li>تعارض و سوء استفاده از مرخصی استعلاجی و خوابیدن در محل کار و ساعات موظف کاری.</li>
                        <li>عدم استفاده از وسایل و ابزار ایمنی و انجام اعمال نا ایمن و پرخطر در محیط کار.</li>
                        <li>اعمال و رفتار مغایر با شئونات اسلامی که به اعتبار و جایگاه شرکت در خارج از سازمان لطمه وارد کند.</li>
                        <li>استفاده از تلفن همراه در محیط کار به منظور مکالمه، ارسال پیامک، بلوتوث، فیلم و عکسبرداری و هرنوع استفاده متعارف یا غیرمتعارف دیگر.</li>
                        <li>اخذ رشوه، جعل اسناد و امضاء و مخدوش نمودن اوراق، اسناد و مدارک شرکت و یا ارائه مدارک تقلبی جهت استخدام یا در حین خدمت.</li>
                        <li>تهدید، ارعاب و اعتصاب فردی و گروهی و یا اقدام و ترغیب کارکنان به کم کاری.</li>
                        <li>عدم توانایی و کارایی لازم جهت انجام وظایف محوله طبق ارزیابی های انجام شده و عدم تبعیت از برنامه های شرکت.</li>
                        <li>سرقت و ایراد خسارت عمدی، سوء استفاده از اموال و دارایی های شرکت و استفاده از وسایل و تجهیزات شرکت جهت مصارف شخصی.</li>
                        <li>تعدیل نیرو به هر علت و دلیل که از طرف کارفرما اتخاذ می‌گردد.</li>
                    </ul>
                    <div class='blue-text'>تبصره6 : احراز تحقق هریک از موارد فوق، بمنظور فسخ قرارداد صرفاً بنا به تشخیص کارفرما می‌باشد و کارپذیر حق هیچگونه اعتراضی را نخواهد داشت.</div>

                    <div class='blue-text'>ماده 8) حل اختلاف:</div>
                    <p>اختلافات ناشی از اجرای این قرارداد، بدواً از طریق سازش بین طرفین و در صورت عدم سازش در مراجع حل اختلاف مندرج در قانون کار حل و فصل خواهد شد.</p>

                    <div class='blue-text'>ماده 9) سایر:</div>
                    <p>سایر موضوعات مندرج در قانون کار و مقررات تبعی از جمله مرخصی استحقاقی، نسبت به این قرارداد اعمال خواهد شد و همچنین بقیه مواردی که در این قرارداد پیش بینی نشده بر اساس قانون کار و سایر قوانین و مقررات مربوطه خواهد بود.</p>

                    {additionalTextHtml}

                    <div class='blue-text'>ماده 10): تعداد نسخ:</div>
                    <p>این قرارداد در 10 ماده و 6 تبصره و در 4 نسخه که در هر کدام حکم واحد را دارا می‌باشد تنظیم که پس از امضای طرفین و مهر کارفرما معتبر خواهد بود.</p>

                    <p style='margin-top: 15px;'>اینجانب <b>{_employee.FirstName} {_employee.LastName}</b> با توجه به قبول تصدی امور فوق کلیه مسئولیت‌های قانونی مرتبط را در طول مدت همکاری به عهده می‌گیرم و با علم کامل بر مفاد این قرارداد خود را ملزم به انجام بندبند موارد مندرج در قرارداد به نحو مطلوب در حین و پایان کار می‌دانم و آنرا امضاء نموده و خارج از مواد مندرج در قرارداد حق طرح هیچگونه اعتراضی را در آینده، چه در شرکت و یا سایر مراجع نخواهم داشت.</p>

                    <table class='signature-table'>
                        <tr>
                            <td>امضاء کارفرما</td>
                            <td>امضاء کارپذیر</td>
                        </tr>
                    </table>

                    <p style='margin-top: 20px;'>این قرارداد بنا به درخواست کارپذیر و تایید کارفرما از تاریخ ......................... لغایت ......................... تمدید میگردد. ضمناً کلیه مفاد قرارداد فوق کماکان به قوت خود باقی است.</p>

                    <table class='signature-table'>
                        <tr>
                            <td>امضاء کارفرما</td>
                            <td>امضاء کارپذیر</td>
                        </tr>
                    </table>
                </div>
            </body>
            </html>";

            browser.DocumentText = html;

            browser.DocumentCompleted += (s, e) =>
            {
                browser.Document.Body.Style = "zoom: 100%;";
                browser.ShowPrintPreviewDialog();
            };
        }
    }
}