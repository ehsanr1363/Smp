using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;

namespace Smp.Forms;

public class EmployeeContractForm : Form
{
    private readonly string _connectionString;
    private readonly Employee _employee;
    private readonly EmployeeContractRepository _repo;

    private DataGridView dgvContracts = null!;
    private Button btnAdd = null!, btnDelete = null!, btnClose = null!;
    private Label lblEmployeeName = null!;

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public EmployeeContractForm(string connectionString, Employee employee)
    {
        _connectionString = connectionString;
        _employee = employee;
        _repo = new EmployeeContractRepository(connectionString);

        BuildUI();

        // 🔴 FIX 1: خط this.ApplyStandardHeader از اینجا حذف شد!
        // چون SetupStandardListLayout خودش این کار را انجام می‌دهد و باعث هدر تکراری می‌شد.

        ShowInTaskbar = false;
        ShortcutHelper.ApplyStandardShortcuts(this);

        Shown += async (s, e) => await LoadContractsAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "EmployeeContractForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // این هلپر خودش هدر را می‌سازد
        var (topRightPanel, topLeftPanel, grid, bottomPanel) = ModernForm.SetupStandardListLayout(
            this, $"سوابق قراردادهای {_employee.FullName}", new Size(1000, 650));

        // ─── نمایش اطلاعات کارمند در بالای فرم (سمت راست) ───
        lblEmployeeName = new Label
        {
            Text = $"کارمند: {_employee.FullName} (کد پرسنلی: {_employee.PersonnelCode})",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            Font = ModernTheme.GetFont(12f, FontStyle.Bold),
            ForeColor = ModernTheme.Primary,
            Margin = new Padding(10, 12, 10, 0)
        };
        topRightPanel.Controls.Add(lblEmployeeName);

        // ─── تنظیمات گرید ───
        dgvContracts = grid;
        dgvContracts.AutoGenerateColumns = false;

        dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractNumber", DataPropertyName = "ContractNumber", HeaderText = "شماره قرارداد", FillWeight = 15 });
        dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartDate", DataPropertyName = "StartDate", HeaderText = "تاریخ شروع", FillWeight = 15 });
        dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndDate", DataPropertyName = "EndDate", HeaderText = "تاریخ پایان", FillWeight = 15 });
        dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", DataPropertyName = "Status", HeaderText = "وضعیت", FillWeight = 10 });
        dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { Name = "BaseSalary", DataPropertyName = "BaseSalary", HeaderText = "حقوق پایه (ریال)", FillWeight = 15 });

        // 🔴 FIX 2: برای جلوگیری از انگلیسی شدن، DataPropertyName را خالی می‌گذاریم تا GridHelper آن را ترجمه نکند
        dgvContracts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColDisplaySalary", DataPropertyName = "", HeaderText = "جمع کل مزایا (ریال)", FillWeight = 15 });

        // فرمت‌دهی و رنگ‌آمیزی سلول‌ها
        dgvContracts.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvContracts.Columns[e.ColumnIndex].Name;

            if (colName == "StartDate" || colName == "EndDate")
            {
                if (e.Value is DateTime dt)
                {
                    e.Value = DateHelper.ToPersianDate(dt);
                    e.FormattingApplied = true;
                }
            }
            else if (colName == "BaseSalary")
            {
                if (e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            }
            // 🔴 FIX 3: فرمت‌دهی به ستون دستی که DataPropertyName ندارد
            else if (colName == "ColDisplaySalary")
            {
                if (dgvContracts.Rows[e.RowIndex].DataBoundItem is EmployeeContract contract)
                {
                    e.Value = NumberHelper.FormatThousands(contract.DisplaySalary, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            }
            else if (colName == "Status")
            {
                string status = e.Value?.ToString() ?? "";
                if (status == "فعال")
                {
                    e.CellStyle.ForeColor = ModernTheme.Success;
                    e.CellStyle.Font = ModernTheme.GetFont(11f, FontStyle.Bold);
                }
                else
                {
                    e.CellStyle.ForeColor = ModernTheme.Danger;
                    e.CellStyle.Font = ModernTheme.GetFont(11f, FontStyle.Regular);
                }
            }
        };

        dgvContracts.SelectionChanged += (s, e) => btnDelete.Enabled = dgvContracts.CurrentRow is not null;

        // ─── دکمه‌ها در نوار پایین ───
        btnAdd = ModernButton.Create("➕ ثبت قرارداد جدید", ModernTheme.Success, 180, 42);
        btnAdd.Click += BtnAdd_Click;

        btnDelete = ModernButton.Create("🗑️ حذف قرارداد انتخاب‌شده", ModernTheme.Danger, 200, 42);
        btnDelete.Enabled = false;
        btnDelete.Click += BtnDelete_Click;

        btnClose = ModernButton.Create("بستن", ModernTheme.Neutral, 100, 42);
        btnClose.Click += (s, e) => Close();

        bottomPanel.Controls.Add(btnAdd);
        bottomPanel.Controls.Add(btnDelete);
        bottomPanel.Controls.Add(btnClose);
    }

    private async Task LoadContractsAsync()
    {
        if (IsDisposed) return;

        try
        {
            var contracts = await _repo.GetContractsByEmployeeIdAsync(_employee.Id);

            // محاسبه جمع کل مزایا
            foreach (var c in contracts)
            {
                c.DisplaySalary = c.BaseSalary + c.YearsBonus + c.AttractionAllowance +
                                  c.ResponsibilityAllowance + c.ManagementAllowance + c.TechnicalAllowance +
                                  c.HousingAllowance + c.GroceryAllowance + c.ChildAllowance +
                                  c.MarriageAllowance + c.OtherAllowances + c.TransportationAllowance;
            }

            dgvContracts.DataSource = contracts;
            btnDelete.Enabled = dgvContracts.CurrentRow is not null;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "EmployeeContractForm.LoadContractsAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری لیست قراردادها:\n" + ex.Message);
        }
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        ModernMessageBox.ShowInfo(@"برای ثبت حکم/قرارداد جدید از منوی «حقوق و دستمزد ← ورود مبالغ حکم حقوقی» استفاده کنید.
قرارداد قبلی به‌صورت خودکار بایگانی می‌شود.", "راهنما");
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (dgvContracts.CurrentRow?.DataBoundItem is not EmployeeContract selected)
        {
            ModernMessageBox.ShowWarning("لطفاً ابتدا یک قرارداد را از لیست انتخاب کنید.");
            return;
        }

        if (!await _actionLock.WaitAsync(0)) return;
        btnDelete.Enabled = false;

        try
        {
            bool hasPayrolls = await Task.Run(() => _repo.HasPayrolls(selected.Id));

            if (hasPayrolls)
            {
                ModernMessageBox.ShowWarning(
                    "⛔ امکان حذف قرارداد وجود ندارد!\n\n" +
                    "برای این قرارداد در سیستم حقوق و دستمزد ثبت شده است. برای حفظ سوابق مالی، حذف قراردادهای دارای فیش حقوقی مجاز نیست.",
                    "عدم امکان حذف");
                return;
            }

            if (!ModernMessageBox.AskConfirm($"آیا از حذف قرارداد شماره «{selected.ContractNumber}» مطمئن هستید؟", "حذف قرارداد"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() => _repo.Delete(selected.Id));
            }, "در حال حذف قرارداد...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("قرارداد با موفقیت حذف شد.", isSuccess: true);
                await LoadContractsAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "EmployeeContractForm.BtnDelete_Click");
            ModernMessageBox.ShowError("خطا در حذف قرارداد:\n" + ex.Message, "خطای سیستمی");
        }
        finally
        {
            if (!IsDisposed) btnDelete.Enabled = true;
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "EmployeeContractForm"); }
        }
    }
}
