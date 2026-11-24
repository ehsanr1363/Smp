using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Smp.Helpers;

namespace Smp.Forms;

/// <summary>
/// پیش‌نمایش ایمپورت DBF بیمه: لیست تک‌تک ردیف‌های فایل با وضعیت هر کدام
/// (جدید / آپدیت / پرسنل یافت نشد / خطا). هیچ داده‌ای در دیتابیس ذخیره نشده است.
/// OK = کاربر «تأیید و ایمپورت» را زده است.
/// </summary>
public class DbfPreviewForm : Form
{
    private readonly DbfImportResult _result;
    private readonly bool _abortOnError;
    private readonly bool _hasIssues;

    private DataGridView grid = null!;
    private ModernCheckBox chkOnlyProblems = null!;

    public DbfPreviewForm(DbfImportResult result, bool abortOnError)
    {
        _result = result;
        _abortOnError = abortOnError;
        _hasIssues = result.ErrorCount > 0 || result.MissingPersonnel.Count > 0;
        BuildUI();
        FillGrid();
    }

    private static string Num(int n) => NumberHelper.FormatThousands(n, usePersianDigits: true);

    private void BuildUI()
    {
        // ───────────── خلاصه ─────────────
        string line1 = $"هدر: {Num(_result.HeaderInsertCount)} جدید، {Num(_result.HeaderUpdateCount)} آپدیت   |   " +
                       $"جزئیات: {Num(_result.DetailsInsertCount)} جدید، {Num(_result.DetailsUpdateCount)} آپدیت";
        if (_result.SkippedWorkshopCount > 0)
            line1 += $"   |   هدر ردشده (کارگاه ثبت‌نشده): {Num(_result.SkippedWorkshopCount)}";

        string line2;
        if (!_hasIssues)
            line2 = "✅ مشکلی پیدا نشد؛ می‌توانید «تأیید و ایمپورت» را بزنید.";
        else
        {
            line2 = $"⚠️ پرسنل یافت نشده: {Num(_result.MissingPersonnel.Count)} نفر   |   ردیف‌های دارای خطا: {Num(_result.ErrorCount)}";
            line2 += _abortOnError
                ? "\n⛔ با گزینه‌ی «توقف کامل در صورت خطا»، ایمپورت واقعی با این مشکلات انجام نمی‌شود."
                : "\nℹ️ ایمپورت واقعی ردیف‌های مشکل‌دار را رد می‌کند و بقیه را ذخیره می‌کند.";
        }

        var lblSummary = new Label
        {
            Text = line1 + "\n" + line2,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,      // فرم Mirror شده: متن سمت راست
            Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
            ForeColor = _hasIssues ? Color.FromArgb(185, 28, 28) : Color.FromArgb(21, 128, 61),
            BackColor = Color.Transparent,
            Padding = new Padding(8, 0, 8, 0)
        };

        chkOnlyProblems = ModernUI.Check("فقط مشکل‌دارها");
        chkOnlyProblems.Width = 190;
        chkOnlyProblems.Anchor = AnchorStyles.Left;
        chkOnlyProblems.Checked = false;
        chkOnlyProblems.CheckedChanged += (s, e) => FillGrid();

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        top.Controls.Add(lblSummary, 0, 0);
        top.Controls.Add(chkOnlyProblems, 1, 0);

        // ───────────── جدول ─────────────
        grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RightToLeft = RightToLeft.Yes,
            EnableHeadersVisualStyles = false
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(243, 240, 235);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = ModernTheme.Text;
        grid.ColumnHeadersDefaultCellStyle.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DefaultCellStyle.Font = ModernTheme.GetFont(10f);
        grid.RowTemplate.Height = 30;
        grid.ColumnHeadersHeight = 34;

        AddCol("RowNo", "ردیف", 6);
        AddCol("Kind", "نوع", 8);
        AddCol("Name", "نام", 20);
        AddCol("NationalCode", "کد ملی", 12);
        AddCol("WorkshopCode", "کارگاه", 10);
        AddCol("Period", "سال/ماه", 8);
        AddCol("ListNo", "لیست", 6);
        AddCol("Days", "روز", 6);
        AddCol("Status", "وضعیت", 12);
        AddCol("Note", "توضیح", 22);

        // ───────────── دکمه‌ها ─────────────
        var btnProceed = ModernButton.Success("✅ تأیید و ایمپورت", 220, 42);
        btnProceed.Enabled = !(_abortOnError && _hasIssues);
        btnProceed.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

        var btnClose = ModernButton.Outline("بستن", ModernTheme.Primary, 120, 42);
        btnClose.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var bar = ModernForm.CreateButtonBar(btnProceed, btnClose);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(20, 12, 20, 0), BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(top, 0, 0);
        root.Controls.Add(grid, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);

        this.ApplyStandardHeader("پیش‌نمایش ایمپورت بیمه (هیچ داده‌ای هنوز ذخیره نشده)", new Size(1100, 680), false);
        ShowInTaskbar = false;
    }

    private void AddCol(string name, string header, float weight)
    {
        var col = new DataGridViewTextBoxColumn { Name = name, HeaderText = header, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable };
        grid.Columns.Add(col);
    }

    private void FillGrid()
    {
        bool onlyProblems = chkOnlyProblems?.Checked ?? false;
        grid.SuspendLayout();
        grid.Rows.Clear();

        foreach (var r in _result.PreviewRows)
        {
            if (onlyProblems && !r.IsProblem) continue;

            int idx = grid.Rows.Add(
                StringHelper.ConvertToPersianDigits(r.RowNo.ToString()),
                r.Kind,
                r.Name,
                StringHelper.ConvertToPersianDigits(r.NationalCode ?? ""),
                StringHelper.ConvertToPersianDigits(r.WorkshopCode ?? ""),
                StringHelper.ConvertToPersianDigits(r.Period ?? ""),
                StringHelper.ConvertToPersianDigits(r.ListNo ?? ""),
                StringHelper.ConvertToPersianDigits(r.Days ?? ""),
                r.Status,
                r.Note);

            Color back = r.IsProblem ? ModernTheme.DangerSoft
                       : r.IsUpdate ? Color.FromArgb(219, 234, 254)
                       : r.Status == "جدید" ? ModernTheme.SuccessSoft
                       : Color.FromArgb(254, 249, 195);          // کارگاه ثبت‌نشده (رد می‌شود)
            grid.Rows[idx].DefaultCellStyle.BackColor = back;
            grid.Rows[idx].DefaultCellStyle.SelectionBackColor = back;
            grid.Rows[idx].DefaultCellStyle.SelectionForeColor = Color.Black;
        }

        grid.ResumeLayout();
    }
}
