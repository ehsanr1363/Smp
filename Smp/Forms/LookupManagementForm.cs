using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace SMP.Forms
{
    public class LookupManagementForm : Form
    {
        private readonly string _connectionString;
        private readonly LookupRepository _repository;

        private readonly Dictionary<string, string> _categories = new()
        {
            { "TankhahOwner", "تنخواه‌دارها (صاحبان تنخواه)" },
            { "AdministrativeCostType", "نوع هزینه اداری" },
            { "CostType", "نوع هزینه (خرید/خدمات)" },
            { "TransactionCategory", "دسته‌بندی تراکنش‌ها" },
            { "Gender", "جنسیت" },
            { "Education", "تحصیلات" },
            { "PersonnelType", "نوع پرسنل" },
            { "WorkLocation", "محل خدمت" },
            { "MilitaryStatus", "وضعیت نظام وظیفه" },
            { "MaritalStatus", "وضعیت تاهل" },
            { "DailyWageCategory", "دسته‌بندی کارگر روزمزد" }
        };

        private TabControl tabControl = null!;

        private readonly Dictionary<string, LookupTabData> _tabData = new(StringComparer.Ordinal);

        private sealed class LookupTabData
        {
            public string CategoryKey { get; init; } = "";
            public DataGridView Grid { get; init; } = null!;
            public TextBox TxtValue { get; init; } = null!;
            public Button BtnAdd { get; init; } = null!;
            public Button BtnDelete { get; init; } = null!;
            public bool Loaded { get; set; } = false;
            public bool Loading { get; set; } = false;
        }

        public LookupManagementForm(string connectionString)
        {
            _connectionString = connectionString;
            _repository = new LookupRepository(connectionString);

            BuildModernUI();
            ShortcutHelper.ApplyStandardShortcuts(this);
            ShowInTaskbar = false;
        }

        private void BuildModernUI()
        {
            UIHelper.SetupForm(this, "مدیریت لیست‌های کشویی (Lookups)", new Size(750, 600));

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(160, 40)
            };

            foreach (var category in _categories)
            {
                string dbKey = category.Key;
                string faName = category.Value;

                var tabPage = new TabPage(faName) { BackColor = UIHelper.WarmBackground, Tag = dbKey };
                tabPage.Controls.Add(CreateLookupTab(dbKey, faName));
                tabControl.TabPages.Add(tabPage);
            }

            tabControl.Selected += async (s, e) =>
            {
                string? key = tabControl.SelectedTab?.Tag as string;
                if (key != null) await LoadTabAsync(key);
            };

            this.Controls.Add(tabControl);

            Shown += async (s, e) =>
            {
                if (tabControl.TabPages.Count > 0)
                {
                    string? firstKey = tabControl.TabPages[0].Tag as string;
                    if (firstKey != null) await LoadTabAsync(firstKey);
                }
            };
        }

        private Control CreateLookupTab(string categoryKey, string categoryName)
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            var inputPanel = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(10) };

            var inputLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));

            var lblValue = new Label { Text = "مقدار جدید:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            var txtValue = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(12f), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(5, 20, 5, 20) };

            var btnAdd = UIHelper.CreateButton("➕ افزودن", UIHelper.ActionGreen, 110, 35);
            btnAdd.Dock = DockStyle.Fill;
            btnAdd.Margin = new Padding(5, 18, 5, 18);

            inputLayout.Controls.Add(lblValue, 0, 0);
            inputLayout.Controls.Add(txtValue, 1, 0);
            inputLayout.Controls.Add(btnAdd, 2, 0);

            inputPanel.Controls.Add(inputLayout);

            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                MultiSelect = false,
                RightToLeft = RightToLeft.Yes,
                BackgroundColor = Color.White
            };
            dgv.ApplyModernStyle();

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Value",
                DataPropertyName = "Value",
                HeaderText = TranslationHelper.GetColumnName("Value"),
                FillWeight = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            var btnDelete = UIHelper.CreateButton("🗑️ حذف انتخاب‌شده", UIHelper.ActionRed, 150, 35);
            btnDelete.Enabled = false;

            var bottomPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5) };
            bottomPanel.Controls.Add(btnDelete);

            _tabData[categoryKey] = new LookupTabData
            {
                CategoryKey = categoryKey,
                Grid = dgv,
                TxtValue = txtValue,
                BtnAdd = btnAdd,
                BtnDelete = btnDelete
            };

            btnAdd.Click += async (s, e) => await AddLookupAsync(categoryKey, categoryName);
            btnDelete.Click += async (s, e) => await DeleteLookupAsync(categoryKey);
            dgv.SelectionChanged += (s, e) => btnDelete.Enabled = dgv.CurrentRow != null;
            dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgv.Rows[e.RowIndex].DataBoundItem is Models.LookupItem item)
                {
                    txtValue.Text = item.Value;
                    txtValue.Focus();
                }
            };

            layout.Controls.Add(inputPanel, 0, 0);
            layout.Controls.Add(dgv, 0, 1);
            layout.Controls.Add(bottomPanel, 0, 2);

            return layout;
        }

        private async Task LoadTabAsync(string categoryKey, bool forceReload = false)
        {
            if (!_tabData.TryGetValue(categoryKey, out var tabData)) return;

            if (!forceReload && (tabData.Loaded || tabData.Loading)) return;
            if (tabData.Loading) return;

            tabData.Loading = true;
            try
            {
                var list = await Task.Run(() => _repository.GetByCategory(categoryKey));

                // فیلتر کردن دسته انتقال از لیست نمایش در مدیریت (اگر به هر دلیلی در دیتابیس مانده باشد)
                if (categoryKey == "TransactionCategory")
                {
                    list = list.Where(x => x.Value.Trim() != "انتقال بین حساب‌ها").ToList();
                }

                if (IsDisposed || tabData.Grid.IsDisposed) return;

                tabData.Grid.DataSource = list;
                tabData.Loaded = true;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, $"LookupManagementForm.LoadTab({categoryKey})");
                if (!IsDisposed)
                    ModernMessageBox.ShowError($"خطا در بارگذاری «{categoryKey}»:\n{ex.Message}");
            }
            finally
            {
                tabData.Loading = false;
            }
        }

        private async Task AddLookupAsync(string categoryKey, string categoryName)
        {
            if (!_tabData.TryGetValue(categoryKey, out var tabData)) return;

            string val = tabData.TxtValue.Text.Trim();
            if (string.IsNullOrWhiteSpace(val))
            {
                ModernMessageBox.ShowWarning("لطفاً مقداری برای افزودن وارد کنید.");
                return;
            }

            if (val == "انتقال بین حساب‌ها")
            {
                ModernMessageBox.ShowWarning("امکان ثبت دسته «انتقال بین حساب‌ها» وجود ندارد. برای انتقال وجه از منوی اختصاصی استفاده کنید.");
                return;
            }

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                string checkQuery = "SELECT COUNT(1) FROM LookupItems WHERE Category = @cat AND Value = @val";
                bool exists = await Task.Run(() =>
                    Convert.ToInt32(DbHelper.ExecuteScalar(
                        _connectionString, checkQuery,
                        new SqlParameter("@cat", categoryKey),
                        new SqlParameter("@val", val))) > 0);

                if (exists)
                    throw new InvalidOperationException("این مقدار قبلاً در این دسته‌بندی ثبت شده است.");

                await Task.Run(() => _repository.Add(new Models.LookupItem { Category = categoryKey, Value = val }));
            }, "در حال ذخیره...");

            if (success)
            {
                ToastHelper.Show("مقدار با موفقیت اضافه شد.", isSuccess: true);
                tabData.TxtValue.Clear();
                await LoadTabAsync(categoryKey, forceReload: true);
            }
        }

        private async Task DeleteLookupAsync(string categoryKey)
        {
            if (!_tabData.TryGetValue(categoryKey, out var tabData)) return;
            if (tabData.Grid.CurrentRow?.DataBoundItem is not Models.LookupItem item) return;

            if (!ModernMessageBox.AskConfirm($"آیا از حذف «{item.Value}» مطمئن هستید؟", "تأیید حذف")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() => _repository.Delete(item.Id));
            }, "در حال حذف...");

            if (success)
            {
                ToastHelper.Show("مقدار حذف شد.", isSuccess: true);
                await LoadTabAsync(categoryKey, forceReload: true);
            }
        }
    }
}