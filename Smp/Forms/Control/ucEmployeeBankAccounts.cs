using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;
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

namespace SMP.Forms.Controls
{
    public class ucEmployeeBankAccounts : UserControl
    {
        private readonly EmployeeBankAccountRepository _bankAccountRepo;
        private readonly List<EmployeeBankAccount> _bankAccounts = new();
        private int? _editingBankAccId = null;

        private DataGridView dgvBankAccounts = null!;
        private TextBox txtShebaNumber = null!;
        private ComboBox cmbAccountType = null!;
        private CheckBox chkIsMainAccount = null!;
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
            btnDownloadTemplate.Click += (s, e) => DownloadTemplate();
            btnImportExcel.Click += async (s, e) => await ImportExcelAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
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
            var mainBankLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            mainBankLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            mainBankLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30f));
            mainBankLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20f));

            dgvBankAccounts = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
            dgvBankAccounts.ApplyModernStyle();
            dgvBankAccounts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ShebaNumber", HeaderText = "شماره شبا", FillWeight = 40 });
            dgvBankAccounts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BankName", HeaderText = "نام بانک", FillWeight = 25 });
            dgvBankAccounts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AccountType", HeaderText = "نوع حساب", FillWeight = 20 });
            dgvBankAccounts.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsMainAccount", HeaderText = "حساب اصلی", FillWeight = 15 });

            mainBankLayout.Controls.Add(dgvBankAccounts, 0, 0);

            var pnlInputs = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 10, 15, 10), BackColor = UIHelper.WarmPanelBackground };

            // ✅ FIX: چیدمان ۴ ستونه برای نظم دقیق
            var tlpBankInputs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Margin = new Padding(0) };

            tlpBankInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F)); // ستون 0: لیبل‌ها
            tlpBankInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F)); // ستون 1: فیلدهای ورودی
            tlpBankInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F)); // ستون 2: چک‌باکس
            tlpBankInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // ستون 3: فضای خالی

            tlpBankInputs.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            tlpBankInputs.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));

            // ─── ردیف 0: شماره شبا ───
            var lblSheba = new Label { Text = "شماره شبا (IR بدون):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText };
            txtShebaNumber = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(5, 8, 5, 5), Font = UIHelper.PersianFont(11f) };

            tlpBankInputs.Controls.Add(lblSheba, 0, 0);
            tlpBankInputs.Controls.Add(txtShebaNumber, 1, 0);
            tlpBankInputs.SetColumnSpan(txtShebaNumber, 3); // شبا تمام عرض را می‌گیرد

            // ─── ردیف 1: نوع حساب و تیک حساب اصلی ───
            var lblAccountType = new Label { Text = "نوع حساب:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText };
            cmbAccountType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(5, 8, 5, 5), Font = UIHelper.PersianFont(11f) };
            cmbAccountType.Items.AddRange(new[] { "حقوق", "بن غیر نقدی" });
            cmbAccountType.SelectedIndex = 0;

            // ✅ FIX: چک‌باکس حالا در ستون 2 (کنار کومبوباکس) قرار دارد
            chkIsMainAccount = new CheckBox { Text = "این حساب اصلی است", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(15, 12, 5, 0) };
            UIHelper.StyleToggle(chkIsMainAccount);

            tlpBankInputs.Controls.Add(lblAccountType, 0, 1);
            tlpBankInputs.Controls.Add(cmbAccountType, 1, 1);
            tlpBankInputs.Controls.Add(chkIsMainAccount, 2, 1);

            pnlInputs.Controls.Add(tlpBankInputs);
            mainBankLayout.Controls.Add(pnlInputs, 0, 1);

            var flpBankBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10), BackColor = UIHelper.WarmPanelBackground };
            btnDeleteBankAcc = UIHelper.CreateButton("حذف حساب", UIHelper.WarmDanger, 120, 42);
            btnAddBankAcc = UIHelper.CreateButton("افزودن / ذخیره حساب", UIHelper.WarmPrimary, 180, 42);
            btnDownloadTemplate = UIHelper.CreateButton("📥 دانلود قالب اکسل", UIHelper.WarmSecondary, 160, 42);
            btnImportExcel = UIHelper.CreateButton("📤 ایمپورت از اکسل", UIHelper.ActionGreen, 160, 42);

            flpBankBtns.Controls.Add(btnDeleteBankAcc);
            flpBankBtns.Controls.Add(btnAddBankAcc);
            flpBankBtns.Controls.Add(btnDownloadTemplate);
            flpBankBtns.Controls.Add(btnImportExcel);
            mainBankLayout.Controls.Add(flpBankBtns, 0, 2);

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
                try { _actionLock.Release(); } catch { }
            }
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
                try { _actionLock.Release(); } catch { }
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
                                if (res == null || res == DBNull.Value) throw new Exception($"کارمند با کد {pCode} یافت نشد.");
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
            finally { try { _actionLock.Release(); } catch { } }
        }
    }
}