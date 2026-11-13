using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using ClosedXML.Excel;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;

namespace SMP.Forms
{
    public class TankhahTransferForm : Form
    {
        private readonly string _connectionString;
        private readonly AccountTransferRepository _transferRepo;
        private readonly BankAccountRepository _bankRepo;
        private readonly TankhahTransferMode _mode;

        private ComboBox cmbSourceAccount = null!, cmbDestAccount = null!;
        private Label lblSourceBalance = null!, lblDestBalance = null!;
        private TextBox txtAmount = null!, txtDate = null!, txtDescription = null!;
        private Button btnSave = null!, btnCancel = null!, btnRefresh = null!;
        private Button btnEditRow = null!, btnDeleteRow = null!;
        private Button btnDownloadTemplate = null!, btnImportExcel = null!;
        private DataGridView dgvRecentTransactions = null!;
        private Label lblGridTitle = null!;

        private List<CompanyBankAccount> _allAccounts = new();
        private readonly SemaphoreSlim _actionLock = new(1, 1);
        private bool _isInitializing = true;

        private int? _editingTransferId = null;
        // ✅ FIX: نگه‌داری توضیحات اصلی برای حفظ در حالت ویرایش
        private string? _editingOriginalDescription = null;

        public TankhahTransferForm(string connectionString, TankhahTransferMode mode, int? preselectedTankhahId = null)
        {
            _connectionString = connectionString;
            _mode = mode;
            _transferRepo = new AccountTransferRepository(connectionString);
            _bankRepo = new BankAccountRepository(connectionString);

            BuildUI();
            string title = mode == TankhahTransferMode.BankToTankhah
                ? "شارژ تنخواه (از حساب بانکی)"
                : "تسویه تنخواه (به حساب بانکی)";
            this.ApplyStandardHeader(title, new Size(1100, 800));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            Shown += async (s, e) =>
            {
                await LoadAccountsAsync(preselectedTankhahId);
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(15)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));

            var lblModeHeader = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(14f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = _mode == TankhahTransferMode.BankToTankhah
                    ? UIHelper.SectionBlue
                    : UIHelper.SectionTeal,
                Text = _mode == TankhahTransferMode.BankToTankhah
                    ? "شارژ تنخواه از حساب بانکی"
                    : "تسویه تنخواه به حساب بانکی"
            };
            mainLayout.Controls.Add(lblModeHeader, 0, 0);

            var inputContainer = UIHelper.CreateWarmPanel();
            var formLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                Padding = new Padding(15, 10, 15, 10)
            };

            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));

            cmbSourceAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f) };
            cmbSourceAccount.SelectedIndexChanged += Cmb_SelectedIndexChanged;

            cmbDestAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f) };
            cmbDestAccount.SelectedIndexChanged += Cmb_SelectedIndexChanged;

            lblSourceBalance = new Label { Text = "موجودی: 0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UIHelper.PersianFont(9f, FontStyle.Bold), ForeColor = UIHelper.WarmText };
            lblDestBalance = new Label { Text = "موجودی: 0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UIHelper.PersianFont(9f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            var pnlSrc = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            pnlSrc.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            pnlSrc.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            pnlSrc.Controls.Add(cmbSourceAccount, 0, 0); pnlSrc.Controls.Add(lblSourceBalance, 1, 0);

            var pnlDst = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            pnlDst.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            pnlDst.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            pnlDst.Controls.Add(cmbDestAccount, 0, 0); pnlDst.Controls.Add(lblDestBalance, 1, 0);

            UIHelper.AddField(formLayout, "از حساب:", pnlSrc, 0, 0, 1);
            UIHelper.AddField(formLayout, "به حساب:", pnlDst, 0, 2, 3);

            txtAmount = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f), TextAlign = HorizontalAlignment.Center };
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
            txtDate = new TextBox { Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(11f) };
            DateHelper.ApplyDateMask(txtDate);
            txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);

            UIHelper.AddField(formLayout, "مبلغ (ریال):", txtAmount, 1, 0, 1);
            UIHelper.AddField(formLayout, "تاریخ:", txtDate, 1, 2, 3);

            txtDescription = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f) };
            UIHelper.AddField(formLayout, "توضیحات:", txtDescription, 2, 0, 1);
            formLayout.SetColumnSpan(txtDescription, 3);

            inputContainer.Controls.Add(formLayout);
            mainLayout.Controls.Add(inputContainer, 0, 1);

            var pnlGridToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = UIHelper.WarmPanelBackground,
                Padding = new Padding(0, 5, 10, 5)
            };

            lblGridTitle = new Label
            {
                Text = "📋 تاریخچه شارژ/تسویه‌های این تنخواه:",
                TextAlign = ContentAlignment.MiddleRight,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                AutoSize = true,
                Margin = new Padding(0, 8, 10, 0)
            };

            btnEditRow = UIHelper.CreateButton("✏️ ویرایش", UIHelper.SectionBlue, 100, 32);
            btnEditRow.Enabled = false;
            btnEditRow.Margin = new Padding(5, 0, 5, 0);
            btnEditRow.Click += BtnEditRow_Click;

            btnDeleteRow = UIHelper.CreateButton("🗑️ حذف", UIHelper.ActionRed, 100, 32);
            btnDeleteRow.Enabled = false;
            btnDeleteRow.Margin = new Padding(5, 0, 0, 0);
            btnDeleteRow.Click += async (s, e) => await BtnDeleteRow_ClickAsync();

            pnlGridToolbar.Controls.Add(lblGridTitle);
            pnlGridToolbar.Controls.Add(btnEditRow);
            pnlGridToolbar.Controls.Add(btnDeleteRow);

            mainLayout.Controls.Add(pnlGridToolbar, 0, 2);

            dgvRecentTransactions = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                RightToLeft = RightToLeft.Yes
            };
            dgvRecentTransactions.ApplyModernStyle();
            dgvRecentTransactions.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvRecentTransactions.SelectionChanged += (s, e) => UpdateButtonsState();

            // ✅ FIX ساختاری: ستون «توضیحات» جدا از «نوع» و «طرف حساب»
            //   قبلاً توضیحات با پیشوند "شارژ تنخواه از X - " ترکیب می‌شد و
            //   هنگام ویرایش، توضیحات اصلی از بین می‌رفت.
            dgvRecentTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            dgvRecentTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "تاریخ", FillWeight = 12 });
            dgvRecentTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Direction", HeaderText = "نوع", FillWeight = 8 });
            dgvRecentTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "توضیحات", FillWeight = 30 });
            dgvRecentTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Counterpart", HeaderText = "طرف حساب", FillWeight = 20 });
            dgvRecentTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 15 });
            dgvRecentTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "وضعیت", FillWeight = 10 });

            dgvRecentTransactions.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var row = dgvRecentTransactions.Rows[e.RowIndex];
                string direction = row.Cells["Direction"].Value?.ToString() ?? "";

                if (direction == "دریافتی")
                {
                    e.CellStyle.BackColor = Color.FromArgb(240, 253, 244);
                    e.CellStyle.SelectionBackColor = Color.FromArgb(220, 252, 231);
                }
                else
                {
                    e.CellStyle.BackColor = Color.FromArgb(254, 242, 242);
                    e.CellStyle.SelectionBackColor = Color.FromArgb(254, 226, 226);
                }
            };

            mainLayout.Controls.Add(dgvRecentTransactions, 0, 3);

            var pnlBottom = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UIHelper.WarmPanelBackground
            };

            var flowBtns = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.None,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(10)
            };

            btnSave = UIHelper.CreateButton(
                _mode == TankhahTransferMode.BankToTankhah ? "💾 ثبت شارژ تنخواه" : "💾 ثبت تسویه تنخواه",
                UIHelper.ActionGreen, 180, 45);
            btnSave.Click += async (s, e) => await SaveSafeAsync();

            btnDownloadTemplate = UIHelper.CreateButton("📥 دانلود قالب اکسل", UIHelper.WarmSecondary, 140, 45);
            btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

            btnImportExcel = UIHelper.CreateButton("📤 ایمپورت از اکسل", UIHelper.SectionTeal, 140, 45);
            btnImportExcel.Click += BtnImportExcel_Click;

            btnCancel = UIHelper.CreateButton("انصراف", UIHelper.SectionGray, 100, 45);
            btnCancel.Click += (s, e) => Close();

            btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی", UIHelper.SectionGray, 120, 45);
            btnRefresh.Click += async (s, e) => await LoadAccountsAsync(null);

            flowBtns.Controls.Add(btnSave);
            flowBtns.Controls.Add(btnDownloadTemplate);
            flowBtns.Controls.Add(btnImportExcel);
            flowBtns.Controls.Add(btnCancel);
            flowBtns.Controls.Add(btnRefresh);

            pnlBottom.Controls.Add(flowBtns);
            pnlBottom.Resize += (s, e) =>
            {
                flowBtns.Left = (pnlBottom.Width - flowBtns.Width) / 2;
                flowBtns.Top = (pnlBottom.Height - flowBtns.Height) / 2;
            };

            mainLayout.Controls.Add(pnlBottom, 0, 4);
            this.Controls.Add(mainLayout);
        }

        private void UpdateButtonsState()
        {
            bool hasSelection = dgvRecentTransactions.CurrentRow != null;
            btnEditRow.Enabled = hasSelection;
            btnDeleteRow.Enabled = hasSelection;
        }

        private async Task LoadAccountsAsync(int? preselectedTankhahId)
        {
            if (IsDisposed) return;

            try
            {
                _isInitializing = true;
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    _allAccounts = await _bankRepo.GetActiveAsync();
                    if (IsDisposed) return;

                    var bankAccounts = _allAccounts.Where(a => !a.IsCashDesk).ToList();
                    var tankhahs = _allAccounts.Where(a => a.IsCashDesk).ToList();

                    if (bankAccounts.Count == 0) throw new InvalidOperationException("هیچ حساب بانکی فعالی وجود ندارد.");
                    if (tankhahs.Count == 0) throw new InvalidOperationException("هیچ تنخواه فعالی وجود ندارد.");

                    cmbSourceAccount.SelectedIndexChanged -= Cmb_SelectedIndexChanged;
                    cmbDestAccount.SelectedIndexChanged -= Cmb_SelectedIndexChanged;

                    if (_mode == TankhahTransferMode.BankToTankhah)
                    {
                        cmbSourceAccount.DataSource = bankAccounts.ToList();
                        cmbDestAccount.DataSource = tankhahs.ToList();

                        if (preselectedTankhahId.HasValue)
                        {
                            var target = tankhahs.FirstOrDefault(t => t.Id == preselectedTankhahId.Value);
                            if (target != null) cmbDestAccount.SelectedItem = target;
                        }
                    }
                    else
                    {
                        cmbSourceAccount.DataSource = tankhahs.ToList();
                        cmbDestAccount.DataSource = bankAccounts.ToList();

                        if (preselectedTankhahId.HasValue)
                        {
                            var target = tankhahs.FirstOrDefault(t => t.Id == preselectedTankhahId.Value);
                            if (target != null) cmbSourceAccount.SelectedItem = target;
                        }
                    }

                    cmbSourceAccount.SelectedIndexChanged += Cmb_SelectedIndexChanged;
                    cmbDestAccount.SelectedIndexChanged += Cmb_SelectedIndexChanged;
                }, "در حال بارگذاری حساب‌ها...");

                if (!success && !IsDisposed)
                {
                    this.BeginInvoke(new Action(() => this.Close()));
                    return;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "TankhahTransferForm.LoadAccountsAsync");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
            finally
            {
                _isInitializing = false;
                if (!IsDisposed)
                {
                    UpdateBalanceLabels();
                    await LoadRecentTransactionsAsync();
                }
            }
        }

        private async void Cmb_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isInitializing) return;
            await OnAccountChangedAsync();
        }

        private void UpdateBalanceLabels()
        {
            if (IsDisposed) return;

            var src = cmbSourceAccount.SelectedItem as CompanyBankAccount;
            var dst = cmbDestAccount.SelectedItem as CompanyBankAccount;

            if (src != null)
            {
                lblSourceBalance.Text = $"موجودی: {NumberHelper.FormatThousands(src.CurrentBalance, usePersianDigits: true)}";
                lblSourceBalance.ForeColor = src.CurrentBalance >= 0 ? UIHelper.ActionGreen : UIHelper.ActionRed;
            }
            else { lblSourceBalance.Text = "موجودی: ---"; lblSourceBalance.ForeColor = UIHelper.WarmText; }

            if (dst != null)
            {
                lblDestBalance.Text = $"موجودی: {NumberHelper.FormatThousands(dst.CurrentBalance, usePersianDigits: true)}";
                lblDestBalance.ForeColor = dst.CurrentBalance >= 0 ? UIHelper.ActionGreen : UIHelper.ActionRed;
            }
            else { lblDestBalance.Text = "موجودی: ---"; lblDestBalance.ForeColor = UIHelper.WarmText; }
        }

        private async Task OnAccountChangedAsync()
        {
            if (IsDisposed || _isInitializing) return;

            if (_editingTransferId.HasValue)
            {
                ResetForm();
            }

            UpdateBalanceLabels();
            await LoadRecentTransactionsAsync();
        }

        // ✅ FIX: ستون Description حالا فقط توضیحات کاربر را نشان می‌دهد (نه ترکیب‌شده)
        private async Task LoadRecentTransactionsAsync()
        {
            if (IsDisposed) return;

            try
            {
                int? tankhahId = null;
                if (_mode == TankhahTransferMode.BankToTankhah)
                    tankhahId = (cmbDestAccount.SelectedItem as CompanyBankAccount)?.Id;
                else
                    tankhahId = (cmbSourceAccount.SelectedItem as CompanyBankAccount)?.Id;

                if (!tankhahId.HasValue)
                {
                    dgvRecentTransactions.Rows.Clear();
                    return;
                }

                string currentTankhahName = _allAccounts.FirstOrDefault(a => a.Id == tankhahId.Value)?.Title ?? "---";
                lblGridTitle.Text = $"📋 تاریخچه شارژ/تسویه‌های تنخواه [{currentTankhahName}]:";

                string sql = @"
                    SELECT 
                        at.Id, 
                        at.TransferDate, 
                        at.Amount, 
                        at.Description, 
                        at.SourceAccountId, 
                        at.DestinationAccountId
                    FROM AccountTransfers at
                    WHERE at.DestinationAccountId = @Id OR at.SourceAccountId = @Id
                    ORDER BY at.TransferDate DESC, at.Id DESC";

                DataTable? dt = null;
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    dt = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, sql, new SqlParameter("@Id", tankhahId.Value)));
                }, "در حال دریافت تاریخچه...");

                if (!success || dt == null || IsDisposed) return;

                dgvRecentTransactions.Rows.Clear();
                foreach (DataRow row in dt.Rows)
                {
                    bool isIncoming = Convert.ToInt32(row["DestinationAccountId"]) == tankhahId.Value;
                    decimal amount = Convert.ToDecimal(row["Amount"]);
                    string desc = row["Description"].ToString() ?? "";

                    string srcTitle = _allAccounts.FirstOrDefault(a => a.Id == Convert.ToInt32(row["SourceAccountId"]))?.Title ?? "حساب مبدا";
                    string destTitle = _allAccounts.FirstOrDefault(a => a.Id == Convert.ToInt32(row["DestinationAccountId"]))?.Title ?? "حساب مقصد";

                    // ✅ توضیحات خالص (بدون پیشوند) برای امکان ویرایش صحیح
                    dgvRecentTransactions.Rows.Add(
                        row["Id"],
                        DateHelper.ToPersianDate(Convert.ToDateTime(row["TransferDate"])),
                        isIncoming ? "دریافتی" : "پرداختی",
                        string.IsNullOrWhiteSpace(desc) ? "---" : desc,
                        isIncoming ? srcTitle : destTitle,
                        NumberHelper.FormatThousands(amount, usePersianDigits: true),
                        "ثبت شده"
                    );
                }

                UpdateButtonsState();
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "TankhahTransferForm.LoadRecentTransactions");
                if (!IsDisposed)
                {
                    dgvRecentTransactions.Rows.Clear();
                    ModernMessageBox.ShowError("خطا در بارگذاری لیست:\n" + ex.Message);
                }
            }
        }

        // ✅ FIX بحرانی: توضیحات اصلی حالا از ستون Description بارگذاری می‌شود
        //   (قبلاً خالی می‌شد و با Save، مقدار null در دیتابیس نوشته می‌شد)
        private void BtnEditRow_Click(object? sender, EventArgs e)
        {
            if (dgvRecentTransactions.CurrentRow == null) return;

            var row = dgvRecentTransactions.CurrentRow;

            _editingTransferId = Convert.ToInt32(row.Cells["Id"].Value);
            _editingOriginalDescription = row.Cells["Description"].Value?.ToString();

            txtAmount.Text = row.Cells["Amount"].Value?.ToString() ?? "";
            txtDate.Text = row.Cells["Date"].Value?.ToString() ?? "";

            // ✅ بارگذاری توضیحات اصلی
            txtDescription.Text = _editingOriginalDescription == "---" ? "" : (_editingOriginalDescription ?? "");
            txtDescription.PlaceholderText = "";

            btnSave.Text = "💾 ذخیره تغییرات";
            btnSave.BackColor = UIHelper.SectionBlue;

            cmbSourceAccount.Enabled = false;
            cmbDestAccount.Enabled = false;

            ToastHelper.Show("اطلاعات برای ویرایش بارگذاری شد.", true);
        }

        private async Task BtnDeleteRow_ClickAsync()
        {
            if (dgvRecentTransactions.CurrentRow == null) return;

            var row = dgvRecentTransactions.CurrentRow;
            int id = Convert.ToInt32(row.Cells["Id"].Value);

            if (!ModernMessageBox.AskConfirm("⚠️ هشدار: آیا از حذف این انتقال مطمئن هستید؟\n\nبا حذف این رکورد، موجودی حساب بانکی و تنخواه به حالت قبل برمی‌گردد.", "حذف انتقال"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _transferRepo.DeleteAsync(id);
            }, "در حال حذف و اصلاح موجودی...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("انتقال حذف شد.", true);
                await LoadAccountsAsync(null);
            }
        }

        private void ResetForm()
        {
            _editingTransferId = null;
            _editingOriginalDescription = null;

            txtAmount.Clear();
            txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);
            txtDescription.Clear();
            txtDescription.PlaceholderText = _mode == TankhahTransferMode.BankToTankhah ? "مثلاً: شارژ هفتگی..." : "مثلاً: برگشت باقی‌مانده...";

            btnSave.Text = _mode == TankhahTransferMode.BankToTankhah ? "💾 ثبت شارژ تنخواه" : "💾 ثبت تسویه تنخواه";
            btnSave.BackColor = UIHelper.ActionGreen;

            cmbSourceAccount.Enabled = true;
            cmbDestAccount.Enabled = true;
        }

        // ✅ FIX: در حالت ویرایش، توضیحات کاربر حفظ می‌شود
        //   - اگر کاربر آن را تغییر نداد → همان مقدار ذخیره می‌شود
        //   - اگر آن را پاک کرد → null ذخیره می‌شود (تصمیم کاربر)
        //   - اگر در حالت جدید خالی بود → مقدار پیش‌فرض استفاده می‌شود
        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            btnSave.Enabled = false;
            try
            {
                var srcAcc = cmbSourceAccount.SelectedItem as CompanyBankAccount;
                var dstAcc = cmbDestAccount.SelectedItem as CompanyBankAccount;

                if (srcAcc == null || dstAcc == null)
                {
                    ModernMessageBox.ShowWarning("لطفاً حساب مبدأ و مقصد را انتخاب کنید.");
                    return;
                }

                decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
                if (amount <= 0)
                {
                    ModernMessageBox.ShowWarning("مبلغ انتقال باید بزرگتر از صفر باشد.");
                    txtAmount.Focus();
                    return;
                }

                DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
                if (date == null)
                {
                    ModernMessageBox.ShowWarning("تاریخ وارد شده معتبر نیست.");
                    txtDate.Focus();
                    return;
                }

                // ✅ منطق جدید توضیحات:
                string finalDescription;
                string userText = txtDescription.Text.Trim();

                if (!string.IsNullOrWhiteSpace(userText))
                {
                    // کاربر متنی وارد کرده → همان را ذخیره کن
                    finalDescription = userText;
                }
                else if (_editingTransferId.HasValue)
                {
                    // در حالت ویرایش و کاربر پاک کرده → null (تصمیم آگاهانه کاربر)
                    finalDescription = string.Empty;
                }
                else
                {
                    // ثبت جدید بدون توضیح → پیش‌فرض
                    finalDescription = _mode == TankhahTransferMode.BankToTankhah ? "شارژ تنخواه" : "تسویه تنخواه";
                }

                var transfer = new AccountTransfer
                {
                    Id = _editingTransferId ?? 0,
                    SourceAccountId = srcAcc.Id,
                    DestinationAccountId = dstAcc.Id,
                    Amount = amount,
                    TransferFee = 0,
                    TransferDate = date.Value,
                    Description = string.IsNullOrWhiteSpace(finalDescription) ? null : finalDescription,
                    TransferType = _mode == TankhahTransferMode.BankToTankhah ? "BankToTankhah" : "TankhahToBank"
                };

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    if (_editingTransferId.HasValue)
                    {
                        await _transferRepo.UpdateAsync(transfer);
                    }
                    else
                    {
                        await _transferRepo.AddAsync(transfer);
                    }
                }, _editingTransferId.HasValue ? "در حال اعمال تغییرات..." : "در حال ثبت انتقال...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show(_editingTransferId.HasValue ? "تغییرات ذخیره شد." : "با موفقیت ثبت شد.", true);
                    ResetForm();
                    await LoadAccountsAsync(null);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "TankhahTransferForm.SaveSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
            }
            finally
            {
                if (!IsDisposed) btnSave.Enabled = true;
                try { _actionLock.Release(); } catch { }
            }
        }

        private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = "Tankhah_Transfer_Template.xlsx",
                Title = "ذخیره قالب اکسل شارژ/تسویه تنخواه"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("انتقالات تنخواه");
                ws.RightToLeft = true;

                string[] headers = {
                    "عنوان حساب مبدا (دقیقاً مطابق سیستم)",
                    "عنوان حساب مقصد (دقیقاً مطابق سیستم)",
                    "مبلغ (ریال)",
                    "تاریخ (شمسی 1403/01/01)",
                    "توضیحات (اختیاری)"
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

                ws.Cell(2, 1).Value = _mode == TankhahTransferMode.BankToTankhah ? "پشتیبان" : "تنخواه 1 سرایی";
                ws.Cell(2, 2).Value = _mode == TankhahTransferMode.BankToTankhah ? "تنخواه 1 سرایی" : "پشتیبان";
                ws.Cell(2, 3).Value = 10000000;
                ws.Cell(2, 4).Value = DateHelper.ToPersianDate(DateTime.Now);
                ws.Cell(2, 5).Value = "شارژ/تسویه گروهی از طریق اکسل";

                ws.Columns().AdjustToContents();
                ws.Row(1).Height = 40;
                workbook.SaveAs(sfd.FileName);

                ToastHelper.Show("قالب اکسل با موفقیت ساخته شد.", true);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "TankhahTransferForm.DownloadTemplate");
                ModernMessageBox.ShowError("خطا در ساخت قالب:\n" + ex.Message);
            }
        }

        private async void BtnImportExcel_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل انتقالات تنخواه" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                int successCount = 0;
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
                            string srcTitle = row.Cell(1).GetFormattedString().Trim();
                            string destTitle = row.Cell(2).GetFormattedString().Trim();
                            string amountStr = row.Cell(3).GetFormattedString().Trim();
                            string dateStr = row.Cell(4).GetFormattedString().Trim();
                            string desc = row.Cell(5).GetFormattedString().Trim();

                            if (string.IsNullOrWhiteSpace(srcTitle) && string.IsNullOrWhiteSpace(destTitle)) continue;

                            var srcAcc = _allAccounts.FirstOrDefault(a =>
                                a.Title.Trim() == srcTitle ||
                                a.DisplayTitle.Trim() == srcTitle);

                            if (srcAcc == null)
                                srcAcc = _allAccounts.FirstOrDefault(a =>
                                    a.Title.Contains(srcTitle) ||
                                    a.DisplayTitle.Contains(srcTitle));

                            if (srcAcc == null) throw new Exception($"حساب مبدا «{srcTitle}» یافت نشد.");

                            var destAcc = _allAccounts.FirstOrDefault(a =>
                                a.Title.Trim() == destTitle ||
                                a.DisplayTitle.Trim() == destTitle);

                            if (destAcc == null)
                                destAcc = _allAccounts.FirstOrDefault(a =>
                                    a.Title.Contains(destTitle) ||
                                    a.DisplayTitle.Contains(destTitle));

                            if (destAcc == null) throw new Exception($"حساب مقصد «{destTitle}» یافت نشد.");

                            if (srcAcc.Id == destAcc.Id) throw new Exception("حساب مبدا و مقصد نمی‌توانند یکسان باشند.");

                            decimal amount = NumberHelper.ParseFormatted(amountStr);
                            if (amount <= 0) throw new Exception("مبلغ نامعتبر است.");

                            DateTime? date = DateHelper.ParsePersianDate(dateStr);
                            if (date == null && DateTime.TryParse(dateStr, out DateTime gDate)) date = gDate;
                            if (date == null) throw new Exception($"تاریخ «{dateStr}» معتبر نیست.");

                            var transfer = new AccountTransfer
                            {
                                SourceAccountId = srcAcc.Id,
                                DestinationAccountId = destAcc.Id,
                                Amount = amount,
                                TransferFee = 0,
                                TransferDate = date.Value,
                                Description = string.IsNullOrWhiteSpace(desc) ? "ایمپورت از اکسل" : desc,
                                TransferType = _mode == TankhahTransferMode.BankToTankhah ? "BankToTankhah" : "TankhahToBank"
                            };

                            await _transferRepo.AddAsync(transfer);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"سطر {rowNum}: {ex.Message}");
                        }
                    }
                }, "در حال ایمپورت از اکسل...");

                if (success && !IsDisposed)
                {
                    string msg = $"✅ تعداد {successCount} انتقال با موفقیت ثبت شد.";
                    if (errors.Count > 0)
                    {
                        msg += $"\n\n⚠️ {errors.Count} خطا:\n" + string.Join("\n", errors.Take(10));
                        if (errors.Count > 10) msg += "\n... و سایر موارد";
                        ModernMessageBox.ShowWarning(msg, "نتیجه ایمپورت");
                    }
                    else ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");

                    await LoadAccountsAsync(null);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "TankhahTransferForm.ImportExcel");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ایمپورت:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}