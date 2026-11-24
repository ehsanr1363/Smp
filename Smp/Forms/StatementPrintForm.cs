using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;

namespace Smp.Forms;

public class StatementPrintForm : Form
{
    private readonly string _connectionString;
    private readonly object _statement; // ClientStatement یا ContractorStatement
    private readonly object _contract;  // ClientContract یا ContractorContract
    private readonly bool _isClient;

    private PrintDocument printDoc = null!;
    private PrintPreviewControl previewControl = null!;
    private Image? _companyLogo = null;
    private Company? _companyInfo = null;

    private List<dynamic> _allStatements = new();
    private string _contractNumber = "";
    private string _contractSubject = "";
    private string _partyName = "";
    private DateTime? _contractDate;
    private DateTime? _endDate;
    private decimal _totalContractAmount = 0;
    private decimal _totalAdvance = 0;
    private decimal _retentionPercent = 0;
    private decimal _vatPercent = 0;
    private decimal _insurancePercent = 0;

    private int _printRowIndex = 0;
    private decimal _cumulativeAmount = 0;

    public StatementPrintForm(string connectionString, object statement, object contract, bool isClient)
    {
        _connectionString = connectionString;
        _statement = statement;
        _contract = contract;
        _isClient = isClient;

        ExtractData();
        LoadCompanyInfo();

        BuildUI();
        string title = _isClient ? "چاپ لیست صورت وضعیت‌های کارفرما" : "چاپ لیست صورت وضعیت‌های پیمانکار";
        this.ApplyStandardHeader(title, new Size(1100, 800), isDialog: false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { printDoc?.Dispose(); } catch (Exception __ex) { LogHelper.LogException(__ex, "StatementPrintForm"); }
            try { _companyLogo?.Dispose(); } catch (Exception __ex) { LogHelper.LogException(__ex, "StatementPrintForm"); }
        }
        base.Dispose(disposing);
    }

    private void ExtractData()
    {
        if (_isClient)
        {
            var con = (ClientContract)_contract;
            _contractNumber = con.ContractNumber;
            _contractSubject = con.Subject;
            _partyName = con.DisplayName;
            _contractDate = con.ContractDate;
            _endDate = con.EndDate;
            _totalContractAmount = con.Amount;
            _totalAdvance = con.TotalAdvanceAmount;
            _retentionPercent = con.RetentionPercentage;
            _vatPercent = con.VatPercentage;
            _insurancePercent = con.InsurancePercentage;

            var repo = new ClientStatementRepository(_connectionString);
            _allStatements = repo.GetByContractId(con.Id).Cast<dynamic>().ToList();
        }
        else
        {
            var con = (ContractorContract)_contract;
            _contractNumber = con.ContractNumber;
            _contractSubject = con.Subject;
            _partyName = con.DisplayName;
            _contractDate = con.ContractDate;
            _endDate = con.EndDate;
            _totalContractAmount = con.Amount;
            _totalAdvance = con.TotalAdvanceAmount;
            _retentionPercent = con.RetentionPercentage;
            _vatPercent = con.VatPercentage;
            _insurancePercent = con.InsurancePercentage;

            var repo = new ContractorStatementRepository(_connectionString);
            _allStatements = repo.GetByContractId(con.Id).Cast<dynamic>().ToList();
        }
    }

    private void LoadCompanyInfo()
    {
        try
        {
            var repo = new CompanyRepository(_connectionString);
            _companyInfo = repo.GetAll().FirstOrDefault();
            if (_companyInfo is not null && _companyInfo.Logo is not null && _companyInfo.Logo.Length > 0)
            {
                _companyLogo = ImageHelper.ByteArrayToImage(_companyInfo.Logo);
            }
        }
        catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "StatementPrintForm"); }
    }

    private void BuildUI()
    {
        printDoc = new PrintDocument();
        printDoc.DefaultPageSettings.Landscape = true; // ✅ حالت افقی
        printDoc.DefaultPageSettings.Margins = new Margins(30, 30, 30, 30);
        printDoc.BeginPrint += (s, e) => { _printRowIndex = 0; _cumulativeAmount = 0; };
        printDoc.PrintPage += PrintDoc_PrintPage;

        previewControl = new PrintPreviewControl
        {
            Dock = DockStyle.Fill,
            Document = printDoc,
            Zoom = 0.7,
            UseAntiAlias = true,
            BackColor = ModernTheme.SurfaceMuted
        };

        var btnPrint = ModernButton.Success("چاپ لیست", 180, 40);
        btnPrint.Click += (s, e) =>
        {
            using var printDialog = new PrintDialog { Document = printDoc };
            if (printDialog.ShowDialog(this) == DialogResult.OK) printDoc.Print();
        };

        var btnCancel = ModernButton.Cancel("انصراف", 110, 40);
        btnCancel.Click += (s, e) => Close();

        var buttonBar = ModernForm.CreateButtonBar(btnPrint, btnCancel);

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

    private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        Graphics g = e.Graphics!;
        float pageWidth = e.MarginBounds.Width;
        float pageHeight = e.MarginBounds.Height;
        float startX = e.MarginBounds.Left;
        float yPos = e.MarginBounds.Top;

        g.SmoothingMode = SmoothingMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        var fontTitle = ModernTheme.GetFont(16, FontStyle.Bold);
        var fontHeader = ModernTheme.GetFont(9f, FontStyle.Bold);
        var fontBody = ModernTheme.GetFont(9f);
        var fontSmall = ModernTheme.GetFont(8f);

        using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
        using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
        using StringFormat rtlFormatLeft = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
        using StringFormat titleFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

        if (_printRowIndex == 0)
        {
            // ═══════ ۱. هدر بالای صفحه (لوگو و عناوین) ═══════
            if (_companyLogo is not null)
            {
                try { g.DrawImage(_companyLogo, pageWidth - 60, yPos, 60, 60); }
                catch (ArgumentException) { }
            }

            string docTitle = _isClient ? "گزارش صورت وضعیت کارفرما" : "گزارش صورت وضعیت پیمانکار";
            g.DrawString(docTitle, fontTitle, Brushes.Black, new RectangleF(startX, yPos + 10, pageWidth, 30), titleFormat);
            string partyTitle = _isClient ? $"کارفرما: {_partyName}" : $"پیمانکار: {_partyName}";
            g.DrawString(partyTitle, fontHeader, Brushes.Black, new RectangleF(startX, yPos + 40, pageWidth, 25), titleFormat);
            yPos += 75;

            // ═══════ ۲. اطلاعات قرارداد (سه ستون در بالا) ═══════
            float colWidth = pageWidth / 3;
            float rightCol = startX + (colWidth * 2);
            float centerCol = startX + colWidth;
            float leftCol = startX;

            // ستون راست
            g.DrawString($"شماره قرارداد: {_contractNumber}", fontBody, Brushes.Black, new RectangleF(rightCol, yPos, colWidth, 20), rtlFormatRight);
            g.DrawString($"تاریخ قرارداد: {DateHelper.ToPersianDate(_contractDate)}", fontBody, Brushes.Black, new RectangleF(rightCol, yPos + 20, colWidth, 20), rtlFormatRight);
            g.DrawString($"تاریخ پایان: {DateHelper.ToPersianDate(_endDate)}", fontBody, Brushes.Black, new RectangleF(rightCol, yPos + 40, colWidth, 20), rtlFormatRight);

            // ستون وسط
            g.DrawString($"مبلغ کل: {NumberHelper.FormatThousands(_totalContractAmount, usePersianDigits: true)} ریال", fontBody, Brushes.Black, new RectangleF(centerCol, yPos, colWidth, 20), rtlFormatCenter);
            g.DrawString($"پیش پرداخت: {NumberHelper.FormatThousands(_totalAdvance, usePersianDigits: true)} ریال", fontBody, Brushes.Black, new RectangleF(centerCol, yPos + 20, colWidth, 20), rtlFormatCenter);
            g.DrawString($"موضوع: {_contractSubject}", fontBody, Brushes.Black, new RectangleF(centerCol, yPos + 40, colWidth, 20), rtlFormatCenter);

            // ستون چپ
            g.DrawString($"حسن انجام کار: {StringHelper.ConvertToPersianDigits(_retentionPercent.ToString("0.#"))}%", fontBody, Brushes.Black, new RectangleF(leftCol, yPos, colWidth, 20), rtlFormatLeft);
            g.DrawString($"ارزش افزوده: {StringHelper.ConvertToPersianDigits(_vatPercent.ToString("0.#"))}%", fontBody, Brushes.Black, new RectangleF(leftCol, yPos + 20, colWidth, 20), rtlFormatLeft);
            g.DrawString($"بیمه: {StringHelper.ConvertToPersianDigits(_insurancePercent.ToString("0.#"))}%", fontBody, Brushes.Black, new RectangleF(leftCol, yPos + 40, colWidth, 20), rtlFormatLeft);

            yPos += 70;

            // ═══════ ۳. هدر جدول ═══════
            string[] headers = { "شماره", "مبلغ انباشته", "مبلغ جاری", "ارزش افزوده", "جمع کل", "حسن انجام کار", "بیمه", "سایر", "پیش پرداخت", "جمع کسورات", "خالص" };
            float[] proportions = { 4, 10, 9, 8, 10, 10, 8, 6, 10, 10, 10 };
            float totalProp = proportions.Sum();
            float[] colWidths = proportions.Select(p => (p / totalProp) * pageWidth).ToArray();

            float rowHeight = 25f;
            float headerRowHeight = 30f;

            float currentX = startX + pageWidth;
            g.FillRectangle(Brushes.LightGray, startX, yPos, pageWidth, headerRowHeight);
            g.DrawRectangle(Pens.Black, startX, yPos, pageWidth, headerRowHeight);

            for (int i = 0; i < headers.Length; i++)
            {
                if (i > 0) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + headerRowHeight);
                currentX -= colWidths[i];
                g.DrawString(headers[i], fontHeader, Brushes.Black, new RectangleF(currentX, yPos, colWidths[i], headerRowHeight), rtlFormatCenter);
            }
            yPos += headerRowHeight;

            // ═══════ ۴. ردیف‌های جدول ═══════
            decimal sumGross = 0, sumVat = 0, sumTotal = 0, sumRet = 0, sumIns = 0, sumOther = 0, sumAdv = 0, sumDed = 0, sumNet = 0;

            while (_printRowIndex < _allStatements.Count)
            {
                var st = _allStatements[_printRowIndex];

                decimal gross = (decimal)st.GrossAmount;
                decimal vat = (decimal)st.VAT;
                decimal total = gross + vat;
                decimal ret = (decimal)st.RetentionDeduction;
                decimal ins = (decimal)st.InsuranceDeduction;
                decimal other = (decimal)st.OtherDeductions;
                decimal adv = (decimal)st.AdvanceRecovery;
                decimal ded = ret + ins + other + adv;

                // ✅ خالص برای کارفرما و پیمانکار متفاوت است
                decimal net = _isClient ? (decimal)st.NetReceivableAmount : (decimal)st.NetPayableAmount;

                _cumulativeAmount += gross;
                sumGross += gross; sumVat += vat; sumTotal += total; sumRet += ret; sumIns += ins;
                sumOther += other; sumAdv += adv; sumDed += ded; sumNet += net;

                currentX = startX + pageWidth;
                g.DrawRectangle(Pens.Black, startX, yPos, pageWidth, rowHeight);

                string[] rowData = {
                    _printRowIndex + 1 == _allStatements.Count ? _printRowIndex + 1 + " (جاری)" : (_printRowIndex + 1).ToString(),
                    NumberHelper.FormatThousands(_cumulativeAmount, usePersianDigits: true),
                    NumberHelper.FormatThousands(gross, usePersianDigits: true),
                    NumberHelper.FormatThousands(vat, usePersianDigits: true),
                    NumberHelper.FormatThousands(total, usePersianDigits: true),
                    NumberHelper.FormatThousands(ret, usePersianDigits: true),
                    NumberHelper.FormatThousands(ins, usePersianDigits: true),
                    NumberHelper.FormatThousands(other, usePersianDigits: true),
                    NumberHelper.FormatThousands(adv, usePersianDigits: true),
                    NumberHelper.FormatThousands(ded, usePersianDigits: true),
                    NumberHelper.FormatThousands(net, usePersianDigits: true)
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    if (i > 0) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + rowHeight);
                    currentX -= colWidths[i];
                    g.DrawString(rowData[i], fontBody, Brushes.Black, new RectangleF(currentX, yPos, colWidths[i], rowHeight), rtlFormatCenter);
                }

                yPos += rowHeight;
                _printRowIndex++;
            }

            // ═══════ ۵. ردیف جمع کل (رنگ بژ) ═══════
            if (_printRowIndex >= _allStatements.Count)
            {
                g.FillRectangle(Brushes.Beige, startX, yPos, pageWidth, rowHeight);
                g.DrawRectangle(Pens.Black, startX, yPos, pageWidth, rowHeight);

                currentX = startX + pageWidth;
                string[] totalValues = {
                    "جمع کل",
                    NumberHelper.FormatThousands(_cumulativeAmount, usePersianDigits: true),
                    NumberHelper.FormatThousands(sumGross, usePersianDigits: true),
                    NumberHelper.FormatThousands(sumVat, usePersianDigits: true),
                    NumberHelper.FormatThousands(sumTotal, usePersianDigits: true),
                    NumberHelper.FormatThousands(sumRet, usePersianDigits: true),
                    NumberHelper.FormatThousands(sumIns, usePersianDigits: true),
                    NumberHelper.FormatThousands(sumOther, usePersianDigits: true),
                    NumberHelper.FormatThousands(sumAdv, usePersianDigits: true),
                    NumberHelper.FormatThousands(sumDed, usePersianDigits: true),
                    NumberHelper.FormatThousands(sumNet, usePersianDigits: true)
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    if (i > 0) g.DrawLine(Pens.Black, currentX, yPos, currentX, yPos + rowHeight);
                    currentX -= colWidths[i];
                    g.DrawString(totalValues[i], fontHeader, Brushes.Black, new RectangleF(currentX, yPos, colWidths[i], rowHeight), rtlFormatCenter);
                }
                yPos += rowHeight + 40;

                // ═══════ ۶. بخش امضاها ═══════
                float signWidth = 150f;
                float signY = pageHeight - 60;

                g.DrawString("مهر و امضای کارفرما", fontHeader, Brushes.Black, new RectangleF(pageWidth - signWidth, signY, signWidth, 20), rtlFormatCenter);
                g.DrawLine(Pens.Black, pageWidth - signWidth, signY + 40, pageWidth, signY + 40);

                g.DrawString("مهر و امضای ناظر", fontHeader, Brushes.Black, new RectangleF(pageWidth / 2 - signWidth / 2, signY, signWidth, 20), rtlFormatCenter);
                g.DrawLine(Pens.Black, pageWidth / 2 - signWidth / 2, signY + 40, pageWidth / 2 + signWidth / 2, signY + 40);

                g.DrawString("مهر و امضای پیمانکار", fontHeader, Brushes.Black, new RectangleF(startX, signY, signWidth, 20), rtlFormatCenter);
                g.DrawLine(Pens.Black, startX, signY + 40, startX + signWidth, signY + 40);
            }
        }
        e.HasMorePages = false;
    }
}
