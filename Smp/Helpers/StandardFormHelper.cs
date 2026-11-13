using System;
using System.Drawing;
using System.Windows.Forms;

namespace SMP.Helpers
{
    public static class StandardFormHelper
    {
        public static (TableLayoutPanel ContentLayout, FlowLayoutPanel ButtonPanel) SetupStandardLayout(
            Form form, string title, Size size)
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            var contentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                AutoSize = true,
                Padding = new Padding(20, 15, 20, 15),
                RightToLeft = RightToLeft.Yes
            };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            scroll.Controls.Add(contentLayout);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(15, 10, 15, 10),
                BackColor = UIHelper.WarmPanelBackground
            };

            mainLayout.Controls.Add(scroll, 0, 0);
            mainLayout.Controls.Add(buttonPanel, 0, 1);

            form.Controls.Add(mainLayout);
            form.ApplyStandardHeader(title, size);
            form.ShowInTaskbar = false;

            return (contentLayout, buttonPanel);
        }

        public static (FlowLayoutPanel TopRightPanel, FlowLayoutPanel TopLeftPanel, DataGridView Grid, FlowLayoutPanel BottomPanel) SetupStandardListLayout(
            Form form, string title, Size size)
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));

            var topLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = UIHelper.WarmPanelBackground };
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var topRightPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = true, Padding = new Padding(15, 5, 15, 5) };
            var topLeftPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Padding = new Padding(15, 5, 15, 5) };

            topLayout.Controls.Add(topRightPanel, 0, 0);
            topLayout.Controls.Add(topLeftPanel, 2, 0);

            var grid = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(10, 0, 10, 0) };
            grid.ApplyModernStyle();
            ApplyGridRtlWorkaround(grid);

            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(15, 10, 15, 10),
                BackColor = UIHelper.WarmPanelBackground
            };

            mainLayout.Controls.Add(topLayout, 0, 0);
            mainLayout.Controls.Add(grid, 0, 1);
            mainLayout.Controls.Add(bottomPanel, 0, 2);

            form.Controls.Add(mainLayout);
            form.ApplyStandardHeader(title, size);
            form.ShowInTaskbar = false;

            return (topRightPanel, topLeftPanel, grid, bottomPanel);
        }

        public static (TableLayoutPanel FormLayout, DataGridView Grid, FlowLayoutPanel ActionPanel) SetupMasterDetailLayout(
            Form form, string title, Size size)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(0) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));

            var inputPanel = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(10, 10, 10, 0) };

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, AutoSize = true, RightToLeft = RightToLeft.Yes };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            inputPanel.Controls.Add(formLayout);
            root.Controls.Add(inputPanel, 0, 0);

            var grid = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(10, 5, 10, 5) };
            grid.ApplyModernStyle();
            ApplyGridRtlWorkaround(grid);
            root.Controls.Add(grid, 0, 1);

            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(15, 10, 15, 10) };
            root.Controls.Add(actionPanel, 0, 2);

            form.Controls.Add(root);
            form.ApplyStandardHeader(title, size);
            form.ShowInTaskbar = false;

            return (formLayout, grid, actionPanel);
        }

        public static void AddRow(TableLayoutPanel layout, float height = 50F)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowCount = layout.RowStyles.Count;
        }

        /// <summary>
        /// ✅ حل قطعی باگ Fill در RTL: گرید را راست‌چین می‌کنیم و با Padding از بریدگی متن جلوگیری می‌کنیم.
        /// </summary>
        private static void ApplyGridRtlWorkaround(DataGridView grid)
        {
            grid.RightToLeft = RightToLeft.Yes;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // حاشیه کوچک برای جلوگیری از چسبیدن متن به لبه‌ها و باگ بریدگی
            grid.DefaultCellStyle.Padding = new Padding(5, 0, 5, 0);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(5, 0, 5, 0);
        }
    }
}