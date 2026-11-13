using SMP.Data;
using SMP.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
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
            this.ApplyStandardHeader("مدیریت سطوح دسترسی (نقش‌ها)", new Size(800, 700));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var topPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            cmbRole = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(12f) };
            cmbRole.Items.AddRange(_roles.Values.ToArray());
            cmbRole.SelectedIndex = 0;
            cmbRole.SelectedIndexChanged += (s, e) => LoadPermissions();

            topPanel.Controls.Add(new Label { Text = "سطح دسترسی (نقش):", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Margin = new Padding(5, 10, 5, 5) });
            topPanel.Controls.Add(cmbRole);

            btnSelectAll = UIHelper.CreateButton("✔ انتخاب همه", UIHelper.WarmSecondary, 110, 30);
            btnSelectAll.Margin = new Padding(15, 5, 5, 5);
            btnSelectAll.Click += (s, e) => ToggleAll(true);

            btnDeselectAll = UIHelper.CreateButton("✖ لغو همه", UIHelper.WarmDanger, 110, 30);
            btnDeselectAll.Margin = new Padding(5, 5, 5, 5);
            btnDeselectAll.Click += (s, e) => ToggleAll(false);

            topPanel.Controls.Add(btnSelectAll);
            topPanel.Controls.Add(btnDeselectAll);

            dgv = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, EditMode = DataGridViewEditMode.EditOnEnter };
            dgv.ApplyModernStyle();
            dgv.ReadOnly = false;

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "MenuKey", DataPropertyName = "MenuKey", Visible = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "MenuTitle", DataPropertyName = "MenuTitle", ReadOnly = true, FillWeight = 80 });

            var chkCol = new DataGridViewCheckBoxColumn { Name = "IsAllowed", DataPropertyName = "IsAllowed", FillWeight = 20 };
            chkCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.Columns.Add(chkCol);

            // 👈 FIX: ثبت فوری تیک چک‌باکس‌ها
            dgv.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgv.IsCurrentCellDirty && dgv.CurrentCell is DataGridViewCheckBoxCell)
                    dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            var btnPanel = new Panel { Dock = DockStyle.Fill };
            btnSave = UIHelper.CreateButton("💾 ذخیره تغییرات", UIHelper.ActionGreen, 200, 45);
            btnSave.Anchor = AnchorStyles.None;
            btnPanel.Controls.Add(btnSave);
            btnPanel.Resize += (s, e) => { btnSave.Left = (btnPanel.Width - btnSave.Width) / 2; btnSave.Top = (btnPanel.Height - btnSave.Height) / 2; };
            btnSave.Click += BtnSave_Click;

            mainLayout.Controls.Add(topPanel, 0, 0);
            mainLayout.Controls.Add(dgv, 0, 1);
            mainLayout.Controls.Add(btnPanel, 0, 2);
            this.Controls.Add(mainLayout);

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
}