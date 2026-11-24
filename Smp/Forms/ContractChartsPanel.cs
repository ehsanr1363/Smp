using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Smp.Models;
using Smp.Helpers;

namespace Smp.Forms;

public class ContractChartsPanel : Panel
{
    private decimal _totalAmount = 0;
    private decimal _totalWorkDone = 0;
    private decimal _totalPaid = 0;
    private decimal _totalDebt = 0;

    public ContractChartsPanel()
    {
        this.DoubleBuffered = true; // جلوگیری از پرش تصویر (Flicker)
        this.BackColor = Color.White;
    }

    public void UpdateData(List<ContractSummary> summaries)
    {
        if (summaries is null) return;

        _totalAmount = summaries.Sum(x => x.TotalAmount);
        _totalWorkDone = summaries.Sum(x => x.TotalWorkDone);
        _totalPaid = summaries.Sum(x => x.TotalPaid);
        _totalDebt = summaries.Sum(x => x.RemainingDebt);

        this.Invalidate(); // رسم مجدد
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_totalAmount <= 0) return;

        // بررسی راست‌چین بودن پنل
        bool isRtl = (this.RightToLeft == RightToLeft.Yes) || (this.Parent?.RightToLeft == RightToLeft.Yes);

        // ابعاد کلی
        int w = this.Width;
        int h = this.Height;

        // محاسبه ایمن ابعاد نمودار
        int pieSize = Math.Min(w / 2 - 40, h - 80);
        if (pieSize < 50) pieSize = 50;

        // موقعیت نمودار دایره‌ای (در RTL سمت راست، در LTR سمت چپ)
        int pieX = isRtl ? w - pieSize - 40 : 40;
        Rectangle pieRect = new Rectangle(pieX, 40, pieSize, pieSize);

        // --- محاسبات زوایا با در نظر گرفتن امنیت ---
        decimal remainingWork = _totalAmount - _totalWorkDone;
        if (remainingWork < 0) remainingWork = 0;

        decimal paidAmount = _totalPaid;
        decimal unpaidAmount = _totalWorkDone - _totalPaid;
        if (unpaidAmount < 0) unpaidAmount = 0;

        float anglePaid = (float)(paidAmount / _totalAmount) * 360f;
        float angleUnpaid = (float)(unpaidAmount / _totalAmount) * 360f;
        float angleRemaining = (float)(remainingWork / _totalAmount) * 360f;

        if (anglePaid + angleUnpaid + angleRemaining > 360f)
            angleRemaining = 360f - (anglePaid + angleUnpaid);

        // --- ۱. رسم نمودار دایره‌ای (Pie Chart) ---
        if (anglePaid > 0)
        {
            using var brushPaid = new SolidBrush(ModernTheme.Success);
            g.FillPie(brushPaid, pieRect, 0, anglePaid);
        }

        if (angleUnpaid > 0)
        {
            using var brushUnpaid = new SolidBrush(ModernTheme.WarningText);
            g.FillPie(brushUnpaid, pieRect, anglePaid, angleUnpaid);
        }

        if (angleRemaining > 0)
        {
            using var brushRemaining = new SolidBrush(ModernTheme.Neutral);
            g.FillPie(brushRemaining, pieRect, anglePaid + angleUnpaid, angleRemaining);
        }

        // رسم خط دور دایره
        using (var pen = new Pen(Color.White, 2))
            g.DrawEllipse(pen, pieRect);

        // عنوان نمودار دایره‌ای
        // 👈 فونت‌های UIHelper نباید Dispose شوند (چون کش شده‌اند)
        var titleFont = ModernTheme.GetFont(12f, FontStyle.Bold);
        using (var titleFormat = new StringFormat { Alignment = StringAlignment.Center })
        using (var textBrush = new SolidBrush(ModernTheme.Text))
        {
            g.DrawString("وضعیت مالی کل پروژه‌ها", titleFont, textBrush, new RectangleF(pieX, 10, pieSize, 30), titleFormat);
        }

        // --- ۲. رسم راهنما (Legend) و آمار ---
        int legendX = isRtl ? 40 : pieRect.Right + 40;
        int legendY = 50;

        // 👈 فونت‌ها را بدون using تعریف می‌کنیم
        var legendFont = ModernTheme.GetFont(10f, FontStyle.Bold);
        var valueFont = ModernTheme.GetFont(10f);

        using (var titleBrush = new SolidBrush(ModernTheme.Text))
        using (var valueBrush = new SolidBrush(Color.Gray))
        {
            DrawLegendItem(g, "مبالغ پرداخت شده", paidAmount, ModernTheme.Success, ref legendY, legendX, isRtl, legendFont, valueFont, titleBrush, valueBrush);
            DrawLegendItem(g, "کارکرد پرداخت نشده (بدهی)", unpaidAmount, ModernTheme.WarningText, ref legendY, legendX, isRtl, legendFont, valueFont, titleBrush, valueBrush);
            DrawLegendItem(g, "مانده قرارداد (کار انجام نشده)", remainingWork, ModernTheme.Neutral, ref legendY, legendX, isRtl, legendFont, valueFont, titleBrush, valueBrush);
        }

        // --- ۳. رسم نوار پیشرفت کل (Progress Bar) ---
        int barY = pieRect.Bottom + 30;
        int barW = w - 80;
        int barH = 20;

        // رسم پس‌زمینه نوار پیشرفت
        using (var bgBrush = new SolidBrush(Color.LightGray))
            g.FillRectangle(bgBrush, 40, barY, barW, barH);

        float progressWidth = (float)(_totalWorkDone / _totalAmount) * barW;
        if (progressWidth > barW) progressWidth = barW;
        if (progressWidth < 0) progressWidth = 0;

        // رسم پیشرفت (در RTL از سمت راست پر می‌شود)
        if (progressWidth > 0)
        {
            using var progressBrush = new SolidBrush(ModernTheme.Primary);
            if (isRtl)
                g.FillRectangle(progressBrush, 40 + barW - progressWidth, barY, progressWidth, barH);
            else
                g.FillRectangle(progressBrush, 40, barY, progressWidth, barH);
        }

        // درصد پیشرفت
        double percent = (double)(_totalWorkDone / _totalAmount) * 100;

        // اگر مقدار بی‌نهایت یا نامعتبر بود، آن را صفر در نظر بگیر
        if (double.IsNaN(percent) || double.IsInfinity(percent)) percent = 0;

        string percentText = $"پیشرفت کل: {StringHelper.ConvertToPersianDigits(percent.ToString("N1"))}%";

        // 👈 استفاده از فونت بدون using
        var percentFont = ModernTheme.GetFont(10f, FontStyle.Bold);

        using (var textBrush = new SolidBrush(ModernTheme.Text))
        using (var format = new StringFormat { Alignment = StringAlignment.Center })
        {
            g.DrawString(percentText, percentFont, textBrush, new RectangleF(40, barY + barH + 5, barW, 20), format);
        }
    }

    private void DrawLegendItem(Graphics g, string title, decimal value, Color color,
                                ref int y, int x, bool isRtl, Font titleFont, Font valueFont,
                                Brush titleBrush, Brush valueBrush)
    {
        using (var colorBrush = new SolidBrush(color))
        {
            g.FillRectangle(colorBrush, x, y, 20, 20);
        }

        // قالب‌بندی عدد به صورت سه‌رقمی و با ارقام فارسی
        string amountText = NumberHelper.FormatThousands(value, usePersianDigits: true) + " ریال";

        using (var format = new StringFormat())
        {
            if (isRtl)
            {
                format.Alignment = StringAlignment.Near;
                format.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                g.DrawString(title, titleFont, titleBrush, x - 10, y, format);
                g.DrawString(amountText, valueFont, valueBrush, x - 10, y + 20, format);
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
