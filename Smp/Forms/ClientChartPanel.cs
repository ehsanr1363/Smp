using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Smp.Models;
using Smp.Helpers;

namespace Smp.Forms;

public class ClientChartPanel : Panel
{
    private decimal _contractAmount = 0;
    private decimal _contractorCosts = 0;
    private decimal _invoiceCosts = 0;
    private decimal _profit = 0;

    public ClientChartPanel()
    {
        this.DoubleBuffered = true; // جلوگیری از پرش (Flicker)
        this.BackColor = ModernTheme.Surface;
    }

    public void UpdateData(decimal contractAmount, decimal contractorCosts, decimal invoiceCosts)
    {
        _contractAmount = contractAmount;
        _contractorCosts = contractorCosts;
        _invoiceCosts = invoiceCosts;
        _profit = contractAmount - (contractorCosts + invoiceCosts);

        this.Invalidate(); // رسم مجدد
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_contractAmount <= 0)
        {
            // به‌جای بوم خالی، دلیل نبودن نمودار را بگوییم (همچنین جلوگیری از تقسیم بر صفر)
            System.Windows.Forms.TextRenderer.DrawText(g, "برای نمایش نمودار، ابتدا مبلغ قرارداد را ثبت کنید.",
                ModernTheme.GetFont(11f), ClientRectangle, ModernTheme.TextMuted,
                System.Windows.Forms.TextFormatFlags.HorizontalCenter | System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.RightToLeft);
            return;
        }

        int w = this.Width;
        int h = this.Height;

        // محاسبه ایمن اندازه نمودار
        int pieSize = Math.Min(w / 2 - 40, h - 80);
        if (pieSize < 50) pieSize = 50;

        // بررسی راست‌چین بودن پنل
        bool isRtl = (this.RightToLeft == RightToLeft.Yes) || (this.Parent?.RightToLeft == RightToLeft.Yes);

        // موقعیت نمودار: در حالت راست‌چین سمت راست، در غیر این صورت سمت چپ
        int pieX = isRtl ? w - pieSize - 40 : 40;
        Rectangle pieRect = new Rectangle(pieX, 40, pieSize, pieSize);

        // محاسبه زوایا
        float angleContractor = (float)(_contractorCosts / _contractAmount) * 360;
        float angleInvoice = (float)(_invoiceCosts / _contractAmount) * 360;
        float angleProfit = 360 - (angleContractor + angleInvoice);

        if (angleProfit < 0) angleProfit = 0; // حالت زیان

        // رسم دایره (Pie Chart) با استفاده از using برای جلوگیری از نشت حافظه
        if (angleContractor > 0)
            using (var brush = new SolidBrush(ModernTheme.Danger))
                g.FillPie(brush, pieRect, 0, angleContractor);

        if (angleInvoice > 0)
            using (var brush = new SolidBrush(ModernTheme.Orange))
                g.FillPie(brush, pieRect, angleContractor, angleInvoice);

        if (angleProfit > 0)
            using (var brush = new SolidBrush(ModernTheme.Success))
                g.FillPie(brush, pieRect, angleContractor + angleInvoice, angleProfit);

        using (var pen = new Pen(ModernTheme.Surface, 2))
            g.DrawEllipse(pen, pieRect);

        // عنوان
        var titleFont = ModernTheme.GetFont(12f, FontStyle.Bold);

        using (var titleFormat = new StringFormat { Alignment = StringAlignment.Center })
        using (var textBrush = new SolidBrush(ModernTheme.Text))
        {
            g.DrawString("تفکیک مالی پروژه", titleFont, textBrush, new RectangleF(pieX, 10, pieSize, 30), titleFormat);
        }

        // راهنما (Legend)
        // ✅ فاصله از سمت چپ را بیشتر کردیم تا متن جا شود
        int legendX = isRtl ? 200 : pieRect.Right + 40;
        int legendY = 50;

        var legendFont = ModernTheme.GetFont(10f, FontStyle.Bold);
        var valueFont = ModernTheme.GetFont(10f);

        using (var titleBrush = new SolidBrush(ModernTheme.Text))
        using (var valueBrush = new SolidBrush(ModernTheme.TextMuted))
        {
            DrawLegendItem(g, "هزینه پیمانکاران", _contractorCosts, ModernTheme.Danger, ref legendY, legendX, isRtl, legendFont, valueFont, titleBrush, valueBrush);
            DrawLegendItem(g, "هزینه‌های فاکتوری", _invoiceCosts, ModernTheme.Orange, ref legendY, legendX, isRtl, legendFont, valueFont, titleBrush, valueBrush);
            DrawLegendItem(g, "سود ناخالص", _profit, ModernTheme.Success, ref legendY, legendX, isRtl, legendFont, valueFont, titleBrush, valueBrush);
        }
    }

    // ✅ اصلاح متد رسم راهنما برای جلوگیری از بریده شدن متن
    private void DrawLegendItem(Graphics g, string title, decimal value, Color color,
                                ref int y, int x, bool isRtl, Font titleFont, Font valueFont,
                                Brush titleBrush, Brush valueBrush)
    {
        using (var colorBrush = new SolidBrush(color))
        {
            // رسم مربع رنگی
            g.FillRectangle(colorBrush, x, y, 20, 20);
        }

        // قالب‌بندی عدد به صورت سه‌رقمی و با ارقام فارسی
        string amountText = NumberHelper.FormatThousands(value, usePersianDigits: true) + " ریال";

        using (var format = new StringFormat())
        {
            if (isRtl)
            {
                // ✅ استفاده از مستطیل با عرض مشخص (150 پیکسل) در سمت چپِ مربع رنگی
                var titleRect = new RectangleF(x - 170, y, 160, 20);
                var valueRect = new RectangleF(x - 170, y + 20, 160, 20);

                format.Alignment = StringAlignment.Near; // راست‌چین بودن متن در کادر
                format.FormatFlags = StringFormatFlags.DirectionRightToLeft;

                g.DrawString(title, titleFont, titleBrush, titleRect, format);
                g.DrawString(amountText, valueFont, valueBrush, valueRect, format);
            }
            else
            {
                format.Alignment = StringAlignment.Near;
                g.DrawString(title, titleFont, titleBrush, x + 30, y, format);
                g.DrawString(amountText, valueFont, valueBrush, x + 30, y + 20, format);
            }
        }

        y += 60;
    }
}
