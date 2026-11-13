using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using SMP.Models;
using SMP.Helpers;
using SMP.Data;

namespace SMP.Forms
{
    public class ClientStatementEditForm : Form
    {
        private readonly string _connectionString;
        private readonly ClientContract _contract;
        private readonly ClientStatement? _existingStatement;
        private readonly bool _isEdit;

        private TextBox txtStatementNumber = null!, txtStatementDate = null!, txtGrossAmount = null!;
        private TextBox txtVat = null!, txtInsurance = null!, txtRetention = null!, txtAdvanceRecovery = null!, txtOther = null!, txtNet = null!;
        private Button btnSave = null!, btnCalcFromExpenses = null!;

        private List<int> _selectedTransactionIds = new();
        private List<int> _selectedProjectExpenseIds = new();
        private List<int> _selectedCheckIds = new();

        // ✅ FIX بحرانی: فقط اگر کاربر از «محاسبه از هزینه‌ها» استفاده کرد، لینک‌ها بازنویسی می‌شوند
        private bool _expensesModified = false;

        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public ClientStatementEditForm(string connectionString, ClientContract contract, ClientStatement? statement = null)
        {
            _connectionString = connectionString;
            _contract = contract;
            _existingStatement = statement;
            _isEdit = statement != null;

            BuildUI();
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            // ✅ بارگذاری لینک‌های موجود قبل از نمایش
            Shown += async (s, e) =>
            {
                if (_isEdit && _existingStatement != null)
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
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            // ✅ ۱. استفاده از الگوی استاندارد
            var (formLayout, btnPanel) = StandardFormHelper.SetupStandardLayout(
                this, _isEdit ? "ویرایش صورت وضعیت کارفرما" : $"ثبت صورت وضعیت - قرارداد {_contract.ContractNumber}", new Size(900, 750));

            // ─── ردیف ۰: شماره و تاریخ ───
            StandardFormHelper.AddRow(formLayout);
            txtStatementNumber = new TextBox();
            txtStatementDate = new TextBox(); DateHelper.ApplyDateMask(txtStatementDate);

            UIHelper.AddField(formLayout, "شماره صورت وضعیت:", txtStatementNumber, 0, 0, 1);
            UIHelper.AddField(formLayout, "تاریخ صورت وضعیت:", txtStatementDate, 0, 2, 3);

            // ─── ردیف ۱: مبلغ کارکرد + دکمه محاسبه ───
            StandardFormHelper.AddRow(formLayout, 50F);

            var pnlGrossAmount = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), RightToLeft = RightToLeft.Yes };
            pnlGrossAmount.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // TextBox
            pnlGrossAmount.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F)); // Button

            txtGrossAmount = new TextBox { Font = UIHelper.PersianFont(12f, FontStyle.Bold), TextAlign = HorizontalAlignment.Center, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 5, 0) };
            SmartInputHelper.EnableSmartAmountTextBox(txtGrossAmount);

            btnCalcFromExpenses = UIHelper.CreateButton("🧮 محاسبه از هزینه‌ها", UIHelper.SectionBlue, 150, 35);
            btnCalcFromExpenses.Dock = DockStyle.Fill;
            btnCalcFromExpenses.Margin = new Padding(0);
            btnCalcFromExpenses.Click += BtnCalcFromExpenses_Click;

            pnlGrossAmount.Controls.Add(txtGrossAmount, 0, 0);
            pnlGrossAmount.Controls.Add(btnCalcFromExpenses, 1, 0);

            UIHelper.AddField(formLayout, "مبلغ کارکرد (ناخالص):", pnlGrossAmount, 1, 0, 1, 3);

            // ─── ردیف ۲: جداکننده کسورات ───
            StandardFormHelper.AddRow(formLayout, 40F);
            var lblSeparator = new Label
            {
                Text = "--- کسورات محاسبه‌شده از قرارداد ---",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray,
                Font = UIHelper.PersianFont(10f, FontStyle.Bold)
            };
            formLayout.Controls.Add(lblSeparator, 0, 2);
            formLayout.SetColumnSpan(lblSeparator, 4);

            // ─── محاسبه درصدهای اولیه از قرارداد ───
            decimal vatRate = _contract.VatPercentage > 1 ? _contract.VatPercentage : _contract.VatPercentage * 100;
            decimal insRate = _contract.InsurancePercentage > 1 ? _contract.InsurancePercentage : _contract.InsurancePercentage * 100;
            decimal retRate = _contract.RetentionPercentage > 1 ? _contract.RetentionPercentage : _contract.RetentionPercentage * 100;
            decimal advRate = _contract.AdvancePaymentPercentage > 1 ? _contract.AdvancePaymentPercentage : _contract.AdvancePaymentPercentage * 100;

            // ─── ردیف ۳: ارزش افزوده و بیمه ───
            StandardFormHelper.AddRow(formLayout);
            txtVat = new TextBox { ReadOnly = true, BackColor = Color.FromArgb(245, 245, 245), TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(11f) };
            txtInsurance = new TextBox { ReadOnly = true, BackColor = Color.FromArgb(245, 245, 245), TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(11f) };

            UIHelper.AddField(formLayout, $"ارزش افزوده ({StringHelper.ConvertToPersianDigits(vatRate.ToString("0.##"))}%):", txtVat, 3, 0, 1);
            UIHelper.AddField(formLayout, $"سپرده بیمه ({StringHelper.ConvertToPersianDigits(insRate.ToString("0.##"))}%):", txtInsurance, 3, 2, 3);

            // ─── ردیف ۴: حسن انجام کار و پیش‌دریافت ───
            StandardFormHelper.AddRow(formLayout);
            txtRetention = new TextBox { ReadOnly = true, BackColor = Color.FromArgb(245, 245, 245), TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(11f) };
            txtAdvanceRecovery = new TextBox { TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmWarningBackground };
            SmartInputHelper.EnableSmartAmountTextBox(txtAdvanceRecovery);

            UIHelper.AddField(formLayout, $"حسن انجام کار ({StringHelper.ConvertToPersianDigits(retRate.ToString("0.##"))}%):", txtRetention, 4, 0, 1);
            UIHelper.AddField(formLayout, $"مستهلک پیش‌دریافت ({StringHelper.ConvertToPersianDigits(advRate.ToString("0.##"))}%):", txtAdvanceRecovery, 4, 2, 3);

            // ─── ردیف ۵: سایر کسورات ───
            StandardFormHelper.AddRow(formLayout);
            txtOther = new TextBox { TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(11f) };
            SmartInputHelper.EnableSmartAmountTextBox(txtOther);
            UIHelper.AddField(formLayout, "سایر کسورات (دستی):", txtOther, 5, 0, 1);

            // ─── ردیف ۶: مبلغ خالص (برجسته) ───
            StandardFormHelper.AddRow(formLayout, 60F);
            txtNet = new TextBox
            {
                ReadOnly = true,
                BackColor = Color.FromArgb(220, 252, 231), // سبز ملایم
                Font = UIHelper.PersianFont(14f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                ForeColor = UIHelper.ActionGreen
            };
            UIHelper.AddField(formLayout, "مبلغ خالص دریافتی:", txtNet, 6, 0, 1, 3);

            // ─── اتصال رویدادها ───
            txtGrossAmount.TextChanged += (s, e) => CalculateAmounts();
            txtAdvanceRecovery.TextChanged += (s, e) => CalculateAmounts();
            txtOther.TextChanged += (s, e) => CalculateAmounts();

            if (!_isEdit)
            {
                txtAdvanceRecovery.Text = "0";
                txtOther.Text = "0";
            }

            // ✅ ۲. دکمه ذخیره
            btnSave = UIHelper.CreateButton("💾 ذخیره صورت وضعیت", UIHelper.ActionGreen, 220, 45);
            btnSave.Font = UIHelper.PersianFont(12f, FontStyle.Bold);
            btnSave.Click += async (s, e) => await SaveSafeAsync();
            btnPanel.Controls.Add(btnSave);
        }

        private void BtnCalcFromExpenses_Click(object? sender, EventArgs e)
        {
            using var form = new ExpensePickerForm(_connectionString, _contract.Id, _existingStatement?.Id);

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                txtGrossAmount.Text = form.SelectedGrossAmount.ToString("N0");
                _selectedTransactionIds = form.SelectedTransactionIds;
                _selectedProjectExpenseIds = form.SelectedProjectExpenseIds;
                _selectedCheckIds = form.SelectedCheckIds;

                // ✅ علامت‌گذاری به‌عنوان تغییر یافته — تنها راهی که در Save بازنویسی لینک‌ها انجام می‌شود
                _expensesModified = true;
            }
        }

        private void CalculateAmounts()
        {
            decimal gross = NumberHelper.ParseFormatted(txtGrossAmount.Text);

            if (gross > 0)
            {
                decimal vatRate = _contract.VatPercentage > 1 ? _contract.VatPercentage / 100 : _contract.VatPercentage;
                decimal insRate = _contract.InsurancePercentage > 1 ? _contract.InsurancePercentage / 100 : _contract.InsurancePercentage;
                decimal retRate = _contract.RetentionPercentage > 1 ? _contract.RetentionPercentage / 100 : _contract.RetentionPercentage;
                decimal advRate = _contract.AdvancePaymentPercentage > 1 ? _contract.AdvancePaymentPercentage / 100 : _contract.AdvancePaymentPercentage;

                decimal vat = Math.Round(gross * vatRate, 0);
                decimal insurance = Math.Round(gross * insRate, 0);
                decimal retention = Math.Round(gross * retRate, 0);

                decimal autoAdvance = Math.Round(gross * advRate, 0);

                if (txtAdvanceRecovery.Text == "0" || string.IsNullOrEmpty(txtAdvanceRecovery.Text))
                {
                    txtAdvanceRecovery.Text = NumberHelper.FormatThousands(autoAdvance, usePersianDigits: true);
                }

                decimal advance = NumberHelper.ParseFormatted(txtAdvanceRecovery.Text);
                decimal other = NumberHelper.ParseFormatted(txtOther.Text);

                txtVat.Text = NumberHelper.FormatThousands(vat, usePersianDigits: true);
                txtInsurance.Text = NumberHelper.FormatThousands(insurance, usePersianDigits: true);
                txtRetention.Text = NumberHelper.FormatThousands(retention, usePersianDigits: true);

                decimal net = (gross + vat) - (insurance + retention + advance + other);

                txtNet.Text = NumberHelper.FormatThousands(net, usePersianDigits: true);
                txtNet.ForeColor = net >= 0 ? UIHelper.ActionGreen : UIHelper.WarmDanger;
            }
            else
            {
                txtVat.Text = "0";
                txtInsurance.Text = "0";
                txtRetention.Text = "0";
                txtNet.Text = "0";
            }
        }

        private async Task LoadDataAsync()
        {
            if (_existingStatement == null) return;

            try
            {
                txtStatementNumber.Text = _existingStatement.StatementNumber;
                txtStatementDate.Text = DateHelper.ToPersianDate(_existingStatement.StatementDate);
                txtGrossAmount.Text = NumberHelper.FormatThousands(_existingStatement.GrossAmount, usePersianDigits: true);

                txtVat.Text = NumberHelper.FormatThousands(_existingStatement.VAT, usePersianDigits: true);
                txtInsurance.Text = NumberHelper.FormatThousands(_existingStatement.InsuranceDeduction, usePersianDigits: true);
                txtRetention.Text = NumberHelper.FormatThousands(_existingStatement.RetentionDeduction, usePersianDigits: true);

                if (_existingStatement.AdvanceRecovery == 0)
                {
                    decimal gross = _existingStatement.GrossAmount;
                    decimal advRate = _contract.AdvancePaymentPercentage > 1 ? _contract.AdvancePaymentPercentage / 100 : _contract.AdvancePaymentPercentage;
                    decimal autoAdvance = Math.Round(gross * advRate, 0);
                    txtAdvanceRecovery.Text = NumberHelper.FormatThousands(autoAdvance, usePersianDigits: true);
                }
                else
                {
                    txtAdvanceRecovery.Text = NumberHelper.FormatThousands(_existingStatement.AdvanceRecovery, usePersianDigits: true);
                }

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
                decimal vat = NumberHelper.ParseFormatted(txtVat.Text);
                decimal insurance = NumberHelper.ParseFormatted(txtInsurance.Text);
                decimal retention = NumberHelper.ParseFormatted(txtRetention.Text);
                decimal advance = NumberHelper.ParseFormatted(txtAdvanceRecovery.Text);
                decimal other = NumberHelper.ParseFormatted(txtOther.Text);

                decimal totalDeductions = insurance + retention + advance + other;
                decimal net = (gross + vat) - totalDeductions;

                if (gross <= 0)
                {
                    ModernMessageBox.ShowWarning("مبلغ کارکرد باید بزرگتر از صفر باشد.");
                    txtGrossAmount.Focus();
                    return;
                }

                var statement = new ClientStatement
                {
                    Id = _isEdit && _existingStatement != null ? _existingStatement.Id : 0,
                    ContractId = _contract.Id,
                    StatementNumber = txtStatementNumber.Text.Trim(),
                    StatementDate = DateHelper.ParsePersianDate(txtStatementDate.Text) ?? DateTime.Now,
                    GrossAmount = gross,
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
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}