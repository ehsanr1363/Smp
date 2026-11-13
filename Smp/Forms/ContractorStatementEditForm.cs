using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Models;
using SMP.Helpers;
using SMP.Data;

namespace SMP.Forms
{
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

        private TextBox txtStatementNumber = null!, txtStatementDate = null!, txtGrossAmount = null!;
        private TextBox txtVat = null!, txtRetention = null!, txtInsurance = null!, txtAdvance = null!, txtNetPayable = null!;
        private Button btnSave = null!;

        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public ContractorStatementEditForm(string connectionString, ContractorContract contract, ContractorStatement? statement = null)
        {
            _connectionString = connectionString;
            _contract = contract;
            _existingStatement = statement;
            _isEdit = statement != null;

            BuildUI();
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            ShowInTaskbar = false;

            Shown += async (s, e) => await InitializeDataAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
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

            if (_isEdit && _existingStatement != null)
                LoadData();
        }

        private void BuildUI()
        {
            // ✅ استفاده از StandardFormHelper
            var (formLayout, btnPanel) = StandardFormHelper.SetupStandardLayout(
                this,
                _isEdit ? "ویرایش صورت وضعیت" : $"ثبت صورت وضعیت - قرارداد {_contract.ContractNumber}",
                new Size(750, 700));

            // ─── ساخت فیلدها ───
            txtStatementNumber = new TextBox();

            txtStatementDate = new TextBox();
            DateHelper.ApplyDateMask(txtStatementDate);

            txtGrossAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtGrossAmount);
            txtGrossAmount.TextChanged += (s, e) => CalculateAmounts();

            txtVat = new TextBox { ReadOnly = true, BackColor = Color.WhiteSmoke, TextAlign = HorizontalAlignment.Center };
            txtRetention = new TextBox { ReadOnly = true, BackColor = Color.WhiteSmoke, TextAlign = HorizontalAlignment.Center };
            txtInsurance = new TextBox { ReadOnly = true, BackColor = Color.WhiteSmoke, TextAlign = HorizontalAlignment.Center };
            txtAdvance = new TextBox { ReadOnly = true, BackColor = Color.WhiteSmoke, TextAlign = HorizontalAlignment.Center };

            txtNetPayable = new TextBox
            {
                ReadOnly = true,
                BackColor = Color.FromArgb(220, 252, 231),
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                ForeColor = UIHelper.ActionGreen
            };

            // ─── ردیف ۰: شماره + تاریخ ───
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "شماره صورت وضعیت:", txtStatementNumber, 0, 0, 1);
            UIHelper.AddField(formLayout, "تاریخ صورت وضعیت:", txtStatementDate, 0, 2, 3);

            // ─── ردیف ۱: مبلغ کارکرد (تمام عرض) ───
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "مبلغ کارکرد (ناخالص):", txtGrossAmount, 1, 0, 1, 3);

            // ─── ردیف ۲: جداکننده کسورات ───
            StandardFormHelper.AddRow(formLayout, 35F);
            var lblSeparator = new Label
            {
                Text = "— کسورات محاسبه‌شده —",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray,
                Font = UIHelper.PersianFont(10f, FontStyle.Bold)
            };
            formLayout.Controls.Add(lblSeparator, 0, 2);
            formLayout.SetColumnSpan(lblSeparator, 4);

            // ─── ردیف ۳: VAT + بیمه ───
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, $"ارزش افزوده ({_contract.VatPercentage}%):", txtVat, 3, 0, 1);
            UIHelper.AddField(formLayout, $"بیمه ({_contract.InsurancePercentage}%):", txtInsurance, 3, 2, 3);

            // ─── ردیف ۴: حسن انجام کار + پیش‌پرداخت ───
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, $"حسن انجام کار ({_contract.RetentionPercentage}%):", txtRetention, 4, 0, 1);
            UIHelper.AddField(formLayout, $"کسر پیش‌پرداخت ({_contract.AdvancePaymentPercentage}%):", txtAdvance, 4, 2, 3);

            // ─── ردیف ۵: خالص ───
            StandardFormHelper.AddRow(formLayout, 60F);
            UIHelper.AddField(formLayout, "مبلغ خالص قابل پرداخت:", txtNetPayable, 5, 0, 1, 3);

            // ─── دکمه ذخیره ───
            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.ActionGreen, 200, 45);
            btnSave.Font = UIHelper.PersianFont(12f, FontStyle.Bold);
            btnSave.Click += async (s, e) => await SaveSafeAsync();
            btnPanel.Controls.Add(btnSave);

            if (!_isEdit) txtStatementDate.Text = DateHelper.ToPersianDate(DateTime.Now);
        }

        private void CalculateAmounts()
        {
            decimal gross = NumberHelper.ParseFormatted(txtGrossAmount.Text);

            if (gross > 0)
            {
                decimal vatRate = NormalizeRate(_contract.VatPercentage);
                decimal retentionRate = NormalizeRate(_contract.RetentionPercentage);
                decimal insuranceRate = NormalizeRate(_contract.InsurancePercentage);
                decimal advanceRate = NormalizeRate(_contract.AdvancePaymentPercentage);

                _currentVat = Math.Round(gross * vatRate, 0);

                decimal calculatedRetention = Math.Round(gross * retentionRate, 0);
                decimal maxRetentionLimit = _contract.Amount * retentionRate;
                decimal remainingRetention = Math.Max(0, maxRetentionLimit - _previousRetention);
                _currentRetention = Math.Min(calculatedRetention, remainingRetention);

                _currentInsurance = Math.Round(gross * insuranceRate, 0);

                decimal calculatedAdvance = Math.Round(gross * advanceRate, 0);
                decimal remainingAdvance = Math.Max(0, _contract.TotalAdvanceAmount - _previousAdvance);
                _currentAdvance = Math.Min(calculatedAdvance, remainingAdvance);

                decimal totalGrossWithVat = gross + _currentVat;
                decimal totalDeductions = _currentRetention + _currentInsurance + _currentAdvance;
                _currentNet = totalGrossWithVat - totalDeductions;

                txtVat.Text = NumberHelper.FormatThousands(_currentVat, usePersianDigits: true);
                txtRetention.Text = NumberHelper.FormatThousands(_currentRetention, usePersianDigits: true);
                txtInsurance.Text = NumberHelper.FormatThousands(_currentInsurance, usePersianDigits: true);
                txtAdvance.Text = NumberHelper.FormatThousands(_currentAdvance, usePersianDigits: true);
                txtNetPayable.Text = NumberHelper.FormatThousands(_currentNet, usePersianDigits: true);

                txtRetention.BackColor = (calculatedRetention > remainingRetention) ? Color.FromArgb(255, 230, 230) : Color.WhiteSmoke;
                txtAdvance.BackColor = (calculatedAdvance > remainingAdvance) ? Color.FromArgb(255, 230, 230) : Color.WhiteSmoke;
            }
            else
            {
                _currentVat = _currentRetention = _currentInsurance = _currentAdvance = _currentNet = 0;
                txtVat.Text = "0";
                txtRetention.Text = "0";
                txtInsurance.Text = "0";
                txtAdvance.Text = "0";
                txtNetPayable.Text = "0";
                txtRetention.BackColor = Color.WhiteSmoke;
                txtAdvance.BackColor = Color.WhiteSmoke;
            }
        }

        private static decimal NormalizeRate(decimal rate)
        {
            return rate > 1m ? rate / 100m : rate;
        }

        private void LoadData()
        {
            if (_existingStatement == null) return;

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

                var statement = new ContractorStatement
                {
                    Id = _isEdit && _existingStatement != null ? _existingStatement.Id : 0,
                    ContractId = _contract.Id,
                    StatementNumber = txtStatementNumber.Text.Trim(),
                    StatementDate = DateHelper.ParsePersianDate(txtStatementDate.Text) ?? DateTime.Now,
                    GrossAmount = gross,
                    VAT = _currentVat,
                    RetentionDeduction = _currentRetention,
                    InsuranceDeduction = _currentInsurance,
                    OtherDeductions = _isEdit && _existingStatement != null ? _existingStatement.OtherDeductions : 0,
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
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}