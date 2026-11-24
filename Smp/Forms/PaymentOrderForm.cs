using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Smp.Forms;

public class PaymentOrderItem
{
    public string Description { get; set; } = "";
    public string ReferenceNumber { get; set; } = "";
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string RecipientName { get; set; } = "";
}

public class PaymentOrderForm : Form
{
    private readonly List<PaymentOrderItem> _items;
    private readonly CompanyBankAccount _account;
    private string _companyName;

    private PrintPreviewControl previewControl = null!;
    private PrintDocument printDoc = null!;
    private Button btnPrint = null!, btnSelectLogo = null!;
    private Image? _logoImage = null;

    private readonly string _logoConfigFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Smp", "logo_path.txt");

    private string _customLogoPath = "";

    private int _printIndex = 0;
    private const int MaxRowsPerPage = 10;

    // ✅ FIX: نگه‌داری پیام هشدار برای نمایش در Shown
    private string? _pendingWarning = null;

    // سازنده برای چک‌ها
    public PaymentOrderForm(List<CashCheck> checks, CompanyBankAccount account, string companyName = "")
    {
        _items = checks.Select(c => new PaymentOrderItem
        {
            Description = c.Description ?? c.RecipientName ?? "---",
            ReferenceNumber = c.CheckNumber,
            Date = c.DueDate,
            Amount = c.Amount,
            RecipientName = c.RecipientName ?? ""
        }).ToList();

        _account = account;
        _companyName = string.IsNullOrWhiteSpace(companyName) ? "شرکت مهندسی ستاوند شمس (سهامی خاص)" : companyName;

        InitForm();
    }

    // سازنده برای ردیف‌های آماده (مثلاً شارژ تنخواه)
    public PaymentOrderForm(List<PaymentOrderItem> items, CompanyBankAccount account, string companyName = "")
    {
        _items = items?.ToList() ?? new List<PaymentOrderItem>();
        _account = account;
        _companyName = string.IsNullOrWhiteSpace(companyName) ? "شرکت مهندسی ستاوند شمس (سهامی خاص)" : companyName;

        InitForm();
    }

    // ✅ FIX: فقط تراکنش‌های «پرداختی» در دستور پرداخت گنجانده می‌شوند
    //         اگر تراکنش دریافتی در لیست بود، به‌طور خودکار حذف می‌شود (با هشدار به کاربر)
    public PaymentOrderForm(List<CashTransaction> transactions, CompanyBankAccount account, string companyName = "")
    {
        var outgoing = transactions?.Where(t => t.Direction == DirectionTypes.Outgoing).ToList()
                      ?? new List<CashTransaction>();

        int filteredCount = (transactions?.Count ?? 0) - outgoing.Count;
        if (filteredCount > 0)
        {
            _pendingWarning = $"توجه: {filteredCount} تراکنش «دریافتی» از دستور پرداخت حذف شد (فقط پرداختی‌ها چاپ می‌شوند).";
        }

        if (outgoing.Count == 0)
        {
            _pendingWarning = "هیچ تراکنش پرداختی برای صدور دستور پرداخت انتخاب نشده است.";
        }

        _items = outgoing.Select(t => new PaymentOrderItem
        {
            Description = t.Description ?? t.RecipientName ?? "---",
            ReferenceNumber = "---",
            Date = t.TransactionDate,
            Amount = t.Amount,
            RecipientName = t.RecipientName ?? t.TafsiliTitle ?? ""
        }).ToList();

        _account = account;
        _companyName = string.IsNullOrWhiteSpace(companyName) ? "شرکت مهندسی ستاوند شمس (سهامی خاص)" : companyName;

        InitForm();
    }

    private void InitForm()
    {
        LoadCompanyInfoAndLogo();
        BuildUI();
        this.ApplyStandardHeader("دستور پرداخت خزانه‌داری", new Size(850, 950), isDialog: false);

        // ✅ نمایش هشدار و بستن اگر هیچ آیتمی وجود ندارد
        Shown += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_pendingWarning))
            {
                ModernMessageBox.ShowWarning(_pendingWarning);
            }

            if (_items.Count == 0)
            {
                Close();
            }
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { printDoc?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PaymentOrderForm"); }
            try { _logoImage?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PaymentOrderForm"); }
        }
        base.Dispose(disposing);
    }

    private void LoadCompanyInfoAndLogo()
    {
        try
        {
            var repo = new CompanyRepository(AppConfig.ConnectionString);
            var company = repo.GetAll().FirstOrDefault();

            if (company is not null)
            {
                if (_companyName.Contains("ستاوند"))
                {
                    _companyName = company.Name;
                }

                if (company.Logo is not null && company.Logo.Length > 0)
                {
                    _logoImage = ImageHelper.ByteArrayToImage(company.Logo);
                }
            }

            if (File.Exists(_logoConfigFile))
            {
                string path = File.ReadAllText(_logoConfigFile).Trim();
                if (File.Exists(path))
                {
                    _customLogoPath = path;
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                    using var img = Image.FromStream(stream);
                    var oldImage = _logoImage;
                    _logoImage = new Bitmap(img);
                    oldImage?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogWarning("خطا در بارگذاری لوگو: " + ex.Message, "PaymentOrderForm");
        }
    }

    private void SaveLogoPath(string path)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_logoConfigFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_logoConfigFile, path);
        }
        catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PaymentOrderForm"); }
    }

    private void BuildUI()
    {
        printDoc = new PrintDocument();
        printDoc.PrintPage += PrintDoc_PrintPage;
        printDoc.BeginPrint += (s, e) => _printIndex = 0;

        previewControl = new PrintPreviewControl
        {
            Dock = DockStyle.Fill,
            Document = printDoc,
            Zoom = 0.8,
            UseAntiAlias = true,
            BackColor = ModernTheme.SurfaceMuted
        };

        btnSelectLogo = ModernButton.Soft("انتخاب فایل لوگو (اختیاری)", ModernTheme.Warning, 240, 40);
        btnSelectLogo.Click += BtnSelectLogo_Click;

        btnPrint = ModernButton.Success("چاپ دستور پرداخت", 190, 40);
        btnPrint.Click += BtnPrint_Click;

        // دکمه‌ها وسط‌چین می‌شوند (بدون محاسبه‌ی دستی موقعیت)
        var buttonBar = ModernForm.CreateButtonBar(btnPrint, btnSelectLogo);
        buttonBar.WrapContents = false;
        buttonBar.Resize += (s, e) =>
        {
            int total = btnPrint.Width + btnSelectLogo.Width + btnPrint.Margin.Horizontal + btnSelectLogo.Margin.Horizontal;
            int side = Math.Max(15, (buttonBar.ClientSize.Width - total) / 2);
            buttonBar.Padding = new Padding(side, 10, side, 10);
        };

        // پیش‌نمایش داخل یک قاب با حاشیه‌ی هم‌رنگ پس‌زمینه‌ی برنامه
        var previewHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 8, 14, 6),
            BackColor = ModernTheme.Background
        };
        previewHost.Controls.Add(previewControl);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            BackColor = ModernTheme.Background
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        mainLayout.Controls.Add(previewHost, 0, 0);
        mainLayout.Controls.Add(buttonBar, 0, 1);
        Controls.Add(mainLayout);
    }

    private void BtnSelectLogo_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp",
            Title = "لطفاً فایل لوگوی شرکت را انتخاب کنید"
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var stream = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read);
            using var img = Image.FromStream(stream);

            var oldImage = _logoImage;
            _logoImage = new Bitmap(img);
            oldImage?.Dispose();

            _customLogoPath = ofd.FileName;
            SaveLogoPath(_customLogoPath);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "PaymentOrderForm.BtnSelectLogo_Click");
            ModernMessageBox.ShowError("فایل انتخاب‌شده به‌عنوان تصویر قابل خواندن نیست.");
            return;
        }

        previewControl.InvalidatePreview();
        ToastHelper.Show("لوگو با موفقیت ذخیره شد.", true);
    }

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        using var printDialog = new PrintDialog { Document = previewControl.Document };
        if (printDialog.ShowDialog(this) == DialogResult.OK) previewControl.Document!.Print();
    }

    private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        Graphics g = e.Graphics!;
        g.PageUnit = GraphicsUnit.Millimeter;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        string fontName = "B Nazanin";

        using Font fontTitle = new Font(fontName, 16, FontStyle.Bold);
        using Font fontSubtitle = new Font(fontName, 14, FontStyle.Bold);
        using Font fontHeader = new Font(fontName, 12, FontStyle.Bold);
        using Font fontNormal = new Font(fontName, 12, FontStyle.Regular);
        using Font fontSmall = new Font(fontName, 10, FontStyle.Regular);

        using Pen pen = new Pen(Color.Black, 0.3f);
        using Pen penThick = new Pen(Color.Black, 0.6f);

        using StringFormat sfRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };
        using StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

        Brush brush = Brushes.Black;
        Brush bgGray = Brushes.LightGray;

        float startX = 10;
        float currentY = 10;
        float pageWidth = 190;

        float headerHeight = 22;
        RectangleF headerRect = new RectangleF(startX, currentY, pageWidth, headerHeight);
        g.FillRectangle(Brushes.White, headerRect);
        g.DrawRectangle(penThick, startX, currentY, pageWidth, headerHeight);

        if (_logoImage is not null)
        {
            try { g.DrawImage(_logoImage, startX + 2, currentY + 2, 18, 18); }
            catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PaymentOrderForm"); }
        }

        g.DrawString(_companyName, fontTitle, brush, new RectangleF(startX + 20, currentY, pageWidth - 25, headerHeight), sfCenter);
        currentY += headerHeight;

        g.DrawRectangle(pen, startX, currentY, pageWidth, 12);
        g.DrawString("فرم دستور پرداخت خزانه داری", fontSubtitle, brush, new RectangleF(startX, currentY, pageWidth, 12), sfCenter);
        currentY += 12;

        float rowHeight = 10;
        float wRow = 10, wDesc = 70, wCheckNo = 40, wDate = 30, wAmount = 40;

        float xRow = startX + pageWidth - wRow;
        float xDesc = xRow - wDesc;
        float xCheckNo = xDesc - wCheckNo;
        float xDate = xCheckNo - wDate;
        float xAmount = startX;

        g.FillRectangle(bgGray, startX, currentY, pageWidth, rowHeight);
        g.DrawRectangle(pen, startX, currentY, pageWidth, rowHeight);

        g.DrawLine(pen, xRow, currentY, xRow, currentY + rowHeight);
        g.DrawLine(pen, xDesc, currentY, xDesc, currentY + rowHeight);
        g.DrawLine(pen, xCheckNo, currentY, xCheckNo, currentY + rowHeight);
        g.DrawLine(pen, xDate, currentY, xDate, currentY + rowHeight);

        g.DrawString("ردیف", fontHeader, brush, new RectangleF(xRow, currentY, wRow, rowHeight), sfCenter);
        g.DrawString("شرح", fontHeader, brush, new RectangleF(xDesc, currentY, wDesc, rowHeight), sfCenter);
        g.DrawString("شماره/مرجع", fontHeader, brush, new RectangleF(xCheckNo, currentY, wCheckNo, rowHeight), sfCenter);
        g.DrawString("تاریخ", fontHeader, brush, new RectangleF(xDate, currentY, wDate, rowHeight), sfCenter);
        g.DrawString("مبلغ - ریال", fontHeader, brush, new RectangleF(xAmount, currentY, wAmount, rowHeight), sfCenter);

        currentY += rowHeight;

        int rowsDrawn = 0;
        while (_printIndex < _items.Count && rowsDrawn < MaxRowsPerPage)
        {
            var item = _items[_printIndex];

            g.DrawRectangle(pen, startX, currentY, pageWidth, rowHeight);
            g.DrawLine(pen, xRow, currentY, xRow, currentY + rowHeight);
            g.DrawLine(pen, xDesc, currentY, xDesc, currentY + rowHeight);
            g.DrawLine(pen, xCheckNo, currentY, xCheckNo, currentY + rowHeight);
            g.DrawLine(pen, xDate, currentY, xDate, currentY + rowHeight);

            g.DrawString((_printIndex + 1).ToString(), fontNormal, brush, new RectangleF(xRow, currentY, wRow, rowHeight), sfCenter);
            g.DrawString(item.Description, fontNormal, brush, new RectangleF(xDesc, currentY, wDesc, rowHeight), sfRight);
            g.DrawString(item.ReferenceNumber, fontNormal, brush, new RectangleF(xCheckNo, currentY, wCheckNo, rowHeight), sfCenter);
            g.DrawString(DateHelper.ToPersianDate(item.Date), fontNormal, brush, new RectangleF(xDate, currentY, wDate, rowHeight), sfCenter);
            g.DrawString(item.Amount.ToString("N0"), fontNormal, brush, new RectangleF(xAmount, currentY, wAmount, rowHeight), sfCenter);

            _printIndex++;
            rowsDrawn++;
            currentY += rowHeight;
        }

        if (_printIndex < _items.Count)
        {
            g.DrawString("ادامه در صفحه بعد...", fontNormal, brush, new RectangleF(startX, currentY + 5, pageWidth, 10), sfCenter);
            e.HasMorePages = true;
            return;
        }

        decimal totalAmount = _items.Sum(x => x.Amount);

        g.DrawRectangle(pen, startX, currentY, pageWidth, rowHeight);
        g.DrawLine(pen, xAmount + wAmount, currentY, xAmount + wAmount, currentY + rowHeight);

        string totalInWords = NumberHelper.ToPersianWords(totalAmount) + " ریال";
        g.DrawString($"خالص قابل پرداخت (به حروف): {totalInWords}", fontSmall, brush, new RectangleF(xAmount + wAmount, currentY, pageWidth - wAmount, rowHeight), sfCenter);
        g.DrawString(totalAmount.ToString("N0"), fontHeader, brush, new RectangleF(xAmount, currentY, wAmount, rowHeight), sfCenter);

        currentY += rowHeight;

        g.DrawRectangle(pen, startX, currentY, pageWidth, 20);
        g.DrawLine(pen, startX + pageWidth / 2, currentY, startX + pageWidth / 2, currentY + 20);
        g.DrawString("صادر کننده\nامور مالی", fontNormal, brush, new RectangleF(startX + pageWidth / 2, currentY, pageWidth / 2, 20), sfCenter);
        g.DrawString("تائید کننده\nمدیر مالی", fontNormal, brush, new RectangleF(startX, currentY, pageWidth / 2, 20), sfCenter);
        currentY += 20;

        float approveHeight = 25;
        g.DrawRectangle(pen, startX, currentY, pageWidth, approveHeight);
        string approveText = $"امور مالی: با پرداخت مبلغ {totalAmount.ToString("N0")} ریال به صورت لیست فوق موافقت میشود.";
        g.DrawString(approveText, fontNormal, brush, new RectangleF(startX + 5, currentY, pageWidth - 10, approveHeight), sfRight);
        g.DrawString("مدیر عامل", fontNormal, brush, new RectangleF(startX + 10, currentY, 30, approveHeight), sfCenter);
        currentY += approveHeight;

        float bankRowHeight = 10;
        float bwBank = 35, bwAcc = 45, bwPayee = 25, bwCheckNo = 20, bwDate = 25, bwAmount = 40;

        float bxBank = startX + pageWidth - bwBank;
        float bxAcc = bxBank - bwAcc;
        float bxPayee = bxAcc - bwPayee;
        float bxCheckNo = bxPayee - bwCheckNo;
        float bxDate = bxCheckNo - bwDate;
        float bxAmount = startX;

        g.FillRectangle(bgGray, startX, currentY, pageWidth, bankRowHeight);
        g.DrawRectangle(pen, startX, currentY, pageWidth, bankRowHeight);
        g.DrawLine(pen, bxBank, currentY, bxBank, currentY + bankRowHeight);
        g.DrawLine(pen, bxAcc, currentY, bxAcc, currentY + bankRowHeight);
        g.DrawLine(pen, bxPayee, currentY, bxPayee, currentY + bankRowHeight);
        g.DrawLine(pen, bxCheckNo, currentY, bxCheckNo, currentY + bankRowHeight);
        g.DrawLine(pen, bxDate, currentY, bxDate, currentY + bankRowHeight);

        g.DrawString("نام بانک و شعبه", fontHeader, brush, new RectangleF(bxBank, currentY, bwBank, bankRowHeight), sfCenter);
        g.DrawString("شماره حساب / نام", fontHeader, brush, new RectangleF(bxAcc, currentY, bwAcc, bankRowHeight), sfCenter);
        g.DrawString("در وجه", fontHeader, brush, new RectangleF(bxPayee, currentY, bwPayee, bankRowHeight), sfCenter);
        g.DrawString("شماره", fontHeader, brush, new RectangleF(bxCheckNo, currentY, bwCheckNo, bankRowHeight), sfCenter);
        g.DrawString("سررسید", fontHeader, brush, new RectangleF(bxDate, currentY, bwDate, bankRowHeight), sfCenter);
        g.DrawString("مبلغ - ریال", fontHeader, brush, new RectangleF(bxAmount, currentY, bwAmount, bankRowHeight), sfCenter);

        currentY += bankRowHeight;

        float dataRowHeight = 20;
        g.DrawRectangle(pen, startX, currentY, pageWidth, dataRowHeight);
        g.DrawLine(pen, bxBank, currentY, bxBank, currentY + dataRowHeight);
        g.DrawLine(pen, bxAcc, currentY, bxAcc, currentY + dataRowHeight);
        g.DrawLine(pen, bxPayee, currentY, bxPayee, currentY + dataRowHeight);
        g.DrawLine(pen, bxCheckNo, currentY, bxCheckNo, currentY + dataRowHeight);
        g.DrawLine(pen, bxDate, currentY, bxDate, currentY + dataRowHeight);

        var firstItem = _items.FirstOrDefault();
        g.DrawString(_account.BankName, fontNormal, brush, new RectangleF(bxBank, currentY, bwBank, dataRowHeight), sfCenter);
        g.DrawString(_account.AccountNumber + "\n" + _account.Title, fontNormal, brush, new RectangleF(bxAcc, currentY, bwAcc, dataRowHeight), sfCenter);
        g.DrawString(firstItem?.RecipientName ?? "", fontNormal, brush, new RectangleF(bxPayee, currentY, bwPayee, dataRowHeight), sfCenter);
        g.DrawString(firstItem?.ReferenceNumber ?? "", fontNormal, brush, new RectangleF(bxCheckNo, currentY, bwCheckNo, dataRowHeight), sfCenter);
        g.DrawString(firstItem is not null ? DateHelper.ToPersianDate(firstItem.Date) : "", fontNormal, brush, new RectangleF(bxDate, currentY, bwDate, dataRowHeight), sfCenter);
        g.DrawString(totalAmount.ToString("N0"), fontNormal, brush, new RectangleF(bxAmount, currentY, bwAmount, dataRowHeight), sfCenter);

        currentY += dataRowHeight;

        float textRowHeight = 10;
        g.DrawRectangle(pen, startX, currentY, pageWidth, textRowHeight);
        string footerText = "در تاریخ ......................... لیست فوق به مبلغ ......................... ریال تحویل اینجانب ......................... گردید.";
        g.DrawString(footerText, fontSmall, brush, new RectangleF(startX + 2, currentY, pageWidth - 4, textRowHeight), sfRight);
        currentY += textRowHeight;

        currentY += 5;
        float signBoxHeight = 35;
        g.DrawRectangle(pen, startX, currentY, pageWidth, signBoxHeight);
        g.DrawLine(pen, startX + pageWidth / 2, currentY, startX + pageWidth / 2, currentY + signBoxHeight);
        g.DrawString("نام و امضاء تحویل دهنده:", fontHeader, brush, new RectangleF(startX + pageWidth / 2, currentY + 3, pageWidth / 2, 10), sfCenter);
        g.DrawString("مهر و امضاء دریافت کننده:", fontHeader, brush, new RectangleF(startX, currentY + 3, pageWidth / 2, 10), sfCenter);
    }
}
