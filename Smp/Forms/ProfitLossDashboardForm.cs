using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Models;
using SMP.Helpers;
using SMP.Data;

namespace SMP.Forms
{
    public class ProfitLossDashboardForm : Form
    {
        private readonly string _connectionString;

        private TextBox txtFromDate = null!, txtToDate = null!;
        private Label lblClientContract = null!, lblClientInvoiced = null!, lblClientReceived = null!;
        private Label lblContractorContract = null!, lblContractorInvoiced = null!, lblContractorPaid = null!;
        private Label lblGrossProfit = null!, lblNetCashFlow = null!;
        private Button btnFilter = null!, btnClear = null!, btnRefresh = null!;

        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public ProfitLossDashboardForm(string connectionString)
        {
            _connectionString = connectionString;

            BuildUI();
            this.ApplyStandardHeader("داشبورد سود و زیان", new Size(1000, 750));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this);

            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _loadLock?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(20) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            var pnlFilter = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground };
            var filterFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };

            var lblFrom = new Label { Text = "از تاریخ:", AutoSize = true, Margin = new Padding(5, 8, 5, 0), Font = UIHelper.PersianFont(11f), ForeColor = UIHelper.WarmText };
            txtFromDate = new TextBox { Width = 120, Margin = new Padding(5, 5, 15, 5), Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmInputBackground, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center };
            DateHelper.ApplyDateMask(txtFromDate);

            var lblTo = new Label { Text = "تا تاریخ:", AutoSize = true, Margin = new Padding(5, 8, 5, 0), Font = UIHelper.PersianFont(11f), ForeColor = UIHelper.WarmText };
            txtToDate = new TextBox { Width = 120, Margin = new Padding(5, 5, 15, 5), Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmInputBackground, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center };
            DateHelper.ApplyDateMask(txtToDate);

            btnFilter = UIHelper.CreateButton("🔍 اعمال فیلتر", UIHelper.WarmPrimary, 120, 35);
            btnFilter.Margin = new Padding(5);
            btnFilter.Click += async (s, e) => await LoadDataSafeAsync();

            btnClear = UIHelper.CreateButton("✖ حذف فیلتر", UIHelper.SectionGray, 100, 35);
            btnClear.Margin = new Padding(5);
            btnClear.Click += async (s, e) => { txtFromDate.Clear(); txtToDate.Clear(); await LoadDataSafeAsync(); };

            filterFlow.Controls.Add(lblFrom);
            filterFlow.Controls.Add(txtFromDate);
            filterFlow.Controls.Add(lblTo);
            filterFlow.Controls.Add(txtToDate);
            filterFlow.Controls.Add(btnFilter);
            filterFlow.Controls.Add(btnClear);
            pnlFilter.Controls.Add(filterFlow);

            var groupIncome = new GroupBox
            {
                Text = "📈 درآمدها (قراردادهای کارفرما)",
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                ForeColor = UIHelper.ActionGreen,
                Padding = new Padding(15)
            };

            var layoutIncome = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            layoutIncome.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            layoutIncome.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            layoutIncome.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4F));

            lblClientContract = CreateValueLabel("مبلغ کل قراردادها:\n0 ریال");
            lblClientInvoiced = CreateValueLabel("مجموع صورت‌وضعیت‌ها:\n0 ریال");
            lblClientReceived = CreateValueLabel("مجموع دریافتی‌ها:\n0 ریال");

            layoutIncome.Controls.Add(lblClientContract, 0, 0);
            layoutIncome.Controls.Add(lblClientInvoiced, 1, 0);
            layoutIncome.Controls.Add(lblClientReceived, 2, 0);
            groupIncome.Controls.Add(layoutIncome);

            var groupExpense = new GroupBox
            {
                Text = "📉 هزینه‌ها (قراردادهای پیمانکار)",
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                ForeColor = UIHelper.WarmDanger,
                Padding = new Padding(15)
            };

            var layoutExpense = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            layoutExpense.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            layoutExpense.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            layoutExpense.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4F));

            lblContractorContract = CreateValueLabel("مبلغ کل قراردادها:\n0 ریال");
            lblContractorInvoiced = CreateValueLabel("مجموع صورت‌وضعیت‌ها:\n0 ریال");
            lblContractorPaid = CreateValueLabel("مجموع پرداختی‌ها:\n0 ریال");

            layoutExpense.Controls.Add(lblContractorContract, 0, 0);
            layoutExpense.Controls.Add(lblContractorInvoiced, 1, 0);
            layoutExpense.Controls.Add(lblContractorPaid, 2, 0);
            groupExpense.Controls.Add(layoutExpense);

            var groupProfit = new GroupBox
            {
                Text = "💰 خلاصه سود و زیان",
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(13f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                Padding = new Padding(15)
            };

            var layoutProfit = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            layoutProfit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layoutProfit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            lblGrossProfit = CreateValueLabel("سود/زیان ناخالص (بر اساس صورت‌وضعیت):\n0 ریال", 16f);
            lblNetCashFlow = CreateValueLabel("سود/زیان نقدی (پول در دست):\n0 ریال", 16f);

            layoutProfit.Controls.Add(lblGrossProfit, 0, 0);
            layoutProfit.Controls.Add(lblNetCashFlow, 1, 0);
            groupProfit.Controls.Add(layoutProfit);

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10) };
            btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی آمار", UIHelper.SectionBlue, 180, 40);
            btnRefresh.Click += async (s, e) => await LoadDataSafeAsync();
            flow.Controls.Add(btnRefresh);

            mainLayout.Controls.Add(pnlFilter, 0, 0);
            mainLayout.Controls.Add(groupIncome, 0, 1);
            mainLayout.Controls.Add(groupExpense, 0, 2);
            mainLayout.Controls.Add(groupProfit, 0, 3);
            mainLayout.Controls.Add(flow, 0, 4);

            Controls.Add(mainLayout);
        }

        private Label CreateValueLabel(string text, float fontSize = 12f)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(fontSize, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                BackColor = UIHelper.WarmPanelBackground,
                Margin = new Padding(10)
            };
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
                    if (parsed == null)
                    {
                        ModernMessageBox.ShowWarning("تاریخ شروع نامعتبر است.\nلطفاً به فرمت 1403/01/01 وارد کنید یا فیلد را خالی بگذارید.");
                        return;
                    }
                }
                if (!string.IsNullOrWhiteSpace(txtToDate.Text))
                {
                    var parsed = DateHelper.ParsePersianDate(txtToDate.Text);
                    if (parsed == null)
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

                    lblClientContract.Text = $"مبلغ کل قراردادها:\n{NumberHelper.FormatThousands(summary.TotalClientContractAmount, usePersianDigits: true)} ریال";
                    lblClientInvoiced.Text = $"مجموع صورت‌وضعیت‌ها:\n{NumberHelper.FormatThousands(summary.TotalClientInvoiced, usePersianDigits: true)} ریال";
                    lblClientReceived.Text = $"مجموع دریافتی‌ها:\n{NumberHelper.FormatThousands(summary.TotalClientReceived, usePersianDigits: true)} ریال";

                    lblContractorContract.Text = $"مبلغ کل قراردادها:\n{NumberHelper.FormatThousands(summary.TotalContractorContractAmount, usePersianDigits: true)} ریال";
                    lblContractorInvoiced.Text = $"مجموع صورت‌وضعیت‌ها:\n{NumberHelper.FormatThousands(summary.TotalContractorInvoiced, usePersianDigits: true)} ریال";
                    lblContractorPaid.Text = $"مجموع پرداختی‌ها:\n{NumberHelper.FormatThousands(summary.TotalContractorPaid, usePersianDigits: true)} ریال";

                    lblGrossProfit.Text = $"سود/زیان ناخالص (صورت‌وضعیت):\n{NumberHelper.FormatThousands(summary.GrossProfit, usePersianDigits: true)} ریال";
                    lblGrossProfit.ForeColor = summary.GrossProfit >= 0 ? UIHelper.ActionGreen : UIHelper.WarmDanger;

                    lblNetCashFlow.Text = $"سود/زیان نقدی (دریافتی - پرداختی):\n{NumberHelper.FormatThousands(summary.NetCashFlow, usePersianDigits: true)} ریال";
                    lblNetCashFlow.ForeColor = summary.NetCashFlow >= 0 ? UIHelper.ActionGreen : UIHelper.WarmDanger;
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
                try { _loadLock.Release(); } catch { }
            }
        }
    }
}