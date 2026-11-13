using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using SMP.Models;
using SMP.Helpers;
using SMP.Data;

namespace SMP.Forms
{
    public class ProjectExpenseEditForm : Form
    {
        private readonly string _connectionString;
        private readonly ClientContract _clientContract;
        private readonly ProjectExpense? _existingExpense;
        private readonly bool _isEdit;
        private readonly LookupRepository _lookupRepo;

        private TextBox txtPartyName = null!;
        private TextBox txtInvoiceNumber = null!, txtInvoiceDate = null!, txtAmount = null!, txtDescription = null!;
        private ComboBox cmbCostType = null!;
        private Button btnSave = null!;
        private int? _selectedPartyId = null;

        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public ProjectExpenseEditForm(string connectionString, ClientContract clientContract, ProjectExpense? expense = null)
        {
            _connectionString = connectionString;
            _clientContract = clientContract;
            _existingExpense = expense;
            _isEdit = expense != null;
            _lookupRepo = new LookupRepository(connectionString);

            BuildUI();
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            Shown += async (s, e) =>
            {
                await LoadCostTypesAsync();

                if (_isEdit && _existingExpense != null)
                {
                    await LoadDataAsync();
                }
                else
                {
                    txtInvoiceDate.Text = DateHelper.ToPersianDate(DateTime.Now);
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
                this, _isEdit ? "ویرایش فاکتور هزینه" : $"ثبت فاکتور هزینه - قرارداد {_clientContract.ContractNumber}", new Size(900, 650));

            // ─── ردیف ۰: طرف حساب ───
            StandardFormHelper.AddRow(formLayout, 50F);

            var pnlParty = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), RightToLeft = RightToLeft.Yes };
            pnlParty.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // TextBox
            pnlParty.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F)); // Button

            txtPartyName = new TextBox
            {
                ReadOnly = true,
                BackColor = UIHelper.WarmInputBackground,
                TextAlign = HorizontalAlignment.Center,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 5, 0)
            };

            var btnSelectParty = UIHelper.CreateButton("🔍 انتخاب", UIHelper.WarmSecondary, 80, 35);
            btnSelectParty.Dock = DockStyle.Fill;
            btnSelectParty.Margin = new Padding(0);
            btnSelectParty.Click += BtnSelectParty_Click;

            pnlParty.Controls.Add(txtPartyName, 0, 0);
            pnlParty.Controls.Add(btnSelectParty, 1, 0);
            UIHelper.AddField(formLayout, "طرف حساب (تفصیلی):", pnlParty, 0, 0, 1, 3);

            // ─── ردیف ۱: شماره فاکتور و تاریخ ───
            StandardFormHelper.AddRow(formLayout);
            txtInvoiceNumber = new TextBox();
            txtInvoiceDate = new TextBox(); DateHelper.ApplyDateMask(txtInvoiceDate);

            UIHelper.AddField(formLayout, "شماره فاکتور:", txtInvoiceNumber, 1, 0, 1);
            UIHelper.AddField(formLayout, "تاریخ فاکتور:", txtInvoiceDate, 1, 2, 3);

            // ─── ردیف ۲: نوع هزینه و مبلغ ───
            StandardFormHelper.AddRow(formLayout);
            cmbCostType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmInputBackground };
            txtAmount = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtAmount);

            UIHelper.AddField(formLayout, "نوع هزینه (ماهیت):", cmbCostType, 2, 0, 1);
            UIHelper.AddField(formLayout, "مبلغ فاکتور (ریال):", txtAmount, 2, 2, 3);

            // ─── ردیف ۳: توضیحات ───
            StandardFormHelper.AddRow(formLayout, 80F);
            txtDescription = new TextBox { Multiline = true, Dock = DockStyle.Fill };
            UIHelper.AddField(formLayout, "توضیحات (بابت چه چیزی):", txtDescription, 3, 0, 1, 3);

            // ✅ ۲. دکمه ذخیره
            btnSave = UIHelper.CreateButton("💾 ذخیره فاکتور", UIHelper.ActionGreen, 200, 45);
            btnSave.Font = UIHelper.PersianFont(12f, FontStyle.Bold);
            btnSave.Click += async (s, e) => await SaveSafeAsync();
            btnPanel.Controls.Add(btnSave);
        }

        private async Task LoadCostTypesAsync()
        {
            try
            {
                var items = await _lookupRepo.GetByCategoryAsync("CostType");
                if (IsDisposed) return;

                var costTypes = items.Select(i => i.Value).ToList();
                if (costTypes.Count == 0) costTypes.AddRange(new[] { "خرید", "خدمات" });

                cmbCostType.DataSource = costTypes;

                if (_isEdit && _existingExpense != null && !string.IsNullOrEmpty(_existingExpense.CostType))
                {
                    if (cmbCostType.Items.Contains(_existingExpense.CostType))
                        cmbCostType.SelectedItem = _existingExpense.CostType;
                }
                else if (cmbCostType.Items.Count > 0)
                {
                    cmbCostType.SelectedIndex = 0;
                }
            }
            catch
            {
                if (IsDisposed) return;
                cmbCostType.Items.Clear();
                cmbCostType.Items.AddRange(new[] { "خرید", "خدمات" });
                cmbCostType.SelectedIndex = 0;
            }
        }

        private void BtnSelectParty_Click(object? sender, EventArgs e)
        {
            using var form = new TafsiliSelectionForm(_connectionString);
            if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed)
            {
                _selectedPartyId = form.SelectedId;
                txtPartyName.Text = form.SelectedName;
            }
        }

        private async Task LoadDataAsync()
        {
            if (_existingExpense == null) return;

            _selectedPartyId = _existingExpense.PartyId;
            txtPartyName.Text = _existingExpense.PartyName ?? "";
            txtInvoiceNumber.Text = _existingExpense.InvoiceNumber;
            txtInvoiceDate.Text = DateHelper.ToPersianDate(_existingExpense.InvoiceDate);
            txtAmount.Text = NumberHelper.FormatThousands(_existingExpense.Amount, usePersianDigits: true);
            txtDescription.Text = _existingExpense.Description;

            // بررسی وجود طرف قرارداد — اگر حذف شده، به کاربر اطلاع بده
            if (_selectedPartyId.HasValue)
            {
                try
                {
                    int exists = await Task.Run(() => Convert.ToInt32(
                        DbHelper.ExecuteScalar(_connectionString,
                            "SELECT COUNT(1) FROM Parties WHERE Id = @id",
                            new SqlParameter("@id", _selectedPartyId.Value))));

                    if (exists == 0)
                    {
                        _selectedPartyId = null;
                        txtPartyName.Text = "";
                        ModernMessageBox.ShowWarning(
                            "طرف قرارداد این فاکتور از سیستم حذف شده است.\n\n" +
                            "فیلد طرف قرارداد خالی شده. اگر می‌خواهید فاکتور را حفظ کنید:\n" +
                            "  • یک طرف قرارداد جدید انتخاب کنید، یا\n" +
                            "  • فیلد را خالی بگذارید و ذخیره کنید (فاکتور بدون طرف قرارداد ذخیره می‌شود).");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "ProjectExpenseEditForm.LoadDataAsync - PartyCheck");
                }
            }
        }

        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
                if (amount <= 0)
                {
                    ModernMessageBox.ShowWarning("لطفاً مبلغ فاکتور را به درستی وارد کنید.");
                    txtAmount.Focus();
                    return;
                }

                if (!DateHelper.IsValidPersianDate(txtInvoiceDate.Text))
                {
                    ModernMessageBox.ShowWarning("تاریخ فاکتور معتبر نیست.");
                    txtInvoiceDate.Focus();
                    return;
                }

                // قبل از ذخیره، وجود طرف قرارداد بررسی می‌شود
                if (_selectedPartyId.HasValue)
                {
                    int exists;
                    try
                    {
                        exists = await Task.Run(() => Convert.ToInt32(
                            DbHelper.ExecuteScalar(_connectionString,
                                "SELECT COUNT(1) FROM Parties WHERE Id = @id",
                                new SqlParameter("@id", _selectedPartyId.Value))));
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogException(ex, "ProjectExpenseEditForm.SaveSafeAsync - PartyCheck");
                        ModernMessageBox.ShowError("خطا در بررسی طرف قرارداد:\n" + ex.Message);
                        return;
                    }

                    if (exists == 0)
                    {
                        ModernMessageBox.ShowWarning(
                            "طرف قرارداد انتخاب‌شده از سیستم حذف شده است.\n\n" +
                            "لطفاً از طریق دکمه «🔍 انتخاب» یک طرف قرارداد جدید انتخاب کنید،\n" +
                            "یا فیلد را خالی بگذارید تا فاکتور بدون طرف قرارداد ذخیره شود.");
                        return;
                    }
                }

                DateTime? invDate = DateHelper.ParsePersianDate(txtInvoiceDate.Text);

                var expense = new ProjectExpense
                {
                    Id = _isEdit && _existingExpense != null ? _existingExpense.Id : 0,
                    ClientContractId = _clientContract.Id,
                    PartyId = _selectedPartyId,
                    InvoiceNumber = txtInvoiceNumber.Text.Trim(),
                    InvoiceDate = invDate.Value,
                    Amount = amount,
                    CostType = cmbCostType.Text,
                    Description = txtDescription.Text.Trim()
                };

                if (_isEdit) DateStampHelper.StampUpdated(expense);
                else DateStampHelper.StampCreated(expense);

                string? fkErrorMessage = null;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    try
                    {
                        var repo = new ProjectExpenseRepository(_connectionString);
                        if (_isEdit)
                            await Task.Run(() => repo.Update(expense));
                        else
                            await Task.Run(() => repo.Add(expense));
                    }
                    catch (SqlException ex) when (ex.Number == 547)
                    {
                        fkErrorMessage =
                            "این فاکتور به رکوردی اشاره می‌کند که در سیستم وجود ندارد (احتمالاً طرف قرارداد یا قرارداد پروژه حذف شده است).\n\n" +
                            "لطفاً فیلدهای مرتبط را بازبینی کرده و دوباره تلاش کنید.";
                        throw new InvalidOperationException(fkErrorMessage, ex);
                    }
                }, "در حال ذخیره فاکتور...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("فاکتور با موفقیت ذخیره شد.", isSuccess: true);
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ProjectExpenseEditForm.SaveSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}