using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using Smp.Data;
using Smp.Models;

namespace Smp.Helpers;

/// <summary>
/// ساخت HTML چاپی قرارداد پرسنلی (مشترک بین چاپ تکی و چاپ گروهی).
/// هر قرارداد = ۲ صفحه‌ی A4.
/// </summary>
internal static class ContractHtmlBuilder
{
    public const string Css = @"
                    @page { size: A4; margin: 8mm; }

                    html, body {
                        font-family: 'B Nazanin', 'Tahoma', sans-serif;
                        font-size: 11.5px;
                        direction: rtl;
                        color: #1b1b1b;
                        margin: 0; padding: 0;
                    }

                    .page-container {
                        width: 100%;
                        border: 1.5px solid #1F497D;
                        position: relative;
                        height: 252mm;
                        padding: 6px 12px 4px 12px;
                        box-sizing: border-box;
                    }
                    .page-break { page-break-before: always; }

                    p, li {
                        margin: 2px 0;
                        padding: 0;
                        text-align: justify;
                        line-height: 1.4;
                        word-wrap: break-word;
                    }
                    p b { color: #0f2e57; }
                    ul { margin: 3px 0; padding-right: 18px; }
                    li { margin: 1px 0; }

                    /* ── سربرگ ── */
                    .hdr { width: 100%; border-collapse: collapse; border-bottom: 3px double #1F497D; margin-bottom: 5px; }
                    .hdr td { padding: 0 0 4px 0; vertical-align: middle; }
                    .doc-title { font-size: 17px; font-weight: bold; color: #1F497D; }
                    .doc-sub { font-size: 11px; color: #666; margin-top: 2px; }
                    .meta { border-collapse: collapse; font-size: 11px; }
                    .meta td { border: none; padding: 1px 4px; white-space: nowrap; }
                    .meta td.k { color: #1F497D; font-weight: bold; }

                    /* ── عنوان ماده و تبصره ── */
                    .article {
                        color: #1F497D; font-weight: bold; font-size: 12.5px;
                        background: #eef3f9; border-right: 4px solid #1F497D;
                        padding: 1px 8px; margin: 6px 0 2px 0;
                    }
                    .note {
                        color: #5a4300; background: #fff8e6; border-right: 3px solid #d9a400;
                        padding: 1px 8px; margin: 3px 0; line-height: 1.4; text-align: justify;
                    }

                    /* ── جدول حقوق ── */
                    .wage {
                        display: inline-block; border: none; background: none;
                        color: #1F497D; font-weight: bold; padding: 1px 0; margin: 2px 0;
                    }
                    .salary-table { width: 100%; border-collapse: collapse; table-layout: fixed; margin: 3px 0 5px 0; }
                    .salary-table td { border: 1px solid #9fb0c8; padding: 2px 8px; font-size: 11px; }
                    .salary-table .th td { background: #1F497D; color: #ffffff; font-weight: bold; text-align: center; border-color: #1F497D; }
                    .salary-table .lbl { background: #f3f6fa; text-align: right; }
                    .salary-table .amt { text-align: center; font-weight: bold; }
                    .salary-table .tot { background: #dfe8f5; color: #0f2e57; font-weight: bold; }

                    /* ── امضاها ── */
                    .signature-table { width: 100%; border-collapse: separate; border-spacing: 14px 0; margin-top: 4px; }
                    .signature-table td {
                        border: none; text-align: center; font-weight: bold; color: #1F497D;
                        height: 42px; vertical-align: top; padding-top: 2px; width: 50%;
                    }
                    .footer { position: absolute; bottom: 5px; left: 0; width: 100%; text-align: center; font-size: 10px; color: #666; }
                ";

    /// <summary>HTML کامل از مجموع صفحه‌های یک یا چند قرارداد</summary>
    public static string WrapDocument(string pagesHtml)
        => "<html><head><meta http-equiv='X-UA-Compatible' content='IE=edge' /><meta charset='utf-8' /><style>" + Css + "</style></head><body>" + pagesHtml + "</body></html>";

    /// <summary>
    /// لوگوی شرکت. اگر logoFilePath داده شود، لوگو یک بار در فایل موقت ذخیره و با آدرس فایل نمایش داده می‌شود
    /// (برای چاپ گروهی، تا حجم HTML به‌ازای هر قرارداد زیاد نشود).
    /// </summary>
    public static string LoadLogoHtml(string? logoFilePath = null)
    {
        try
        {
            var companyRepo = new CompanyRepository(AppConfig.ConnectionString);
            var company = companyRepo.GetAll().FirstOrDefault();
            if (company is null || company.Logo is null || company.Logo.Length == 0) return "";

            if (!string.IsNullOrEmpty(logoFilePath))
            {
                File.WriteAllBytes(logoFilePath, company.Logo);
                string uri = new Uri(logoFilePath).AbsoluteUri;
                return $"<img src='{uri}' style='max-height: 60px;' />";
            }
            return $"<img src='data:image/png;base64,{Convert.ToBase64String(company.Logo)}' style='max-height: 60px;' />";
        }
        catch { return ""; }
    }

    public static async Task<string> LoadCustomTextAsync()
    {
        try
        {
            var templateRepo = new ContractTemplateRepository(AppConfig.ConnectionString);
            return await templateRepo.GetActiveTemplateContentAsync() ?? "";
        }
        catch { return ""; }
    }

    /// <summary>صفحه‌های (۲ صفحه) قرارداد یک کارمند. pageBreakBefore=true یعنی قبل از آن صفحه‌ی جدید شروع شود.</summary>
    public static async Task<string> BuildPagesAsync(Employee employee, string logoHtml, string customText, bool pageBreakBefore = false)
    {
        var emp = employee;
        string firstBreak = pageBreakBefore ? " page-break" : "";

        // ─── ۱. به‌روزرسانی اطلاعات کارمند از دیتابیس ───
        try
        {
            var empRepo = new EmployeeRepository(AppConfig.ConnectionString);
            var dbEmp = empRepo.GetById(emp.Id);
            if (dbEmp is not null) emp = dbEmp;
        }
        catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractHtmlBuilder"); }

        // ─── ۲. خواندن مستقیم آدرس واحد سازمانی در صورت خالی بودن ───
        if (string.IsNullOrWhiteSpace(emp.DepartmentAddress) && emp.DepartmentId.HasValue)
        {
            try
            {
                using var conn = new SqlConnection(AppConfig.ConnectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SELECT Address FROM Departments WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", emp.DepartmentId.Value);
                var res = await cmd.ExecuteScalarAsync();
                if (res is not null && res != DBNull.Value)
                {
                    emp.DepartmentAddress = res.ToString();
                }
            }
            catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractHtmlBuilder"); }
        }

        // ─── ۳. خواندن قرارداد فعال و تنظیمات سال ───
        EmployeeContract? activeContract = null;
        SalarySettings? settings = null;
        try
        {
            var contractRepo = new EmployeeContractRepository(AppConfig.ConnectionString);
            activeContract = await contractRepo.GetActiveContractByEmployeeIdAsync(emp.Id);

            int year = emp.ContractYear ?? new System.Globalization.PersianCalendar().GetYear(DateTime.Now);
            var settingsRepo = new SalarySettingsRepository(AppConfig.ConnectionString);
            settings = await settingsRepo.GetByYearAsync(year);
        }
        catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractHtmlBuilder"); }

        // ─── ۴. استخراج دقیق مبالغ ───
        decimal baseSalary = emp.BaseSalary > 0 ? emp.BaseSalary : (activeContract?.BaseSalary ?? 0);
        decimal yearsBonus = emp.YearsBonus > 0 ? emp.YearsBonus : (activeContract?.YearsBonus ?? 0);
        decimal management = emp.ManagementAllowance > 0 ? emp.ManagementAllowance : (activeContract?.ManagementAllowance ?? 0);
        decimal responsibility = emp.ResponsibilityAllowance > 0 ? emp.ResponsibilityAllowance : (activeContract?.ResponsibilityAllowance ?? 0);
        decimal technical = emp.TechnicalAllowance > 0 ? emp.TechnicalAllowance : (activeContract?.TechnicalAllowance ?? 0);
        decimal attraction = emp.AttractionAllowance > 0 ? emp.AttractionAllowance : (activeContract?.AttractionAllowance ?? 0);
        decimal other = emp.OtherAllowances > 0 ? emp.OtherAllowances : (activeContract?.OtherAllowances ?? 0);

        decimal housing = emp.HousingAllowance > 0 ? emp.HousingAllowance : (activeContract?.HousingAllowance ?? 0);
        if (housing == 0 && settings is not null) housing = settings.HousingAllowance;

        decimal grocery = emp.GroceryAllowance > 0 ? emp.GroceryAllowance : (activeContract?.GroceryAllowance ?? 0);
        if (grocery == 0 && settings is not null) grocery = settings.FoodAllowance;

        decimal marriage = emp.MarriageAllowance > 0 ? emp.MarriageAllowance : (activeContract?.MarriageAllowance ?? 0);
        if (marriage == 0 && settings is not null)
        {
            string marital = StringHelper.NormalizePersianText(emp.MaritalStatus ?? "");
            if (marital.Contains("متاهل") || marital.Contains("متأهل"))
                marriage = settings.MarriageAllowance;
        }

        decimal child = emp.ChildAllowance > 0 ? emp.ChildAllowance : (activeContract?.ChildAllowance ?? 0);
        if (child == 0 && settings is not null && emp.ChildrenCount > 0)
        {
            decimal childRate = settings.ChildAllowanceRate > 1 ? settings.ChildAllowanceRate / 100m : settings.ChildAllowanceRate;
            child = Math.Round(settings.MinimumWage * childRate * emp.ChildrenCount, 0);
        }

        // ─── ۵. محاسبه جمع کل ───
        decimal totalSalary = baseSalary + yearsBonus + management + responsibility + technical + attraction + other + grocery + housing + child + marriage;
        decimal dailyWage = baseSalary > 0 ? Math.Round(baseSalary / 30m, 0) : 0;


        string additionalTextHtml = "";
        if (!string.IsNullOrWhiteSpace(customText))
        {
            string formattedText = customText.Replace("\r\n", "<br>").Replace("\n", "<br>");
            additionalTextHtml = "<div class='article'>شرایط و توضیحات اضافی:</div><div style='text-align: justify;'>" + formattedText + "</div>";
        }

        // آدرس در صورت خالی بودن، با خط تیره نمایش داده می‌شود
        string deptAddress = string.IsNullOrWhiteSpace(emp.DepartmentAddress) ? "دفتر مرکزی" : emp.DepartmentAddress;
        string workLocation = string.IsNullOrWhiteSpace(emp.WorkLocation) ? "دفتر مرکزی" : emp.WorkLocation;

        // ─── ۶. ساخت قالب HTML ───

        return $@"
<!-- ═══════════ صفحه ۱ ═══════════ -->
                <div class='page-container{firstBreak}'>
                    
                    <table class='hdr'><tr><td style='width: 20%; text-align: right;'>{logoHtml}</td><td style='width: 60%; text-align: center;'><div class='doc-title'>قرارداد کار (مدت موقت)</div><div class='doc-sub'>شرکت مهندسی ستاوند شمس (سهامی خاص)</div></td><td style='width: 20%; text-align: left;'><table class='meta'><tr><td class='k'>شماره</td><td>.....................</td></tr><tr><td class='k'>تاریخ</td><td>.....................</td></tr></table></td></tr></table>

                    <p>این قرارداد بین <b>شرکت مهندسی ستاوند شمس (سهامی خاص)</b> به شماره ثبت: 76723 و آدرس: مشهد - بلوار سجاد 3 - نبش میلاد 2 - پلاک 72 که به اختصار کارفرما نامیده می‌شود و خانم / آقای: <b>{emp.FirstName} {emp.LastName}</b> فرزند: <b>{emp.FatherName}</b> کد ملی: <b>{emp.NationalCode}</b> به شماره شناسنامه: <b>{emp.BirthCertificateNumber}</b> صادره از: <b>{emp.IssuancePlace}</b> متولد: <b>{DateHelper.ToPersianDate(emp.BirthDate)}</b> با وضعیت تأهل: <b>{emp.MaritalStatus}</b> دارای مدرک تحصیلی: <b>{emp.Education}</b> رشته: <b>{emp.SpecializedField}</b> به نشانی: <b>{emp.Address}</b> تلفن: <b>{emp.PhoneNumber}</b> که از این پس کارپذیر نامیده می‌شود، منعقد می‌گردد.</p>

                    <div class='note'>تبصره1 : این قرارداد جهت مدت موقت با توجه به بند (و) از ماده 10 قانون کار بین طرفین تنظیم گردیده است.</div>

                    <div class='article'>ماده 1) موضوع قرارداد:</div>
                    <!-- 🔴 آدرس واحد سازمانی اینجا جایگذاری شد -->
                    <p style='text-align: right;'>خدمت در واحد سازمانی <b>{emp.DepartmentName}</b> با عنوان <b>{emp.Position}</b> در <b>{workLocation}</b> به آدرس <b>{deptAddress}</b> می باشد.</p>
                    <div class='note'>تبصره2 : کارپذیر متعهد است وظایف محوله از سوی کارفرما را با رعایت مقررات و طبق شرح وظایف ابلاغ شده به ایشان انجام دهد.</div>

                    <div class='article'>ماده 2) مدت قرارداد:</div>
                    <p>مدت این قرارداد از تاریخ <b>{DateHelper.ToPersianDate(emp.ContractStartDate)}</b> لغایت <b>{DateHelper.ToPersianDate(emp.ContractEndDate)}</b> می‌باشد که در پایان مدت، در صورت تایید عملکرد و نیاز کارفرما و رضایت طرفین قابل تمدید می‌باشد در غیر این صورت، عدم نیاز به کارپذیر و خاتمه قرارداد تلقی می‌گردد.</p>
                    <div class='note'>تبصره3 : این قرارداد از ابتدا به مدت 3 ماه جنبه آزمایشی دارد و در حین و یا در پایان مدت آزمایشی چنانچه نحوه خدمت کارپذیر مورد تایید کارفرما نباشد به این قرارداد خاتمه داده خواهد شد و کارپذیر حق هیچگونه اعتراضی نخواهد داشت.</div>

                    <div class='article'>ماده 3) ساعت کاری:</div>
                    <p>ساعت حضور و فعالیت در محل کار برابر یک شیفت می‌باشد.(طبق ضوابط و مقررات اداره کار معادل 44 ساعت در هفته و 176 ساعت در ماه می باشد) که در صورت کاهش به همان نسبت از حقوق و مزایای ماهیانه کسر خواهد شد.</p>

                    <div class='article'>ماده 4) حقوق و مزایا:</div>
                    <p>حقوق و مزایای مندرج در این ماده طبق جدول زیر جمعاً به مبلغ <b>{NumberHelper.FormatThousands(totalSalary, usePersianDigits: true)}</b> ریال که پس از تایید گزارش کار توسط امور اداری و کسر کسورات قانونی و با لحاظ نمودن مفاد ماده 3، در صورت تامین منابع مالی در پایان هر ماه قابل پرداخت می باشد.</p>

                    <div style='text-align: left;'><span class='wage'>دستمزد روزانه : {NumberHelper.FormatThousands(dailyWage, usePersianDigits: true)} ریال</span></div>

                    <table class='salary-table'>
                        <tr class='th'><td style='width:30%;'>شرح</td><td style='width:20%;'>مبلغ</td><td style='width:30%;'>شرح</td><td style='width:20%;'>مبلغ</td></tr>
                        <tr>
                            <td class='lbl'>1 - حقوق مبنا ( 30 روزه )</td>
                            <td class='amt'>{NumberHelper.FormatThousands(baseSalary, usePersianDigits: true)} ریال</td>
                            <td class='lbl'>7 - کمک هزینه خوار و بار و بن</td>
                            <td class='amt'>{NumberHelper.FormatThousands(grocery, usePersianDigits: true)} ریال</td>
                        </tr>
                        <tr>
                            <td class='lbl'>2 - پایه سنوات</td>
                            <td class='amt'>{NumberHelper.FormatThousands(yearsBonus, usePersianDigits: true)} ریال</td>
                            <td class='lbl'>8 - کمک هزینه مسکن</td>
                            <td class='amt'>{NumberHelper.FormatThousands(housing, usePersianDigits: true)} ریال</td>
                        </tr>
                        <tr>
                            <td class='lbl'>3 - حق مدیریت</td>
                            <td class='amt'>{NumberHelper.FormatThousands(management, usePersianDigits: true)} ریال</td>
                            <td class='lbl'>9 - کمک هزینه عائله مندی</td>
                            <td class='amt'>{NumberHelper.FormatThousands(child, usePersianDigits: true)} ریال</td>
                        </tr>
                        <tr>
                            <td class='lbl'>4 - حق مسئولیت</td>
                            <td class='amt'>{NumberHelper.FormatThousands(responsibility, usePersianDigits: true)} ریال</td>
                            <td class='lbl'>10 - حق تاهل</td>
                            <td class='amt'>{NumberHelper.FormatThousands(marriage, usePersianDigits: true)} ریال</td>
                        </tr>
                        <tr>
                            <td class='lbl'>5 - حق فنی</td>
                            <td class='amt'>{NumberHelper.FormatThousands(technical, usePersianDigits: true)} ریال</td>
                            <td class='lbl'>11 - سایر مزایا</td>
                            <td class='amt'>{NumberHelper.FormatThousands(other, usePersianDigits: true)} ریال</td>
                        </tr>
                        <tr>
                            <td class='lbl'>6 - حق جذب</td>
                            <td class='amt'>{NumberHelper.FormatThousands(attraction, usePersianDigits: true)} ریال</td>
                            <td class='lbl tot'>جمع حقوق و مزایا :</td>
                            <td class='amt tot'>{NumberHelper.FormatThousands(totalSalary, usePersianDigits: true)} ریال</td>
                        </tr>
                    </table>

                    <div class='article'>ماده 5) تعهدات کارپذیر:</div>
                    <p>1- با توجه به ماده 10 قانون مدنی کارپذیر متعهد می‌شود، ضمن رعایت مقررات انضباطی محیط کار، کلیه تعهدات مندرج در این قرارداد و شرح وظایف ابلاغ شده به ایشان و همچنین قوانین مرتبط با موضوع فعالیت شرکت را به انجام رساند.</p>
                    <div class='note'>تبصره4 : در صورت بروز هرگونه تخلف و یا عدم اجرای هر یک از مفاد این قرارداد بخصوص موضوع تبصره 2 (شرح وظایف) و یا غیبت های مکرر و یا تاخیر چه عمداً چه سهواً که موجب ایجاد هرگونه خسارت و یا ضرر و زیان ناشی از سوء عملکرد کارپذیر باشد، می‌بایستی نسبت به پاسخگویی به کارفرما تا رفع مشکل آن باشد. بدیهی است در غیر اینصورت کارفرما مجاز به فسخ یک طرفه قرارداد بوده و کارپذیر حق هیچگونه اعتراضی نخواهد داشت.</div>
                    <p>2- چنانچه کارپذیر به هر دلیل تمایل به ادامه همکاری با کارفرما را نداشته باشد مکلف می‌باشد (حداقل یک ماه) قبل استعفای کتبی خود را به شرکت تقدیم دارد، تا در صورت موافقت، کارفرما برای تعیین جانشین مناسب اقدامات لازم را به انجام برساند. همچنین موظف است قبل از قطع رابطه کاری با رعایت حفظ منابع و منافع شرکت امور مورد نظر در قرارداد (موضوع تبصره ماده یک) را به روز رسانیده و طی صورتجلسه‌ای نسبت به تحویل و تحول کارها، اطلاعات و امور و تجهیزات در اختیار اقدام نماید.</p>
                    <p>3- کارپذیر اقرار می‌نماید دارای صلاحیت‌های قضایی و انتظامی و اخلاقی، دینی و سلامت جسمی و روانی بوده و برای انجام این قرارداد تجربه، تخصص، فن و اطلاعات کامل را تماماً دارا بوده و هیچگونه ابهام و ناآگاهی از مفاد این قرارداد یا عدم آگاهی با نوع کار واگذار شده فوق را نداشته و قادر و صالح و آماده به کار می‌باشد و اعتراف می‌نماید که همچنان بدون هیچگونه عذری ملزم به اجرای کلیه مسئولیت های واگذاری به وی می‌باشد.</p>
                    <p>4- کارپذیر شخصاً مسئول حفظ و نگهداری کلیه اسناد، مدارک، اموال و تجهیزاتی می‌باشد که در طول مدت همکاری در اختیار داشته و یا چنانچه در اثر ناآگاهی مفقود یا انتقال به اشخاص یا واحدهای دیگر و یا دچار خسارت به کارفرما شود متعهد میگردد تا میزان واقعی آن هزینه‌های مربوطه را پاسخگو باشد.</p>
                    <div class='footer'>صفحه 1 از 2 &nbsp;&nbsp;&nbsp; قرارداد کار {emp.FirstName} {emp.LastName}</div>
                </div>

                <!-- ═══════════ صفحه ۲ ═══════════ -->
                <div class='page-container page-break'>
                    
                    <table class='hdr'><tr><td style='width: 20%; text-align: right;'>{logoHtml}</td><td style='width: 60%; text-align: center;'><div class='doc-title'>قرارداد کار (مدت موقت)</div><div class='doc-sub'>شرکت مهندسی ستاوند شمس (سهامی خاص)</div></td><td style='width: 20%; text-align: left;'></td></tr></table>

                    <div class='note'>تبصره5 : کارپذیر به هیچ وجه حق واگذاری تمام یا قسمتی از کار را به غیر ندارد.</div>

                    <div class='article'>ماده 6) بیمه:</div>
                    <p>به موجب ماده 148 قانون کار کارفرما مکلف است کارپذیر را نزد سازمان تامین اجتماعی یا سایر دستگاه های بیمه گذار بیمه نماید.</p>

                    <div class='article'>ماده 7) : موارد فسخ قرارداد:</div>
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
                    <div class='note'>تبصره6 : احراز تحقق هریک از موارد فوق، بمنظور فسخ قرارداد صرفاً بنا به تشخیص کارفرما می‌باشد و کارپذیر حق هیچگونه اعتراضی را نخواهد داشت.</div>

                    <div class='article'>ماده 8) حل اختلاف:</div>
                    <p>اختلافات ناشی از اجرای این قرارداد، بدواً از طریق سازش بین طرفین و در صورت عدم سازش در مراجع حل اختلاف مندرج در قانون کار حل و فصل خواهد شد.</p>

                    <div class='article'>ماده 9) سایر:</div>
                    <p>سایر موضوعات مندرج در قانون کار و مقررات تبعی از جمله مرخصی استحقاقی، نسبت به این قرارداد اعمال خواهد شد و همچنین بقیه مواردی که در این قرارداد پیش بینی نشده بر اساس قانون کار و سایر قوانین و مقررات مربوطه خواهد بود.</p>

                    {additionalTextHtml}

                    <div class='article'>ماده 10): تعداد نسخ:</div>
                    <p>این قرارداد در 10 ماده و 6 تبصره و در 4 نسخه که در هر کدام حکم واحد را دارا می‌باشد تنظیم که پس از امضای طرفین و مهر کارفرما معتبر خواهد بود.</p>

                    <p style='margin-top: 14px;'>اینجانب <b>{emp.FirstName} {emp.LastName}</b> با توجه به قبول تصدی امور فوق کلیه مسئولیت‌های قانونی مرتبط را در طول مدت همکاری به عهده می‌گیرم و با علم کامل بر مفاد این قرارداد خود را ملزم به انجام بندبند موارد مندرج در قرارداد به نحو مطلوب در حین و پایان کار می‌دانم و آنرا امضاء نموده و خارج از مواد مندرج در قرارداد حق طرح هیچگونه اعتراضی را در آینده، چه در شرکت و یا سایر مراجع نخواهم داشت.</p>

                    <table class='signature-table' style='margin-top: 22px;'>
                        <tr>
                            <td>امضاء و مهر کارفرما</td>
                            <td>امضاء کارپذیر</td>
                        </tr>
                    </table>

                    <p style='margin-top: 22px;'>این قرارداد بنا به درخواست کارپذیر و تایید کارفرما از تاریخ ......................... لغایت ......................... تمدید میگردد. ضمناً کلیه مفاد قرارداد فوق کماکان به قوت خود باقی است.</p>

                    <table class='signature-table' style='margin-top: 16px;'>
                        <tr>
                            <td>امضاء و مهر کارفرما</td>
                            <td>امضاء کارپذیر</td>
                        </tr>
                    </table>
                    <div class='footer'>صفحه 2 از 2 &nbsp;&nbsp;&nbsp; قرارداد کار {emp.FirstName} {emp.LastName}</div>
                </div>
";
    }

    // ───── حذف سرصفحه/پاصفحه‌ی خودکار مرورگر (about:blank و تاریخ) و کم‌کردن حاشیه‌ی کاغذ ─────
    private const string PageSetupKey = @"Software\Microsoft\Internet Explorer\PageSetup";
    private static readonly string[] PageSetupValues = { "header", "footer", "margin_left", "margin_right", "margin_top", "margin_bottom" };

    /// <summary>پیش‌نمایش چاپ با سرصفحه/پاصفحه‌ی خالی و حاشیه‌ی کم؛ تنظیمات قبلی بعد از بستن برمی‌گردد</summary>
    public static void ShowPrintPreview(WebBrowser browser)
    {
        var saved = SetPrintPageSetup();
        try { browser.ShowPrintPreviewDialog(); }
        finally { RestorePrintPageSetup(saved); }
    }

    private static System.Collections.Generic.Dictionary<string, object?> SetPrintPageSetup()
    {
        var saved = new System.Collections.Generic.Dictionary<string, object?>();
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(PageSetupKey);
            if (key is null) return saved;
            foreach (var n in PageSetupValues) saved[n] = key.GetValue(n);
            key.SetValue("header", "&b");   // کدهای خالی؛ مقدار تهی/فاصله را IE به پیش‌فرض برمی‌گرداند
            key.SetValue("footer", "&b");
            key.SetValue("margin_left", "0.200000");
            key.SetValue("margin_right", "0.200000");
            key.SetValue("margin_top", "0.200000");
            key.SetValue("margin_bottom", "0.200000");
        }
        catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractHtmlBuilder"); }
        return saved;
    }

    private static void RestorePrintPageSetup(System.Collections.Generic.Dictionary<string, object?> saved)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(PageSetupKey);
            if (key is null) return;
            foreach (var kv in saved)
            {
                if (kv.Value is null) key.DeleteValue(kv.Key, false);
                else key.SetValue(kv.Key, kv.Value);
            }
        }
        catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractHtmlBuilder"); }
    }
}
