using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace Smp.Forms;

public class PayslipForm : Form
{
    private readonly PayrollResult _payroll;
    private Panel mainPanel = null!;          // «برگه‌ی فیش» — همین پنل چاپ می‌شود
    private Bitmap? _memoryImage;
    private ModernButton btnPrint = null!, btnClose = null!;

    private static readonly Color PositiveText = ModernPaint.Darken(ModernTheme.Success, 70);

    public PayslipForm(PayrollResult payroll)
    {
        _payroll = payroll;

        BuildUI();

        this.ApplyStandardHeader($"فیش حقوقی - {payroll.EmployeeName}", new Size(880, 800));
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        CancelButton = btnClose;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnPrint);
    }

    // ═══════════════════════════════════════════════════════════
    //  رابط کاربری
    // ═══════════════════════════════════════════════════════════
    private void BuildUI()
    {
        // زمینه‌ی اسکرول‌دار + برگه‌ی سفید فیش
        var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ModernTheme.Background, Padding = new Padding(24, 20, 24, 20) };

        mainPanel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.White,
            Padding = new Padding(28, 22, 28, 24)
        };
        mainPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, mainPanel.Width - 1, mainPanel.Height - 1);
        };

        // ترتیب افزودن: آخرین کنترل Dock=Top بالاترین جا را می‌گیرد؛ پس از پایین به بالا اضافه می‌کنیم
        mainPanel.Controls.Add(BuildWordsLabel());
        mainPanel.Controls.Add(BuildNetBanner());
        mainPanel.Controls.Add(Spacer(14));
        mainPanel.Controls.Add(BuildTable());
        mainPanel.Controls.Add(Spacer(16));
        mainPanel.Controls.Add(BuildHeader());

        scrollPanel.Controls.Add(mainPanel);

        // نوار دکمه‌ها
        btnPrint = ModernButton.Create("🖨️ چاپ فیش حقوقی", ModernTheme.Primary, 200, 40);
        btnPrint.Click += BtnPrint_Click;

        btnClose = ModernButton.Cancel("بستن", 110, 40);
        btnClose.DialogResult = DialogResult.Cancel;

        var bar = ModernForm.CreateButtonBar(btnPrint, btnClose);
        bar.Dock = DockStyle.Bottom;
        bar.Height = 64;
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1f);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        Controls.Add(scrollPanel);   // Fill
        Controls.Add(bar);           // Bottom
    }

    private static Panel Spacer(int height) => new Panel { Dock = DockStyle.Top, Height = height, BackColor = Color.Transparent };

    /// <summary>سربرگ برگه: لوگو + نام شرکت + عنوان سند، با خط رنگی زیر آن</summary>
    private Control BuildHeader()
    {
        var header = new BufferedTable { Dock = DockStyle.Top, Height = 84, ColumnCount = 2, RowCount = 2, BackColor = Color.White, Padding = new Padding(0, 0, 0, 6) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));   // ستون ۰ = راست
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
        header.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Primary, 2f);
            e.Graphics.DrawLine(pen, 0, header.Height - 2, header.Width, header.Height - 2);
        };

        var pic = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 10, 0),
            Image = ModernForm.GetCompanyLogo()
        };
        header.Controls.Add(pic, 0, 0);
        header.SetRowSpan(pic, 2);

        header.Controls.Add(new Label
        {
            Text = "شرکت مهندسی ستاوند شمس",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,   // فرم Mirror: «چپ» یعنی کنار لوگو در سمت راست
            Font = ModernTheme.GetFont(15f, FontStyle.Bold),
            ForeColor = ModernTheme.TextStrong,
            BackColor = Color.White,
            Margin = Padding.Empty
        }, 1, 0);

        header.Controls.Add(new Label
        {
            Text = "فیش حقوق و مزایای ماهانه",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ModernTheme.GetFont(12f, FontStyle.Bold),
            ForeColor = ModernTheme.Primary,
            BackColor = Color.White,
            Margin = Padding.Empty
        }, 1, 1);

        return header;
    }

    /// <summary>
    /// جدول اصلی. خطوط جدول همان رنگ زمینه‌ی خود جدول است که از لابه‌لای حاشیه‌ی ۱ پیکسلی سلول‌ها دیده می‌شود
    /// (هیچ نقاشی دستی لازم نیست، پس در فرم Mirror و هنگام چاپ هم دقیق می‌ماند).
    /// </summary>
    private Control BuildTable()
    {
        var layout = new BufferedTable
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = ModernTheme.InputBorder,
            Padding = new Padding(1, 1, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

        int row = 0;

        // ── ردیف اطلاعات پرسنل ──
        AddRow(layout, 32F);
        layout.Controls.Add(Cell($"نام: {_payroll.EmployeeName}", ModernTheme.SurfaceMuted, label: true, bold: true), 0, row);
        layout.Controls.Add(Cell($"کد: {_payroll.PersonnelCode}", ModernTheme.SurfaceMuted, label: true), 1, row);
        layout.Controls.Add(Cell($"ماه: {TranslationHelper.GetMonthName(_payroll.Month)}", ModernTheme.SurfaceMuted, label: true), 2, row);
        layout.Controls.Add(Cell($"سال: {StringHelper.ConvertToPersianDigits(_payroll.Year.ToString())}", ModernTheme.SurfaceMuted, label: true), 3, row);
        row++;

        // ── هدر ستون‌ها ──
        AddRow(layout, 34F);
        layout.Controls.Add(Header("شرح مزایا"), 0, row);
        layout.Controls.Add(Header("مبلغ (ریال)"), 1, row);
        layout.Controls.Add(Header("شرح کسورات"), 2, row);
        layout.Controls.Add(Header("مبلغ (ریال)"), 3, row);
        row++;

        // ── داده‌ها ──
        var benefits = new List<(string Name, decimal Amount)>
        {
            ("حقوق پایه", _payroll.BaseSalary),
            ("پایه سنوات", _payroll.YearsBonus),
            ("حق مسکن", _payroll.HousingAllowance),
            ("حق خواربار", _payroll.GroceryAllowance),
            ("حق اولاد", _payroll.ChildAllowance),
            ("حق تاهل", _payroll.MarriageAllowance),
            ("حق جذب", _payroll.AttractionAllowance),
            ("حق مسئولیت", _payroll.ResponsibilityAllowance),
            ("حق مدیریت", _payroll.ManagementAllowance),
            ("حق فنی", _payroll.TechnicalAllowance),
            ("حق ایاب و ذهاب", _payroll.TransportationAllowance),
            ("سایر مزایا", _payroll.OtherAllowances),
            ("اضافه‌کاری", _payroll.OvertimePay)
        };

        var deductions = new List<(string Name, decimal Amount)>
        {
            ("بیمه سهم کارمند", _payroll.InsuranceEmployee),
            ("مالیات بر درآمد", _payroll.IncomeTax),
            ("کسر وام", _payroll.LoanDeduction),
            ("کسر مساعده", _payroll.AdvanceDeduction),
            ("بیمه تکمیلی", _payroll.SupplementaryInsuranceDeduction),
            ("بیمه خودرو", _payroll.CarInsuranceDeduction),
            ("سایر کسورات", _payroll.OtherDeductions)
        };

        int maxRows = Math.Max(benefits.Count, deductions.Count);
        for (int i = 0; i < maxRows; i++)
        {
            AddRow(layout, 30F);
            Color back = i % 2 == 1 ? ModernTheme.SurfaceAlt : Color.White;

            if (i < benefits.Count)
            {
                layout.Controls.Add(Cell(benefits[i].Name, back, label: true), 0, row);
                layout.Controls.Add(Amount(benefits[i].Amount, back), 1, row);
            }
            else
            {
                layout.Controls.Add(Cell("", back), 0, row);
                layout.Controls.Add(Cell("", back), 1, row);
            }

            if (i < deductions.Count)
            {
                layout.Controls.Add(Cell(deductions[i].Name, back, label: true), 2, row);
                layout.Controls.Add(Amount(deductions[i].Amount, back), 3, row);
            }
            else
            {
                layout.Controls.Add(Cell("", back), 2, row);
                layout.Controls.Add(Cell("", back), 3, row);
            }
            row++;
        }

        // ── جمع کل ──
        AddRow(layout, 38F);
        layout.Controls.Add(Cell("جمع مزایا", ModernTheme.SuccessSoft, label: true, bold: true, color: PositiveText), 0, row);
        layout.Controls.Add(Cell(Fmt(_payroll.TotalEarnings), ModernTheme.SuccessSoft, bold: true, color: PositiveText), 1, row);
        layout.Controls.Add(Cell("جمع کسورات", ModernTheme.DangerSoft, label: true, bold: true, color: ModernTheme.DangerText), 2, row);
        layout.Controls.Add(Cell(Fmt(_payroll.TotalDeductions), ModernTheme.DangerSoft, bold: true, color: ModernTheme.DangerText), 3, row);

        return layout;
    }

    private Control BuildNetBanner() => new RoundedBanner
    {
        Dock = DockStyle.Top,
        Height = 56,
        Text = $"خالص پرداختی: {Fmt(_payroll.NetPay)} ریال",
        Fill = ModernTheme.Success,
        ForeColor = Color.White,
        TextFont = ModernTheme.GetFont(14f, FontStyle.Bold)
    };

    private Control BuildWordsLabel() => new Label
    {
        Text = $"مبلغ قابل پرداخت به حروف: {NumberHelper.ToPersianWords(_payroll.NetPay)} ریال",
        Dock = DockStyle.Top,
        Height = 56,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = ModernTheme.GetFont(11f, FontStyle.Bold),
        ForeColor = ModernTheme.Text,
        BackColor = Color.White,
        Padding = new Padding(4, 8, 4, 0)
    };

    // ── ابزارهای ساخت سلول ──
    private static string Fmt(decimal v) => NumberHelper.FormatThousands(v, usePersianDigits: true);

    private static void AddRow(TableLayoutPanel t, float height)
    {
        t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
    }

    /// <summary>label=true: متن راست‌چین (ستون‌های شرح)؛ در غیر این صورت وسط‌چین</summary>
    private static Label Cell(string text, Color back, bool label = false, bool bold = false, Color? color = null) => new Label
    {
        Text = text,
        Dock = DockStyle.Fill,
        // فرم Mirror شده: MiddleLeft یعنی متن در سمت راست سلول
        TextAlign = label ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter,
        Font = ModernTheme.GetFont(10.5f, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = color ?? ModernTheme.Text,
        BackColor = back,
        Margin = new Padding(0, 0, 1, 1),
        Padding = new Padding(8, 0, 8, 0)
    };

    private static Label Header(string text) => new Label
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
        ForeColor = ModernTheme.GridHeaderText,
        BackColor = ModernTheme.GridHeaderBack,
        Margin = new Padding(0, 0, 1, 1)
    };

    /// <summary>مبلغ صفر کم‌رنگ نشان داده می‌شود تا ردیف‌های پرمقدار بهتر دیده شوند</summary>
    private static Label Amount(decimal value, Color back)
        => Cell(Fmt(value), back, color: value == 0 ? ModernTheme.TextDisabled : ModernTheme.TextStrong);

    // ═══════════════════════════════════════════════════════════
    //  چاپ
    // ═══════════════════════════════════════════════════════════
    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        try
        {
            _memoryImage?.Dispose();
            _memoryImage = new Bitmap(mainPanel.Width, mainPanel.Height);
            mainPanel.DrawToBitmap(_memoryImage, new Rectangle(0, 0, mainPanel.Width, mainPanel.Height));

            using var pd = new PrintDocument();
            pd.PrintPage += PrintPage;
            pd.DefaultPageSettings.Landscape = false;

            ModernPrint.ShowPreview(pd, "پیش‌نمایش چاپ فیش حقوقی");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "PayslipForm.BtnPrint");
            ModernMessageBox.ShowError("خطا در آماده‌سازی چاپ:\n" + ex.Message);
        }
    }

    private void PrintPage(object sender, PrintPageEventArgs e)
    {
        if (_memoryImage is not null)
        {
            int x = e.MarginBounds.X;
            int y = e.MarginBounds.Y;
            int width = e.MarginBounds.Width;
            int height = (int)((float)width * _memoryImage.Height / _memoryImage.Width);

            e.Graphics?.DrawImage(_memoryImage, x, y, width, height);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _memoryImage?.Dispose();
        base.Dispose(disposing);
    }

    // ═══════════════════════════════════════════════════════════
    //  بنر گوشه‌گرد (خالص پرداختی)
    // ═══════════════════════════════════════════════════════════
    private sealed class RoundedBanner : Control
    {
        public Color Fill { get; set; } = ModernTheme.Success;
        public Font TextFont { get; set; } = ModernTheme.GetFont(13f, FontStyle.Bold);

        public RoundedBanner()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(ModernPaint.BehindColor(this));

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width < 12 || rect.Height < 12) return;
            using (var path = ModernPaint.RoundedRect(rect, ModernTheme.CornerRadius))
            using (var br = new SolidBrush(Fill))
                g.FillPath(br, path);

            TextRenderer.DrawText(g, Text, TextFont, rect, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
        }
    }
}
