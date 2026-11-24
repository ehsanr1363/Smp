using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Forms;

/// <summary>پیش‌نمایش ردیف‌های اکسل تنخواه قبل از ثبت؛ فقط ردیف‌های معتبر (و در صورت انتخاب، تکراری‌ها) ثبت می‌شوند</summary>
public class TankhahImportPreviewForm : Form
{
    private readonly List<TankhahImportRow> _rows;
    private readonly decimal _currentBalance;

    private DataGridView dgv = null!;
    private Label lblSummary = null!;
    private CheckBox chkDuplicates = null!;
    private Button btnSave = null!;

    /// <summary>ردیف‌هایی که باید در دیتابیس ثبت شوند (پس از تأیید کاربر)</summary>
    public List<CashTransaction> ToImport { get; private set; } = new();

    public TankhahImportPreviewForm(string title, List<TankhahImportRow> rows, decimal currentBalance)
    {
        _rows = rows;
        _currentBalance = currentBalance;

        ModernForm.Setup(this, title, new Size(1350, 720), isDialog: false);
        ShowInTaskbar = false;
        BuildUI();
        FillGrid();
        RefreshSummary();
    }

    private void BuildUI()
    {
        lblSummary = new Label
        {
            Dock = DockStyle.Top,
            Height = 54,
            Padding = new Padding(14, 8, 14, 0),
            Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
            ForeColor = ModernTheme.Text,
            TextAlign = ContentAlignment.MiddleRight
        };

        dgv = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true, AllowUserToAddRows = false };
        dgv.ApplyModernStyle();
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.MultiSelect = false;
        dgv.Columns.Add(Col("XlRow", "ردیف اکسل", 7));
        dgv.Columns.Add(Col("Date", "تاریخ", 9));
        dgv.Columns.Add(Col("Desc", "شرح", 22));
        dgv.Columns.Add(Col("Amount", "مبلغ (ریال)", 11));
        dgv.Columns.Add(Col("Type", "نوع پرداخت", 12));
        dgv.Columns.Add(Col("Cost", "ماهیت هزینه", 10));
        dgv.Columns.Add(Col("Party", "طرف حساب", 11));
        dgv.Columns.Add(Col("Contract", "قرارداد", 9));
        dgv.Columns.Add(Col("Status", "وضعیت", 7));
        dgv.Columns.Add(Col("Message", "توضیح", 22));
        foreach (var c in new[] { "XlRow", "Date", "Amount", "Status" })
            dgv.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        chkDuplicates = new CheckBox
        {
            Text = "ردیف‌های تکراری هم ثبت شوند",
            AutoSize = true,
            Font = ModernTheme.GetFont(10f),
            Margin = new Padding(12, 12, 12, 0)
        };
        chkDuplicates.CheckedChanged += (s, e) => RefreshSummary();

        btnSave = ModernButton.Save("ثبت ردیف‌ها در لیست", 240, 40);
        btnSave.Click += BtnSave_Click;
        var btnCancel = ModernButton.Cancel("انصراف", 110, 40);
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var bar = ModernForm.CreateButtonBar(btnSave, btnCancel, chkDuplicates);
        var barHost = new Panel { Dock = DockStyle.Bottom, Height = 66, BackColor = ModernTheme.Background };
        barHost.Controls.Add(bar);

        // ترتیب افزودن مهم است: پایین و بالا اول چیده می‌شوند، جدول بقیه‌ی فضا را می‌گیرد
        Controls.Add(dgv);
        Controls.Add(lblSummary);
        Controls.Add(barHost);
    }

    private static DataGridViewTextBoxColumn Col(string name, string header, float weight)
        => new() { Name = name, HeaderText = header, FillWeight = weight };

    private void FillGrid()
    {
        dgv.Rows.Clear();
        foreach (var r in _rows)
        {
            int i = dgv.Rows.Add(
                StringHelper.ConvertToPersianDigits(r.ExcelRow.ToString()),
                r.DateText,
                r.Description,
                r.Amount > 0 ? NumberHelper.FormatThousands(r.Amount, usePersianDigits: true) : "",
                r.PaymentTypeText,
                r.CostTypeText,
                r.PartyText,
                r.ContractText,
                r.StatusText,
                r.Message);

            var style = dgv.Rows[i].DefaultCellStyle;
            switch (r.Status)
            {
                case TankhahImportStatus.Valid:
                    style.BackColor = ModernTheme.SuccessSoft;
                    break;
                case TankhahImportStatus.Duplicate:
                    style.BackColor = ModernTheme.WarningSoft;
                    style.ForeColor = ModernTheme.WarningText;
                    break;
                default:
                    style.BackColor = ModernTheme.DangerSoft;
                    style.ForeColor = ModernTheme.DangerText;
                    break;
            }
        }
        dgv.ClearSelection();
    }

    private IEnumerable<TankhahImportRow> Selected() => _rows.Where(r =>
        r.Transaction is not null &&
        (r.Status == TankhahImportStatus.Valid || (chkDuplicates.Checked && r.Status == TankhahImportStatus.Duplicate)));

    private void RefreshSummary()
    {
        int valid = _rows.Count(r => r.Status == TankhahImportStatus.Valid);
        int dup = _rows.Count(r => r.Status == TankhahImportStatus.Duplicate);
        int err = _rows.Count(r => r.Status == TankhahImportStatus.Error);
        var sel = Selected().ToList();
        decimal total = sel.Sum(r => r.Amount);
        decimal after = _currentBalance - total;

        string F(decimal d) => NumberHelper.FormatThousands(d, usePersianDigits: true);
        string N(int n) => StringHelper.ConvertToPersianDigits(n.ToString());

        lblSummary.Text =
            $"کل ردیف‌ها: {N(_rows.Count)}   |   معتبر: {N(valid)}   |   تکراری: {N(dup)}   |   دارای خطا: {N(err)}\n" +
            $"مجموع ردیف‌های قابل ثبت: {F(total)} ریال   |   مانده‌ی لیست پس از ثبت: {F(after)} ریال";

        btnSave.Enabled = sel.Count > 0;
        btnSave.Text = sel.Count > 0 ? $"ثبت {N(sel.Count)} ردیف در لیست" : "ردیفی برای ثبت وجود ندارد";
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var sel = Selected().ToList();
        if (sel.Count == 0) return;

        decimal total = sel.Sum(r => r.Amount);
        string msg = $"{NumberHelper.FormatThousands(sel.Count, usePersianDigits: true)} ردیف به مبلغ کل {NumberHelper.FormatThousands(total, usePersianDigits: true)} ریال در لیست ثبت شود؟";
        if (_currentBalance - total < 0)
            msg += "\n\n⚠ مانده‌ی لیست پس از ثبت منفی می‌شود (مجموع هزینه‌ها از شارژ لیست بیشتر است).";

        if (!ModernMessageBox.AskConfirm(msg, "تایید ایمپورت")) return;

        ToImport = sel.Select(r => r.Transaction!).ToList();
        DialogResult = DialogResult.OK;
        Close();
    }
}
