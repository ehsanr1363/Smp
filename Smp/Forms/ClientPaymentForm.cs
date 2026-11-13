using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using ClosedXML.Excel;
using SMP.Models;
using SMP.Helpers;
using SMP.Data;

namespace SMP.Forms
{
    public class ClientPaymentForm : Form
    {
        private readonly string _connectionString;
        private ClientContract? _contract;

        private readonly CashTransactionRepository _cashRepo;
        private readonly CashCheckRepository _checkRepo;

        private TableLayoutPanel formLayout = null!;
        private TextBox txtAmount = null!, txtDescription = null!, txtReceiptDate = null!;
        private ComboBox cmbReceiptMethod = null!, cmbAccount = null!, cmbContract = null!;
        private TextBox txtCheckNumber = null!, txtDueDate = null!;
        private Label lblCheckNumber = null!, lblDueDate = null!;

        private Button btnSave = null!, btnDownloadTemplate = null!, btnImportExcel = null!;

        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public ClientPaymentForm(string connectionString, ClientContract contract) : this(connectionString)
        {
            _contract = contract;
        }

        public ClientPaymentForm(string connectionString)
        {
            _connectionString = connectionString;
            _cashRepo = new CashTransactionRepository(connectionString);
            _checkRepo = new CashCheckRepository(connectionString);

            BuildUI();
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            Shown += async (s, e) =>
            {
                await LoadContractsAsync();
                await LoadAccountsAsync();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            string title = _contract != null ? $"ثبت دریافت وجه - قرارداد {_contract.ContractNumber}" : "ثبت دریافت وجه از کارفرما";

            UIHelper.SetupForm(this, title, new Size(600, 680));
            ShowInTaskbar = false;

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F));

            var inputPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };

            formLayout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            formLayout.RowCount = 8;
            for (int i = 0; i < 8; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            // --- انتخاب قرارداد ---
            cmbContract = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id" };
            if (_contract == null)
            {
                UIHelper.AddField(formLayout, "انتخاب قرارداد کارفرما:", cmbContract, 0);
            }
            else
            {
                cmbContract.Items.Add(new { Id = _contract.Id, DisplayTitle = $"{_contract.ContractNumber} - {_contract.Subject}" });
                cmbContract.SelectedIndex = 0;
                cmbContract.Enabled = false;
                UIHelper.AddField(formLayout, "قرارداد کارفرما:", cmbContract, 0);
            }

            // --- فیلدهای اصلی ---
            txtReceiptDate = new TextBox();
            DateHelper.ApplyDateMask(txtReceiptDate);
            txtReceiptDate.Text = DateHelper.ToPersianDate(DateTime.Now);

            txtAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount);

            cmbReceiptMethod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbReceiptMethod.Items.AddRange(new[] { "نقدی", "حواله بانکی", "چک", "سایر" });
            cmbReceiptMethod.SelectedIndex = 0;
            cmbReceiptMethod.SelectedIndexChanged += (s, e) => ToggleCheckFields();

            cmbAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id" };

            UIHelper.AddField(formLayout, "تاریخ دریافت:", txtReceiptDate, 1);
            UIHelper.AddField(formLayout, "مبلغ دریافتی (ریال):", txtAmount, 2);
            UIHelper.AddField(formLayout, "حساب بانکی / صندوق:", cmbAccount, 3);
            UIHelper.AddField(formLayout, "روش دریافت:", cmbReceiptMethod, 4);

            // --- فیلدهای چک (ردیف 5 و 6) ---
            lblCheckNumber = new Label { Text = "شماره چک:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f) };
            txtCheckNumber = new TextBox();
            formLayout.Controls.Add(lblCheckNumber, 0, 5);
            formLayout.Controls.Add(txtCheckNumber, 1, 5);

            lblDueDate = new Label { Text = "تاریخ سررسید چک:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f) };
            txtDueDate = new TextBox();
            DateHelper.ApplyDateMask(txtDueDate);
            formLayout.Controls.Add(lblDueDate, 0, 6);
            formLayout.Controls.Add(txtDueDate, 1, 6);

            // --- توضیحات (ردیف 7) ---
            txtDescription = new TextBox { Multiline = true, Height = 60 };
            UIHelper.AddField(formLayout, "توضیحات (اختیاری):", txtDescription, 7);

            inputPanel.Controls.Add(formLayout);

            // ==========================================
            // ۲. پنل دکمه‌ها
            // ==========================================
            var btnPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            btnPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            btnPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            btnPanel.Padding = new Padding(10, 0, 10, 10);

            btnSave = UIHelper.CreateButton("💾 ثبت دریافتی", UIHelper.ActionGreen, 250, 45);
            btnSave.Anchor = AnchorStyles.None;
            btnSave.Click += async (s, e) => await SaveSafeAsync();

            var excelPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            excelPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            excelPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            btnDownloadTemplate = UIHelper.CreateButton("📥 دانلود قالب اکسل", UIHelper.WarmSecondary, 180, 40);
            btnDownloadTemplate.Anchor = AnchorStyles.None;
            btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

            btnImportExcel = UIHelper.CreateButton("📤 ایمپورت از اکسل", UIHelper.SectionTeal, 180, 40);
            btnImportExcel.Anchor = AnchorStyles.None;
            btnImportExcel.Click += async (s, e) => await BtnImportExcel_Click(s, e);

            excelPanel.Controls.Add(btnDownloadTemplate, 0, 0);
            excelPanel.Controls.Add(btnImportExcel, 1, 0);

            btnPanel.Controls.Add(btnSave, 0, 0);
            btnPanel.Controls.Add(excelPanel, 0, 1);

            mainLayout.Controls.Add(inputPanel, 0, 0);
            mainLayout.Controls.Add(btnPanel, 0, 1);
            Controls.Add(mainLayout);

            ToggleCheckFields();
        }

        private async Task LoadContractsAsync()
        {
            if (_contract != null) return;
            try
            {
                var repo = new ClientContractRepository(_connectionString);
                var list = await Task.Run(() => repo.GetAll());
                cmbContract.DataSource = list.Select(c => new { c.Id, DisplayTitle = $"{c.ContractNumber} - {c.Subject}" }).ToList();
                if (cmbContract.Items.Count > 0) cmbContract.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClientPaymentForm.LoadContracts");
            }
        }

        private async Task LoadAccountsAsync()
        {
            try
            {
                var bankRepo = new BankAccountRepository(_connectionString);
                var accounts = await bankRepo.GetActiveAsync();
                cmbAccount.DataSource = accounts;
                if (cmbAccount.Items.Count > 0) cmbAccount.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClientPaymentForm.LoadAccounts");
            }
        }

        private void ToggleCheckFields()
        {
            bool isCheck = cmbReceiptMethod.Text == "چک";

            lblCheckNumber.Visible = isCheck;
            txtCheckNumber.Visible = isCheck;
            lblDueDate.Visible = isCheck;
            txtDueDate.Visible = isCheck;

            float height = isCheck ? 50F : 0F;
            formLayout.RowStyles[5].Height = height;
            formLayout.RowStyles[6].Height = height;
        }

        // ✅ اصلاح شده: استفاده از ریپازیتوری‌ها به جای SQL مستقیم
        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (cmbContract.SelectedItem == null)
                {
                    ModernMessageBox.ShowWarning("لطفاً ابتدا قرارداد کارفرما را انتخاب کنید.");
                    return;
                }

                if (cmbAccount.SelectedItem is not CompanyBankAccount selectedAcc)
                {
                    ModernMessageBox.ShowWarning("لطفاً حساب بانکی / صندوق را انتخاب کنید.");
                    return;
                }

                decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
                if (amount <= 0) { ModernMessageBox.ShowWarning("مبلغ معتبر نیست."); return; }

                DateTime? pDate = DateHelper.ParsePersianDate(txtReceiptDate.Text);
                if (pDate == null) { ModernMessageBox.ShowWarning("تاریخ دریافت معتبر نیست."); return; }

                string method = cmbReceiptMethod.Text;
                DateTime? checkDate = null;

                if (method == "چک")
                {
                    if (string.IsNullOrWhiteSpace(txtCheckNumber.Text)) { ModernMessageBox.ShowWarning("شماره چک را وارد کنید."); return; }
                    checkDate = DateHelper.ParsePersianDate(txtDueDate.Text);
                    if (checkDate == null) { ModernMessageBox.ShowWarning("تاریخ سررسید چک معتبر نیست."); return; }
                }

                dynamic selectedContract = cmbContract.SelectedItem;
                int contractId = (int)selectedContract.Id;
                string contractTitle = _contract != null ? (_contract.Subject ?? "") : selectedContract.DisplayTitle.ToString();
                string desc = txtDescription.Text.Trim();

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    if (method == "چک" && checkDate.HasValue)
                    {
                        // ✅ استفاده از ریپازیتوری چک (خودش موجودی را مدیریت می‌کند)
                        var check = new CashCheck
                        {
                            CheckNumber = txtCheckNumber.Text.Trim(),
                            BankName = selectedAcc.BankName,
                            DueDate = checkDate.Value,
                            Amount = amount,
                            CheckType = DirectionTypes.Incoming,
                            Status = "در جریان",
                            AccountId = selectedAcc.Id,
                            Description = desc,
                            ContractId = contractId,
                            ContractTitle = contractTitle
                        };
                        await _checkRepo.AddAsync(check);
                    }
                    else
                    {
                        // ✅ استفاده از ریپازیتوری تراکنش نقدی (خودش موجودی را مدیریت می‌کند)
                        var trans = new CashTransaction
                        {
                            ContractId = contractId,
                            ContractTitle = contractTitle,
                            TransactionDate = pDate.Value,
                            Amount = amount,
                            TransactionType = method,
                            Direction = DirectionTypes.Incoming,
                            AccountId = selectedAcc.Id,
                            AccountTitle = selectedAcc.Title,
                            Category = "دریافت از کارفرما",
                            CostType = "خدمات",
                            Description = desc
                        };
                        await _cashRepo.AddAsync(trans);
                    }
                }, "در حال ثبت دریافتی...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("دریافتی با موفقیت ثبت شد.", isSuccess: true);
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClientPaymentForm.SaveSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"Template_Receipt.xlsx",
                Title = "ذخیره قالب اکسل دریافتی‌ها"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("دریافتی‌ها");
                ws.RightToLeft = true;

                string[] headers = {
                    "تاریخ دریافت (1403/01/01)", "مبلغ (ریال)", "روش دریافت (نقدی/حواله بانکی/چک)",
                    "عنوان حساب (دقیقا مطابق سیستم)", "شماره چک (اختیاری)", "تاریخ سررسید چک (اختیاری)", "توضیحات"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.WrapText = true;
                }

                ws.Cell(2, 1).Value = DateHelper.ToPersianDate(DateTime.Now);
                ws.Cell(2, 2).Value = 10000000;
                ws.Cell(2, 3).Value = "نقدی";
                ws.Cell(2, 4).Value = "جاری اصلی";
                ws.Cell(2, 7).Value = "بابت دریافت وجه";

                ws.Range("C2:C1000").CreateDataValidation().List("\"نقدی,حواله بانکی,چک,سایر\"");

                ws.Columns().AdjustToContents();
                ws.Row(1).Height = 40;
                workbook.SaveAs(sfd.FileName);

                ToastHelper.Show("قالب اکسل با موفقیت ذخیره شد.", true);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClientPaymentForm.DownloadTemplate");
                ModernMessageBox.ShowError("خطا در ساخت قالب:\n" + ex.Message);
            }
        }

        // ✅ اصلاح شده: ایمپورت اکسل با استفاده از ریپازیتوری‌ها
        private async Task BtnImportExcel_Click(object? sender, EventArgs e)
        {
            if (cmbContract.SelectedItem == null)
            {
                ModernMessageBox.ShowWarning("لطفاً ابتدا قرارداد کارفرما را انتخاب کنید.");
                return;
            }

            using var ofd = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls",
                Title = "انتخاب فایل اکسل دریافتی‌ها"
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                dynamic selectedContract = cmbContract.SelectedItem;
                int contractId = (int)selectedContract.Id;
                string contractTitle = _contract != null ? (_contract.Subject ?? "") : selectedContract.DisplayTitle.ToString();

                var bankRepo = new BankAccountRepository(_connectionString);
                var accounts = await bankRepo.GetActiveAsync();

                int successCount = 0;
                int errorCount = 0;
                List<string> errors = new();

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    using var workbook = new XLWorkbook(ofd.FileName);
                    var ws = workbook.Worksheet(1);
                    var rows = ws.RowsUsed().Skip(1);

                    int rowNum = 1;
                    foreach (var row in rows)
                    {
                        rowNum++;
                        try
                        {
                            string dateStr = row.Cell(1).GetString().Trim();
                            string amountStr = row.Cell(2).GetString().Trim();
                            string method = row.Cell(3).GetString().Trim();
                            string accTitle = row.Cell(4).GetString().Trim();
                            string checkNum = row.Cell(5).GetString().Trim();
                            string checkDateStr = row.Cell(6).GetString().Trim();
                            string desc = row.Cell(7).GetString().Trim();

                            if (string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(amountStr)) continue;

                            DateTime? date = DateHelper.ParsePersianDate(dateStr);
                            if (date == null) throw new Exception("تاریخ دریافت نامعتبر است.");

                            decimal amount = NumberHelper.ParseFormatted(amountStr);
                            if (amount <= 0) throw new Exception("مبلغ نامعتبر است.");

                            if (string.IsNullOrWhiteSpace(method)) throw new Exception("روش دریافت خالی است.");
                            if (method != "نقدی" && method != "حواله بانکی" && method != "چک" && method != "سایر")
                                throw new Exception("روش دریافت نامعتبر است.");

                            var account = accounts.FirstOrDefault(a => a.Title == accTitle);
                            if (account == null) throw new Exception($"حساب «{accTitle}» یافت نشد.");

                            DateTime? checkDate = null;
                            if (method == "چک")
                            {
                                if (string.IsNullOrWhiteSpace(checkNum)) throw new Exception("شماره چک خالی است.");
                                checkDate = DateHelper.ParsePersianDate(checkDateStr);
                                if (checkDate == null) throw new Exception("تاریخ سررسید چک نامعتبر است.");
                            }

                            // ✅ استفاده از ریپازیتوری‌ها (بدون SQL دستی)
                            if (method == "چک" && checkDate.HasValue)
                            {
                                var check = new CashCheck
                                {
                                    CheckNumber = checkNum,
                                    BankName = account.BankName,
                                    DueDate = checkDate.Value,
                                    Amount = amount,
                                    CheckType = DirectionTypes.Incoming,
                                    Status = "در جریان",
                                    AccountId = account.Id,
                                    Description = desc,
                                    ContractId = contractId,
                                    ContractTitle = contractTitle
                                };
                                await _checkRepo.AddAsync(check);
                            }
                            else
                            {
                                var trans = new CashTransaction
                                {
                                    ContractId = contractId,
                                    ContractTitle = contractTitle,
                                    TransactionDate = date.Value,
                                    Amount = amount,
                                    TransactionType = method,
                                    Direction = DirectionTypes.Incoming,
                                    AccountId = account.Id,
                                    AccountTitle = account.Title,
                                    Category = "دریافت از کارفرما",
                                    CostType = "خدمات",
                                    Description = desc
                                };
                                await _cashRepo.AddAsync(trans);
                            }

                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add($"ردیف {rowNum}: {ex.Message}");
                        }
                    }
                }, "در حال ایمپورت دریافتی‌ها از اکسل...");

                if (success && !IsDisposed)
                {
                    string msg = $"✅ تعداد {successCount} ردیف دریافتی با موفقیت ثبت شد.";
                    if (errorCount > 0)
                    {
                        msg += $"\n\n⚠️ {errorCount} ردیف ثبت نشد:\n" + string.Join("\n", errors.Take(5));
                        if (errors.Count > 5) msg += "\n... و موارد دیگر";
                        ModernMessageBox.ShowWarning(msg, "نتیجه ایمپورت");
                    }
                    else
                    {
                        ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClientPaymentForm.ImportExcel");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ایمپورت:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}