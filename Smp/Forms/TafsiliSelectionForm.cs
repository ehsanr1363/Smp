using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class TafsiliSelectionForm : Form
{
    private readonly TafsiliRepository _repository;
    private DataGridView dgv = null!;
    private TextBox txtSearch = null!;
    private System.Windows.Forms.Timer _searchTimer = null!;
    private Button btnSelect = null!, btnAddNew = null!;

    // ✅ متغیرهای صفحه‌بندی
    private int _currentPage = 1;
    private const int _pageSize = 50;
    private int _totalPages = 1;
    private int _totalCount = 0;
    private Button btnNextPage = null!, btnPrevPage = null!;
    private Label lblPageInfo = null!;

    // ✅ خروجی‌های فرم شامل شناسه
    public int SelectedId { get; private set; }
    public string SelectedCode { get; private set; } = "";
    public string SelectedName { get; private set; } = "";

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private readonly string? _initialSearch;

    public TafsiliSelectionForm(string connectionString, string? initialSearch = null)
    {
        _initialSearch = initialSearch;
        _repository = new TafsiliRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("انتخاب حساب تفصیلی", new Size(850, 650));
        this.ShowInTaskbar = false;

        Shown += async (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(_initialSearch))
            {
                txtSearch.Text = _initialSearch;
                _searchTimer.Stop();                 // جستجوی اولیه همین‌جا انجام می‌شود، نه دوباره با تایمر
                txtSearch.SelectionStart = txtSearch.TextLength;
            }
            await LoadDataAsync();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "TafsiliSelectionForm"); }
            try { _searchTimer?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "TafsiliSelectionForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10) };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));  // جستجو و افزودن
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // گرید
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));  // صفحه‌بندی
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));  // دکمه‌های پایین

        // --- پنل بالا ---
        var pnlTop = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5) };

        var lblSearch = ModernUI.Caption("جستجو (کد یا نام):", 11f, bold: true);
        lblSearch.AutoSize = true;
        lblSearch.Margin = new Padding(5, 10, 5, 5);
        txtSearch = new TextBox { PlaceholderText = "کد یا نام تفصیلی..." };
        var searchHost = new ModernInputHost(txtSearch) { Width = 300, Height = ModernTheme.InputHeight, Margin = new Padding(5, 4, 5, 5) };

        _searchTimer = new System.Windows.Forms.Timer { Interval = 400 };
        _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); _currentPage = 1; _ = LoadDataAsync(); };
        txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

        // قابلیت اینتر زدن در کادر جستجو
        txtSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SelectItem();
            }
        };

        btnAddNew = ModernButton.Create("➕ افزودن تفصیلی جدید", ModernTheme.Success, 180, 35);
        btnAddNew.Click += BtnAddNew_Click;

        pnlTop.Controls.Add(btnAddNew);
        pnlTop.Controls.Add(searchHost);
        pnlTop.Controls.Add(lblSearch);

        // --- گرید ---
        dgv = ModernGrid.Create("Id");
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", HeaderText = "کد تفصیلی", FillWeight = 30 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "نام تفصیلی", FillWeight = 70 });

        dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) SelectItem(); };

        dgv.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SelectItem();
            }
        };

        // --- پنل صفحه‌بندی ---
        var pnlPagination = new Panel { Dock = DockStyle.Fill, BackColor = ModernTheme.Background };
        var pageFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(15, 5, 15, 5) };

        lblPageInfo = ModernUI.Caption("صفحه 1 از 1 (کل: 0 رکورد)", 11f, bold: true);
        lblPageInfo.AutoSize = true;
        lblPageInfo.Margin = new Padding(10, 6, 10, 0);

        btnPrevPage = ModernButton.Create("◀ صفحه قبلی", ModernTheme.Primary, 100, 32);
        btnPrevPage.Click += async (s, e) => { if (_currentPage > 1) { _currentPage--; await LoadDataAsync(); } };

        btnNextPage = ModernButton.Create("صفحه بعدی ▶", ModernTheme.Primary, 100, 32);
        btnNextPage.Click += async (s, e) => { if (_currentPage < _totalPages) { _currentPage++; await LoadDataAsync(); } };

        pageFlow.Controls.Add(btnPrevPage);
        pageFlow.Controls.Add(btnNextPage);
        pageFlow.Controls.Add(lblPageInfo);
        pnlPagination.Controls.Add(pageFlow);

        // --- دکمه‌های پایین ---
        var pnlBottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(5, 10, 5, 5) };

        btnSelect = ModernButton.Create("✔ انتخاب", ModernTheme.Success, 120, 40);
        btnSelect.Click += (s, e) => SelectItem();

        var btnCancel = ModernButton.Cancel("انصراف", 100, 40);
        btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

        pnlBottom.Controls.Add(btnSelect);
        pnlBottom.Controls.Add(btnCancel);

        mainLayout.Controls.Add(pnlTop, 0, 0);
        mainLayout.Controls.Add(dgv, 0, 1);
        mainLayout.Controls.Add(pnlPagination, 0, 2);
        mainLayout.Controls.Add(pnlBottom, 0, 3);
        this.Controls.Add(mainLayout);
    }

    private async Task LoadDataAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            string search = txtSearch.Text.Trim();

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var (list, totalCount) = await _repository.GetPagedAsync(_currentPage, _pageSize, search);
                _totalCount = totalCount;
                _totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);
                if (_totalPages == 0) _totalPages = 1;

                if (IsDisposed) return;

                dgv.Rows.Clear();
                foreach (var item in list)
                {
                    dgv.Rows.Add(item.Id, item.Code, item.Name);
                }

                lblPageInfo.Text = $"صفحه {NumberHelper.FormatThousands(_currentPage, usePersianDigits: true)} از {NumberHelper.FormatThousands(_totalPages, usePersianDigits: true)}  (کل: {NumberHelper.FormatThousands(_totalCount, usePersianDigits: true)} رکورد)";
                btnPrevPage.Enabled = _currentPage > 1;
                btnNextPage.Enabled = _currentPage < _totalPages;

                // انتخاب خودکار اولین ردیف
                if (dgv.Rows.Count > 0)
                {
                    dgv.ClearSelection();
                    dgv.Rows[0].Selected = true;
                    dgv.CurrentCell = dgv.Rows[0].Cells["Name"];
                }

            }, "در حال بارگذاری لیست تفصیلی‌ها...");

            if (!success && !IsDisposed) ModernMessageBox.ShowError("خطا در دریافت لیست تفصیلی‌ها.");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TafsiliSelectionForm.LoadDataAsync");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "TafsiliSelectionForm"); }
        }
    }

    private void SelectItem()
    {
        if (dgv.CurrentRow is null)
        {
            ModernMessageBox.ShowWarning("لطفاً یک حساب تفصیلی را انتخاب کنید.");
            return;
        }

        SelectedId = Convert.ToInt32(dgv.CurrentRow.Cells["Id"].Value);
        SelectedCode = dgv.CurrentRow.Cells["Code"].Value?.ToString() ?? "";
        SelectedName = dgv.CurrentRow.Cells["Name"].Value?.ToString() ?? "";

        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    private async void BtnAddNew_Click(object? sender, EventArgs e)
    {
        using (var form = new TafsiliManagementForm(AppConfig.ConnectionString))
        {
            form.ShowDialog(this);
            if (!IsDisposed)
            {
                _currentPage = 1; // برگشت به صفحه اول برای دیدن رکورد جدید
                await LoadDataAsync();
            }
        }
    }
}
