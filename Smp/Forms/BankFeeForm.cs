using ClosedXML.Excel;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Smp.Forms;

public class BankFeeForm : Form
{
    private readonly string _connectionString;
    private readonly CashTransactionRepository _transRepo;
    private readonly BankAccountRepository _bankRepo;

    private ComboBox cmbAccount = null!;
    private TextBox txtAmount = null!, txtDate = null!, txtDescription = null!;
    private Button btnSave = null!, btnDownloadTemplate = null!, btnImportExcel = null!;
    private Button btnEdit = null!, btnDelete = null!, btnCancelEdit = null!;
    private ModernCard cardInput = null!;

    // حالت ویرایش: رکورد کامل در حال ویرایش (برای حفظ سایر فیلدها مثل جهت، دسته‌بندی و ...)
    private CashTransaction? _editingEntity;
    private DataGridView dgvRecent = null!;
    private ModernStatCard statCount = null!, statTotal = null!;

    public BankFeeForm(string connectionString)
    {
        _connectionString = connectionString;
        _transRepo = new CashTransactionRepository(connectionString);
        _bankRepo = new BankAccountRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("ثبت کارمزد بانکی", new Size(750, 800));
        ShowInTaskbar = false;

        Shown += async (s, e) => await LoadDataAsync();
    }

    private void BuildUI()
    {
        // ═══════ ۱. کارت ورودی ═══════
        cmbAccount = ModernUI.Combo();
        cmbAccount.DisplayMember = "DisplayTitle";
        cmbAccount.ValueMember = "Id";
        txtAmount = new TextBox();
        SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
        txtDate = new TextBox { TextAlign = HorizontalAlignment.Center };
        DateHelper.ApplyDateMask(txtDate);
        txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);
        txtDescription = new TextBox { PlaceholderText = "مثلاً: کارمزد انتقال وجه، صدور چک و..." };

        const int rh = 50;
        var card = new ModernCard("اطلاعات کارمزد", ModernTheme.Brand, 2, rh, rh, rh);
        cardInput = card;
        var rowAccount = ModernUI.Row("حساب بانکی", cmbAccount);
        card.Body.Controls.Add(rowAccount, 0, 0);
        card.Body.SetColumnSpan(rowAccount, 2);
        card.Body.Controls.Add(ModernUI.Row("مبلغ (ریال)", txtAmount), 0, 1);
        card.Body.Controls.Add(ModernUI.Row("تاریخ", txtDate, 80), 1, 1);
        var rowDesc = ModernUI.Row("توضیحات (بابت)", txtDescription);
        card.Body.Controls.Add(rowDesc, 0, 2);
        card.Body.SetColumnSpan(rowDesc, 2);

        // ═══════ ۲. کارت‌های خلاصه ═══════
        var strip = ModernReport.CreateStatStrip(2, 76);
        statCount = strip.Add("تعداد کارمزدهای اخیر", ModernTheme.Primary);
        statTotal = strip.Add("جمع کارمزدهای اخیر", ModernTheme.Danger);
        statCount.SetCount(0, "کارمزد");
        statTotal.SetAmount(0, "ریال");
        strip.Dock = DockStyle.Fill;

        // ═══════ ۳. عنوان لیست + دکمه‌های اکسل ═══════
        var lblGrid = ModernUI.Caption("لیست کارمزدهای ثبت‌شده اخیر", 11f, true, ModernTheme.TextStrong);
        lblGrid.Dock = DockStyle.Fill;

        btnDownloadTemplate = ModernButton.Soft("دانلود قالب اکسل", ModernTheme.Secondary, 160, 36);
        btnDownloadTemplate.Click += BtnDownloadTemplate_Click;
        btnImportExcel = ModernButton.Soft("ایمپورت از اکسل", ModernTheme.Teal, 150, 36);
        btnImportExcel.Click += BtnImportExcel_Click;

        var excelFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
            BackColor = Color.Transparent
        };
        excelFlow.Controls.Add(btnDownloadTemplate);
        excelFlow.Controls.Add(btnImportExcel);

        var toolbar = new BufferedTable
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Margin = Padding.Empty, BackColor = Color.Transparent
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340F));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        toolbar.Controls.Add(lblGrid, 0, 0);
        toolbar.Controls.Add(excelFlow, 1, 0);

        // ═══════ ۴. جدول کارمزدهای اخیر ═══════
        dgvRecent = ModernGrid.Create();
        dgvRecent.AutoGenerateColumns = false;
        dgvRecent.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvRecent.MultiSelect = true;
        dgvRecent.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) _ = StartEditSelectedAsync(); };
        dgvRecent.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Delete) { e.Handled = true; await DeleteSelectedAsync(); }
        };

        dgvRecent.Columns.Add(new DataGridViewTextBoxColumn { Name = "TransactionDate", DataPropertyName = "TransactionDate", HeaderText = "تاریخ", FillWeight = 18 });
        dgvRecent.Columns.Add(new DataGridViewTextBoxColumn { Name = "AccountTitle", DataPropertyName = "AccountTitle", HeaderText = "عنوان حساب", FillWeight = 34 });
        dgvRecent.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 20 });
        dgvRecent.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 28 });

        dgvRecent.UseAiryLook(44);
        dgvRecent.UseSelectionAccent();
        dgvRecent.UseTextAlignment("AccountTitle", "Description");

        dgvRecent.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvRecent.Columns[e.ColumnIndex].DataPropertyName;

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

        // ═══════ ۵. دکمه ثبت ═══════
        btnSave = ModernButton.Save("ثبت کارمزد", 200, 40);
        btnSave.Click += async (s, e) => await SaveFeeAsync();
        btnCancelEdit = ModernButton.Cancel("انصراف از ویرایش", 150, 40);
        btnCancelEdit.Visible = false;
        btnCancelEdit.Click += (s, e) => ExitEditMode(clearInputs: true);
        btnEdit = ModernButton.Soft("ویرایش انتخاب‌شده", ModernTheme.Primary, 160, 40);
        btnEdit.Click += async (s, e) => await StartEditSelectedAsync();
        btnDelete = ModernButton.Soft("حذف انتخاب‌شده‌ها", ModernTheme.Danger, 160, 40);
        btnDelete.Click += async (s, e) => await DeleteSelectedAsync();
        var bar = ModernForm.CreateButtonBar(btnSave, btnCancelEdit, btnEdit, btnDelete);

        // ═══════ چیدمان صفحه ═══════
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            Padding = new Padding(14, 12, 14, 6), BackColor = ModernTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));              // کارت‌های خلاصه
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, card.Height + 10)); // کارت ورودی
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));              // عنوان + اکسل
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));              // جدول
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));              // دکمه ثبت
        card.Dock = DockStyle.Fill;
        root.Controls.Add(strip, 0, 0);
        root.Controls.Add(card, 0, 1);
        root.Controls.Add(toolbar, 0, 2);
        root.Controls.Add(ModernReport.WrapGrid(dgvRecent), 0, 3);
        root.Controls.Add(bar, 0, 4);

        this.Controls.Add(root);
    }

    private void UpdateStats(DataTable dt)
    {
        decimal total = 0;
        foreach (DataRow r in dt.Rows)
            if (r["Amount"] != DBNull.Value) total += Convert.ToDecimal(r["Amount"]);
        statCount.SetCount(dt.Rows.Count, "کارمزد");
        statTotal.SetAmount(total, "ریال");
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // ۱. فیلتر کردن حساب‌ها: فقط حساب‌های بانکی (حذف تنخواه‌ها)
            var allAccounts = await _bankRepo.GetActiveAsync();
            var bankAccounts = allAccounts.Where(a => !a.IsCashDesk).ToList();

            cmbAccount.DataSource = bankAccounts;

            // ۲. خواندن مستقیم کارمزدها از دیتابیس (برای نمایش اطلاعات قبلی)
            // این کوئری هر رکوردی که کلمه کارمزد در نوع تراکنش، دسته بندی یا توضیحات آن باشد را می‌آورد.
            string sql = @"
                    SELECT TOP 100 
                        t.Id, 
                        t.TransactionType, 
                        t.Category, 
                        t.TransactionDate, 
                        ISNULL(t.AccountTitle, '---') AS AccountTitle, 
                        t.Amount, 
                        ISNULL(t.Description, '---') AS Description
                    FROM CashTransactions t
                    WHERE t.TransactionType = N'کارمزد' 
                       OR t.Category = N'کارمزد بانکی' 
                       OR t.Category = N'کارمزد'
                       OR t.Description LIKE N'%کارمزد%'
                    ORDER BY t.TransactionDate DESC, t.Id DESC";

            var dt = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, sql));
            dgvRecent.DataSource = dt;
            UpdateStats(dt);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "BankFeeForm.LoadDataAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات:\n" + ex.Message);
        }
    }

    private async Task SaveFeeAsync()
    {
        if (cmbAccount.SelectedItem is null) { ModernMessageBox.ShowWarning("لطفاً حساب بانکی را انتخاب کنید."); return; }
        decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
        if (amount <= 0) { ModernMessageBox.ShowWarning("مبلغ کارمزد نامعتبر است."); return; }
        DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
        if (date is null) { ModernMessageBox.ShowWarning("تاریخ نامعتبر است."); return; }

        var acc = (CompanyBankAccount)cmbAccount.SelectedItem;

        if (_editingEntity is not null)
        {
            await SaveEditAsync(acc, amount, date.Value);
            return;
        }

        var trans = new CashTransaction
        {
            AccountId = acc.Id,
            AccountTitle = acc.Title,
            TransactionDate = date.Value,
            Amount = amount,
            Direction = DirectionTypes.Outgoing,
            TransactionType = "کارمزد",
            PaymentScope = PaymentScopes.Administrative,
            Category = "کارمزد بانکی",
            CostType = "سایر",
            Description = txtDescription.Text.Trim()
        };

        bool success = await AsyncHelper.RunWithLoading(this, async () => {
            await _transRepo.AddAsync(trans);
        }, "در حال ثبت کارمزد...");

        if (success)
        {
            ToastHelper.Show("کارمزد با موفقیت ثبت شد.", true);
            txtAmount.Clear();
            txtDescription.Clear();
            await LoadDataAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ویرایش و حذف
    // ═══════════════════════════════════════════════════════════════
    private static string CleanAccountTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return title;
        const string inactivePrefix = "[غیرفعال] ";
        if (title.StartsWith(inactivePrefix)) return title.Substring(inactivePrefix.Length);
        if (title.StartsWith("❌ حساب حذف‌شده")) return "حساب حذف‌شده";
        return title;
    }

    // کوئری لیست هر رکوردی که در توضیحات کلمه «کارمزد» دارد را هم می‌آورد؛
    // ویرایش/حذف فقط برای کارمزدهای واقعی بانکی مجاز است.
    private static bool IsRealFee(DataRow r)
    {
        string type = r["TransactionType"] == DBNull.Value ? "" : r["TransactionType"].ToString() ?? "";
        string cat = r["Category"] == DBNull.Value ? "" : r["Category"].ToString() ?? "";
        return type == "کارمزد" || cat == "کارمزد بانکی" || cat == "کارمزد";
    }

    private List<DataRow> GetSelectedRows()
    {
        var list = new List<DataRow>();
        foreach (DataGridViewRow gr in dgvRecent.SelectedRows)
            if (gr.DataBoundItem is DataRowView drv) list.Add(drv.Row);
        return list;
    }

    private async Task StartEditSelectedAsync()
    {
        var rows = GetSelectedRows();
        if (rows.Count == 0) { ModernMessageBox.ShowWarning("لطفاً یک ردیف را از جدول انتخاب کنید."); return; }
        if (rows.Count > 1) { ModernMessageBox.ShowWarning("برای ویرایش فقط یک ردیف انتخاب کنید."); return; }
        if (!IsRealFee(rows[0]))
        {
            ModernMessageBox.ShowWarning("این ردیف کارمزد بانکی ثبت‌شده از این فرم نیست (فقط در توضیحات آن کلمه «کارمزد» آمده است).\n\nبرای ویرایش آن از فرم/لیست مربوط به خودش استفاده کنید.");
            return;
        }

        try
        {
            int id = Convert.ToInt32(rows[0]["Id"]);
            var entity = await _transRepo.GetByIdAsync(id);
            if (entity is null)
            {
                ModernMessageBox.ShowWarning("این رکورد دیگر وجود ندارد (احتمالاً حذف شده است).");
                await LoadDataAsync();
                return;
            }
            if (entity.ClientStatementId.HasValue)
            {
                ModernMessageBox.ShowWarning("این تراکنش در یک صورت‌حساب ثبت شده و از اینجا قابل ویرایش نیست.");
                return;
            }

            if (entity.AccountId.HasValue) await EnsureAccountInComboAsync(entity.AccountId.Value);

            _editingEntity = entity;
            if (entity.AccountId.HasValue) cmbAccount.SelectedValue = entity.AccountId.Value;
            txtAmount.Text = NumberHelper.FormatThousands(entity.Amount, usePersianDigits: true);
            txtDate.Text = DateHelper.ToPersianDate(entity.TransactionDate);
            txtDescription.Text = entity.Description ?? "";

            btnSave.Text = "ذخیره تغییرات";
            btnCancelEdit.Visible = true;
            cardInput.TitleLabel.Text = "ویرایش کارمزد";
            txtAmount.Focus();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "BankFeeForm.StartEdit");
            ModernMessageBox.ShowError("خطا در بارگذاری کارمزد برای ویرایش:\n" + ex.Message);
        }
    }

    // اگر حساب رکورد غیرفعال/حذف‌شده باشد در کمبو نیست؛ موقتاً اضافه می‌شود تا حساب اصلی حفظ شود.
    private async Task EnsureAccountInComboAsync(int accountId)
    {
        var current = (cmbAccount.DataSource as IEnumerable<CompanyBankAccount>)?.ToList() ?? new List<CompanyBankAccount>();
        if (current.Any(a => a.Id == accountId)) return;

        var all = await _bankRepo.GetAllAsync();
        var stored = all.FirstOrDefault(a => a.Id == accountId);
        current.Add(stored is not null
            ? new CompanyBankAccount
            {
                Id = stored.Id,
                Title = $"[غیرفعال] {stored.Title}",
                BankName = stored.BankName,
                AccountNumber = stored.AccountNumber,
                IsActive = false,
                IsCashDesk = stored.IsCashDesk
            }
            : new CompanyBankAccount
            {
                Id = accountId,
                Title = $"❌ حساب حذف‌شده (#{accountId})",
                BankName = "---",
                AccountNumber = "---",
                IsActive = false,
                IsCashDesk = false
            });
        cmbAccount.DataSource = null;
        cmbAccount.DataSource = current;
    }

    private void ExitEditMode(bool clearInputs)
    {
        _editingEntity = null;
        btnSave.Text = "ثبت کارمزد";
        btnCancelEdit.Visible = false;
        cardInput.TitleLabel.Text = "اطلاعات کارمزد";
        if (clearInputs)
        {
            txtAmount.Clear();
            txtDescription.Clear();
            txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);
        }
    }

    private async Task SaveEditAsync(CompanyBankAccount acc, decimal amount, DateTime date)
    {
        var entity = _editingEntity!;
        // فقط فیلدهای قابل ویرایش تغییر می‌کنند؛ بقیه (جهت، نوع، دسته‌بندی و ...) همان مقدار قبلی می‌مانند
        entity.AccountId = acc.Id;
        entity.AccountTitle = CleanAccountTitle(acc.Title);
        entity.TransactionDate = date;
        entity.Amount = amount;
        entity.Description = txtDescription.Text.Trim();

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            await _transRepo.UpdateAsync(entity);
        }, "در حال ذخیره تغییرات...");

        if (success)
        {
            ToastHelper.Show("تغییرات کارمزد با موفقیت ذخیره شد.", true);
            ExitEditMode(clearInputs: true);
            await LoadDataAsync();
        }
    }

    private async Task DeleteSelectedAsync()
    {
        var rows = GetSelectedRows();
        if (rows.Count == 0) { ModernMessageBox.ShowWarning("لطفاً حداقل یک ردیف را از جدول انتخاب کنید."); return; }

        var valid = rows.Where(IsRealFee).ToList();
        int skipped = rows.Count - valid.Count;
        if (valid.Count == 0)
        {
            ModernMessageBox.ShowWarning("ردیف(های) انتخاب‌شده کارمزد بانکی ثبت‌شده از این فرم نیستند و از اینجا قابل حذف نیستند.");
            return;
        }

        decimal sum = valid.Sum(r => r["Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Amount"]));
        string msg = valid.Count == 1
            ? $"آیا از حذف این کارمزد مطمئن هستید؟\n\nمبلغ: {NumberHelper.FormatThousands(sum, usePersianDigits: true)} ریال"
            : $"آیا از حذف {NumberHelper.FormatThousands(valid.Count, usePersianDigits: true)} کارمزد انتخاب‌شده مطمئن هستید؟\n\nجمع مبلغ: {NumberHelper.FormatThousands(sum, usePersianDigits: true)} ریال";
        if (skipped > 0) msg += $"\n\n({skipped} ردیف غیرکارمزد از حذف مستثنی شد.)";
        msg += "\n\nتوجه: مبلغ به موجودی حساب بانکی برگردانده می‌شود.";
        if (!ModernMessageBox.AskConfirm(msg, "حذف کارمزد")) return;

        var ids = valid.Select(r => Convert.ToInt32(r["Id"])).ToList();
        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _transRepo.DeleteBatchAsync(ids);
            }, "در حال حذف کارمزد...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("کارمزد حذف و موجودی حساب اصلاح شد.", true);
                if (_editingEntity is not null && ids.Contains(_editingEntity.Id)) ExitEditMode(clearInputs: true);
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "BankFeeForm.Delete");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در حذف:\n" + ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // دانلود قالب اکسل استاندارد
    // ═══════════════════════════════════════════════════════════════
    private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx", FileName = "BankFee_Template.xlsx", Title = "ذخیره قالب اکسل کارمزد" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("کارمزدها");
            ws.RightToLeft = true;

            string[] headers = { "تاریخ (شمسی)", "مبلغ (ریال)", "توضیحات", "عنوان حساب (اختیاری)" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // ردیف نمونه
            ws.Cell(2, 1).Value = DateHelper.ToPersianDate(DateTime.Now);
            ws.Cell(2, 2).Value = 50000;
            ws.Cell(2, 3).Value = "کارمزد انتقال وجه";
            ws.Cell(2, 4).Value = ""; // اگر خالی باشد، حساب انتخاب شده در فرم استفاده می‌شود

            ws.Columns().AdjustToContents();
            ws.Row(1).Height = 30;
            wb.SaveAs(sfd.FileName);
            ToastHelper.Show("قالب اکسل ذخیره شد.", true);
        }
        catch (Exception ex) { ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
    }

    // ═══════════════════════════════════════════════════════════════
    // ایمپورت هوشمند از اکسل
    // ═══════════════════════════════════════════════════════════════
    private async void BtnImportExcel_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل کارمزدها" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        List<CashTransaction> transactions = new();
        List<string> errors = new();

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            var allAccounts = await _bankRepo.GetActiveAsync();
            var accounts = allAccounts.Where(a => !a.IsCashDesk).ToList(); // فقط حساب‌های بانکی

            using var wb = new XLWorkbook(ofd.FileName);
            var ws = wb.Worksheet(1);
            var rows = ws.RowsUsed().ToList();
            if (rows.Count < 2) return;

            // پیدا کردن ایندکس ستون‌ها بر اساس نام هدر (نه شماره ستون)
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in rows.First().CellsUsed())
            {
                string text = cell.GetString().Trim();
                if (!string.IsNullOrEmpty(text) && !headers.ContainsKey(text))
                    headers[text] = cell.Address.ColumnNumber;
            }

            int dateCol = headers.FirstOrDefault(h => h.Key.Contains("تاریخ")).Value;
            int amountCol = headers.FirstOrDefault(h => h.Key.Contains("مبلغ")).Value;
            int descCol = headers.FirstOrDefault(h => h.Key.Contains("توضیحات")).Value;
            int accCol = headers.FirstOrDefault(h => h.Key.Contains("حساب")).Value;

            int rowNum = 1;
            foreach (var row in rows.Skip(1))
            {
                rowNum++;
                try
                {
                    string dateStr = dateCol > 0 ? row.Cell(dateCol).GetString().Trim() : "";
                    string amountStr = amountCol > 0 ? row.Cell(amountCol).GetString().Trim() : "";
                    string descStr = descCol > 0 ? row.Cell(descCol).GetString().Trim() : "";
                    string accStr = accCol > 0 ? row.Cell(accCol).GetString().Trim() : "";

                    if (string.IsNullOrWhiteSpace(amountStr)) continue;

                    DateTime? date = DateHelper.ParsePersianDate(dateStr) ?? DateTime.Today;
                    decimal amount = NumberHelper.ParseFormatted(amountStr);

                    if (amount <= 0) throw new Exception("مبلغ نامعتبر است.");

                    // پیدا کردن حساب
                    CompanyBankAccount? targetAcc = null;
                    if (!string.IsNullOrWhiteSpace(accStr))
                    {
                        targetAcc = accounts.FirstOrDefault(a => a.Title.Contains(accStr) || a.DisplayTitle.Contains(accStr));
                    }

                    // اگر در اکسل حسابی نبود یا پیدا نشد، از حسابی که در بالای فرم انتخاب شده استفاده کن
                    if (targetAcc is null)
                    {
                        targetAcc = cmbAccount.SelectedItem as CompanyBankAccount;
                    }

                    if (targetAcc is null) throw new Exception("حساب بانکی مشخص نشد.");

                    transactions.Add(new CashTransaction
                    {
                        AccountId = targetAcc.Id,
                        AccountTitle = targetAcc.Title,
                        TransactionDate = date.Value,
                        Amount = amount,
                        Direction = DirectionTypes.Outgoing,
                        TransactionType = "کارمزد",
                        PaymentScope = PaymentScopes.Administrative,
                        Category = "کارمزد بانکی",
                        CostType = "سایر",
                        Description = string.IsNullOrWhiteSpace(descStr) ? "ایمپورت کارمزد از اکسل" : descStr
                    });
                }
                catch (Exception ex)
                {
                    errors.Add($"سطر {rowNum}: {ex.Message}");
                }
            }

            // ثبت در دیتابیس
            if (transactions.Any())
            {
                await _transRepo.BulkInsertAsync(transactions);
            }
        }, "در حال ایمپورت کارمزدها...");

        if (success)
        {
            string msg = $"✅ تعداد {transactions.Count} کارمزد با موفقیت ثبت شد.";
            if (errors.Count > 0) msg += $"\n\n⚠️ {errors.Count} خطا رخ داد:\n" + string.Join("\n", errors.Take(5));

            if (errors.Count > 0) ModernMessageBox.ShowWarning(msg, "نتیجه ایمپورت");
            else ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");

            await LoadDataAsync();
        }
    }
}
