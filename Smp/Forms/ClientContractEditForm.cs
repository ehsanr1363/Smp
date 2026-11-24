using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class ClientContractEditForm : Form
{
    private readonly string _connectionString;
    private readonly ClientContract _contract;
    private readonly bool _isEdit;

    private TextBox txtPartyName = null!;
    private int _selectedPartyId = 0;

    private TextBox txtContractNumber = null!, txtContractRow = null!, txtSubject = null!, txtAmount = null!;
    private TextBox txtContractDate = null!, txtEndDate = null!;
    private ComboBox cmbStatus = null!;
    private TextBox txtTerminationDate = null!;
    private TextBox txtVat = null!, txtRetention = null!, txtInsurance = null!, txtAdvancePercent = null!, txtTotalAdvance = null!, txtFeePercent = null!, txtOverhead = null!, txtFeeDivisor = null!;
    private CheckBox chkHasFee = null!;
    private ComboBox cmbWorkLocation = null!;
    private ModernButton btnSave = null!;

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    // ✅ FIX: جلوگیری از حلقه بی‌نهایت هنگام محاسبه درصد و مبلغ
    private bool _isCalculatingAdvance = false;

    public ClientContractEditForm(string connectionString, ClientContract? contract = null)
    {
        _connectionString = connectionString;
        _contract = contract ?? new ClientContract();
        _isEdit = contract is not null;

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        if (_isEdit) LoadData();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientContractEditForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        int fh = ModernUI.FieldHeight;

        ModernTextBox Box(bool center = false)
        {
            var b = ModernUI.Input();
            if (center) b.TextAlign = HorizontalAlignment.Center;
            return b;
        }

        // ───── کارت ۱: کارفرما ─────
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

        var cardParty = new ModernCard("کارفرما", ModernTheme.Primary, 1, fh);
        cardParty.Body.Controls.Add(ModernUI.Field("کارفرما", pnlParty), 0, 0);

        // ───── کارت ۲: مشخصات قرارداد ─────
        var numBox = Box(); txtContractNumber = numBox.TextBox;
        var rowBox = Box(); txtContractRow = rowBox.TextBox;
        var subjBox = Box(); txtSubject = subjBox.TextBox;
        var amountBox = Box(true); txtAmount = amountBox.TextBox; SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
        var dateBox = Box(true); txtContractDate = dateBox.TextBox; DateHelper.ApplyDateMask(txtContractDate);

        var cardInfo = new ModernCard("مشخصات قرارداد", ModernTheme.Teal, 2, fh, fh, fh);
        cardInfo.Body.Controls.Add(ModernUI.Field("شماره قرارداد", numBox), 0, 0);
        cardInfo.Body.Controls.Add(ModernUI.Field("ردیف قرارداد", rowBox), 1, 0);
        var fldSubject = ModernUI.Field("موضوع", subjBox);
        cardInfo.Body.Controls.Add(fldSubject, 0, 1);
        cardInfo.Body.SetColumnSpan(fldSubject, 2);
        cardInfo.Body.Controls.Add(ModernUI.Field("مبلغ (ریال)", amountBox), 0, 2);
        cardInfo.Body.Controls.Add(ModernUI.Field("تاریخ قرارداد", dateBox), 1, 2);

        // ───── کارت ۳: شرایط مالی ─────
        var vatBox = Box(true); txtVat = vatBox.TextBox; SmartInputHelper.EnablePercentPointsTextBox(txtVat);
        var retBox = Box(true); txtRetention = retBox.TextBox; SmartInputHelper.EnablePercentPointsTextBox(txtRetention);
        var insBox = Box(true); txtInsurance = insBox.TextBox; SmartInputHelper.EnablePercentPointsTextBox(txtInsurance);
        var advBox = Box(true); txtAdvancePercent = advBox.TextBox; SmartInputHelper.EnablePercentPointsTextBox(txtAdvancePercent);
        var totAdvBox = Box(true); txtTotalAdvance = totAdvBox.TextBox; SmartInputHelper.EnableSmartAmountTextBox(txtTotalAdvance);

        // قرارداد دو بخشی: کارکرد (بدون ارزش افزوده) + حق‌الزحمه (مشمول ارزش افزوده)
        chkHasFee = new CheckBox { Text = "قرارداد دو بخشی است: کارکرد (بدون ارزش افزوده) + حق‌الزحمه (مشمول ارزش افزوده)", AutoSize = true, RightToLeft = RightToLeft.Yes };
        var feeBox = Box(true); txtFeePercent = feeBox.TextBox; SmartInputHelper.EnablePercentPointsTextBox(txtFeePercent);
        txtFeePercent.Enabled = false;

        // اتصال به لیست حقوق کارگران روزمزد از طریق «محل خدمت»
        var overheadBox = Box(true); txtOverhead = overheadBox.TextBox; SmartInputHelper.EnablePercentPointsTextBox(txtOverhead);
        var divisorBox = Box(true); txtFeeDivisor = divisorBox.TextBox; SmartInputHelper.EnablePercentPointsTextBox(txtFeeDivisor);
        // مقسوم‌علیه «عدد درصد» است (۸۳٫۳۳)؛ اگر کسر وارد شود (۰٫۸۳۳۳) خودکار به درصد تبدیل می‌شود
        txtFeeDivisor.Leave += (s, e) =>
        {
            decimal v = ParsePercent(txtFeeDivisor.Text);
            if (v > 0m) txtFeeDivisor.Text = ClientFeeHelper.NormalizeDivisorPoints(v).ToString("0.####", CultureInfo.InvariantCulture);
        };
        cmbWorkLocation = ModernUI.Combo();
        cmbWorkLocation.Items.Add("");   // خالی = بدون اتصال به لیست حقوق
        try
        {
            foreach (var it in new LookupRepository(_connectionString).GetByCategory("WorkLocation"))
                cmbWorkLocation.Items.Add(it.Value);
        }
        catch (Exception ex) { LogHelper.LogException(ex, "ClientContractEditForm.LoadWorkLocations"); }

        void ApplyFeeEnabled()
        {
            txtFeePercent.Enabled = chkHasFee.Checked;
            cmbWorkLocation.Enabled = chkHasFee.Checked;
            txtOverhead.Enabled = chkHasFee.Checked;
            txtFeeDivisor.Enabled = chkHasFee.Checked;
        }
        ApplyFeeEnabled();
        chkHasFee.CheckedChanged += (s2, e2) => ApplyFeeEnabled();

        var cardFinance = new ModernCard("شرایط مالی", ModernTheme.Orange, 2, fh, fh, fh, fh, fh, fh, fh);
        cardFinance.Body.Controls.Add(ModernUI.Field("درصد ارزش افزوده (%)", vatBox), 0, 0);
        cardFinance.Body.Controls.Add(ModernUI.Field("درصد سپرده حسن انجام کار (%)", retBox), 1, 0);
        cardFinance.Body.Controls.Add(ModernUI.Field("درصد سپرده بیمه (%)", insBox), 0, 1);
        cardFinance.Body.Controls.Add(ModernUI.Field("درصد پیش‌دریافت (%)", advBox), 1, 1);
        var fldTotAdv = ModernUI.Field("مبلغ کل پیش‌دریافت", totAdvBox);
        cardFinance.Body.Controls.Add(fldTotAdv, 0, 2);
        cardFinance.Body.SetColumnSpan(fldTotAdv, 2);
        cardFinance.Body.Controls.Add(chkHasFee, 0, 3);
        cardFinance.Body.SetColumnSpan(chkHasFee, 2);
        var fldFee = ModernUI.Field("درصد حق‌الزحمه نسبت به کارکرد (%) — ارزش افزوده فقط روی حق‌الزحمه اعمال می‌شود", feeBox);
        cardFinance.Body.Controls.Add(fldFee, 0, 4);
        cardFinance.Body.SetColumnSpan(fldFee, 2);
        var fldLoc = ModernUI.Field("محل خدمت کارگران روزمزد (اتصال به لیست حقوق؛ خالی = بدون اتصال)", cmbWorkLocation);
        cardFinance.Body.Controls.Add(fldLoc, 0, 5);
        cardFinance.Body.SetColumnSpan(fldLoc, 2);
        cardFinance.Body.Controls.Add(ModernUI.Field("درصد بالاسری (%)", overheadBox), 0, 6);
        cardFinance.Body.Controls.Add(ModernUI.Field("درصد بیمه (مقسوم‌علیه حق‌الزحمه)", divisorBox), 1, 6);

        // ───── کارت ۴: مدت و وضعیت ─────
        var endBox = Box(true); txtEndDate = endBox.TextBox; DateHelper.ApplyDateMask(txtEndDate);
        cmbStatus = ModernUI.Combo();
        cmbStatus.Items.AddRange(ContractStatuses.All);
        cmbStatus.SelectedItem = ContractStatuses.Active;
        var termBox = Box(true); txtTerminationDate = termBox.TextBox; DateHelper.ApplyDateMask(txtTerminationDate);

        var cardStatus = new ModernCard("مدت و وضعیت", ModernTheme.Purple, 3, fh);
        cardStatus.Body.Controls.Add(ModernUI.Field("تاریخ پایان", endBox), 0, 0);
        cardStatus.Body.Controls.Add(ModernUI.Field("وضعیت قرارداد", cmbStatus), 1, 0);
        cardStatus.Body.Controls.Add(ModernUI.Field("تاریخ فسخ/خاتمه", termBox), 2, 0);

        // ✅ اتصال رویدادها
        txtAmount.TextChanged += CalculateTotalAdvance;
        txtAdvancePercent.TextChanged += CalculateTotalAdvance;
        txtTotalAdvance.TextChanged += CalculateAdvancePercent;

        // ───── صفحه‌ی اسکرول‌دار + هدر + نوار دکمه‌ها ─────
        var page = new ModernPage();
        foreach (var c in new Control[] { cardParty, cardInfo, cardFinance, cardStatus }) page.AddCard(c, 8);

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

        string title = _isEdit ? "ویرایش قرارداد کارفرما" : "افزودن قرارداد کارفرما";
        SuspendLayout();
        ModernForm.Setup(this, title, new Size(900, 780), isDialog: true);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(ModernForm.CreateHeader(title, "ثبت و ویرایش قرارداد با کارفرما"), 0, 0);
        root.Controls.Add(page, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
        ResumeLayout();
    }

    // ✅ FIX: همیشه TotalAdvance را بازنشانی می‌کند — دیگر مقدار قبلی نمی‌ماند
    private void CalculateTotalAdvance(object? sender, EventArgs e)
    {
        if (_isCalculatingAdvance) return;

        _isCalculatingAdvance = true;
        try
        {
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);

            if (chkHasFee.Checked && !string.IsNullOrWhiteSpace(cmbWorkLocation.Text) && ParsePercent(txtFeeDivisor.Text) <= 0m)
            {
                ModernMessageBox.ShowWarning("برای اتصال به لیست حقوق، «درصد بیمه (مقسوم‌علیه حق‌الزحمه)» باید بزرگ‌تر از صفر باشد.");
                txtFeeDivisor.Focus();
                return;
            }
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
        finally
        {
            _isCalculatingAdvance = false;
        }
    }

    // ✅ محاسبه درصد از روی مبلغ (وقتی کاربر مبلغ را دستی وارد می‌کند)
    private void CalculateAdvancePercent(object? sender, EventArgs e)
    {
        if (_isCalculatingAdvance) return;

        _isCalculatingAdvance = true;
        try
        {
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            decimal totalAdvance = NumberHelper.ParseFormatted(txtTotalAdvance.Text);

            if (amount > 0)
            {
                decimal percent = (totalAdvance / amount) * 100;
                txtAdvancePercent.Text = percent.ToString("0.##");
            }
        }
        finally
        {
            _isCalculatingAdvance = false;
        }
    }

    private void BtnSelect_Click(object? sender, EventArgs e)
    {
        var result = PartySelectionHelper.ShowSelectionDialog(_connectionString, isContractor: false, owner: this);
        if (result.Id > 0 && !IsDisposed)
        {
            _selectedPartyId = result.Id;
            txtPartyName.Text = result.Name;
        }
    }

    // ✅ FIX: در طول بارگذاری، _isCalculatingAdvance = true است تا هیچ بازمحاسبه‌ای رخ ندهد
    // و مقادیر ذخیره‌شده در دیتابیس دست‌نخورده بمانند
    private void LoadData()
    {
        _isCalculatingAdvance = true;
        try
        {
            _selectedPartyId = _contract.PartyId;
            txtPartyName.Text = _contract.DisplayName;

            txtContractNumber.Text = _contract.ContractNumber;
            txtContractRow.Text = _contract.ContractRow;
            txtSubject.Text = _contract.Subject;
            txtAmount.Text = NumberHelper.FormatThousands(_contract.Amount);
            txtContractDate.Text = DateHelper.ToPersianDate(_contract.ContractDate);
            txtEndDate.Text = DateHelper.ToPersianDate(_contract.EndDate);

            // ✅ وضعیت ثبت‌شده حفظ و نمایش داده می‌شود (قبلاً با هر ویرایش به «فعال» برمی‌گشت)
            ContractStatuses.EnsureItem(cmbStatus, _contract.Status);
            txtTerminationDate.Text = DateHelper.ToPersianDate(_contract.TerminationDate);

            txtVat.Text = ToDisplayPercent(_contract.VatPercentage).ToString("0.##");
            txtRetention.Text = ToDisplayPercent(_contract.RetentionPercentage).ToString("0.##");
            txtInsurance.Text = ToDisplayPercent(_contract.InsurancePercentage).ToString("0.##");
            txtAdvancePercent.Text = ToDisplayPercent(_contract.AdvancePaymentPercentage).ToString("0.##");

            txtTotalAdvance.Text = NumberHelper.FormatThousands(_contract.TotalAdvanceAmount, usePersianDigits: true);

            chkHasFee.Checked = _contract.HasFeeSection;
            txtFeePercent.Text = ToDisplayPercent(_contract.FeePercentage).ToString("0.##");

            if (!string.IsNullOrWhiteSpace(_contract.WorkLocation))
            {
                if (!cmbWorkLocation.Items.Contains(_contract.WorkLocation)) cmbWorkLocation.Items.Add(_contract.WorkLocation);
                cmbWorkLocation.SelectedItem = _contract.WorkLocation;
            }
            txtOverhead.Text = ToDisplayPercent(_contract.OverheadPercentage).ToString("0.##");
            txtFeeDivisor.Text = ClientFeeHelper.NormalizeDivisorPoints(ToDisplayPercent(_contract.FeeDivisorPercentage)).ToString("0.####", CultureInfo.InvariantCulture);
        }
        finally
        {
            _isCalculatingAdvance = false;
        }
    }

    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (_selectedPartyId == 0)
            {
                ModernMessageBox.ShowWarning("لطفاً یک کارفرما را از لیست انتخاب کنید.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(txtContractDate.Text) && !DateHelper.IsValidPersianDate(txtContractDate.Text))
            {
                ModernMessageBox.ShowWarning("تاریخ قرارداد وارد شده معتبر نیست.");
                txtContractDate.Focus();
                return;
            }

            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);

            var c = new ClientContract
            {
                Id = _isEdit ? _contract.Id : 0,
                PartyId = _selectedPartyId,
                ContractNumber = txtContractNumber.Text.Trim(),
                ContractRow = txtContractRow.Text.Trim(),
                Subject = txtSubject.Text.Trim(),
                Amount = amount,
                ContractDate = DateHelper.ParsePersianDate(txtContractDate.Text),
                EndDate = DateHelper.ParsePersianDate(txtEndDate.Text),

                VatPercentage = ParsePercent(txtVat.Text),
                RetentionPercentage = ParsePercent(txtRetention.Text),
                InsurancePercentage = ParsePercent(txtInsurance.Text),
                AdvancePaymentPercentage = ParsePercent(txtAdvancePercent.Text),
                TotalAdvanceAmount = NumberHelper.ParseFormatted(txtTotalAdvance.Text),
                HasFeeSection = chkHasFee.Checked,
                FeePercentage = chkHasFee.Checked ? ParsePercent(txtFeePercent.Text) : 0,
                WorkLocation = chkHasFee.Checked ? cmbWorkLocation.Text.Trim() : "",
                OverheadPercentage = chkHasFee.Checked ? ParsePercent(txtOverhead.Text) : 0,
                FeeDivisorPercentage = chkHasFee.Checked ? ClientFeeHelper.NormalizeDivisorPoints(ParsePercent(txtFeeDivisor.Text)) : 0,

                Status = cmbStatus.Text,
                TerminationDate = ContractStatuses.ResolveTerminationDate(cmbStatus.Text, DateHelper.ParsePersianDate(txtTerminationDate.Text))
            };

            if (_isEdit) DateStampHelper.StampUpdated(c);
            else DateStampHelper.StampCreated(c);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ClientContractRepository(_connectionString);

                if (_isEdit)
                {
                    await Task.Run(() => repo.Update(c));
                }
                else
                {
                    await Task.Run(() => repo.Add(c));
                }
            }, "در حال ذخیره قرارداد...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("قرارداد با موفقیت ذخیره شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientContractEditForm.SaveSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientContractEditForm"); }
        }
    }

    private static decimal ParsePercent(string text)
    {
        string clean = StringHelper.ConvertToEnglishDigits(text ?? "")
            .Replace("%", "")
            .Replace("٫", ".")
            .Replace("/", ".")
            .Replace(",", "")
            .Replace("٬", "")
            .Trim();

        if (!decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v)) return 0m;
        return v < 0 ? 0m : v;
    }

    private static decimal ToDisplayPercent(decimal percent) => percent;
}
