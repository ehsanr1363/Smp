using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Models;
using SMP.Helpers;

namespace SMP.Forms
{
    public class DepartmentManagementForm : Form
    {
        private DataGridView dgv = null!;
        private TextBox txtCode = null!;
        private TextBox txtName = null!;
        private TextBox txtAddress = null!;
        private Button btnSave = null!, btnDelete = null!, btnNew = null!;

        private readonly DepartmentRepository _repo;
        private int _selectedId = 0;

        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public DepartmentManagementForm(string connectionString)
        {
            _repo = new DepartmentRepository(connectionString);

            BuildUI();

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave, btnNew);
            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _loadLock?.Dispose(); } catch { }
                try { _actionLock?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var (formLayout, grid, bottomPanel) = StandardFormHelper.SetupMasterDetailLayout(
                this, "مدیریت واحدهای سازمانی", new Size(950, 680));

            dgv = grid;
            dgv.AutoGenerateColumns = false;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", DataPropertyName = "Code", HeaderText = "کد واحد", FillWeight = 20 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", DataPropertyName = "Name", HeaderText = "عنوان واحد", FillWeight = 40 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Address", DataPropertyName = "Address", HeaderText = "آدرس", FillWeight = 40 });
            dgv.CellClick += Dgv_CellClick;
            dgv.SelectionChanged += (s, e) => UpdateButtonsState();

            txtCode = new TextBox();
            txtName = new TextBox();
            txtAddress = new TextBox();

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "کد واحد:", txtCode, 0, 0, 1);
            UIHelper.AddField(formLayout, "عنوان واحد:", txtName, 0, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "آدرس واحد:", txtAddress, 1, 0, 1);
            formLayout.SetColumnSpan(txtAddress, 3);

            btnSave = UIHelper.CreateButton("💾 ذخیره / ویرایش", UIHelper.WarmPrimary, 150, 42);
            btnDelete = UIHelper.CreateButton("🗑️ حذف", UIHelper.WarmDanger, 100, 42);
            btnNew = UIHelper.CreateButton("➕ جدید", UIHelper.WarmSecondary, 100, 42);

            btnDelete.Enabled = false;
            btnSave.Click += async (s, e) => await SaveSafeAsync();
            btnDelete.Click += async (s, e) => await DeleteSafeAsync();
            btnNew.Click += (s, e) => BtnNew_Click(s, e);

            bottomPanel.Controls.Add(btnSave);
            bottomPanel.Controls.Add(btnDelete);
            bottomPanel.Controls.Add(btnNew);
        }

        private async Task LoadDataSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;
            try
            {
                var (success, list) = await AsyncHelper.RunWithLoading(this, () => Task.Run(() => _repo.GetAll()), "در حال بارگذاری...");
                if (!success || list == null || IsDisposed) return;
                dgv.DataSource = list;
                UpdateButtonsState();
            }
            catch (Exception ex) { LogHelper.LogException(ex, "DepartmentManagementForm.LoadDataSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
            finally { try { _loadLock.Release(); } catch { } }
        }

        private void UpdateButtonsState()
        {
            if (IsDisposed) return;
            btnDelete.Enabled = _selectedId > 0;
        }

        private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgv.Rows[e.RowIndex].DataBoundItem is Department dept)
            {
                _selectedId = dept.Id;
                txtCode.Text = dept.Code ?? "";
                txtName.Text = dept.Name ?? "";
                txtAddress.Text = dept.Address ?? "";
                UpdateButtonsState();
            }
        }

        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;
            try
            {
                if (string.IsNullOrWhiteSpace(txtName.Text)) { ModernMessageBox.ShowWarning("عنوان واحد الزامی است."); txtName.Focus(); return; }
                var dept = new Department { Id = _selectedId, Code = txtCode.Text.Trim(), Name = txtName.Text.Trim(), Address = txtAddress.Text.Trim() };
                if (_selectedId == 0) DateStampHelper.StampCreated(dept); else DateStampHelper.StampUpdated(dept);

                bool success = await AsyncHelper.RunWithLoading(this, async () => {
                    if (_selectedId == 0) await Task.Run(() => _repo.Add(dept));
                    else await Task.Run(() => _repo.Update(dept));
                }, "در حال ذخیره اطلاعات...");

                if (success && !IsDisposed) { ToastHelper.Show("اطلاعات با موفقیت ذخیره شد.", isSuccess: true); BtnNew_Click(null, EventArgs.Empty); await LoadDataSafeAsync(); }
            }
            catch (Exception ex) { LogHelper.LogException(ex, "DepartmentManagementForm.SaveSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
            finally { try { _actionLock.Release(); } catch { } }
        }

        private async Task DeleteSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;
            try
            {
                if (_selectedId == 0) { ModernMessageBox.ShowWarning("لطفاً ابتدا یک واحد را انتخاب کنید."); return; }
                if (!ModernMessageBox.AskConfirm("آیا از حذف این واحد مطمئن هستید؟", "حذف واحد سازمانی")) return;

                bool success = await AsyncHelper.RunWithLoading(this, async () => { await Task.Run(() => _repo.Delete(_selectedId)); }, "در حال حذف...");
                if (success && !IsDisposed) { ToastHelper.Show("واحد سازمانی با موفقیت حذف شد.", isSuccess: true); BtnNew_Click(null, EventArgs.Empty); await LoadDataSafeAsync(); }
            }
            catch (Exception ex) { LogHelper.LogException(ex, "DepartmentManagementForm.DeleteSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
            finally { try { _actionLock.Release(); } catch { } }
        }

        private void BtnNew_Click(object? sender, EventArgs e)
        {
            _selectedId = 0;
            txtCode.Clear();
            txtName.Clear();
            txtAddress.Clear();
            if (!IsDisposed) { dgv.ClearSelection(); UpdateButtonsState(); txtName.Focus(); }
        }
    }
}