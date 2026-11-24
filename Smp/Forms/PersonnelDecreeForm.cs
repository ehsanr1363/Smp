using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class PersonnelDecreeForm : Form
{
    private readonly Employee _employee;
    private Panel mainPanel = null!;          // «برگه‌ی حکم» — همین پنل چاپ می‌شود
    private PictureBox picLogo = null!;
    private ModernButton btnPrint = null!, btnClose = null!;
    private Bitmap? _memoryImage;

    /// <summary>یک جفت «عنوان: مقدار» در جدول مشخصات</summary>
    private readonly record struct Field(string Label, string? Value, bool Muted = false);

    public PersonnelDecreeForm(Employee employee)
    {
        _employee = employee;

        BuildUI();

        this.ApplyStandardHeader($"حکم پرسنلی - {employee.FullName}", new Size(860, 900));
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        CancelButton = btnClose;

        // میانبرهای Ctrl+S (چاپ) و Esc (بستن)
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnPrint);

        // اگر لوگوی فرم هدر خالی بود، اولین شرکتِ دارای لوگو پس از نمایش فرم بارگذاری می‌شود
        Shown += async (s, e) => { if (picLogo.Image is null) await LoadLogoAsync(); };
    }

    // ═══════════════════════════════════════════════════════════
    //  رابط کاربری
    // ═══════════════════════════════════════════════════════════
    private void BuildUI()
    {
        var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ModernTheme.Background, Padding = new Padding(24, 20, 24, 20) };

        mainPanel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.White,
            Padding = new Padding(34, 26, 34, 28)
        };
        mainPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, mainPanel.Width - 1, mainPanel.Height - 1);
        };

        decimal total = _employee.BaseSalary + _employee.HousingAllowance + _employee.GroceryAllowance +
                        _employee.ChildAllowance + _employee.YearsBonus + _employee.AttractionAllowance +
                        _employee.ResponsibilityAllowance + _employee.TechnicalAllowance +
                        _employee.ManagementAllowance + _employee.OtherAllowances;

        // اجزای برگه از بالا به پایین
        var parts = new List<Control>
        {
            BuildHeader(),
            Spacer(14),
            BuildMetaRow(),
            BuildIntro(),
            SectionTitle("مشخصات پرسنل"),
            BuildGrid(new[]
            {
                (Info("نام و نام خانوادگی", _employee.FullName), Info("کد ملی", _employee.NationalCode)),
                (Info("واحد سازمانی", _employee.DepartmentName), Info("سمت", _employee.Position)),
                (Info("تاریخ استخدام", DateHelper.ToPersianDate(_employee.HireDate)), Info("نوع قرارداد", _employee.PersonnelType)),
                (Info("شروع قرارداد", DateHelper.ToPersianDate(_employee.ContractStartDate)), Info("پایان قرارداد", DateHelper.ToPersianDate(_employee.ContractEndDate)))
            }),
            Spacer(8),
            SectionTitle("حقوق و مزایا"),
            BuildGrid(new[]
            {
                (Money("حقوق پایه", _employee.BaseSalary), Money("حق مسکن", _employee.HousingAllowance)),
                (Money("حق بن خواربار", _employee.GroceryAllowance), Money("حق اولاد", _employee.ChildAllowance)),
                (Money("پایه سنوات", _employee.YearsBonus), Money("حق جذب", _employee.AttractionAllowance)),
                (Money("حق مسئولیت", _employee.ResponsibilityAllowance), Money("حق تخصصی", _employee.TechnicalAllowance)),
                (Money("حق مدیریت", _employee.ManagementAllowance), Money("سایر مزایا", _employee.OtherAllowances))
            }),
            Spacer(12),
            new RoundedBanner
            {
                Dock = DockStyle.Top,
                Height = 54,
                Text = $"جمع کل حقوق و مزایا: {Fmt(total)} ریال",
                Fill = ModernTheme.Primary,
                ForeColor = Color.White,
                TextFont = ModernTheme.GetFont(13.5f, FontStyle.Bold)
            },
            Spacer(18),
            BuildSignatures()
        };

        // آخرین کنترل Dock=Top بالاترین جا را می‌گیرد؛ پس معکوس اضافه می‌کنیم
        for (int i = parts.Count - 1; i >= 0; i--) mainPanel.Controls.Add(parts[i]);
        scrollPanel.Controls.Add(mainPanel);

        // نوار دکمه‌ها
        btnPrint = ModernButton.Create("🖨️ چاپ حکم", ModernTheme.Primary, 170, 40);
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

    /// <summary>سربرگ: لوگو (راست)، «بسمه تعالی / حکم پرسنلی» (وسط)، خط رنگی زیر آن</summary>
    private Control BuildHeader()
    {
        var header = new BufferedTable { Dock = DockStyle.Top, Height = 104, ColumnCount = 3, RowCount = 2, BackColor = Color.White, Padding = new Padding(0, 0, 0, 8) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));   // ستون ۰ = راست (لوگو)
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));   // برای متقارن ماندن عنوان
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 62F));
        header.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Primary, 2f);
            e.Graphics.DrawLine(pen, 0, header.Height - 2, header.Width, header.Height - 2);
        };

        picLogo = new PictureBox
        {
            Name = "picLogo",
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 10, 0),
            Image = ModernForm.GetCompanyLogo()
        };
        header.Controls.Add(picLogo, 0, 0);
        header.SetRowSpan(picLogo, 2);

        header.Controls.Add(new Label
        {
            Text = "بسمه تعالی",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomCenter,
            Font = ModernTheme.GetFont(11f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.White,
            Margin = Padding.Empty
        }, 1, 0);

        header.Controls.Add(new Label
        {
            Text = "حکم پرسنلی",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(22f, FontStyle.Bold),
            ForeColor = ModernTheme.Primary,
            BackColor = Color.White,
            Margin = Padding.Empty
        }, 1, 1);

        return header;
    }

    /// <summary>تاریخ (راست) و شماره (چپ)</summary>
    private Control BuildMetaRow()
    {
        var t = new BufferedTable { Dock = DockStyle.Top, Height = 36, ColumnCount = 2, RowCount = 1, BackColor = Color.White };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // فرم Mirror شده: MiddleLeft = متن سمت راست، MiddleRight = متن سمت چپ
        t.Controls.Add(MetaLabel($"تاریخ: {DateHelper.ToPersianDate(DateTime.Now)}", ContentAlignment.MiddleLeft), 0, 0);
        t.Controls.Add(MetaLabel($"شماره: {_employee.PersonnelCode}", ContentAlignment.MiddleRight), 1, 0);
        return t;
    }

    private static Label MetaLabel(string text, ContentAlignment align) => new Label
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = align,
        Font = ModernTheme.GetFont(11f),
        ForeColor = ModernTheme.Text,
        BackColor = Color.White,
        Margin = Padding.Empty
    };

    private static Control BuildIntro() => new Label
    {
        Text = "بدینوسیله به اطلاع می‌رساند:",
        Dock = DockStyle.Top,
        Height = 44,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = ModernTheme.GetFont(12.5f, FontStyle.Bold),
        ForeColor = ModernTheme.TextStrong,
        BackColor = Color.White
    };

    private static Control SectionTitle(string text) => new Label
    {
        Text = text,
        Dock = DockStyle.Top,
        Height = 34,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = ModernTheme.GetFont(12f, FontStyle.Bold),
        ForeColor = ModernTheme.Primary,
        BackColor = Color.White
    };

    // ── جدول مشخصات: عنوان | مقدار | عنوان | مقدار ──
    // خطوط جدول همان رنگ زمینه‌ی خود جدول است که از لابه‌لای حاشیه‌ی ۱ پیکسلی سلول‌ها دیده می‌شود
    // (بدون نقاشی دستی؛ در فرم Mirror و هنگام چاپ هم دقیق می‌ماند).
    private static Control BuildGrid((Field Right, Field Left)[] rows)
    {
        var grid = new BufferedTable
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            RowCount = rows.Length,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = ModernTheme.InputBorder,
            Padding = new Padding(1, 1, 0, 0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));   // ستون ۰ = راست
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

        for (int r = 0; r < rows.Length; r++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            grid.Controls.Add(CaptionCell(rows[r].Right.Label), 0, r);
            grid.Controls.Add(ValueCell(rows[r].Right), 1, r);
            grid.Controls.Add(CaptionCell(rows[r].Left.Label), 2, r);
            grid.Controls.Add(ValueCell(rows[r].Left), 3, r);
        }
        return grid;
    }

    private static Label CaptionCell(string text) => new Label
    {
        Text = text + ":",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,   // Mirror: متن در سمت راست سلول
        Font = ModernTheme.GetFont(10.5f),
        ForeColor = ModernTheme.TextMuted,
        BackColor = ModernTheme.SurfaceMuted,
        Margin = new Padding(0, 0, 1, 1),
        Padding = new Padding(8, 0, 8, 0)
    };

    private static Label ValueCell(Field f) => new Label
    {
        Text = string.IsNullOrWhiteSpace(f.Value) ? "---" : f.Value,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = ModernTheme.GetFont(11f, FontStyle.Bold),
        ForeColor = f.Muted || string.IsNullOrWhiteSpace(f.Value) ? ModernTheme.TextDisabled : ModernTheme.TextStrong,
        BackColor = Color.White,
        Margin = new Padding(0, 0, 1, 1),
        Padding = new Padding(8, 0, 8, 0)
    };

    private static Field Info(string label, string? value) => new Field(label, value);

    /// <summary>مبلغ صفر کم‌رنگ نشان داده می‌شود</summary>
    private static Field Money(string label, decimal value) => new Field(label, Fmt(value) + " ریال", value == 0);

    private static string Fmt(decimal v) => NumberHelper.FormatThousands(v, usePersianDigits: true);

    /// <summary>جای امضا: عنوان بالا + خط امضا پایین</summary>
    private static Control BuildSignatures()
    {
        var t = new BufferedTable { Dock = DockStyle.Top, Height = 120, ColumnCount = 2, RowCount = 1, BackColor = Color.White };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        t.Controls.Add(SignBox("امضاء کارمند"), 0, 0);
        t.Controls.Add(SignBox("امضاء مدیریت"), 1, 0);
        return t;
    }

    private static Control SignBox(string caption)
    {
        var box = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(12, 0, 12, 0) };
        box.Paint += (s, e) =>
        {
            int y = box.Height - 18;
            using var pen = new Pen(ModernTheme.InputBorder, 1.2f) { DashStyle = DashStyle.Dash };
            e.Graphics.DrawLine(pen, box.Width * 0.12f, y, box.Width * 0.88f, y);
        };
        box.Controls.Add(new Label
        {
            Text = caption + ":",
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(11f, FontStyle.Bold),
            ForeColor = ModernTheme.Text,
            BackColor = Color.White
        });
        return box;
    }

    // ═══════════════════════════════════════════════════════════
    //  لوگو (در صورت نبودن در کش هدر)
    // ═══════════════════════════════════════════════════════════
    private async Task LoadLogoAsync()
    {
        try
        {
            var repo = new CompanyRepository(AppConfig.ConnectionString);
            var company = await Task.Run(() => repo.GetAll().FirstOrDefault(c => c.Logo is not null && c.Logo.Length > 0));

            if (company is not null && company.Logo is not null && company.Logo.Length > 0 && !IsDisposed)
            {
                // ImageHelper برای جلوگیری از باگ GDI+ Stream
                picLogo.Image = ImageHelper.ByteArrayToImage(company.Logo);
            }
        }
        catch (Exception ex)
        {
            // خطای لوگو نباید مانع کار شود، فقط لاگ می‌شود.
            LogHelper.LogWarning("خطا در بارگذاری لوگو در حکم پرسنلی: " + ex.Message, "PersonnelDecreeForm");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  چاپ و پیش‌نمایش
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

            ModernPrint.ShowPreview(pd, "پیش‌نمایش چاپ حکم پرسنلی");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "PersonnelDecreeForm.BtnPrint");
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
    //  بنر گوشه‌گرد (جمع کل)
    // ═══════════════════════════════════════════════════════════
    private sealed class RoundedBanner : Control
    {
        public Color Fill { get; set; } = ModernTheme.Primary;
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
