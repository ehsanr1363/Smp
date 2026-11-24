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

/// <summary>
/// تسویه تنخواه به بانک: تنخواه‌دار پولی را به حساب بانکی شرکت برمی‌گرداند.
/// تنخواه‌دار + حساب بانکی مقصد انتخاب می‌شود. سند مجزاست و در لیست‌های تنخواه ثبت نمی‌شود؛
/// فقط موجودی بانک زیاد و از مانده‌ی کل تنخواه‌دار کم می‌شود.
/// </summary>
public class TankhahSettleForm : Form
{
    private readonly TankhahRepository _tankhahRepo;
    private readonly BankAccountRepository _bankRepo;

    private ComboBox cmbOwner = null!, cmbBank = null!;
    private TextBox txtAmount = null!, txtDate = null!, txtDescription = null!;
    private Label lblListBalance = null!;
    private Button btnSave = null!, btnDelete = null!;
    private DataGridView dgv = null!;

    private decimal _ownerBalance;
    private List<TankhahCharge> _settlements = new();
    private bool _loading = true;
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public TankhahSettleForm(string connectionString)
    {
        _tankhahRepo = new TankhahRepository(connectionString);
        _bankRepo = new BankAccountRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("تسویه تنخواه (برگشت وجه به حساب بانکی)", new Size(1000, 780));
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
        Shown += async (s, e) => await LoadDataAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception ex) { LogHelper.LogException(ex, "TankhahSettleForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        cmbOwner = ModernUI.Combo();
        cmbOwner.DisplayMember = "OwnerName";
        cmbOwner.ValueMember = "Id";
        cmbOwner.SelectedIndexChanged += async (s, e) => { if (!_loading) await OnOwnerChangedAsync(); };

        cmbBank = ModernUI.Combo();
        cmbBank.DisplayMember = "DisplayTitle";
        cmbBank.ValueMember = "Id";

        lblListBalance = ModernUI.Caption("مانده تنخواه‌دار: —", 10f, true, ModernTheme.TextMuted);
        lblListBalance.Dock = DockStyle.Fill;
        lblListBalance.TextAlign = ContentAlignment.MiddleCenter;

        txtAmount = new TextBox();
        SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
        txtDate = new TextBox { TextAlign = HorizontalAlignment.Center };
        DateHelper.ApplyDateMask(txtDate);
        txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);
        txtDescription = new TextBox { PlaceholderText = "مثلاً: برگشت باقی‌مانده تنخواه..." };

        var cols = new[] { new ColumnStyle(SizeType.Percent, 50F), new ColumnStyle(SizeType.Percent, 50F) };
        var card = new ModernCard("اطلاعات تسویه (از تنخواه‌دار به حساب بانکی شرکت)", ModernTheme.Teal, cols, 50, 50, 50, 28);
        card.Body.Controls.Add(ModernUI.Row("از تنخواه‌دار", cmbOwner), 0, 0);
        card.Body.Controls.Add(ModernUI.Row("به حساب بانکی", cmbBank), 1, 0);
        card.Body.Controls.Add(ModernUI.Row("مبلغ (ریال)", txtAmount), 0, 1);
        card.Body.Controls.Add(ModernUI.Row("تاریخ", txtDate), 1, 1);
        var descRow = ModernUI.Row("توضیحات", txtDescription);
        card.Body.Controls.Add(descRow, 0, 2);
        card.Body.SetColumnSpan(descRow, 2);
        card.Body.Controls.Add(lblListBalance, 0, 3);
        card.Body.SetColumnSpan(lblListBalance, 2);

        var lblGrid = ModernUI.Caption("تسویه‌های ثبت‌شده", 11f, true, ModernTheme.TextStrong);
        lblGrid.Dock = DockStyle.Fill;

        dgv = ModernGrid.Create();
        dgv.AutoGenerateColumns = false;
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgv.MultiSelect = false;
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TankhahCharge.Date), HeaderText = "تاریخ", FillWeight = 14 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TankhahCharge.OwnerName), HeaderText = "تنخواه‌دار", FillWeight = 18 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TankhahCharge.BankTitle), HeaderText = "حساب بانکی", FillWeight = 24 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TankhahCharge.Amount), HeaderText = "مبلغ (ریال)", FillWeight = 16 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TankhahCharge.Description), HeaderText = "توضیحات", FillWeight = 30 });
        dgv.UseAiryLook(44);
        dgv.UseSelectionAccent();
        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
            else if (e.Value is decimal dec) { e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true); e.FormattingApplied = true; }
        };
        dgv.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Delete) { e.Handled = true; await DeleteSelectedAsync(); } };

        btnSave = ModernButton.Save("ثبت تسویه", 200, 40);
        btnSave.Click += async (s, e) => await SaveAsync();
        btnDelete = ModernButton.Soft("حذف تسویه انتخاب‌شده", ModernTheme.Danger, 200, 40);
        btnDelete.Click += async (s, e) => await DeleteSelectedAsync();
        var bar = ModernForm.CreateButtonBar(btnSave, btnDelete);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            Padding = new Padding(14, 12, 14, 6), BackColor = ModernTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, card.Height + 10));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        card.Dock = DockStyle.Fill;
        root.Controls.Add(card, 0, 0);
        root.Controls.Add(lblGrid, 0, 1);
        root.Controls.Add(ModernReport.WrapGrid(dgv), 0, 2);
        root.Controls.Add(bar, 0, 3);
        Controls.Add(root);
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _loading = true;
            var owners = await _tankhahRepo.GetAllTankhahsAsync();
            var banks = (await _bankRepo.GetActiveAsync()).Where(a => !a.IsCashDesk).ToList();
            if (IsDisposed) return;

            cmbOwner.DataSource = owners;
            cmbBank.DataSource = banks;
            _loading = false;

            await OnOwnerChangedAsync();
            await LoadSettlementsAsync();
        }
        catch (Exception ex)
        {
            _loading = false;
            LogHelper.LogException(ex, "TankhahSettleForm.LoadData");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات:\n" + ex.Message);
        }
    }

    private async Task OnOwnerChangedAsync()
    {
        if (IsDisposed || cmbOwner.SelectedItem is not Tankhah owner) { _ownerBalance = 0; UpdateBalanceLabel(null); return; }
        try
        {
            // مانده‌ی تنخواه‌دار (همان فرمول مدیریت تنخواه‌ها) = شارژ − هزینه − تسویه‌های برگشتی به بانک
            var lists = await _tankhahRepo.GetListsByTankhahIdAsync(owner.Id);
            var charges = await _tankhahRepo.GetChargesAsync(tankhahId: owner.Id);
            decimal settled = await _tankhahRepo.GetSettledTotalAsync(owner.OwnerName);
            _ownerBalance = charges.Sum(c => c.Amount) - lists.Sum(l => l.ChargeAmount - l.CurrentBalance) - settled;
            if (!IsDisposed) UpdateBalanceLabel(_ownerBalance);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahSettleForm.OnOwnerChanged");
        }
    }

    private void UpdateBalanceLabel(decimal? balance)
    {
        if (balance.HasValue)
        {
            lblListBalance.Text = $"مانده فعلی تنخواه‌دار: {NumberHelper.FormatThousands(balance.Value, usePersianDigits: true)} ریال";
            lblListBalance.ForeColor = balance.Value > 0 ? ModernTheme.Success : ModernTheme.TextMuted;
        }
        else
        {
            lblListBalance.Text = "مانده تنخواه‌دار: —";
            lblListBalance.ForeColor = ModernTheme.TextMuted;
        }
    }

    private async Task LoadSettlementsAsync()
    {
        try
        {
            _settlements = await _tankhahRepo.GetSettlementsAsync();
            if (!IsDisposed) dgv.DataSource = _settlements.ToList();
        }
        catch (Exception ex) { LogHelper.LogException(ex, "TankhahSettleForm.LoadSettlements"); }
    }

    private async Task SaveAsync()
    {
        if (IsDisposed) return;
        if (cmbOwner.SelectedItem is not Tankhah owner) { ModernMessageBox.ShowWarning("لطفاً تنخواه‌دار را انتخاب کنید."); return; }
        if (cmbBank.SelectedItem is not CompanyBankAccount bank) { ModernMessageBox.ShowWarning("لطفاً حساب بانکی مقصد را انتخاب کنید."); return; }

        decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
        if (amount <= 0) { ModernMessageBox.ShowWarning("مبلغ نامعتبر است."); return; }
        DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
        if (date is null) { ModernMessageBox.ShowWarning("تاریخ نامعتبر است."); return; }
        if (amount > _ownerBalance &&
            !ModernMessageBox.AskConfirm($"مبلغ تسویه از مانده‌ی فعلی تنخواه‌دار بیشتر است.\n\nمانده: {NumberHelper.FormatThousands(_ownerBalance, usePersianDigits: true)} ریال\n\nآیا ادامه می‌دهید؟", "مبلغ بیش از مانده")) return;

        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            string desc = txtDescription.Text.Trim();
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _tankhahRepo.SettleToBankAsync(owner.Id, bank.Id, amount, date.Value, desc);
            }, "در حال ثبت تسویه...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show($"تسویه به حساب «{bank.Title}» ثبت شد.", true);
                txtAmount.Clear();
                txtDescription.Clear();
                await OnOwnerChangedAsync();
                await LoadSettlementsAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahSettleForm.Save");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ثبت:\n" + ex.Message);
        }
        finally { try { _actionLock.Release(); } catch (Exception ex) { LogHelper.LogException(ex, "TankhahSettleForm"); } }
    }

    private async Task DeleteSelectedAsync()
    {
        if (dgv.CurrentRow?.DataBoundItem is not TankhahCharge item)
        {
            ModernMessageBox.ShowWarning("لطفاً یک تسویه را از جدول انتخاب کنید.");
            return;
        }
        if (!ModernMessageBox.AskConfirm(
            $"آیا از حذف این تسویه مطمئن هستید؟\n\nمبلغ: {NumberHelper.FormatThousands(item.Amount, usePersianDigits: true)} ریال\n\n" +
            "توجه: مبلغ از موجودی حساب بانکی کسر و دوباره به مانده‌ی تنخواه‌دار اضافه می‌شود.", "حذف تسویه")) return;

        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _tankhahRepo.DeleteSettlementAsync(item.Id);
            }, "در حال حذف تسویه...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("تسویه حذف و موجودی‌ها اصلاح شد.", true);
                await OnOwnerChangedAsync();
                await LoadSettlementsAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahSettleForm.Delete");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در حذف:\n" + ex.Message);
        }
        finally { try { _actionLock.Release(); } catch (Exception ex) { LogHelper.LogException(ex, "TankhahSettleForm"); } }
    }
}
