using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class BankFeeForm : Form
    {
        private readonly string _connectionString;
        private readonly int? _transactionId;
        private readonly CashTransactionRepository _cashRepo;

        private ComboBox cmbAccount = null!;
        private TextBox txtAmount = null!, txtDate = null!, txtDescription = null!;
        private Button btnSave = null!, btnDownloadTemplate = null!, btnImportExcel = null!;

        private DataGridView dgvFees = null!;
        private Label lblGridTitle = null!;

        private readonly SemaphoreSlim _actionLock = new(1, 1);

        // ✅ FIX: نگه‌داری شناسه و عنوان اصلی حساب
        private int? _originalAccountId = null;
        private string? _originalAccountTitle = null;

        // ✅ FIX: ردیابی تغییر حساب توسط کاربر
        private bool _userChangedAccount = false;
        private bool _suppressAccountChange = true;

        public BankFeeForm(string connectionString, int? transactionId = null)
        {
            _connectionString = connectionString;
            _transactionId = transactionId;
            _cashRepo = new CashTransactionRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader(_transactionId.HasValue ? "ویرایش کارمزد بانکی" : "ثبت کارمزد بانکی", new Size(700, 850));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            // ✅ FIX: ثبت‌نام تغییر حساب کاربر
            cmbAccount.SelectedIndexChanged += (s, e) =>
            {
                if (!_suppressAccountChange) _userChangedAccount = true;
            };

            Shown += async (s, e) =>
            {
                _suppressAccountChange = true;
                try
                {
                    await LoadAccountsAsync();
                    await LoadGridDataAsync();
                    if (_transactionId.HasValue) await LoadTransactionDataAsync(_transactionId.Value);
                }
                finally
                {
                    _suppressAccountChange = false;
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
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(20, 10, 20, 10) };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 4; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            cmbAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id", Dock = DockStyle.Fill };
            txtAmount = new TextBox { Dock = DockStyle.Fill }; SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
            txtDate = new TextBox { Dock = DockStyle.Fill }; DateHelper.ApplyDateMask(txtDate); txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);
            txtDescription = new TextBox { PlaceholderText = "مثلاً: کارمزد انتقال وجه، کارمزد صدور چک و...", Dock = DockStyle.Fill };

            UIHelper.AddField(formLayout, "حساب بانکی:", cmbAccount, 0);
            UIHelper.AddField(formLayout, "مبلغ کارمزد (ریال):", txtAmount, 1);
            UIHelper.AddField(formLayout, "تاریخ:", txtDate, 2);
            UIHelper.AddField(formLayout, "توضیحات (بابت):", txtDescription, 3);

            mainLayout.Controls.Add(formLayout, 0, 0);

            var pnlExcel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(20, 10, 20, 0) };
            btnDownloadTemplate = UIHelper.CreateButton("📥 دانلود قالب اکسل", UIHelper.WarmSecondary, 180, 38);
            btnDownloadTemplate.Click += BtnDownloadTemplate_Click;
            btnImportExcel = UIHelper.CreateButton("📤 ایمپورت از اکسل", UIHelper.SectionTeal, 180, 38);
            btnImportExcel.Click += BtnImportExcel_Click;
            pnlExcel.Controls.Add(btnImportExcel);
            pnlExcel.Controls.Add(btnDownloadTemplate);
            mainLayout.Controls.Add(pnlExcel, 0, 1);

            var pnlGrid = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            lblGridTitle = new Label
            {
                Text = "📋 لیست کارمزدهای ثبت شده اخیر",
                Dock = DockStyle.Top,
                Height = 25,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = UIHelper.WarmDanger,
                TextAlign = ContentAlignment.MiddleRight
            };

            dgvFees = new DataGridView { Dock = DockStyle.Fill };
            dgvFees.ApplyModernStyle("AccountId", "Category", "CostType", "TransactionType", "Direction", "PaymentScope", "ContractId", "ContractorContractId", "ClientStatementId");
            dgvFees.AutoGenerateColumns = false;

            dgvFees.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TransactionDate", HeaderText = "تاریخ", FillWeight = 20 });
            dgvFees.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AccountTitle", HeaderText = "حساب بانکی", FillWeight = 30 });
            dgvFees.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 20 });
            dgvFees.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 30 });

            dgvFees.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string colName = dgvFees.Columns[e.ColumnIndex].DataPropertyName;
                if (colName == "TransactionDate" && e.Value is DateTime dt)
                {
                    e.Value = DateHelper.ToPersianDate(dt);
                    e.FormattingApplied = true;
                }
                else if (colName == "Amount" && e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            };

            pnlGrid.Controls.Add(dgvFees);
            pnlGrid.Controls.Add(lblGridTitle);
            mainLayout.Controls.Add(pnlGrid, 0, 2);

            btnSave = UIHelper.CreateButton(_transactionId.HasValue ? "💾 ذخیره تغییرات" : "💾 ثبت کارمزد", UIHelper.WarmDanger, 220, 45);
            var btnPanel = new Panel { Dock = DockStyle.Fill };
            btnSave.Anchor = AnchorStyles.None;
            btnPanel.Controls.Add(btnSave);
            btnPanel.Resize += (s, e) => { btnSave.Left = (btnPanel.Width - btnSave.Width) / 2; btnSave.Top = (btnPanel.Height - btnSave.Height) / 2; };
            btnSave.Click += async (s, e) => await SaveSafeAsync();

            mainLayout.Controls.Add(btnPanel, 0, 3);
            this.Controls.Add(mainLayout);
        }

        private async Task LoadGridDataAsync()
        {
            if (IsDisposed) return;
            try
            {
                string query = @"
                    SELECT Id, TransactionDate, AccountTitle, Amount, Description 
                    FROM CashTransactions 
                    WHERE Category = N'هزینه بانکی'
                    ORDER BY TransactionDate DESC, Id DESC";

                DataTable dt = null!;
                await AsyncHelper.RunWithLoading(this, async () =>
                {
                    dt = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, query));
                }, "در حال بارگذاری لیست کارمزدها...");

                if (dt != null && !IsDisposed)
                {
                    dgvFees.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "BankFeeForm.LoadGridDataAsync");
            }
        }

        private async Task LoadAccountsAsync()
        {
            if (IsDisposed) return;
            try
            {
                var bankRepo = new BankAccountRepository(_connectionString);
                var accounts = await bankRepo.GetActiveAsync();
                if (IsDisposed) return;
                cmbAccount.DataSource = accounts;
                if (cmbAccount.Items.Count > 0) cmbAccount.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "BankFeeForm.LoadAccounts");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری حساب‌ها:\n" + ex.Message);
            }
        }

        // ✅ FIX: ذخیره‌ی عنوان اصلی و شناسه حساب
        private async Task LoadTransactionDataAsync(int id)
        {
            try
            {
                var trans = await _cashRepo.GetByIdAsync(id);
                if (trans == null || IsDisposed) return;

                _originalAccountId = trans.AccountId;
                _originalAccountTitle = trans.AccountTitle;

                if (trans.AccountId.HasValue)
                {
                    cmbAccount.SelectedValue = trans.AccountId.Value;
                    bool selected = cmbAccount.SelectedValue != null &&
                                    Convert.ToInt32(cmbAccount.SelectedValue) == trans.AccountId.Value;

                    if (!selected)
                    {
                        var bankRepo = new BankAccountRepository(_connectionString);
                        var allAccounts = await bankRepo.GetAllAsync();
                        var storedAcc = allAccounts.FirstOrDefault(a => a.Id == trans.AccountId.Value);

                        CompanyBankAccount displayEntry = storedAcc != null
                            ? new CompanyBankAccount
                            {
                                Id = storedAcc.Id,
                                Title = $"[غیرفعال] {storedAcc.Title}",
                                BankName = storedAcc.BankName,
                                AccountNumber = storedAcc.AccountNumber,
                                IsActive = false,
                                IsCashDesk = storedAcc.IsCashDesk
                            }
                            : new CompanyBankAccount
                            {
                                Id = trans.AccountId.Value,
                                Title = $"❌ حساب حذف‌شده (#{trans.AccountId.Value})",
                                BankName = "---",
                                AccountNumber = "---",
                                IsActive = false,
                                IsCashDesk = false
                            };

                        var currentList = new List<CompanyBankAccount>();
                        if (cmbAccount.DataSource is IEnumerable<CompanyBankAccount> existing)
                            currentList.AddRange(existing);
                        currentList.Add(displayEntry);

                        cmbAccount.DataSource = null;
                        cmbAccount.DataSource = currentList;
                        cmbAccount.SelectedValue = displayEntry.Id;

                        ModernMessageBox.ShowWarning(
                            $"حساب بانکی اصلی این کارمزد {(storedAcc != null ? "غیرفعال شده است" : "از سیستم حذف شده است")}.\n\n" +
                            "اگر بدون تغییر حساب ذخیره کنید، کارمزد روی همان حساب اصلی باقی می‌ماند.\n" +
                            "اگر می‌خواهید کارمزد را به حساب دیگری منتقل کنید، از کادر بالا حساب جدید را انتخاب کنید.");
                    }
                }

                txtAmount.Text = NumberHelper.FormatThousands(trans.Amount, usePersianDigits: true);
                txtDate.Text = DateHelper.ToPersianDate(trans.TransactionDate);
                txtDescription.Text = trans.Description;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "BankFeeForm.LoadTransactionData");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات کارمزد:\n" + ex.Message);
            }
        }

        private static string NormalizeAccountTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return title;
            const string inactivePrefix = "[غیرفعال] ";
            if (title.StartsWith(inactivePrefix))
                return title.Substring(inactivePrefix.Length);
            if (title.StartsWith("❌ حساب حذف‌شده"))
                return "حساب حذف‌شده";
            return title;
        }

        // ✅ FIX: استفاده از _originalAccountId به عنوان fallback + تایید هنگام تغییر حساب
        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (cmbAccount.SelectedValue == null) { ModernMessageBox.ShowWarning("لطفاً یک حساب بانکی را انتخاب کنید."); return; }
                decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
                if (amount <= 0) { ModernMessageBox.ShowWarning("مبلغ کارمزد باید بزرگتر از صفر باشد."); return; }
                DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
                if (date == null) { ModernMessageBox.ShowWarning("تاریخ وارد شده معتبر نیست."); return; }

                var selectedAcc = (CompanyBankAccount)cmbAccount.SelectedItem!;
                string desc = string.IsNullOrWhiteSpace(txtDescription.Text) ? "کارمزد بانکی" : txtDescription.Text.Trim();

                // ✅ FIX: تصمیم‌گیری درباره حساب مؤثر
                int effectiveAccountId;
                string effectiveTitle;

                if (_transactionId.HasValue && !_userChangedAccount && _originalAccountId.HasValue)
                {
                    // کاربر حساب را تغییر نداده → روی همان حساب اصلی بمان
                    effectiveAccountId = _originalAccountId.Value;
                    effectiveTitle = !string.IsNullOrWhiteSpace(_originalAccountTitle)
                        ? _originalAccountTitle!
                        : NormalizeAccountTitle(selectedAcc.Title);
                }
                else
                {
                    effectiveAccountId = selectedAcc.Id;
                    effectiveTitle = NormalizeAccountTitle(selectedAcc.Title);

                    // ✅ FIX: اگر در حالت ویرایش است و کاربر واقعاً حساب را عوض کرده، تایید بگیر
                    if (_transactionId.HasValue && _userChangedAccount && _originalAccountId.HasValue
                        && effectiveAccountId != _originalAccountId.Value)
                    {
                        var originalAccTitle = !string.IsNullOrWhiteSpace(_originalAccountTitle)
                            ? _originalAccountTitle!
                            : $"حساب شناسه {_originalAccountId.Value}";

                        var confirmed = ModernMessageBox.AskConfirm(
                            $"⚠️ شما در حال تغییر حساب بانکی این کارمزد هستید.\n\n" +
                            $"از: «{originalAccTitle}»\n" +
                            $"به: «{effectiveTitle}»\n\n" +
                            $"با تایید:\n" +
                            $"  • مبلغ قبلی به حساب اصلی بازگردانده می‌شود\n" +
                            $"  • مبلغ جدید از حساب انتخابی کسر می‌شود\n\n" +
                            $"آیا ادامه می‌دهید؟",
                            "تغییر حساب کارمزد");

                        if (!confirmed) return;
                    }
                }

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var trans = new CashTransaction
                    {
                        Id = _transactionId ?? 0,
                        TransactionDate = date.Value,
                        Amount = amount,
                        TransactionType = "نقدی",
                        Direction = DirectionTypes.Outgoing,
                        AccountId = effectiveAccountId,
                        AccountTitle = effectiveTitle,
                        Category = "هزینه بانکی",
                        CostType = "خدمات",
                        Description = desc
                    };

                    if (_transactionId.HasValue)
                    {
                        await _cashRepo.UpdateAsync(trans);
                    }
                    else
                    {
                        await _cashRepo.AddAsync(trans);
                    }
                }, "در حال ثبت کارمزد...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show(_transactionId.HasValue ? "تغییرات با موفقیت ذخیره شد." : "کارمزد بانکی با موفقیت ثبت شد.", isSuccess: true);

                    if (!_transactionId.HasValue)
                    {
                        txtAmount.Clear();
                        txtDescription.Clear();
                        await LoadGridDataAsync();
                    }
                    else
                    {
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "BankFeeForm.SaveSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ثبت:\n" + ex.Message);
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
                FileName = "BankFee_Import_Template.xlsx",
                Title = "ذخیره قالب اکسل کارمزد بانکی"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("کارمزد بانکی");
                ws.RightToLeft = true;

                string[] headers = { "تاریخ (شمسی)", "مبلغ (ریال)", "توضیحات" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                ws.Cell(2, 1).Value = DateHelper.ToPersianDate(DateTime.Now);
                ws.Cell(2, 2).Value = 50000;
                ws.Cell(2, 3).Value = "کارمزد انتقال وجه";

                ws.Columns().AdjustToContents();
                workbook.SaveAs(sfd.FileName);
                ToastHelper.Show("قالب اکسل ساخته شد.", true);
            }
            catch (Exception ex)
            {
                ModernMessageBox.ShowError("خطا در ساخت قالب:\n" + ex.Message);
            }
        }

        private async void BtnImportExcel_Click(object? sender, EventArgs e)
        {
            if (cmbAccount.SelectedValue == null) { ModernMessageBox.ShowWarning("لطفاً ابتدا حساب بانکی را انتخاب کنید."); return; }

            using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل کارمزدها" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                var selectedAcc = (CompanyBankAccount)cmbAccount.SelectedItem!;
                string cleanAccountTitle = NormalizeAccountTitle(selectedAcc.Title);
                int successCount = 0;
                List<string> errors = new();

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    using var workbook = new XLWorkbook(ofd.FileName);
                    var ws = workbook.Worksheet(1);
                    var rows = ws.RowsUsed().Skip(1);

                    foreach (var row in rows)
                    {
                        try
                        {
                            string dateStr = row.Cell(1).GetFormattedString().Trim();
                            string amountStr = row.Cell(2).GetFormattedString().Trim();
                            string desc = row.Cell(3).GetFormattedString().Trim();

                            if (string.IsNullOrWhiteSpace(dateStr) && string.IsNullOrWhiteSpace(amountStr)) continue;

                            DateTime? date = DateHelper.ParsePersianDate(dateStr);
                            if (date == null) throw new Exception($"تاریخ «{dateStr}» معتبر نیست.");

                            decimal amount = NumberHelper.ParseFormatted(amountStr);
                            if (amount <= 0) throw new Exception("مبلغ نامعتبر است.");

                            var trans = new CashTransaction
                            {
                                TransactionDate = date.Value,
                                Amount = amount,
                                TransactionType = "نقدی",
                                Direction = DirectionTypes.Outgoing,
                                AccountId = selectedAcc.Id,
                                AccountTitle = cleanAccountTitle,
                                Category = "هزینه بانکی",
                                CostType = "خدمات",
                                Description = string.IsNullOrWhiteSpace(desc) ? "کارمزد بانکی" : desc
                            };

                            await _cashRepo.AddAsync(trans);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"سطر {row.RowNumber()}: {ex.Message}");
                        }
                    }
                }, "در حال ایمپورت از اکسل...");

                if (success && !IsDisposed)
                {
                    string msg = $"✅ تعداد {successCount} ردیف کارمزد ثبت شد.";
                    if (errors.Count > 0)
                    {
                        msg += $"\n\n⚠️ {errors.Count} خطا:\n" + string.Join("\n", errors.Take(5));
                    }
                    ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                    await LoadGridDataAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "BankFeeForm.ImportExcel");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ایمپورت:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}