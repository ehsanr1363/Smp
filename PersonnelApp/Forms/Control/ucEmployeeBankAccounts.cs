using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using PersonnelApp.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms.Controls
{
    public class ucEmployeeBankAccounts : UserControl
    {
        private readonly EmployeeBankAccountRepository _bankAccountRepo;
        private readonly List<EmployeeBankAccount> _bankAccounts = new();
        private int? _editingBankAccId = null;

        private DataGridView dgvBankAccounts = null!;
        private TextBox txtShebaNumber = null!;
        private CheckBox chkIsMainAccount = null!;
        private Button btnAddBankAcc = null!, btnDeleteBankAcc = null!;

        public ucEmployeeBankAccounts(EmployeeBankAccountRepository bankAccountRepo)
        {
            this.Dock = DockStyle.Fill;
            this.RightToLeft = RightToLeft.Yes;

            _bankAccountRepo = bankAccountRepo;
            BuildUI();

            dgvBankAccounts.CellClick += DgvBankAccounts_CellClick;
            btnAddBankAcc.Click += BtnAddBankAcc_Click;
            btnDeleteBankAcc.Click += BtnDeleteBankAcc_Click;
        }

        public async Task LoadDataAsync(int? employeeId)
        {
            _bankAccounts.Clear();
            if (employeeId.HasValue && employeeId.Value > 0)
            {
                var list = await _bankAccountRepo.GetByEmployeeIdAsync(employeeId.Value);
                _bankAccounts.AddRange(list);
            }
            dgvBankAccounts.DataSource = new BindingList<EmployeeBankAccount>(_bankAccounts);
            ClearForm();
        }

        public async Task<bool> SaveDataAsync(int employeeId)
        {
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
                    ModernMessageBox.ShowError($"خطا در ذخیره حساب بانکی {acc.ShebaNumber}:\n{ex.Message}");
                    return false;
                }
            }
            return true;
        }

        private void BuildUI()
        {
            var mainBankLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            mainBankLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            mainBankLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35f));
            mainBankLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 15f));

            dgvBankAccounts = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
            dgvBankAccounts.ApplyModernStyle();
            dgvBankAccounts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ShebaNumber", HeaderText = "شماره شبا", FillWeight = 50 });
            dgvBankAccounts.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BankName", HeaderText = "نام بانک", FillWeight = 30 });
            dgvBankAccounts.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsMainAccount", HeaderText = "حساب اصلی", FillWeight = 20 });

            mainBankLayout.Controls.Add(dgvBankAccounts, 0, 0);

            var tlpBankInputs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Padding = new Padding(10) };
            for (int i = 0; i < 4; i++) tlpBankInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            for (int i = 0; i < 3; i++) tlpBankInputs.RowStyles.Add(new RowStyle(SizeType.Percent, 33f));

            UIHelper.AddField(tlpBankInputs, "شماره شبا (IR بدون):", txtShebaNumber = new TextBox(), row: 0, labelCol: 0, controlCol: 1);
            tlpBankInputs.SetColumnSpan(txtShebaNumber, 3);

            chkIsMainAccount = new CheckBox { Text = "این حساب اصلی است", AutoSize = true, Anchor = AnchorStyles.Right };
            UIHelper.StyleToggle(chkIsMainAccount);
            tlpBankInputs.Controls.Add(chkIsMainAccount, 1, 1);
            tlpBankInputs.SetColumnSpan(chkIsMainAccount, 3);

            mainBankLayout.Controls.Add(tlpBankInputs, 0, 1);

            var flpBankBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
            btnDeleteBankAcc = UIHelper.CreateButton("حذف حساب", UIHelper.WarmDanger, 130, 42);
            btnAddBankAcc = UIHelper.CreateButton("افزودن / ذخیره حساب", UIHelper.WarmPrimary, 180, 42);
            flpBankBtns.Controls.Add(btnDeleteBankAcc);
            flpBankBtns.Controls.Add(btnAddBankAcc);
            mainBankLayout.Controls.Add(flpBankBtns, 0, 2);

            this.Controls.Add(mainBankLayout);
        }

        private void ClearForm()
        {
            _editingBankAccId = null;
            btnAddBankAcc.Text = "افزودن / ذخیره حساب";
            txtShebaNumber.Clear();
            chkIsMainAccount.Checked = false;
        }

        private void DgvBankAccounts_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvBankAccounts.Rows[e.RowIndex].DataBoundItem is not EmployeeBankAccount acc) return;
            _editingBankAccId = acc.Id;
            txtShebaNumber.Text = acc.ShebaNumber?.Replace("IR", "") ?? "";
            chkIsMainAccount.Checked = acc.IsMainAccount;
            btnAddBankAcc.Text = "ذخیره تغییرات";
        }

        private async void BtnAddBankAcc_Click(object? sender, EventArgs e)
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
                ModernMessageBox.ShowWarning("شماره شبا وارد شده معتبر نیست (خطای چک‌سام).");
                return;
            }

            string bankCode = BankHelper.GetBankCodeFromSheba(cleanSheba) ?? "";

            var acc = new EmployeeBankAccount
            {
                Id = _editingBankAccId ?? 0,
                EmployeeId = 0,
                AccountNumber = cleanSheba.Substring(6, 18),
                ShebaNumber = "IR" + cleanSheba,
                BankCode = bankCode,
                BankName = BankHelper.GetBankNameFromSheba(cleanSheba),
                IsMainAccount = chkIsMainAccount.Checked
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

                if (success) await LoadDataAsync(parentFormEmpId);
            }
            else
            {
                if (isEditing)
                {
                    var existing = _bankAccounts.FirstOrDefault(x => x.Id == acc.Id);
                    if (existing != null)
                    {
                        if (acc.IsMainAccount) foreach (var a in _bankAccounts.Where(a => a.Id != acc.Id)) a.IsMainAccount = false;
                        _bankAccounts[_bankAccounts.IndexOf(existing)] = acc;
                        if (!acc.IsMainAccount && !_bankAccounts.Any(a => a.IsMainAccount)) acc.IsMainAccount = true;
                    }
                }
                else
                {
                    bool isMain = acc.IsMainAccount || _bankAccounts.Count == 0;
                    if (isMain) { foreach (var a in _bankAccounts) a.IsMainAccount = false; acc.IsMainAccount = true; }
                    acc.Id = -_bankAccounts.Count - 1;
                    _bankAccounts.Add(acc);
                }
                dgvBankAccounts.DataSource = new BindingList<EmployeeBankAccount>(_bankAccounts);
                ClearForm();
            }
        }

        private async void BtnDeleteBankAcc_Click(object? sender, EventArgs e)
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

                if (success) await LoadDataAsync(parentFormEmpId);
            }
            else
            {
                _bankAccounts.Remove(acc);
                if (acc.IsMainAccount && _bankAccounts.Count > 0) _bankAccounts[0].IsMainAccount = true;
                dgvBankAccounts.DataSource = new BindingList<EmployeeBankAccount>(_bankAccounts);
                ClearForm();
            }
        }

        private int GetParentEmployeeId()
        {
            if (this.ParentForm is EmployeeEditForm parentForm)
                return parentForm.CurrentEmployeeId;
            return 0;
        }
    }
}