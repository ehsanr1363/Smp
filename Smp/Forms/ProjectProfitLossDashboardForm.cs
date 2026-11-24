using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Smp.Forms;

public class ProjectProfitLossDashboardForm : Form
{
    private readonly string _connectionString;
    private readonly ClientContractRepository _contractRepo;

    private ModernTextBox txtSearch = null!;
    private ModernComboBox cmbStatus = null!;
    private ModernButton btnRefresh = null!;
    private FlowLayoutPanel pnlCards = null!;

    private ModernStatCard statCount = null!, statContracts = null!, statCosts = null!, statNet = null!;

    private List<ProjectProfitLossDto> _allProjects = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private const string StatusAll = "همه پروژه‌ها";
    private const string StatusProfit = "فقط سودده";
    private const string StatusLoss = "فقط زیان‌ده";

    public ProjectProfitLossDashboardForm(string connectionString)
    {
        _connectionString = connectionString;
        _contractRepo = new ClientContractRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("داشبورد سود و زیان پروژه‌ها", new Size(1350, 850));

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);

        Shown += async (s, e) => await LoadDataAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ProjectProfitLossDashboardForm"); } }
        base.Dispose(disposing);
    }

    // ═══════════════════════════════════════════════════════════
    //  رابط کاربری
    // ═══════════════════════════════════════════════════════════
    private void BuildUI()
    {
        var root = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = ModernTheme.Background, Padding = new Padding(8, 4, 8, 0) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        root.Controls.Add(BuildToolbar(), 0, 0);
        root.Controls.Add(BuildSummaryStrip(), 0, 1);

        pnlCards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = ModernTheme.Background,
            Padding = new Padding(8, 6, 8, 6)
        };
        pnlCards.SizeChanged += (s, e) => ResizeCards();
        root.Controls.Add(pnlCards, 0, 2);

        Controls.Add(root);
    }

    private Control BuildToolbar()
    {
        var bar = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, BackColor = Color.Transparent };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390F));   // عنوان (راست)
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));   // جستجو
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));   // وضعیت
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));   // بروزرسانی
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var lblTitle = new Label
        {
            Text = "📊 تحلیل سود و زیان قراردادهای کارفرما",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,      // فرم Mirror: متن در سمت راست
            Font = ModernTheme.GetFont(14f, FontStyle.Bold),
            ForeColor = ModernTheme.Primary,
            BackColor = Color.Transparent,
            Margin = new Padding(6, 0, 6, 0)
        };

        txtSearch = ModernUI.Input(ModernInputKind.Text, "جستجوی شماره یا موضوع قرارداد...");
        txtSearch.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        txtSearch.Margin = new Padding(6, 0, 6, 0);
        txtSearch.TextBox.TextChanged += (s, e) => ApplyFilter();

        cmbStatus = ModernUI.Combo();
        cmbStatus.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        cmbStatus.Margin = new Padding(6, 0, 6, 0);
        cmbStatus.Items.AddRange(new object[] { StatusAll, StatusProfit, StatusLoss });
        cmbStatus.SelectedIndex = 0;
        cmbStatus.SelectedIndexChanged += (s, e) => ApplyFilter();

        btnRefresh = ModernButton.Create("🔄 بروزرسانی", ModernTheme.Neutral, 130, ModernTheme.InputHeight);
        btnRefresh.Anchor = AnchorStyles.None;
        btnRefresh.Click += async (s, e) => await LoadDataAsync();

        bar.Controls.Add(lblTitle, 0, 0);
        bar.Controls.Add(txtSearch, 1, 0);
        bar.Controls.Add(cmbStatus, 2, 0);
        bar.Controls.Add(btnRefresh, 3, 0);
        return bar;
    }

    private Control BuildSummaryStrip()
    {
        var strip = ModernReport.CreateStatStrip(4, 84);
        strip.Dock = DockStyle.Fill;

        statCount     = strip.Add("تعداد قراردادها", ModernTheme.Primary);
        statContracts = strip.Add("جمع مبلغ قراردادها", ModernTheme.Purple);
        statCosts     = strip.Add("جمع هزینه‌ها", ModernTheme.Orange);
        statNet       = strip.Add("سود / زیان خالص", ModernTheme.Success);

        foreach (var c in new[] { statCount, statContracts, statCosts, statNet })
        {
            c.ValueFontSize = 13f;
            c.TitleFontSize = 10f;
        }
        statCount.SetCount(0, "قرارداد");
        statContracts.SetAmount(0, "ریال");
        statCosts.SetAmount(0, "ریال");
        statNet.SetAmount(0, "ریال");
        return strip;
    }

    private void ResizeCards()
    {
        int w = Math.Max(300, pnlCards.ClientSize.Width - pnlCards.Padding.Horizontal);
        pnlCards.SuspendLayout();
        foreach (Control c in pnlCards.Controls)
            c.Width = w - c.Margin.Horizontal;
        pnlCards.ResumeLayout();
    }

    // ═══════════════════════════════════════════════════════════
    //  بارگذاری داده‌ها
    // ═══════════════════════════════════════════════════════════
    private async Task LoadDataAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                // ✅ کرایه حمل هم در محاسبات لحاظ شد
                string query = @"
                                SELECT 
                                    c.Id AS ContractId,
                                    c.ContractNumber,
                                    c.Subject,
                                    c.Amount AS ContractAmount,
                                    
                                    (ISNULL((SELECT SUM(Amount) FROM CashTransactions 
                                             WHERE ContractId = c.Id AND Direction = N'پرداختی' AND PaymentScope = N'DirectPurchase'), 0) +
                                     ISNULL((SELECT SUM(Amount) FROM CashChecks 
                                             WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope = N'DirectPurchase'), 0)
                                    ) AS DirectPurchase,
        
                                    (ISNULL((SELECT SUM(Amount) FROM CashTransactions 
                                             WHERE ContractId = c.Id AND Direction = N'پرداختی' AND PaymentScope = N'ProjectShipping'), 0) +
                                     ISNULL((SELECT SUM(Amount) FROM CashChecks 
                                             WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope = N'ProjectShipping'), 0)
                                    ) AS ProjectShipping, -- ✅ فیلد جدید
        
                                    -- ✅ اصلاح ۲: هزینه‌های پروژه (PaymentScope = ProjectExpense) که قبلاً جمع زده نمی‌شد
                                    (ISNULL((SELECT SUM(Amount) FROM CashTransactions 
                                             WHERE ContractId = c.Id AND Direction = N'پرداختی' AND PaymentScope = N'ProjectExpense'), 0) +
                                     ISNULL((SELECT SUM(Amount) FROM CashChecks 
                                             WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope = N'ProjectExpense'), 0)
                                    ) AS ProjectExpenseCost,
        
                                    (ISNULL((SELECT SUM(Amount) FROM CashTransactions 
                                             WHERE ContractId = c.Id AND Direction = N'پرداختی' AND PaymentScope = N'InformalContractor'), 0) +
                                     ISNULL((SELECT SUM(Amount) FROM CashChecks 
                                             WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope = N'InformalContractor'), 0)
                                    ) AS InformalContractor,
        
                                    (ISNULL((SELECT SUM(Amount) FROM CashTransactions 
                                             WHERE ContractId = c.Id AND Direction = N'پرداختی' AND PaymentScope = N'ContractorContract'), 0) +
                                     ISNULL((SELECT SUM(Amount) FROM CashChecks 
                                             WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope = N'ContractorContract'), 0)
                                    ) AS FormalContractor,
        
                                    ISNULL((SELECT SUM(Amount) FROM ProjectExpenses WHERE ClientContractId = c.Id), 0) AS InvoiceExpenses,
        
                                    (ISNULL((SELECT SUM(Amount) FROM CashTransactions 
                                             WHERE ContractId = c.Id AND Direction = N'پرداختی' AND PaymentScope = N'Administrative'), 0) +
                                     ISNULL((SELECT SUM(Amount) FROM CashChecks 
                                             WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope = N'Administrative'), 0)
                                    ) AS AdminExpenses
                                FROM ClientContracts c
                                ORDER BY c.Id DESC";

                var dt = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, query));

                if (IsDisposed) return;

                _allProjects.Clear();

                foreach (DataRow row in dt.Rows)
                {
                    _allProjects.Add(new ProjectProfitLossDto
                    {
                        ContractId = Convert.ToInt32(row["ContractId"]),
                        ContractNumber = row["ContractNumber"].ToString() ?? "",
                        Subject = row["Subject"].ToString() ?? "",
                        ContractAmount = Convert.ToDecimal(row["ContractAmount"]),
                        DirectPurchase = Convert.ToDecimal(row["DirectPurchase"]),
                        ProjectShipping = Convert.ToDecimal(row["ProjectShipping"]),
                        ProjectExpenseCost = Convert.ToDecimal(row["ProjectExpenseCost"]),
                        InformalContractor = Convert.ToDecimal(row["InformalContractor"]),
                        FormalContractor = Convert.ToDecimal(row["FormalContractor"]),
                        InvoiceExpenses = Convert.ToDecimal(row["InvoiceExpenses"]),
                        AdminExpenses = Convert.ToDecimal(row["AdminExpenses"])
                    });
                }
            }, "در حال محاسبه سود و زیان پروژه‌ها...");

            if (!success || IsDisposed) return;

            ApplyFilter();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ProjectProfitLossDashboardForm.LoadDataAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات:\n" + ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ProjectProfitLossDashboardForm"); }
        }
    }

    private void ApplyFilter()
    {
        if (IsDisposed || pnlCards is null) return;

        string search = txtSearch.Text.Trim();
        string status = cmbStatus.SelectedItem?.ToString() ?? StatusAll;

        var filtered = _allProjects.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(p =>
                p.ContractNumber.Contains(search) ||
                (p.Subject is not null && p.Subject.Contains(search)));
        }
        if (status == StatusProfit) filtered = filtered.Where(p => p.Profit >= 0);
        else if (status == StatusLoss) filtered = filtered.Where(p => p.Profit < 0);

        var displayList = filtered.OrderBy(p => p.Profit).ToList();

        UpdateSummary(displayList);

        pnlCards.SuspendLayout();
        var old = pnlCards.Controls.Cast<Control>().ToList();
        pnlCards.Controls.Clear();
        foreach (var c in old) c.Dispose();

        if (displayList.Count == 0)
        {
            pnlCards.Controls.Add(new Label
            {
                Text = "هیچ پروژه‌ای با این مشخصات یافت نشد.",
                AutoSize = true,
                Font = ModernTheme.GetFont(12f, FontStyle.Bold),
                ForeColor = ModernTheme.TextMuted,
                Margin = new Padding(20)
            });
        }
        else
        {
            foreach (var project in displayList)
                pnlCards.Controls.Add(CreateProjectCard(project));
            ResizeCards();
        }

        pnlCards.ResumeLayout(true);
    }

    private void UpdateSummary(List<ProjectProfitLossDto> list)
    {
        decimal contracts = list.Sum(p => p.ContractAmount);
        decimal costs = list.Sum(p => p.TotalCosts);
        decimal net = contracts - costs;
        int profitCount = list.Count(p => p.Profit >= 0);
        int lossCount = list.Count - profitCount;

        statCount.SetCount(list.Count, "قرارداد");
        statCount.SubText = $"{StringHelper.ConvertToPersianDigits(profitCount.ToString())} سودده  ·  {StringHelper.ConvertToPersianDigits(lossCount.ToString())} زیان‌ده";
        statContracts.SetAmount(contracts, "ریال");
        statCosts.SetAmount(costs, "ریال");
        statNet.Accent = net >= 0 ? ModernTheme.Success : ModernTheme.Danger;
        statNet.SetAmount(net, "ریال");
    }

    private ProjectCard CreateProjectCard(ProjectProfitLossDto dto)
    {
        var card = new ProjectCard(dto) { Width = Math.Max(300, pnlCards.ClientSize.Width - pnlCards.Padding.Horizontal) };
        card.Opened += (s, e) =>
        {
            try
            {
                var contract = _contractRepo.GetById(dto.ContractId);
                if (contract is not null)
                {
                    using var form = new ClientContractReportForm(_connectionString, contract);
                    form.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ProjectProfitLossDashboardForm.OpenContract");
                ModernMessageBox.ShowError("خطا در باز کردن قرارداد:\n" + ex.Message);
            }
        };
        return card;
    }

    // ═══════════════════════════════════════════════════════════
    //  کارت پروژه (کاملاً رسم‌شده؛ هم‌سبک ModernStatCard: نوار رنگی لبه‌ی راست، متن راست‌چین)
    // ═══════════════════════════════════════════════════════════
    private sealed class ProjectCard : Control
    {
        private const int PadR = 22, PadL = 16;
        private readonly ProjectProfitLossDto _dto;
        private readonly ModernProgressBar _bar;
        private readonly Color _accent;
        private bool _hot;

        public event EventHandler? Opened;

        public ProjectCard(ProjectProfitLossDto dto)
        {
            _dto = dto;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            RightToLeft = RightToLeft.Yes;
            Height = 168;
            Margin = new Padding(0, 0, 0, 12);
            Cursor = Cursors.Hand;

            _accent = dto.Profit >= 0 ? ModernTheme.Success : ModernTheme.Danger;

            double pct = dto.ProgressPercent;
            _bar = new ModernProgressBar
            {
                Value = (decimal)pct,
                Accent = pct > 100 ? ModernTheme.Danger : (pct > 80 ? ModernTheme.Warning : ModernTheme.Success),
                Caption = $"مصرف بودجه: {StringHelper.ConvertToPersianDigits(pct.ToString("N1"))}%",
                Cursor = Cursors.Hand
            };
            Controls.Add(_bar);

            Click += (s, e) => Opened?.Invoke(this, EventArgs.Empty);
            _bar.Click += (s, e) => Opened?.Invoke(this, EventArgs.Empty);
            MouseEnter += (s, e) => SetHot(true);
            MouseLeave += (s, e) => SetHot(ClientRectangle.Contains(PointToClient(Cursor.Position)));
            _bar.MouseEnter += (s, e) => SetHot(true);
            _bar.MouseLeave += (s, e) => SetHot(ClientRectangle.Contains(PointToClient(Cursor.Position)));
        }

        private void SetHot(bool hot)
        {
            if (_hot == hot) return;
            _hot = hot;
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            _bar.SetBounds(PadL, Height - 38, Math.Max(40, Width - PadL - PadR), 24);
        }

        private static string Fmt(decimal v) => NumberHelper.FormatThousands(v, usePersianDigits: true);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(ModernPaint.BehindColor(this));

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width < 120 || rect.Height < 60) return;

            using (var path = ModernPaint.RoundedRect(rect, 12))
            {
                using (var br = new SolidBrush(_hot ? ModernTheme.SurfaceAlt : ModernTheme.Surface)) g.FillPath(br, path);

                var state = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                using (var sb = new SolidBrush(_accent)) g.FillRectangle(sb, Width - 5, 0, 5, Height);
                g.Restore(state);

                using var pen = new Pen(_hot ? ModernPaint.Lighten(_accent, 0.35f) : ModernTheme.Border, _hot ? 1.6f : 1f);
                g.DrawPath(pen, path);
            }

            var right = TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix |
                        TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;
            var center = TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix |
                         TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;

            // ── ردیف ۱: عنوان (راست) + نشان سود/زیان (چپ) ──
            bool profit = _dto.Profit >= 0;
            string status = profit
                ? $"سود: {Fmt(_dto.Profit)} ریال"
                : $"زیان: {Fmt(Math.Abs(_dto.Profit))} ریال";
            var statusFont = ModernTheme.GetFont(11.5f, FontStyle.Bold);
            int pillW = Math.Min(Width / 2, TextRenderer.MeasureText(g, status, statusFont, new Size(int.MaxValue, 30), center).Width + 30);
            var pill = new Rectangle(PadL, 12, pillW, 30);
            using (var pp = ModernPaint.RoundedRect(pill, 15))
            using (var pb = new SolidBrush(profit ? ModernTheme.SuccessSoft : ModernTheme.DangerSoft))
                g.FillPath(pb, pp);
            TextRenderer.DrawText(g, status, statusFont, pill,
                profit ? ModernPaint.Darken(ModernTheme.Success, 70) : ModernTheme.DangerText, center);

            var titleRect = new Rectangle(PadL + pillW + 12, 12, Width - PadR - (PadL + pillW + 12), 30);
            TextRenderer.DrawText(g, $"قرارداد {_dto.ContractNumber} — {_dto.Subject}",
                ModernTheme.GetFont(12f, FontStyle.Bold), titleRect, ModernTheme.TextStrong, right);

            // ── ردیف ۲: مبلغ قرارداد و جمع هزینه‌ها ──
            TextRenderer.DrawText(g,
                $"مبلغ قرارداد: {Fmt(_dto.ContractAmount)} ریال     |     جمع هزینه‌ها: {Fmt(_dto.TotalCosts)} ریال",
                ModernTheme.GetFont(10f, FontStyle.Bold),
                new Rectangle(PadL, 46, Width - PadL - PadR, 24), ModernTheme.TextMuted, right);

            // ── ردیف ۳: ریز هزینه‌ها (۷ خانه‌ی هم‌عرض؛ اولین خانه در راست) ──
            var items = new (string Caption, decimal Value)[]
            {
                ("🛒 خرید مستقیم", _dto.DirectPurchase),
                ("🚚 کرایه حمل", _dto.ProjectShipping),
                ("🧰 هزینه‌های پروژه", _dto.ProjectExpenseCost),
                ("👷 پیمانکار غیررسمی", _dto.InformalContractor),
                ("📋 پیمانکار رسمی", _dto.FormalContractor),
                ("🧾 فاکتورها", _dto.InvoiceExpenses),
                ("🏢 اداری", _dto.AdminExpenses)
            };
            int areaW = Width - PadL - PadR;
            int cellW = areaW / items.Length;
            int top = 74;
            var capFont = ModernTheme.GetFont(8.5f);
            var valFont = ModernTheme.GetFont(10f, FontStyle.Bold);
            for (int i = 0; i < items.Length; i++)
            {
                int x = Width - PadR - (i + 1) * cellW;
                if (i > 0)
                {
                    using var sep = new Pen(ModernTheme.GridLine, 1f);
                    g.DrawLine(sep, x + cellW, top + 2, x + cellW, top + 44);
                }
                TextRenderer.DrawText(g, items[i].Caption, capFont, new Rectangle(x, top, cellW, 20), ModernTheme.TextMuted, center);
                TextRenderer.DrawText(g, Fmt(items[i].Value), valFont, new Rectangle(x, top + 20, cellW, 24),
                    items[i].Value == 0 ? ModernTheme.TextDisabled : ModernTheme.TextStrong, center);
            }
        }
    }

    private class ProjectProfitLossDto
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = "";
        public string Subject { get; set; } = "";
        public decimal ContractAmount { get; set; }
        public decimal DirectPurchase { get; set; }
        public decimal ProjectShipping { get; set; } // ✅ فیلد جدید
        public decimal ProjectExpenseCost { get; set; } // ✅ هزینه‌های پروژه (ProjectExpense)
        public decimal InformalContractor { get; set; }
        public decimal FormalContractor { get; set; }
        public decimal InvoiceExpenses { get; set; }
        public decimal AdminExpenses { get; set; }

        // ✅ کرایه حمل هم به مجموع هزینه‌ها اضافه شد
        public decimal TotalCosts => DirectPurchase + ProjectShipping + ProjectExpenseCost + InformalContractor + FormalContractor + InvoiceExpenses + AdminExpenses;
        public decimal Profit => ContractAmount - TotalCosts;
        public double ProgressPercent => ContractAmount > 0 ? (double)(TotalCosts / ContractAmount) * 100 : 0;
    }
}
