using Smp.Models;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Helpers;

/// <summary>جدول مشترک «شارژهای تنخواه» برای فرم‌های تنخواه</summary>
public static class TankhahChargeGrid
{
    public static DataGridView Create(bool showListColumn)
    {
        var g = ModernGrid.Create("Id");
        g.AutoGenerateColumns = false;
        g.AllowUserToAddRows = false;
        g.ReadOnly = true;
        g.MultiSelect = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        g.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        g.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "تاریخ شارژ", FillWeight = 12 });
        if (showListColumn)
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = "List", HeaderText = "لیست", FillWeight = 12 });
        g.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bank", HeaderText = "از حساب بانکی", FillWeight = 24 });
        g.Columns.Add(new DataGridViewTextBoxColumn { Name = "Desc", HeaderText = "شرح", FillWeight = 34 });
        g.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "مبلغ شارژ (ریال)", FillWeight = 18 });
        foreach (var c in new[] { "Date", "List", "Amount" })
            if (g.Columns.Contains(c)) g.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        return g;
    }

    /// <summary>جدول را با شارژها پر می‌کند و جمع مبلغ را برمی‌گرداند</summary>
    public static decimal Fill(DataGridView g, IEnumerable<TankhahCharge> charges, Label? emptyLabel = null)
    {
        g.Rows.Clear();
        decimal total = 0;
        int count = 0;
        foreach (var c in charges)
        {
            total += c.Amount;
            count++;
            var row = new List<object?> { c.Id, DateHelper.ToPersianDate(c.Date) };
            if (g.Columns.Contains("List")) row.Add(c.ListTitle);
            row.Add(c.BankTitle);
            row.Add(string.IsNullOrWhiteSpace(c.Description) ? "---" : c.Description);
            row.Add(NumberHelper.FormatThousands(c.Amount, usePersianDigits: true));
            g.Rows.Add(row.ToArray());
        }
        if (emptyLabel is not null) emptyLabel.Visible = count == 0;
        return total;
    }

    /// <summary>جدول + عنوان کوچک بالایش، داخل یک پنل</summary>
    public static Control WrapWithCaption(DataGridView g, string caption, out Label emptyLabel)
    {
        emptyLabel = new Label
        {
            Text = "هنوز شارژی ثبت نشده است.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(10f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent,
            Visible = false
        };
        g.Controls.Add(emptyLabel);

        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 0, 6), BackColor = ModernTheme.Background };
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        host.Controls.Add(new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes,
            Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
            ForeColor = ModernTheme.TextStrong,
            BackColor = Color.Transparent
        }, 0, 0);
        host.Controls.Add(ModernReport.WrapGrid(g), 0, 1);
        return host;
    }
}
