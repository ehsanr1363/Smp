using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class PartyManagementForm : Form
{
    private readonly string _connectionString;
    private readonly PartyRepository _repo;
    private DataGridView dgvParties = null!;
    private TextBox txtSearch = null!;
    private ModernStatCard cardAll = null!, cardContractors = null!, cardClients = null!;
    private System.Windows.Forms.Timer _searchTimer = null!;

    private Button btnAdd = null!, btnEdit = null!, btnDelete = null!, btnBankAcc = null!, btnRefresh = null!;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public PartyManagementForm(string connectionString)
    {
        _connectionString = connectionString;
        _repo = new PartyRepository(connectionString);
        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnRefresh);

        Shown += async (s, e) => await LoadPartiesSafeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PartyManagementForm"); }
            try { _searchTimer?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PartyManagementForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── کارت‌های خلاصه ──
        var strip = ModernReport.CreateStatStrip(3, 84);
        cardAll = strip.Add("تعداد اشخاص", ModernTheme.Primary);
        cardContractors = strip.Add("پیمانکاران", ModernTheme.Teal);
        cardClients = strip.Add("کارفرمایان", ModernTheme.Purple);
        strip.Dock = DockStyle.Fill;

        // ── جستجو (یک ردیف ثابت؛ بدون بریده‌شدن کادر) ──
        txtSearch = new TextBox { PlaceholderText = "جستجو بر اساس نام، شرکت، کد ملی یا شناسه ملی..." };
        _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await LoadPartiesSafeAsync(); };
        txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };
        var searchHost = new ModernInputHost(txtSearch) { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 6) };
        var filterBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        filterBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        filterBar.Controls.Add(searchHost, 0, 0);

        // ── جدول ──
        // نکته: AutoGenerateColumns باید false باشد، وگرنه ستون‌های اضافه (IsForeign و ...) هم نمایش داده می‌شوند
        dgvParties = ModernGrid.Create("Id");
        dgvParties.AutoGenerateColumns = false;
        dgvParties.FillColumns();
        dgvParties.ReadOnly = true;
        dgvParties.AllowUserToAddRows = false;
        dgvParties.MultiSelect = false;
        dgvParties.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvParties.UseAiryLook(52);

        dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        // نام + نوع شخص (حقیقی/حقوقی) به‌صورت آواتار و متن کوچک زیر نام
        dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "DisplayName", DataPropertyName = "DisplayName", HeaderText = "نام / شرکت", FillWeight = 38 });
        dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "PartyType", DataPropertyName = "PartyType", HeaderText = "نوع", Visible = false });
        // یک ستون برای کد ملی (حقیقی) یا شناسه ملی (حقوقی)
        dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "IdentityNo", DataPropertyName = "IdentityNo", HeaderText = "کد ملی / شناسه ملی", FillWeight = 20 });
        dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "Phone", DataPropertyName = "Phone", HeaderText = "تلفن تماس", FillWeight = 18 });
        dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "RoleText", DataPropertyName = "RoleText", HeaderText = "نقش", FillWeight = 24 });
        foreach (var c in new[] { "IdentityNo", "Phone", "RoleText" })
            dgvParties.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvParties.Columns["DisplayName"].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        dgvParties.Columns["DisplayName"].HeaderCell.Style.Padding = new Padding(0, 0, 58, 0);
        dgvParties.PinHeaderTexts();

        dgvParties.UsePersonColumn("DisplayName", "PartyType", "");
        dgvParties.UseBadges("RoleText", new Dictionary<string, Color>
        {
            ["پیمانکار"] = ModernTheme.Teal,
            ["کارفرما"] = ModernTheme.Purple,
            ["پیمانکار و کارفرما"] = ModernTheme.Primary
        });

        dgvParties.CellFormatting += DgvParties_CellFormatting;
        dgvParties.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenEditForm(); };
        dgvParties.SelectionChanged += (s, e) => UpdateButtonsState();
        var gridHost = ModernReport.WrapGrid(dgvParties);

        // ── دکمه‌ها ──
        btnAdd = ModernButton.Success("افزودن شخص", 150);
        btnAdd.Click += (s, e) => { using var f = new PartyEditForm(_connectionString); if (f.ShowDialog(this) == DialogResult.OK) _ = LoadPartiesSafeAsync(); };
        btnEdit = ModernButton.Primary("ویرایش", 110);
        btnEdit.Enabled = false;
        btnEdit.Click += (s, e) => OpenEditForm();
        btnDelete = ModernButton.Danger("حذف", 100);
        btnDelete.Enabled = false;
        btnDelete.Click += BtnDelete_Click;
        btnBankAcc = ModernButton.Soft("حساب‌های بانکی", ModernTheme.Teal, 160);
        btnBankAcc.Enabled = false;
        btnBankAcc.Click += BtnBankAcc_Click;
        btnRefresh = ModernButton.Outline("بروزرسانی", ModernTheme.Neutral, 120);
        btnRefresh.Click += async (s, e) => await LoadPartiesSafeAsync();
        var bar = ModernForm.CreateButtonBar(btnAdd, btnEdit, btnDelete, btnBankAcc, btnRefresh);

        // ── چیدمان ──
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(16, 12, 16, 0), BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.Controls.Add(strip, 0, 0);
        root.Controls.Add(filterBar, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        root.Controls.Add(bar, 0, 3);
        Controls.Add(root);

        this.ApplyStandardHeader("مدیریت طرفین قرارداد (پیمانکاران و کارفرمایان)", new Size(1250, 800), isDialog: false);
    }

    private void UpdateButtonsState()
    {
        if (IsDisposed) return;
        bool hasSelection = dgvParties.CurrentRow is not null;
        btnEdit.Enabled = hasSelection;
        btnDelete.Enabled = hasSelection;
        btnBankAcc.Enabled = hasSelection;
    }

    private async Task LoadPartiesSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            string search = txtSearch.Text.Trim();

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var dt = await _repo.SearchAsync(search);

                if (IsDisposed) return;
                dgvParties.DataSource = dt;

                cardAll.SetCount(dt.Rows.Count, "نفر");
                cardContractors.SetCount(dt.Rows.Cast<DataRow>().Count(r => IsTrue(r["IsContractor"])), "نفر");
                cardClients.SetCount(dt.Rows.Cast<DataRow>().Count(r => IsTrue(r["IsClient"])), "نفر");

            }, "در حال بارگذاری لیست...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "PartyManagementForm.LoadPartiesSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
        finally { try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PartyManagementForm"); } }
    }

    private static bool IsTrue(object? v)
    {
        if (v is null || v == DBNull.Value) return false;
        try { return Convert.ToInt32(v) == 1; }
        catch { try { return Convert.ToBoolean(v); } catch { return false; } }
    }

    private void DgvParties_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        string colName = dgvParties.Columns[e.ColumnIndex].Name;

        if (colName == "IdentityNo" || colName == "Phone")
        {
            string v = Convert.ToString(e.Value)?.Trim() ?? "";
            if (v.Length == 0) { e.Value = "—"; e.CellStyle.ForeColor = ModernTheme.TextDisabled; }
            else e.Value = StringHelper.ConvertToPersianDigits(v);
            e.FormattingApplied = true;
        }
    }

    private void OpenEditForm()
    {
        if (dgvParties.CurrentRow is null) return;
        int id = Convert.ToInt32(dgvParties.CurrentRow.Cells["Id"].Value);
        using var f = new PartyEditForm(_connectionString, id);
        if (f.ShowDialog(this) == DialogResult.OK) _ = LoadPartiesSafeAsync();
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (dgvParties.CurrentRow is null) return;
        int id = Convert.ToInt32(dgvParties.CurrentRow.Cells["Id"].Value);

        if (!ModernMessageBox.AskConfirm("آیا از حذف این شخص مطمئن هستید؟", "حذف")) return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await _repo.DeleteAsync(id);
        }, "در حال حذف...");

        if (success && !IsDisposed) { ToastHelper.Show("با موفقیت حذف شد.", true); await LoadPartiesSafeAsync(); }
    }

    private void BtnBankAcc_Click(object? sender, EventArgs e)
    {
        if (dgvParties.CurrentRow is null) return;
        int id = Convert.ToInt32(dgvParties.CurrentRow.Cells["Id"].Value);
        string name = dgvParties.CurrentRow.Cells["DisplayName"].Value?.ToString() ?? "";
        using var f = new PartyBankAccountsForm(_connectionString, id, name);
        f.ShowDialog(this);
    }
}
