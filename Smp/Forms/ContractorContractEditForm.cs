using System;
using System.Collections.Generic;
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
    public class ContractorContractEditForm : Form
    {
        private readonly string _connectionString;
        private readonly ContractorContract? _contract;
        private readonly bool _isEdit;

        private TextBox txtPartyName = null!;
        private int _selectedPartyId = 0;
        private ComboBox cmbClientContract = null!;

        private TextBox txtContractNumber = null!, txtContractRow = null!, txtSubject = null!, txtAmount = null!, txtContractDate = null!, txtEndDate = null!;
        private TextBox txtDurationMonths = null!;
        private TextBox txtGuarantee = null!, txtRetention = null!, txtInsurance = null!, txtAdvancePercent = null!, txtTotalAdvance = null!, txtVat = null!;
        private Button btnSave = null!;

        private Label lblSumTotalAmount = null!, lblSumStatements = null!, lblSumPaid = null!, lblSumGoods = null!, lblSumServices = null!, lblSumDebt = null!;

        private readonly SemaphoreSlim _actionLock = new(1, 1);

        // ✅ نگه‌داری پیام هشدار برای نمایش در Shown
        private string? _pendingWarning = null;

        public ContractorContractEditForm(string connectionString, ContractorContract? contract = null)
        {
            _connectionString = connectionString;
            _contract = contract;
            _isEdit = contract != null;

            BuildUI();
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            Shown += async (s, e) =>
            {
                try
                {
                    await LoadClientContractsAsync();
                    if (_isEdit && _contract != null)
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
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            // ✅ ۱. ساخت ساختار اصلی با هلپر
            var (formLayout, btnPanel) = StandardFormHelper.SetupStandardLayout(
                this, _isEdit ? "ویرایش قرارداد پیمانکار" : "افزودن قرارداد پیمانکار", new Size(950, 850));

            // ─── ردیف ۰: انتخاب پیمانکار ───
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
            UIHelper.AddField(formLayout, "پیمانکار:", pnlParty, 0, 0, 1, 3);

            // ─── ردیف ۱: انتخاب قرارداد کارفرما ───
            StandardFormHelper.AddRow(formLayout);
            cmbClientContract = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmWarningBackground };
            UIHelper.AddField(formLayout, "قرارداد کارفرما (اجباری):", cmbClientContract, 1, 0, 1, 3);

            // ─── ردیف ۲: شماره و ردیف قرارداد ───
            StandardFormHelper.AddRow(formLayout);
            txtContractNumber = new TextBox();
            txtContractRow = new TextBox();
            UIHelper.AddField(formLayout, "شماره قرارداد:", txtContractNumber, 2, 0, 1);
            UIHelper.AddField(formLayout, "ردیف پیمان:", txtContractRow, 2, 2, 3);

            // ─── ردیف ۳: موضوع ───
            StandardFormHelper.AddRow(formLayout);
            txtSubject = new TextBox();
            UIHelper.AddField(formLayout, "موضوع:", txtSubject, 3, 0, 1, 3);

            // ─── ردیف ۴: مبلغ و تاریخ قرارداد ───
            StandardFormHelper.AddRow(formLayout);
            txtAmount = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
            txtContractDate = new TextBox(); DateHelper.ApplyDateMask(txtContractDate);
            UIHelper.AddField(formLayout, "مبلغ (ریال):", txtAmount, 4, 0, 1);
            UIHelper.AddField(formLayout, "تاریخ قرارداد:", txtContractDate, 4, 2, 3);

            // ─── ردیف ۵: مدت و تاریخ پایان ───
            StandardFormHelper.AddRow(formLayout);
            txtDurationMonths = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Center };
            txtEndDate = new TextBox(); DateHelper.ApplyDateMask(txtEndDate);
            UIHelper.AddField(formLayout, "مدت (ماه):", txtDurationMonths, 5, 0, 1);
            UIHelper.AddField(formLayout, "تاریخ پایان:", txtEndDate, 5, 2, 3);

            // ─── ردیف ۶: درصد تضمین و حسن انجام کار ───
            StandardFormHelper.AddRow(formLayout);
            txtGuarantee = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Center };
            txtRetention = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Center };
            UIHelper.AddField(formLayout, "درصد تضمین (%):", txtGuarantee, 6, 0, 1);
            UIHelper.AddField(formLayout, "درصد حسن انجام کار (%):", txtRetention, 6, 2, 3);

            // ─── ردیف ۷: درصد بیمه و پیش‌پرداخت ───
            StandardFormHelper.AddRow(formLayout);
            txtInsurance = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Center };
            txtAdvancePercent = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Center };
            UIHelper.AddField(formLayout, "درصد بیمه (%):", txtInsurance, 7, 0, 1);
            UIHelper.AddField(formLayout, "درصد پیش‌پرداخت (%):", txtAdvancePercent, 7, 2, 3);

            // ─── ردیف ۸: مبلغ پیش‌پرداخت و ارزش افزوده ───
            StandardFormHelper.AddRow(formLayout, 50F);
            txtTotalAdvance = new TextBox { Text = "0", ReadOnly = true, TextAlign = HorizontalAlignment.Center, BackColor = UIHelper.WarmInputBackground };
            txtVat = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Center };
            UIHelper.AddField(formLayout, "مبلغ کل پیش‌پرداخت:", txtTotalAdvance, 8, 0, 1);
            UIHelper.AddField(formLayout, "درصد ارزش افزوده (%):", txtVat, 8, 2, 3);

            // ─── ردیف ۹: خلاصه وضعیت مالی (در حالت ویرایش) ───
            StandardFormHelper.AddRow(formLayout, _isEdit ? 160F : 0F);

            var pnlSummary = new GroupBox
            {
                Text = "📊 خلاصه وضعیت مالی پیمانکار",
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = UIHelper.SectionBlue,
                Padding = new Padding(15),
                Visible = _isEdit
            };

            var summaryLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6 };
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            lblSumTotalAmount = CreateSummaryLabel(summaryLayout, "مبلغ کل قرارداد:", 0);
            lblSumStatements = CreateSummaryLabel(summaryLayout, "جمع خالص صورت‌وضعیت‌ها:", 1);
            lblSumPaid = CreateSummaryLabel(summaryLayout, "جمع پرداخت‌های انجام شده:", 2);
            lblSumGoods = CreateSummaryLabel(summaryLayout, "↳ بابت خرید کالا:", 3);
            lblSumServices = CreateSummaryLabel(summaryLayout, "↳ بابت خرید خدمات:", 4);
            lblSumDebt = CreateSummaryLabel(summaryLayout, "مانده بدهی به پیمانکار:", 5);

            pnlSummary.Controls.Add(summaryLayout);
            formLayout.Controls.Add(pnlSummary, 0, 9);
            formLayout.SetColumnSpan(pnlSummary, 4);

            // ✅ اتصال رویدادها برای محاسبات خودکار
            txtAmount.TextChanged += CalculateTotalAdvance;
            txtAdvancePercent.TextChanged += CalculateTotalAdvance;
            txtContractDate.Leave += CalculateEndDate;
            txtDurationMonths.TextChanged += CalculateEndDate;

            // ✅ ۲. دکمه ذخیره
            btnSave = UIHelper.CreateButton("💾 ذخیره قرارداد", UIHelper.ActionGreen, 220, 45);
            btnSave.Font = UIHelper.PersianFont(12f, FontStyle.Bold);
            btnSave.Click += async (s, e) => await SaveSafeAsync();
            btnPanel.Controls.Add(btnSave);
        }

        private Label CreateSummaryLabel(TableLayoutPanel layout, string title, int row)
        {
            var lblTitle = new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(10f, FontStyle.Bold), ForeColor = UIHelper.WarmText };
            var lblValue = new Label { Text = "---", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UIHelper.PersianFont(10f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            // چیدمان در دو ستون: لیبل‌ها سمت راست، مقادیر سمت چپ
            layout.Controls.Add(lblTitle, 0, row);
            layout.Controls.Add(lblValue, 1, row);
            return lblValue;
        }

        private async Task LoadFinancialSummaryAsync()
        {
            if (_contract == null) return;
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

                if (con == null || IsDisposed) return;

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
                lblSumDebt.ForeColor = debt > 0 ? UIHelper.ActionRed : (debt < 0 ? UIHelper.ActionGreen : UIHelper.WarmText);
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

            if (!success || list == null || IsDisposed) return;

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
            if (_contract == null || IsDisposed) return;

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
                    if (idProp != null && (int)idProp.GetValue(item)! == _contract.ClientContractId.Value)
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

            txtGuarantee.Text = _contract.GuaranteePercentage.ToString();
            txtRetention.Text = _contract.RetentionPercentage.ToString();
            txtInsurance.Text = _contract.InsurancePercentage.ToString();
            txtAdvancePercent.Text = _contract.AdvancePaymentPercentage.ToString();
            txtTotalAdvance.Text = NumberHelper.FormatThousands(_contract.TotalAdvanceAmount, usePersianDigits: true);
            txtVat.Text = _contract.VatPercentage.ToString();
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

                if (cmbClientContract.SelectedItem == null)
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
                if (cmbClientContract.SelectedItem != null)
                {
                    var idProp = cmbClientContract.SelectedItem.GetType().GetProperty("Id");
                    if (idProp != null)
                    {
                        int id = (int)idProp.GetValue(cmbClientContract.SelectedItem)!;
                        if (id > 0) selectedClientContractId = id;
                    }
                }

                var c = new ContractorContract
                {
                    Id = _isEdit && _contract != null ? _contract.Id : 0,
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
                    VatPercentage = vatPercent
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
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}