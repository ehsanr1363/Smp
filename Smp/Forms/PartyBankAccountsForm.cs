using Microsoft.Data.SqlClient;
using SMP.Helpers;
using SMP.Services;
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
    public class PartyBankAccountsForm : Form
    {
        private readonly string _connectionString;
        private readonly int _partyId;
        private readonly string _partyName;

        private DataGridView dgvBanks = null!;
        private TextBox txtSheba = null!, txtBankName = null!;
        private Button btnAdd = null!, btnDelete = null!, btnSetMain = null!;

        private readonly Dictionary<string, string> _bankCodes = new();

        // ✅ FIX P2: جلوگیری از تداخل
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public PartyBankAccountsForm(string connectionString, int partyId, string partyName)
        {
            _connectionString = connectionString;
            _partyId = partyId;
            _partyName = partyName;

            BuildUI();

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnAdd);
            ShowInTaskbar = false;

            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _loadLock?.Dispose(); } catch { }
                try { _actionLock?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, $"حساب‌های بانکی: {_partyName}", new Size(900, 650));

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var pnlTop = UIHelper.CreateWarmPanel();
            pnlTop.Padding = new Padding(15);

            var layoutTop = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
            layoutTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            layoutTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layoutTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            layoutTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layoutTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            layoutTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            var lblSheba = new Label { Text = "شماره شبا (IR...):", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(5, 10, 5, 5) };
            UIHelper.StyleLabel(lblSheba);

            txtSheba = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(5, 8, 5, 5), Font = UIHelper.PersianFont(12f), BackColor = UIHelper.WarmInputBackground, BorderStyle = BorderStyle.FixedSingle };
            txtSheba.TextChanged += TxtSheba_TextChanged;
            layoutTop.Controls.Add(lblSheba, 0, 0);
            layoutTop.Controls.Add(txtSheba, 1, 0);

            var lblBankName = new Label { Text = "نام بانک:", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(5, 10, 5, 5) };
            UIHelper.StyleLabel(lblBankName);

            txtBankName = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Margin = new Padding(5, 8, 5, 5), Font = UIHelper.PersianFont(12f), BackColor = UIHelper.WarmInputBackground, BorderStyle = BorderStyle.FixedSingle };
            layoutTop.Controls.Add(lblBankName, 2, 0);
            layoutTop.Controls.Add(txtBankName, 3, 0);

            btnAdd = UIHelper.CreateButton("➕ ثبت و افزودن شماره شبا", UIHelper.WarmPrimary, 250, 45);
            btnAdd.Dock = DockStyle.Fill;
            btnAdd.Click += async (s, e) => await BtnAdd_Click(s, e);
            layoutTop.Controls.Add(btnAdd, 0, 1);
            layoutTop.SetColumnSpan(btnAdd, 4);

            pnlTop.Controls.Add(layoutTop);
            mainLayout.Controls.Add(pnlTop, 0, 0);

            dgvBanks = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, Margin = new Padding(0, 10, 0, 10) };
            dgvBanks.ApplyModernStyle();
            dgvBanks.AutoGenerateColumns = false;

            dgvBanks.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", Visible = false });
            dgvBanks.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ShebaNumber", HeaderText = "شماره شبا", FillWeight = 40 });
            dgvBanks.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BankName", HeaderText = "نام بانک", FillWeight = 30 });
            dgvBanks.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "IsMainAccount", HeaderText = "وضعیت", FillWeight = 15 });

            dgvBanks.CellFormatting += DgvBanks_CellFormatting;
            mainLayout.Controls.Add(dgvBanks, 0, 1);

            var pnlBottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(10),
                BackColor = UIHelper.WarmPanelBackground
            };

            btnDelete = UIHelper.CreateButton("🗑️ حذف شماره شبا", UIHelper.WarmDanger, 180, 40);
            btnDelete.Click += async (s, e) => await BtnDelete_Click(s, e);

            btnSetMain = UIHelper.CreateButton("⭐ تنظیم به عنوان حساب اصلی", UIHelper.WarmSecondary, 220, 40);
            btnSetMain.Click += async (s, e) => await BtnSetMain_Click(s, e);

            pnlBottom.Controls.Add(btnDelete);
            pnlBottom.Controls.Add(btnSetMain);
            mainLayout.Controls.Add(pnlBottom, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private async Task LoadDataSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var banksTask = Task.Run(() => DbHelper.ExecuteQuery(_connectionString, "SELECT BankCode, BankName FROM Banks;"));
                    var shebasTask = Task.Run(() => DbHelper.ExecuteQuery(_connectionString,
                        "SELECT Id, ShebaNumber, BankName, IsMainAccount FROM PartyBankAccounts WHERE PartyId = @pid ORDER BY IsMainAccount DESC, Id DESC;",
                        new SqlParameter("@pid", _partyId)));

                    await Task.WhenAll(banksTask, shebasTask);

                    if (IsDisposed) return;

                    _bankCodes.Clear();
                    foreach (DataRow row in banksTask.Result.Rows)
                    {
                        string code = row[0]?.ToString() ?? "";
                        string name = row[1]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(code) && !_bankCodes.ContainsKey(code))
                            _bankCodes.Add(code, name);
                    }

                    dgvBanks.DataSource = shebasTask.Result;
                }, "در حال بارگذاری اطلاعات...");

                if (!success && !IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات.");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PartyBankAccountsForm.LoadDataSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }

        private void TxtSheba_TextChanged(object? sender, EventArgs e)
        {
            string sheba = StringHelper.ConvertToEnglishDigits(txtSheba.Text).Trim().Replace(" ", "").ToUpper();

            string? bankCode = null;
            if (sheba.StartsWith("IR") && sheba.Length >= 7)
                bankCode = sheba.Substring(4, 3);
            else if (sheba.Length >= 5)
                bankCode = sheba.Substring(2, 3);

            if (!string.IsNullOrEmpty(bankCode) && _bankCodes.ContainsKey(bankCode))
                txtBankName.Text = _bankCodes[bankCode];
            else
                txtBankName.Text = "";
        }

        private void DgvBanks_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvBanks.Columns[e.ColumnIndex].Name != "IsMainAccount") return;

            var rowView = dgvBanks.Rows[e.RowIndex].DataBoundItem as DataRowView;
            if (rowView == null) return;

            int isMain = Convert.ToInt32(rowView["IsMainAccount"]);
            if (isMain == 1)
            {
                e.Value = "⭐ حساب اصلی";
                e.CellStyle.ForeColor = UIHelper.WarmSecondary;
                e.CellStyle.Font = UIHelper.PersianFont(12f, FontStyle.Bold);
            }
            else
            {
                e.Value = "-";
                e.CellStyle.ForeColor = UIHelper.WarmText;
            }
            e.FormattingApplied = true;
        }

        private async Task BtnAdd_Click(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                string rawSheba = txtSheba.Text.Trim();
                string normalizedSheba = BankHelper.NormalizeSheba(rawSheba);

                if (normalizedSheba == null || normalizedSheba.Length != 24)
                {
                    ModernMessageBox.ShowError("شماره شبا وارد شده معتبر نیست.");
                    return;
                }

                string fullSheba = "IR" + normalizedSheba;

                if (!ShebaHelper.IsValid(fullSheba))
                {
                    ModernMessageBox.ShowError("شماره شبا وارد شده از نظر چک‌سام معتبر نیست.");
                    return;
                }

                string bankName = BankHelper.GetBankNameFromSheba(normalizedSheba);
                if (string.IsNullOrEmpty(bankName) || bankName.StartsWith("بانک ناشناخته"))
                {
                    ModernMessageBox.ShowError("کد بانک در شماره شبا نامعتبر است یا در سیستم تعریف نشده است.");
                    return;
                }

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    string checkSql = "SELECT COUNT(1) FROM PartyBankAccounts WHERE ShebaNumber = @sheba;";
                    var exists = Convert.ToInt32(await Task.Run(() => DbHelper.ExecuteScalar(_connectionString, checkSql, new SqlParameter("@sheba", fullSheba)))) > 0;

                    if (exists)
                        throw new InvalidOperationException("این شماره شبا قبلاً در سیستم ثبت شده است.");

                    string countSql = "SELECT COUNT(1) FROM PartyBankAccounts WHERE PartyId = @pid;";
                    var count = Convert.ToInt32(await Task.Run(() => DbHelper.ExecuteScalar(_connectionString, countSql, new SqlParameter("@pid", _partyId))));
                    bool isFirstAccount = count == 0;
                    int isMain = isFirstAccount ? 1 : 0;

                    string insertSql = @"
                        INSERT INTO PartyBankAccounts (PartyId, ShebaNumber, BankCode, BankName, IsMainAccount)
                        VALUES (@pid, @sheba, @bcode, @bname, @isMain);";

                    await Task.Run(() => DbHelper.ExecuteNonQuery(_connectionString, insertSql,
                        new SqlParameter("@pid", _partyId),
                        new SqlParameter("@sheba", fullSheba),
                        new SqlParameter("@bcode", normalizedSheba.Substring(0, 3)),
                        new SqlParameter("@bname", bankName),
                        new SqlParameter("@isMain", isMain)));
                }, "در حال ثبت شماره شبا...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("شماره شبا با موفقیت ثبت شد.", isSuccess: true);
                    txtSheba.Clear();
                    txtBankName.Clear();
                    await LoadDataSafeAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PartyBankAccountsForm.BtnAdd_Click");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private async Task BtnSetMain_Click(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (dgvBanks.CurrentRow?.DataBoundItem is not DataRowView rowView)
                {
                    ModernMessageBox.ShowWarning("لطفاً یک شماره شبا را از جدول انتخاب کنید.");
                    return;
                }

                int id = Convert.ToInt32(rowView["Id"]);
                int isMain = Convert.ToInt32(rowView["IsMainAccount"]);

                if (isMain == 1) return;

                if (!ModernMessageBox.AskConfirm("آیا این شماره شبا به عنوان حساب اصلی تنظیم شود؟", "تغییر حساب اصلی")) return;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    await Task.Run(() =>
                    {
                        using var conn = new SqlConnection(_connectionString);
                        conn.Open();
                        using var transaction = conn.BeginTransaction();

                        try
                        {
                            using (var cmd1 = new SqlCommand("UPDATE PartyBankAccounts SET IsMainAccount = 0 WHERE PartyId = @pid;", conn, transaction))
                            {
                                cmd1.Parameters.AddWithValue("@pid", _partyId);
                                cmd1.ExecuteNonQuery();
                            }

                            using (var cmd2 = new SqlCommand("UPDATE PartyBankAccounts SET IsMainAccount = 1 WHERE Id = @id;", conn, transaction))
                            {
                                cmd2.Parameters.AddWithValue("@id", id);
                                cmd2.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    });
                }, "در حال تنظیم حساب اصلی...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("حساب اصلی با موفقیت تغییر یافت.", isSuccess: true);
                    await LoadDataSafeAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PartyBankAccountsForm.BtnSetMain_Click");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private async Task BtnDelete_Click(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (dgvBanks.CurrentRow?.DataBoundItem is not DataRowView rowView)
                {
                    ModernMessageBox.ShowWarning("لطفاً یک شماره شبا را از جدول انتخاب کنید.");
                    return;
                }

                int id = Convert.ToInt32(rowView["Id"]);
                string sheba = rowView["ShebaNumber"].ToString() ?? "";
                bool isMain = Convert.ToInt32(rowView["IsMainAccount"]) == 1;

                if (isMain)
                {
                    ModernMessageBox.ShowWarning("شما نمی‌توانید حساب اصلی را حذف کنید. ابتدا یک حساب دیگر را به عنوان اصلی تنظیم کنید.");
                    return;
                }

                if (!ModernMessageBox.AskConfirm($"آیا از حذف شماره شبا «{sheba}» مطمئن هستید؟", "حذف شماره شبا")) return;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    string deleteSql = "DELETE FROM PartyBankAccounts WHERE Id = @id;";
                    await Task.Run(() => DbHelper.ExecuteNonQuery(_connectionString, deleteSql, new SqlParameter("@id", id)));
                }, "در حال حذف...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("شماره شبا با موفقیت حذف شد.", isSuccess: true);
                    await LoadDataSafeAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PartyBankAccountsForm.BtnDelete_Click");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}