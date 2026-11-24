using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Models;
using Smp.Helpers;

namespace Smp.Forms;

public class DepartmentManagementForm : Form
{
    private DataGridView dgv = null!;
    private ModernTextBox txtCode = null!;
    private ModernTextBox txtName = null!;
    private ModernTextBox txtAddress = null!;
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
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "DepartmentManagementForm"); }
            try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "DepartmentManagementForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── جدول ──
        dgv = ModernGrid.Create();
        dgv.AutoGenerateColumns = false;
        dgv.FillColumns();
        dgv.UseAiryLook(46);
        dgv.UseSelectionAccent(ModernTheme.Primary);
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", DataPropertyName = "Code", HeaderText = "کد واحد", FillWeight = 15, MinimumWidth = 90, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", DataPropertyName = "Name", HeaderText = "عنوان واحد", FillWeight = 35, MinimumWidth = 140 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Address", DataPropertyName = "Address", HeaderText = "آدرس", FillWeight = 50, MinimumWidth = 180 });
        ModernListKit.PinHeaders(dgv);   // جلوگیری از جایگزینی «عنوان واحد» با «نام / شرکت»
        dgv.CellClick += Dgv_CellClick;
        dgv.SelectionChanged += (s, e) => UpdateButtonsState();

        // ── فرم ──
        txtCode = ModernUI.Input();
        txtName = ModernUI.Input();
        txtAddress = ModernUI.Input();

        int rh = ModernUI.RowHeight;
        var card = new ModernCard("مشخصات واحد", ModernTheme.Primary, 2, rh, rh);
        card.Body.Controls.Add(ModernUI.Row("کد واحد", txtCode), 0, 0);
        card.Body.Controls.Add(ModernUI.Row("عنوان واحد", txtName), 1, 0);
        var addressRow = ModernUI.Row("آدرس واحد", txtAddress);
        card.Body.Controls.Add(addressRow, 0, 1);
        card.Body.SetColumnSpan(addressRow, 2);

        // ── دکمه‌ها ──
        btnSave = ModernButton.Save("ذخیره / ویرایش", 150, 36);
        btnNew = ModernButton.Soft("جدید", ModernTheme.Success, 100, 36);
        btnDelete = ModernButton.Soft("حذف", ModernTheme.Danger, 100, 36);
        btnDelete.Enabled = false;
        btnSave.Click += async (s, e) => await SaveSafeAsync();
        btnDelete.Click += async (s, e) => await DeleteSafeAsync();
        btnNew.Click += (s, e) => BtnNew_Click(s, e);

        var actions = ModernListKit.BuildActionBar(new Control[] { btnSave, btnNew }, new Control[] { btnDelete });

        ModernListKit.ComposeRows(this, "مدیریت واحدهای سازمانی", "تعریف و ویرایش واحدها و دفترهای شرکت", new Size(950, 680),
            (card, card.Height + 12), (ModernReport.WrapGrid(dgv), 0), (actions, 64));
    }

    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;
        try
        {
            var (success, list) = await AsyncHelper.RunWithLoading(this, () => Task.Run(() => _repo.GetAll()), "در حال بارگذاری...");
            if (!success || list is null || IsDisposed) return;
            dgv.DataSource = list;
            UpdateButtonsState();
        }
        catch (Exception ex) { LogHelper.LogException(ex, "DepartmentManagementForm.LoadDataSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
        finally { try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "DepartmentManagementForm"); } }
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
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "DepartmentManagementForm"); } }
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
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "DepartmentManagementForm"); } }
    }

    private void BtnNew_Click(object? sender, EventArgs e)
    {
        _selectedId = 0;
        txtCode.Text = string.Empty;
        txtName.Text = string.Empty;
        txtAddress.Text = string.Empty;
        if (!IsDisposed) { dgv.ClearSelection(); UpdateButtonsState(); txtName.Focus(); }
    }
}
