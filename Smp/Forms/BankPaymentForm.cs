using Smp.Data;
using Smp.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class BankPaymentForm : Form
{
    private readonly PayrollRepository _repository;

    private ComboBox cmbYear = null!;
    private ComboBox cmbMonth = null!;
    private Button btnLoad = null!;
    private Button btnExport = null!;
    private DataGridView dgvBankList = null!;
    private Label lblCount = null!;
    private Label lblEmpty = null!;
    private ModernStatCard _stCount = null!, _stTotal = null!;

    private DataTable? _currentData;

    public BankPaymentForm(string connectionString)
    {
        _repository = new PayrollRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("فایل واریز حقوق به بانک", new Size(1100, 700), isDialog: false);
        ShowInTaskbar = false;

        LoadDefaults();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnLoad); // Ctrl+S → Load
    }

    // ==========================================================
    // 📅 آیتم ماه (نمایش نام ماه، نگهداری شماره ماه)
    // ==========================================================
    private sealed class MonthItem
    {
        public int Number { get; }
        public MonthItem(int number) { Number = number; }
        public override string ToString()
            => $"{StringHelper.ConvertToPersianDigits(Number.ToString())} - {TranslationHelper.GetMonthName(Number)}";
    }

    private int SelectedYear => cmbYear.SelectedItem is null ? 0 : Convert.ToInt32(cmbYear.SelectedItem);
    private int SelectedMonth => cmbMonth.SelectedItem is MonthItem mi ? mi.Number : 0;

    // ==========================================================
    // 🎯 مقدار پیش‌فرض (سال و ماه جاری شمسی)
    // ==========================================================
    private void LoadDefaults()
    {
        int y = 0, m = 1;
        try
        {
            var pc = new PersianCalendar();
            y = pc.GetYear(DateTime.Now);
            m = pc.GetMonth(DateTime.Now);
        }
        catch { /* مقدار پیش‌فرض پایین استفاده می‌شود */ }

        if (y > 0 && cmbYear.Items.Contains(y)) cmbYear.SelectedItem = y;
        else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = cmbYear.Items.Count - 1;

        if (m >= 1 && m <= 12) cmbMonth.SelectedIndex = m - 1;
        else if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
    }

    // ==========================================================
    // 🎨 رابط کاربری
    // ==========================================================
    private void BuildUI()
    {
        // ---- ورودی‌ها ----
        cmbYear = ModernUI.Combo();
        ComboBoxHelper.FillYears(cmbYear);
        cmbYear.Anchor = AnchorStyles.None;
        cmbYear.Size = new Size(120, ModernUI.InputHeight);

        cmbMonth = ModernUI.Combo();
        for (int m = 1; m <= 12; m++) cmbMonth.Items.Add(new MonthItem(m));
        cmbMonth.Anchor = AnchorStyles.None;
        cmbMonth.Size = new Size(170, ModernUI.InputHeight);

        Label FieldLabel(string t) => new Label
        {
            Text = t, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
            ForeColor = ModernTheme.TextMuted, BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 6, 0)
        };

        btnLoad = ModernButton.Create("🔍  نمایش لیست", ModernTheme.Primary, 150, 36);
        btnLoad.Anchor = AnchorStyles.None;
        btnLoad.Click += BtnLoad_Click;

        btnExport = ModernButton.Soft("📥  خروجی اکسل بانک", ModernTheme.Success, 190, 36);
        btnExport.Anchor = AnchorStyles.None;
        btnExport.Enabled = false;
        btnExport.Click += BtnExport_Click;

        // ---- نوار فیلتر: جدول با ستون‌های ثابت (ستون ۰ = راست‌ترین) ----
        //  [سال] [ترکیب سال] [ماه] [کمبوی ماه] [نمایش لیست] ........ [خروجی اکسل]
        var bar = new BufferedTable
        {
            Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1,
            RightToLeft = RightToLeft.Yes, BackColor = Color.Transparent, Margin = Padding.Empty
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44F));    // برچسب سال
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));   // سال
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52F));    // برچسب ماه
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));   // ماه
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));   // نمایش لیست
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));    // فاصله
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));   // خروجی اکسل
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        bar.Controls.Add(FieldLabel("سال"), 0, 0);
        bar.Controls.Add(cmbYear, 1, 0);
        bar.Controls.Add(FieldLabel("ماه"), 2, 0);
        bar.Controls.Add(cmbMonth, 3, 0);
        bar.Controls.Add(btnLoad, 4, 0);
        bar.Controls.Add(btnExport, 6, 0);

        var topCard = new ModernCard("", ModernTheme.Primary, 1) { Dock = DockStyle.Fill };
        topCard.Body.Padding = new Padding(14, 6, 20, 6);
        topCard.Body.Controls.Add(bar, 0, 0);

        // ---- کارت‌های خلاصه ----
        var strip = ModernReport.CreateStatStrip(2, 96);
        _stCount = strip.Add("تعداد واریزها", ModernTheme.Primary, "کارکنان دارای فیش حقوقی");
        _stTotal = strip.Add("جمع کل مبلغ واریزی", ModernTheme.Success, "مجموع خالص پرداختی");
        _stCount.ValueFontSize = 16f;
        _stTotal.ValueFontSize = 16f;
        _stCount.SetCount(0, "نفر");
        _stTotal.SetAmount(0, "ریال");

        // ---- گرید ----
        dgvBankList = new DataGridView { Dock = DockStyle.Fill };
        dgvBankList.ApplyModernStyle(); // + Enter-Nav، ترجمه هدرها
        dgvBankList.UseAiryLook(44);
        dgvBankList.UseSelectionAccent(ModernTheme.Primary);
        dgvBankList.DataBindingComplete += (s, e) => FormatGrid();

        // پیام حالت خالی (وسط جدول)
        lblEmpty = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(11f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = ModernTheme.Surface,
            Text = "📄\n\nبرای مشاهده‌ی لیست واریز، سال و ماه را انتخاب کنید\nو روی «نمایش لیست» بزنید."
        };
        var gridHost = ModernReport.WrapGrid(dgvBankList);
        gridHost.Controls.Add(lblEmpty);
        lblEmpty.BringToFront();

        // ---- پیام پایین ----
        lblCount = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ModernTheme.TextMuted,
            Font = ModernTheme.GetFont(9.5f),
            BackColor = Color.Transparent,
            Padding = new Padding(8, 0, 8, 0)
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(16, 14, 16, 6) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        strip.Dock = DockStyle.Fill;
        strip.Padding = Padding.Empty;
        topCard.Margin = new Padding(0, 0, 0, 12);
        strip.Margin = new Padding(0, 0, 0, 12);
        root.Controls.Add(topCard, 0, 0);
        root.Controls.Add(strip, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        root.Controls.Add(lblCount, 0, 3);
        Controls.Add(root);
    }

    /// <summary>قالب‌بندی ستون‌های جدول (مبلغ با هزارگان، کد ملی/شبا چپ‌به‌راست)</summary>
    private void FormatGrid()
    {
        foreach (DataGridViewColumn col in dgvBankList.Columns)
        {
            switch (col.DataPropertyName)
            {
                case "NetPay":
                    col.DefaultCellStyle.Format = "N0";
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    col.DefaultCellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
                    col.DefaultCellStyle.ForeColor = ModernTheme.TextStrong;
                    break;
                case "NationalCode":
                case "ShebaNumber":
                case "BankCode":
                case "EmployeeId":
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    break;
            }
        }
    }

    private void UpdateStats(DataTable? dt)
    {
        _stCount.SetCount(dt?.Rows.Count ?? 0, "نفر");
        decimal total = 0;
        if (dt is not null && dt.Columns.Contains("NetPay"))
            foreach (DataRow r in dt.Rows)
                if (r["NetPay"] != DBNull.Value) total += Convert.ToDecimal(r["NetPay"]);
        _stTotal.SetAmount(total, "ریال");
    }

    // ==========================================================
    // 📥 بارگذاری
    // ==========================================================
    private async void BtnLoad_Click(object? sender, EventArgs e)
    {
        if (SelectedYear == 0 || SelectedMonth == 0)
        {
            ModernMessageBox.ShowWarning("لطفاً سال و ماه را انتخاب کنید.");
            return;
        }

        int year = SelectedYear;
        int month = SelectedMonth;

        DataTable? data = null;
        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            data = await _repository.GetBankPaymentListAsync(year, month);
        }, "در حال بارگذاری لیست واریز...");

        if (!success) return;

        _currentData = data;

        if (_currentData is null || _currentData.Rows.Count == 0)
        {
            dgvBankList.DataSource = null;
            lblEmpty.Text = $"📭\n\nبرای {TranslationHelper.GetMonthName(month)} {StringHelper.ConvertToPersianDigits(year.ToString())} هیچ فیش حقوقی ذخیره نشده است.";
            lblEmpty.Visible = true;
            btnExport.Enabled = false;
            lblCount.Text = "هیچ رکوردی یافت نشد.";
            UpdateStats(null);
            _stCount.SubText = _stTotal.SubText = "";
            ModernMessageBox.ShowWarning("برای این ماه هیچ فیش حقوقی ذخیره نشده است.");
            return;
        }

        dgvBankList.DataSource = _currentData;
        lblEmpty.Visible = false;
        UpdateStats(_currentData);
        string period = $"{TranslationHelper.GetMonthName(month)} {StringHelper.ConvertToPersianDigits(year.ToString())}";
        _stCount.SubText = $"دوره‌ی {period}";
        _stTotal.SubText = $"مجموع خالص پرداختی {period}";
        btnExport.Enabled = true;

        int count = _currentData.Rows.Count;
        lblCount.Text = $"{NumberHelper.FormatThousands(count)} رکورد";
        ToastHelper.Show(
            $"{NumberHelper.FormatThousands(count)} رکورد بارگذاری شد.",
            isSuccess: true);
    }

    // ==========================================================
    // 📤 خروجی اکسل (روی thread جداگانه)
    // ==========================================================
    private async void BtnExport_Click(object? sender, EventArgs e)
    {
        if (_currentData is null || _currentData.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("ابتدا لیست را بارگذاری کنید.");
            return;
        }

        // نام فایل ایمن (اگر SelectedItem خالی بود، پیش‌فرض)
        string yearStr = SelectedYear > 0 ? SelectedYear.ToString() : "0000";
        string monthStr = SelectedMonth > 0 ? SelectedMonth.ToString("00") : "00";

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            FileName = $"BankPayment_{yearStr}_{monthStr}.xlsx",
            Title = "ذخیره فایل واریز بانک",
            OverwritePrompt = true
        };

        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            // ExportDataGridViewToExcel هم UI را لمس می‌کند هم I/O می‌کند.
            // برای سادگی و امنیت، کل کار را روی UI thread نگه می‌داریم
            // ولی با overlay تا کاربر ببیند چه اتفاقی می‌افتد.
            await Task.Yield(); // اجازه بده overlay رسم شود
            ExcelExportHelper.ExportDataGridViewToExcel(dgvBankList, sfd.FileName, "واریز حقوق");
        }, "در حال تولید فایل اکسل...");

        if (!success) return;

        ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true);

        // پرسش باز کردن فایل
        if (ModernMessageBox.AskConfirm(
            "فایل اکسل ذخیره شد.\nآیا می‌خواهید آن را باز کنید؟", "باز کردن فایل"))
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "BankPaymentForm.OpenFile");
                ModernMessageBox.ShowWarning("فایل ذخیره شد اما باز کردن آن ناموفق بود:\n" + ex.Message);
            }
        }
    }
}
