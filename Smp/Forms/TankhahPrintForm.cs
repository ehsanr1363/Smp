using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;

namespace SMP.Forms
{
    public class TankhahPrintForm : Form
    {
        private readonly string _connectionString;
        private readonly CompanyBankAccount _tankhah;
        private readonly List<CashTransaction> _transactions;

        private PrintDocument _printDoc = null!;
        private PrintPreviewControl _previewControl = null!;
        private Image? _companyLogo = null;
        private Company? _companyInfo = null;

        private decimal _totalBaseAmount, _totalDiscount, _totalShipping, _totalVAT, _totalSum;

        private Font _titleFont = null!;
        private Font _headerFont = null!;
        private Font _bodyFont = null!;
        private Font _smallFont = null!;
        private Font _totalFont = null!;

        private StringFormat _rtlCenter = null!;
        private StringFormat _rtlRight = null!;
        private StringFormat _centerFmt = null!;

        public TankhahPrintForm(string connectionString, CompanyBankAccount tankhah, List<CashTransaction> transactions)
        {
            _connectionString = connectionString;
            _tankhah = tankhah ?? throw new ArgumentNullException(nameof(tankhah));

            _transactions = (transactions ?? new List<CashTransaction>())
                .Where(t => t.Direction == DirectionTypes.Outgoing)
                .OrderBy(t => t.TransactionDate)
                .ThenBy(t => t.Id)
                .ToList();

            InitializeResources();
            LoadCompanyInfo();
            CalculateTotals();
            BuildUI();

            this.ApplyStandardHeader($"چاپ صورت ریز مخارج - {_tankhah.Title}", new Size(1150, 850));
            ShowInTaskbar = false;
            ShortcutHelper.ApplyStandardShortcuts(this);
        }

        private void InitializeResources()
        {
            _titleFont = GetPersianPrintFont(16, FontStyle.Bold);
            _headerFont = GetPersianPrintFont(10f, FontStyle.Bold);
            _bodyFont = GetPersianPrintFont(9.5f, FontStyle.Regular);
            _smallFont = GetPersianPrintFont(8.5f, FontStyle.Regular);
            _totalFont = GetPersianPrintFont(10f, FontStyle.Bold);

            _rtlCenter = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            };

            _rtlRight = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            };

            _centerFmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.DirectionRightToLeft
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _printDoc?.Dispose(); } catch { }
                try { _companyLogo?.Dispose(); } catch { }
                _titleFont?.Dispose(); _headerFont?.Dispose(); _bodyFont?.Dispose(); _smallFont?.Dispose(); _totalFont?.Dispose();
                _rtlCenter?.Dispose(); _rtlRight?.Dispose(); _centerFmt?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void LoadCompanyInfo()
        {
            try
            {
                var repo = new CompanyRepository(_connectionString);
                _companyInfo = repo.GetAll().FirstOrDefault();
                if (_companyInfo?.Logo != null && _companyInfo.Logo.Length > 0)
                    _companyLogo = ImageHelper.ByteArrayToImage(_companyInfo.Logo);
            }
            catch { }
        }

        private void CalculateTotals()
        {
            foreach (var t in _transactions)
            {
                _totalBaseAmount += t.BaseAmount;
                _totalDiscount += t.Discount;
                _totalShipping += t.Shipping;
                _totalVAT += t.VAT;
                _totalSum += t.Amount;
            }
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            _printDoc = new PrintDocument();
            _printDoc.DefaultPageSettings.Landscape = false;

            // 🔴 افزایش حاشیه راست به 50 پیکسل تا در پرینترهای حاشیه‌دار متن قطع نشود
            // ترتیب: چپ، راست، بالا، پایین
            _printDoc.DefaultPageSettings.Margins = new Margins(30, 50, 30, 30);

            _printDoc.PrintPage += PrintDoc_PrintPage;

            _previewControl = new PrintPreviewControl { Dock = DockStyle.Fill, Document = _printDoc, Zoom = 0.85, UseAntiAlias = true, BackColor = Color.FromArgb(240, 240, 240) };

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10) };
            var btnPrint = UIHelper.CreateButton("🖨️ چاپ گزارش", UIHelper.ActionGreen, 180, 45);
            btnPrint.Click += (s, e) => { using var pd = new PrintDialog { Document = _printDoc, UseEXDialog = true }; if (pd.ShowDialog(this) == DialogResult.OK) _printDoc.Print(); };
            var btnClose = UIHelper.CreateButton("بستن", UIHelper.SectionGray, 100, 45);
            btnClose.Click += (s, e) => Close();

            btnPanel.Controls.Add(btnPrint); btnPanel.Controls.Add(btnClose);
            mainLayout.Controls.Add(_previewControl, 0, 0); mainLayout.Controls.Add(btnPanel, 0, 1);
            Controls.Add(mainLayout);
        }

        private static Font GetPersianPrintFont(float size, FontStyle style = FontStyle.Regular)
        {
            try
            {
                foreach (var name in new[] { "B Nazanin", "Vazirmatn", "IRANSans", "Tahoma" })
                {
                    try { var f = new Font(name, size, style); if (f.Name == name) return f; f.Dispose(); } catch { }
                }
            }
            catch { }
            return new Font("Tahoma", size, style);
        }

        // 🔴 تابع جدید: کوچک کردن خودکار فونت اگر متن طولانی باشد
        private Font GetFittedFont(Graphics g, string text, Font originalFont, float maxWidth, StringFormat format)
        {
            if (string.IsNullOrEmpty(text)) return originalFont;

            SizeF textSize = g.MeasureString(text, originalFont, int.MaxValue, format);
            if (textSize.Width <= maxWidth) return originalFont;

            float currentSize = originalFont.Size - 0.5f;
            while (currentSize > 6f) // حداقل سایز فونت 6
            {
                Font testFont = new Font(originalFont.FontFamily, currentSize, originalFont.Style);
                textSize = g.MeasureString(text, testFont, int.MaxValue, format);
                if (textSize.Width <= maxWidth) return testFont;
                testFont.Dispose();
                currentSize -= 0.5f;
            }
            return new Font(originalFont.FontFamily, 6f, originalFont.Style);
        }

        private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            var g = e.Graphics!;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            var bounds = e.MarginBounds;
            float yPos = bounds.Top;

            // ═══════════ ۱. هدر صفحه ═══════════
            if (_companyLogo != null) try { g.DrawImage(_companyLogo, bounds.Right - 75, yPos - 5, 75, 75); } catch { }

            string companyName = (_companyInfo == null || string.IsNullOrWhiteSpace(_companyInfo.Name)) ? "شرکت" : _companyInfo.Name;
            RectangleF fullWidthRect = new RectangleF(bounds.X, yPos, bounds.Width, 25);

            g.DrawString(companyName, _titleFont, Brushes.Black, fullWidthRect, _centerFmt);
            fullWidthRect.Y += 30;
            g.DrawString("صورت ریز مخارج تنخواه", _titleFont, Brushes.Black, fullWidthRect, _centerFmt);
            fullWidthRect.Y += 30;
            g.DrawString(_tankhah.Title, _headerFont, Brushes.Black, fullWidthRect, _centerFmt);
            fullWidthRect.Y += 20;
            g.DrawString($"تاریخ چاپ: {DateHelper.ToPersianDate(DateTime.Now)}", _smallFont, Brushes.Gray, fullWidthRect, _centerFmt);

            yPos += 100;

            // ═══════════ ۲. محاسبه داینامیک ارتفاع سطرها ═══════════
            float bottomReservedSpace = 120f;
            float availableHeightForTable = bounds.Bottom - yPos - bottomReservedSpace;
            int totalRowsCount = _transactions.Count + 2;

            float rowHeight = availableHeightForTable / totalRowsCount;
            if (rowHeight > 32f) rowHeight = 32f;
            if (rowHeight < 20f) rowHeight = 20f;

            float headerRowHeight = rowHeight + 5;

            // ═══════════ ۳. رسم جدول ═══════════
            float[] proportions = { 5, 32, 13, 7, 7, 7, 14, 15 };
            float totalProp = proportions.Sum();
            float[] colWidths = proportions.Select(p => (p / totalProp) * bounds.Width).ToArray();
            float tableWidth = colWidths.Sum();

            string[] headers = { "ردیف", "شرح", "مبلغ (ریال)", "تخفیفات", "کرایه حمل", "ارزش افزوده", "جمع (ریال)", "تاریخ" };

            using (var bgBrush = new SolidBrush(Color.FromArgb(240, 235, 225)))
                g.FillRectangle(bgBrush, bounds.X, yPos, tableWidth, headerRowHeight);
            g.DrawRectangle(Pens.Black, bounds.X, yPos, tableWidth, headerRowHeight);

            float currentX = bounds.Right;
            for (int i = 0; i < headers.Length; i++)
            {
                currentX -= colWidths[i];
                if (i < headers.Length - 1) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + headerRowHeight);

                // هدرها هم در صورت نیاز فونتشان کوچک می‌شود
                Font cellFont = GetFittedFont(g, headers[i], _headerFont, colWidths[i] - 4, _rtlCenter);
                g.DrawString(headers[i], cellFont, Brushes.Black, new RectangleF(currentX + 2, yPos, colWidths[i] - 4, headerRowHeight), _rtlCenter);
                if (cellFont != _headerFont) cellFont.Dispose();
            }
            yPos += headerRowHeight;

            // رسم ردیف‌ها
            for (int r = 0; r < _transactions.Count; r++)
            {
                var trans = _transactions[r];

                string[] rowData = {
                    (r + 1).ToString(),
                    trans.Description ?? "",
                    NumberHelper.FormatThousands(trans.BaseAmount, usePersianDigits: true),
                    NumberHelper.FormatThousands(trans.Discount, usePersianDigits: true),
                    NumberHelper.FormatThousands(trans.Shipping, usePersianDigits: true),
                    NumberHelper.FormatThousands(trans.VAT, usePersianDigits: true),
                    NumberHelper.FormatThousands(trans.Amount, usePersianDigits: true),
                    DateHelper.ToPersianDate(trans.TransactionDate)
                };

                g.DrawRectangle(Pens.Black, bounds.X, yPos, tableWidth, rowHeight);
                currentX = bounds.Right;
                for (int i = 0; i < rowData.Length; i++)
                {
                    currentX -= colWidths[i];
                    if (i < rowData.Length - 1) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + rowHeight);

                    // 🔴 استفاده از تابع کوچک‌کننده فونت برای متن‌های طولانی
                    Font cellFont = GetFittedFont(g, rowData[i], _bodyFont, colWidths[i] - 6, _rtlCenter);
                    g.DrawString(rowData[i], cellFont, Brushes.Black, new RectangleF(currentX + 3, yPos, colWidths[i] - 6, rowHeight), _rtlCenter);

                    if (cellFont != _bodyFont) cellFont.Dispose(); // آزادسازی فونت ساخته شده
                }
                yPos += rowHeight;
            }

            // ═══════════ ۴. ردیف جمع کل ═══════════
            using (var bgBrush = new SolidBrush(Color.FromArgb(250, 245, 235)))
                g.FillRectangle(bgBrush, bounds.X, yPos, tableWidth, rowHeight);
            g.DrawRectangle(Pens.Black, bounds.X, yPos, tableWidth, rowHeight);

            string[] totalsRow = {
                "",
                "جمع کل",
                NumberHelper.FormatThousands(_totalBaseAmount, usePersianDigits: true),
                NumberHelper.FormatThousands(_totalDiscount, usePersianDigits: true),
                NumberHelper.FormatThousands(_totalShipping, usePersianDigits: true),
                NumberHelper.FormatThousands(_totalVAT, usePersianDigits: true),
                NumberHelper.FormatThousands(_totalSum, usePersianDigits: true),
                ""
            };

            currentX = bounds.Right;
            for (int i = 0; i < totalsRow.Length; i++)
            {
                currentX -= colWidths[i];
                if (i < totalsRow.Length - 1) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + rowHeight);

                Font cellFont = GetFittedFont(g, totalsRow[i], _totalFont, colWidths[i] - 4, _rtlCenter);
                g.DrawString(totalsRow[i], cellFont, Brushes.Black, new RectangleF(currentX + 2, yPos, colWidths[i] - 4, rowHeight), _rtlCenter);
                if (cellFont != _totalFont) cellFont.Dispose();
            }
            yPos += rowHeight + 15;

            // ═══════════ ۵. مبلغ به حروف و امضاها ═══════════
            string amountInWords = NumberHelper.ToPersianWords(_totalSum) + " ریال";
            g.DrawString($"مبلغ کل به حروف: {amountInWords}", _headerFont, Brushes.Black, new RectangleF(bounds.X, yPos, tableWidth, 25), _rtlRight);

            float sigY = bounds.Bottom - 70, sigColW = tableWidth / 3;
            string[] sigTitles = { "تهیه کننده", "بررسی کننده", "تائید کننده" };
            for (int i = 0; i < 3; i++)
            {
                float sigX = bounds.Right - (i + 1) * sigColW;
                g.DrawString(sigTitles[i], _headerFont, Brushes.Black, new RectangleF(sigX, sigY, sigColW, 25), _rtlCenter);
                g.DrawLine(Pens.Black, sigX + 20, sigY + 45, sigX + sigColW - 20, sigY + 45);
            }

            e.HasMorePages = false;
        }
    }
}