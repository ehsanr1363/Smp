using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using PersonnelApp.Data;
using System;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class ExcelTemplateGenerator
    {
        private static string GetLookupList(string category, string defaultFallback)
        {
            try
            {
                var dt = DbHelper.ExecuteQuery(AppConfig.ConnectionString,
                    "SELECT Value FROM LookupItems WHERE Category = @Cat ORDER BY Id",
                    new SqlParameter("@Cat", category));

                if (dt.Rows.Count > 0)
                {
                    var values = dt.AsEnumerable()
                        .Select(r => r.Field<string>("Value"))
                        .Where(v => !string.IsNullOrWhiteSpace(v) && !v.Contains(","))
                        .ToList();

                    if (values.Count > 0)
                    {
                        string result = string.Join(",", values);
                        if (result.Length > 250) result = result.Substring(0, 250).TrimEnd(',');
                        return result;
                    }
                }
            }
            catch { }
            return defaultFallback;
        }

        private static string GetDepartmentsList(string defaultFallback)
        {
            try
            {
                var dt = DbHelper.ExecuteQuery(AppConfig.ConnectionString, "SELECT Name FROM Departments ORDER BY Name");
                if (dt.Rows.Count > 0)
                {
                    var values = dt.AsEnumerable()
                        .Select(r => r.Field<string>("Name"))
                        .Where(v => !string.IsNullOrWhiteSpace(v) && !v.Contains(","))
                        .ToList();

                    if (values.Count > 0)
                    {
                        string result = string.Join(",", values);
                        if (result.Length > 250) result = result.Substring(0, 250).TrimEnd(',');
                        return result;
                    }
                }
            }
            catch { }
            return defaultFallback;
        }

        public static void GeneratePersonnelTemplate()
        {
            using var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = "Personnel_Import_Template.xlsx", Title = "ذخیره فایل نمونه اکسل پرسنل" };
            if (sfd.ShowDialog() != DialogResult.OK) return;
            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("اطلاعات پرسنل");

                string[] headers = {
                    "کد پرسنلی", "نام", "نام خانوادگی", "کد ملی", "نام پدر",
                    "شماره شناسنامه", "تاریخ تولد", "محل تولد", "محل صدور", "جنسیت",
                    "وضعیت تاهل", "وضعیت نظام وظیفه", "تحصیلات", "رشته تخصصی", "تعداد فرزند",
                    "شماره بیمه", "شماره تماس", "آدرس", "تاریخ استخدام", "تاریخ شروع قرارداد",
                    "تاریخ پایان قرارداد", "سال قرارداد", "شماره قرارداد", "نام شرکت",
                    "واحد سازمانی", "سمت", "نوع پرسنلی", "محل کار", "وضعیت استخدام",
                    "حقوق مبنا", "پایه سنوات", "حق جذب", "حق مسئولیت", "حق مسکن",
                    "حق خواربار", "حق اولاد", "حق تاهل", "حق مدیریت", "حق فنی",
                    "سایر مزایا", "وضعیت (فعال/غیرفعال)", "شماره شبا"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontName = "Tahoma";
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                ws.Columns().AdjustToContents();
                ws.RightToLeft = true;
                ws.Row(1).Height = 30;

                string listGender = GetLookupList("Gender", "مرد,زن");
                string listMarital = GetLookupList("MaritalStatus", "متأهل,مجرد");
                string listMilitary = GetLookupList("MilitaryStatus", "مشمول,معاف,پایان خدمت");
                string listEducation = GetLookupList("Education", "زیر دیپلم,دیپلم,کاردانی,کارشناسی,کارشناسی ارشد,دکتری");
                string listDepts = GetDepartmentsList("دفتر مرکزی,فنی,مالی,منابع انسانی");
                string listPersonnelType = GetLookupList("PersonnelType", "قراردادی,روزمزد,ساعتی");
                string listWorkLocation = GetLookupList("WorkLocation", "دفتر مرکزی,پروژه 1,پروژه 2");
                string listStatus = GetLookupList("EmploymentStatus", "فعال,بازنشسته,اتمام,مرخصی بدون حقوق");

                ws.Range("J2:J1000").CreateDataValidation().List($"\"{listGender}\"");
                ws.Range("K2:K1000").CreateDataValidation().List($"\"{listMarital}\"");
                ws.Range("L2:L1000").CreateDataValidation().List($"\"{listMilitary}\"");
                ws.Range("M2:M1000").CreateDataValidation().List($"\"{listEducation}\"");

                ws.Range("Y2:Y1000").CreateDataValidation().List($"\"{listDepts}\"");
                ws.Range("AA2:AA1000").CreateDataValidation().List($"\"{listPersonnelType}\"");
                ws.Range("AB2:AB1000").CreateDataValidation().List($"\"{listWorkLocation}\"");
                ws.Range("AC2:AC1000").CreateDataValidation().List($"\"{listStatus}\"");
                ws.Range("AO2:AO1000").CreateDataValidation().List("\"فعال,غیرفعال\"");

                workbook.SaveAs(sfd.FileName);
                ModernMessageBox.ShowInfo("فایل قالب اکسل پرسنل ساخته شد.", "موفق");
            }
            catch (Exception ex)
            {
                ModernMessageBox.ShowError("خطا در ساخت قالب:\n" + ex.Message);
            }
        }

        // ... (متدهای تحت تکفل و قراردادها بدون تغییر)
        public static void GenerateDependentsTemplate()
        {
            using var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = "Dependents_Import_Template.xlsx", Title = "ذخیره فایل نمونه اکسل افراد تحت تکفل" };
            if (sfd.ShowDialog() != DialogResult.OK) return;
            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("افراد تحت تکفل");
                string[] headers = { "کد ملی پرسنل", "نام", "نام خانوادگی", "کد ملی تحت تکفل", "شماره شناسنامه", "نام پدر", "تاریخ تولد", "جنسیت", "محل تولد", "نسبت", "بیمه تکمیلی" };
                for (int i = 0; i < headers.Length; i++) { var cell = ws.Cell(1, i + 1); cell.Value = headers[i]; cell.Style.Font.Bold = true; cell.Style.Font.FontName = "Tahoma"; cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59); cell.Style.Font.FontColor = XLColor.White; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; }
                ws.Columns().AdjustToContents(); ws.RightToLeft = true; ws.Row(1).Height = 30;

                string listGender = GetLookupList("Gender", "مرد,زن");
                ws.Range("H2:H1000").CreateDataValidation().List($"\"{listGender}\"");
                ws.Range("J2:J1000").CreateDataValidation().List("\"همسر,فرزند,پدر,مادر,سایر\"");
                ws.Range("K2:K1000").CreateDataValidation().List("\"بله,خیر\"");

                workbook.SaveAs(sfd.FileName); ModernMessageBox.ShowInfo("فایل قالب اکسل افراد تحت تکفل ساخته شد.", "موفق");
            }
            catch (Exception ex) { ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
        }

        public static void GenerateContractorContractsTemplate()
        {
            using var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = "Contractor_Contracts_Template.xlsx", Title = "ذخیره فایل نمونه اکسل قراردادهای پیمانکاری" };
            if (sfd.ShowDialog() != DialogResult.OK) return;
            try
            {
                using var workbook = new XLWorkbook(); var ws = workbook.Worksheets.Add("قراردادهای پیمانکاری");
                string[] headers = { "شناسه طرف قرارداد (کد ملی/شناسه ملی)", "شماره قرارداد", "کد کارگاه", "ردیف پیمان", "موضوع قرارداد", "مبلغ قرارداد", "تاریخ قرارداد", "تاریخ پایان" };
                for (int i = 0; i < headers.Length; i++) { var cell = ws.Cell(1, i + 1); cell.Value = headers[i]; cell.Style.Font.Bold = true; cell.Style.Font.FontName = "Tahoma"; cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59); cell.Style.Font.FontColor = XLColor.White; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; }
                ws.Columns().AdjustToContents(); ws.RightToLeft = true; ws.Row(1).Height = 30;
                ws.Cell(2, 1).Value = "0012345678"; ws.Cell(2, 2).Value = "C-1001"; ws.Cell(2, 3).Value = "123456"; ws.Cell(2, 4).Value = "1"; ws.Cell(2, 5).Value = "اجرای پروژه ساختمانی"; ws.Cell(2, 6).Value = 500000000; ws.Cell(2, 7).Value = "1403/01/01"; ws.Cell(2, 8).Value = "1403/12/29";
                workbook.SaveAs(sfd.FileName); ModernMessageBox.ShowInfo("فایل قالب اکسل قراردادهای پیمانکاری ساخته شد.", "موفق");
            }
            catch (Exception ex) { ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
        }

        public static void GenerateClientContractsTemplate()
        {
            using var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = "Client_Contracts_Template.xlsx", Title = "ذخیره فایل نمونه اکسل قراردادهای کارفرمایی" };
            if (sfd.ShowDialog() != DialogResult.OK) return;
            try
            {
                using var workbook = new XLWorkbook(); var ws = workbook.Worksheets.Add("قراردادهای کارفرمایی");
                string[] headers = { "شناسه طرف قرارداد (کد ملی/شناسه ملی)", "شماره قرارداد", "ردیف قرارداد", "موضوع قرارداد", "مبلغ قرارداد", "تاریخ قرارداد", "تاریخ پایان" };
                for (int i = 0; i < headers.Length; i++) { var cell = ws.Cell(1, i + 1); cell.Value = headers[i]; cell.Style.Font.Bold = true; cell.Style.Font.FontName = "Tahoma"; cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59); cell.Style.Font.FontColor = XLColor.White; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; }
                ws.Columns().AdjustToContents(); ws.RightToLeft = true; ws.Row(1).Height = 30;
                ws.Cell(2, 1).Value = "0012345678"; ws.Cell(2, 2).Value = "CL-1001"; ws.Cell(2, 3).Value = "1"; ws.Cell(2, 4).Value = "اجرای پروژه ساختمانی"; ws.Cell(2, 5).Value = 500000000; ws.Cell(2, 6).Value = "1403/01/01"; ws.Cell(2, 7).Value = "1403/12/29";
                workbook.SaveAs(sfd.FileName); ModernMessageBox.ShowInfo("فایل قالب اکسل قراردادهای کارفرمایی ساخته شد.", "موفق");
            }
            catch (Exception ex) { ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
        }

        public static void GenerateContractsTemplate()
        {
            using var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = "Contracts_Import_Template.xlsx", Title = "ذخیره فایل نمونه اکسل قراردادهای پرسنل" };
            if (sfd.ShowDialog() != DialogResult.OK) return;
            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("قراردادهای پرسنل");
                ws.RightToLeft = true;

                string[] headers = {
                    "کد پرسنلی", "شماره قرارداد",
                    "تاریخ شروع (1403/01/01)", "تاریخ پایان (خالی = دائمی)",
                    "حقوق پایه", "پایه سنوات", "حق جذب", "حق مسئولیت",
                    "حق مدیریت", "حق فنی", "حق مسکن", "حق خواربار",
                    "حق اولاد", "حق تاهل", "سایر مزایا",
                    "حق ایاب و ذهاب",
                    "حقوق روزانه (روزمزد)", "حقوق ساعتی (ساعتی)",
                    "بیمه دارد (دارد/ندارد)"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontName = "Tahoma";
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.WrapText = true;
                }

                ws.Cell(2, 1).Value = "1001";
                ws.Cell(2, 2).Value = "CR-1001-1403";
                ws.Cell(2, 3).Value = "1403/01/01";
                ws.Cell(2, 4).Value = "1403/12/29";
                ws.Cell(2, 5).Value = 15000000;
                ws.Cell(2, 11).Value = 9000000;
                ws.Cell(2, 16).Value = 1500000;
                ws.Cell(2, 19).Value = "دارد";

                ws.Range("S2:S1000").CreateDataValidation().List("\"دارد,ندارد\"");
                ws.Range("E2:R1000").Style.NumberFormat.Format = "#,##0";

                ws.Columns().AdjustToContents();
                ws.Row(1).Height = 40;

                workbook.SaveAs(sfd.FileName);
                ModernMessageBox.ShowInfo("فایل قالب اکسل قراردادهای پرسنل ساخته شد.", "موفق");
            }
            catch (Exception ex) { ModernMessageBox.ShowError("خطا در ساخت قالب:\n" + ex.Message); }
        }
    }
}