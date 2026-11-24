using Smp.Data;
using Smp.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class RolePermissionForm : Form
{
    private readonly PermissionRepository _repo;
    private DataGridView dgv = null!;
    private ComboBox cmbRole = null!;
    private Button btnSave = null!, btnSelectAll = null!, btnDeselectAll = null!;

    private readonly Dictionary<string, string> _roles = new()
    {
        { "HR", "کارشناس منابع انسانی" },
        { "User", "کاربر عادی" },
        { "Viewer", "بازدیدکننده" }
    };

    public RolePermissionForm(string connectionString)
    {
        _repo = new PermissionRepository(connectionString);
        BuildUI();

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
    }

    private void BuildUI()
    {
        // ── جدول ──
        dgv = ModernGrid.Create();
        dgv.AutoGenerateColumns = false;
        dgv.EditMode = DataGridViewEditMode.EditOnEnter;
        dgv.ReadOnly = false;
        dgv.FillColumns();
        dgv.UseAiryLook(44);
        dgv.UseSelectionAccent(ModernTheme.Primary);

        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "MenuKey", DataPropertyName = "MenuKey", Visible = false });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "MenuTitle", DataPropertyName = "MenuTitle", HeaderText = "عنوان منو / بخش", ReadOnly = true, FillWeight = 80 });
        var chkCol = new DataGridViewCheckBoxColumn { Name = "IsAllowed", DataPropertyName = "IsAllowed", HeaderText = "دسترسی دارد؟", FillWeight = 20, MinimumWidth = 110 };
        chkCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgv.Columns.Add(chkCol);
        ModernListKit.PinHeaders(dgv);

        // ثبت فوری تیک چک‌باکس‌ها
        dgv.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (dgv.IsCurrentCellDirty && dgv.CurrentCell is DataGridViewCheckBoxCell)
                dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        // ── انتخاب نقش و انتخاب/لغو همه ──
        cmbRole = ModernUI.Combo();
        cmbRole.Items.AddRange(_roles.Values.ToArray());
        cmbRole.SelectedIndex = 0;
        cmbRole.SelectedIndexChanged += (s, e) => LoadPermissions();

        btnSelectAll = ModernButton.Soft("انتخاب همه", ModernTheme.Success, 120, 34);
        btnSelectAll.Click += (s, e) => ToggleAll(true);
        btnDeselectAll = ModernButton.Soft("لغو همه", ModernTheme.Danger, 120, 34);
        btnDeselectAll.Click += (s, e) => ToggleAll(false);

        var toolbar = ModernListKit.BuildToolbar(
            (ModernListKit.FilterLabel("سطح دسترسی (نقش):"), 150),
            (cmbRole, 260),
            (btnSelectAll, 130),
            (btnDeselectAll, 130));

        // ── ذخیره ──
        btnSave = ModernButton.Save("ذخیره تغییرات", 180, 38);
        btnSave.Click += BtnSave_Click;
        var actions = ModernListKit.BuildActionBar(new Control[] { btnSave }, new Control[0]);

        ModernListKit.ComposeRows(this, "مدیریت سطوح دسترسی (نقش‌ها)", "تعیین منوهایی که هر نقش می‌تواند ببیند", new Size(800, 700),
            (toolbar, 56), (ModernReport.WrapGrid(dgv), 0), (actions, 64));

        LoadPermissions();
    }

    private void ToggleAll(bool isAllowed)
    {
        dgv.EndEdit();
        foreach (DataGridViewRow row in dgv.Rows)
        {
            if (row.DataBoundItem is PermissionViewItem item)
            {
                item.IsAllowed = isAllowed;
            }
        }
        dgv.Refresh();
    }

    private void LoadPermissions()
    {
        string selectedRoleName = cmbRole.SelectedItem?.ToString() ?? "کاربر عادی";
        string roleKey = _roles.FirstOrDefault(x => x.Value == selectedRoleName).Key ?? "User";

        var allowedKeys = _repo.GetAllowedMenus(roleKey);

        var data = new List<PermissionViewItem>();
        foreach (var menu in MenuHelper.AllMenus)
        {
            data.Add(new PermissionViewItem
            {
                MenuKey = menu.Key,
                MenuTitle = menu.Value,
                IsAllowed = allowedKeys.Contains(menu.Key)
            });
        }

        dgv.DataSource = null;
        dgv.DataSource = data;
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        string selectedRoleName = cmbRole.SelectedItem?.ToString() ?? "کاربر عادی";
        string roleKey = _roles.FirstOrDefault(x => x.Value == selectedRoleName).Key ?? "User";

        dgv.EndEdit();
        var dict = new Dictionary<string, bool>();
        foreach (DataGridViewRow row in dgv.Rows)
        {
            if (row.DataBoundItem is PermissionViewItem item)
            {
                dict[item.MenuKey] = item.IsAllowed;
            }
        }

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await _repo.SavePermissionsAsync(roleKey, dict);
        }, "در حال ذخیره سطوح دسترسی...");

        if (success && !IsDisposed)
        {
            ToastHelper.Show("سطوح دسترسی با موفقیت ذخیره شد.", true);
        }
    }

    private class PermissionViewItem
    {
        public string MenuKey { get; set; } = "";
        public string MenuTitle { get; set; } = "";
        public bool IsAllowed { get; set; }
    }
}
