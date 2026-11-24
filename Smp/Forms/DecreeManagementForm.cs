using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class DecreeManagementForm : Form
{
    private readonly EmployeeRepository _empRepo;
    private DataGridView dgvEmployees = null!;
    private TextBox txtSearch = null!;
    private Button btnPrint = null!;

    public DecreeManagementForm(string connectionString)
    {
        _empRepo = new EmployeeRepository(connectionString);
        BuildUI();
        this.ApplyStandardHeader("صدور حکم حقوقی پرسنل", new Size(1000, 700));
        ShowInTaskbar = false;
        Shown += async (s, e) => await LoadDataAsync();
    }

    private void BuildUI()
    {
        // ── نوار جستجو ──
        var filter = ModernReport.CreateFilterBar();
        txtSearch = new TextBox { PlaceholderText = "جستجو نام یا کد پرسنلی..." };
        txtSearch.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await LoadDataAsync(); } };
        ModernReport.AddFilter(filter, "جستجو", txtSearch, 300);

        var btnSearch = ModernButton.Primary("🔍 جستجو", 110, 36);
        btnSearch.Margin = new Padding(8, 4, 0, 0);
        btnSearch.Click += async (s, e) => await LoadDataAsync();
        filter.Controls.Add(btnSearch);

        // ── جدول ──
        dgvEmployees = new DataGridView { AutoGenerateColumns = false };
        dgvEmployees.ApplyModernStyle();
        dgvEmployees.FillColumns();

        dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", FillWeight = 20 });
        dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FullName", HeaderText = "نام و نام خانوادگی", FillWeight = 30 });
        dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Position", HeaderText = "سمت", FillWeight = 20 });
        dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DepartmentName", HeaderText = "واحد سازمانی", FillWeight = 20 });
        dgvEmployees.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsActive", HeaderText = "فعال", FillWeight = 10 });

        // دابل کلیک روی ردیف برای چاپ
        dgvEmployees.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) PrintDecree(); };

        var gridHost = ModernReport.WrapGrid(dgvEmployees);
        gridHost.Margin = new Padding(16, 4, 16, 8);

        // ── نوار دکمه‌ها ──
        btnPrint = ModernButton.Success("🖨️ چاپ حکم حقوقی", 200, 40);
        btnPrint.Click += (s, e) => PrintDecree();
        var bar = ModernForm.CreateButtonBar(btnPrint);
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(gridHost, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
    }

    private async Task LoadDataAsync()
    {
        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            var list = await _empRepo.GetEmployeesWithDetailsAsync(txtSearch.Text.Trim(), false);
            dgvEmployees.DataSource = list;
        }, "در حال بارگذاری لیست پرسنل...");

        if (!success) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات.");
    }

    private void PrintDecree()
    {
        if (dgvEmployees.CurrentRow?.DataBoundItem is not Employee emp)
        {
            ModernMessageBox.ShowWarning("لطفاً یک کارمند را از جدول انتخاب کنید.");
            return;
        }

        // باز کردن فرم چاپ حکم
        using var form = new PersonnelDecreeForm(emp);
        form.ShowDialog(this);
    }
}
