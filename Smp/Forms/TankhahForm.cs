using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace SMP.Forms
{
    public class TankhahForm : Form
    {
        private readonly string _connectionString;
        private readonly BankAccountRepository _bankRepo;
        private readonly CashTransactionRepository _transRepo;
        private readonly CashCheckRepository _checkRepo;
        private readonly AccountTransferRepository _transferRepo;
        private readonly LookupRepository _lookupRepo;

        private ComboBox cmbTankhah = null!;
        private DataGridView dgvTankhahs = null!;

        private Label lblOwnerTotalBalance = null!, lblOwnerTotalSpent = null!;

        private Button btnAddTankhah = null!, btnRefresh = null!, btnEditTankhah = null!, btnDeleteTankhah = null!;
        private Button btnViewDetails = null!, btnRepairBalances = null!;

        private List<CompanyBankAccount> _allTankhahs = new();
        private CompanyBankAccount? _activeTankhah = null;

        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private bool _isInitializing = true;

        private class TankhahOwnerItem
        {
            public string OwnerName { get; set; } = "";
            public List<CompanyBankAccount> Accounts { get; set; } = new();
            public string DisplayTitle => $"تنخواه شخص: {OwnerName}";
        }

        public TankhahForm(string connectionString)
        {
            _connectionString = connectionString;
            _bankRepo = new BankAccountRepository(connectionString);
            _transRepo = new CashTransactionRepository(connectionString);
            _checkRepo = new CashCheckRepository(connectionString);
            _transferRepo = new AccountTransferRepository(connectionString);
            _lookupRepo = new LookupRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("مدیریت تنخواه‌ها و لیست‌های هزینه", new Size(1350, 700));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this);
            Shown += async (s, e) => await LoadTankhahsAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _loadLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var pnlTop = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(10, 10, 10, 10) };
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            var pnlSelect = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            pnlSelect.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            pnlSelect.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var lblSelectTankhah = new Label { Text = "انتخاب شخص:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            cmbTankhah = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(12f, FontStyle.Bold), BackColor = UIHelper.WarmWarningBackground, Margin = new Padding(5, 15, 5, 15) };
            cmbTankhah.SelectedIndexChanged += async (s, e) => { if (!_isInitializing) await OnOwnerSelectedAsync(); };

            pnlSelect.Controls.Add(lblSelectTankhah, 0, 0);
            pnlSelect.Controls.Add(cmbTankhah, 1, 0);
            pnlTop.Controls.Add(pnlSelect, 0, 0);

            var pnlBalance = CreateKpiCard("💰 مجموع موجودی شخص", "0", UIHelper.ActionGreen);
            lblOwnerTotalBalance = (Label)pnlBalance.Controls["lblValue"]!;
            pnlTop.Controls.Add(pnlBalance, 1, 0);

            var pnlSpent = CreateKpiCard("📤 مجموع پرداختی شخص", "0", UIHelper.ActionRed);
            lblOwnerTotalSpent = (Label)pnlSpent.Controls["lblValue"]!;
            pnlTop.Controls.Add(pnlSpent, 2, 0);

            var pnlToolbarTop = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(5, 8, 5, 5), WrapContents = true };

            btnAddTankhah = UIHelper.CreateButton("➕ ثبت لیست جدید", UIHelper.SectionBlue, 180, 40);
            btnAddTankhah.Click += BtnNewTankhah_Click;

            btnViewDetails = UIHelper.CreateButton("🔍 مشاهده جزئیات لیست", UIHelper.ActionGreen, 180, 40);
            btnViewDetails.Enabled = false;
            btnViewDetails.Click += BtnViewDetails_Click;

            btnRepairBalances = UIHelper.CreateButton("🔧 اصلاح موجودی‌ها", UIHelper.ActionAmber, 150, 40);
            btnRepairBalances.Click += async (s, e) => await RepairBalancesAsync();

            btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی", UIHelper.SectionGray, 120, 40);
            btnRefresh.Click += async (s, e) => await LoadTankhahsAsync();

            btnEditTankhah = UIHelper.CreateButton("✏️ ویرایش لیست", UIHelper.WarmSecondary, 150, 40);
            btnEditTankhah.Enabled = false;
            btnEditTankhah.Click += BtnEditTankhah_Click;

            btnDeleteTankhah = UIHelper.CreateButton("🗑️ حذف لیست", UIHelper.WarmDanger, 130, 40);
            btnDeleteTankhah.Enabled = false;
            btnDeleteTankhah.Click += BtnDeleteTankhah_Click;

            pnlToolbarTop.Controls.Add(btnAddTankhah);
            pnlToolbarTop.Controls.Add(btnViewDetails);
            pnlToolbarTop.Controls.Add(btnRepairBalances);
            pnlToolbarTop.Controls.Add(btnEditTankhah);
            pnlToolbarTop.Controls.Add(btnDeleteTankhah);
            pnlToolbarTop.Controls.Add(btnRefresh);

            dgvTankhahs = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = Color.White, RightToLeft = RightToLeft.Yes, AutoGenerateColumns = false };
            dgvTankhahs.ApplyModernStyle();
            dgvTankhahs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvTankhahs.SelectionChanged += DgvTankhahs_SelectionChanged;
            dgvTankhahs.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnViewDetails_Click(null, EventArgs.Empty); };

            dgvTankhahs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvTankhahs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", DataPropertyName = "Title", HeaderText = "عنوان لیست", FillWeight = 30 });
            dgvTankhahs.Columns.Add(new DataGridViewTextBoxColumn { Name = "InitialBalance", DataPropertyName = "InitialBalance", HeaderText = "موجودی اولیه", FillWeight = 18 });
            dgvTankhahs.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentBalance", DataPropertyName = "CurrentBalance", HeaderText = "موجودی واقعی", FillWeight = 18 });
            dgvTankhahs.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalSpent", DataPropertyName = "TotalSpent", HeaderText = "مجموع هزینه‌ها", FillWeight = 18 });
            dgvTankhahs.Columns.Add(new DataGridViewTextBoxColumn { Name = "CreatedAt", DataPropertyName = "CreatedAt", HeaderText = "تاریخ ایجاد", FillWeight = 16 });
            dgvTankhahs.CellFormatting += DgvTankhahs_CellFormatting;

            mainLayout.Controls.Add(pnlTop, 0, 0);
            mainLayout.Controls.Add(pnlToolbarTop, 0, 1);
            mainLayout.Controls.Add(dgvTankhahs, 0, 2);
            this.Controls.Add(mainLayout);
        }

        private Panel CreateKpiCard(string title, string value, Color color)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5) };
            card.Paint += (s, e) => ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid);

            var lblTitle = new Label { Text = title, Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(9f, FontStyle.Bold), ForeColor = color };
            var lblValue = new Label { Name = "lblValue", Text = value, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(14f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            card.Controls.Add(lblValue);
            card.Controls.Add(lblTitle);
            return card;
        }

        private async Task LoadTankhahsAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;

            bool success = false;
            try
            {
                _isInitializing = true;
                success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var allAccounts = await _bankRepo.GetAllAsync();
                    _allTankhahs = allAccounts.Where(a => a.IsCashDesk).OrderBy(a => a.Id).ToList();
                    if (IsDisposed) return;

                    var groupedTankhahs = _allTankhahs.GroupBy(a =>
                    {
                        string clean = Regex.Replace(a.Title, @"[0-9]", "").Replace("تنخواه", "").Replace("لیست", "").Replace("شماره", "").Trim();
                        return StringHelper.NormalizePersianText(clean);
                    }).Select(g => new TankhahOwnerItem { OwnerName = g.Key, Accounts = g.OrderBy(a => a.Id).ToList() }).OrderBy(x => x.OwnerName).ToList();

                    cmbTankhah.DataSource = null;
                    cmbTankhah.DisplayMember = "DisplayTitle";
                    cmbTankhah.DataSource = groupedTankhahs;

                    if (groupedTankhahs.Count > 0) cmbTankhah.SelectedIndex = 0;
                    else { dgvTankhahs.DataSource = null; ClearUI(); }
                }, "در حال بارگذاری اطلاعات...");

                if (!success && !IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری لیست تنخواه‌ها.");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "TankhahForm.LoadTankhahsAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
            }
            finally
            {
                _isInitializing = false;
                try { _loadLock.Release(); } catch { }
            }

            if (success && !IsDisposed && cmbTankhah.SelectedItem != null)
            {
                try { await OnOwnerSelectedAsync(); }
                catch (Exception ex) { LogHelper.LogException(ex, "TankhahForm.LoadTankhahsAsync - OnOwnerSelectedAsync"); }
            }
        }

        // ✅ FIX بحرانی: فرمول موجودی کامل (شامل دریافتی‌ها)
        // - قبلاً فقط «پرداختی‌ها» کم می‌شد و هرگونه «دریافتی» (نقدی/چک) نادیده گرفته می‌شد
        // - حالا: موجودی = اولیه + شارژ + دریافتی نقدی/چک − تسویه − پرداختی نقدی/چک
        private async Task OnOwnerSelectedAsync()
        {
            if (IsDisposed || cmbTankhah.SelectedItem is not TankhahOwnerItem selectedOwner) return;

            try
            {
                _isInitializing = true;
                dgvTankhahs.DataSource = null;
                _activeTankhah = null;
                btnViewDetails.Enabled = false;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var displayTankhahs = new List<object>();
                    decimal totalBalance = 0;
                    decimal totalSpent = 0;

                    foreach (var acc in selectedOwner.Accounts)
                    {
                        var accTrans = await _transRepo.GetByAccountIdAsync(acc.Id);
                        var accChecks = await _checkRepo.GetByAccountIdAsync(acc.Id);
                        var accTransfers = await _transferRepo.GetByAccountIdAsync(acc.Id);

                        // ─── محاسبه صحیح گردش ───
                        // تراکنش‌های نقدی/حواله (بدون انتقال بین حساب‌ها)
                        var cashIn = accTrans
                            .Where(t => t.Direction == DirectionTypes.Incoming
                                     && !string.Equals(t.Category?.Trim(), "انتقال بین حساب‌ها", StringComparison.Ordinal))
                            .Sum(t => t.Amount);

                        var cashOut = accTrans
                            .Where(t => t.Direction == DirectionTypes.Outgoing
                                     && !string.Equals(t.Category?.Trim(), "انتقال بین حساب‌ها", StringComparison.Ordinal))
                            .Sum(t => t.Amount);

                        // چک‌ها (بدون باطل/برگشتی)
                        var checkIn = accChecks
                            .Where(c => c.CheckType == DirectionTypes.Incoming
                                     && c.Status != "باطل شده" && c.Status != "برگشتی")
                            .Sum(c => c.Amount);

                        var checkOut = accChecks
                            .Where(c => c.CheckType == DirectionTypes.Outgoing
                                     && c.Status != "باطل شده" && c.Status != "برگشتی")
                            .Sum(c => c.Amount);

                        // انتقالات بین حساب‌ها
                        var transferIn = accTransfers
                            .Where(t => t.DestinationAccountId == acc.Id)
                            .Sum(t => t.Amount);

                        var transferOut = accTransfers
                            .Where(t => t.SourceAccountId == acc.Id)
                            .Sum(t => t.Amount);

                        // جمع هزینه‌ها (فقط خروجی نقدی + چک)
                        decimal totalSpentForThisAcc = cashOut + checkOut;

                        // موجودی واقعی
                        decimal trueBalance = acc.InitialBalance
                                            + transferIn + cashIn + checkIn
                                            - transferOut - cashOut - checkOut;

                        totalBalance += trueBalance;
                        totalSpent += totalSpentForThisAcc;

                        string number = GetTankhahNumber(acc.Id);

                        displayTankhahs.Add(new
                        {
                            acc.Id,
                            Title = $"لیست {number}",
                            acc.InitialBalance,
                            CurrentBalance = trueBalance,
                            TotalSpent = totalSpentForThisAcc,
                            acc.CreatedAt
                        });
                    }

                    if (IsDisposed) return;

                    dgvTankhahs.DataSource = displayTankhahs.OrderByDescending(x => ((dynamic)x).Id).ToList();

                    lblOwnerTotalBalance.Text = NumberHelper.FormatThousands(totalBalance, usePersianDigits: true);
                    lblOwnerTotalBalance.ForeColor = totalBalance >= 0 ? UIHelper.ActionGreen : UIHelper.ActionRed;

                    lblOwnerTotalSpent.Text = NumberHelper.FormatThousands(totalSpent, usePersianDigits: true);
                    lblOwnerTotalSpent.ForeColor = UIHelper.ActionRed;
                }, "در حال بارگذاری لیست‌ها...");

                _isInitializing = false;

                if (success && !IsDisposed && dgvTankhahs.Rows.Count > 0)
                {
                    dgvTankhahs.ClearSelection();
                    dgvTankhahs.Rows[0].Selected = true;
                    dgvTankhahs.CurrentCell = dgvTankhahs.Rows[0].Cells["Title"];
                    DgvTankhahs_SelectionChanged(null, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "TankhahForm.OnOwnerSelectedAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        // ✅ FIX: فیلتر Category = «انتقال بین حساب‌ها» اضافه شد (هم‌خوان با GetTrueBalanceAsync)
        private async Task RepairBalancesAsync()
        {
            if (!ModernMessageBox.AskConfirm("این عملیات موجودی تمام حساب‌ها و تنخواه‌ها را بر اساس گردش واقعی (تراکنش‌ها، چک‌ها و انتقالات) بازسازی کرده و در دیتابیس ذخیره می‌کند. ادامه می‌دهید؟", "اصلاح موجودی‌ها")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var tx = conn.BeginTransaction();
                try
                {
                    var accounts = await _bankRepo.GetAllAsync();
                    foreach (var acc in accounts)
                    {
                        decimal inflow = 0, outflow = 0;

                        using (var cmd = new SqlCommand("SELECT ISNULL(SUM(Amount), 0) FROM AccountTransfers WHERE DestinationAccountId = @Id", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Id", acc.Id);
                            inflow += Convert.ToDecimal(await cmd.ExecuteScalarAsync());
                        }

                        using (var cmd = new SqlCommand("SELECT ISNULL(SUM(Amount), 0) FROM AccountTransfers WHERE SourceAccountId = @Id", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Id", acc.Id);
                            outflow += Convert.ToDecimal(await cmd.ExecuteScalarAsync());
                        }

                        // ✅ فیلتر Category = «انتقال بین حساب‌ها» برای جلوگیری از دوباره‌شماری
                        using (var cmd = new SqlCommand(@"
                            SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions 
                            WHERE AccountId = @Id AND Direction = N'دریافتی'
                              AND LTRIM(RTRIM(ISNULL(Category, ''))) <> N'انتقال بین حساب‌ها'", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Id", acc.Id);
                            inflow += Convert.ToDecimal(await cmd.ExecuteScalarAsync());
                        }

                        using (var cmd = new SqlCommand(@"
                            SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions 
                            WHERE AccountId = @Id AND Direction = N'پرداختی'
                              AND LTRIM(RTRIM(ISNULL(Category, ''))) <> N'انتقال بین حساب‌ها'", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Id", acc.Id);
                            outflow += Convert.ToDecimal(await cmd.ExecuteScalarAsync());
                        }

                        using (var cmd = new SqlCommand("SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE AccountId = @Id AND CheckType = N'دریافتی' AND Status NOT IN (N'باطل شده', N'برگشتی')", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Id", acc.Id);
                            inflow += Convert.ToDecimal(await cmd.ExecuteScalarAsync());
                        }

                        using (var cmd = new SqlCommand("SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE AccountId = @Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی')", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Id", acc.Id);
                            outflow += Convert.ToDecimal(await cmd.ExecuteScalarAsync());
                        }

                        decimal trueBalance = acc.InitialBalance + inflow - outflow;

                        using var updateCmd = new SqlCommand("UPDATE CompanyBankAccounts SET CurrentBalance = @Bal WHERE Id = @Id", conn, tx);
                        updateCmd.Parameters.AddWithValue("@Bal", trueBalance);
                        updateCmd.Parameters.AddWithValue("@Id", acc.Id);
                        await updateCmd.ExecuteNonQueryAsync();
                    }

                    await tx.CommitAsync();
                }
                catch { await tx.RollbackAsync(); throw; }
            }, "در حال بازسازی موجودی‌ها...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("موجودی همه حساب‌ها و تنخواه‌ها بر اساس گردش واقعی اصلاح شد.", true);
                await LoadTankhahsAsync();
            }
        }

        private string GetTankhahNumber(int accountId)
        {
            var acc = _allTankhahs.FirstOrDefault(a => a.Id == accountId);
            if (acc == null) return "1";
            var match = Regex.Match(acc.Title, @"(?:تنخواه|لیست)\s*(?:شماره\s*)?(\d+)");
            return match.Success ? match.Groups[1].Value : "1";
        }

        private void DgvTankhahs_SelectionChanged(object? sender, EventArgs e)
        {
            if (_isInitializing || IsDisposed) return;

            if (dgvTankhahs.CurrentRow == null)
            {
                _activeTankhah = null;
                btnViewDetails.Enabled = false;
                btnEditTankhah.Enabled = false; btnDeleteTankhah.Enabled = false;
                return;
            }

            int tankhahId = Convert.ToInt32(dgvTankhahs.CurrentRow.Cells["Id"].Value);
            _activeTankhah = _allTankhahs.FirstOrDefault(a => a.Id == tankhahId);

            if (_activeTankhah == null) return;

            btnViewDetails.Enabled = true;
            btnEditTankhah.Enabled = true; btnDeleteTankhah.Enabled = true;
        }

        private void BtnViewDetails_Click(object? sender, EventArgs e)
        {
            if (_activeTankhah == null || IsDisposed) return;
            using var form = new TankhahDetailForm(_connectionString, _activeTankhah);
            form.ShowDialog(this);
            _ = OnOwnerSelectedAsync();
        }

        private void ClearUI()
        {
            _activeTankhah = null;
            lblOwnerTotalBalance.Text = "0"; lblOwnerTotalSpent.Text = "0";
            btnViewDetails.Enabled = false;
            btnEditTankhah.Enabled = false; btnDeleteTankhah.Enabled = false;
        }

        private void DgvTankhahs_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvTankhahs.Columns[e.ColumnIndex].Name;

            if (colName == "InitialBalance" || colName == "CurrentBalance" || colName == "TotalSpent")
            {
                if (e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;

                    if (colName == "CurrentBalance" && dec < 0)
                    {
                        e.CellStyle.ForeColor = UIHelper.WarmDanger;
                        e.CellStyle.Font = new Font(dgvTankhahs.Font, FontStyle.Bold);
                    }
                }
            }
            else if (colName == "CreatedAt")
            {
                if (e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
            }
        }

        private async Task<string?> AskForTankhahOwnerAsync()
        {
            var owners = await _lookupRepo.GetByCategoryAsync("TankhahOwner");
            using var form = new Form { Text = "ثبت لیست جدید", Size = new Size(450, 220), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true, Font = UIHelper.PersianFont(11f), BackColor = Color.White };
            var lbl = new Label { Text = "نام صاحب تنخواه (مسئول):", Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(20, 10, 20, 0), Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
            var cmb = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDown, Font = UIHelper.PersianFont(12f), BackColor = UIHelper.WarmInputBackground };
            cmb.Items.AddRange(owners.Select(o => o.Value).ToArray());
            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(20, 10, 20, 10) };
            var btnOk = UIHelper.CreateButton("✔ تایید و ایجاد لیست", UIHelper.ActionGreen, 180, 40);
            btnOk.DialogResult = DialogResult.OK; btnOk.Dock = DockStyle.Right; pnlBottom.Controls.Add(btnOk);
            var pnlContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 10, 20, 10) };
            pnlContent.Controls.Add(cmb); pnlContent.Controls.Add(lbl);
            form.Controls.Add(pnlContent); form.Controls.Add(pnlBottom); form.AcceptButton = btnOk;

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                string ownerName = cmb.Text.Trim();
                if (!string.IsNullOrEmpty(ownerName))
                {
                    if (!owners.Any(o => o.Value == ownerName)) _lookupRepo.Add(new LookupItem { Category = "TankhahOwner", Value = ownerName });
                    return ownerName;
                }
            }
            return null;
        }

        private async void BtnNewTankhah_Click(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            string? ownerName = await AskForTankhahOwnerAsync();
            if (string.IsNullOrWhiteSpace(ownerName)) return;

            string normalizedOwner = StringHelper.NormalizePersianText(ownerName.Trim());
            var existingNumbers = new HashSet<int>();
            foreach (var t in _allTankhahs)
            {
                string normalizedTitle = StringHelper.NormalizePersianText(t.Title);
                if (normalizedTitle.EndsWith(normalizedOwner))
                {
                    var match = Regex.Match(t.Title, @"(?:تنخواه|لیست)\s*(?:شماره\s*)?(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int num)) existingNumbers.Add(num);
                }
            }

            int nextNum = 1;
            while (existingNumbers.Contains(nextNum)) nextNum++;

            string newTitle = $"لیست {nextNum} {ownerName.Trim()}";

            var account = new CompanyBankAccount { Title = newTitle, IsCashDesk = true, IsActive = true, InitialBalance = 0, CurrentBalance = 0 };

            bool success = await AsyncHelper.RunWithLoading(this, async () => { await _bankRepo.AddAsync(account); }, "در حال ثبت لیست...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show($"لیست جدید ثبت شد: {newTitle}", isSuccess: true);
                await LoadTankhahsAsync();
                if (!IsDisposed)
                {
                    for (int i = 0; i < cmbTankhah.Items.Count; i++)
                    {
                        if (cmbTankhah.Items[i] is TankhahOwnerItem item && item.OwnerName == normalizedOwner) { cmbTankhah.SelectedIndex = i; break; }
                    }
                    await Task.Delay(300);
                    if (dgvTankhahs.Rows.Count > 0)
                    {
                        dgvTankhahs.ClearSelection();
                        dgvTankhahs.Rows[0].Selected = true;
                        dgvTankhahs.CurrentCell = dgvTankhahs.Rows[0].Cells["Title"];
                    }
                }
            }
        }

        private async void BtnEditTankhah_Click(object? sender, EventArgs e)
        {
            if (_activeTankhah == null || IsDisposed) return;
            using var form = new TankhahListEditForm(_connectionString, _activeTankhah);
            if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed)
            {
                await LoadTankhahsAsync();
            }
        }

        private async void BtnDeleteTankhah_Click(object? sender, EventArgs e)
        {
            if (_activeTankhah == null || IsDisposed) return;
            if (!ModernMessageBox.AskConfirm($"آیا از حذف «{_activeTankhah.Title}» مطمئن هستید؟", "حذف لیست")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () => { await _bankRepo.DeleteAsync(_activeTankhah.Id); }, "در حال حذف...");
            if (success && !IsDisposed) { ToastHelper.Show("لیست حذف شد.", true); await LoadTankhahsAsync(); }
        }
    }
}