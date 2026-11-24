using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Models;
using Smp.Helpers;
using Smp.Data;

namespace Smp.Forms;

public class ContractorStatementEditForm : Form
{
    private readonly string _connectionString;
    private readonly ContractorContract _contract;
    private readonly ContractorStatement? _existingStatement;
    private readonly bool _isEdit;

    private decimal _previousRetention = 0;
    private decimal _previousAdvance = 0;

    private decimal _currentVat = 0;
    private decimal _currentRetention = 0;
    private decimal _currentInsurance = 0;
    private decimal _currentAdvance = 0;
    private decimal _currentNet = 0;
    private decimal _currentOther = 0; // سایر کسورات (از رکورد موجود حفظ می‌شود ولی اکنون در خالص لحاظ می‌گردد)

    private TextBox txtStatementNumber = null!, txtStatementDate = null!, txtGrossAmount = null!;
    private TextBox txtVat = null!, txtRetention = null!, txtInsurance = null!, txtAdvance = null!, txtNetPayable = null!;
    private ModernButton btnSave = null!;

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public ContractorStatementEditForm(string connectionString, ContractorContract contract, ContractorStatement? statement = null)
    {
        _connectionString = connectionString;
        _contract = contract;
        _existingStatement = statement;
        _isEdit = statement is not null;

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
        ShowInTaskbar = false;

        Shown += async (s, e) => await InitializeDataAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorStatementEditForm"); } }
        base.Dispose(disposing);
    }

    private async Task InitializeDataAsync()
    {
        if (IsDisposed) return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            var repo = new ContractorStatementRepository(_connectionString);
            var totals = await Task.Run(() => repo.GetDeductionsTotal(_contract.Id, _isEdit ? _existingStatement?.Id : null));
            _previousRetention = totals.TotalRetention;
            _previousAdvance = totals.TotalAdvance;
        }, "در حال دریافت اطلاعات کسورات...");

        if (!success || IsDisposed) return;

        txtGrossAmount.Enabled = true;

        if (_isEdit && _existingStatement is not null)
            LoadData();
    }

    private void BuildUI()
    {
        int fh = ModernUI.FieldHeight;

        ModernTextBox Box()
        {
            var b = ModernUI.Input();
            b.TextAlign = HorizontalAlignment.Center;
            return b;
        }

        ModernTextBox ReadOnlyBox(Color fill)
        {
            var b = Box();
            b.ReadOnly = true;
            b.TextBox.BackColor = fill;
            return b;
        }

        // ───── ساخت فیلدها ─────
        var numBox = Box(); txtStatementNumber = numBox.TextBox;

        var dateBox = Box(); txtStatementDate = dateBox.TextBox;
        DateHelper.ApplyDateMask(txtStatementDate);

        var grossBox = Box();
        grossBox.Font = ModernTheme.GetFont(12f, FontStyle.Bold);
        txtGrossAmount = grossBox.TextBox;
        SmartInputHelper.EnableSmartAmountTextBox(txtGrossAmount);
        txtGrossAmount.TextChanged += (s, e) => CalculateAmounts();

        // ✅ تا وقتی مجموع کسورات قبلی (حسن انجام کار/پیش‌پرداخت) بارگذاری نشده، ورود مبلغ ممکن نیست
        //    (قبلاً با تایپ سریع، سقف‌ها اعمال نمی‌شدند)
        txtGrossAmount.Enabled = false;

        var vatBox = ReadOnlyBox(ModernTheme.SurfaceMuted); txtVat = vatBox.TextBox;
        var retBox = ReadOnlyBox(ModernTheme.SurfaceMuted); txtRetention = retBox.TextBox;
        var insBox = ReadOnlyBox(ModernTheme.SurfaceMuted); txtInsurance = insBox.TextBox;
        var advBox = ReadOnlyBox(ModernTheme.SurfaceMuted); txtAdvance = advBox.TextBox;

        var netBox = ReadOnlyBox(ModernTheme.SuccessSoft);
        netBox.Font = ModernTheme.GetFont(13f, FontStyle.Bold);
        txtNetPayable = netBox.TextBox;
        txtNetPayable.ForeColor = ModernTheme.Success;

        // ───── کارت ۱: اطلاعات صورت وضعیت ─────
        var cardHead = new ModernCard("اطلاعات صورت وضعیت", ModernTheme.Primary, 2, fh, fh);
        cardHead.Body.Controls.Add(ModernUI.Field("شماره صورت وضعیت", numBox), 0, 0);
        cardHead.Body.Controls.Add(ModernUI.Field("تاریخ صورت وضعیت", dateBox), 1, 0);
        var fldGross = ModernUI.Field("مبلغ کارکرد (ناخالص)", grossBox);
        cardHead.Body.Controls.Add(fldGross, 0, 1);
        cardHead.Body.SetColumnSpan(fldGross, 2);

        // ───── کارت ۲: کسورات ─────
        var cardDeduct = new ModernCard("کسورات محاسبه‌شده", ModernTheme.Orange, 2, fh, fh);
        cardDeduct.Body.Controls.Add(ModernUI.Field($"ارزش افزوده ({_contract.VatPercentage}%)", vatBox), 0, 0);
        cardDeduct.Body.Controls.Add(ModernUI.Field($"بیمه ({_contract.InsurancePercentage}%)", insBox), 1, 0);
        cardDeduct.Body.Controls.Add(ModernUI.Field($"حسن انجام کار ({_contract.RetentionPercentage}%)", retBox), 0, 1);
        cardDeduct.Body.Controls.Add(ModernUI.Field($"کسر پیش‌پرداخت ({_contract.AdvancePaymentPercentage}%)", advBox), 1, 1);

        // ───── کارت ۳: خالص ─────
        var cardNet = new ModernCard("نتیجه", ModernTheme.Success, 1, fh);
        cardNet.Body.Controls.Add(ModernUI.Field("مبلغ خالص قابل پرداخت", netBox), 0, 0);

        // ───── دکمه‌ها ─────
        btnSave = ModernButton.Success("💾 ذخیره", 200, 40);
        btnSave.Click += async (s, e) => await SaveSafeAsync();
        var btnCancel = ModernButton.Cancel();
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var bar = ModernForm.CreateButtonBar(btnSave, btnCancel);
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var page = new ModernPage();
        foreach (var c in new Control[] { cardHead, cardDeduct, cardNet }) page.AddCard(c, 8);

        string title = _isEdit ? "ویرایش صورت وضعیت" : $"ثبت صورت وضعیت - قرارداد {_contract.ContractNumber}";
        SuspendLayout();
        ModernForm.Setup(this, title, new Size(750, 780), isDialog: true);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(ModernForm.CreateHeader(title, "ثبت کارکرد و کسورات صورت وضعیت پیمانکار"), 0, 0);
        root.Controls.Add(page, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
        ResumeLayout();

        if (!_isEdit) txtStatementDate.Text = DateHelper.ToPersianDate(DateTime.Now);
    }

    private void CalculateAmounts()
    {
        decimal gross = NumberHelper.ParseFormatted(txtGrossAmount.Text);

        if (gross > 0)
        {
            // ✅ در ویرایش، تا وقتی مبلغ کارکرد تغییر نکرده، مقادیر ذخیره‌شده‌ی خودِ صورت‌وضعیت حفظ می‌شود
            //    (قبلاً با درصدهای فعلی قرارداد بازمحاسبه می‌شد و ارقام تاریخی بی‌صدا تغییر می‌کرد)
            bool useStored = _isEdit && _existingStatement is not null && gross == _existingStatement.GrossAmount;

            decimal vatRate = NormalizeRate(_contract.VatPercentage);
            decimal retentionRate = NormalizeRate(_contract.RetentionPercentage);
            decimal insuranceRate = NormalizeRate(_contract.InsurancePercentage);
            decimal advanceRate = NormalizeRate(_contract.AdvancePaymentPercentage);

            _currentVat = useStored ? _existingStatement!.VAT : Math.Round(gross * vatRate, 0);

            decimal calculatedRetention = Math.Round(gross * retentionRate, 0);
            decimal maxRetentionLimit = _contract.Amount * retentionRate;
            decimal remainingRetention = Math.Max(0, maxRetentionLimit - _previousRetention);
            _currentRetention = useStored ? _existingStatement!.RetentionDeduction : Math.Min(calculatedRetention, remainingRetention);

            _currentInsurance = useStored ? _existingStatement!.InsuranceDeduction : Math.Round(gross * insuranceRate, 0);

            decimal calculatedAdvance = Math.Round(gross * advanceRate, 0);
            decimal remainingAdvance = Math.Max(0, _contract.TotalAdvanceAmount - _previousAdvance);
            _currentAdvance = useStored ? _existingStatement!.AdvanceRecovery : Math.Min(calculatedAdvance, remainingAdvance);

            decimal totalGrossWithVat = gross + _currentVat;
            _currentOther = (_isEdit && _existingStatement is not null) ? _existingStatement.OtherDeductions : 0m;
            decimal totalDeductions = _currentRetention + _currentInsurance + _currentAdvance + _currentOther;
            _currentNet = totalGrossWithVat - totalDeductions;

            txtVat.Text = NumberHelper.FormatThousands(_currentVat, usePersianDigits: true);
            txtRetention.Text = NumberHelper.FormatThousands(_currentRetention, usePersianDigits: true);
            txtInsurance.Text = NumberHelper.FormatThousands(_currentInsurance, usePersianDigits: true);
            txtAdvance.Text = NumberHelper.FormatThousands(_currentAdvance, usePersianDigits: true);
            txtNetPayable.Text = NumberHelper.FormatThousands(_currentNet, usePersianDigits: true);

            txtRetention.BackColor = (!useStored && calculatedRetention > remainingRetention) ? ModernTheme.DangerSoft : ModernTheme.SurfaceMuted;
            txtAdvance.BackColor = (!useStored && calculatedAdvance > remainingAdvance) ? ModernTheme.DangerSoft : ModernTheme.SurfaceMuted;
        }
        else
        {
            _currentVat = _currentRetention = _currentInsurance = _currentAdvance = _currentNet = _currentOther = 0;
            txtVat.Text = "0";
            txtRetention.Text = "0";
            txtInsurance.Text = "0";
            txtAdvance.Text = "0";
            txtNetPayable.Text = "0";
            txtRetention.BackColor = ModernTheme.SurfaceMuted;
            txtAdvance.BackColor = ModernTheme.SurfaceMuted;
        }
    }

    private static decimal NormalizeRate(decimal rate)
    {
        return PercentHelper.ToRate(rate);
    }

    private void LoadData()
    {
        if (_existingStatement is null) return;

        txtStatementNumber.Text = _existingStatement.StatementNumber;
        txtStatementDate.Text = DateHelper.ToPersianDate(_existingStatement.StatementDate);
        txtGrossAmount.Text = NumberHelper.FormatThousands(_existingStatement.GrossAmount, usePersianDigits: true);

        CalculateAmounts();
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

            decimal gross = NumberHelper.ParseFormatted(txtGrossAmount.Text);
            if (gross <= 0)
            {
                ModernMessageBox.ShowWarning("لطفاً مبلغ کارکرد معتبری وارد کنید.");
                txtGrossAmount.Focus();
                return;
            }

            if (!DateHelper.IsValidPersianDate(txtStatementDate.Text))
            {
                ModernMessageBox.ShowWarning("تاریخ صورت وضعیت معتبر نیست.");
                txtStatementDate.Focus();
                return;
            }

            // ✅ هشدار سقف: جمع مبلغ کارکرد صورت‌وضعیت‌ها نباید از مبلغ قرارداد بیشتر شود (هشدار، نه ممنوعیت؛ ممکن است الحاقیه‌ای در کار باشد)
            var stRepo = new ContractorStatementRepository(_connectionString);
            int? excludeStatementId = _isEdit ? _existingStatement?.Id : null;
            decimal previousGross = await Task.Run(() => stRepo.GetGrossTotal(_contract.Id, excludeStatementId));
            if (_contract.Amount > 0m && previousGross + gross > _contract.Amount)
            {
                string msg = $"جمع مبلغ کارکرد صورت‌وضعیت‌ها ({NumberHelper.FormatThousands(previousGross + gross, usePersianDigits: true)} ریال) " +
                             $"از مبلغ قرارداد ({NumberHelper.FormatThousands(_contract.Amount, usePersianDigits: true)} ریال) بیشتر می‌شود.\n\nادامه می‌دهید؟";
                if (!ModernMessageBox.AskConfirm(msg, "هشدار سقف قرارداد")) return;
            }

            var statement = new ContractorStatement
            {
                Id = _isEdit && _existingStatement is not null ? _existingStatement.Id : 0,
                ContractId = _contract.Id,
                StatementNumber = txtStatementNumber.Text.Trim(),
                StatementDate = DateHelper.ParsePersianDate(txtStatementDate.Text) ?? DateTime.Now,
                GrossAmount = gross,
                VAT = _currentVat,
                RetentionDeduction = _currentRetention,
                InsuranceDeduction = _currentInsurance,
                OtherDeductions = _currentOther,
                AdvanceRecovery = _currentAdvance,
                NetPayableAmount = _currentNet
            };

            if (_isEdit) DateStampHelper.StampUpdated(statement);
            else DateStampHelper.StampCreated(statement);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorStatementRepository(_connectionString);
                if (_isEdit)
                    await Task.Run(() => repo.Update(statement));
                else
                    await Task.Run(() => repo.Add(statement));
            }, "در حال ذخیره صورت وضعیت...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("صورت وضعیت با موفقیت ذخیره شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractorStatementEditForm.SaveSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorStatementEditForm"); }
        }
    }
}
