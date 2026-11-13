using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using SMP.Models;
using SMP.Helpers;

namespace SMP.Forms
{
    public class ClientChartPanel : Panel
    {
        private decimal _contractAmount = 0;
        private decimal _contractorCosts = 0;
        private decimal _invoiceCosts = 0;
        private decimal _profit = 0;

        public ClientChartPanel()
        {
            this.DoubleBuffered = true; // جلوگیری از پرش (Flicker)
            this.BackColor = Color.White;
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

            if (_contractAmount <= 0) return; // جلوگیری از تقسیم بر صفر

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
                using (var brush = new SolidBrush(UIHelper.WarmDanger))
                    g.FillPie(brush, pieRect, 0, angleContractor);

            if (angleInvoice > 0)
                using (var brush = new SolidBrush(UIHelper.SectionOrange))
                    g.FillPie(brush, pieRect, angleContractor, angleInvoice);

            if (angleProfit > 0)
                using (var brush = new SolidBrush(UIHelper.ActionGreen))
                    g.FillPie(brush, pieRect, angleContractor + angleInvoice, angleProfit);

            using (var pen = new Pen(Color.White, 2))
                g.DrawEllipse(pen, pieRect);

            // عنوان
            var titleFont = UIHelper.PersianFont(12f, FontStyle.Bold);

            using (var titleFormat = new StringFormat { Alignment = StringAlignment.Center })
            using (var textBrush = new SolidBrush(UIHelper.WarmText))
            {
                g.DrawString("تفکیک مالی پروژه", titleFont, textBrush, new RectangleF(pieX, 10, pieSize, 30), titleFormat);
            }

            // راهنما (Legend)
            // ✅ فاصله از سمت چپ را بیشتر کردیم تا متن جا شود
            int legendX = isRtl ? 200 : pieRect.Right + 40;
            int legendY = 50;

            var legendFont = UIHelper.PersianFont(10f, FontStyle.Bold);
            var valueFont = UIHelper.PersianFont(10f);

            using (var titleBrush = new SolidBrush(UIHelper.WarmText))
            using (var valueBrush = new SolidBrush(Color.Gray))
            {
                DrawLegendItem(g, "هزینه پیمانکاران", _contractorCosts, UIHelper.WarmDanger, ref legendY, legendX, isRtl, legendFont, valueFont, titleBrush, valueBrush);
                DrawLegendItem(g, "هزینه‌های فاکتوری", _invoiceCosts, UIHelper.SectionOrange, ref legendY, legendX, isRtl, legendFont, valueFont, titleBrush, valueBrush);
                DrawLegendItem(g, "سود ناخالص", _profit, UIHelper.ActionGreen, ref legendY, legendX, isRtl, legendFont, valueFont, titleBrush, valueBrush);
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
}