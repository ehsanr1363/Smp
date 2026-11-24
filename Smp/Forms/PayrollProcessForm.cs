using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class PayrollProcessForm : Form
{
    private readonly string _connectionString;
    private readonly PayrollCalculatorService _calculator;
    private readonly PayrollRepository _payrollRepo;

    private ComboBox cmbYear = null!, cmbMonth = null!;
    private Button btnCalculate = null!, btnCalcMissing = null!, btnRecalcSelected = null!, btnDelete = null!, btnSave = null!;
    private PayrollInfoBar _infoBar = null!;
    private TableLayoutPanel _mainLayout = null!;
    private ModernStatCard _stSaved = null!, _stUnsaved = null!, _stNet = null!;
    private PayrollSegmentTabs _tabs = null!;

    private DataGridView dgvContractual = null!;
    private DataGridView dgvDaily = null!;
    private DataGridView dgvHourly = null!;

    // ✅ همه‌ی ردیف‌های دوره‌ی جاری (هم ذخیره‌شده از دیتابیس، هم محاسبه‌ی جدید)؛ کلید = EmployeeId
    private readonly Dictionary<int, PayrollResult> _rows = new();

    // ✅ کارمندانی که الآن در دیتابیس فیش دارند + اقساطی که در همان فیش کسر شده
    // (برای محاسبه مجدد، همین اقساط استفاده می‌شود تا با برگشت/کسر دوباره‌ی ریپازیتوری هم‌خوان بماند)
    private readonly Dictionary<int, (decimal Loan, decimal Advance, decimal Supp, decimal Car)> _savedDeductions = new();

    // دوره‌ای که الآن روی صفحه نمایش داده می‌شود
    private int _periodYear;
    private int _periodMonth;

    private bool _formReady;
    private bool _suppressPeriodChange;
    private bool _isBusy;

    public PayrollProcessForm(string connectionString)
    {
        _connectionString = connectionString;
        _calculator = new PayrollCalculatorService();
        _payrollRepo = new PayrollRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("پردازش حقوق ماهانه (تفکیک‌شده)", new Size(1300, 820));
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        // ✅ با باز شدن فرم، فیش‌های ذخیره‌شده‌ی ماه انتخاب‌شده خودکار نمایش داده می‌شود (بدون زدن «محاسبه»)
        this.Shown += async (s, e) =>
        {
            await LoadSavedAsync();
            _formReady = true;
        };

        cmbYear.SelectedIndexChanged += async (s, e) => await OnPeriodChangedAsync();
        cmbMonth.SelectedIndexChanged += async (s, e) => await OnPeriodChangedAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    // UI
    // ═══════════════════════════════════════════════════════════════
    private void BuildUI()
    {
        var pc = new System.Globalization.PersianCalendar();

        // ─── انتخاب دوره (سال / ماه) ───
        cmbYear = ModernUI.Combo();
        for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);
        cmbYear.SelectedItem = pc.GetYear(DateTime.Now);
        cmbYear.Width = 96;
        cmbYear.Margin = new Padding(2, 0, 14, 0);

        cmbMonth = ModernUI.Combo();
        for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);
        cmbMonth.SelectedItem = pc.GetMonth(DateTime.Now);
        cmbMonth.Width = 80;
        cmbMonth.Margin = new Padding(2, 0, 0, 0);

        // لیبل هم‌ارتفاع با کمبو (۳۶) و وسط‌چین تا دقیقاً هم‌تراز شوند
        Label PeriodLabel(string t)
        {
            var font = ModernTheme.GetFont(10f, FontStyle.Bold);
            int w = TextRenderer.MeasureText(t, font).Width + 8;
            return new Label
            {
                Text = t, AutoSize = false, Size = new Size(w, ModernTheme.InputHeight),
                TextAlign = ContentAlignment.MiddleCenter, Font = font,
                ForeColor = ModernTheme.TextMuted, Margin = new Padding(0, 0, 2, 0), BackColor = Color.Transparent
            };
        }

        // ─── دکمه‌ها (آیکن‌های ترسیمی به‌جای ایموجی؛ ظاهر یکدست روی همه‌ی سیستم‌ها) ───
        btnCalculate = ToolButton("محاسبه همه", ModernTheme.Primary, ModernButtonStyle.Filled, PayrollIcon.Calc);
        btnCalculate.Click += async (s, e) => await CalculateAsync(null);

        btnCalcMissing = ToolButton("محاسبه افراد بدون حقوق", ModernTheme.Purple, ModernButtonStyle.Soft, PayrollIcon.Plus);
        btnCalcMissing.Click += async (s, e) => await CalculateAsync(null, onlyMissing: true);

        btnRecalcSelected = ToolButton("محاسبه مجدد انتخاب‌شده‌ها", ModernTheme.Teal, ModernButtonStyle.Soft, PayrollIcon.Refresh);
        btnRecalcSelected.Click += async (s, e) => await RecalculateSelectedAsync();

        btnDelete = ToolButton("حذف انتخاب‌شده‌ها", ModernTheme.Danger, ModernButtonStyle.Soft, PayrollIcon.Trash);
        btnDelete.Click += async (s, e) => await DeleteSelectedAsync();

        btnSave = ToolButton("ذخیره", ModernTheme.Success, ModernButtonStyle.Filled, PayrollIcon.Save);
        btnSave.Click += async (s, e) => await SaveSafeAsync();

        FlowLayoutPanel Group(params Control[] items)
        {
            var f = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false,
                FlowDirection = FlowDirection.RightToLeft, RightToLeft = RightToLeft.Yes,
                Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.Transparent,
                Anchor = AnchorStyles.None
            };
            foreach (var c in items) f.Controls.Add(c);
            return f;
        }

        Panel Divider() => new Panel
        {
            Width = 1, Height = 28, BackColor = ModernTheme.Border,
            Margin = new Padding(16, 0, 16, 0), Anchor = AnchorStyles.None
        };

        // راست → چپ:  [دوره] | [محاسبه‌ها] ........ [حذف | ذخیره]
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1,
            RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = Padding.Empty
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        bar.Controls.Add(Group(PeriodLabel("سال"), cmbYear, PeriodLabel("ماه"), cmbMonth), 0, 0);
        bar.Controls.Add(Divider(), 1, 0);
        bar.Controls.Add(Group(btnCalculate, btnCalcMissing, btnRecalcSelected), 2, 0);
        bar.Controls.Add(Group(btnDelete, btnSave), 4, 0);

        var pnlTop = new ModernCard("", ModernTheme.Primary, 1) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
        pnlTop.Body.Controls.Add(bar, 0, 0);

        // ─── کارت‌های خلاصه ───
        var strip = ModernReport.CreateStatStrip(3, 88);
        _stSaved = strip.Add("ذخیره‌شده", ModernTheme.Success, "فیش ثبت‌شده در دیتابیس");
        _stUnsaved = strip.Add("محاسبه‌شده و ذخیره‌نشده", ModernTheme.Warning, "در انتظار ذخیره");
        _stNet = strip.Add("جمع خالص پرداختی (این ماه)", ModernTheme.Primary, "");
        foreach (var card in new[] { _stSaved, _stUnsaved, _stNet }) card.ValueFontSize = 15f;
        _stSaved.SetCount(0, "نفر");
        _stUnsaved.SetCount(0, "نفر");
        _stNet.SetAmount(0, "ریال");
        strip.Dock = DockStyle.Fill;
        strip.Margin = new Padding(0, 0, 0, 10);

        // ─── تب‌های قطعه‌ای (با شمارنده‌ی نفرات) + راهنمای رنگ ردیف‌ها ───
        _tabs = new PayrollSegmentTabs { Dock = DockStyle.Right, Width = 540 };
        _tabs.AddTab("قراردادی / ماهانه");
        _tabs.AddTab("روزمزد");
        _tabs.AddTab("ساعتی");
        _tabs.SelectedIndexChanged += (s, e) => ShowActiveGrid();

        var legend = new PayrollLegend { Dock = DockStyle.Left, Width = 300 };

        var tabHeader = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 8), BackColor = Color.Transparent, Margin = Padding.Empty };
        tabHeader.Controls.Add(legend);
        tabHeader.Controls.Add(_tabs);

        // ─── جدول‌ها (هر سه داخل یک کادر؛ فقط یکی نمایش داده می‌شود) ───
        dgvContractual = CreateGrid(PayrollKind.Monthly);
        dgvDaily = CreateGrid(PayrollKind.Daily);
        dgvHourly = CreateGrid(PayrollKind.Hourly);

        var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = ModernTheme.Border, Margin = Padding.Empty };
        foreach (var g in new[] { dgvHourly, dgvDaily, dgvContractual }) { g.Dock = DockStyle.Fill; gridHost.Controls.Add(g); }
        ShowActiveGrid();

        // ─── نوار پیام (فقط وقتی ردیف ذخیره‌نشده هست دیده می‌شود) ───
        _infoBar = new PayrollInfoBar { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0), Visible = false };

        _mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(14, 12, 14, 10) };
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));    // نوار ابزار
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 98F));    // کارت‌های خلاصه
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));    // تب‌ها
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));    // جدول
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));     // پیام (پویا)
        _mainLayout.Controls.Add(pnlTop, 0, 0);
        _mainLayout.Controls.Add(strip, 0, 1);
        _mainLayout.Controls.Add(tabHeader, 0, 2);
        _mainLayout.Controls.Add(gridHost, 0, 3);
        _mainLayout.Controls.Add(_infoBar, 0, 4);
        this.Controls.Add(_mainLayout);
    }

    /// <summary>دکمه‌ی نوار ابزار: متن + آیکن ترسیمی، عرض متناسب با متن</summary>
    private static ModernButton ToolButton(string text, Color accent, ModernButtonStyle style, PayrollIcon icon)
    {
        var b = ModernButton.Create(text, accent, 400, 36, style);
        Color iconColor = style == ModernButtonStyle.Filled ? Color.White : ModernPaint.Darken(accent, 40);
        b.Icon = PayrollIcons.Make(icon, iconColor);
        b.IconSize = 16;
        b.Height = 36;
        b.Width = TextRenderer.MeasureText(text, b.Font).Width + 16 + 8 + 36;   // متن + آیکن + فاصله + حاشیه
        b.Margin = new Padding(4, 0, 4, 0);
        return b;
    }

    private void ShowActiveGrid()
    {
        var active = ActiveGrid();
        foreach (var g in new[] { dgvContractual, dgvDaily, dgvHourly })
            g.Visible = ReferenceEquals(g, active);
        active.BringToFront();
    }

    private DataGridView CreateGrid(PayrollKind kind)
    {
        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true
        };
        dgv.ApplyModernStyle();
        dgv.UseAiryLook(48);
        dgv.UseSelectionAccent(ModernTheme.Primary);
        dgv.ReadOnly = false; // فقط ستون «انتخاب» قابل ویرایش است (بقیه ستون‌ها ReadOnly هستند)
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        dgv.ColumnHeadersHeight = 50;

        // عرض و عنوان دلخواه ستون‌ها؛ ModernGrid بعد از هر Bind ستون‌ها را «اندازه‌ی محتوا» و عنوان‌ها را ترجمه می‌کند
        // و ظاهر سه تب ناهمسان می‌شد؛ در DataBindingComplete دوباره همین مقادیر اعمال می‌شوند.
        var widths = new Dictionary<string, int>();
        var headers = new Dictionary<string, string>();

        void Col(string prop, string header, int width)
        {
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = prop, DataPropertyName = prop, HeaderText = header, Width = width, MinimumWidth = 60 });
            widths[prop] = width;
            headers[prop] = header;
        }

        dgv.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsSelected", HeaderText = "انتخاب", Width = 64 });
        widths["IsSelected"] = 64;
        headers["IsSelected"] = "انتخاب";
        Col("PersonnelCode", "کد پرسنلی", 80);          // فقط زیر نام نمایش داده می‌شود
        dgv.Columns["PersonnelCode"]!.Visible = false;
        Col("EmployeeName", "پرسنل", 250);
        Col("StatusText", "وضعیت", 180);

        // ─── مبنای کارکرد (بسته به نوع پرسنل) ───
        var basis = new List<string>();
        if (kind == PayrollKind.Daily) { Col("DailyWage", "حقوق روزانه", 110); basis.Add("DailyWage"); }
        if (kind == PayrollKind.Hourly) { Col("HourlyWage", "حقوق ساعتی", 110); basis.Add("HourlyWage"); }
        Col("WorkingDays", "روزهای کارکرد", 96); basis.Add("WorkingDays");
        Col("OvertimeHours", "ساعت اضافه‌کار", 100); basis.Add("OvertimeHours");

        // ─── اقلام حقوق و مزایا ───
        // روزمزد: فقط حقوق و اضافه‌کار (مزایای ماهانه ندارد)؛ کسورات برای همه‌ی انواع پرسنل کامل است.
        bool isDaily = kind == PayrollKind.Daily;
        var earnings = new List<string>();
        void Earn(string prop, string header, int width) { Col(prop, header, width); earnings.Add(prop); }

        Earn("BaseSalary", isDaily ? "حقوق" : "حقوق پایه", 112);
        if (!isDaily)
        {
            Earn("YearsBonus", "پایه سنوات", 104);
            Earn("AttractionAllowance", "حق جذب", 104);
            Earn("ResponsibilityAllowance", "حق مسئولیت", 108);
            Earn("ManagementAllowance", "حق مدیریت", 104);
            Earn("TechnicalAllowance", "حق فنی", 104);
            Earn("HousingAllowance", "حق مسکن", 104);
            Earn("GroceryAllowance", "حق خواربار", 104);
            Earn("ChildAllowance", "حق اولاد", 104);
            Earn("MarriageAllowance", "حق تأهل", 104);
            Earn("TransportationAllowance", "حق ایاب و ذهاب", 116);
            Earn("OtherAllowances", "سایر مزایا", 104);
        }
        Earn("OvertimePay", "اضافه کار", 108);
        Earn("TotalEarnings", "جمع مزایا", 124);

        // ─── کسورات ───
        var deductions = new List<string>();
        void Ded(string prop, string header, int width) { Col(prop, header, width); deductions.Add(prop); }

        Ded("AbsenceHours", "ساعت کسر کار", 100);
        Ded("AbsenceDeduction", "کسر غیبت", 104);
        Ded("InsuranceEmployee", "بیمه کارمند", 112);
        Ded("IncomeTax", "مالیات", 104);
        Ded("LoanDeduction", "قسط وام", 104);
        Ded("AdvanceDeduction", "مساعده", 104);
        Ded("SupplementaryInsuranceDeduction", "بیمه تکمیلی", 108);
        Ded("CarInsuranceDeduction", "بیمه ماشین", 108);
        Ded("OtherDeductions", "سایر کسورات", 108);
        Ded("TotalDeductions", "جمع کسور", 116);
        Col("NetPay", "خالص پرداختی", 140);

        // ستون‌های اول هنگام اسکرول افقی ثابت بمانند
        dgv.Columns["IsSelected"]!.Frozen = true;
        dgv.Columns["EmployeeName"]!.Frozen = true;

        dgv.UsePersonColumn("EmployeeName", "PersonnelCode");
        dgv.UseBadges("StatusText", new Dictionary<string, Color>
        {
            ["ذخیره‌شده"] = ModernTheme.Success,
            ["محاسبه‌شده (ذخیره‌نشده)"] = ModernTheme.Warning
        });

        foreach (DataGridViewColumn col in dgv.Columns)
            col.ReadOnly = col.Name != "IsSelected";

        // ─── سربرگ گروه‌بندی‌شده با رنگ: مبنا (خنثی) / مزایا (سبز) / کسورات (قرمز) / خالص (آبی) ───
        void Tint(IEnumerable<string> names, Color back, Color fore)
        {
            foreach (var n in names)
            {
                if (!dgv.Columns.Contains(n)) continue;
                var st = dgv.Columns[n]!.HeaderCell.Style;
                st.BackColor = back; st.SelectionBackColor = back;
                st.ForeColor = fore; st.SelectionForeColor = fore;
            }
        }
        Tint(earnings, ModernPaint.Blend(Color.White, ModernTheme.Success, 0.09f), ModernPaint.Darken(ModernTheme.Success, 90));
        Tint(deductions, ModernPaint.Blend(Color.White, ModernTheme.Danger, 0.08f), ModernTheme.DangerText);
        Tint(new[] { "NetPay" }, ModernPaint.Blend(Color.White, ModernTheme.Primary, 0.16f), ModernPaint.Darken(ModernTheme.Primary, 80));

        // ستون «خالص پرداختی» کمی پررنگ‌تر از بقیه تا چشم اول به آن برسد
        dgv.Columns["NetPay"]!.DefaultCellStyle.BackColor = ModernPaint.Blend(Color.White, ModernTheme.Success, 0.07f);

        dgv.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewColumn col in dgv.Columns)
            {
                if (widths.TryGetValue(col.Name, out int w)) col.Width = w;
                if (headers.TryGetValue(col.Name, out string? h)) col.HeaderText = h;
            }
            dgv.ColumnHeadersHeight = 50;
        };

        // تیک چک‌باکس بلافاصله ثبت شود
        dgv.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (dgv.IsCurrentCellDirty) dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // ردیف‌های محاسبه‌شده‌ی ذخیره‌نشده با رنگ زرد روشن مشخص می‌شوند
            if (dgv.Rows[e.RowIndex].DataBoundItem is PayrollResult pr && !pr.IsSaved)
                e.CellStyle.BackColor = ModernTheme.WarningSoft;

            string colName = dgv.Columns[e.ColumnIndex].DataPropertyName;
            if (e.CellStyle is not null)
            {
                if (colName == "NetPay") { e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold); e.CellStyle.ForeColor = ModernPaint.Darken(ModernTheme.Success, 70); }
                else if (colName == "TotalDeductions") { e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold); e.CellStyle.ForeColor = ModernTheme.DangerText; }
                else if (colName == "TotalEarnings") e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
            }
            if (colName == "OvertimeHours" && e.Value is decimal ot)
            {
                // ساعت اعشاری → ساعت:دقیقه (مثلاً ۷٫۵ → ۰۷:۳۰)
                int totalMinutes = (int)Math.Round(Math.Abs(ot) * 60m, MidpointRounding.AwayFromZero);
                string hhmm = $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
                e.Value = StringHelper.ConvertToPersianDigits(hhmm);
                e.FormattingApplied = true;
            }
            else if ((colName == "WorkingDays" || colName == "AbsenceHours") && e.Value is decimal qty)
            {
                e.Value = StringHelper.ConvertToPersianDigits(qty.ToString("0.##"));
                e.FormattingApplied = true;
            }
            else if (colName != "PersonnelCode" && colName != "EmployeeName" && colName != "StatusText" && e.Value is decimal dec)
            {
                // مقدارهای صفر کم‌رنگ می‌شوند تا اعداد واقعی بیشتر دیده شوند
                if (dec == 0m && e.CellStyle is not null && colName != "NetPay")
                    e.CellStyle.ForeColor = ModernTheme.Placeholder;
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.FormattingApplied = true;
            }
        };

        // حالت خالی: به‌جای جدول سفید و بی‌صدا، توضیح و راهنمای شروع نمایش داده می‌شود
        dgv.Paint += (s, e) =>
        {
            if (dgv.Rows.Count == 0) PaintEmptyState(dgv, e.Graphics);
        };

        return dgv;
    }

    private void PaintEmptyState(DataGridView dgv, Graphics g)
    {
        int top = dgv.ColumnHeadersHeight;
        var area = new Rectangle(0, top, dgv.ClientSize.Width, Math.Max(0, dgv.ClientSize.Height - top - 24));
        if (area.Width < 200 || area.Height < 150) return;

        bool nothingAtAll = _rows.Count == 0;
        string title = nothingAtAll
            ? $"برای {PeriodText()} هنوز حقوقی ثبت یا محاسبه نشده است"
            : "در این بخش پرسنلی وجود ندارد";
        string hint = nothingAtAll
            ? "برای شروع، دکمه‌ی «محاسبه همه» را بزنید؛ نتیجه را بررسی و سپس «ذخیره» کنید."
            : "پرسنل این ماه در تب‌های دیگر قرار دارند.";

        g.SmoothingMode = SmoothingMode.AntiAlias;
        int d = 76;
        int cx = area.X + area.Width / 2;
        int cy = area.Y + area.Height / 2 - 34;
        var circle = new Rectangle(cx - d / 2, cy - d / 2, d, d);
        using (var br = new SolidBrush(ModernPaint.Blend(Color.White, ModernTheme.Primary, 0.12f)))
            g.FillEllipse(br, circle);

        // آیکن لیست داخل دایره
        using (var pen = new Pen(ModernPaint.Darken(ModernTheme.Primary, 20), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            for (int i = 0; i < 3; i++)
            {
                int y = cy - 11 + i * 11;
                g.DrawLine(pen, cx - 4, y, cx + 14, y);
                g.DrawLine(pen, cx - 15, y, cx - 12, y);
            }
        }

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix |
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.Top;
        var titleRect = new Rectangle(area.X + 20, circle.Bottom + 16, area.Width - 40, 28);
        TextRenderer.DrawText(g, title, ModernTheme.GetFont(12.5f, FontStyle.Bold), titleRect, ModernTheme.TextStrong, flags);
        var hintRect = new Rectangle(area.X + 20, titleRect.Bottom + 4, area.Width - 40, 44);
        TextRenderer.DrawText(g, hint, ModernTheme.GetFont(10f), hintRect, ModernTheme.TextMuted, flags);
    }

    private string PeriodText()
    {
        int y = _periodYear, m = _periodMonth;
        if (y == 0 || m == 0) TryGetPeriod(out y, out m);
        return $"{TranslationHelper.GetMonthName(m)} {StringHelper.ConvertToPersianDigits(y.ToString())}";
    }

    private DataGridView ActiveGrid() => _tabs.SelectedIndex switch
    {
        1 => dgvDaily,
        2 => dgvHourly,
        _ => dgvContractual
    };

    // ═══════════════════════════════════════════════════════════════
    // نمایش / انتخاب
    // ═══════════════════════════════════════════════════════════════
    private void RebindGrids()
    {
        var ordered = _rows.Values.OrderBy(r => r.PersonnelCode).ThenBy(r => r.EmployeeName).ToList();

        var contractual = new List<PayrollResult>();
        var daily = new List<PayrollResult>();
        var hourly = new List<PayrollResult>();

        foreach (var r in ordered)
        {
            switch (PayrollCalculatorService.ResolveKind(r.PersonnelType))
            {
                case PayrollKind.Daily: daily.Add(r); break;
                case PayrollKind.Hourly: hourly.Add(r); break;
                default: contractual.Add(r); break;
            }
        }

        dgvContractual.DataSource = new BindingList<PayrollResult>(contractual);
        dgvDaily.DataSource = new BindingList<PayrollResult>(daily);
        dgvHourly.DataSource = new BindingList<PayrollResult>(hourly);

        _tabs.SetCount(0, contractual.Count);
        _tabs.SetCount(1, daily.Count);
        _tabs.SetCount(2, hourly.Count);

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int total = _rows.Count;
        int saved = _rows.Values.Count(r => r.IsSaved);
        int unsaved = total - saved;

        _stSaved.SetCount(saved, "نفر");
        _stSaved.SubText = total == 0
            ? "فیش ثبت‌شده در دیتابیس"
            : $"از {StringHelper.ConvertToPersianDigits(total.ToString())} نفر";

        _stUnsaved.SetCount(unsaved, "نفر");
        _stUnsaved.SubText = unsaved == 0 ? "موردی در انتظار ذخیره نیست" : "در انتظار ذخیره";

        _stNet.SetAmount(_rows.Values.Sum(r => r.NetPay), "ریال");
        _stNet.SubText = PeriodText();

        // پیام فقط وقتی لازم است نشان داده می‌شود؛ حالت «خالی» را خود جدول توضیح می‌دهد
        bool show = unsaved > 0;
        _infoBar.Message = show
            ? $"{StringHelper.ConvertToPersianDigits(unsaved.ToString())} ردیف زردرنگ محاسبه شده‌اند ولی هنوز ذخیره نشده‌اند؛ برای ثبت نهایی «ذخیره» را بزنید."
            : string.Empty;
        _infoBar.Visible = show;
        _mainLayout.RowStyles[4].Height = show ? 48F : 0F;

        foreach (var g in new[] { dgvContractual, dgvDaily, dgvHourly }) g.Invalidate();
    }

    /// <summary>
    /// ردیف‌های انتخاب‌شده: اگر کاربر تیک «انتخاب» زده باشد همان‌ها، وگرنه ردیف‌های هایلایت‌شده‌ی تب فعلی.
    /// </summary>
    private List<PayrollResult> GetSelectedResults()
    {
        var checkedRows = new List<PayrollResult>();
        foreach (var grid in new[] { dgvContractual, dgvDaily, dgvHourly })
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.DataBoundItem is PayrollResult pr && row.Cells["IsSelected"].Value is bool b && b)
                    checkedRows.Add(pr);
            }
        }
        if (checkedRows.Count > 0) return checkedRows;

        return ActiveGrid().SelectedRows
            .Cast<DataGridViewRow>()
            .Select(r => r.DataBoundItem)
            .OfType<PayrollResult>()
            .ToList();
    }

    private bool TryGetPeriod(out int year, out int month)
    {
        year = 0; month = 0;
        if (cmbYear.SelectedItem is null || cmbMonth.SelectedItem is null) return false;
        year = Convert.ToInt32(cmbYear.SelectedItem);
        month = Convert.ToInt32(cmbMonth.SelectedItem);
        return true;
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        // هنگام انجام کار، دکمه‌ها غیرفعال می‌شوند تا کلیک دوباره‌ی ناخواسته ممکن نباشد
        foreach (var b in new[] { btnCalculate, btnCalcMissing, btnRecalcSelected, btnDelete, btnSave })
            if (b is not null && !b.IsDisposed) b.Enabled = !busy;
    }

    // ═══════════════════════════════════════════════════════════════
    // بارگذاری فیش‌های ذخیره‌شده
    // ═══════════════════════════════════════════════════════════════
    private static decimal D(DataRow row, string col)
        => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? Convert.ToDecimal(row[col]) : 0m;

    private static PayrollResult MapSaved(DataRow row)
    {
        var saved = new PayrollResult
        {
            EmployeeId = Convert.ToInt32(row["EmployeeId"]),
            EmployeeName = row["EmployeeName"]?.ToString() ?? "",
            PersonnelCode = row["PersonnelCode"]?.ToString() ?? "",
            Year = Convert.ToInt32(row["Year"]),
            Month = Convert.ToInt32(row["Month"]),
            PersonnelType = row.Table.Columns.Contains("PersonnelType") && row["PersonnelType"] != DBNull.Value
                ? row["PersonnelType"].ToString() ?? "ماهانه" : "ماهانه",

            BaseSalary = D(row, "BaseSalary"),
            YearsBonus = D(row, "YearsBonus"),
            HousingAllowance = D(row, "HousingAllowance"),
            GroceryAllowance = D(row, "GroceryAllowance"),
            ChildAllowance = D(row, "ChildAllowance"),
            AttractionAllowance = D(row, "AttractionAllowance"),
            ResponsibilityAllowance = D(row, "ResponsibilityAllowance"),
            ManagementAllowance = D(row, "ManagementAllowance"),
            TechnicalAllowance = D(row, "TechnicalAllowance"),
            MarriageAllowance = D(row, "MarriageAllowance"),
            TransportationAllowance = D(row, "TransportationAllowance"),
            OtherAllowances = D(row, "OtherAllowances"),
            OvertimePay = D(row, "OvertimePay"),
            TotalEarnings = D(row, "TotalEarnings"),

            InsuranceBase = D(row, "InsuranceBase"),
            TaxableBase = D(row, "TaxableBase"),
            WorkingDays = D(row, "WorkingDays"),
            OvertimeHours = D(row, "OvertimeHours"),
            AbsenceHours = D(row, "AbsenceHours"),

            AbsenceDeduction = D(row, "AbsenceDeduction"),
            InsuranceEmployee = D(row, "InsuranceEmployee"),
            InsuranceEmployer = D(row, "InsuranceEmployer"),
            IncomeTax = D(row, "IncomeTax"),
            LoanDeduction = D(row, "LoanDeduction"),
            AdvanceDeduction = D(row, "AdvanceDeduction"),
            SupplementaryInsuranceDeduction = D(row, "SupplementaryInsuranceDeduction"),
            CarInsuranceDeduction = D(row, "CarInsuranceDeduction"),
            OtherDeductions = D(row, "OtherDeductions"),
            TotalDeductions = D(row, "TotalDeductions"),
            NetPay = D(row, "NetPay"),

            IsSaved = true
        };

        // «حقوق روزانه» در جدول Payrolls ذخیره نمی‌شود (فقط حقوق پایه و روزهای کارکرد)؛
        // برای روزمزد از همان دو مقدار برگردانده می‌شود: حقوق پایه = روزهای کارکرد × دستمزد روزانه
        if (saved.PersonnelType == "روزمزد" && saved.WorkingDays > 0 && saved.BaseSalary > 0)
            saved.DailyWage = Math.Round(saved.BaseSalary / saved.WorkingDays, 0);

        return saved;
    }

    private async Task LoadSavedAsync()
    {
        if (IsDisposed || _isBusy || !TryGetPeriod(out int year, out int month)) return;
        SetBusy(true);
        try
        {
            DataTable? dt = null;
            bool ok = await AsyncHelper.RunWithLoading(this, async () =>
            {
                dt = await _payrollRepo.GetSavedPayrollsAsync(year, month);
            }, "در حال بارگذاری حقوق ذخیره‌شده...");

            if (!ok || dt is null || IsDisposed) return;

            _rows.Clear();
            _savedDeductions.Clear();

            foreach (DataRow row in dt.Rows)
            {
                var r = MapSaved(row);
                _rows[r.EmployeeId] = r;
                _savedDeductions[r.EmployeeId] = (r.LoanDeduction, r.AdvanceDeduction, r.SupplementaryInsuranceDeduction, r.CarInsuranceDeduction);
            }

            _periodYear = year;
            _periodMonth = month;
            RebindGrids();
        }
        finally { SetBusy(false); }
    }

    private async Task OnPeriodChangedAsync()
    {
        if (!_formReady || _suppressPeriodChange || IsDisposed || _isBusy) return;
        if (!TryGetPeriod(out int year, out int month)) return;
        if (year == _periodYear && month == _periodMonth) return;

        if (_rows.Values.Any(r => !r.IsSaved))
        {
            if (!ModernMessageBox.AskConfirm(
                    "محاسبه‌های ذخیره‌نشده‌ی ماه فعلی (ردیف‌های زرد) از بین می‌روند.\n\nادامه می‌دهید؟",
                    "تغییر ماه"))
            {
                _suppressPeriodChange = true;
                try
                {
                    cmbYear.SelectedItem = _periodYear;
                    cmbMonth.SelectedItem = _periodMonth;
                }
                finally { _suppressPeriodChange = false; }
                return;
            }
        }

        await LoadSavedAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    // محاسبه
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// قسط ماهانه‌ی قابل کسر یک کارمند. دقیقاً هم‌منطق با PayrollRepository.DeductKindAsync:
    /// برای هر ردیف فعال min(قسط، مانده) (اگر قسط ۰ بود کل مانده) و جمع ردیف‌ها.
    /// </summary>
    private static decimal SumInstallment<T>(
        Dictionary<int, List<T>> map, int empId,
        Func<T, decimal> installment, Func<T, decimal> remaining)
    {
        if (!map.TryGetValue(empId, out var list) || list.Count == 0) return 0m;

        decimal sum = 0m;
        foreach (var x in list)
        {
            decimal rem = remaining(x);
            if (rem <= 0) continue;
            decimal inst = installment(x);
            sum += inst > 0 ? Math.Min(inst, rem) : rem;
        }
        return sum;
    }

    private async Task RecalculateSelectedAsync()
    {
        var selected = GetSelectedResults();
        if (selected.Count == 0)
        {
            ModernMessageBox.ShowWarning("ابتدا یک یا چند نفر را انتخاب کنید (تیک «انتخاب» یا هایلایت ردیف).");
            return;
        }
        await CalculateAsync(new HashSet<int>(selected.Select(r => r.EmployeeId)));
    }

    /// <param name="onlyEmpIds">null = همه‌ی پرسنل؛ در غیر این صورت فقط همین کارمندان محاسبه می‌شوند.</param>
    /// <param name="onlyMissing">true = فقط کارمندانی که الآن هیچ ردیفی (ذخیره‌شده یا محاسبه‌شده) در لیست ندارند (مثلاً بعد از حذف).</param>
    private async Task CalculateAsync(HashSet<int>? onlyEmpIds, bool onlyMissing = false)
    {
        if (IsDisposed || _isBusy || !TryGetPeriod(out int year, out int month)) return;

        // اگر دوره با دوره‌ی نمایش‌داده‌شده فرق دارد (نباید پیش بیاید) اول فیش‌های آن دوره بارگذاری شود
        if (year != _periodYear || month != _periodMonth)
        {
            await LoadSavedAsync();
            if (year != _periodYear || month != _periodMonth) return;
        }

        SetBusy(true);
        try
        {
            var newResults = new List<PayrollResult>();
            var overdueAdvances = new List<string>();   // مساعده‌های کسرنشده‌ی ماه‌های قبل (برای هشدار)

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var attRepo = new AttendanceRepository(_connectionString);
                var settingsRepo = new SalarySettingsRepository(_connectionString);
                var empRepo = new EmployeeRepository(_connectionString);
                var loanRepo = new LoanRepository(_connectionString);
                var suppInsRepo = new SupplementaryInsuranceRepository(_connectionString);
                var carInsRepo = new CarInsuranceRepository(_connectionString);

                var settings = await settingsRepo.GetByYearAsync(year);
                if (settings is null) throw new Exception($"تنظیمات حقوق سال {year} یافت نشد.");

                var attendances = await attRepo.GetAttendanceForMonthAsync(year, month);
                var employees = await empRepo.GetEmployeesWithDetailsAsync("", true, false);

                var activeLoans = await loanRepo.GetActiveLoansAsync();
                var activeSuppIns = await suppInsRepo.GetActiveInsurancesAsync();
                var activeCarIns = await carInsRepo.GetActiveCarInsurancesAsync();

                // وام و مساعده جدا از هم محاسبه می‌شوند
                var loansByEmp = activeLoans.Where(l => !l.IsAdvance).GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
                // مساعده فقط در ماه شروع خودش کسر می‌شود
                var advByEmp = activeLoans.Where(l => l.IsAdvance && l.IsDueInPeriod(year, month))
                                          .GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
                foreach (var l in activeLoans.Where(l => l.IsAdvance && l.RemainingAmount > 0 && l.IsOverdueBefore(year, month)))
                    overdueAdvances.Add($"• {l.EmployeeName} — {NumberHelper.FormatThousands(l.RemainingAmount, usePersianDigits: true)} ریال (ماه شروع: {l.StartPeriod})");
                var suppByEmp = activeSuppIns.GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
                var carByEmp = activeCarIns.GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

                var taxBrackets = (settings.TaxBrackets is not null && settings.TaxBrackets.Count > 0)
                    ? settings.TaxBrackets
                    : _calculator.GetTaxBracketsSafe(year);

                var empById = employees.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First());

                // روزمزدها: فقط کسانی که در همین ماه حکم دارند و کارکرد ثبت‌شده دارند محاسبه می‌شوند
                var pc = new System.Globalization.PersianCalendar();
                DateTime monthStart = pc.ToDateTime(year, month, 1, 0, 0, 0, 0);
                DateTime monthEnd = pc.ToDateTime(year, month, pc.GetDaysInMonth(year, month), 23, 59, 59, 0);
                var contractRepo = new EmployeeContractRepository(_connectionString);
                var idsWithDecree = await contractRepo.GetEmployeeIdsWithContractInRangeAsync(monthStart, monthEnd);

                foreach (var att in attendances)
                {
                    if (onlyEmpIds is not null && !onlyEmpIds.Contains(att.EmployeeId)) continue;
                    if (onlyMissing && _rows.ContainsKey(att.EmployeeId)) continue;
                    if (!empById.TryGetValue(att.EmployeeId, out var emp)) continue;

                    if (PayrollCalculatorService.ResolveKind(emp.PersonnelType) == PayrollKind.Daily)
                    {
                        // بدون حکم در این ماه → محاسبه نشود
                        if (!idsWithDecree.Contains(emp.Id)) continue;
                        // بدون رکورد حضور و غیاب ثبت‌شده، یا با جمع روزهای کارکرد صفر → محاسبه نشود
                        // (اگر رکوردی نباشد، کوئری حضور و غیاب به‌صورت پیش‌فرض ۳۰ روز می‌گذارد؛ این حالت کارکرد واقعی نیست)
                        if (att.Id <= 0 || att.WorkingDays + att.HolidayDays <= 0) continue;
                    }

                    try
                    {
                        decimal loanInst, advInst, suppInst, carInst;

                        // ✅ اگر این کارمند الآن فیش ذخیره‌شده دارد، اقساطِ همان فیش مبنا قرار می‌گیرد؛
                        //    چون مانده‌ی وام/بیمه قبلاً با همان فیش کم شده و ذخیره‌ی مجدد اول آن را برمی‌گرداند.
                        //    (برای اعمال تغییر جدید وام، ابتدا فیش را «حذف» کنید و بعد محاسبه بزنید.)
                        if (_savedDeductions.TryGetValue(emp.Id, out var sd))
                        {
                            loanInst = sd.Loan;
                            advInst = sd.Advance;
                            suppInst = sd.Supp;
                            carInst = sd.Car;
                        }
                        else
                        {
                            loanInst = SumInstallment(loansByEmp, emp.Id, l => l.MonthlyInstallment, l => l.RemainingAmount);
                            advInst = SumInstallment(advByEmp, emp.Id, l => l.MonthlyInstallment, l => l.RemainingAmount);
                            suppInst = SumInstallment(suppByEmp, emp.Id, i => i.MonthlyInstallment, i => i.RemainingAmount);
                            carInst = SumInstallment(carByEmp, emp.Id, i => i.MonthlyInstallment, i => i.RemainingAmount);
                        }

                        var result = _calculator.CalculatePayroll(emp, att, settings, loanInst, suppInst, carInst, 0m, taxBrackets, advInst);
                        result.IsSaved = false;
                        newResults.Add(result);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogException(ex, $"Payroll_Calc_{emp.PersonnelCode}");
                    }
                }
            }, "در حال محاسبه حقوق...");

            if (!success || IsDisposed) return;

            if (onlyMissing && newResults.Count == 0)
            {
                ToastHelper.Show("همه‌ی پرسنل این ماه در لیست هستند؛ فرد جدیدی برای محاسبه پیدا نشد.", false);
                return;
            }

            // ردیف‌های جدید جای ردیف قبلی همان کارمند را می‌گیرند؛ بقیه دست‌نخورده می‌مانند
            foreach (var r in newResults)
                _rows[r.EmployeeId] = r;

            RebindGrids();

            string msg = onlyMissing
                ? $"حقوق {newResults.Count} نفر که در لیست نبودند محاسبه شد. برای اعمال، «ذخیره» را بزنید."
                : onlyEmpIds is null
                ? "محاسبه همه انجام شد. ردیف‌های زرد را بررسی و «ذخیره» کنید."
                : $"محاسبه مجدد {newResults.Count} نفر انجام شد. برای اعمال، «ذخیره» را بزنید.";
            ToastHelper.Show(msg, true);

            if (overdueAdvances.Count > 0)
            {
                string list = string.Join("\n", overdueAdvances.Take(12));
                if (overdueAdvances.Count > 12) list += $"\n… و {overdueAdvances.Count - 12} مورد دیگر";
                ModernMessageBox.ShowWarning(
                    "مساعده‌ی کسرنشده وجود دارد!\n\n" +
                    "این مساعده‌ها در ماه شروع خودشان کسر نشده‌اند و در حقوق این ماه هم کسر نمی‌شوند:\n\n" + list + "\n\n" +
                    "اگر می‌خواهید از همین ماه کسر شوند، در «مدیریت وام و مساعده» ماه شروع آن‌ها را عوض کنید و حقوق را دوباره محاسبه کنید.");
            }
        }
        finally { SetBusy(false); }
    }

    // ═══════════════════════════════════════════════════════════════
    // ذخیره
    // ═══════════════════════════════════════════════════════════════
    private async Task SaveSafeAsync()
    {
        if (IsDisposed || _isBusy) return;

        var toSave = _rows.Values.Where(r => !r.IsSaved).ToList();
        if (toSave.Count == 0 || _periodYear == 0 || _periodMonth == 0)
        {
            ModernMessageBox.ShowWarning("محاسبه‌ی ذخیره‌نشده‌ای وجود ندارد. ابتدا «محاسبه همه» یا «محاسبه مجدد انتخاب‌شده‌ها» را بزنید.");
            return;
        }

        int year = _periodYear;
        int month = _periodMonth;

        int overwrite = toSave.Count(r => _savedDeductions.ContainsKey(r.EmployeeId));
        if (overwrite > 0)
        {
            if (!ModernMessageBox.AskConfirm(
                    $"برای {overwrite} نفر از این افراد در {TranslationHelper.GetMonthName(month)} {year} قبلاً فیش ذخیره شده است.\n\n" +
                    "با ادامه، فیش همین افراد بازنویسی می‌شود و اقساط قبلی آن‌ها اول برگردانده و سپس دوباره کسر می‌شود.\n\nادامه می‌دهید؟",
                    "بازنویسی فیش‌ها"))
                return;
        }

        SetBusy(true);
        bool success;
        try
        {
            success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _payrollRepo.SavePayrollBatchAsync(toSave, year, month);
            }, "در حال ذخیره در دیتابیس...");
        }
        finally { SetBusy(false); }

        if (!success || IsDisposed) return;

        ToastHelper.Show($"حقوق {toSave.Count} نفر با موفقیت ذخیره شد.", true);

        // لیست از دیتابیس دوباره خوانده می‌شود تا همه ردیف‌ها «ذخیره‌شده» و اقساط مبنا به‌روز شود
        await LoadSavedAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    // حذف
    // ═══════════════════════════════════════════════════════════════
    private async Task DeleteSelectedAsync()
    {
        if (IsDisposed || _isBusy) return;

        var selected = GetSelectedResults();
        if (selected.Count == 0)
        {
            ModernMessageBox.ShowWarning("ابتدا یک یا چند نفر را انتخاب کنید (تیک «انتخاب» یا هایلایت ردیف).");
            return;
        }

        var inDb = selected.Where(r => _savedDeductions.ContainsKey(r.EmployeeId)).ToList();
        int onlyOnScreen = selected.Count - inDb.Count;

        string names = string.Join("، ", selected.Take(5).Select(r => r.EmployeeName));
        if (selected.Count > 5) names += $" و {selected.Count - 5} نفر دیگر";

        string msg = $"حذف حقوق {selected.Count} نفر:\n{names}\n\n";
        if (inDb.Count > 0)
            msg += $"• فیش ذخیره‌شده‌ی {inDb.Count} نفر از دیتابیس حذف می‌شود و اقساط (وام/بیمه) کسرشده‌ی آن‌ها برگردانده می‌شود.\n";
        if (onlyOnScreen > 0)
            msg += $"• محاسبه‌ی ذخیره‌نشده‌ی {onlyOnScreen} نفر از لیست کنار گذاشته می‌شود.\n";
        msg += "\nادامه می‌دهید؟";

        if (!ModernMessageBox.AskConfirm(msg, "حذف حقوق")) return;

        int year = _periodYear;
        int month = _periodMonth;

        SetBusy(true);
        try
        {
            bool ok = await AsyncHelper.RunWithLoading(this, async () =>
            {
                foreach (var r in inDb)
                    await _payrollRepo.DeletePayrollForEmployeeAsync(r.EmployeeId, year, month);
            }, "در حال حذف...");

            if (!ok || IsDisposed) return;

            foreach (var r in selected)
            {
                _rows.Remove(r.EmployeeId);
                _savedDeductions.Remove(r.EmployeeId);
            }

            RebindGrids();
            ToastHelper.Show($"حقوق {selected.Count} نفر حذف شد. برای محاسبه‌ی دوباره از «محاسبه افراد بدون حقوق» استفاده کنید.", true);
        }
        finally { SetBusy(false); }
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  کنترل‌های اختصاصی ظاهر فرم «پردازش حقوق ماهانه»
//  (همه با رنگ و فونت ModernTheme؛ هیچ تغییری در کنترل‌های مشترک پروژه لازم نیست)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>تب‌های قطعه‌ای (Segmented) با شمارنده‌ی نفرات؛ تب اول در راست</summary>
internal sealed class PayrollSegmentTabs : Control
{
    private readonly List<string> _titles = new();
    private readonly List<int> _counts = new();
    private int _selected;
    private int _hot = -1;

    public event EventHandler? SelectedIndexChanged;

    public PayrollSegmentTabs()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Height = 44;
        RightToLeft = RightToLeft.Yes;
    }

    public void AddTab(string title) { _titles.Add(title); _counts.Add(0); Invalidate(); }

    public void SetCount(int index, int count)
    {
        if (index < 0 || index >= _counts.Count) return;
        _counts[index] = count;
        Invalidate();
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value < 0 || value >= _titles.Count || value == _selected) return;
            _selected = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private Rectangle TabRect(int i)
    {
        int n = _titles.Count;
        if (n == 0) return Rectangle.Empty;
        const int pad = 4;
        int w = (Width - pad * 2) / n;
        int x = Width - pad - (i + 1) * w;               // تب اول سمت راست
        return new Rectangle(x, pad, w, Height - pad * 2);
    }

    private int HitTest(Point p)
    {
        for (int i = 0; i < _titles.Count; i++)
            if (TabRect(i).Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = HitTest(e.Location);
        if (h != _hot) { _hot = h; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hot != -1) { _hot = -1; Invalidate(); }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            int h = HitTest(e.Location);
            if (h >= 0) SelectedIndex = h;
        }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ModernPaint.BehindColor(this));

        var track = new Rectangle(0, 0, Width - 1, Height - 1);
        if (track.Width < 40 || track.Height < 24) return;

        using (var path = ModernPaint.RoundedRect(track, 12))
        {
            using var br = new SolidBrush(ModernTheme.SurfaceMuted);
            g.FillPath(br, path);
            using var pen = new Pen(ModernTheme.Border, 1f);
            g.DrawPath(pen, path);
        }

        var titleFont = ModernTheme.GetFont(10f, FontStyle.Bold);
        var badgeFont = ModernTheme.GetFont(8.5f, FontStyle.Bold);
        const TextFormatFlags measure = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        const TextFormatFlags draw = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine |
                                     TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft;

        for (int i = 0; i < _titles.Count; i++)
        {
            var rect = Rectangle.Inflate(TabRect(i), -1, -1);
            bool sel = i == _selected;
            bool hot = i == _hot && !sel;

            if (sel)
            {
                using var shadow = ModernPaint.RoundedRect(new Rectangle(rect.X, rect.Y + 1, rect.Width, rect.Height), 9);
                using (var sb = new SolidBrush(Color.FromArgb(28, 60, 50, 45))) g.FillPath(sb, shadow);
                using var pill = ModernPaint.RoundedRect(rect, 9);
                using (var br = new SolidBrush(ModernTheme.Surface)) g.FillPath(br, pill);
                using (var pen = new Pen(ModernTheme.Border, 1f)) g.DrawPath(pen, pill);
            }
            else if (hot)
            {
                using var pill = ModernPaint.RoundedRect(rect, 9);
                using var br = new SolidBrush(ModernPaint.Blend(ModernTheme.SurfaceMuted, Color.White, 0.55f));
                g.FillPath(br, pill);
            }

            string title = _titles[i];
            string count = StringHelper.ConvertToPersianDigits(_counts[i].ToString());

            int titleW = TextRenderer.MeasureText(g, title, titleFont, new Size(int.MaxValue, rect.Height), measure).Width;
            int countW = TextRenderer.MeasureText(g, count, badgeFont, new Size(int.MaxValue, rect.Height), measure).Width;
            int badgeW = Math.Max(24, countW + 14), badgeH = 20, gap = 8;
            int total = titleW + gap + badgeW;
            int startX = rect.X + (rect.Width - total) / 2;

            // عنوان سمت راست، شمارنده سمت چپ آن
            var titleRect = new Rectangle(startX + badgeW + gap, rect.Y, titleW + 2, rect.Height);
            TextRenderer.DrawText(g, title, titleFont, titleRect,
                sel ? ModernTheme.TextStrong : ModernTheme.TextMuted, draw);

            var badge = new Rectangle(startX, rect.Y + (rect.Height - badgeH) / 2, badgeW, badgeH);
            Color bFill = sel ? ModernTheme.Primary : ModernPaint.Blend(Color.White, ModernTheme.Neutral, 0.20f);
            Color bText = sel ? Color.White : ModernPaint.Darken(ModernTheme.Neutral, 30);
            using (var bp = ModernPaint.RoundedRect(badge, 10))
            using (var bb = new SolidBrush(bFill))
                g.FillPath(bb, bp);
            TextRenderer.DrawText(g, count, badgeFont, badge, bText, draw);
        }
    }
}

/// <summary>راهنمای رنگ ردیف‌ها (ذخیره‌شده / ذخیره‌نشده) کنار تب‌ها</summary>
internal sealed class PayrollLegend : Control
{
    public PayrollLegend()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        RightToLeft = RightToLeft.Yes;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ModernPaint.BehindColor(this));

        var font = ModernTheme.GetFont(9f);
        var items = new[]
        {
            ("ذخیره‌شده", ModernTheme.Success),
            ("ذخیره‌نشده (زرد)", ModernTheme.Warning)
        };

        int x = Width - 4;   // از راست شروع می‌شود
        int cy = Height / 2 - 4;
        foreach (var (text, color) in items)
        {
            int tw = TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, Height),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;

            var dot = new Rectangle(x - 10, cy - 5, 10, 10);
            using (var br = new SolidBrush(color)) g.FillEllipse(br, dot);

            var tr = new Rectangle(dot.Left - 6 - tw - 2, 0, tw + 2, Height - 8);
            TextRenderer.DrawText(g, text, font, tr, ModernTheme.TextMuted,
                TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            x = tr.Left - 18;
        }
    }
}

/// <summary>نوار پیام نرم (هشدار) با آیکن و متن راست‌چین</summary>
internal sealed class PayrollInfoBar : Control
{
    private string _message = string.Empty;

    public PayrollInfoBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        RightToLeft = RightToLeft.Yes;
    }

    public string Message { get => _message; set { _message = value ?? string.Empty; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ModernPaint.BehindColor(this));

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (rect.Width < 40 || rect.Height < 20 || _message.Length == 0) return;

        Color accent = ModernTheme.Warning;
        using (var path = ModernPaint.RoundedRect(rect, 10))
        {
            using var br = new SolidBrush(ModernPaint.Blend(Color.White, accent, 0.14f));
            g.FillPath(br, path);
            using var pen = new Pen(ModernPaint.Blend(Color.White, accent, 0.50f), 1f);
            g.DrawPath(pen, path);
        }

        int d = 22;
        var icon = new Rectangle(rect.Right - 14 - d, rect.Y + (rect.Height - d) / 2, d, d);
        using (var br = new SolidBrush(accent)) g.FillEllipse(br, icon);
        TextRenderer.DrawText(g, "!", ModernTheme.GetFont(10f, FontStyle.Bold), icon, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

        var tr = new Rectangle(rect.X + 14, rect.Y, icon.Left - 10 - rect.X - 14, rect.Height);
        TextRenderer.DrawText(g, _message, ModernTheme.GetFont(10f, FontStyle.Bold), tr, ModernTheme.WarningText,
            TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

internal enum PayrollIcon { Calc, Plus, Refresh, Trash, Save, Search, Export }

/// <summary>آیکن‌های خطی ساده که با GDI+ رسم می‌شوند (۳۲×۳۲، رنگ دلخواه)</summary>
internal static class PayrollIcons
{
    public static Bitmap Make(PayrollIcon kind, Color color)
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var pen = new Pen(color, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var br = new SolidBrush(color);

        switch (kind)
        {
            case PayrollIcon.Calc:
                using (var p = ModernPaint.RoundedRect(new Rectangle(6, 3, 20, 26), 4)) g.DrawPath(pen, p);
                g.DrawLine(pen, 11, 10, 21, 10);
                foreach (int y in new[] { 17, 23 })
                    foreach (int x in new[] { 11, 16, 21 })
                        g.FillEllipse(br, x - 1.7f, y - 1.7f, 3.4f, 3.4f);
                break;

            case PayrollIcon.Plus:
                g.DrawLine(pen, 16, 7, 16, 25);
                g.DrawLine(pen, 7, 16, 25, 16);
                break;

            case PayrollIcon.Refresh:
            {
                g.DrawArc(pen, new RectangleF(6, 6, 20, 20), 40f, 280f);
                double a = 320 * Math.PI / 180.0;
                float cx = 16 + 10 * (float)Math.Cos(a), cy = 16 + 10 * (float)Math.Sin(a);
                float tx = (float)(-Math.Sin(a)), ty = (float)Math.Cos(a);   // مماس در جهت حرکت
                float nx = -ty, ny = tx;
                var tip = new PointF(cx + tx * 6, cy + ty * 6);
                var b1 = new PointF(cx + nx * 5, cy + ny * 5);
                var b2 = new PointF(cx - nx * 5, cy - ny * 5);
                g.FillPolygon(br, new[] { tip, b1, b2 });
                break;
            }

            case PayrollIcon.Trash:
                g.DrawLine(pen, 6, 9, 26, 9);
                g.DrawLines(pen, new[] { new PointF(12, 9), new PointF(12, 5), new PointF(20, 5), new PointF(20, 9) });
                g.DrawLines(pen, new[] { new PointF(9, 9), new PointF(10.5f, 27), new PointF(21.5f, 27), new PointF(23, 9) });
                g.DrawLine(pen, 14, 13, 14, 23);
                g.DrawLine(pen, 18, 13, 18, 23);
                break;

            case PayrollIcon.Search:
                g.DrawEllipse(pen, 5f, 5f, 16f, 16f);
                g.DrawLine(pen, 19, 19, 27, 27);
                break;

            case PayrollIcon.Export:
                g.DrawLine(pen, 16, 5, 16, 19);
                g.DrawLines(pen, new[] { new PointF(10, 13), new PointF(16, 19), new PointF(22, 13) });
                g.DrawLines(pen, new[] { new PointF(6, 22), new PointF(6, 27), new PointF(26, 27), new PointF(26, 22) });
                break;

            case PayrollIcon.Save:
                using (var p = ModernPaint.RoundedRect(new Rectangle(5, 5, 22, 22), 4)) g.DrawPath(pen, p);
                g.DrawRectangle(pen, 10, 5, 11, 7);
                g.DrawRectangle(pen, 9, 18, 14, 9);
                break;
        }
        return bmp;
    }
}
