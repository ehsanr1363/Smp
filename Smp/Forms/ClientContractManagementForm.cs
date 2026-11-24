using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Models;
using Smp.Helpers;

using Timer = System.Windows.Forms.Timer;

namespace Smp.Forms;

public class ClientContractManagementForm : Form
{
    private readonly ClientContractRepository _repo;
    private DataGridView dgv = null!;
    private ModernTextBox txtSearch = null!;
    private Button btnAdd = null!, btnEdit = null!, btnReport = null!, btnDelete = null!, btnRefresh = null!;
    private Timer _searchTimer = null!;
    private ModernStatCard cardCount = null!, cardTotal = null!;

    private const int StatHeight = 72;

    // ✅ FIX P2: جلوگیری از تداخل
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public ClientContractManagementForm(string connectionString)
    {
        _repo = new ClientContractRepository(connectionString);

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);
        ShowInTaskbar = false;

        Shown += async (s, e) => await LoadDataSafeAsync();
    }

    // ✅ FIX P2: Dispose تایمر و قفل
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _searchTimer?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientContractManagementForm"); }
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientContractManagementForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── کارت‌های خلاصه (کم‌ارتفاع و با مقدار درشت) ──
        var strip = ModernReport.CreateStatStrip(2, StatHeight);
        cardCount = strip.Add("تعداد قراردادها", ModernTheme.Primary);
        cardTotal = strip.Add("جمع مبلغ قراردادها", ModernTheme.Success);
        cardCount.ValueFontSize = 14f;
        cardTotal.ValueFontSize = 14f;

        // ── جستجو (در نوار ابزار استاندارد، سمت راست) ──
        txtSearch = ModernUI.Input(placeholder: "🔍  جستجو در شماره قرارداد، نام کارفرما یا موضوع...");
        _searchTimer = new Timer { Interval = 300 };
        _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await LoadDataSafeAsync(); };
        txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };
        var toolbar = ModernListKit.BuildToolbar((txtSearch, 460));

        // ── جدول ──
        dgv = ModernGrid.Create("Id");
        dgv.AutoGenerateColumns = false;
        dgv.FillColumns();
        dgv.ReadOnly = true;
        dgv.AllowUserToAddRows = false;
        dgv.UseAiryLook(44);
        dgv.UseSelectionAccent(ModernTheme.Primary);

        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractNumber", HeaderText = "شماره قرارداد", FillWeight = 11, MinimumWidth = 100 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractRow", HeaderText = "ردیف پیمان", FillWeight = 11, MinimumWidth = 100 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DisplayName", HeaderText = "نام کارفرما", FillWeight = 24, MinimumWidth = 160 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Subject", HeaderText = "موضوع", FillWeight = 26, MinimumWidth = 180 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 15, MinimumWidth = 130 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractDate", HeaderText = "تاریخ قرارداد", FillWeight = 12, MinimumWidth = 100 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EndDate", HeaderText = "تاریخ پایان", FillWeight = 12, MinimumWidth = 100 });

        // عنوان ستون‌ها یک‌خطی بماند (قبلاً «ردیف پیمان» دو خطی می‌شد)
        dgv.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        dgv.ShowCellToolTips = true;   // موضوع‌های بلند با نگه‌داشتن موس کامل دیده می‌شوند

        foreach (DataGridViewColumn c in dgv.Columns)
            if (c.DataPropertyName is "ContractNumber" or "ContractRow" or "Amount" or "ContractDate" or "EndDate")
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgv.UseTextAlignment("DisplayName", "Subject");

        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string dataProp = dgv.Columns[e.ColumnIndex].DataPropertyName;

            if (dataProp == "ContractDate" || dataProp == "EndDate")
            {
                if (e.Value is DateTime dt)
                {
                    e.Value = DateHelper.ToPersianDate(dt);
                    e.FormattingApplied = true;
                }
                else if (e.Value is null || e.Value == DBNull.Value)
                {
                    e.Value = "---";
                    e.CellStyle.ForeColor = ModernTheme.TextDisabled;
                    e.FormattingApplied = true;
                }
            }
            else if (dataProp == "Amount")
            {
                if (e.Value is not null && e.Value != DBNull.Value && decimal.TryParse(e.Value.ToString(), out decimal dec))
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.CellStyle.ForeColor = ModernTheme.Success;
                    e.CellStyle.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
                    e.FormattingApplied = true;
                }
            }
            else if (dataProp == "ContractNumber")
            {
                e.CellStyle.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
            }
        };

        dgv.SelectionChanged += (s, e) => UpdateButtonsState();
        dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnEdit_Click(null, EventArgs.Empty); };

        // ── دکمه‌ها: اصلی‌ها راست، حذف و بروزرسانی انتهای چپ ──
        btnAdd = ModernButton.Success("➕ افزودن قرارداد", 170, 40);
        btnAdd.Click += (s, e) => { using var form = new ClientContractEditForm(AppConfig.ConnectionString); if (form.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync(); };
        btnEdit = ModernButton.Primary("✏️ ویرایش", 120, 40);
        btnEdit.Click += BtnEdit_Click;
        btnReport = ModernButton.Soft("📊 گزارش مالی", ModernTheme.Teal, 150, 40);
        btnReport.Click += BtnReport_Click;
        btnDelete = ModernButton.Danger("🗑 حذف", 110, 40);
        btnDelete.Click += BtnDelete_Click;
        btnRefresh = ModernButton.Outline("🔄 بروزرسانی", ModernTheme.Neutral, 130, 40);
        btnRefresh.Click += async (s, e) => await LoadDataSafeAsync();

        var actions = ModernListKit.BuildActionBar(
            new Control[] { btnAdd, btnEdit, btnReport },
            new Control[] { btnDelete, btnRefresh });

        // ── اسکلت استاندارد: هدر مدرن + کارت‌ها + جستجو + جدول + دکمه‌ها ──
        ModernListKit.ComposeRows(this, "مدیریت قراردادهای کارفرما",
            "ثبت، ویرایش و پیگیری مالی قراردادهای کارفرمایان", new Size(1250, 720),
            (strip, StatHeight + 12), (toolbar, 52), (ModernReport.WrapGrid(dgv), 0), (actions, 64));
        MinimumSize = new Size(1000, 600);
        UpdateButtonsState();
    }

    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return; // ✅ FIX: جلوگیری از بارگذاری موازی هنگام تایپ سریع

        try
        {
            string search = txtSearch.Text.Trim();

            var (success, list) = await AsyncHelper.RunWithLoading(
                this,
                () => Task.Run(() => _repo.GetAll(search)),
                "در حال بارگذاری لیست...");

            if (!success || list is null || IsDisposed) return;

            dgv.DataSource = list;
            UpdateButtonsState();

            if (!IsDisposed)
            {
                cardCount.SetCount(list.Count, "قرارداد");
                cardTotal.SetAmount(list.Sum(c => c.Amount), "ریال");
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientContractManagementForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientContractManagementForm"); }
        }
    }

    private void UpdateButtonsState()
    {
        if (IsDisposed) return;
        bool hasSelection = dgv.CurrentRow is not null;
        btnEdit.Enabled = hasSelection;
        btnReport.Enabled = hasSelection;
        btnDelete.Enabled = hasSelection;
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (dgv.CurrentRow?.DataBoundItem is not ClientContract contract) return;
        using var form = new ClientContractEditForm(AppConfig.ConnectionString, contract);
        if (form.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync();
    }

    private void BtnReport_Click(object? sender, EventArgs e)
    {
        if (dgv.CurrentRow?.DataBoundItem is not ClientContract contract) return;
        using var form = new ClientContractReportForm(AppConfig.ConnectionString, contract);
        form.ShowDialog(this);
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (dgv.CurrentRow?.DataBoundItem is not ClientContract contract || IsDisposed) return;

        if (!ModernMessageBox.AskConfirm($"آیا از حذف قرارداد «{contract.ContractNumber}» مطمئن هستید؟", "حذف قرارداد"))
            return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await Task.Run(() => _repo.Delete(contract.Id));
        }, "در حال حذف...");

        if (success && !IsDisposed)
        {
            ToastHelper.Show("قرارداد با موفقیت حذف شد.", isSuccess: true);
            await LoadDataSafeAsync();
        }
    }
}
