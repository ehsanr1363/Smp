using Smp.Data;
using Smp.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class UnbilledExpensesForm : Form
{
    private readonly UnbilledExpensesRepository _repository;

    private DataGridView dgvExpenses = null!;
    private TextBox txtSearch = null!;
    private ModernStatCard lblTotalAmount = null!, lblTotalCount = null!, lblContractCount = null!;
    private static readonly Font _boldFont = ModernTheme.GetFont(11f, FontStyle.Bold);

    private List<UnbilledExpenseItem> _allItems = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public UnbilledExpensesForm(string connectionString)
    {
        _repository = new UnbilledExpensesRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("گزارش هزینه‌های صورت وضعیت نشده", new Size(1200, 700));

        ShortcutHelper.ApplyStandardShortcuts(this);

        Shown += async (s, e) => await LoadDataAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "UnbilledExpensesForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ═══════ ۱. فیلتر (هم‌سبک گزارش‌های مدرن) ═══════
        var filter = ModernReport.CreateFilterBar();

        txtSearch = new TextBox { PlaceholderText = "شماره/موضوع قرارداد یا توضیحات..." };
        txtSearch.TextChanged += (s, e) => ApplyFilter();
        ModernReport.AddFilter(filter, "جستجو", txtSearch, 380);

        var btnRefresh = ModernButton.Outline("🔄 بروزرسانی", ModernTheme.Neutral, 130, 36);
        btnRefresh.Margin = new Padding(0, 4, 18, 0);
        btnRefresh.Click += async (s, e) => await LoadDataAsync();
        filter.Controls.Add(btnRefresh);

        // ═══════ ۲. کارت‌های خلاصه (جایگزین نوار جمع کل پایین صفحه) ═══════
        var strip = new StatStrip(3, 76);
        lblTotalCount = strip.Add("تعداد اقلام", ModernTheme.Dark);
        lblContractCount = strip.Add("تعداد پروژه / قرارداد", ModernTheme.Primary);
        lblTotalAmount = strip.Add("جمع هزینه‌های صورت وضعیت نشده (ریال)", ModernTheme.Orange);
        lblTotalCount.SetCount(0);
        lblContractCount.SetCount(0);
        lblTotalAmount.SetAmount(0m);
        strip.Margin = new Padding(10, 4, 10, 4);

        // ═══════ ۳. گرید ═══════
        dgvExpenses = new DataGridView
        {
            AllowUserToAddRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        dgvExpenses.ApplyModernStyle();
        dgvExpenses.FillColumns();
        dgvExpenses.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvExpenses.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractTitle", HeaderText = "پروژه / قرارداد", FillWeight = 25,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "تاریخ", FillWeight = 12 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "نوع سند", FillWeight = 15 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "CostType", HeaderText = "ماهیت", FillWeight = 10 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "توضیحات", FillWeight = 25,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 15,
            DefaultCellStyle = new DataGridViewCellStyle { Font = _boldFont } });

        // ماهیت هزینه به‌صورت قرص رنگی (مقدارهای دیگر خاکستری)
        dgvExpenses.UseBadges("CostType", new Dictionary<string, Color>
        {
            ["خرید کالا"] = ModernTheme.Orange, ["کالا"] = ModernTheme.Orange,
            ["خرید خدمات"] = ModernTheme.Teal, ["خدمات"] = ModernTheme.Teal
        });

        var gridHost = ModernReport.WrapGrid(dgvExpenses);
        gridHost.Margin = new Padding(16, 8, 16, 12);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(strip, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        this.Controls.Add(root);
    }

    private async Task LoadDataAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                _allItems = await Task.Run(() => _repository.GetUnbilledExpenses());
                if (IsDisposed) return;
                ApplyFilter();
            }, "در حال دریافت هزینه‌های صورت وضعیت نشده...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "UnbilledExpensesForm.LoadDataAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات:\n" + ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "UnbilledExpensesForm"); }
        }
    }

    private void ApplyFilter()
    {
        if (IsDisposed) return;

        string search = txtSearch.Text.Trim();
        var filtered = _allItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                x.ContractTitle.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();

        dgvExpenses.Rows.Clear();
        foreach (var item in list)
        {
            int rowIndex = dgvExpenses.Rows.Add(
                item.ContractTitle,
                DateHelper.ToPersianDate(item.Date),
                item.Type,
                item.CostType,
                item.Description,
                NumberHelper.FormatThousands(item.Amount, usePersianDigits: true)
            );

            // رنگ‌آمیزی بر اساس نوع (اختیاری برای زیبایی)
            if (item.SourceType == "Exp")
                dgvExpenses.Rows[rowIndex].DefaultCellStyle.BackColor = ModernPaint.Tint(ModernTheme.Orange, 6); // نارنجی خیلی کم‌رنگ
        }

        decimal total = list.Sum(x => x.Amount);
        lblTotalCount.SetCount(list.Count);
        lblContractCount.SetCount(list.Select(x => x.ContractTitle).Distinct().Count());
        lblTotalAmount.SetAmount(total);
    }
}
