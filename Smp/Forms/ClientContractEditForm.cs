using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class ClientContractEditForm : Form
    {
        private readonly string _connectionString;
        private readonly ClientContract _contract;
        private readonly bool _isEdit;

        private TextBox txtPartyName = null!;
        private int _selectedPartyId = 0;

        private TextBox txtContractNumber = null!, txtContractRow = null!, txtSubject = null!, txtAmount = null!;
        private TextBox txtContractDate = null!, txtEndDate = null!;
        private TextBox txtVat = null!, txtRetention = null!, txtInsurance = null!, txtAdvancePercent = null!, txtTotalAdvance = null!;
        private Button btnSave = null!;

        private readonly SemaphoreSlim _actionLock = new(1, 1);

        // ✅ FIX: جلوگیری از حلقه بی‌نهایت هنگام محاسبه درصد و مبلغ
        private bool _isCalculatingAdvance = false;

        public ClientContractEditForm(string connectionString, ClientContract? contract = null)
        {
            _connectionString = connectionString;
            _contract = contract ?? new ClientContract();
            _isEdit = contract != null;

            BuildUI();
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            if (_isEdit) LoadData();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var (formLayout, btnPanel) = StandardFormHelper.SetupStandardLayout(
                this, _isEdit ? "ویرایش قرارداد کارفرما" : "افزودن قرارداد کارفرما", new Size(900, 750));

            // ─── ردیف ۰: انتخاب کارفرما ───
            StandardFormHelper.AddRow(formLayout, 50F);

            var pnlParty = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), RightToLeft = RightToLeft.Yes };
            pnlParty.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlParty.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));

            txtPartyName = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, TextAlign = HorizontalAlignment.Center, BackColor = UIHelper.WarmInputBackground, Font = UIHelper.PersianFont(11f), Margin = new Padding(0, 0, 5, 0) };
            var btnSelect = UIHelper.CreateButton("🔍 انتخاب", UIHelper.WarmSecondary, 80, 35);
            btnSelect.Dock = DockStyle.Fill;
            btnSelect.Margin = new Padding(0);
            btnSelect.Click += BtnSelect_Click;

            pnlParty.Controls.Add(txtPartyName, 0, 0);
            pnlParty.Controls.Add(btnSelect, 1, 0);
            UIHelper.AddField(formLayout, "کارفرما:", pnlParty, 0, 0, 1, 3);

            // ─── ردیف ۱: شماره و ردیف قرارداد ───
            StandardFormHelper.AddRow(formLayout);
            txtContractNumber = new TextBox();
            txtContractRow = new TextBox();
            UIHelper.AddField(formLayout, "شماره قرارداد:", txtContractNumber, 1, 0, 1);
            UIHelper.AddField(formLayout, "ردیف قرارداد:", txtContractRow, 1, 2, 3);

            // ─── ردیف ۲: موضوع ───
            StandardFormHelper.AddRow(formLayout);
            txtSubject = new TextBox();
            UIHelper.AddField(formLayout, "موضوع:", txtSubject, 2, 0, 1, 3);

            // ─── ردیف ۳: مبلغ و تاریخ قرارداد ───
            StandardFormHelper.AddRow(formLayout);
            txtAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
            txtContractDate = new TextBox();
            DateHelper.ApplyDateMask(txtContractDate);

            UIHelper.AddField(formLayout, "مبلغ (ریال):", txtAmount, 3, 0, 1);
            UIHelper.AddField(formLayout, "تاریخ قرارداد:", txtContractDate, 3, 2, 3);

            // ─── ردیف ۴: تاریخ پایان ───
            StandardFormHelper.AddRow(formLayout);
            txtEndDate = new TextBox();
            DateHelper.ApplyDateMask(txtEndDate);
            UIHelper.AddField(formLayout, "تاریخ پایان:", txtEndDate, 4, 0, 1);

            // ─── ردیف ۵: درصد ارزش افزوده و حسن انجام کار ───
            StandardFormHelper.AddRow(formLayout);
            txtVat = new TextBox(); SmartInputHelper.EnableSmartPercentageTextBox(txtVat);
            txtRetention = new TextBox(); SmartInputHelper.EnableSmartPercentageTextBox(txtRetention);
            UIHelper.AddField(formLayout, "درصد ارزش افزوده (%):", txtVat, 5, 0, 1);
            UIHelper.AddField(formLayout, "درصد سپرده حسن انجام کار (%):", txtRetention, 5, 2, 3);

            // ─── ردیف ۶: درصد بیمه و پیش‌دریافت ───
            StandardFormHelper.AddRow(formLayout);
            txtInsurance = new TextBox(); SmartInputHelper.EnableSmartPercentageTextBox(txtInsurance);
            txtAdvancePercent = new TextBox(); SmartInputHelper.EnableSmartPercentageTextBox(txtAdvancePercent);
            UIHelper.AddField(formLayout, "درصد سپرده بیمه (%):", txtInsurance, 6, 0, 1);
            UIHelper.AddField(formLayout, "درصد پیش‌دریافت (%):", txtAdvancePercent, 6, 2, 3);

            // ─── ردیف ۷: مبلغ کل پیش‌دریافت ───
            StandardFormHelper.AddRow(formLayout, 50F);
            txtTotalAdvance = new TextBox { ReadOnly = false, BackColor = Color.White, TextAlign = HorizontalAlignment.Center };
            SmartInputHelper.EnableSmartAmountTextBox(txtTotalAdvance);
            UIHelper.AddField(formLayout, "مبلغ کل پیش‌دریافت:", txtTotalAdvance, 7, 0, 1, 3);

            // ✅ اتصال رویدادها
            txtAmount.TextChanged += CalculateTotalAdvance;
            txtAdvancePercent.TextChanged += CalculateTotalAdvance;
            txtTotalAdvance.TextChanged += CalculateAdvancePercent;

            // ✅ دکمه ذخیره
            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.ActionGreen, 200, 45);
            btnSave.Font = UIHelper.PersianFont(12f, FontStyle.Bold);
            btnSave.Click += async (s, e) => await SaveSafeAsync();
            btnPanel.Controls.Add(btnSave);
        }

        // ✅ FIX: همیشه TotalAdvance را بازنشانی می‌کند — دیگر مقدار قبلی نمی‌ماند
        private void CalculateTotalAdvance(object? sender, EventArgs e)
        {
            if (_isCalculatingAdvance) return;

            _isCalculatingAdvance = true;
            try
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

                txtVat.Text = ToDisplayPercent(_contract.VatPercentage).ToString("0.##");
                txtRetention.Text = ToDisplayPercent(_contract.RetentionPercentage).ToString("0.##");
                txtInsurance.Text = ToDisplayPercent(_contract.InsurancePercentage).ToString("0.##");
                txtAdvancePercent.Text = ToDisplayPercent(_contract.AdvancePaymentPercentage).ToString("0.##");

                txtTotalAdvance.Text = NumberHelper.FormatThousands(_contract.TotalAdvanceAmount, usePersianDigits: true);
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
                    TotalAdvanceAmount = NumberHelper.ParseFormatted(txtTotalAdvance.Text)
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
                try { _actionLock.Release(); } catch { }
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
            return v > 0 && v <= 1m ? v * 100m : v;
        }

        private static decimal ToDisplayPercent(decimal rate) => rate > 0 && rate <= 1m ? rate * 100m : rate;
    }
}