using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Smp.Models;
using System;

namespace Smp.Helpers;

public static class PdfHelper
{
    static PdfHelper()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void GeneratePayslip(PayrollResult payroll, string filePath, string companyName = "شرکت نمونه", byte[] logoBytes = null)
    {
        try
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Tahoma"));

                    page.Header().Element(x => ComposePayslipHeader(x, payroll, companyName, logoBytes));
                    page.Content().Element(x => ComposePayslipContent(x, payroll));

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            })
            .WithSettings(new DocumentSettings
            {
                ContentDirection = ContentDirection.RightToLeft
            })
            .GeneratePdf(filePath);
        }
        catch (Exception ex)
        {
            throw new Exception("خطا در تولید فایل PDF: " + ex.Message, ex);
        }
    }

    public static void GenerateBatchPayslip(
        PayrollResult payroll, string filePath, string companyName, byte[]? logoBytes = null)
    {
        try
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Tahoma"));

                    page.Header().Element(x => ComposePayslipHeader(x, payroll, companyName, logoBytes));
                    page.Content().Element(x => ComposePayslipContent(x, payroll));

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            })
            .WithSettings(new DocumentSettings { ContentDirection = ContentDirection.RightToLeft })
            .GeneratePdf(filePath);
        }
        catch (Exception ex)
        {
            throw new Exception("خطا در تولید فایل PDF: " + ex.Message, ex);
        }
    }

    static void ComposePayslipHeader(IContainer container, PayrollResult payroll, string companyName, byte[] logoBytes)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(companyName).FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text("فیش حقوقی ماهانه").FontSize(14).Bold();
                column.Item().Text($"ماه: {TranslationHelper.GetMonthName(payroll.Month)} {payroll.Year}").FontSize(11);
            });

            if (logoBytes is not null && logoBytes.Length > 0)
            {
                row.ConstantItem(80).Image(logoBytes);
            }
        });
    }

    static void ComposePayslipContent(IContainer container, PayrollResult payroll)
    {
        container.PaddingVertical(15).Column(column =>
        {
            column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(info =>
            {
                info.Item().Text($"نام و نام خانوادگی: {payroll.EmployeeName}");
                info.Item().Text($"کد پرسنلی: {payroll.PersonnelCode}");
            });

            column.Item().PaddingTop(15);

            // جدول مزایا
            column.Item().Text("مزایا (درآمدها)").FontSize(13).Bold().FontColor(Colors.Green.Darken2);
            column.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("شرح آیتم").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignCenter().Text("مبلغ (ریال)").Bold();
                });

                AddTableRow(table, "حقوق پایه", payroll.BaseSalary);
                AddTableRow(table, "پایه سنوات", payroll.YearsBonus);
                AddTableRow(table, "حق مسکن", payroll.HousingAllowance);
                AddTableRow(table, "حق خواربار", payroll.GroceryAllowance);
                AddTableRow(table, "حق اولاد", payroll.ChildAllowance);
                AddTableRow(table, "سایر مزایا", payroll.OtherAllowances);
                AddTableRow(table, "اضافه‌کاری", payroll.OvertimePay);

                table.Cell().BorderTop(1).BorderColor(Colors.Black).Padding(5).Text("جمع کل مزایا").Bold();
                table.Cell().BorderTop(1).BorderColor(Colors.Black).Padding(5).AlignCenter().Text(payroll.TotalEarnings.ToString("N0")).Bold();
            });

            column.Item().PaddingTop(15);

            // جدول کسورات
            column.Item().Text("کسورات").FontSize(13).Bold().FontColor(Colors.Red.Darken2);
            column.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("شرح آیتم").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignCenter().Text("مبلغ (ریال)").Bold();
                });

                AddTableRow(table, "بیمه سهم کارمند (7%)", payroll.InsuranceEmployee);
                AddTableRow(table, "مالیات بر درآمد", payroll.IncomeTax);
                AddTableRow(table, "کسر غیبت", payroll.AbsenceDeduction);

                if (payroll.SupplementaryInsuranceDeduction > 0)
                    AddTableRow(table, "بیمه تکمیلی (سهم کارمند)", payroll.SupplementaryInsuranceDeduction);

                if (payroll.LoanDeduction > 0)
                    AddTableRow(table, "کسر وام", payroll.LoanDeduction);

                if (payroll.AdvanceDeduction > 0)
                    AddTableRow(table, "کسر مساعده", payroll.AdvanceDeduction);

                if (payroll.CarInsuranceDeduction > 0)
                    AddTableRow(table, "بیمه خودرو", payroll.CarInsuranceDeduction);

                if (payroll.OtherDeductions > 0)
                    AddTableRow(table, "سایر کسورات", payroll.OtherDeductions);

                table.Cell().BorderTop(1).BorderColor(Colors.Black).Padding(5).Text("جمع کل کسورات").Bold();
                table.Cell().BorderTop(1).BorderColor(Colors.Black).Padding(5).AlignCenter().Text(payroll.TotalDeductions.ToString("N0")).Bold();
            });

            column.Item().PaddingTop(20);

            column.Item().Background(Colors.Blue.Lighten4).Padding(10).AlignCenter()
                .Text($"خالص پرداختی: {payroll.NetPay:N0} ریال")
                .FontSize(15).Bold().FontColor(Colors.Blue.Darken3);
        });
    }

    static void AddTableRow(TableDescriptor table, string title, decimal amount)
    {
        if (amount == 0 && title != "حقوق پایه") return;

        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(title);
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(amount.ToString("N0"));
    }
}
