using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

/// <summary>
/// انتخاب یک یا چند سند خرید (چک پرداختی / تراکنش نقدی / هزینه پروژه) برای اتصال به مال.
/// </summary>
public class AssetPurchasePickerForm : Form
{
    private readonly AssetRepository _repo;
    private readonly HashSet<(string type, int id)> _alreadyLinked;

    private ComboBox cmbType = null!;
    private ModernTextBox txtSearch = null!;
    private DataGridView dgv = null!;
    private System.Windows.Forms.Timer _searchTimer = null!;
    private int _loadVersion;

    /// <summary>اسناد انتخاب‌شده (بعد از تأیید)</summary>
    public List<PurchaseDocItem> SelectedDocs { get; } = new();

    private class TypeItem
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }

    private class DocRow
    {
        public PurchaseDocItem Item { get; set; } = null!;
        public string DateText { get; set; } = "";
        public string DocNumber { get; set; } = "";
        public string Party { get; set; } = "";
        public string AmountText { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public AssetPurchasePickerForm(string connectionString, IEnumerable<AssetPurchaseLink> alreadyLinked)
    {
        _repo = new AssetRepository(connectionString);
        _alreadyLinked = new HashSet<(string, int)>(alreadyLinked.Select(l => (l.DocType, l.DocId)));

        BuildUI();
        ShowInTaskbar = false;
        ShortcutHelper.ApplyStandardShortcuts(this);
        Load += async (s, e) => await ReloadAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _searchTimer?.Dispose(); } catch (Exception ex) { LogHelper.LogException(ex, "AssetPurchasePickerForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── فیلتر: نوع سند + جستجو ──
        cmbType = ModernUI.Combo();
        cmbType.Items.Add(new TypeItem { Key = AssetDocTypes.Check, Name = "چک پرداختی" });
        cmbType.Items.Add(new TypeItem { Key = AssetDocTypes.CashTransaction, Name = "تراکنش نقدی (پرداختی)" });
        cmbType.Items.Add(new TypeItem { Key = AssetDocTypes.ProjectExpense, Name = "هزینه پروژه" });
        cmbType.SelectedIndex = 0;
        cmbType.SelectedIndexChanged += async (s, e) => await ReloadAsync();

        txtSearch = ModernUI.Input(placeholder: "جستجو در شماره، طرف حساب، توضیحات...");
        _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await ReloadAsync(); };
        txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

        var filter = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Color.Transparent };
        filter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
        filter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        filter.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        var fType = ModernUI.Field("نوع سند", cmbType);
        var fSearch = ModernUI.Field("جستجو", txtSearch);
        fType.Dock = DockStyle.Fill; fSearch.Dock = DockStyle.Fill;
        filter.Controls.Add(fType, 0, 0);
        filter.Controls.Add(fSearch, 1, 0);

        // ── جدول ──
        dgv = ModernGrid.Create();
        dgv.AutoGenerateColumns = false;
        dgv.MultiSelect = true;
        dgv.FillColumns();
        dgv.UseAiryLook(46);
        dgv.UseSelectionAccent(ModernTheme.Teal);
        AddCol("DateText", "تاریخ", 14, true);
        AddCol("DocNumber", "شماره", 14, true);
        AddCol("Party", "طرف حساب", 26);
        AddCol("AmountText", "مبلغ (ریال)", 16, true);
        AddCol("Description", "توضیحات", 30);
        dgv.Columns["AmountText"].DefaultCellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
        dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Confirm(); };

        // ── دکمه‌ها ──
        var ok = ModernButton.Success("✔ افزودن به مال", 170, 42);
        ok.Click += (s, e) => Confirm();
        var cancel = ModernButton.Cancel();
        cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        var bar = ModernForm.CreateButtonBar(ok, cancel);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12, 12, 12, 0), BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, ModernUI.FieldHeight + 10F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(ModernReport.WrapGrid(dgv), 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);

        this.ApplyStandardHeader("انتخاب سند خرید", new Size(980, 640));
    }

    private void AddCol(string name, string header, float weight, bool center = false)
    {
        var col = new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = header, FillWeight = weight, MinimumWidth = 70 };
        col.DefaultCellStyle.Alignment = center ? DataGridViewContentAlignment.MiddleCenter : DataGridViewContentAlignment.MiddleRight;
        dgv.Columns.Add(col);
    }

    private async Task ReloadAsync()
    {
        if (IsDisposed || cmbType.SelectedItem is not TypeItem type) return;
        int version = ++_loadVersion;   // اگر در حین بارگذاری فیلتر عوض شد، نتیجه‌ی قدیمی نادیده گرفته می‌شود
        try
        {
            var docs = await _repo.SearchPurchaseDocsAsync(type.Key, txtSearch.Text);
            if (IsDisposed || version != _loadVersion) return;

            var rows = docs
                .Where(d => !_alreadyLinked.Contains((d.DocType, d.DocId)))
                .Select(d => new DocRow
                {
                    Item = d,
                    DateText = d.Date.HasValue ? DateHelper.ToPersianDate(d.Date.Value) : "",
                    DocNumber = d.DocNumber,
                    Party = d.Party,
                    AmountText = NumberHelper.FormatThousands(d.Amount, usePersianDigits: true),
                    Description = d.Description
                }).ToList();

            dgv.DataSource = null;
            dgv.DataSource = rows;
            dgv.ClearSelection();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AssetPurchasePickerForm.ReloadAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در دریافت اسناد:\n" + ex.Message);
        }
    }

    private void Confirm()
    {
        var picked = dgv.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.DataBoundItem as DocRow)
            .Where(r => r is not null)
            .Select(r => r!.Item)
            .ToList();

        if (picked.Count == 0) { ModernMessageBox.ShowWarning("حداقل یک سند را انتخاب کنید."); return; }

        SelectedDocs.Clear();
        SelectedDocs.AddRange(picked);
        DialogResult = DialogResult.OK;
        Close();
    }
}
