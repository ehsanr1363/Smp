using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Smp.Forms;

public class LookupManagementForm : Form
{
    private readonly string _connectionString;
    private readonly LookupRepository _repository;

    private readonly Dictionary<string, string> _categories = new()
    {
        { "TankhahOwner", "تنخواه‌دارها (صاحبان تنخواه)" },
        { "AdministrativeCostType", "نوع هزینه اداری" },
        { "OtherReceiptType", "نوع دریافت متفرقه (غیر از کارفرما)" },
        { "CostType", "نوع هزینه (خرید/خدمات)" },
        { "TransactionCategory", "دسته‌بندی تراکنش‌ها" },
        { "Gender", "جنسیت" },
        { "Education", "تحصیلات" },
        { "PersonnelType", "نوع پرسنل" },
        { "WorkLocation", "محل خدمت" },
        { "MilitaryStatus", "وضعیت نظام وظیفه" },
        { "MaritalStatus", "وضعیت تاهل" },
        { "DailyWageCategory", "دسته‌بندی کارگر روزمزد" },
        { "AssetCategory", "دسته‌بندی اموال" }
    };

    private ModernListBox lstCategories = null!;
    private readonly List<string> _categoryKeys = new();
    private readonly Dictionary<string, Control> _pages = new(StringComparer.Ordinal);

    private readonly Dictionary<string, LookupTabData> _tabData = new(StringComparer.Ordinal);

    private sealed class LookupTabData
    {
        public string CategoryKey { get; init; } = "";
        public DataGridView Grid { get; init; } = null!;
        public ModernTextBox TxtValue { get; init; } = null!;
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
    }

    private void BuildModernUI()
    {
        // ── فهرست دسته‌ها (راست) ──
        lstCategories = new ModernListBox { ItemHeight = 40, Dock = DockStyle.Fill };
        foreach (var category in _categories)
        {
            _categoryKeys.Add(category.Key);
            lstCategories.Items.Add(category.Value);
        }

        // ── محتوای دسته‌ی انتخاب‌شده (چپ): همه‌ی صفحه‌ها ساخته می‌شوند و فقط یکی دیده می‌شود ──
        var pageHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        foreach (var category in _categories)
        {
            var page = CreateLookupTab(category.Key, category.Value);
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            _pages[category.Key] = page;
            pageHost.Controls.Add(page);
        }

        var body = new BufferedTable
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270F));   // ستون ۰ = راست
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        lstCategories.Margin = new Padding(0, 0, 12, 0);
        body.Controls.Add(lstCategories, 0, 0);
        body.Controls.Add(pageHost, 1, 0);

        lstCategories.SelectedIndexChanged += async (s, e) =>
        {
            int idx = lstCategories.SelectedIndex;
            if (idx < 0 || idx >= _categoryKeys.Count) return;
            string key = _categoryKeys[idx];
            foreach (var kv in _pages) kv.Value.Visible = kv.Key == key;
            await LoadTabAsync(key);
        };

        ModernListKit.ComposeRows(this, "مدیریت لیست‌های کشویی (Lookups)", "مقدارهایی که در لیست‌های انتخاب فرم‌ها نمایش داده می‌شوند", new Size(1000, 640),
            (body, 0));

        Shown += (s, e) => { if (lstCategories.Items.Count > 0) lstCategories.SelectedIndex = 0; };
    }

    private Control CreateLookupTab(string categoryKey, string categoryName)
    {
        // ── ورود مقدار جدید ──
        var txtValue = ModernUI.Input(placeholder: "مقدار جدید را وارد کنید");
        var btnAdd = ModernButton.Success("افزودن", 110, 36);
        var inputBar = ModernListKit.BuildToolbar(
            (ModernListKit.FilterLabel("مقدار جدید:"), 90),
            (txtValue, -1),
            (btnAdd, 120));

        // ── جدول ──
        var dgv = ModernGrid.Create();
        dgv.AutoGenerateColumns = false;
        dgv.FillColumns();
        dgv.UseAiryLook(44);
        dgv.UseSelectionAccent(ModernTheme.Primary);
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        // ستون شماره‌ی ردیف (سمت راست) + ستون مقدار (راست‌چین؛ در جدول RTL مقدار MiddleLeft یعنی چسبیده به راست)
        dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "RowNo",
            HeaderText = "ردیف",
            FillWeight = 8,
            MinimumWidth = 56,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                ForeColor = ModernTheme.TextMuted,
                Font = ModernTheme.GetFont(10f)
            }
        });
        dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Value",
            DataPropertyName = "Value",
            HeaderText = categoryName,
            FillWeight = 92,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 8, 0),
                Font = ModernTheme.GetFont(11f)
            }
        });
        dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        dgv.Columns["RowNo"]!.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgv.DataBindingComplete += (s, e) =>
        {
            for (int i = 0; i < dgv.Rows.Count; i++)
                dgv.Rows[i].Cells["RowNo"].Value = StringHelper.ConvertToPersianDigits((i + 1).ToString());
        };
        ModernListKit.PinHeaders(dgv);

        // ── حذف ──
        var btnDelete = ModernButton.Soft("حذف انتخاب‌شده", ModernTheme.Danger, 160, 36);
        btnDelete.Enabled = false;
        var bottomBar = ModernListKit.BuildActionBar(new Control[] { btnDelete }, new Control[0]);

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
        dgv.SelectionChanged += (s, e) => btnDelete.Enabled = dgv.CurrentRow is not null;
        dgv.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && dgv.Rows[e.RowIndex].DataBoundItem is Models.LookupItem item)
            {
                txtValue.Text = item.Value;
                txtValue.Focus();
            }
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = Color.Transparent };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        inputBar.Dock = DockStyle.Fill; bottomBar.Dock = DockStyle.Fill;
        layout.Controls.Add(inputBar, 0, 0);
        layout.Controls.Add(ModernReport.WrapGrid(dgv), 0, 1);
        layout.Controls.Add(bottomBar, 0, 2);
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
            tabData.TxtValue.Text = string.Empty;
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
