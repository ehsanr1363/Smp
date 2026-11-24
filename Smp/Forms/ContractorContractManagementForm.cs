using System;
using System.Collections.Generic;
using System.Data;
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

public class ContractorContractManagementForm : Form
{
    private readonly string _connectionString;
    private readonly ContractorContractRepository _repo;

    private DataGridView dgv = null!;
    private ModernTextBox txtSearch = null!;
    private ModernStatCard cardCount = null!, cardTotal = null!;
    private Timer _searchTimer = null!;

    private Button btnAdd = null!;
    private Button btnEdit = null!;
    private Button btnStatements = null!;
    private Button btnDelete = null!;
    private Button btnRefresh = null!;
    private Button btnDownloadTemplate = null!;
    private Button btnImportExcel = null!;

    private const int StatHeight = 72;

    // ✅ FIX P2: جلوگیری از تداخل
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public ContractorContractManagementForm(string connectionString)
    {
        _connectionString = connectionString;
        _repo = new ContractorContractRepository(connectionString);

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
            try { _searchTimer?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorContractManagementForm"); }
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorContractManagementForm"); }
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
        txtSearch = ModernUI.Input(placeholder: "🔍  جستجو در نام، شماره قرارداد یا موضوع...");
        _searchTimer = new Timer { Interval = 300 };
        _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await LoadDataSafeAsync(); };
        txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };
        var toolbar = ModernListKit.BuildToolbar((txtSearch, 460));

        // ── جدول ──
        dgv = ModernGrid.Create();
        dgv.AutoGenerateColumns = false;
        dgv.FillColumns();
        dgv.ReadOnly = true;
        dgv.AllowUserToAddRows = false;
        dgv.UseAiryLook(44);
        dgv.UseSelectionAccent(ModernTheme.Primary);

        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "PartyType", HeaderText = "نوع", DataPropertyName = "PartyType", FillWeight = 8, MinimumWidth = 80 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Identifier", HeaderText = "شناسه (کد/شناسه ملی)", DataPropertyName = "Identifier", FillWeight = 14, MinimumWidth = 130 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "DisplayName", HeaderText = "نام / شرکت", DataPropertyName = "DisplayName", FillWeight = 18, MinimumWidth = 150 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractNumber", HeaderText = "شماره قرارداد", DataPropertyName = "ContractNumber", FillWeight = 11, MinimumWidth = 100 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Subject", HeaderText = "موضوع", DataPropertyName = "Subject", FillWeight = 24, MinimumWidth = 180 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "مبلغ (ریال)", DataPropertyName = "Amount", FillWeight = 13, MinimumWidth = 130 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractDate", HeaderText = "تاریخ قرارداد", DataPropertyName = "ContractDate", FillWeight = 12, MinimumWidth = 100 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndDate", HeaderText = "تاریخ پایان", DataPropertyName = "EndDate", FillWeight = 12, MinimumWidth = 100 });

        dgv.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;   // عنوان ستون‌ها یک‌خطی
        dgv.ShowCellToolTips = true;                                                // موضوع‌های بلند با نگه‌داشتن موس کامل دیده می‌شوند

        foreach (DataGridViewColumn c in dgv.Columns)
            if (c.Name is "PartyType" or "Identifier" or "ContractNumber" or "Amount" or "ContractDate" or "EndDate")
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgv.UseTextAlignment("DisplayName", "Subject");

        // نوع طرف قرارداد به‌صورت قرص رنگی (حقیقی آبی، حقوقی فیروزه‌ای)
        dgv.UseBadges("PartyType", new Dictionary<string, Color>
        {
            ["حقیقی"] = ModernTheme.Primary,
            ["حقوقی"] = ModernTheme.Teal
        });

        dgv.CellFormatting += Dgv_CellFormatting;
        dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) btnEdit.PerformClick(); };
        dgv.SelectionChanged += (s, e) => UpdateButtonsState();

        // ── دکمه‌ها: اصلی‌ها راست، بقیه انتهای چپ ──
        btnAdd = ModernButton.Success("➕ افزودن قرارداد", 170, 40);
        btnAdd.Click += (s, e) => { using var f = new ContractorContractEditForm(_connectionString); if (f.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync(); };
        btnEdit = ModernButton.Primary("✏️ ویرایش", 120, 40);
        btnEdit.Click += (s, e) =>
        {
            if (dgv.CurrentRow?.DataBoundItem is not ContractorContract c) return;
            using var f = new ContractorContractEditForm(_connectionString, c);
            if (f.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync();
        };
        btnStatements = ModernButton.Soft("🧾 صورت وضعیت‌ها", ModernTheme.Teal, 165, 40);
        btnStatements.Click += (s, e) =>
        {
            if (dgv.CurrentRow?.DataBoundItem is not ContractorContract c) return;
            using var form = new ContractorStatementReportForm(_connectionString, c);
            form.ShowDialog(this);
        };
        btnDelete = ModernButton.Danger("🗑 حذف", 105, 40);
        btnDelete.Click += BtnDelete_Click;
        btnRefresh = ModernButton.Outline("🔄 بروزرسانی", ModernTheme.Neutral, 130, 40);
        btnRefresh.Click += async (s, e) => await LoadDataSafeAsync();
        btnDownloadTemplate = ModernButton.Soft("📥 دانلود قالب اکسل", ModernTheme.Neutral, 170, 40);
        btnDownloadTemplate.Click += (s, e) => ExcelTemplateGenerator.GenerateContractorContractsTemplate();
        btnImportExcel = ModernButton.Soft("📤 ایمپورت از اکسل", ModernTheme.Secondary, 160, 40);
        btnImportExcel.Click += BtnImportExcel_Click;

        var actions = ModernListKit.BuildActionBar(
            new Control[] { btnAdd, btnEdit, btnStatements },
            new Control[] { btnDelete, btnRefresh, btnDownloadTemplate, btnImportExcel });

        // ── اسکلت استاندارد: هدر مدرن + کارت‌ها + جستجو + جدول + دکمه‌ها ──
        ModernListKit.ComposeRows(this, "مدیریت قراردادهای پیمانکاری",
            "ثبت، ویرایش و پیگیری قراردادها و صورت‌وضعیت‌های پیمانکاران", new Size(1350, 760),
            (strip, StatHeight + 12), (toolbar, 52), (ModernReport.WrapGrid(dgv), 0), (actions, 64));
        MinimumSize = new Size(1100, 600);
        UpdateButtonsState();
    }

    private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        string colName = dgv.Columns[e.ColumnIndex].DataPropertyName;

        if (colName == "ContractDate" || colName == "EndDate")
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
        else if (colName == "Amount" && e.Value is decimal amount)
        {
            e.Value = NumberHelper.FormatThousands(amount, usePersianDigits: true);
            e.CellStyle.ForeColor = ModernTheme.Success;
            e.CellStyle.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
            e.FormattingApplied = true;
        }
        else if (colName == "ContractNumber")
        {
            e.CellStyle.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
        }
    }

    private void UpdateButtonsState()
    {
        if (IsDisposed) return;
        bool hasSelection = dgv.CurrentRow is not null;
        btnEdit.Enabled = hasSelection;
        btnStatements.Enabled = hasSelection;
        btnDelete.Enabled = hasSelection;
    }

    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return; // ✅ FIX

        try
        {
            string search = txtSearch.Text.Trim();

            var (success, list) = await AsyncHelper.RunWithLoading(
                this,
                () => Task.Run(() => _repo.GetAll(search)),
                "در حال بارگذاری لیست قراردادها...");

            if (!success || list is null || IsDisposed) return;

            dgv.DataSource = list;
            if (!IsDisposed)
            {
                cardCount.SetCount(list.Count, "قرارداد");
                cardTotal.SetAmount(list.Sum(c => c.Amount), "ریال");
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractorContractManagementForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorContractManagementForm"); }
        }
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (dgv.CurrentRow?.DataBoundItem is not ContractorContract c || IsDisposed) return;

        if (!ModernMessageBox.AskConfirm($"آیا از حذف قرارداد «{c.ContractNumber}» مطمئن هستید؟", "حذف قرارداد"))
            return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await Task.Run(() => _repo.Delete(c.Id));
        }, "در حال حذف قرارداد...");

        if (success && !IsDisposed)
        {
            ToastHelper.Show("قرارداد با موفقیت حذف شد.", isSuccess: true);
            await LoadDataSafeAsync();
        }
    }

    private async void BtnImportExcel_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل قراردادهای پیمانکاری" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        DataTable? dt = null;
        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            dt = await ExcelImportHelper.ParseContractorContractsExcelAsync(ofd.FileName);
        }, "در حال خواندن فایل اکسل...");

        if (!success || dt is null || IsDisposed) return;

        if (dt.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("داده‌ای در فایل اکسل یافت نشد.");
            return;
        }

        ShowContractorContractsPreviewForm(dt);
    }

    private void ShowContractorContractsPreviewForm(DataTable dt)
    {
        var previewForm = new Form();
        ModernForm.Setup(previewForm, "پیش‌نمایش قراردادهای پیمانکاری", new Size(1400, 700));
        previewForm.Padding = new Padding(15);
        previewForm.ShowInTaskbar = false;

        var dgvPreview = new DataGridView { Dock = DockStyle.Fill, DataSource = dt };
        dgvPreview.ApplyModernStyle();

        dgvPreview.DataBindingComplete += (s, e) => {
            foreach (DataGridViewRow row in dgvPreview.Rows)
            {
                var statusVal = row.Cells["وضعیت"].Value?.ToString();

                if (statusVal != "معتبر")
                {
                    row.DefaultCellStyle.BackColor = ModernTheme.DangerSoft;
                    row.DefaultCellStyle.ForeColor = ModernTheme.DangerText;
                }
                else
                {
                    row.DefaultCellStyle.BackColor = ModernTheme.SuccessSoft;
                }
            }
        };

        var btnSave = ModernButton.Create("انتقال قراردادهای معتبر به دیتابیس", ModernTheme.Brand, 300, 50);
        btnSave.Dock = DockStyle.Bottom;

        btnSave.Click += async (s, ev) =>
        {
            var validRows = dt.Rows.Cast<DataRow>().Where(r => r["وضعیت"].ToString() == "معتبر").CopyToDataTable();

            if (validRows.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("هیچ ردیف معتبری برای ثبت وجود ندارد.");
                return;
            }

            if (!ModernMessageBox.AskConfirm($"آیا از ثبت {NumberHelper.FormatThousands(validRows.Rows.Count, usePersianDigits: true)} قرارداد معتبر مطمئن هستید؟", "تایید ایمپورت"))
                return;

            string msg = "";
            bool saveSuccess = await AsyncHelper.RunWithLoading(previewForm, async () =>
            {
                msg = await ExcelImportHelper.SaveContractorContractsToDbAsync(validRows);
            }, "در حال ثبت قراردادها در دیتابیس...");

            if (saveSuccess && !IsDisposed)
            {
                ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                ToastHelper.Show("ایمپورت با موفقیت انجام شد.", isSuccess: true);
                previewForm.Close();
                await LoadDataSafeAsync();
            }
        };

        previewForm.Controls.Add(dgvPreview);
        previewForm.Controls.Add(btnSave);
        previewForm.ShowDialog(this);
    }
}
