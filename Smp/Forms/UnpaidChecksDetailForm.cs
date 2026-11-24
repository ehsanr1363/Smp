using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Forms;

/// <summary>
/// لیست چک‌های پاس‌نشده.
/// • دابل‌کلیک روی هر ردیف (یا دکمه‌ی «ویرایش») → فرم ویرایش چک
/// • دکمه‌ی «چاپ دستور پرداخت» → برای چک‌های انتخاب‌شده (اگر چیزی انتخاب نشده باشد، همه‌ی چک‌های لیست)
/// </summary>
public class UnpaidChecksDetailForm : Form
{
    private static readonly string[] UnpaidStatuses = { "در جریان", "برگشتی" };

    private readonly List<CashCheck> _checks;
    private readonly CashCheckRepository _checkRepo = new(AppConfig.ConnectionString);
    private readonly BankAccountRepository _bankRepo = new(AppConfig.ConnectionString);
    private readonly DataTable _table = new();
    private DataGridView _dgv = null!;
    private Button _btnEdit = null!, _btnPrint = null!, _btnPrintList = null!;
    private readonly string _title;

    /// <summary>اگر کاربر چکی را ویرایش کرده باشد true می‌شود (تا فرم والد داده‌هایش را تازه کند)</summary>
    public bool Changed { get; private set; }

    public UnpaidChecksDetailForm(List<CashCheck> checks, string? title = null)
    {
        _checks = checks.OrderBy(c => c.DueDate).ToList();
        _title = title ?? "لیست چک‌های پاس‌نشده (در جریان / برگشتی)";
        BuildUI();
        this.ApplyStandardHeader(_title, new Size(900, 600));
        Reload();
    }

    private void BuildUI()
    {
        _table.Columns.Add("Id", typeof(int));
        foreach (var h in new[] { "شماره چک", "نام گیرنده", "بانک", "تاریخ سررسید", "مبلغ", "وضعیت", "توضیحات" })
            _table.Columns.Add(h, typeof(string));

        _dgv = ModernGrid.Create();
        _dgv.MultiSelect = true;
        _dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _dgv.DataSource = _table;
        _dgv.DataBindingComplete += (s, e) => { if (_dgv.Columns["Id"] is not null) _dgv.Columns["Id"].Visible = false; };
        _dgv.CellDoubleClick += async (s, e) => { if (e.RowIndex >= 0) await EditSelectedAsync(); };
        _dgv.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; await EditSelectedAsync(); } };

        _btnEdit = ModernButton.Primary("✏️ ویرایش چک", 150, 40);
        _btnEdit.Click += async (s, e) => await EditSelectedAsync();

        _btnPrint = ModernButton.Success("🖨️ چاپ دستور پرداخت", 200, 40);
        _btnPrint.Click += async (s, e) => await PrintOrderAsync();

        _btnPrintList = ModernButton.Soft("🖨️ چاپ لیست", ModernTheme.Primary, 150, 40);
        _btnPrintList.Click += (s, e) => PrintList();

        var close = ModernButton.Cancel("بستن");
        close.Click += (s, e) => Close();

        var bar = ModernForm.CreateButtonBar(_btnPrint, _btnPrintList, _btnEdit, close);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(_dgv, 0, 0);
        root.Controls.Add(bar, 0, 1);
        Controls.Add(root);
    }

    private void Reload()
    {
        _table.Rows.Clear();
        foreach (var c in _checks)
        {
            _table.Rows.Add(c.Id, c.CheckNumber, c.RecipientName ?? "---", c.BankName ?? "---",
                DateHelper.ToPersianDate(c.DueDate), c.Amount.ToString("N0"), c.Status, c.Description ?? "---");
        }
        bool any = _checks.Count > 0;
        _btnEdit.Enabled = any;
        _btnPrint.Enabled = any;
        _btnPrintList.Enabled = any;
    }

    private int? SelectedCheckId()
    {
        if (_dgv.CurrentRow is null) return null;
        return Convert.ToInt32(_dgv.CurrentRow.Cells["Id"].Value);
    }

    // ───────────────────────── ویرایش ─────────────────────────
    private async System.Threading.Tasks.Task EditSelectedAsync()
    {
        if (IsDisposed) return;
        int? id = SelectedCheckId();
        if (id is null) { ModernMessageBox.ShowWarning("لطفاً یک چک را انتخاب کنید."); return; }

        using (var form = new CashCheckEditForm(AppConfig.ConnectionString, id.Value))
        {
            if (form.ShowDialog(this) != DialogResult.OK) return;
        }
        Changed = true;

        try
        {
            // چک ویرایش‌شده را از دیتابیس دوباره بخوان؛ اگر دیگر پاس‌نشده نیست از لیست حذف شود
            var fresh = await _checkRepo.GetByIdAsync(id.Value);
            int idx = _checks.FindIndex(c => c.Id == id.Value);
            if (idx >= 0)
            {
                bool stillUnpaid = fresh is not null && fresh.CheckType == DirectionTypes.Outgoing && UnpaidStatuses.Contains(fresh.Status);
                if (stillUnpaid) _checks[idx] = fresh!; else _checks.RemoveAt(idx);
            }
            if (IsDisposed) return;
            _checks.Sort((a, b) => a.DueDate.CompareTo(b.DueDate));
            Reload();
            if (_checks.Count == 0) Close();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "UnpaidChecksDetailForm.EditSelectedAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در تازه‌سازی لیست:\n" + ex.Message);
        }
    }

    // ───────────────────────── چاپ لیست (جدول با لوگو) ─────────────────────────
    private void PrintList()
    {
        if (IsDisposed || _checks.Count == 0) return;
        try
        {
            int count = _checks.Count;
            decimal total = _checks.Sum(c => c.Amount);
            string summary = StringHelper.ConvertToPersianDigits(
                $"تعداد چک: {count:N0}     |     جمع مبالغ: {total:N0} ریال");

            string subtitle = StringHelper.ConvertToPersianDigits(
                $"وضعیت: در جریان / برگشتی     |     بازه‌ی سررسید: {DateHelper.ToPersianDate(_checks.Min(c => c.DueDate))} تا {DateHelper.ToPersianDate(_checks.Max(c => c.DueDate))}");

            // لوگوی شرکت (همان لوگوی هدر فرم‌ها؛ کش‌شده است و نباید Dispose شود)
            var logo = ModernForm.GetCompanyLogo();
            ModernPrint.PrintGrid(_dgv, _title, subtitle, summary, logo, landscape: true);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "UnpaidChecksDetailForm.PrintList");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در چاپ لیست:\n" + ex.Message);
        }
    }

    // ───────────────────────── چاپ دستور پرداخت ─────────────────────────
    private async System.Threading.Tasks.Task PrintOrderAsync()
    {
        if (IsDisposed || _checks.Count == 0) return;
        try
        {
            var ids = _dgv.SelectedRows.Cast<DataGridViewRow>()
                          .Select(r => Convert.ToInt32(r.Cells["Id"].Value)).ToList();
            if (ids.Count == 0) ids = _checks.Select(c => c.Id).ToList();   // چیزی انتخاب نشده → همه

            // لیست داشبورد AccountId ندارد؛ چک‌های کامل را از دیتابیس بخوان
            var full = new List<CashCheck>();
            foreach (int id in ids)
            {
                var c = await _checkRepo.GetByIdAsync(id);
                if (c is not null) full.Add(c);
            }
            if (IsDisposed) return;
            if (full.Count == 0) return;

            var accountIds = full.Where(c => c.AccountId.HasValue).Select(c => c.AccountId!.Value).Distinct().ToList();
            if (accountIds.Count == 0) { ModernMessageBox.ShowWarning("چک‌های انتخاب‌شده به هیچ حساب بانکی متصل نیستند."); return; }
            if (accountIds.Count > 1)
            {
                ModernMessageBox.ShowWarning("چک‌های انتخاب‌شده از چند حساب بانکی مختلف‌اند.\nبرای چاپ دستور پرداخت، چک‌های یک حساب را انتخاب کنید.");
                return;
            }

            var accounts = await _bankRepo.GetAllAsync();
            var account = accounts.FirstOrDefault(a => a.Id == accountIds[0]);
            if (IsDisposed) return;
            if (account is null) { ModernMessageBox.ShowWarning("حساب بانکی مرتبط با این چک‌ها در سیستم وجود ندارد."); return; }
            if (account.IsCashDesk) { ModernMessageBox.ShowWarning("دستور پرداخت بانکی برای صندوق نقدی صادر نمی‌شود."); return; }
            if (!account.IsActive) { ModernMessageBox.ShowWarning($"حساب بانکی «{account.Title}» غیرفعال شده است."); return; }

            using var printForm = new PaymentOrderForm(full, account);
            printForm.ShowDialog(this);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "UnpaidChecksDetailForm.PrintOrderAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در چاپ:\n" + ex.Message);
        }
    }
}
