using System;
using System.Data;
using System.Drawing;
using System.Linq; // 👈 برای محاسبه مجموع نیاز به این خط است
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using SMP.Data;
using SMP.Helpers;

namespace SMP.Forms
{
    public class ContractsManagementDashboardForm : Form
    {
        private readonly ContractsDashboardRepository _repository;

        private Label lblTotalClientValue = null!, lblTotalContractorValue = null!;
        private Label lblReceivables = null!, lblPayables = null!, lblExpiring = null!;

        private Chart chartStatus = null!;
        private DataGridView dgvExpiring = null!, dgvAtRisk = null!;

        public ContractsManagementDashboardForm(string connectionString)
        {
            _repository = new ContractsDashboardRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("داشبورد مدیریت قراردادها", new Size(1300, 850));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this);

            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(15) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // ==========================================
            // ۱. کارت‌های KPI
            // ==========================================
            var kpiLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
            for (int i = 0; i < 5; i++) kpiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            lblTotalClientValue = CreateKpiCard(kpiLayout, "مبلغ قراردادهای کارفرما", UIHelper.SectionBlue, 0);
            lblTotalContractorValue = CreateKpiCard(kpiLayout, "مبلغ قراردادهای پیمانکار", UIHelper.SectionOrange, 1);
            lblReceivables = CreateKpiCard(kpiLayout, "مانده مطالبات (طلب)", UIHelper.ActionGreen, 2);
            lblPayables = CreateKpiCard(kpiLayout, "مانده بدهی‌ها (بدهی)", UIHelper.ActionRed, 3);
            lblExpiring = CreateKpiCard(kpiLayout, "در آستانه انقضا (۳۰ روز)", UIHelper.ActionAmber, 4);

            // ==========================================
            // ۲. نمودارها
            // ==========================================
            var chartsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            chartsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            chartsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));

            chartStatus = CreatePieChart("وضعیت قراردادهای کارفرما");
            chartsLayout.Controls.Add(chartStatus, 0, 0);

            var pnlInfo = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5) };
            pnlInfo.Controls.Add(new Label
            {
                Text = "از این داشبورد برای پایش قراردادهای در معرض خطر و در آستانه انقضا استفاده کنید.\nاطلاعات این داشبورد به صورت زنده از دیتابیس خوانده می‌شود.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText
            });
            chartsLayout.Controls.Add(pnlInfo, 1, 0);

            // ==========================================
            // ۳. جدول‌های هشدار
            // ==========================================
            var tabControl = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
            ModernLayoutHelper.StyleModernTabControl(tabControl);

            var tabExpiring = new TabPage("⏳ در آستانه انقضا (۳۰ روز آینده)");
            var tabAtRisk = new TabPage("⚠️ قراردادهای ضررده (ریسک مالی)");

            dgvExpiring = CreateGrid();
            dgvExpiring.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvExpiring.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractNumber", DataPropertyName = "ContractNumber", HeaderText = "شماره قرارداد" });
            dgvExpiring.Columns.Add(new DataGridViewTextBoxColumn { Name = "PartyName", DataPropertyName = "PartyName", HeaderText = "کارفرما" });
            dgvExpiring.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndDate", DataPropertyName = "EndDate", HeaderText = "تاریخ پایان" });
            dgvExpiring.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", DataPropertyName = "Status", HeaderText = "وضعیت" });
            dgvExpiring.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", DataPropertyName = "Amount", HeaderText = "مبلغ قرارداد" });
            dgvExpiring.CellFormatting += Grid_CellFormatting;
            tabExpiring.Controls.Add(dgvExpiring);

            dgvAtRisk = CreateGrid();
            dgvAtRisk.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvAtRisk.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractNumber", DataPropertyName = "ContractNumber", HeaderText = "شماره قرارداد" });
            dgvAtRisk.Columns.Add(new DataGridViewTextBoxColumn { Name = "PartyName", DataPropertyName = "PartyName", HeaderText = "کارفرما" });
            dgvAtRisk.Columns.Add(new DataGridViewTextBoxColumn { Name = "ClientAmount", DataPropertyName = "ClientAmount", HeaderText = "مبلغ قرارداد" });
            dgvAtRisk.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalCosts", DataPropertyName = "TotalCosts", HeaderText = "جمع هزینه‌ها (پیمانکار + فاکتور)" });
            dgvAtRisk.CellFormatting += Grid_CellFormatting;
            tabAtRisk.Controls.Add(dgvAtRisk);

            tabControl.TabPages.Add(tabExpiring);
            tabControl.TabPages.Add(tabAtRisk);

            mainLayout.Controls.Add(kpiLayout, 0, 0);
            mainLayout.Controls.Add(chartsLayout, 0, 1);
            mainLayout.Controls.Add(tabControl, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private Label CreateKpiCard(TableLayoutPanel parent, string title, Color color, int col)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5) };
            card.Paint += (s, e) => { ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle, color, 3, ButtonBorderStyle.Solid, color, 3, ButtonBorderStyle.Solid, color, 3, ButtonBorderStyle.Solid, color, 3, ButtonBorderStyle.Solid); };

            var lblTitle = new Label { Text = title, Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(10f, FontStyle.Bold), ForeColor = color };
            var lblValue = new Label { Text = "0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(16f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            card.Controls.Add(lblValue);
            card.Controls.Add(lblTitle);
            parent.Controls.Add(card, col, 0);

            return lblValue;
        }

        private Chart CreatePieChart(string title)
        {
            var chart = new Chart { Dock = DockStyle.Fill, Margin = new Padding(5), BackColor = Color.White };
            var area = new ChartArea("MainArea") { BackColor = Color.White };
            chart.ChartAreas.Add(area);

            chart.Legends.Add(new Legend("Legend1") { Docking = Docking.Bottom, Font = UIHelper.PersianFont(9f), Alignment = StringAlignment.Center });
            chart.Titles.Add(new Title(title, Docking.Top, UIHelper.PersianFont(12f, FontStyle.Bold), UIHelper.WarmText));

            // فرمت لیبل در اینجا تنظیم نمی‌شود و در زمان بارگذاری به صورت دستی مقداردهی می‌شود
            var series = new Series("Data") { ChartType = SeriesChartType.Pie, IsValueShownAsLabel = false };
            chart.Series.Add(series);

            return chart;
        }

        private DataGridView CreateGrid()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                AutoGenerateColumns = false // 👈 جلوگیری از ایجاد ستون‌های تکراری
            };
            dgv.ApplyModernStyle();
            return dgv;
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var dgv = sender as DataGridView;
            if (dgv == null) return;

            string colName = dgv.Columns[e.ColumnIndex].Name;

            if (colName == "EndDate" && e.Value is DateTime dt)
            {
                e.Value = DateHelper.ToPersianDate(dt);
                e.FormattingApplied = true;
            }
            else if ((colName == "Amount" || colName == "ClientAmount" || colName == "TotalCosts") && e.Value is decimal dec)
            {
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.FormattingApplied = true;
            }

            if (colName == "TotalCosts" && dgv.Columns.Contains("ClientAmount"))
            {
                decimal clientAmt = Convert.ToDecimal(dgv.Rows[e.RowIndex].Cells["ClientAmount"].Value ?? 0);
                decimal costs = Convert.ToDecimal(dgv.Rows[e.RowIndex].Cells["TotalCosts"].Value ?? 0);
                if (costs > clientAmt)
                {
                    e.CellStyle.BackColor = UIHelper.WarmErrorBackground;
                    e.CellStyle.ForeColor = UIHelper.WarmErrorText;
                }
            }
        }

        private async Task LoadDataSafeAsync()
        {
            if (IsDisposed) return;

            try
            {
                DataSet? ds = null;
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    ds = await Task.Run(() => _repository.GetDashboardData());
                }, "در حال بارگذاری داشبورد قراردادها...");

                if (!success || ds == null || IsDisposed) return;

                // ۱. آپدیت KPI ها
                if (ds.Tables["KPI"].Rows.Count > 0)
                {
                    var row = ds.Tables["KPI"].Rows[0];
                    decimal clientVal = Convert.ToDecimal(row["TotalClientValue"]);
                    decimal contractorVal = Convert.ToDecimal(row["TotalContractorValue"]);
                    decimal receivables = Convert.ToDecimal(row["TotalReceivables"]);
                    decimal payables = Convert.ToDecimal(row["TotalPayables"]);
                    int expiring = Convert.ToInt32(row["ExpiringContractsCount"]);

                    lblTotalClientValue.Text = NumberHelper.FormatThousands(clientVal, usePersianDigits: true);
                    lblTotalContractorValue.Text = NumberHelper.FormatThousands(contractorVal, usePersianDigits: true);

                    lblReceivables.Text = NumberHelper.FormatThousands(Math.Max(0, receivables), usePersianDigits: true);
                    lblPayables.Text = NumberHelper.FormatThousands(Math.Max(0, payables), usePersianDigits: true);

                    lblExpiring.Text = NumberHelper.FormatThousands(expiring, usePersianDigits: true);
                }

                // ۲. آپدیت نمودار (محاسبه دستی درصدها)
                chartStatus.Series[0].Points.Clear();
                Color[] chartColors = { UIHelper.ActionGreen, UIHelper.SectionGray, UIHelper.ActionRed, UIHelper.ActionAmber };

                var dtStatus = ds.Tables["StatusDistribution"];
                int totalCount = dtStatus.AsEnumerable().Sum(r => Convert.ToInt32(r["RecordCount"])); // مجموع کل قراردادها

                int idx = 0;
                foreach (DataRow row in dtStatus.Rows)
                {
                    string status = row["Status"].ToString() ?? "نامشخص";
                    int count = Convert.ToInt32(row["RecordCount"]);
                    double percent = totalCount > 0 ? (count * 100.0 / totalCount) : 0;

                    int pointIdx = chartStatus.Series[0].Points.AddXY(status, count);
                    var point = chartStatus.Series[0].Points[pointIdx];

                    point.Color = chartColors[idx % chartColors.Length];
                    point.LegendText = status; // 👈 نام کامل در راهنما

                    // 👈 محاسبه دستی درصد و تبدیل به اعداد فارسی برای نمایش روی دایره
                    string percentStr = StringHelper.ConvertToPersianDigits(percent.ToString("0"));
                    point.Label = $"{percentStr}%";

                    idx++;
                }

                // ۳. آپدیت جدول‌ها
                dgvExpiring.DataSource = ds.Tables["ExpiringContracts"];
                dgvAtRisk.DataSource = ds.Tables["AtRiskContracts"];
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ContractsManagementDashboardForm.LoadDataSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری داشبورد:\n" + ex.Message);
            }
        }
    }
}