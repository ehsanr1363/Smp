using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Models;
using Smp.Helpers;
using Smp.Data;

namespace Smp.Forms;

public class ProfitLossDashboardForm : Form
{
    private readonly string _connectionString;

    private ModernTextBox txtFromDate = null!, txtToDate = null!;
    private ModernStatCard cardClientContract = null!, cardClientInvoiced = null!, cardClientReceived = null!;
    private ModernStatCard cardContractorContract = null!, cardContractorInvoiced = null!, cardContractorPaid = null!;
    private ModernStatCard cardGrossProfit = null!, cardNetCashFlow = null!;
    private ModernButton btnFilter = null!, btnClear = null!, btnRefresh = null!;

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private const string GrossBasis = "درآمد صورت‌وضعیت − (پیمانکار + خرید مستقیم + فاکتورها)";
    private const string CashBasis = "دریافتی − (پرداختی پیمانکار + اداری + خرید مستقیم + فاکتورها)";

    public ProfitLossDashboardForm(string connectionString)
    {
        _connectionString = connectionString;

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);   // Ctrl+S = بروزرسانی

        Shown += async (s, e) => await LoadDataSafeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ProfitLossDashboardForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── نوار فیلتر: بازه‌ی تاریخ + دکمه‌ها ──
        txtFromDate = MakeDateInput();
        txtToDate = MakeDateInput();

        btnFilter = ModernButton.Primary("🔍 اعمال فیلتر", 130, 36);
        btnFilter.Click += async (s, e) => await LoadDataSafeAsync();

        btnClear = ModernButton.Outline("✖ حذف فیلتر", ModernTheme.Neutral, 120, 36);
        btnClear.Click += async (s, e) => { txtFromDate.Text = string.Empty; txtToDate.Text = string.Empty; await LoadDataSafeAsync(); };

        btnRefresh = ModernButton.Soft("🔄 بروزرسانی", ModernTheme.Primary, 140, 36);
        btnRefresh.Click += async (s, e) => await LoadDataSafeAsync();

        var toolbar = ModernListKit.BuildToolbar(
            (ModernListKit.FilterLabel("از تاریخ"), 0), (txtFromDate, 130),
            (ModernListKit.FilterLabel("تا تاریخ"), 0), (txtToDate, 130),
            (btnFilter, 138), (btnClear, 128), (btnRefresh, 148));

        // ── کارت درآمدها ──
        var incomeCard = new ModernCard("📈 درآمدها (قراردادهای کارفرما)", ModernTheme.Success, 3);
        cardClientContract = AddStat(incomeCard, "مبلغ کل قراردادها", ModernTheme.Success, 0);
        cardClientInvoiced = AddStat(incomeCard, "مجموع صورت‌وضعیت‌ها", ModernTheme.Success, 1);
        cardClientReceived = AddStat(incomeCard, "مجموع دریافتی‌ها", ModernTheme.Success, 2);

        // ── کارت هزینه‌ها ──
        var expenseCard = new ModernCard("📉 هزینه‌ها (قراردادهای پیمانکار)", ModernTheme.Danger, 3);
        cardContractorContract = AddStat(expenseCard, "مبلغ کل قراردادها", ModernTheme.Danger, 0);
        cardContractorInvoiced = AddStat(expenseCard, "مجموع صورت‌وضعیت‌ها", ModernTheme.Danger, 1);
        cardContractorPaid = AddStat(expenseCard, "مجموع پرداختی‌ها", ModernTheme.Danger, 2);

        // ── کارت خلاصه سود و زیان ──
        var profitCard = new ModernCard("💰 خلاصه سود و زیان", ModernTheme.Purple, 2);
        cardGrossProfit = AddStat(profitCard, "سود/زیان ناخالص", ModernTheme.Neutral, 0, 18f, GrossBasis);
        cardNetCashFlow = AddStat(profitCard, "سود/زیان نقدی (پول در دست)", ModernTheme.Neutral, 1, 18f, CashBasis);

        incomeCard.Margin = new Padding(0, 0, 0, 6);
        expenseCard.Margin = new Padding(0, 6, 0, 6);
        profitCard.Margin = new Padding(0, 6, 0, 0);

        var body = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
        body.Controls.Add(incomeCard, 0, 0);
        body.Controls.Add(expenseCard, 0, 1);
        body.Controls.Add(profitCard, 0, 2);

        ModernListKit.ComposeRows(this, "داشبورد سود و زیان",
            "درآمد، هزینه و سود/زیان بر اساس قراردادهای کارفرما و پیمانکار",
            new Size(1100, 760),
            (toolbar, 50), (body, 0));
    }

    private static ModernTextBox MakeDateInput()
    {
        var t = ModernUI.Input(ModernInputKind.Text, "۱۴۰۵/۰۱/۰۱");
        t.TextAlign = HorizontalAlignment.Center;
        DateHelper.ApplyDateMask(t.TextBox);
        return t;
    }

    private static ModernStatCard AddStat(ModernCard card, string title, Color accent, int col,
                                          float valueSize = 15f, string sub = "")
    {
        var c = new ModernStatCard
        {
            Dock = DockStyle.Fill,
            Title = title,
            Accent = accent,
            SubText = sub,
            TitleFontSize = 10.5f,
            ValueFontSize = valueSize,
            FillColor = ModernTheme.SurfaceAlt,
            DimZero = true,
            Margin = new Padding(5)
        };
        c.SetAmount(0, "ریال");
        card.Body.Controls.Add(c, col, 0);
        return c;
    }

    /// <summary>مبلغ سود/زیان: رنگ نوار و برچسب بر اساس علامت (سبز = سود، قرمز = زیان)</summary>
    private static void SetProfit(ModernStatCard card, decimal value, string basis)
    {
        card.SetAmount(value, "ریال");
        card.Accent = value >= 0 ? ModernTheme.Success : ModernTheme.Danger;
        card.SubText = (value >= 0 ? "سود" : "زیان") + " — " + basis;
    }

    // ✅ FIX: اعتبارسنجی تاریخ قبل از اجرای کوئری
    //  - اگر کاربر تاریخ نامعتبر وارد کند، پیام واضح می‌گیرد (قبلاً بی‌صدا نادیده گرفته می‌شد)
    //  - اگر From > To باشد، پیام می‌گیرد (قبلاً بازه معکوس اجرا می‌شد و صفر برمی‌گشت)
    //  - هیچ تغییر در فرمول‌های FinancialSummary داده نشده
    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            // ✅ اعتبارسنجی: اگر کاربر متن وارد کرده ولی نامعتبر است
            if (!string.IsNullOrWhiteSpace(txtFromDate.Text))
            {
                var parsed = DateHelper.ParsePersianDate(txtFromDate.Text);
                if (parsed is null)
                {
                    ModernMessageBox.ShowWarning("تاریخ شروع نامعتبر است.\nلطفاً به فرمت 1403/01/01 وارد کنید یا فیلد را خالی بگذارید.");
                    return;
                }
            }
            if (!string.IsNullOrWhiteSpace(txtToDate.Text))
            {
                var parsed = DateHelper.ParsePersianDate(txtToDate.Text);
                if (parsed is null)
                {
                    ModernMessageBox.ShowWarning("تاریخ پایان نامعتبر است.\nلطفاً به فرمت 1403/01/01 وارد کنید یا فیلد را خالی بگذارید.");
                    return;
                }
            }

            DateTime? fromDate = DateHelper.ParsePersianDate(txtFromDate.Text);
            DateTime? toDate = DateHelper.ParsePersianDate(txtToDate.Text);

            // ✅ اعتبارسنجی: From باید قبل یا مساوی To باشد
            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                ModernMessageBox.ShowWarning(
                    "تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد.\n\n" +
                    $"تاریخ شروع: {DateHelper.ToPersianDate(fromDate)}\n" +
                    $"تاریخ پایان: {DateHelper.ToPersianDate(toDate)}\n\n" +
                    "لطفاً بازه را اصلاح کنید یا یکی از فیلدها را خالی بگذارید.");
                return;
            }

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new FinancialDashboardRepository(_connectionString);
                var summary = await Task.Run(() => repo.GetSummary(fromDate, toDate));

                if (IsDisposed) return;

                cardClientContract.SetAmount(summary.TotalClientContractAmount, "ریال");
                cardClientInvoiced.SetAmount(summary.TotalClientInvoiced, "ریال");
                cardClientReceived.SetAmount(summary.TotalClientReceived, "ریال");

                cardContractorContract.SetAmount(summary.TotalContractorContractAmount, "ریال");
                cardContractorInvoiced.SetAmount(summary.TotalContractorInvoiced, "ریال");
                cardContractorPaid.SetAmount(summary.TotalContractorPaid, "ریال");

                SetProfit(cardGrossProfit, summary.GrossProfit, GrossBasis);
                SetProfit(cardNetCashFlow, summary.NetCashFlow, CashBasis);
            }, "در حال بارگذاری داشبورد...");
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ProfitLossDashboardForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری داشبورد:\n" + ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ProfitLossDashboardForm"); }
        }
    }
}
