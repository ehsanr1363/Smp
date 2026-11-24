using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;

namespace Smp.Forms.Controls;

public class ucEmployeeBankAccounts : UserControl
{
    private readonly EmployeeBankAccountRepository _bankAccountRepo;
    private readonly List<EmployeeBankAccount> _bankAccounts = new();
    private int? _editingBankAccId = null;

    private DataGridView dgvBankAccounts = null!;
    private TextBox txtShebaNumber = null!;
    private ComboBox cmbAccountType = null!;
    private CheckBox chkIsMainAccount = null!;
    private TextBox txtBonCard = null!;
    private Button btnSaveBonCard = null!;
    private string _loadedBonCard = "";
    private Button btnAddBankAcc = null!, btnDeleteBankAcc = null!, btnDownloadTemplate = null!, btnImportExcel = null!;

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public ucEmployeeBankAccounts(EmployeeBankAccountRepository bankAccountRepo)
    {
        this.Dock = DockStyle.Fill;
        this.RightToLeft = RightToLeft.Yes;

        _bankAccountRepo = bankAccountRepo;
        BuildUI();

        dgvBankAccounts.CellClick += DgvBankAccounts_CellClick;
        btnAddBankAcc.Click += async (s, e) => await AddBankAccSafeAsync();
        btnDeleteBankAcc.Click += async (s, e) => await DeleteBankAccSafeAsync();
        btnSaveBonCard.Click += async (s, e) => await SaveBonCardNowAsync();
        txtBonCard.Leave += (s, e) =>
        {
            string d = EmployeeBonCardStore.Normalize(txtBonCard.Text);
            if (d.Length == 16) txtBonCard.Text = EmployeeBonCardStore.Format(d);
        };
        btnDownloadTemplate.Click += (s, e) => DownloadTemplate();
        btnImportExcel.Click += async (s, e) => await ImportExcelAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ucEmployeeBankAccounts"); } }
        base.Dispose(disposing);
    }

    public async Task LoadDataAsync(int? employeeId)
    {
        if (IsDisposed) return;
        try
        {
            _bankAccounts.Clear();
            if (employeeId.HasValue && employeeId.Value > 0)
            {
                var list = await _bankAccountRepo.GetByEmployeeIdAsync(employeeId.Value);
                if (IsDisposed) return;
                _bankAccounts.AddRange(list);
            }
            dgvBankAccounts.DataSource = new BindingList<EmployeeBankAccount>(_bankAccounts);
            ClearForm();

            _loadedBonCard = employeeId.HasValue && employeeId.Value > 0
                ? EmployeeBonCardStore.Normalize(await EmployeeBonCardStore.GetAsync(employeeId.Value))
                : "";
            if (IsDisposed) return;
            txtBonCard.Text = EmployeeBonCardStore.Format(_loadedBonCard);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ucEmployeeBankAccounts.LoadDataAsync");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
    }

    public async Task<bool> SaveDataAsync(int employeeId)
    {
        if (IsDisposed) return false;

        // بن کارت (برای کارمند جدید یا تغییرات ذخیره‌نشده)
        string bon = EmployeeBonCardStore.Normalize(txtBonCard.Text);
        if (bon != _loadedBonCard)
        {
            if (bon.Length != 0 && !EmployeeBonCardStore.IsValidFormat(bon))
            {
                ModernMessageBox.ShowWarning("شماره بن کارت باید دقیقاً ۱۶ رقم باشد.");
                return false;
            }
            try
            {
                await EmployeeBonCardStore.SaveAsync(employeeId, bon);
                _loadedBonCard = bon;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ucEmployeeBankAccounts.SaveBonCard");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره بن کارت:\n" + ex.Message);
                return false;
            }
        }

        if (!_bankAccounts.Any(b => b.Id <= 0)) return true;

        foreach (var acc in _bankAccounts.Where(b => b.Id <= 0))
        {
            acc.EmployeeId = employeeId;
            try
            {
                await _bankAccountRepo.AddAsync(acc);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ucEmployeeBankAccounts.SaveData");
                if (!IsDisposed) ModernMessageBox.ShowError($"خطا در ذخیره حساب بانکی {acc.ShebaNumber}:\n{ex.Message}");
                return false;
            }
        }
        return true;
    }

    private void BuildUI()
    {
        const int F = ModernUI.FieldHeight;

        // ─── جدول ───
        dgvBankAccounts = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        dgvBankAccounts.ApplyModernStyle();
        dgvBankAccounts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;   // شماره شبا کامل و بدون ... نمایش داده شود
        dgvBankAccounts.UseAiryLook(44);
        dgvBankAccounts.UseSelectionAccent();
        dgvBankAccounts.UseBadges("AccountType", new Dictionary<string, Color> { ["حقوق"] = ModernTheme.Success, ["بن غیر نقدی"] = ModernTheme.Orange });
        dgvBankAccounts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ShebaNumber", HeaderText = "شماره شبا", FillWeight = 45, MinimumWidth = 230 });
        dgvBankAccounts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BankName", HeaderText = "نام بانک", FillWeight = 25 });
        dgvBankAccounts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AccountType", HeaderText = "نوع حساب", FillWeight = 20 });
        dgvBankAccounts.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsMainAccount", HeaderText = "حساب اصلی", FillWeight = 15 });

        var gridCard = new ModernCard("🏦  حساب‌های بانکی کارمند", ModernTheme.Teal, 1);
        gridCard.Margin = new Padding(0, 0, 0, 12);
        gridCard.Body.Controls.Add(dgvBankAccounts, 0, 0);

        // ─── ورودی‌ها ───
        txtShebaNumber = new TextBox { TextAlign = HorizontalAlignment.Center };
        cmbAccountType = ModernUI.Combo();
        cmbAccountType.Items.AddRange(new[] { "حقوق", "بن غیر نقدی" });
        cmbAccountType.SelectedIndex = 0;

        chkIsMainAccount = ModernUI.Check("این حساب اصلی است", ModernTheme.Success);
        chkIsMainAccount.Width = 200;

        var inputCard = new ModernCard("✏️  ثبت / ویرایش حساب", ModernTheme.Orange, 3, F, F);
        var tb = inputCard.Body;
        var fldSheba = ModernUI.Field("شماره شبا (بدون IR)", txtShebaNumber);
        tb.Controls.Add(fldSheba, 0, 0);
        tb.SetColumnSpan(fldSheba, 3);
        tb.Controls.Add(ModernUI.Field("نوع حساب", cmbAccountType), 0, 1);
        var cellMain = ModernUI.CenterInCell(chkIsMainAccount);
        tb.Controls.Add(cellMain, 1, 1);
        tb.SetColumnSpan(cellMain, 2);

        // ─── بن کارت ───
        txtBonCard = new TextBox { TextAlign = HorizontalAlignment.Center, MaxLength = 24, RightToLeft = RightToLeft.No };
        btnSaveBonCard = ModernButton.Create("💾  ذخیره بن کارت", ModernTheme.Orange, 170, 40);

        var bonCard = new ModernCard("💳  شماره بن کارت (۱۶ رقمی)", ModernTheme.Teal, 3, F);
        bonCard.Margin = new Padding(0, 0, 0, 12);
        var bb = bonCard.Body;
        var fldBon = ModernUI.Field("شماره کارت (۱۶ رقم)", txtBonCard);
        bb.Controls.Add(fldBon, 0, 0);
        bb.SetColumnSpan(fldBon, 2);
        bb.Controls.Add(ModernUI.CenterInCell(btnSaveBonCard), 2, 0);

        // ─── دکمه‌ها ───
        var flpBankBtns = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = Color.Transparent
        };
        btnAddBankAcc = ModernButton.Create("افزودن / ذخیره حساب", ModernTheme.Success, 210, 44);
        btnDeleteBankAcc = ModernButton.Create("🗑  حذف حساب", ModernTheme.Danger, 150, 44, ModernButtonStyle.Soft);
        btnImportExcel = ModernButton.Create("📤  ایمپورت از اکسل", ModernTheme.Success, 175, 44, ModernButtonStyle.Soft);
        btnDownloadTemplate = ModernButton.Create("📥  دانلود قالب اکسل", ModernTheme.Neutral, 175, 44, ModernButtonStyle.Outline);

        flpBankBtns.Controls.Add(btnAddBankAcc);
        flpBankBtns.Controls.Add(btnDeleteBankAcc);
        flpBankBtns.Controls.Add(btnImportExcel);
        flpBankBtns.Controls.Add(btnDownloadTemplate);

        // ─── چیدمان کلی ───
        var mainBankLayout = new BufferedTable
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(20, 14, 20, 6),
            BackColor = ModernTheme.Background
        };
        mainBankLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        mainBankLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        mainBankLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, bonCard.Height + 12));
        mainBankLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, inputCard.Height + 12));
        mainBankLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62f));

        bonCard.Dock = DockStyle.Fill;
        inputCard.Dock = DockStyle.Fill;
        inputCard.Margin = new Padding(0, 0, 0, 12);
        gridCard.Dock = DockStyle.Fill;

        mainBankLayout.Controls.Add(gridCard, 0, 0);
        mainBankLayout.Controls.Add(bonCard, 0, 1);
        mainBankLayout.Controls.Add(inputCard, 0, 2);
        mainBankLayout.Controls.Add(flpBankBtns, 0, 3);

        this.Controls.Add(mainBankLayout);
    }

    private void ClearForm()
    {
        if (IsDisposed) return;
        _editingBankAccId = null;
        btnAddBankAcc.Text = "افزودن / ذخیره حساب";
        txtShebaNumber.Clear();
        cmbAccountType.SelectedIndex = 0;
        chkIsMainAccount.Checked = false;

        if (dgvBankAccounts.Rows.Count > 0)
        {
            dgvBankAccounts.ClearSelection();
            dgvBankAccounts.CurrentCell = null;
        }
    }

    private void DgvBankAccounts_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || IsDisposed) return;
        if (dgvBankAccounts.Rows[e.RowIndex].DataBoundItem is not EmployeeBankAccount acc) return;
        _editingBankAccId = acc.Id;
        txtShebaNumber.Text = acc.ShebaNumber?.Replace("IR", "") ?? "";
        cmbAccountType.SelectedItem = acc.AccountType;
        chkIsMainAccount.Checked = acc.IsMainAccount;
        btnAddBankAcc.Text = "ذخیره تغییرات";
    }

    private async Task AddBankAccSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (string.IsNullOrWhiteSpace(txtShebaNumber.Text))
            {
                ModernMessageBox.ShowWarning("شماره شبا الزامی است.");
                return;
            }

            string cleanSheba = StringHelper.ConvertToEnglishDigits(txtShebaNumber.Text).Replace("-", "").Replace(" ", "").ToUpper().Trim();
            if (cleanSheba.StartsWith("IR")) cleanSheba = cleanSheba.Substring(2);

            if (cleanSheba.Length != 24 || !cleanSheba.All(char.IsDigit))
            {
                ModernMessageBox.ShowWarning("شماره شبا باید ۲۴ رقم باشد.");
                return;
            }

            if (!ShebaHelper.IsValid("IR" + cleanSheba))
            {
                ModernMessageBox.ShowWarning("شماره شبا وارد شده معتبر نیست.");
                return;
            }

            var acc = new EmployeeBankAccount
            {
                Id = _editingBankAccId ?? 0,
                EmployeeId = GetParentEmployeeId(),
                AccountNumber = cleanSheba.Substring(6, 18),
                ShebaNumber = "IR" + cleanSheba,
                BankCode = BankHelper.GetBankCodeFromSheba(cleanSheba) ?? "",
                BankName = BankHelper.GetBankNameFromSheba(cleanSheba),
                IsMainAccount = chkIsMainAccount.Checked,
                AccountType = cmbAccountType.SelectedItem?.ToString() ?? "حقوق",
                IsActive = true // در پس‌زمینه همیشه فعال است
            };

            bool isEditing = _editingBankAccId.HasValue && _editingBankAccId.Value > 0;
            int parentFormEmpId = GetParentEmployeeId();

            if (parentFormEmpId > 0)
            {
                bool success = await AsyncHelper.RunWithLoading(this.FindForm()!, async () =>
                {
                    if (isEditing) await _bankAccountRepo.UpdateAsync(acc);
                    else await _bankAccountRepo.AddAsync(acc);
                }, "در حال ذخیره حساب بانکی...");

                if (success && !IsDisposed) await LoadDataAsync(parentFormEmpId);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ucEmployeeBankAccounts.AddBankAccSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ucEmployeeBankAccounts"); }
        }
    }

    private async Task SaveBonCardNowAsync()
    {
        if (IsDisposed) return;
        int empId = GetParentEmployeeId();
        if (empId <= 0)
        {
            ModernMessageBox.ShowInfo("بن کارت همراه با ذخیره‌ی اطلاعات کارمند ثبت می‌شود (دکمه‌ی «ذخیره» پایین فرم).");
            return;
        }
        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            string bon = EmployeeBonCardStore.Normalize(txtBonCard.Text);
            if (bon.Length != 0 && !EmployeeBonCardStore.IsValidFormat(bon))
            {
                ModernMessageBox.ShowWarning("شماره بن کارت باید دقیقاً ۱۶ رقم باشد.");
                return;
            }
            if (bon.Length == 16 && !EmployeeBonCardStore.PassesLuhn(bon) &&
                !ModernMessageBox.AskConfirm("این شماره کارت از اعتبارسنجی استاندارد عبور نکرد (احتمال اشتباه در یک رقم).\nبا این حال ذخیره شود؟", "بررسی شماره کارت"))
                return;

            bool ok = await AsyncHelper.RunWithLoading(this.FindForm()!, async () =>
            {
                await EmployeeBonCardStore.SaveAsync(empId, bon);
            }, "در حال ذخیره بن کارت...");

            if (ok && !IsDisposed)
            {
                _loadedBonCard = bon;
                txtBonCard.Text = EmployeeBonCardStore.Format(bon);
                ToastHelper.Show(bon.Length == 0 ? "بن کارت پاک شد." : "بن کارت ذخیره شد.", true);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ucEmployeeBankAccounts.SaveBonCardNow");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { LogHelper.LogException(__ex, "ucEmployeeBankAccounts"); } }
    }

    private async Task DeleteBankAccSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (dgvBankAccounts.CurrentRow?.DataBoundItem is not EmployeeBankAccount acc) return;
            if (!ModernMessageBox.AskConfirm("آیا از حذف این حساب مطمئن هستید؟", "حذف حساب بانکی")) return;

            int parentFormEmpId = GetParentEmployeeId();
            if (parentFormEmpId > 0 && acc.Id > 0)
            {
                bool success = await AsyncHelper.RunWithLoading(this.FindForm()!, async () =>
                {
                    await _bankAccountRepo.DeleteAsync(acc.Id);
                }, "در حال حذف...");

                if (success && !IsDisposed) await LoadDataAsync(parentFormEmpId);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ucEmployeeBankAccounts.DeleteBankAccSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ucEmployeeBankAccounts"); }
        }
    }

    private int GetParentEmployeeId()
    {
        if (this.ParentForm is EmployeeEditForm parentForm)
            return parentForm.CurrentEmployeeId;
        return 0;
    }

    private void DownloadTemplate()
    {
        using var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = "BankAccounts_Import_Template.xlsx", Title = "ذخیره قالب اکسل" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("شباها");
            ws.RightToLeft = true;

            string[] headers = { "کد پرسنلی", "شماره شبا (با یا بدون IR)", "نوع حساب (حقوق / بن غیر نقدی)", "حساب اصلی (بله/خیر)" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            ws.Cell(2, 1).Value = "1001";
            ws.Cell(2, 2).Value = "120170000000000000000001";
            ws.Cell(2, 3).Value = "حقوق";
            ws.Cell(2, 4).Value = "بله";

            ws.Range("C2:C1000").CreateDataValidation().List("\"حقوق,بن غیر نقدی\"");
            ws.Range("D2:D1000").CreateDataValidation().List("\"بله,خیر\"");

            ws.Columns().AdjustToContents();
            wb.SaveAs(sfd.FileName);
            ToastHelper.Show("قالب اکسل ذخیره شد.", true);
        }
        catch (Exception ex) { ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
    }

    private async Task ImportExcelAsync()
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            int successCount = 0;
            List<string> errors = new();

            bool success = await AsyncHelper.RunWithLoading(this.FindForm()!, async () =>
            {
                using var wb = new XLWorkbook(ofd.FileName);
                var ws = wb.Worksheet(1);
                var rows = ws.RowsUsed().Skip(1);

                using var conn = new SqlConnection(AppConfig.ConnectionString);
                await conn.OpenAsync();

                foreach (var row in rows)
                {
                    try
                    {
                        string pCode = row.Cell(1).GetString().Trim();
                        string shebaRaw = row.Cell(2).GetString().Trim();
                        string type = row.Cell(3).GetString().Trim();
                        string isMainStr = row.Cell(4).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(pCode) || string.IsNullOrWhiteSpace(shebaRaw)) continue;

                        int empId = 0;
                        using (var cmd = new SqlCommand("SELECT TOP 1 Id FROM Employees WHERE PersonnelCode = @code", conn))
                        {
                            cmd.Parameters.AddWithValue("@code", pCode);
                            var res = await cmd.ExecuteScalarAsync();
                            if (res is null || res == DBNull.Value) throw new Exception($"کارمند با کد {pCode} یافت نشد.");
                            empId = Convert.ToInt32(res);
                        }

                        string cleanSheba = BankHelper.NormalizeSheba(shebaRaw);
                        if (cleanSheba.Length != 24) throw new Exception("طول شبا نامعتبر است.");

                        var acc = new EmployeeBankAccount
                        {
                            EmployeeId = empId,
                            ShebaNumber = "IR" + cleanSheba,
                            AccountNumber = cleanSheba.Substring(6, 18),
                            BankCode = BankHelper.GetBankCodeFromSheba(cleanSheba) ?? "",
                            BankName = BankHelper.GetBankNameFromSheba(cleanSheba),
                            AccountType = string.IsNullOrWhiteSpace(type) ? "حقوق" : type,
                            IsMainAccount = isMainStr == "بله" || isMainStr == "1" || isMainStr == "True",
                            IsActive = true
                        };

                        await _bankAccountRepo.AddAsync(acc);
                        successCount++;
                    }
                    catch (Exception ex) { errors.Add($"ردیف {row.RowNumber()}: {ex.Message}"); }
                }
            }, "در حال ایمپورت از اکسل...");

            if (success && !IsDisposed)
            {
                string msg = $"✅ تعداد {successCount} شبا ثبت شد.";
                if (errors.Count > 0) msg += $"\n\n⚠️ {errors.Count} خطا:\n" + string.Join("\n", errors.Take(5));
                ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");

                int parentFormEmpId = GetParentEmployeeId();
                if (parentFormEmpId > 0) await LoadDataAsync(parentFormEmpId);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ucEmployeeBankAccounts.ImportExcel");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ucEmployeeBankAccounts"); } }
    }
}
