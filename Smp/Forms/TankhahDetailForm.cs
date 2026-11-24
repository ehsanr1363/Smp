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

/// <summary>جزئیات یک لیست تنخواه: ثبت، ویرایش و حذف ردیف‌های هزینه + چاپ صورت ریز مخارج</summary>
public class TankhahDetailForm : Form
{
    private readonly string _connectionString;
    private readonly TankhahRepository _repo;
    private readonly string _ownerName;
    private TankhahList _list;

    private DataGridView dgvExpenses = null!;
    private DataGridView dgvCharges = null!;
    private Label lblNoCharges = null!;
    private List<TankhahCharge> _charges = new();
    private ModernStatCard cardCharge = null!, cardSpent = null!, cardBalance = null!;
    private Button btnAdd = null!, btnEdit = null!, btnDelete = null!, btnPrint = null!, btnImport = null!, btnTemplate = null!;
    private Label lblEmpty = null!;

    private List<CashTransaction> _rows = new();
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public bool DataChanged { get; private set; }

    public TankhahDetailForm(string connectionString, TankhahList list, string ownerName = "")
    {
        _connectionString = connectionString;
        _repo = new TankhahRepository(connectionString);
        _list = list;
        _ownerName = ownerName ?? "";

        BuildUI();
        string headerTitle = string.IsNullOrWhiteSpace(_ownerName) ? $"جزئیات {_list.Title}" : $"جزئیات {_list.Title} - {_ownerName}";
        this.ApplyStandardHeader(headerTitle, new Size(1250, 720));
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this);
        Shown += async (s, e) => await LoadDataAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception ex) { LogHelper.LogException(ex, "TankhahDetailForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── کارت‌های خلاصه ──
        var strip = ModernReport.CreateStatStrip(3, 72);
        cardCharge = strip.Add("مبلغ شارژ لیست", ModernTheme.Primary);
        cardSpent = strip.Add("مجموع هزینه‌ها", ModernTheme.Danger);
        cardBalance = strip.Add("مانده لیست", ModernTheme.Success);

        // ── جدول ردیف‌ها ──
        dgvExpenses = ModernGrid.Create("Id");
        dgvExpenses.AutoGenerateColumns = false;
        dgvExpenses.FillColumns();
        dgvExpenses.AllowUserToAddRows = false;
        dgvExpenses.ReadOnly = true;
        dgvExpenses.MultiSelect = false;
        dgvExpenses.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Row", HeaderText = "ردیف", FillWeight = 6 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "تاریخ", FillWeight = 11 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "شرح هزینه", FillWeight = 29 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Scope", HeaderText = "بابت", FillWeight = 14 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Party", HeaderText = "طرف حساب", FillWeight = 16 });
        dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 14 });
        foreach (var c in new[] { "Row", "Date", "Amount" })
            dgvExpenses.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        dgvExpenses.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnEdit_Click(null, EventArgs.Empty); };
        dgvExpenses.SelectionChanged += (s, e) => UpdateButtons();
        dgvExpenses.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) { BtnDelete_Click(null, EventArgs.Empty); e.Handled = true; } };

        lblEmpty = new Label
        {
            Text = "هنوز هزینه‌ای در این لیست ثبت نشده است.\nبرای شروع روی «ثبت ردیف جدید» بزنید.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(11f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent,
            Visible = false
        };
        dgvExpenses.Controls.Add(lblEmpty);

        var gridHost = ModernReport.WrapGrid(dgvExpenses);

        // ── شارژهای همین لیست ──
        dgvCharges = TankhahChargeGrid.Create(showListColumn: false);
        var chargesHost = TankhahChargeGrid.WrapWithCaption(dgvCharges, "شارژهای این لیست", out lblNoCharges);

        // ── دکمه‌ها ──
        btnAdd = ModernButton.Success("➕ ثبت ردیف جدید", 170);
        btnAdd.Click += BtnAdd_Click;

        btnEdit = ModernButton.Soft("✏️ ویرایش ردیف", ModernTheme.Warning, 150);
        btnEdit.Enabled = false;
        btnEdit.Click += BtnEdit_Click;

        btnDelete = ModernButton.Danger("🗑 حذف ردیف", 130);
        btnDelete.Enabled = false;
        btnDelete.Click += BtnDelete_Click;

        btnPrint = ModernButton.Primary("🖨️ چاپ صورت ریز مخارج", 210);
        btnPrint.Enabled = false;
        btnPrint.Click += BtnPrint_Click;

        btnImport = ModernButton.Outline("📥 ایمپورت از اکسل", ModernTheme.Teal, 170);
        btnImport.Click += BtnImport_Click;

        btnTemplate = ModernButton.Outline("📄 نمونه فایل اکسل", ModernTheme.Neutral, 160);
        btnTemplate.Click += (s, e) => TankhahExcelImporter.GenerateTemplate();

        var btnClose = ModernButton.Cancel("بستن", 110);
        btnClose.Click += (s, e) => Close();

        var bar = ModernForm.CreateButtonBar(btnAdd, btnEdit, btnDelete, btnImport, btnTemplate, btnPrint, btnClose);

        // ── چیدمان ──
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12, 10, 12, 0), BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 66F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 34F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        strip.Dock = DockStyle.Fill;
        root.Controls.Add(strip, 0, 0);
        root.Controls.Add(gridHost, 0, 1);
        root.Controls.Add(chargesHost, 0, 2);
        root.Controls.Add(bar, 0, 3);
        Controls.Add(root);
    }

    private void UpdateButtons()
    {
        bool has = SelectedRow() is not null;
        btnEdit.Enabled = has;
        btnDelete.Enabled = has;
        btnPrint.Enabled = _rows.Count > 0;
    }

    private CashTransaction? SelectedRow()
    {
        if (dgvExpenses.CurrentRow is null) return null;
        var idObj = dgvExpenses.CurrentRow.Cells["Id"].Value;
        if (idObj is null) return null;
        int id = Convert.ToInt32(idObj);
        return _rows.FirstOrDefault(r => r.Id == id);
    }

    private async Task LoadDataAsync(int? selectId = null)
    {
        if (IsDisposed) return;
        try
        {
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                var fresh = await _repo.GetListByIdAsync(_list.Id);
                if (fresh is not null) _list = fresh;
                _rows = await _repo.GetListExpensesAsync(_list.Id);
                _charges = await _repo.GetChargesAsync(listId: _list.Id);
            }, "در حال بارگذاری ردیف‌های لیست...");
            if (IsDisposed) return;

            dgvExpenses.Rows.Clear();
            decimal spent = 0;
            int n = 0;
            foreach (var t in _rows)
            {
                spent += t.Amount;
                n++;
                string party = !string.IsNullOrWhiteSpace(t.TafsiliTitle) ? t.TafsiliTitle!
                             : !string.IsNullOrWhiteSpace(t.RecipientName) ? t.RecipientName! : "---";
                string desc = string.IsNullOrWhiteSpace(t.Description) ? (t.CostType ?? "---") : t.Description!;
                dgvExpenses.Rows.Add(
                    t.Id,
                    StringHelper.ConvertToPersianDigits(n.ToString()),
                    DateHelper.ToPersianDate(t.TransactionDate),
                    desc,
                    t.PaymentScopeDisplay.Trim(),
                    party,
                    NumberHelper.FormatThousands(t.Amount, usePersianDigits: true));
            }

            TankhahChargeGrid.Fill(dgvCharges, _charges, lblNoCharges);
            cardCharge.SetAmount(_list.ChargeAmount, "ریال");
            cardSpent.SetAmount(spent, "ریال");
            cardBalance.NegativeRed = true;
            cardBalance.SetAmount(_list.CurrentBalance, "ریال");

            lblEmpty.Visible = _rows.Count == 0;

            if (_rows.Count > 0)
            {
                int idx = selectId.HasValue ? _rows.FindIndex(r => r.Id == selectId.Value) : _rows.Count - 1;
                if (idx < 0) idx = _rows.Count - 1;
                dgvExpenses.ClearSelection();
                dgvExpenses.Rows[idx].Selected = true;
                dgvExpenses.CurrentCell = dgvExpenses.Rows[idx].Cells["Description"];
            }
            UpdateButtons();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahDetailForm.LoadDataAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری:\n" + ex.Message);
        }
    }

    private string DisplayTitle => string.IsNullOrWhiteSpace(_ownerName) ? _list.Title : $"{_ownerName} - {_list.Title}";

    private async void BtnAdd_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        using var form = new TankhahRowEditForm(_connectionString, _list.Id, DisplayTitle);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            DataChanged = true;
            await LoadDataAsync();
        }
    }

    private async void BtnEdit_Click(object? sender, EventArgs e)
    {
        var row = SelectedRow();
        if (row is null || IsDisposed) return;
        using var form = new TankhahRowEditForm(_connectionString, _list.Id, DisplayTitle, row.Id);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            DataChanged = true;
            await LoadDataAsync(row.Id);
        }
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        var row = SelectedRow();
        if (row is null || IsDisposed) return;
        if (!ModernMessageBox.AskConfirm($"ردیف «{(string.IsNullOrWhiteSpace(row.Description) ? row.CostType : row.Description)}» به مبلغ {NumberHelper.FormatThousands(row.Amount, usePersianDigits: true)} ریال حذف شود؟\nمبلغ به مانده‌ی لیست برمی‌گردد.", "حذف ردیف")) return;

        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            bool ok = await AsyncHelper.RunWithLoading(this, async () => { await _repo.DeleteListExpenseAsync(_list.Id, row.Id); }, "در حال حذف...");
            if (ok && !IsDisposed)
            {
                DataChanged = true;
                ToastHelper.Show("ردیف حذف شد.", true);
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahDetailForm.Delete");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally { try { _actionLock.Release(); } catch (Exception ex) { LogHelper.LogException(ex, "TankhahDetailForm"); } }
    }

    // ═══════════════════════════════════════════════════════════
    //  ایمپورت از اکسل
    // ═══════════════════════════════════════════════════════════
    private async void BtnImport_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;

        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل هزینه‌های تنخواه" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            List<TankhahImportRow>? parsed = null;
            bool ok = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var clientRepo = new ClientContractRepository(_connectionString);
                var contractorRepo = new ContractorContractRepository(_connectionString);
                var tafsiliRepo = new TafsiliRepository(_connectionString);

                var clientTask = Task.Run(() => clientRepo.GetAll());
                var contractorTask = Task.Run(() => contractorRepo.GetAll());
                var tafsiliTask = tafsiliRepo.GetAllAsync();
                var existingTask = _repo.GetListExpensesAsync(_list.Id);
                await Task.WhenAll(clientTask, contractorTask, tafsiliTask, existingTask);

                string path = ofd.FileName;
                string title = DisplayTitle;
                var existing = existingTask.Result;
                var clients = clientTask.Result;
                var contractors = contractorTask.Result;
                var tafsilis = tafsiliTask.Result;
                parsed = await Task.Run(() => TankhahExcelImporter.Parse(path, title, existing, clients, contractors, tafsilis));
            }, "در حال خواندن فایل اکسل...");

            if (!ok || IsDisposed) return;
            if (parsed is null || parsed.Count == 0)
            {
                ModernMessageBox.ShowWarning("ردیف داده‌ای در فایل اکسل یافت نشد.\nاز دکمه‌ی «نمونه فایل اکسل» برای دیدن قالب درست استفاده کنید.");
                return;
            }

            using var preview = new TankhahImportPreviewForm($"پیش‌نمایش ایمپورت - {DisplayTitle}", parsed, _list.CurrentBalance);
            if (preview.ShowDialog(this) != DialogResult.OK || preview.ToImport.Count == 0) return;

            int count = 0;
            bool saved = await AsyncHelper.RunWithLoading(this, async () =>
            {
                count = await _repo.AddListExpensesBatchAsync(_list.Id, preview.ToImport);
            }, "در حال ثبت ردیف‌ها...");

            if (saved && !IsDisposed)
            {
                DataChanged = true;
                ToastHelper.Show($"{StringHelper.ConvertToPersianDigits(count.ToString())} ردیف با موفقیت ایمپورت شد.", true);
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahDetailForm.Import");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ایمپورت:\n" + ex.Message);
        }
        finally { try { _actionLock.Release(); } catch (Exception ex) { LogHelper.LogException(ex, "TankhahDetailForm"); } }
    }

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        if (_rows.Count == 0 || IsDisposed) return;

        // فرم چاپ قدیمی یک «حساب» می‌گیرد؛ فقط عنوان و تاریخ آن استفاده می‌شود
        var holder = new CompanyBankAccount { Title = DisplayTitle, CreatedAt = _list.CreatedAt };
        using var form = new TankhahPrintForm(_connectionString, holder, _rows, _list.ChargeDate);
        form.ShowDialog(this);
    }
}
