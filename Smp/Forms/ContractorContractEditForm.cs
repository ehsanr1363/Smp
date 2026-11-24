using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Models;
using Smp.Helpers;
using Smp.Data;

namespace Smp.Forms;

public class ContractorContractEditForm : Form
{
    private readonly string _connectionString;
    private readonly ContractorContract? _contract;
    private readonly bool _isEdit;

    private TextBox txtPartyName = null!;
    private int _selectedPartyId = 0;
    private ComboBox cmbClientContract = null!;
    private ComboBox cmbStatus = null!;
    private TextBox txtTerminationDate = null!;

    private TextBox txtContractNumber = null!, txtContractRow = null!, txtSubject = null!, txtAmount = null!, txtContractDate = null!, txtEndDate = null!;
    private TextBox txtDurationMonths = null!;
    private TextBox txtGuarantee = null!, txtRetention = null!, txtInsurance = null!, txtAdvancePercent = null!, txtTotalAdvance = null!, txtVat = null!;
    private ModernButton btnSave = null!;

    private Label lblSumTotalAmount = null!, lblSumStatements = null!, lblSumPaid = null!, lblSumGoods = null!, lblSumServices = null!, lblSumDebt = null!;

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    // ✅ نگه‌داری پیام هشدار برای نمایش در Shown
    private string? _pendingWarning = null;

    public ContractorContractEditForm(string connectionString, ContractorContract? contract = null)
    {
        _connectionString = connectionString;
        _contract = contract;
        _isEdit = contract is not null;

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        Shown += async (s, e) =>
        {
            try
            {
                await LoadClientContractsAsync();
                if (_isEdit && _contract is not null)
                {
                    await LoadDataAsync();
                    await LoadFinancialSummaryAsync();
                }

                // ✅ نمایش هشدار در صورت وجود
                if (!string.IsNullOrEmpty(_pendingWarning) && !IsDisposed)
                {
                    ModernMessageBox.ShowWarning(_pendingWarning);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ContractorContractEditForm.Shown");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری فرم:\n" + ex.Message);
            }
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorContractEditForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        int fh = ModernUI.FieldHeight;

        // ───── کارت ۱: طرفین قرارداد ─────
        var partyBox = ModernUI.Input();
        partyBox.ReadOnly = true;
        partyBox.TextAlign = HorizontalAlignment.Center;
        txtPartyName = partyBox.TextBox;
        partyBox.Dock = DockStyle.Fill;
        partyBox.Margin = new Padding(0, 0, 6, 0);

        var btnSelect = ModernButton.Soft("🔍 انتخاب", ModernTheme.Teal, 90, ModernUI.InputHeight);
        btnSelect.Anchor = AnchorStyles.None;
        btnSelect.Click += BtnSelect_Click;

        var pnlParty = new BufferedTable { Margin = Padding.Empty, BackColor = Color.Transparent, ColumnCount = 2, RowCount = 1, RightToLeft = RightToLeft.Yes };
        pnlParty.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pnlParty.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        pnlParty.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        pnlParty.Controls.Add(partyBox, 0, 0);
        pnlParty.Controls.Add(btnSelect, 1, 0);

        cmbClientContract = ModernUI.Combo();

        var cardParties = new ModernCard("طرفین قرارداد", ModernTheme.Primary, 2, fh, fh);
        var fldParty = ModernUI.Field("پیمانکار", pnlParty);
        cardParties.Body.Controls.Add(fldParty, 0, 0);
        cardParties.Body.SetColumnSpan(fldParty, 2);
        var fldClientContract = ModernUI.Field("قرارداد کارفرما (اجباری)", cmbClientContract);
        cardParties.Body.Controls.Add(fldClientContract, 0, 1);
        cardParties.Body.SetColumnSpan(fldClientContract, 2);

        // ───── کارت ۲: مشخصات قرارداد ─────
        ModernTextBox Box(bool center = false)
        {
            var b = ModernUI.Input();
            if (center) b.TextAlign = HorizontalAlignment.Center;
            return b;
        }

        var numBox = Box(); txtContractNumber = numBox.TextBox;
        var rowBox = Box(); txtContractRow = rowBox.TextBox;
        var subjBox = Box(); txtSubject = subjBox.TextBox;
        var amountBox = Box(true); txtAmount = amountBox.TextBox; SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
        var dateBox = Box(true); txtContractDate = dateBox.TextBox; DateHelper.ApplyDateMask(txtContractDate);
        var durBox = Box(true); txtDurationMonths = durBox.TextBox; txtDurationMonths.Text = "0";
        var endBox = Box(true); txtEndDate = endBox.TextBox; DateHelper.ApplyDateMask(txtEndDate);

        var cardInfo = new ModernCard("مشخصات قرارداد", ModernTheme.Teal, 2, fh, fh, fh, fh);
        cardInfo.Body.Controls.Add(ModernUI.Field("شماره قرارداد", numBox), 0, 0);
        cardInfo.Body.Controls.Add(ModernUI.Field("ردیف پیمان", rowBox), 1, 0);
        var fldSubject = ModernUI.Field("موضوع", subjBox);
        cardInfo.Body.Controls.Add(fldSubject, 0, 1);
        cardInfo.Body.SetColumnSpan(fldSubject, 2);
        cardInfo.Body.Controls.Add(ModernUI.Field("مبلغ (ریال)", amountBox), 0, 2);
        cardInfo.Body.Controls.Add(ModernUI.Field("تاریخ قرارداد", dateBox), 1, 2);
        cardInfo.Body.Controls.Add(ModernUI.Field("مدت (ماه)", durBox), 0, 3);
        cardInfo.Body.Controls.Add(ModernUI.Field("تاریخ پایان", endBox), 1, 3);

        // ───── کارت ۳: شرایط مالی ─────
        var guarBox = Box(true); txtGuarantee = guarBox.TextBox; txtGuarantee.Text = "0";
        var retBox = Box(true); txtRetention = retBox.TextBox; txtRetention.Text = "0";
        var insBox = Box(true); txtInsurance = insBox.TextBox; txtInsurance.Text = "0";
        var advBox = Box(true); txtAdvancePercent = advBox.TextBox; txtAdvancePercent.Text = "0";
        var totAdvBox = Box(true); totAdvBox.ReadOnly = true; txtTotalAdvance = totAdvBox.TextBox; txtTotalAdvance.Text = "0";
        var vatBox = Box(true); txtVat = vatBox.TextBox; txtVat.Text = "0";

        var cardFinance = new ModernCard("شرایط مالی", ModernTheme.Orange, 2, fh, fh, fh);
        cardFinance.Body.Controls.Add(ModernUI.Field("درصد تضمین (%)", guarBox), 0, 0);
        cardFinance.Body.Controls.Add(ModernUI.Field("درصد حسن انجام کار (%)", retBox), 1, 0);
        cardFinance.Body.Controls.Add(ModernUI.Field("درصد بیمه (%)", insBox), 0, 1);
        cardFinance.Body.Controls.Add(ModernUI.Field("درصد پیش‌پرداخت (%)", advBox), 1, 1);
        cardFinance.Body.Controls.Add(ModernUI.Field("مبلغ کل پیش‌پرداخت", totAdvBox), 0, 2);
        cardFinance.Body.Controls.Add(ModernUI.Field("درصد ارزش افزوده (%)", vatBox), 1, 2);

        // ───── کارت ۴: وضعیت ─────
        cmbStatus = ModernUI.Combo();
        cmbStatus.Items.AddRange(ContractStatuses.All);
        cmbStatus.SelectedItem = ContractStatuses.Active;
        var termBox = Box(true); txtTerminationDate = termBox.TextBox; DateHelper.ApplyDateMask(txtTerminationDate);

        var cardStatus = new ModernCard("وضعیت قرارداد", ModernTheme.Purple, 2, fh);
        cardStatus.Body.Controls.Add(ModernUI.Field("وضعیت قرارداد", cmbStatus), 0, 0);
        cardStatus.Body.Controls.Add(ModernUI.Field("تاریخ فسخ/خاتمه", termBox), 1, 0);

        // ───── کارت ۵: خلاصه وضعیت مالی (فقط ویرایش) ─────
        int sh = 40;
        var cardSummary = new ModernCard("📊 خلاصه وضعیت مالی پیمانکار", ModernTheme.Primary, 2, sh, sh, sh);
        lblSumTotalAmount = CreateSummaryLabel(cardSummary, "مبلغ کل قرارداد", 0, 0);
        lblSumStatements = CreateSummaryLabel(cardSummary, "جمع خالص صورت‌وضعیت‌ها", 1, 0);
        lblSumPaid = CreateSummaryLabel(cardSummary, "جمع پرداخت‌های انجام شده", 0, 1);
        lblSumGoods = CreateSummaryLabel(cardSummary, "↳ بابت خرید کالا", 1, 1);
        lblSumServices = CreateSummaryLabel(cardSummary, "↳ بابت خرید خدمات", 0, 2);
        lblSumDebt = CreateSummaryLabel(cardSummary, "مانده بدهی به پیمانکار", 1, 2);

        // ✅ اتصال رویدادها برای محاسبات خودکار
        txtAmount.TextChanged += CalculateTotalAdvance;
        txtAdvancePercent.TextChanged += CalculateTotalAdvance;
        txtContractDate.Leave += CalculateEndDate;
        txtDurationMonths.TextChanged += CalculateEndDate;

        // ───── صفحه‌ی اسکرول‌دار + هدر + نوار دکمه‌ها ─────
        var cards = new List<Control> { cardParties, cardInfo, cardFinance, cardStatus };
        if (_isEdit) cards.Add(cardSummary);

        var page = new ModernPage();
        foreach (var c in cards) page.AddCard(c, 8);

        btnSave = ModernButton.Success("💾 ذخیره قرارداد", 220, 40);
        btnSave.Click += async (s, e) => await SaveSafeAsync();
        var btnCancel = ModernButton.Cancel();
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var bar = ModernForm.CreateButtonBar(btnSave, btnCancel);
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        string title = _isEdit ? "ویرایش قرارداد پیمانکار" : "افزودن قرارداد پیمانکار";
        SuspendLayout();
        ModernForm.Setup(this, title, new Size(950, 800), isDialog: true);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(ModernForm.CreateHeader(title, "ثبت و ویرایش قرارداد با پیمانکار"), 0, 0);
        root.Controls.Add(page, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
        ResumeLayout();
    }

    private Label CreateSummaryLabel(ModernCard card, string title, int col, int row)
    {
        var lblValue = ModernUI.Caption("---", 10.5f, true, ModernTheme.Text);
        lblValue.Dock = DockStyle.Fill;
        lblValue.TextAlign = ContentAlignment.MiddleRight;
        card.Body.Controls.Add(ModernUI.Row(title, lblValue, 170), col, row);
        return lblValue;
    }

    private async Task LoadFinancialSummaryAsync()
    {
        if (_contract is null) return;
        try
        {
            var stRepo = new ContractorStatementRepository(_connectionString);
            var transRepo = new CashTransactionRepository(_connectionString);
            var checkRepo = new CashCheckRepository(_connectionString);
            var legacyPayRepo = new ContractorPaymentRepository(_connectionString);
            var cRepo = new ContractorContractRepository(_connectionString);

            var stList = await Task.Run(() => stRepo.GetByContractId(_contract.Id));
            var transPayments = await transRepo.GetByContractorContractIdAsync(_contract.Id);
            var checkPayments = await checkRepo.GetByContractorContractIdAsync(_contract.Id);
            var legacyPayments = await Task.Run(() => legacyPayRepo.GetByContractId(_contract.Id));
            var con = await Task.Run(() => cRepo.GetById(_contract.Id));

            if (con is null || IsDisposed) return;

            decimal totalAmount = con.Amount;
            decimal totalStatements = stList.Sum(x => x.NetPayableAmount);

            var filteredTrans = transPayments.Where(t => t.Direction == DirectionTypes.Outgoing).ToList();
            var filteredChecks = checkPayments.Where(c => c.CheckType == DirectionTypes.Outgoing && c.Status != "باطل شده" && c.Status != "برگشتی").ToList();
            var oldPayments = legacyPayments ?? new List<ContractorPayment>();

            decimal totalPaid = filteredTrans.Sum(t => t.Amount) + filteredChecks.Sum(c => c.Amount) + oldPayments.Sum(p => p.Amount);

            decimal paidGoods = filteredTrans.Where(t => t.CostType == "خرید کالا").Sum(t => t.Amount) +
                                filteredChecks.Where(c => c.CostType == "خرید کالا").Sum(c => c.Amount) +
                                oldPayments.Where(p => p.CostType == "خرید کالا").Sum(p => p.Amount);

            decimal paidServices = filteredTrans.Where(t => t.CostType == "خرید خدمات").Sum(t => t.Amount) +
                                   filteredChecks.Where(c => c.CostType == "خرید خدمات").Sum(c => c.Amount) +
                                   oldPayments.Where(p => p.CostType == "خرید خدمات").Sum(p => p.Amount);

            decimal debt = totalStatements - totalPaid;

            lblSumTotalAmount.Text = NumberHelper.FormatThousands(totalAmount, usePersianDigits: true);
            lblSumStatements.Text = NumberHelper.FormatThousands(totalStatements, usePersianDigits: true);
            lblSumPaid.Text = NumberHelper.FormatThousands(totalPaid, usePersianDigits: true);
            lblSumGoods.Text = NumberHelper.FormatThousands(paidGoods, usePersianDigits: true);
            lblSumServices.Text = NumberHelper.FormatThousands(paidServices, usePersianDigits: true);

            lblSumDebt.Text = NumberHelper.FormatThousands(debt, usePersianDigits: true);
            lblSumDebt.ForeColor = debt > 0 ? ModernTheme.Danger : (debt < 0 ? ModernTheme.Success : ModernTheme.Text);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractorContractEditForm.LoadFinancialSummary");
        }
    }

    private async Task LoadClientContractsAsync()
    {
        var (success, list) = await AsyncHelper.RunWithLoading(
            this,
            () => Task.Run(() => new ClientContractRepository(_connectionString).GetAll()),
            "در حال بارگذاری لیست قراردادها...");

        if (!success || list is null || IsDisposed) return;

        if (!list.Any())
        {
            ModernMessageBox.ShowWarning("در سیستم هیچ قرارداد کارفرمایی ثبت نشده است. برای ثبت قرارداد پیمانکار، ابتدا باید یک قرارداد کارفرما ایجاد کنید.");
            cmbClientContract.Enabled = false;
            btnSave.Enabled = false;
            return;
        }

        var displayList = list.Select(c => new
        {
            c.Id,
            DisplayInfo = string.IsNullOrWhiteSpace(c.Subject) ? c.ContractNumber : $"{c.ContractNumber} - {c.Subject}"
        }).ToList();

        cmbClientContract.DataSource = null;
        cmbClientContract.DisplayMember = "DisplayInfo";
        cmbClientContract.ValueMember = "Id";
        cmbClientContract.DataSource = displayList;
    }

    private void CalculateEndDate(object? sender, EventArgs e)
    {
        if (int.TryParse(txtDurationMonths.Text, out int months) && months > 0)
        {
            DateTime? startDate = DateHelper.ParsePersianDate(txtContractDate.Text);
            if (startDate.HasValue)
            {
                DateTime endDate = startDate.Value.AddMonths(months);
                txtEndDate.Text = DateHelper.ToPersianDate(endDate);
            }
        }
    }

    private void CalculateTotalAdvance(object? sender, EventArgs e)
    {
        decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
        decimal percent = NumberHelper.ParseFormatted(txtAdvancePercent.Text);

        if (amount > 0 && percent > 0)
        {
            decimal total = (amount * percent) / 100;
            txtTotalAdvance.Text = NumberHelper.FormatThousands(total, usePersianDigits: true);
        }
        else
        {
            txtTotalAdvance.Text = "0";
        }
    }

    private void BtnSelect_Click(object? sender, EventArgs e)
    {
        var result = PartySelectionHelper.ShowSelectionDialog(_connectionString, isContractor: true, owner: this);
        if (result.Id > 0 && !IsDisposed)
        {
            _selectedPartyId = result.Id;
            txtPartyName.Text = result.Name;
        }
    }

    private async Task LoadDataAsync()
    {
        if (_contract is null || IsDisposed) return;

        _selectedPartyId = _contract.PartyId;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            string query = "SELECT PartyType, FirstName, LastName, CompanyName FROM Parties WHERE Id = @id";
            var dt = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, query, new Microsoft.Data.SqlClient.SqlParameter("@id", _selectedPartyId)));

            if (dt.Rows.Count > 0 && !IsDisposed)
            {
                var row = dt.Rows[0];
                string type = row["PartyType"].ToString()!;
                txtPartyName.Text = type == "حقیقی" ? $"{row["FirstName"]} {row["LastName"]}" : row["CompanyName"].ToString();
            }
        }, "در حال دریافت اطلاعات پیمانکار...");

        if (!success || IsDisposed) return;

        if (_contract.ClientContractId.HasValue && _contract.ClientContractId.Value > 0)
        {
            bool found = false;
            for (int i = 0; i < cmbClientContract.Items.Count; i++)
            {
                var item = cmbClientContract.Items[i];
                var idProp = item.GetType().GetProperty("Id");
                if (idProp is not null && (int)idProp.GetValue(item)! == _contract.ClientContractId.Value)
                {
                    cmbClientContract.SelectedIndex = i;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                cmbClientContract.SelectedIndex = -1;
                _pendingWarning = $"⚠️ قرارداد کارفرمای مرتبط با این قرارداد پیمانکار (شناسه: {_contract.ClientContractId.Value}) در سیستم یافت نشد.\n\nلطفاً یک قرارداد کارفرما از لیست انتخاب کنید.";
            }
        }
        else
        {
            cmbClientContract.SelectedIndex = -1;
            _pendingWarning = "⚠️ این قرارداد پیمانکار به هیچ قرارداد کارفرمایی متصل نیست (قرارداد قدیمی).\n\nلطفاً یک قرارداد کارفرما انتخاب کنید.";
        }

        txtContractNumber.Text = _contract.ContractNumber;
        txtContractRow.Text = _contract.ContractRow;
        txtSubject.Text = _contract.Subject;
        txtAmount.Text = NumberHelper.FormatThousands(_contract.Amount, usePersianDigits: true);
        txtContractDate.Text = DateHelper.ToPersianDate(_contract.ContractDate);
        txtEndDate.Text = DateHelper.ToPersianDate(_contract.EndDate);

        if (_contract.ContractDate.HasValue && _contract.EndDate.HasValue)
        {
            int monthDiff = ((_contract.EndDate.Value.Year - _contract.ContractDate.Value.Year) * 12) + _contract.EndDate.Value.Month - _contract.ContractDate.Value.Month;
            txtDurationMonths.Text = monthDiff.ToString();
        }

        txtGuarantee.Text = _contract.GuaranteePercentage.ToString("0.##");
        txtRetention.Text = _contract.RetentionPercentage.ToString("0.##");
        txtInsurance.Text = _contract.InsurancePercentage.ToString("0.##");
        txtAdvancePercent.Text = _contract.AdvancePaymentPercentage.ToString("0.##");
        txtTotalAdvance.Text = NumberHelper.FormatThousands(_contract.TotalAdvanceAmount, usePersianDigits: true);
        txtVat.Text = _contract.VatPercentage.ToString("0.##");

        // ✅ وضعیت ثبت‌شده حفظ می‌شود (قبلاً ذخیره/نمایش داده نمی‌شد)
        ContractStatuses.EnsureItem(cmbStatus, _contract.Status);
        txtTerminationDate.Text = DateHelper.ToPersianDate(_contract.TerminationDate);
    }

    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (_selectedPartyId == 0)
            {
                ModernMessageBox.ShowWarning("لطفاً یک پیمانکار را از لیست انتخاب کنید.");
                return;
            }

            if (cmbClientContract.SelectedItem is null)
            {
                ModernMessageBox.ShowWarning("انتخاب «قرارداد کارفرما» الزامی است.");
                return;
            }

            if (!DateHelper.IsValidPersianDate(txtContractDate.Text) || (!string.IsNullOrWhiteSpace(txtEndDate.Text) && !DateHelper.IsValidPersianDate(txtEndDate.Text)))
            {
                ModernMessageBox.ShowWarning("فرمت تاریخ وارد شده معتبر نیست.");
                return;
            }

            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            decimal guarantee = NumberHelper.ParseFormatted(txtGuarantee.Text);
            decimal retention = NumberHelper.ParseFormatted(txtRetention.Text);
            decimal insurance = NumberHelper.ParseFormatted(txtInsurance.Text);
            decimal advancePercent = NumberHelper.ParseFormatted(txtAdvancePercent.Text);
            decimal totalAdvance = NumberHelper.ParseFormatted(txtTotalAdvance.Text);
            decimal vatPercent = NumberHelper.ParseFormatted(txtVat.Text);

            int? selectedClientContractId = null;
            if (cmbClientContract.SelectedItem is not null)
            {
                var idProp = cmbClientContract.SelectedItem.GetType().GetProperty("Id");
                if (idProp is not null)
                {
                    int id = (int)idProp.GetValue(cmbClientContract.SelectedItem)!;
                    if (id > 0) selectedClientContractId = id;
                }
            }

            // ✅ اعتبارسنجی‌های تکمیلی
            if (string.IsNullOrWhiteSpace(txtContractNumber.Text))
            {
                ModernMessageBox.ShowWarning("شماره قرارداد را وارد کنید.");
                txtContractNumber.Focus();
                return;
            }

            if (amount <= 0)
            {
                ModernMessageBox.ShowWarning("مبلغ قرارداد باید بزرگ‌تر از صفر باشد.");
                txtAmount.Focus();
                return;
            }

            var percentChecks = new (string Name, decimal Value)[]
            {
                ("درصد تضمین", guarantee), ("درصد حسن انجام کار", retention), ("درصد بیمه", insurance),
                ("درصد پیش‌پرداخت", advancePercent), ("درصد ارزش افزوده", vatPercent)
            };
            foreach (var pc in percentChecks)
            {
                if (pc.Value < 0m || pc.Value > 100m)
                {
                    ModernMessageBox.ShowWarning($"{pc.Name} باید عددی بین ۰ تا ۱۰۰ باشد (۱۰ یعنی ۱۰٪).");
                    return;
                }
            }

            DateTime? startDate = DateHelper.ParsePersianDate(txtContractDate.Text);
            DateTime? finishDate = DateHelper.ParsePersianDate(txtEndDate.Text);
            if (startDate.HasValue && finishDate.HasValue && finishDate.Value.Date < startDate.Value.Date)
            {
                ModernMessageBox.ShowWarning("تاریخ پایان نباید قبل از تاریخ قرارداد باشد.");
                txtEndDate.Focus();
                return;
            }

            if (!string.IsNullOrWhiteSpace(txtTerminationDate.Text) && !DateHelper.IsValidPersianDate(txtTerminationDate.Text))
            {
                ModernMessageBox.ShowWarning("تاریخ فسخ/خاتمه معتبر نیست.");
                txtTerminationDate.Focus();
                return;
            }

            int excludeId = _isEdit && _contract is not null ? _contract.Id : 0;
            var checkRepo = new ContractorContractRepository(_connectionString);

            string numberToCheck = txtContractNumber.Text.Trim();
            bool duplicateNumber = await Task.Run(() => checkRepo.ContractNumberExists(numberToCheck, excludeId));
            if (duplicateNumber)
            {
                ModernMessageBox.ShowWarning($"شماره قرارداد «{numberToCheck}» قبلاً برای قرارداد پیمانکار دیگری ثبت شده است.");
                txtContractNumber.Focus();
                return;
            }

            // کنترل سقف: جمع قراردادهای پیمانکاری یک پروژه نباید از مبلغ قرارداد کارفرما بیشتر شود (هشدار، نه ممنوعیت)
            if (selectedClientContractId.HasValue)
            {
                int clientId = selectedClientContractId.Value;
                var (clientAmount, othersTotal) = await Task.Run(() =>
                {
                    var cc = new ClientContractRepository(_connectionString).GetById(clientId);
                    return (cc?.Amount ?? 0m, checkRepo.GetTotalAmountByClientContract(clientId, excludeId));
                });

                if (clientAmount > 0m && othersTotal + amount > clientAmount)
                {
                    string msg = $"مجموع قراردادهای پیمانکاری این قرارداد کارفرما ({NumberHelper.FormatThousands(othersTotal + amount, usePersianDigits: true)} ریال) " +
                                 $"از مبلغ قرارداد کارفرما ({NumberHelper.FormatThousands(clientAmount, usePersianDigits: true)} ریال) بیشتر می‌شود.\n\nادامه می‌دهید؟";
                    if (!ModernMessageBox.AskConfirm(msg, "هشدار سقف قرارداد")) return;
                }
            }

            var c = new ContractorContract
            {
                Id = _isEdit && _contract is not null ? _contract.Id : 0,
                PartyId = _selectedPartyId,
                ClientContractId = selectedClientContractId,
                ContractNumber = txtContractNumber.Text.Trim(),
                ContractRow = txtContractRow.Text.Trim(),
                Subject = txtSubject.Text.Trim(),
                Amount = amount,
                ContractDate = DateHelper.ParsePersianDate(txtContractDate.Text),
                EndDate = DateHelper.ParsePersianDate(txtEndDate.Text),
                GuaranteePercentage = guarantee,
                RetentionPercentage = retention,
                InsurancePercentage = insurance,
                AdvancePaymentPercentage = advancePercent,
                TotalAdvanceAmount = totalAdvance,
                VatPercentage = vatPercent,

                Status = cmbStatus.Text,
                TerminationDate = ContractStatuses.ResolveTerminationDate(cmbStatus.Text, DateHelper.ParsePersianDate(txtTerminationDate.Text))
            };

            if (_isEdit) DateStampHelper.StampUpdated(c);
            else DateStampHelper.StampCreated(c);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorContractRepository(_connectionString);
                if (_isEdit)
                    await Task.Run(() => repo.Update(c));
                else
                    await Task.Run(() => repo.Add(c));
            }, "در حال ذخیره قرارداد...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("قرارداد پیمانکار با موفقیت ذخیره شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractorContractEditForm.SaveSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractorContractEditForm"); }
        }
    }
}
