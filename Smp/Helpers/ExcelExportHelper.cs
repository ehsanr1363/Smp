using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace Smp.Helpers;

public static class ExcelExportHelper
{
    /// <summary>
    /// خروجی اکسل از جدول Employees با هدرهای فارسی.
    /// </summary>
    public static int BackupEmployees(string connectionString, string saveFilePath)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("کارکنان");
        ws.RightToLeft = true;

        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT * FROM Employees", conn);
        using var reader = cmd.ExecuteReader();

        var persianHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Id", "شناسه" }, { "PersonnelCode", "کد پرسنلی" }, { "FirstName", "نام" },
            { "LastName", "نام خانوادگی" }, { "FatherName", "نام پدر" }, { "NationalCode", "کد ملی" },
            { "BirthCertificateNumber", "شماره شناسنامه" }, { "BirthDate", "تاریخ تولد" },
            { "BirthPlace", "محل تولد" }, { "IssuancePlace", "محل صدور" }, { "MaritalStatus", "وضعیت تاهل" },
            { "MilitaryStatus", "وضعیت نظام وظیفه" }, { "Education", "مدرک تحصیلی" },
            { "SpecializedField", "رشته تحصیلی" }, { "ChildrenCount", "تعداد فرزند" },
            { "InsuranceNumber", "شماره بیمه" }, { "ContractStartDate", "تاریخ شروع قرارداد" },
            { "ContractYear", "سال قرارداد" }, { "ContractNumber", "شماره قرارداد" },
            { "CompanyTitle", "نام شرکت" }, { "DepartmentName", "واحد سازمانی" }, { "Position", "سمت" },
            { "PersonnelType", "نوع پرسنلی" }, { "WorkLocation", "محل کار" }, { "Status", "وضعیت" },
            { "PhoneNumber", "شماره تماس" }, { "BaseSalary", "حقوق پایه" }, { "YearsBonus", "پایه سنوات" },
            { "AttractionAllowance", "حق جذب" }, { "ResponsibilityAllowance", "حق مسئولیت" },
            { "GroceryAllowance", "حق خواربار" }, { "HousingAllowance", "حق مسکن" },
            { "ChildAllowance", "حق اولاد" }, { "MarriageAllowance", "حق تاهل" },
            { "ManagementAllowance", "حق مدیریت" }, { "TechnicalAllowance", "حق تخصص" },
            { "OtherAllowances", "سایر مزایا" }, { "IsActive", "فعال" }, { "Gender", "جنسیت" }
        };

        for (int i = 0; i < reader.FieldCount; i++)
        {
            string engName = reader.GetName(i);
            string displayName = persianHeaders.TryGetValue(engName, out var fa) ? fa : engName;
            var cell = ws.Cell(1, i + 1);
            cell.Value = displayName;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        int row = 2;
        while (reader.Read())
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var cell = ws.Cell(row, i + 1);
                object value = reader.GetValue(i);
                string colName = reader.GetName(i);

                if (value is null || value == DBNull.Value) cell.Clear();
                else if (value is byte[] bytes) cell.Value = $"[عکس - {bytes.Length} بایت]";
                else if (value is DateTime dt) { cell.Value = dt; cell.Style.DateFormat.Format = "yyyy/MM/dd"; }
                else if (value is int || value is long || value is short ||
                         value is decimal || value is double || value is float)
                    cell.Value = Convert.ToDecimal(value);
                else
                {
                    // ستون‌های کد/شناسه را رشته نگه دار (صفرهای ابتدایی حفظ شود)
                    bool isCode = colName.IndexOf("Code", StringComparison.OrdinalIgnoreCase) >= 0
                               || colName.IndexOf("Number", StringComparison.OrdinalIgnoreCase) >= 0
                               || colName.IndexOf("Id", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (isCode) cell.Value = value.ToString();
                    else cell.Value = value.ToString();
                }
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        workbook.SaveAs(saveFilePath);
        return row - 2;
    }

    /// <summary>
    /// خروجی اکسل از DataGridView.
    /// برای ردیف جمع کل: یا Tag = "Total" بگذار، یا totalRowIndex را پاس بده.
    /// صفرهای ابتدایی کدها حفظ می‌شوند.
    /// </summary>
    public static void ExportDataGridViewToExcel(DataGridView dgv, string saveFilePath,
                                                 string sheetName = "گزارش",
                                                 int totalRowIndex = -1)
    {
        if (dgv is null) throw new ArgumentNullException(nameof(dgv));

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(sheetName);
        ws.RightToLeft = true;

        // ۱) هدرها
        int colIndex = 1;
        var visibleColumns = new List<DataGridViewColumn>();
        foreach (DataGridViewColumn col in dgv.Columns)
        {
            if (!col.Visible) continue;
            visibleColumns.Add(col);

            var cell = ws.Cell(1, colIndex);
            cell.Value = col.HeaderText;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            colIndex++;
        }

        // ۲) داده‌ها
        int rowIndex = 2;
        foreach (DataGridViewRow row in dgv.Rows)
        {
            if (row.IsNewRow) continue;

            bool isTotalRow = (row.Index == totalRowIndex) ||
                              (row.Tag is not null && row.Tag.ToString() == "Total");

            colIndex = 1;
            foreach (var col in visibleColumns)
            {
                var cell = ws.Cell(rowIndex, colIndex);
                object value = row.Cells[col.Index].Value;

                // آیا ستون «کد» است؟
                bool isCodeColumn =
                    col.HeaderText.Contains("کد", StringComparison.Ordinal) ||
                    col.HeaderText.Contains("شناسه", StringComparison.Ordinal) ||
                    col.HeaderText.Contains("شبا", StringComparison.Ordinal) ||
                    col.HeaderText.Contains("شماره", StringComparison.Ordinal);

                if (value is null || value == DBNull.Value)
                {
                    cell.Clear();
                }
                else if (value is DateTime dt)
                {
                    cell.Value = dt;
                    cell.Style.DateFormat.Format = "yyyy/MM/dd";
                }
                else if (value is bool b)
                {
                    cell.Value = b;
                }
                else if (isCodeColumn)
                {
                    // صفرهای ابتدایی حفظ می‌شوند
                    cell.Value = value.ToString();
                }
                else if (value is int || value is long || value is short ||
                         value is decimal || value is double || value is float)
                {
                    cell.Value = Convert.ToDecimal(value);
                }
                else
                {
                    string strVal = value.ToString() ?? "";
                    string normalized = StringHelper.ConvertToEnglishDigits(strVal)
                        .Replace(",", "").Replace("٬", "").Trim();

                    if (normalized.Length > 0
                        && decimal.TryParse(normalized, NumberStyles.Number,
                                            CultureInfo.InvariantCulture, out decimal num)
                        && (char.IsDigit(normalized[0]) || normalized[0] == '-'))
                        cell.Value = num;
                    else
                        cell.Value = strVal;
                }

                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                if (isTotalRow)
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(240, 230, 220);
                }
                colIndex++;
            }
            rowIndex++;
        }

        if (rowIndex > 1)
        {
            try { ws.Columns().AdjustToContents(); } catch (Exception __ex) { /* شیت خالی */ Smp.Helpers.LogHelper.LogException(__ex, "ExcelExportHelper"); }
            ws.SheetView.FreezeRows(1);
        }
        workbook.SaveAs(saveFilePath);
    }
}
