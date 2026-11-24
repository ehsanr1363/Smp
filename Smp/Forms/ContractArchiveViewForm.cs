using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;

namespace Smp.Forms;

public class ContractArchiveViewForm : Form
{
    private readonly string _connectionString;
    private readonly int _employeeId;
    private readonly string _employeeName;

    private DataGridView dgvArchives = null!;

    public ContractArchiveViewForm(string connectionString, int employeeId, string employeeName)
    {
        _connectionString = connectionString;
        _employeeId = employeeId;
        _employeeName = employeeName;

        BuildUI();
        ShowInTaskbar = false;

        Shown += async (s, e) => await LoadDataAsync();
    }

    private void BuildUI()
    {
        dgvArchives = ModernGrid.Create();
        dgvArchives.AutoGenerateColumns = false;
        dgvArchives.AllowUserToAddRows = false;
        dgvArchives.ReadOnly = true;
        dgvArchives.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvArchives.FillColumns();

        var center = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };

        dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ArchiveDate", HeaderText = "وضعیت / تاریخ", FillWeight = 15, MinimumWidth = 150 });
        dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractNumber", HeaderText = "شماره قرارداد", FillWeight = 15, MinimumWidth = 110 });
        dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractStartDate", HeaderText = "شروع قرارداد", FillWeight = 12, MinimumWidth = 100, DefaultCellStyle = center });
        dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractEndDate", HeaderText = "پایان قرارداد", FillWeight = 12, MinimumWidth = 100, DefaultCellStyle = center });
        dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Position", HeaderText = "سمت", FillWeight = 15, MinimumWidth = 100 });
        dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BaseSalary", HeaderText = "حقوق پایه", FillWeight = 12, MinimumWidth = 100, DefaultCellStyle = center });
        dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DepartmentName", HeaderText = "واحد سازمانی", FillWeight = 10, MinimumWidth = 90 });
        dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompanyTitle", HeaderText = "شرکت", FillWeight = 9, MinimumWidth = 80 });
        ModernListKit.PinHeaders(dgvArchives);   // عنوان‌های فارسی بعد از بایند حفظ شوند

        dgvArchives.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvArchives.Columns[e.ColumnIndex].DataPropertyName;

            var rowItem = dgvArchives.Rows[e.RowIndex].DataBoundItem as ContractArchive;

            if (colName == "ArchiveDate")
            {
                if (rowItem is not null && rowItem.Id < 0)
                {
                    e.Value = "✅ قرارداد جاری (فعال)";
                    e.CellStyle.ForeColor = ModernTheme.Success;
                    e.CellStyle.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
                    e.FormattingApplied = true;
                }
                else if (e.Value is DateTime dt)
                {
                    e.Value = "آرشیو شده در: " + DateHelper.ToPersianDate(dt);
                    e.FormattingApplied = true;
                }
            }
            else if ((colName.Contains("Date")) && e.Value is DateTime dateVal)
            {
                e.Value = DateHelper.ToPersianDate(dateVal);
                e.FormattingApplied = true;
            }
            else if (colName == "BaseSalary" && e.Value is decimal dec)
            {
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.FormattingApplied = true;
            }
        };

        dgvArchives.CellDoubleClick += DgvArchives_CellDoubleClick;

        ModernListKit.ComposeRows(this,
            $"آرشیو و سوابق قراردادهای {_employeeName}",
            "برای مشاهده و چاپ قرارداد، روی ردیف دوبار کلیک کنید",
            new Size(1100, 600),
            (ModernReport.WrapGrid(dgvArchives), 0));
    }

    private async void DgvArchives_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (dgvArchives.Rows[e.RowIndex].DataBoundItem is not ContractArchive selectedRow) return;

        var empRepo = new EmployeeRepository(_connectionString);
        var employee = await Task.Run(() => empRepo.GetById(selectedRow.EmployeeId));

        if (employee is not null)
        {
            if (selectedRow.Id > 0)
            {
                employee.ContractNumber = selectedRow.ContractNumber;
                employee.ContractStartDate = selectedRow.ContractStartDate;
                employee.ContractEndDate = selectedRow.ContractEndDate;
                employee.BaseSalary = selectedRow.BaseSalary;
                employee.YearsBonus = selectedRow.YearsBonus;
                employee.AttractionAllowance = selectedRow.AttractionAllowance;
                employee.ResponsibilityAllowance = selectedRow.ResponsibilityAllowance;
                employee.GroceryAllowance = selectedRow.GroceryAllowance;
                employee.HousingAllowance = selectedRow.HousingAllowance;
                employee.ChildAllowance = selectedRow.ChildAllowance;
                employee.MarriageAllowance = selectedRow.MarriageAllowance;
                employee.ManagementAllowance = selectedRow.ManagementAllowance;
                employee.TechnicalAllowance = selectedRow.TechnicalAllowance;
                employee.OtherAllowances = selectedRow.OtherAllowances;
                employee.Position = selectedRow.Position;
            }

            using var printForm = new ContractPrintForm(employee);
            printForm.ShowDialog(this);
        }
        else
        {
            ModernMessageBox.ShowWarning("اطلاعات کارمند یافت نشد.");
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var list = new List<ContractArchive>();

                // ۱. خواندن قراردادهای ثبت شده در جدول EmployeeContracts
                var contractRepo = new EmployeeContractRepository(_connectionString);
                var allContracts = await contractRepo.GetContractsByEmployeeIdAsync(_employeeId);

                // قراردادهای فعال (برای تشخیص نسخه‌های تکراری و نامعتبر)
                var activeKeys = allContracts
                    .Where(x => (x.Status ?? "").Trim() == "فعال")
                    .Select(x => (x.ContractNumber, x.StartDate.Date))
                    .ToHashSet();

                foreach (var c in allContracts)
                {
                    string status = (c.Status ?? "").Trim();
                    bool isActive = status == "فعال";

                    // نسخه تکراری: خاتمه‌یافته با پایان قبل از شروع، در حالی که همان شماره و شروع یک قرارداد فعال دارد
                    if (!isActive && c.EndDate.HasValue && c.EndDate.Value.Date < c.StartDate.Date
                        && activeKeys.Contains((c.ContractNumber, c.StartDate.Date)))
                        continue;

                    list.Add(new ContractArchive
                    {
                        Id = isActive ? -c.Id : c.Id,
                        EmployeeId = c.EmployeeId,
                        ArchiveDate = isActive ? DateTime.Now : (c.EndDate ?? DateTime.Now),
                        ContractNumber = c.ContractNumber,
                        ContractStartDate = c.StartDate,
                        ContractEndDate = c.EndDate,
                        BaseSalary = c.BaseSalary,
                        YearsBonus = c.YearsBonus,
                        AttractionAllowance = c.AttractionAllowance,
                        ResponsibilityAllowance = c.ResponsibilityAllowance,
                        GroceryAllowance = c.GroceryAllowance,
                        HousingAllowance = c.HousingAllowance,
                        ChildAllowance = c.ChildAllowance,
                        MarriageAllowance = c.MarriageAllowance,
                        ManagementAllowance = c.ManagementAllowance,
                        TechnicalAllowance = c.TechnicalAllowance,
                        OtherAllowances = c.OtherAllowances,
                        Position = c.Position,
                        DepartmentName = "---",
                        CompanyTitle = "---"
                    });
                }

                // ۲. خواندن قراردادهای آرشیو شده از جدول ContractArchives
                var archiveRepo = new ContractArchiveRepository(_connectionString);
                var archivedContracts = await Task.Run(() => archiveRepo.GetByEmployeeId(_employeeId));

                foreach (var arc in archivedContracts)
                {
                    if (!list.Any(x => x.ContractNumber == arc.ContractNumber && x.ContractStartDate == arc.ContractStartDate))
                    {
                        list.Add(arc);
                    }
                }

                // ✅ حذف شد: دیگر اگر لیست خالی بود، از جدول Employees رکورد نمی‌سازیم
                // (تا ردیف خالی و جعلیِ "قرارداد جاری" نمایش داده نشود)

                var sortedList = list
                    .OrderByDescending(x => x.Id < 0)
                    .ThenByDescending(x => x.ContractStartDate)
                    .ToList();

                if (IsDisposed || dgvArchives.IsDisposed) return;

                dgvArchives.DataSource = sortedList;
            }, "در حال بارگذاری سوابق قرارداد...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractArchiveViewForm.LoadDataAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات:\n" + ex.Message);
        }
    }
}
