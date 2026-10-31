using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class BankAccountManagementForm : Form
    {
        private readonly BankAccountRepository _repo;
        private DataGridView dgv = null!;
        private TextBox txtTitle = null!, txtAccountNumber = null!, txtInitialBalance = null!;
        private CheckBox chkIsCashDesk = null!, chkIsActive = null!;
        private Button btnSave = null!, btnDelete = null!, btnNew = null!;
        private int _editingId = 0;

        public BankAccountManagementForm(string connectionString)
        {
            _repo = new BankAccountRepository(connectionString);
            BuildUI();
            this.ApplyStandardHeader("مدیریت حساب‌های بانکی و صندوق", new Size(850, 600));
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            Shown += async (s, e) => await LoadDataAsync();
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));

            dgv = new DataGridView { Dock = DockStyle.Fill }; dgv.ApplyModernStyle(); dgv.SelectionChanged += Dgv_SelectionChanged;

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Padding = new Padding(10) };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F)); formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F)); formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));

            txtTitle = new TextBox(); txtAccountNumber = new TextBox();
            txtInitialBalance = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtInitialBalance);
            chkIsCashDesk = new CheckBox { Text = "این یک صندوق است" }; chkIsActive = new CheckBox { Text = "حساب فعال است", Checked = true };

            UIHelper.AddField(formLayout, "عنوان حساب:", txtTitle, 0, 0, 1);
            UIHelper.AddField(formLayout, "شماره حساب:", txtAccountNumber, 0, 2, 3);
            UIHelper.AddField(formLayout, "موجودی اولیه:", txtInitialBalance, 1, 0, 1);

            var flpChk = new FlowLayoutPanel { Dock = DockStyle.Fill }; flpChk.Controls.Add(chkIsCashDesk); flpChk.Controls.Add(chkIsActive);
            formLayout.Controls.Add(flpChk, 1, 2); formLayout.SetColumnSpan(flpChk, 2);

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft };
            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.ActionGreen, 120, 40);
            btnDelete = UIHelper.CreateButton("🗑️ حذف", UIHelper.ActionRed, 100, 40);
            btnNew = UIHelper.CreateButton("➕ جدید", UIHelper.SectionBlue, 100, 40);

            btnSave.Click += BtnSave_Click; btnDelete.Click += BtnDelete_Click;
            btnNew.Click += (s, e) => { _editingId = 0; txtTitle.Clear(); txtAccountNumber.Clear(); txtInitialBalance.Text = "0"; chkIsCashDesk.Checked = false; btnDelete.Enabled = false; };

            btnPanel.Controls.Add(btnSave); btnPanel.Controls.Add(btnDelete); btnPanel.Controls.Add(btnNew);
            formLayout.Controls.Add(btnPanel, 2, 2); formLayout.SetColumnSpan(btnPanel, 2);

            mainLayout.Controls.Add(dgv, 0, 0); mainLayout.Controls.Add(formLayout, 0, 1); Controls.Add(mainLayout);
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var accounts = await _repo.GetAllAsync(); dgv.DataSource = accounts;
                if (dgv.Columns.Contains("Id")) dgv.Columns["Id"].Visible = false;
                if (dgv.Columns.Contains("CreatedAt")) dgv.Columns["CreatedAt"].Visible = false;
            }
            catch (Exception ex) { ModernMessageBox.ShowError(ex.Message); }
        }

        private void Dgv_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgv.CurrentRow?.DataBoundItem is not CompanyBankAccount acc) return;
            _editingId = acc.Id; txtTitle.Text = acc.Title; txtAccountNumber.Text = acc.AccountNumber;
            txtInitialBalance.Text = NumberHelper.FormatThousands(acc.InitialBalance);
            chkIsCashDesk.Checked = acc.IsCashDesk; chkIsActive.Checked = acc.IsActive; btnDelete.Enabled = true;
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text)) { ModernMessageBox.ShowWarning("عنوان الزامی است."); return; }
            var acc = new CompanyBankAccount { Id = _editingId, Title = txtTitle.Text.Trim(), AccountNumber = txtAccountNumber.Text.Trim(), InitialBalance = NumberHelper.ParseFormatted(txtInitialBalance.Text), IsCashDesk = chkIsCashDesk.Checked, IsActive = chkIsActive.Checked };
            bool success = await AsyncHelper.RunWithLoading(this, async () => {
                if (_editingId == 0) await _repo.AddAsync(acc); else await _repo.UpdateAsync(acc);
            }, "در حال ذخیره...");
            if (success) { ToastHelper.Show("ذخیره شد.", true); await LoadDataAsync(); }
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (_editingId == 0) return;
            if (!ModernMessageBox.AskConfirm("آیا از حذف این حساب مطمئن هستید؟", "حذف")) return;
            bool success = await AsyncHelper.RunWithLoading(this, async () => { await _repo.DeleteAsync(_editingId); }, "در حال حذف...");
            if (success) { ToastHelper.Show("حذف شد.", true); await LoadDataAsync(); }
        }
    }
}