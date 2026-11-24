using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using Smp.Models;
using Smp.Helpers;
using Smp.Data;

namespace Smp.Forms;

public class ClientStatementEditForm : Form
{
    private readonly string _connectionString;
    private readonly ClientContract _contract;
    private readonly ClientStatement? _existingStatement;
    private readonly bool _isEdit;

    private TextBox txtStatementNumber = null!, txtStatementDate = null!, txtGrossAmount = null!;
    private TextBox txtFee = null!;
    private TextBox txtVat = null!, txtInsurance = null!, txtRetention = null!, txtAdvanceRecovery = null!, txtOther = null!, txtNet = null!;
    private ModernButton btnSave = null!, btnCalcFromExpenses = null!;

    private List<int> _selectedTransactionIds = new();
    private List<int> _selectedProjectExpenseIds = new();
    private List<int> _selectedCheckIds = new();

    // ✅ FIX بحرانی: فقط اگر کاربر از «محاسبه از هزینه‌ها» استفاده کرد، لینک‌ها بازنویسی می‌شوند
    private bool _expensesModified = false;

    // ✅ سقف کسورات و مدیریت مقدار خودکار پیش‌دریافت
    private decimal _previousRetention = 0;
    private decimal _previousAdvance = 0;
    private bool _advanceManual = false;   // کاربر مقدار را دستی وارد کرده
    private bool _settingAdvance = false;  // مقداردهی برنامه‌ای (نه کاربر)
    private bool _feeManual = false;       // حق‌الزحمه دستی وارد شده (نه درصدی از قرارداد)
    private bool _settingFee = false;

    private bool FeeMode => _contract.HasFeeSection;
    private bool PayrollMode => _contract.IsPayrollBased;   // کارکرد/حق‌الزحمه از لیست حقوق کارگران روزمزدِ محل خدمت

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public ClientStatementEditForm(string connectionString, ClientContract contract, ClientStatement? statement = null)
    {
        _connectionString = connectionString;
        _contract = contract;
        _existingStatement = statement;
        _isEdit = statement is not null;

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        // ✅ بارگذاری لینک‌های موجود قبل از نمایش
        Shown += async (s, e) =>
        {
            await LoadPreviousTotalsAsync();

            if (_isEdit && _existingStatement is not null)
            {
                await LoadDataAsync();
            }
            else
            {
                txtStatementDate.Text = DateHelper.ToPersianDate(DateTime.Now);
            }
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientStatementEditForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        int fh = ModernUI.FieldHeight;

        ModernTextBox Box(bool center = true)
        {
            var b = ModernUI.Input();
            if (center) b.TextAlign = HorizontalAlignment.Center;
            return b;
        }

        ModernTextBox ReadOnlyBox(Color fill)
        {
            var b = Box();
            b.ReadOnly = true;
            b.TextBox.BackColor = fill;
            return b;
        }

        // ───── کارت ۱: اطلاعات صورت وضعیت ─────
        var numBox = Box(); txtStatementNumber = numBox.TextBox;
        var dateBox = Box(); txtStatementDate = dateBox.TextBox; DateHelper.ApplyDateMask(txtStatementDate);

        var cardHead = new ModernCard("اطلاعات صورت وضعیت", ModernTheme.Primary, 2, fh);
        cardHead.Body.Controls.Add(ModernUI.Field("شماره صورت وضعیت", numBox), 0, 0);
        cardHead.Body.Controls.Add(ModernUI.Field("تاریخ صورت وضعیت", dateBox), 1, 0);

        // ───── کارت ۲: مبلغ کارکرد + دکمه محاسبه ─────
        var grossBox = Box();
        grossBox.Font = ModernTheme.GetFont(12f, FontStyle.Bold);
        txtGrossAmount = grossBox.TextBox;
        SmartInputHelper.EnableSmartAmountTextBox(txtGrossAmount);
        grossBox.Dock = DockStyle.Fill;
        grossBox.Margin = new Padding(0, 0, 6, 0);

        btnCalcFromExpenses = ModernButton.Soft(PayrollMode ? "📋 محاسبه از لیست حقوق" : "🧮 محاسبه از هزینه‌ها", ModernTheme.Primary, 160, ModernUI.InputHeight);
        btnCalcFromExpenses.Anchor = AnchorStyles.None;
        btnCalcFromExpenses.Click += BtnCalcFromExpenses_Click;

        var pnlGross = new BufferedTable { Margin = Padding.Empty, BackColor = Color.Transparent, ColumnCount = 2, RowCount = 1, RightToLeft = RightToLeft.Yes };
        pnlGross.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pnlGross.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
        pnlGross.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        pnlGross.Controls.Add(grossBox, 0, 0);
        pnlGross.Controls.Add(btnCalcFromExpenses, 1, 0);

        var cardGross = new ModernCard("مبلغ کارکرد", ModernTheme.Teal, 1, fh);
        cardGross.Body.Controls.Add(ModernUI.Field("مبلغ کارکرد (ناخالص)", pnlGross), 0, 0);

        // ───── کارت حق‌الزحمه (فقط قرارداد دو بخشی) ─────
        ModernCard? cardFee = null;
        if (FeeMode)
        {
            var feeBox = Box();
            feeBox.Font = ModernTheme.GetFont(12f, FontStyle.Bold);
            txtFee = feeBox.TextBox;
            SmartInputHelper.EnableSmartAmountTextBox(txtFee);
            cardFee = new ModernCard("حق‌الزحمه (بخش دوم — مشمول ارزش افزوده)", ModernTheme.Purple, 1, fh);
            cardFee.Body.Controls.Add(ModernUI.Field(
                PayrollMode
                    ? $"مبلغ حق‌الزحمه ((حقوق و اضافه‌کار − بیمه ۷٪) × {StringHelper.ConvertToPersianDigits(ClientFeeHelper.OverheadText(_contract.OverheadPercentage))} ÷ {StringHelper.ConvertToPersianDigits(ClientFeeHelper.DivisorText(_contract.FeeDivisorPercentage))}؛ قابل ویرایش)"
                    : $"مبلغ حق‌الزحمه ({StringHelper.ConvertToPersianDigits(_contract.FeePercentage.ToString("0.##"))}% کارکرد؛ قابل ویرایش)", feeBox), 0, 0);
        }
        else txtFee = new TextBox(); // جلوگیری از null در حالت قدیمی

        // ───── محاسبه درصدهای اولیه از قرارداد ─────
        decimal vatRate = _contract.VatPercentage;
        decimal insRate = _contract.InsurancePercentage;
        decimal retRate = _contract.RetentionPercentage;
        decimal advRate = _contract.AdvancePaymentPercentage;

        // ───── کارت ۳: کسورات ─────
        var vatBox = ReadOnlyBox(ModernTheme.SurfaceMuted); txtVat = vatBox.TextBox;
        var insBox = ReadOnlyBox(ModernTheme.SurfaceMuted); txtInsurance = insBox.TextBox;
        var retBox = ReadOnlyBox(ModernTheme.SurfaceMuted); txtRetention = retBox.TextBox;
        var advBox = Box(); txtAdvanceRecovery = advBox.TextBox;
        txtAdvanceRecovery.BackColor = ModernTheme.WarningSoft;
        SmartInputHelper.EnableSmartAmountTextBox(txtAdvanceRecovery);
        var otherBox = Box(); txtOther = otherBox.TextBox;
        SmartInputHelper.EnableSmartAmountTextBox(txtOther);

        var cardDeduct = new ModernCard("کسورات محاسبه‌شده از قرارداد", ModernTheme.Orange, 2, fh, fh, fh);
        string vatLabel = FeeMode
            ? $"ارزش افزوده ({StringHelper.ConvertToPersianDigits(vatRate.ToString("0.##"))}% فقط روی حق‌الزحمه)"
            : $"ارزش افزوده ({StringHelper.ConvertToPersianDigits(vatRate.ToString("0.##"))}%)";
        cardDeduct.Body.Controls.Add(ModernUI.Field(vatLabel, vatBox), 0, 0);
        cardDeduct.Body.Controls.Add(ModernUI.Field($"سپرده بیمه ({StringHelper.ConvertToPersianDigits(insRate.ToString("0.##"))}%)", insBox), 1, 0);
        cardDeduct.Body.Controls.Add(ModernUI.Field($"حسن انجام کار ({StringHelper.ConvertToPersianDigits(retRate.ToString("0.##"))}%)", retBox), 0, 1);
        cardDeduct.Body.Controls.Add(ModernUI.Field($"مستهلک پیش‌دریافت ({StringHelper.ConvertToPersianDigits(advRate.ToString("0.##"))}%)", advBox), 1, 1);
        var fldOther = ModernUI.Field("سایر کسورات (دستی)", otherBox);
        cardDeduct.Body.Controls.Add(fldOther, 0, 2);
        cardDeduct.Body.SetColumnSpan(fldOther, 2);

        // ───── کارت ۴: مبلغ خالص (برجسته) ─────
        var netBox = ReadOnlyBox(ModernTheme.SuccessSoft);
        netBox.Font = ModernTheme.GetFont(14f, FontStyle.Bold);
        txtNet = netBox.TextBox;
        txtNet.ForeColor = ModernTheme.Success;

        var cardNet = new ModernCard("نتیجه", ModernTheme.Success, 1, fh);
        cardNet.Body.Controls.Add(ModernUI.Field("مبلغ خالص دریافتی", netBox), 0, 0);

        // ───── اتصال رویدادها ─────
        txtGrossAmount.TextChanged += (s, e) => CalculateAmounts();
        txtAdvanceRecovery.TextChanged += (s, e) =>
        {
            if (_settingAdvance) return;
            // خالی کردن فیلد = بازگشت به محاسبه خودکار
            _advanceManual = !string.IsNullOrWhiteSpace(txtAdvanceRecovery.Text);
            CalculateAmounts();
        };
        txtOther.TextChanged += (s, e) => CalculateAmounts();
        if (FeeMode)
        {
            txtFee.TextChanged += (s, e) =>
            {
                if (_settingFee) return;
                _feeManual = !string.IsNullOrWhiteSpace(txtFee.Text);   // خالی = بازگشت به محاسبه خودکار
                CalculateAmounts();
            };
        }

        if (!_isEdit)
        {
            SetAdvanceText(0);
            txtOther.Text = "0";
        }

        // ───── صفحه‌ی اسکرول‌دار + هدر + نوار دکمه‌ها ─────
        var page = new ModernPage();
        var cards = new List<Control> { cardHead, cardGross };
        if (cardFee is not null) cards.Add(cardFee);
        cards.Add(cardDeduct);
        cards.Add(cardNet);
        foreach (var c in cards) page.AddCard(c, 8);

        btnSave = ModernButton.Success("💾 ذخیره صورت وضعیت", 220, 40);
        btnSave.Click += async (s, e) => await SaveSafeAsync();
        var btnCancel = ModernButton.Cancel();
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var bar = ModernForm.CreateButtonBar(btnSave, btnCancel);
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        string title = _isEdit ? "ویرایش صورت وضعیت کارفرما" : $"ثبت صورت وضعیت - قرارداد {_contract.ContractNumber}";
        SuspendLayout();
        ModernForm.Setup(this, title, new Size(900, FeeMode ? 880 : 780), isDialog: true);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(ModernForm.CreateHeader(title, "ثبت کارکرد و کسورات صورت وضعیت"), 0, 0);
        root.Controls.Add(page, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
        ResumeLayout();
    }

    private void BtnCalcFromExpenses_Click(object? sender, EventArgs e)
    {
        if (PayrollMode) { CalcFromPayroll(); return; }

        using var form = new ExpensePickerForm(_connectionString, _contract.Id, _existingStatement?.Id, FeeMode, _contract.FeePercentage);

        if (form.ShowDialog(this) == DialogResult.OK)
        {
            if (FeeMode)
            {
                // کارکرد = جمع هزینه‌ها (بدون ارزش افزوده)؛ حق‌الزحمه جدا از آن
                SetFeeText(form.SelectedFeeAmount);
                _feeManual = true;
            }
            txtGrossAmount.Text = form.SelectedGrossAmount.ToString("N0");
            if (FeeMode)
            {
                decimal autoFee = Math.Round(form.SelectedGrossAmount * PercentHelper.ToRate(_contract.FeePercentage), 0);
                _feeManual = form.SelectedFeeAmount != autoFee;
                CalculateAmounts();
            }
            _selectedTransactionIds = form.SelectedTransactionIds;
            _selectedProjectExpenseIds = form.SelectedProjectExpenseIds;
            _selectedCheckIds = form.SelectedCheckIds;

            // ✅ علامت‌گذاری به‌عنوان تغییر یافته — تنها راهی که در Save بازنویسی لینک‌ها انجام می‌شود
            _expensesModified = true;
        }
    }

    private void SetAdvanceText(decimal value)
    {
        _settingAdvance = true;
        try { txtAdvanceRecovery.Text = NumberHelper.FormatThousands(value, usePersianDigits: true); }
        finally { _settingAdvance = false; }
    }

    /// <summary>کارکرد و حق‌الزحمه را از لیست حقوق کارگران روزمزدِ محل خدمت قرارداد پر می‌کند</summary>
    private void CalcFromPayroll()
    {
        var d = DateHelper.ParsePersianDate(txtStatementDate.Text) ?? DateTime.Now;
        var pc = new System.Globalization.PersianCalendar();

        using var dlg = new PayrollPeriodPickerForm(_connectionString, _contract, pc.GetYear(d), pc.GetMonth(d));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        SetFeeText(dlg.SelectedFeeAmount);
        _feeManual = true;
        txtGrossAmount.Text = dlg.SelectedGrossAmount.ToString("N0");
        CalculateAmounts();   // اگر متن کارکرد تغییری نکرده بود، رویداد TextChanged اجرا نمی‌شود
    }

    private void SetFeeText(decimal value)
    {
        _settingFee = true;
        try { txtFee.Text = NumberHelper.FormatThousands(value, usePersianDigits: true); }
        finally { _settingFee = false; }
    }

    private decimal GetRemainingAdvance() => Math.Max(0m, _contract.TotalAdvanceAmount - _previousAdvance);

    private async Task LoadPreviousTotalsAsync()
    {
        try
        {
            var repo = new ClientStatementRepository(_connectionString);
            int? excludeId = _isEdit ? _existingStatement?.Id : null;
            var totals = await Task.Run(() => repo.GetDeductionsTotal(_contract.Id, excludeId));
            _previousRetention = totals.TotalRetention;
            _previousAdvance = totals.TotalAdvance;
            if (!IsDisposed) CalculateAmounts();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientStatementEditForm.LoadPreviousTotals");
        }
    }

    private void CalculateAmounts()
    {
        decimal gross = NumberHelper.ParseFormatted(txtGrossAmount.Text);

        if (gross > 0)
        {
            // ✅ در ویرایش، تا وقتی مبلغ کارکرد تغییر نکرده، مقادیر ذخیره‌شده‌ی خودِ صورت‌وضعیت حفظ می‌شود
            //    (قبلاً با درصدهای فعلی قرارداد بازمحاسبه می‌شد و ارقام تاریخی بی‌صدا تغییر می‌کرد)
            decimal feeRate = PercentHelper.ToRate(_contract.FeePercentage);
            decimal fee = 0m;
            if (FeeMode)
            {
                if (!_feeManual) SetFeeText(PayrollMode ? 0m : Math.Round(gross * feeRate, 0));
                fee = NumberHelper.ParseFormatted(txtFee.Text);
            }

            bool useStored = _isEdit && _existingStatement is not null
                             && gross == _existingStatement.GrossAmount
                             && fee == _existingStatement.FeeAmount;

            // محاسبه‌ی مبالغ در ClientStatementCalculator (کلاس خالص و تست‌شده)
            decimal remainingAdvance = GetRemainingAdvance();
            var calc = ClientStatementCalculator.Calculate(new ClientStatementCalculator.Input(
                Gross: gross,
                Fee: fee,
                FeeMode: FeeMode,
                VatRate: PercentHelper.ToRate(_contract.VatPercentage),
                InsuranceRate: PercentHelper.ToRate(_contract.InsurancePercentage),
                RetentionRate: PercentHelper.ToRate(_contract.RetentionPercentage),
                AdvanceRate: PercentHelper.ToRate(_contract.AdvancePaymentPercentage),
                ContractAmount: _contract.Amount,
                PreviousRetention: _previousRetention,
                RemainingAdvance: remainingAdvance,
                ManualAdvance: _advanceManual ? NumberHelper.ParseFormatted(txtAdvanceRecovery.Text) : null,
                Other: NumberHelper.ParseFormatted(txtOther.Text),
                Stored: useStored
                    ? new ClientStatementCalculator.StoredAmounts(
                        _existingStatement!.VAT, _existingStatement.InsuranceDeduction, _existingStatement.RetentionDeduction)
                    : null));

            if (!_advanceManual) SetAdvanceText(calc.Advance);

            txtVat.Text = NumberHelper.FormatThousands(calc.Vat, usePersianDigits: true);
            txtInsurance.Text = NumberHelper.FormatThousands(calc.Insurance, usePersianDigits: true);
            txtRetention.Text = NumberHelper.FormatThousands(calc.Retention, usePersianDigits: true);

            txtRetention.BackColor = calc.RetentionCapped ? ModernTheme.DangerSoft : ModernTheme.SurfaceMuted;
            txtAdvanceRecovery.BackColor = calc.Advance > remainingAdvance ? ModernTheme.DangerSoft : ModernTheme.WarningSoft;

            decimal net = calc.Net;

            txtNet.Text = NumberHelper.FormatThousands(net, usePersianDigits: true);
            txtNet.ForeColor = net >= 0 ? ModernTheme.Success : ModernTheme.Danger;
        }
        else
        {
            if (FeeMode && !_feeManual) SetFeeText(0);
            txtVat.Text = "0";
            txtInsurance.Text = "0";
            txtRetention.Text = "0";
            txtNet.Text = "0";
        }
    }

    private async Task LoadDataAsync()
    {
        if (_existingStatement is null) return;

        try
        {
            txtStatementNumber.Text = _existingStatement.StatementNumber;
            txtStatementDate.Text = DateHelper.ToPersianDate(_existingStatement.StatementDate);
            txtGrossAmount.Text = NumberHelper.FormatThousands(_existingStatement.GrossAmount, usePersianDigits: true);

            if (FeeMode)
            {
                // اگر حق‌الزحمه ذخیره‌شده همان درصد قرارداد نیست، دستی تلقی می‌شود
                _feeManual = PayrollMode || _existingStatement.FeeAmount !=
                             Math.Round(_existingStatement.GrossAmount * PercentHelper.ToRate(_contract.FeePercentage), 0);
                SetFeeText(_existingStatement.FeeAmount);
            }

            txtVat.Text = NumberHelper.FormatThousands(_existingStatement.VAT, usePersianDigits: true);
            txtInsurance.Text = NumberHelper.FormatThousands(_existingStatement.InsuranceDeduction, usePersianDigits: true);
            txtRetention.Text = NumberHelper.FormatThousands(_existingStatement.RetentionDeduction, usePersianDigits: true);

            // ✅ مقدار ذخیره‌شده (حتی صفر) عیناً نمایش داده می‌شود و بازنویسی نمی‌شود
            _advanceManual = true;
            SetAdvanceText(_existingStatement.AdvanceRecovery);

            txtOther.Text = NumberHelper.FormatThousands(_existingStatement.OtherDeductions, usePersianDigits: true);

            CalculateAmounts();

            // ✅ بارگذاری لینک‌های فعلی — تا اگر Save بدون تغییر زده شد، پاک نشوند
            await LoadCurrentLinkedExpensesAsync(_existingStatement.Id);
            _expensesModified = false;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientStatementEditForm.LoadDataAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری صورت وضعیت:\n" + ex.Message);
        }
    }

    private async Task LoadCurrentLinkedExpensesAsync(int statementId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
                SELECT 'Tx' AS SourceType, Id FROM CashTransactions WHERE ClientStatementId = @stId
                UNION ALL
                SELECT 'Exp' AS SourceType, Id FROM ProjectExpenses WHERE ClientStatementId = @stId
                UNION ALL
                SELECT 'Chk' AS SourceType, Id FROM CashChecks WHERE ClientStatementId = @stId";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@stId", statementId);

        _selectedTransactionIds.Clear();
        _selectedProjectExpenseIds.Clear();
        _selectedCheckIds.Clear();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string sourceType = reader.IsDBNull(0) ? "" : reader.GetString(0);
            int id = reader.GetInt32(1);

            switch (sourceType)
            {
                case "Tx": _selectedTransactionIds.Add(id); break;
                case "Exp": _selectedProjectExpenseIds.Add(id); break;
                case "Chk": _selectedCheckIds.Add(id); break;
            }
        }
    }

    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (string.IsNullOrWhiteSpace(txtStatementNumber.Text))
            {
                ModernMessageBox.ShowWarning("لطفاً شماره صورت وضعیت را وارد کنید.");
                txtStatementNumber.Focus();
                return;
            }

            if (!DateHelper.IsValidPersianDate(txtStatementDate.Text))
            {
                ModernMessageBox.ShowWarning("تاریخ وارد شده معتبر نیست.");
                txtStatementDate.Focus();
                return;
            }

            decimal gross = NumberHelper.ParseFormatted(txtGrossAmount.Text);
            decimal fee = FeeMode ? NumberHelper.ParseFormatted(txtFee.Text) : 0m;
            decimal vat = NumberHelper.ParseFormatted(txtVat.Text);
            decimal insurance = NumberHelper.ParseFormatted(txtInsurance.Text);
            decimal retention = NumberHelper.ParseFormatted(txtRetention.Text);
            decimal advance = NumberHelper.ParseFormatted(txtAdvanceRecovery.Text);
            decimal other = NumberHelper.ParseFormatted(txtOther.Text);

            decimal totalDeductions = insurance + retention + advance + other;
            decimal net = (gross + fee + vat) - totalDeductions;

            if (gross <= 0)
            {
                ModernMessageBox.ShowWarning("مبلغ کارکرد باید بزرگتر از صفر باشد.");
                txtGrossAmount.Focus();
                return;
            }

            decimal remainingAdv = GetRemainingAdvance();
            if (advance > remainingAdv)
            {
                ModernMessageBox.ShowWarning(
                    $"مبلغ مستهلک پیش‌دریافت ({NumberHelper.FormatThousands(advance, usePersianDigits: true)}) " +
                    $"از مانده پیش‌دریافت قرارداد ({NumberHelper.FormatThousands(remainingAdv, usePersianDigits: true)}) بیشتر است.");
                txtAdvanceRecovery.Focus();
                return;
            }

            // ✅ هشدار سقف: جمع مبلغ کارکرد صورت‌وضعیت‌ها نباید از مبلغ قرارداد بیشتر شود (هشدار، نه ممنوعیت)
            var gRepo = new ClientStatementRepository(_connectionString);
            int? excludeStId = _isEdit ? _existingStatement?.Id : null;
            decimal previousGross = await Task.Run(() => gRepo.GetGrossTotal(_contract.Id, excludeStId));
            if (_contract.Amount > 0m && previousGross + gross + fee > _contract.Amount)
            {
                string msg = $"جمع مبلغ صورت‌وضعیت‌ها بدون ارزش افزوده ({NumberHelper.FormatThousands(previousGross + gross + fee, usePersianDigits: true)} ریال) " +
                             $"از مبلغ قرارداد ({NumberHelper.FormatThousands(_contract.Amount, usePersianDigits: true)} ریال) بیشتر می‌شود.\n\nادامه می‌دهید؟";
                if (!ModernMessageBox.AskConfirm(msg, "هشدار سقف قرارداد")) return;
            }

            var statement = new ClientStatement
            {
                Id = _isEdit && _existingStatement is not null ? _existingStatement.Id : 0,
                ContractId = _contract.Id,
                StatementNumber = txtStatementNumber.Text.Trim(),
                StatementDate = DateHelper.ParsePersianDate(txtStatementDate.Text) ?? DateTime.Now,
                GrossAmount = gross,
                FeeAmount = fee,
                VAT = vat,
                InsuranceDeduction = insurance,
                RetentionDeduction = retention,
                AdvanceRecovery = advance,
                OtherDeductions = other,
                Deductions = totalDeductions,
                NetReceivableAmount = net
            };

            if (_isEdit) DateStampHelper.StampUpdated(statement);
            else DateStampHelper.StampCreated(statement);

            // snapshot برای استفاده در closure
            bool expensesWereModified = _expensesModified;
            var txIds = _selectedTransactionIds.ToList();
            var expIds = _selectedProjectExpenseIds.ToList();
            var chkIds = _selectedCheckIds.ToList();

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ClientStatementRepository(_connectionString);

                if (_isEdit)
                {
                    await Task.Run(() => repo.Update(statement));

                    // ✅ فقط اگر کاربر از «محاسبه از هزینه‌ها» استفاده کرده، لینک‌ها بازنویسی می‌شوند
                    if (expensesWereModified)
                    {
                        await Task.Run(() => repo.LinkExpensesToStatement(statement.Id, txIds, expIds, chkIds));
                    }
                }
                else
                {
                    await Task.Run(() => repo.Add(statement));
                    if (txIds.Count > 0 || expIds.Count > 0 || chkIds.Count > 0)
                    {
                        await Task.Run(() => repo.LinkExpensesToStatement(statement.Id, txIds, expIds, chkIds));
                    }
                }
            }, "در حال ذخیره صورت وضعیت...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show(
                    _isEdit && !expensesWereModified
                        ? "صورت وضعیت ذخیره شد (هزینه‌های متصل دست‌نخورده ماندند)."
                        : "صورت وضعیت با موفقیت ذخیره شد.",
                    isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientStatementEditForm.SaveSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientStatementEditForm"); }
        }
    }
}
