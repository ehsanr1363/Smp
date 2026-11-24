using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using ClosedXML.Excel;
using Smp.Models;
using Smp.Helpers;
using Smp.Data;

namespace Smp.Forms;

public class ClientPaymentForm : Form
{
    private readonly string _connectionString;
    private ClientContract? _contract;

    private readonly CashTransactionRepository _cashRepo;
    private readonly CashCheckRepository _checkRepo;

    private ModernPage page = null!;
    private ModernCard cardMain = null!, cardCheck = null!, cardDesc = null!;
    private bool _layoutReady;
    private bool _checkVisible;
    private const int HeaderHeight = 50;   // هدر مدرن فرم
    private const int FooterHeight = 64;   // نوار دکمه‌ها
    private const int CardGap = 8;
    private TextBox txtAmount = null!, txtDescription = null!, txtReceiptDate = null!;
    private ComboBox cmbReceiptMethod = null!, cmbAccount = null!, cmbContract = null!;
    private TextBox txtCheckNumber = null!, txtDueDate = null!;

    private Button btnSave = null!, btnDownloadTemplate = null!, btnImportExcel = null!;

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    // حالت ویرایش
    private readonly int? _editTransactionId;
    private CashTransaction? _editing;

    public ClientPaymentForm(string connectionString, ClientContract contract) : this(connectionString, contract, true) { }

    public ClientPaymentForm(string connectionString) : this(connectionString, null, true) { }

    /// <summary>حالت ویرایش: یک دریافتی نقدی / حواله‌ی بانکی ثبت‌شده از کارفرما را (با شناسه‌ی CashTransactions) ویرایش می‌کند</summary>
    public ClientPaymentForm(string connectionString, int editTransactionId) : this(connectionString, null, true, editTransactionId) { }

    // _contract باید قبل از BuildUI مقدار بگیرد (عنوان و کمبوی قرارداد به آن وابسته است)
    private ClientPaymentForm(string connectionString, ClientContract? contract, bool _, int? editTransactionId = null)
    {
        _editTransactionId = editTransactionId;
        _contract = contract;
        _connectionString = connectionString;
        _cashRepo = new CashTransactionRepository(connectionString);
        _checkRepo = new CashCheckRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader(GetFormTitle(), new Size(700, 480), isDialog: true);
        ShowInTaskbar = false;
        _layoutReady = true;
        ApplyLayoutHeight();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        Shown += async (s, e) =>
        {
            await LoadContractsAsync();
            await LoadAccountsAsync();
            if (_editTransactionId.HasValue) await LoadForEditAsync(_editTransactionId.Value);
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientPaymentForm"); } }
        base.Dispose(disposing);
    }

    private string GetFormTitle()
        => _editTransactionId.HasValue ? "ویرایش دریافت وجه از کارفرما"
         : _contract is not null ? $"ثبت دریافت وجه - قرارداد {_contract.ContractNumber}" : "ثبت دریافت وجه از کارفرما";

    /// <summary>ارتفاع فرم را به اندازه‌ی کارت‌های نمایان می‌کند (بدون فضای خالی اضافه)</summary>
    private void ApplyLayoutHeight()
    {
        if (!_layoutReady || page is null) return;
        // نکته: Control.Visible قبل از نمایش فرم برای همه false است؛ پس از فلگ خودمان استفاده می‌کنیم
        int content = page.Padding.Vertical + cardMain.Height + CardGap + cardDesc.Height + CardGap;
        if (_checkVisible) content += cardCheck.Height + CardGap;

        ClientSize = new Size(ClientSize.Width, HeaderHeight + FooterHeight + content);

        var wa = (IsHandleCreated ? Screen.FromControl(this) : Screen.PrimaryScreen)?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        if (Height > wa.Height - 10) Height = wa.Height - 10;
        if (IsHandleCreated && Bottom > wa.Bottom) Top = Math.Max(wa.Top, wa.Bottom - Height);
    }

    private void BuildUI()
    {
        int FH = ModernUI.FieldHeight;

        // ───────────── کارت ۱: اطلاعات دریافت ─────────────
        cardMain = new ModernCard("💰 اطلاعات دریافت", ModernTheme.Success, 2, FH, FH, FH);

        cmbContract = ModernUI.Combo();
        cmbContract.DisplayMember = "DisplayTitle"; cmbContract.ValueMember = "Id";
        string contractLabel = "انتخاب قرارداد کارفرما";
        if (_contract is not null)
        {
            cmbContract.Items.Add(new { Id = _contract.Id, DisplayTitle = $"{_contract.ContractNumber} - {_contract.Subject}" });
            cmbContract.SelectedIndex = 0;
            cmbContract.Enabled = false;
            contractLabel = "قرارداد کارفرما";
        }

        var dateBox = ModernUI.Input();
        dateBox.TextAlign = HorizontalAlignment.Center;
        txtReceiptDate = dateBox.TextBox;
        DateHelper.ApplyDateMask(txtReceiptDate);
        txtReceiptDate.Text = DateHelper.ToPersianDate(DateTime.Now);

        var amountBox = ModernUI.Money();
        amountBox.Font = ModernTheme.GetFont(13f, FontStyle.Bold);
        txtAmount = amountBox.TextBox;
        txtAmount.ForeColor = ModernTheme.Dark;

        cmbReceiptMethod = ModernUI.Combo();
        cmbReceiptMethod.Items.AddRange(new[] { "نقدی", "حواله بانکی", "چک", "سایر" });
        cmbReceiptMethod.SelectedIndex = 0;
        cmbReceiptMethod.SelectedIndexChanged += (s, e) => ToggleCheckFields();

        cmbAccount = ModernUI.Combo();
        cmbAccount.DisplayMember = "DisplayTitle"; cmbAccount.ValueMember = "Id";

        var fldContract = ModernUI.Field(contractLabel, cmbContract);
        cardMain.Body.Controls.Add(fldContract, 0, 0);
        cardMain.Body.SetColumnSpan(fldContract, 2);
        cardMain.Body.Controls.Add(ModernUI.Field("تاریخ دریافت", dateBox), 0, 1);
        cardMain.Body.Controls.Add(ModernUI.Field("مبلغ دریافتی (ریال)", amountBox), 1, 1);
        cardMain.Body.Controls.Add(ModernUI.Field("حساب بانکی / صندوق", cmbAccount), 0, 2);
        cardMain.Body.Controls.Add(ModernUI.Field("روش دریافت", cmbReceiptMethod), 1, 2);

        // ───────────── کارت ۲: اطلاعات چک (فقط وقتی روش = چک) ─────────────
        cardCheck = new ModernCard("🧾 اطلاعات چک", ModernTheme.Orange, 2, FH);
        var checkBox = ModernUI.Input();
        txtCheckNumber = checkBox.TextBox;
        var dueBox = ModernUI.Input();
        dueBox.TextAlign = HorizontalAlignment.Center;
        txtDueDate = dueBox.TextBox;
        DateHelper.ApplyDateMask(txtDueDate);
        cardCheck.Body.Controls.Add(ModernUI.Field("شماره چک", checkBox), 0, 0);
        cardCheck.Body.Controls.Add(ModernUI.Field("تاریخ سررسید چک", dueBox), 1, 0);

        // ───────────── کارت ۳: توضیحات ─────────────
        cardDesc = new ModernCard("📝 توضیحات (اختیاری)", ModernTheme.Neutral, 1, ModernUI.InputHeight + 4);
        var memo = ModernUI.Input();
        memo.Dock = DockStyle.Fill;
        memo.Margin = new Padding(6, 2, 6, 2);
        txtDescription = memo.TextBox;
        cardDesc.Body.Controls.Add(memo, 0, 0);

        page = new ModernPage();
        page.AddCard(cardMain, CardGap);
        page.AddCard(cardCheck, CardGap);
        page.AddCard(cardDesc, CardGap);

        // ───────────── نوار دکمه‌ها (همه در یک ردیف) ─────────────
        btnSave = ModernButton.Success("💾 ثبت دریافتی", 170, 40);
        btnSave.Click += async (s, e) => await SaveSafeAsync();

        btnImportExcel = ModernButton.Primary("📤 ایمپورت از اکسل", 160, 40);
        btnImportExcel.Click += async (s, e) => await BtnImportExcel_Click(s, e);

        btnDownloadTemplate = ModernButton.Outline("📥 دانلود قالب اکسل", ModernTheme.Neutral, 160, 40);
        btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

        var cancel = ModernButton.Cancel();
        cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var bar = ModernForm.CreateButtonBar(btnSave, btnImportExcel, btnDownloadTemplate, cancel);
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight));
        root.Controls.Add(page, 0, 0);
        root.Controls.Add(bar, 0, 1);
        Controls.Add(root);

        ToggleCheckFields();
    }

    private async Task LoadContractsAsync()
    {
        if (_contract is not null) return;
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

    /// <summary>پر کردن فرم با اطلاعات دریافتی ثبت‌شده (حالت ویرایش)</summary>
    private async Task LoadForEditAsync(int id)
    {
        try
        {
            var trans = await _cashRepo.GetByIdAsync(id);
            if (trans is null || IsDisposed)
            {
                if (!IsDisposed) ModernMessageBox.ShowWarning("دریافتی موردنظر پیدا نشد (ممکن است حذف شده باشد).");
                return;
            }
            _editing = trans;

            // ویرایش چک‌ها از فرم مخصوص چک انجام می‌شود؛ تبدیل دریافتی نقدی به چک در اینجا ممکن نیست
            cmbReceiptMethod.Items.Remove("چک");
            if (!string.IsNullOrWhiteSpace(trans.TransactionType) && !cmbReceiptMethod.Items.Contains(trans.TransactionType))
                cmbReceiptMethod.Items.Add(trans.TransactionType);
            cmbReceiptMethod.Text = string.IsNullOrWhiteSpace(trans.TransactionType) ? "نقدی" : trans.TransactionType;

            // قرارداد: اگر دریافتی به قراردادی وصل نبود، انتخاب خالی می‌ماند تا کاربر خودش انتخاب کند
            cmbContract.SelectedIndex = -1;
            if (trans.ContractId.HasValue) cmbContract.SelectedValue = trans.ContractId.Value;

            // حساب: اگر حساب قبلی غیرفعال شده باشد در فهرست نیست؛ بی‌صدا حساب دیگری جایگزین نمی‌شود
            cmbAccount.SelectedIndex = -1;
            if (trans.AccountId.HasValue) cmbAccount.SelectedValue = trans.AccountId.Value;
            if (cmbAccount.SelectedItem is null)
                ModernMessageBox.ShowWarning("حساب بانکی قبلی این دریافتی در فهرست حساب‌های فعال نیست. لطفاً حساب را انتخاب کنید.");

            txtReceiptDate.Text = DateHelper.ToPersianDate(trans.TransactionDate);
            txtAmount.Text = NumberHelper.FormatThousands(trans.Amount, usePersianDigits: true);
            txtDescription.Text = trans.Description ?? "";

            // دانلود قالب / ایمپورت اکسل فقط برای ثبت جدید است
            btnDownloadTemplate.Visible = false;
            btnImportExcel.Visible = false;
            ToggleCheckFields();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ClientPaymentForm.LoadForEditAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری دریافتی:\n" + ex.Message);
        }
    }

    private void ToggleCheckFields()
    {
        _checkVisible = cmbReceiptMethod.Text == "چک";
        cardCheck.Visible = _checkVisible;
        ApplyLayoutHeight();
    }

    // ✅ اصلاح شده: استفاده از ریپازیتوری‌ها به جای SQL مستقیم
    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (cmbContract.SelectedItem is null)
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
            if (pDate is null) { ModernMessageBox.ShowWarning("تاریخ دریافت معتبر نیست."); return; }

            string method = cmbReceiptMethod.Text;
            DateTime? checkDate = null;

            if (method == "چک")
            {
                if (string.IsNullOrWhiteSpace(txtCheckNumber.Text)) { ModernMessageBox.ShowWarning("شماره چک را وارد کنید."); return; }
                checkDate = DateHelper.ParsePersianDate(txtDueDate.Text);
                if (checkDate is null) { ModernMessageBox.ShowWarning("تاریخ سررسید چک معتبر نیست."); return; }
            }

            dynamic selectedContract = cmbContract.SelectedItem;
            int contractId = (int)selectedContract.Id;
            string contractTitle = _contract is not null ? (_contract.Subject ?? "") : selectedContract.DisplayTitle.ToString();
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
                    if (_editTransactionId.HasValue)
                    {
                        // ویرایش: همان رکورد بارگذاری‌شده با حفظ سایر فیلدها (دسته‌بندی، صورت‌وضعیت و ...) به‌روزرسانی می‌شود
                        if (_editing is null) throw new InvalidOperationException("اطلاعات دریافتی بارگذاری نشده است.");
                        _editing.ContractId = contractId;
                        _editing.ContractTitle = contractTitle;
                        _editing.TransactionDate = pDate.Value;
                        _editing.Amount = amount;
                        _editing.TransactionType = method;
                        _editing.Direction = DirectionTypes.Incoming;
                        _editing.AccountId = selectedAcc.Id;
                        _editing.AccountTitle = selectedAcc.Title;
                        _editing.Description = desc;
                        await _cashRepo.UpdateAsync(_editing);
                    }
                    else
                    {
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
                }
            }, _editTransactionId.HasValue ? "در حال ذخیره تغییرات..." : "در حال ثبت دریافتی...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show(_editTransactionId.HasValue ? "دریافتی با موفقیت ویرایش شد." : "دریافتی با موفقیت ثبت شد.", isSuccess: true);
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
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientPaymentForm"); }
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
        if (cmbContract.SelectedItem is null)
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
            string contractTitle = _contract is not null ? (_contract.Subject ?? "") : selectedContract.DisplayTitle.ToString();

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
                        if (date is null) throw new Exception("تاریخ دریافت نامعتبر است.");

                        decimal amount = NumberHelper.ParseFormatted(amountStr);
                        if (amount <= 0) throw new Exception("مبلغ نامعتبر است.");

                        if (string.IsNullOrWhiteSpace(method)) throw new Exception("روش دریافت خالی است.");
                        if (method != "نقدی" && method != "حواله بانکی" && method != "چک" && method != "سایر")
                            throw new Exception("روش دریافت نامعتبر است.");

                        var account = accounts.FirstOrDefault(a => a.Title == accTitle);
                        if (account is null) throw new Exception($"حساب «{accTitle}» یافت نشد.");

                        DateTime? checkDate = null;
                        if (method == "چک")
                        {
                            if (string.IsNullOrWhiteSpace(checkNum)) throw new Exception("شماره چک خالی است.");
                            checkDate = DateHelper.ParsePersianDate(checkDateStr);
                            if (checkDate is null) throw new Exception("تاریخ سررسید چک نامعتبر است.");
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
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ClientPaymentForm"); }
        }
    }
}
