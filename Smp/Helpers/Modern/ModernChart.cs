using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;

namespace Smp.Helpers;

/// <summary>سازنده‌ی مشترک نمودارهای داشبورد (دونات، ستونی، خطی) با ظاهر یکدست</summary>
public static class ModernChart
{
    public static readonly Color[] Palette =
    {
        ModernTheme.Primary, ModernTheme.Success, ModernTheme.Warning, ModernTheme.Purple,
        ModernTheme.Teal, ModernTheme.Orange, ModernTheme.Danger, ModernTheme.Neutral
    };

    private static Chart CreateBase(bool legend, Docking legendDock = Docking.Bottom)
    {
        var chart = new Chart
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AntiAliasing = AntiAliasingStyles.All,
            TextAntiAliasingQuality = TextAntiAliasingQuality.High,
            RightToLeft = RightToLeft.Yes
        };
        if (legend)
            chart.Legends.Add(new Legend
            {
                Docking = legendDock,
                Alignment = StringAlignment.Center,
                Font = ModernTheme.GetFont(9f, FontStyle.Bold),
                ForeColor = ModernTheme.Text,
                BackColor = Color.Transparent
            });
        return chart;
    }

    private static void StyleAxes(ChartArea area, string? yFormat)
    {
        area.BackColor = Color.Transparent;
        foreach (var axis in new[] { area.AxisX, area.AxisY })
        {
            axis.LineColor = ModernTheme.Border;
            axis.MajorTickMark.LineColor = ModernTheme.Border;
            axis.LabelStyle.ForeColor = ModernTheme.TextMuted;
        }
        area.AxisX.MajorGrid.Enabled = false;
        area.AxisY.MajorGrid.LineColor = ModernTheme.GridLine;
        area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
        area.AxisX.LabelStyle.Font = ModernTheme.GetFont(9f, FontStyle.Bold);
        area.AxisY.LabelStyle.Font = ModernTheme.GetFont(8.5f);
        if (yFormat is not null) area.AxisY.LabelStyle.Format = yFormat;
    }

    // ───────────── دونات ─────────────
    public static Chart CreateDoughnut()
    {
        var chart = CreateBase(true);
        chart.ChartAreas.Add(new ChartArea { BackColor = Color.Transparent });
        var s = new Series("Data")
        {
            ChartType = SeriesChartType.Doughnut,
            IsValueShownAsLabel = true,
            BorderColor = Color.White,
            BorderWidth = 2
        };
        s["DoughnutRadius"] = "55";
        s["PieLabelStyle"] = "Inside";
        chart.Series.Add(s);
        return chart;
    }

    /// <summary>پر کردن دونات: درصد فارسی روی هر قطعه، «نام (تعداد)» در راهنما. داده‌ی خالی = پیام وسط نمودار.</summary>
    public static void FillDoughnut(Chart chart, IEnumerable<(string Name, double Value)> items)
    {
        if (chart is null || chart.IsDisposed) return;
        chart.SuspendLayout();
        try
        {
            var s = chart.Series[0];
            s.Points.Clear();
            chart.Annotations.Clear();

            var list = items.Where(i => i.Value > 0).ToList();
            double total = list.Sum(i => i.Value);
            if (total <= 0) { ShowEmpty(chart); return; }

            int idx = 0;
            foreach (var (name, value) in list)
            {
                int p = s.Points.AddXY(name, value);
                var pt = s.Points[p];
                pt.Color = Palette[idx++ % Palette.Length];
                double pct = value * 100.0 / total;
                pt.Label = pct >= 4 ? StringHelper.ConvertToPersianDigits(pct.ToString("0")) + "٪" : "";   // قطعه‌های ریز بدون برچسب
                pt.LabelForeColor = Color.White;
                pt.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
                pt.LegendText = $"{name} ({StringHelper.ConvertToPersianDigits(value.ToString("0"))})";
                pt.ToolTip = $"{name}: {StringHelper.ConvertToPersianDigits(value.ToString("0"))} ({StringHelper.ConvertToPersianDigits(pct.ToString("0.#"))}٪)";
            }
        }
        finally { chart.ResumeLayout(); }
    }

    // ───────────── ستونی / خطی ─────────────
    public static Chart CreateCartesian(string? yFormat = "#,#,, 'M'")
    {
        var chart = CreateBase(true, Docking.Top);
        var area = new ChartArea();
        StyleAxes(area, yFormat);
        chart.ChartAreas.Add(area);
        return chart;
    }

    public static Series AddSeries(Chart chart, string name, Color color, SeriesChartType type = SeriesChartType.Column)
    {
        var s = new Series(name) { ChartType = type, Color = color };
        if (type == SeriesChartType.Column || type == SeriesChartType.Bar)
        {
            s["PointWidth"] = "0.7";
            s["DrawingStyle"] = "Cylinder";
            s.BorderWidth = 0;
        }
        else
        {
            s.BorderWidth = 3;
            s.MarkerStyle = MarkerStyle.Circle;
            s.MarkerSize = 6;
        }
        chart.Series.Add(s);
        return s;
    }

    /// <summary>نمایش پیام «داده‌ای وجود ندارد» وسط نمودار</summary>
    public static void ShowEmpty(Chart chart, string text = "داده‌ای برای نمایش وجود ندارد")
    {
        chart.Annotations.Clear();
        chart.Annotations.Add(new TextAnnotation
        {
            Text = text,
            Font = ModernTheme.GetFont(10.5f),
            ForeColor = ModernTheme.TextMuted,
            X = 50, Y = 50,
            Alignment = ContentAlignment.MiddleCenter,
            AnchorX = 50, AnchorY = 50
        });
    }

    // ───────────── کمک‌ها ─────────────
    /// <summary>هر نمودار داخل یک کارت مدرن با عنوان و نوار رنگی</summary>
    public static ModernCard InCard(string title, Color accent, Control content)
    {
        var card = new ModernCard(title, accent, 1);
        card.Body.Controls.Add(content, 0, 0);
        return card;
    }
}
