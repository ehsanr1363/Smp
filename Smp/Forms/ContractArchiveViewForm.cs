using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;

namespace SMP.Forms
{
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
            this.ApplyStandardHeader($"آرشیو و سوابق قراردادهای {employeeName}", new Size(1100, 600));
            ShowInTaskbar = false;

            Shown += async (s, e) => await LoadDataAsync();
        }

        private void BuildUI()
        {
            dgvArchives = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                RightToLeft = RightToLeft.Yes
            };
            dgvArchives.ApplyModernStyle();

            dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ArchiveDate", HeaderText = "وضعیت / تاریخ", FillWeight = 15 });
            dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractNumber", HeaderText = "شماره قرارداد", FillWeight = 15 });
            dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractStartDate", HeaderText = "شروع قرارداد", FillWeight = 12 });
            dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractEndDate", HeaderText = "پایان قرارداد", FillWeight = 12 });
            dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Position", HeaderText = "سمت", FillWeight = 15 });
            dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BaseSalary", HeaderText = "حقوق پایه", FillWeight = 12 });
            dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DepartmentName", HeaderText = "واحد سازمانی", FillWeight = 10 });
            dgvArchives.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompanyTitle", HeaderText = "شرکت", FillWeight = 9 });

            dgvArchives.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string colName = dgvArchives.Columns[e.ColumnIndex].DataPropertyName;

                var rowItem = dgvArchives.Rows[e.RowIndex].DataBoundItem as ContractArchive;

                if (colName == "ArchiveDate")
                {
                    if (rowItem != null && rowItem.Id < 0)
                    {
                        e.Value = "✅ قرارداد جاری (فعال)";
                        e.CellStyle.ForeColor = UIHelper.ActionGreen;
                        e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold);
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

            // ✅ اضافه شدن قابلیت دابل کلیک برای چاپ قرارداد
            dgvArchives.CellDoubleClick += DgvArchives_CellDoubleClick;

            this.Controls.Add(dgvArchives);
        }

        private async void DgvArchives_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvArchives.Rows[e.RowIndex].DataBoundItem is not ContractArchive selectedRow) return;

            // دریافت اطلاعات کامل کارمند از دیتابیس
            var empRepo = new EmployeeRepository(_connectionString);
            var employee = await Task.Run(() => empRepo.GetById(selectedRow.EmployeeId));

            if (employee != null)
            {
                // اگر روی یک قراردادِ آرشیو شده (گذشته) دابل کلیک شده باشد،
                // اطلاعات فعلی کارمند را موقتاً با اطلاعات همان قرارداد قدیمی جایگزین می‌کنیم
                // تا فرم چاپ، همان مقادیر قدیمی را چاپ کند.
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

                // باز کردن فرم چاپ قرارداد
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

                    var contractRepo = new EmployeeContractRepository(_connectionString);
                    var allContracts = await contractRepo.GetContractsByEmployeeIdAsync(_employeeId);

                    foreach (var c in allContracts)
                    {
                        string status = (c.Status ?? "").Trim();
                        bool isActive = status == "فعال";

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

                    if (list.Count == 0)
                    {
                        var empRepo = new EmployeeRepository(_connectionString);
                        var emp = await Task.Run(() => empRepo.GetById(_employeeId));

                        if (emp != null)
                        {
                            list.Add(new ContractArchive
                            {
                                Id = -1,
                                EmployeeId = emp.Id,
                                ArchiveDate = DateTime.Now,
                                ContractNumber = string.IsNullOrWhiteSpace(emp.ContractNumber) ? "بدون شماره" : emp.ContractNumber,
                                ContractStartDate = emp.ContractStartDate ?? emp.HireDate,
                                ContractEndDate = emp.ContractEndDate,
                                BaseSalary = emp.BaseSalary,
                                YearsBonus = emp.YearsBonus,
                                AttractionAllowance = emp.AttractionAllowance,
                                ResponsibilityAllowance = emp.ResponsibilityAllowance,
                                GroceryAllowance = emp.GroceryAllowance,
                                HousingAllowance = emp.HousingAllowance,
                                ChildAllowance = emp.ChildAllowance,
                                MarriageAllowance = emp.MarriageAllowance,
                                ManagementAllowance = emp.ManagementAllowance,
                                TechnicalAllowance = emp.TechnicalAllowance,
                                OtherAllowances = emp.OtherAllowances,
                                Position = emp.Position,
                                DepartmentName = emp.DepartmentName ?? "---",
                                CompanyTitle = emp.CompanyTitle ?? "---"
                            });
                        }
                    }
                    else
                    {
                        var archiveRepo = new ContractArchiveRepository(_connectionString);
                        var archivedContracts = await Task.Run(() => archiveRepo.GetByEmployeeId(_employeeId));

                        foreach (var arc in archivedContracts)
                        {
                            if (!list.Any(x => x.ContractNumber == arc.ContractNumber && x.ContractStartDate == arc.ContractStartDate))
                            {
                                list.Add(arc);
                            }
                        }
                    }

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
}