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

/// <summary>فرم جداگانه‌ی «لیست شارژهای تنخواه»: شارژهای یک تنخواه‌دار یا همه‌ی شارژهای سیستم</summary>
public class TankhahChargesForm : Form
{
    private readonly TankhahRepository _repo;
    private readonly int? _initialOwnerId;

    private ComboBox cmbOwner = null!;
    private DataGridView dgvCharges = null!;
    private Label lblNoCharges = null!;
    private ModernStatCard cardTotal = null!, cardCount = null!;
    private bool _isInitializing = true;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public TankhahChargesForm(string connectionString, int? initialOwnerId = null)
    {
        _repo = new TankhahRepository(connectionString);
        _initialOwnerId = initialOwnerId;

        BuildUI();
        this.ApplyStandardHeader("لیست شارژهای تنخواه", new Size(1200, 700));
        ShowInTaskbar = false;
        ShortcutHelper.ApplyStandardShortcuts(this);
        Shown += async (s, e) => await LoadOwnersAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _loadLock?.Dispose(); } catch (Exception ex) { LogHelper.LogException(ex, "TankhahChargesForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        cmbOwner = ModernUI.Combo();
        cmbOwner.DisplayMember = "OwnerName";
        cmbOwner.ValueMember = "Id";
        cmbOwner.SelectedIndexChanged += async (s, e) => { if (!_isInitializing) await LoadChargesAsync(); };

        var filterBar = ModernReport.CreateFilterBar();
        filterBar.Dock = DockStyle.Top;
        ModernReport.AddFilter(filterBar, "تنخواه‌دار", cmbOwner, 380);

        var strip = ModernReport.CreateStatStrip(2, 72);
        cardTotal = strip.Add("جمع شارژها", ModernTheme.Primary);
        cardCount = strip.Add("تعداد شارژها", ModernTheme.Teal);
        strip.Dock = DockStyle.Fill;

        dgvCharges = TankhahChargeGrid.Create(showListColumn: true);
        var chargesHost = TankhahChargeGrid.WrapWithCaption(dgvCharges, "شارژهای ثبت‌شده", out lblNoCharges);

        var btnRefresh = ModernButton.Outline("🔄 بروزرسانی", ModernTheme.Neutral, 130);
        btnRefresh.Click += async (s, e) => await LoadChargesAsync();
        var btnClose = ModernButton.Cancel("بستن", 110, 44);
        btnClose.Click += (s, e) => Close();
        var bar = ModernForm.CreateButtonBar(btnRefresh, btnClose);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12, 10, 12, 0), BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.Controls.Add(filterBar, 0, 0);
        root.Controls.Add(strip, 0, 1);
        root.Controls.Add(chargesHost, 0, 2);
        root.Controls.Add(bar, 0, 3);
        Controls.Add(root);
    }

    private async Task LoadOwnersAsync()
    {
        if (IsDisposed) return;
        try
        {
            _isInitializing = true;
            var owners = new List<Tankhah> { new Tankhah { Id = 0, OwnerName = "همه‌ی شارژهای سیستم" } };
            await AsyncHelper.RunWithLoading(this, async () => { owners.AddRange(await _repo.GetAllTankhahsAsync()); }, "در حال بارگذاری...");
            if (IsDisposed) return;

            cmbOwner.DataSource = null;
            cmbOwner.DataSource = owners;
            int idx = _initialOwnerId.HasValue ? owners.FindIndex(o => o.Id == _initialOwnerId.Value) : 0;
            cmbOwner.SelectedIndex = idx >= 0 ? idx : 0;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahChargesForm.LoadOwnersAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
        finally { _isInitializing = false; }

        if (!IsDisposed) await LoadChargesAsync();
    }

    private async Task LoadChargesAsync()
    {
        if (IsDisposed || cmbOwner.SelectedItem is not Tankhah owner) return;
        if (!await _loadLock.WaitAsync(0)) return;
        try
        {
            List<TankhahCharge> charges = new();
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                charges = owner.Id == 0
                    ? await _repo.GetAllChargesEverywhereAsync()
                    : await _repo.GetChargesAsync(tankhahId: owner.Id);
            }, "در حال بارگذاری شارژها...");
            if (IsDisposed) return;

            decimal total = TankhahChargeGrid.Fill(dgvCharges, charges, lblNoCharges);
            cardTotal.SetAmount(total, "ریال");
            cardCount.SetAmount(charges.Count, "مورد");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahChargesForm.LoadChargesAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
        finally { try { _loadLock.Release(); } catch (Exception ex) { LogHelper.LogException(ex, "TankhahChargesForm"); } }
    }
}
