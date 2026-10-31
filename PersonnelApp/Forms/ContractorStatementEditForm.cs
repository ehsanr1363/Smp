using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Models;
using PersonnelApp.Helpers;
using PersonnelApp.Data;

namespace PersonnelApp.Forms
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

        private async Task InitializeDataAsync()
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorStatementRepository(_connectionString);
                var totals = await Task.Run(() => repo.GetDeductionsTotal(_contract.Id, _isEdit ? _existingStatement?.Id : null));
                _previousRetention = totals.TotalRetention;
                _previousAdvance = totals.TotalAdvance;
            }, "در حال دریافت اطلاعات کسورات...");

            if (!success) return;

            if (_isEdit && _existingStatement != null)
                LoadData();
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, _isEdit ? "ویرایش صورت وضعیت" : $"ثبت صورت وضعیت - قرارداد {_contract.ContractNumber}", new Size(600, 580));

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9, AutoSize = true };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            for (int i = 0; i < 9; i++)
                formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            txtStatementNumber = new TextBox();

            txtStatementDate = new TextBox();
            DateHelper.ApplyDateMask(txtStatementDate);

            txtGrossAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtGrossAmount);

            txtVat = new TextBox { ReadOnly = true, BackColor = Color.WhiteSmoke, TextAlign = HorizontalAlignment.Center };
            txtRetention = new TextBox { ReadOnly = true, BackColor = Color.WhiteSmoke, TextAlign = HorizontalAlignment.Center };
            txtInsurance = new TextBox { ReadOnly = true, BackColor = Color.WhiteSmoke, TextAlign = HorizontalAlignment.Center };
            txtAdvance = new TextBox { ReadOnly = true, BackColor = Color.WhiteSmoke, TextAlign = HorizontalAlignment.Center };

            txtNetPayable = new TextBox
            {
                ReadOnly = true,
                BackColor = Color.FromArgb(220, 252, 231),
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center
            };

            UIHelper.AddField(formLayout, "شماره صورت وضعیت:", txtStatementNumber, 0);
            UIHelper.AddField(formLayout, "تاریخ صورت وضعیت:", txtStatementDate, 1);
            UIHelper.AddField(formLayout, "مبلغ کارکرد (ناخالص):", txtGrossAmount, 2);

            formLayout.Controls.Add(new Label { Text = "", Height = 10 }, 0, 3);

            UIHelper.AddField(formLayout, $"ارزش افزوده ({_contract.VatPercentage}%):", txtVat, 4);
            UIHelper.AddField(formLayout, $"حسن انجام کار ({_contract.RetentionPercentage}%):", txtRetention, 5);
            UIHelper.AddField(formLayout, $"بیمه ({_contract.InsurancePercentage}%):", txtInsurance, 6);
            UIHelper.AddField(formLayout, $"کسر پیش‌پرداخت ({_contract.AdvancePaymentPercentage}%):", txtAdvance, 7);

            formLayout.Controls.Add(new Label { Text = "", Height = 10 }, 0, 8);

            UIHelper.AddField(formLayout, "مبلغ خالص قابل پرداخت:", txtNetPayable, 8);

            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.WarmPrimary, 250, 50);
            btnSave.Font = UIHelper.PersianFont(14f, FontStyle.Bold);

            var btnPanel = new Panel { Dock = DockStyle.Fill };
            btnSave.Anchor = AnchorStyles.None;
            btnPanel.Controls.Add(btnSave);

            btnPanel.Resize += (s, e) => {
                btnSave.Left = (btnPanel.Width - btnSave.Width) / 2;
                btnSave.Top = (btnPanel.Height - btnSave.Height) / 2;
            };
            btnSave.Click += BtnSave_Click;

            mainLayout.Controls.Add(formLayout, 0, 0);
            mainLayout.Controls.Add(btnPanel, 0, 1);

            Controls.Add(mainLayout);

            if (!_isEdit) txtStatementDate.Text = DateHelper.ToPersianDate(DateTime.Now);

            txtGrossAmount.TextChanged += (s, e) => CalculateAmounts();
        }

        private void CalculateAmounts()
        {
            decimal gross = NumberHelper.ParseFormatted(txtGrossAmount.Text);

            if (gross > 0)
            {
                // 👈 اصلاح شد: نرمال‌سازی درصدها (اگر 5 بود یعنی 0.05 و اگر 0.05 بود همان 0.05)
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

        // 👈 متد کمکی برای اطمینان از اینکه درصدها همیشه به صورت اعشاری محاسبه می‌شوند
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

        private async void BtnSave_Click(object? sender, EventArgs e)
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

            if (success)
            {
                ToastHelper.Show("صورت وضعیت با موفقیت ذخیره شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}