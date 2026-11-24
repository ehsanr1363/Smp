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

public class TankhahForm : Form
{
    private readonly string _connectionString;
    private readonly TankhahRepository _tankhahRepo;

    private ComboBox cmbTankhah = null!;
    private DataGridView dgvTankhahs = null!;
    private ModernStatCard cardBalance = null!, cardSpent = null!, cardCharged = null!;
    private Button btnChargesList = null!, btnDiag = null!;
    private List<TankhahCharge> _charges = new();

    private Button btnAddTankhah = null!, btnRefresh = null!, btnEditTankhah = null!, btnDeleteTankhah = null!;
    private Button btnViewDetails = null!;
    private Label lblEmpty = null!;

    private List<Tankhah> _allOwners = new();
    private List<TankhahList> _currentLists = new();
    private TankhahList? _activeList = null;

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public TankhahForm(string connectionString)
    {
        _connectionString = connectionString;
        _tankhahRepo = new TankhahRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("مدیریت تنخواه‌ها و لیست‌های هزینه", new Size(1350, 700));
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this);
        Shown += async (s, e) => await LoadOwnersAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "TankhahForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── انتخاب شخص ──
        cmbTankhah = ModernUI.Combo();
        cmbTankhah.DisplayMember = "OwnerName";
        cmbTankhah.ValueMember = "Id";
        cmbTankhah.SelectedIndexChanged += async (s, e) => { if (!_isInitializing) await LoadListsAsync(); };

        var filterBar = ModernReport.CreateFilterBar();
        filterBar.Dock = DockStyle.Top;
        ModernReport.AddFilter(filterBar, "انتخاب شخص (تنخواه‌دار)", cmbTankhah, 380);

        // ── کارت‌های خلاصه ──
        var strip = ModernReport.CreateStatStrip(3, 72);
        cardCharged = strip.Add("مجموع شارژهای شخص", ModernTheme.Primary);
        cardBalance = strip.Add("مانده نزد تنخواه‌دار (شارژ − هزینه − تسویه)", ModernTheme.Success);
        cardSpent = strip.Add("مجموع هزینه‌های شخص", ModernTheme.Danger);
        strip.Dock = DockStyle.Fill;

        // ── جدول ──
        dgvTankhahs = ModernGrid.Create("Id");
        dgvTankhahs.AutoGenerateColumns = false;
        dgvTankhahs.FillColumns();
        dgvTankhahs.AllowUserToAddRows = false;
        dgvTankhahs.ReadOnly = true;
        dgvTankhahs.MultiSelect = false;
        dgvTankhahs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvTankhahs.SelectionChanged += DgvTankhahs_SelectionChanged;
        dgvTankhahs.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnViewDetails_Click(null, EventArgs.Empty); };

        dgvTankhahs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgvTankhahs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", DataPropertyName = "Title", HeaderText = "عنوان لیست", FillWeight = 30 });
        dgvTankhahs.Columns.Add(new DataGridViewTextBoxColumn { Name = "InitialBalance", DataPropertyName = "InitialBalance", HeaderText = "موجودی اولیه", FillWeight = 18 });
        dgvTankhahs.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentBalance", DataPropertyName = "CurrentBalance", HeaderText = "موجودی واقعی", FillWeight = 18 });
        dgvTankhahs.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalSpent", DataPropertyName = "TotalSpent", HeaderText = "مجموع هزینه‌ها", FillWeight = 18 });
        dgvTankhahs.Columns.Add(new DataGridViewTextBoxColumn { Name = "ChargeDate", DataPropertyName = "ChargeDate", HeaderText = "تاریخ شارژ / صورت وضعیت", FillWeight = 16 });
        foreach (var c in new[] { "InitialBalance", "CurrentBalance", "TotalSpent", "ChargeDate" })
            dgvTankhahs.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        dgvTankhahs.CellFormatting += DgvTankhahs_CellFormatting;

        lblEmpty = new Label
        {
            Text = "برای این شخص هنوز لیستی ثبت نشده است.\nبا «ثبت لیست جدید»، از «لیست 1» شروع می‌شود.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(11f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent,
            Visible = false
        };
        dgvTankhahs.Controls.Add(lblEmpty);

        var gridHost = ModernReport.WrapGrid(dgvTankhahs);
        gridHost.Margin = new Padding(0, 4, 0, 8);

        // ── دکمه‌ها ──
        btnAddTankhah = ModernButton.Success("➕ ثبت لیست جدید", 170);
        btnAddTankhah.Click += BtnNewTankhah_Click;

        btnViewDetails = ModernButton.Primary("📋 جزئیات و ثبت هزینه‌ها", 210);
        btnViewDetails.Enabled = false;
        btnViewDetails.Click += BtnViewDetails_Click;

        btnEditTankhah = ModernButton.Soft("✏️ ویرایش لیست", ModernTheme.Warning, 150);
        btnEditTankhah.Enabled = false;
        btnEditTankhah.Click += BtnEditTankhah_Click;

        btnDeleteTankhah = ModernButton.Danger("🗑 حذف لیست", 130);
        btnDeleteTankhah.Enabled = false;
        btnDeleteTankhah.Click += BtnDeleteTankhah_Click;

        btnRefresh = ModernButton.Outline("🔄 بروزرسانی", ModernTheme.Neutral, 130);
        btnRefresh.Click += async (s, e) => await LoadOwnersAsync();

        // لیست شارژها در فرم جداگانه نمایش داده می‌شود
        btnChargesList = ModernButton.Outline("💳 لیست شارژها", ModernTheme.Teal, 170);
        btnChargesList.Click += (s, e) =>
        {
            int? ownerId = (cmbTankhah.SelectedItem as Tankhah)?.Id;
            using var f = new TankhahChargesForm(_connectionString, ownerId);
            f.ShowDialog(this);
        };

        btnDiag = ModernButton.Outline("🩺 عیب‌یابی شارژها", ModernTheme.Neutral, 170);
        btnDiag.Click += async (s, e) =>
        {
            try
            {
                string report = "";
                await AsyncHelper.RunWithLoading(this, async () => { report = await _tankhahRepo.GetChargeDiagnosticsAsync(); }, "در حال بررسی دیتابیس...");
                using var f = new Form { Text = "عیب‌یابی شارژ تنخواه", Size = new Size(900, 620), StartPosition = FormStartPosition.CenterParent, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
                f.Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10f), Text = report.Replace("\n", "\r\n") });
                f.ShowDialog(this);
            }
            catch (Exception ex) { LogHelper.LogException(ex, "TankhahForm.Diag"); if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
        };

        var bar = ModernForm.CreateButtonBar(btnAddTankhah, btnViewDetails, btnEditTankhah, btnDeleteTankhah, btnRefresh, btnChargesList, btnDiag);

        // ── چیدمان ──
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12, 10, 12, 0), BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.Controls.Add(filterBar, 0, 0);
        root.Controls.Add(strip, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        root.Controls.Add(bar, 0, 3);
        this.Controls.Add(root);
    }

    private bool _isInitializing = true;

    private async Task LoadOwnersAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            _isInitializing = true;
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                _allOwners = await _tankhahRepo.GetAllTankhahsAsync();
                if (IsDisposed) return;

                cmbTankhah.DataSource = null;
                cmbTankhah.DataSource = _allOwners;
                if (_allOwners.Count > 0) cmbTankhah.SelectedIndex = 0;
            }, "در حال بارگذاری اطلاعات...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahForm.LoadOwnersAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
        finally
        {
            _isInitializing = false;
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "TankhahForm"); }
        }

        if (!IsDisposed && cmbTankhah.SelectedItem is not null)
        {
            await LoadListsAsync();
        }
    }

    private async Task LoadListsAsync(int? selectListId = null)
    {
        if (IsDisposed || cmbTankhah.SelectedItem is not Tankhah selectedOwner) return;

        try
        {
            _isInitializing = true;
            int? wantedId = selectListId ?? _activeList?.Id;   // لیست انتخاب‌شده بعد از بارگذاری حفظ شود
            dgvTankhahs.DataSource = null;
            _activeList = null;
            btnViewDetails.Enabled = false;

            await AsyncHelper.RunWithLoading(this, async () =>
            {
                _currentLists = await _tankhahRepo.GetListsByTankhahIdAsync(selectedOwner.Id);
                _charges = await _tankhahRepo.GetChargesAsync(tankhahId: selectedOwner.Id);
                if (IsDisposed) return;

                var displayData = _currentLists.Select(x => new
                {
                    x.Id,
                    x.Title,
                    InitialBalance = x.ChargeAmount,
                    CurrentBalance = x.CurrentBalance,
                    TotalSpent = x.ChargeAmount - x.CurrentBalance, // محاسبه مجموع هزینه‌ها
                    ChargeDate = x.ChargeDate
                }).OrderByDescending(x => TankhahRepository.ParseListNumber(x.Title) ?? int.MinValue).ThenByDescending(x => x.Id).ToList();   // بزرگ به کوچک: لیست‌های جدید بالا

                dgvTankhahs.DataSource = displayData;

                decimal totalSpent = _currentLists.Sum(x => x.ChargeAmount - x.CurrentBalance);
                // شارژها شامل همه‌ی منابع (لیست‌های جدید + انتقال‌های قدیمی بانک → تنخواه)؛ بدهی تنخواه‌دار = شارژ − هزینه
                decimal totalCharged = _charges.Sum(c => c.Amount);
                // تسویه‌های برگشتی به بانک (سندِ مجزا خارج از لیست‌ها) از بدهی تنخواه‌دار کم می‌شود
                decimal totalSettled = await _tankhahRepo.GetSettledTotalAsync(selectedOwner.OwnerName);
                decimal totalBalance = totalCharged - totalSpent - totalSettled;

                cardBalance.NegativeRed = true;
                cardBalance.SetAmount(totalBalance, "ریال");
                cardSpent.SetAmount(totalSpent, "ریال");
                cardCharged.SetAmount(totalCharged, "ریال");

            }, "در حال بارگذاری لیست‌ها...");

            _isInitializing = false;

            lblEmpty.Visible = dgvTankhahs.Rows.Count == 0;
            btnEditTankhah.Enabled = false; btnDeleteTankhah.Enabled = false;

            if (dgvTankhahs.Rows.Count > 0)
            {
                int target = 0;   // پیش‌فرض: بالاترین (جدیدترین) لیست
                if (wantedId.HasValue)
                {
                    foreach (DataGridViewRow row in dgvTankhahs.Rows)
                        if (Convert.ToInt32(row.Cells["Id"].Value) == wantedId.Value) { target = row.Index; break; }
                }
                dgvTankhahs.ClearSelection();
                dgvTankhahs.Rows[target].Selected = true;
                dgvTankhahs.CurrentCell = dgvTankhahs.Rows[target].Cells["Title"];
                DgvTankhahs_SelectionChanged(null, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahForm.LoadListsAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void DgvTankhahs_SelectionChanged(object? sender, EventArgs e)
    {
        if (_isInitializing || IsDisposed) return;

        if (dgvTankhahs.CurrentRow is null)
        {
            _activeList = null;
            btnViewDetails.Enabled = false;
            btnEditTankhah.Enabled = false; btnDeleteTankhah.Enabled = false;
            return;
        }

        int listId = Convert.ToInt32(dgvTankhahs.CurrentRow.Cells["Id"].Value);
        _activeList = _currentLists.FirstOrDefault(a => a.Id == listId);

        if (_activeList is null) return;

        btnViewDetails.Enabled = true;
        btnEditTankhah.Enabled = true; btnDeleteTankhah.Enabled = true;
    }

    private void DgvTankhahs_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        string colName = dgvTankhahs.Columns[e.ColumnIndex].Name;

        if (colName == "InitialBalance" || colName == "CurrentBalance" || colName == "TotalSpent")
        {
            if (e.Value is decimal dec)
            {
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.FormattingApplied = true;

                if (colName == "CurrentBalance" && dec < 0)
                {
                    e.CellStyle.ForeColor = ModernTheme.Danger;
                    e.CellStyle.Font = ModernTheme.GetFont(11f, FontStyle.Bold);
                }
            }
        }
        else if (colName == "ChargeDate")
        {
            if (e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
            else if (e.Value is null || e.Value == DBNull.Value) { e.Value = "---"; e.FormattingApplied = true; }
        }
    }

    /// <summary>دیالوگ مدرن ثبت/ویرایش لیست؛ در صورت انصراف null برمی‌گرداند</summary>
    private (string Title, decimal Amount)? ShowListDialog(string caption, string okText, string initialTitle, string initialAmount)
    {
        using var form = new Form();

        var txtTitle = new TextBox { Text = initialTitle };
        var txtAmount = new TextBox { Text = initialAmount };
        SmartInputHelper.EnableSmartAmountTextBox(txtAmount);

        int rh = ModernUI.RowHeight;
        var card = new ModernCard("مشخصات لیست", ModernTheme.Success, 1, rh, rh);
        card.Body.Controls.Add(ModernUI.Row("عنوان لیست", txtTitle), 0, 0);
        card.Body.Controls.Add(ModernUI.Row("مبلغ شارژ (ریال)", txtAmount), 0, 1);
        card.Dock = DockStyle.Fill;

        var btnOk = ModernButton.Success(okText, 170, 44);
        btnOk.Click += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text)) { ModernMessageBox.ShowWarning("عنوان لیست الزامی است."); return; }
            form.DialogResult = DialogResult.OK;
        };
        var btnCancel = ModernButton.Cancel("انصراف", 110, 44);
        btnCancel.Click += (s, e) => form.DialogResult = DialogResult.Cancel;
        var bar = ModernForm.CreateButtonBar(btnOk, btnCancel);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12, 10, 12, 0), BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, card.Height + 8));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.Controls.Add(card, 0, 0);
        root.Controls.Add(bar, 0, 2);
        form.Controls.Add(root);
        form.ApplyStandardHeader(caption, new Size(560, 380));
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        if (form.ShowDialog(this) != DialogResult.OK) return null;
        return (txtTitle.Text.Trim(), NumberHelper.ParseFormatted(txtAmount.Text));
    }

    /// <summary>
    /// ساخت لیست بعدی بدون هیچ دیالوگی. شماره از ۱ شروع می‌شود و کوچک‌ترین شماره‌ی آزاد شخص انتخاب‌شده
    /// استفاده می‌شود؛ پس بعد از حذف یک لیست، لیست بعدی دوباره با همان شماره ساخته می‌شود.
    /// </summary>
    private async void BtnNewTankhah_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (cmbTankhah.SelectedItem is not Tankhah selected)
        {
            ModernMessageBox.ShowWarning("ابتدا شخص (تنخواه‌دار) را انتخاب کنید.");
            return;
        }

        btnAddTankhah.Enabled = false;   // جلوگیری از دوبار کلیک و ساخت دو لیست
        try
        {
            TankhahList? created = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                created = await _tankhahRepo.AddNextListAsync(selected.Id);
            }, "در حال ساخت لیست جدید...");

            if (success && created is not null && !IsDisposed)
            {
                ToastHelper.Show($"«{created.Title}» ساخته شد.", true);
                await LoadListsAsync(created.Id);
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahForm.BtnNewTankhah_Click");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ساخت لیست:\n" + ex.Message);
        }
        finally
        {
            if (!IsDisposed) btnAddTankhah.Enabled = true;
        }
    }

    private async void BtnViewDetails_Click(object? sender, EventArgs e)
    {
        if (_activeList is null || IsDisposed) return;
        int listId = _activeList.Id;
        string owner = (cmbTankhah.SelectedItem as Tankhah)?.OwnerName ?? "";
        using (var form = new TankhahDetailForm(_connectionString, _activeList, owner))
        {
            form.ShowDialog(this);
        }
        if (!IsDisposed) await LoadListsAsync(listId);
    }

    private async void BtnEditTankhah_Click(object? sender, EventArgs e)
    {
        if (_activeList is null || IsDisposed) return;

        var input = ShowListDialog("ویرایش لیست", "ذخیره تغییرات", _activeList.Title,
            NumberHelper.FormatThousands(_activeList.ChargeAmount, usePersianDigits: true));
        if (input.HasValue)
        {
            var (listTitle, listAmount) = input.Value;

            _activeList.Title = listTitle;
            _activeList.ChargeAmount = listAmount;

            bool success = await AsyncHelper.RunWithLoading(this, async () => { await _tankhahRepo.UpdateListAsync(_activeList); }, "در حال ذخیره...");
            if (success && !IsDisposed)
            {
                ToastHelper.Show("تغییرات ذخیره شد.", true);
                await LoadListsAsync();
            }
        }
    }

    private async void BtnDeleteTankhah_Click(object? sender, EventArgs e)
    {
        if (_activeList is null || IsDisposed) return;
        if (!ModernMessageBox.AskConfirm($"آیا از حذف «{_activeList.Title}» مطمئن هستید؟", "حذف لیست")) return;

        bool success = false;
        string errMsg = "";
        try
        {
            success = await AsyncHelper.RunWithLoading(this, async () => { await _tankhahRepo.DeleteListAsync(_activeList.Id); }, "در حال حذف...");
        }
        catch (Exception ex) { errMsg = ex.Message; }

        if (success && !IsDisposed) { ToastHelper.Show("لیست حذف شد.", true); await LoadListsAsync(); }
        else if (!string.IsNullOrEmpty(errMsg)) { ModernMessageBox.ShowError(errMsg); }
    }
}
