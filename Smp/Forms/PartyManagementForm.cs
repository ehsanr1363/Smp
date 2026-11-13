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
    public class PartyManagementForm : Form
    {
        private readonly string _connectionString;
        private DataGridView dgvParties = null!;
        private TextBox txtSearch = null!;
        private System.Windows.Forms.Timer _searchTimer = null!;

        private Button btnAdd = null!, btnEdit = null!, btnDelete = null!, btnBankAcc = null!, btnRefresh = null!;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public PartyManagementForm(string connectionString)
        {
            _connectionString = connectionString;
            BuildUI();
            ShortcutHelper.ApplyStandardShortcuts(this, btnRefresh);

            Shown += async (s, e) => await LoadPartiesSafeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _loadLock?.Dispose(); } catch { }
                try { _searchTimer?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            // ✅ استفاده از الگوی استاندارد لیست
            var (topRightPanel, topLeftPanel, grid, bottomPanel) = StandardFormHelper.SetupStandardListLayout(
                this, "مدیریت طرفین قرارداد (پیمانکاران و کارفرمایان)", new Size(1200, 800));

            dgvParties = grid;

            // ✅ تغییر ستون‌های تیک به TextBoxColumn برای نمایش فانتزی
            dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "DisplayName", DataPropertyName = "DisplayName", HeaderText = "نام / شرکت", FillWeight = 30 });
            dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "PartyType", DataPropertyName = "PartyType", HeaderText = "نوع شخص", FillWeight = 10 });
            dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "NationalCode", DataPropertyName = "NationalCode", HeaderText = "کد ملی", FillWeight = 15 });
            dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "NationalId", DataPropertyName = "NationalId", HeaderText = "شناسه ملی", FillWeight = 15 });
            dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "Phone", DataPropertyName = "Phone", HeaderText = "تلفن تماس", FillWeight = 15 });
            dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsContractor", DataPropertyName = "IsContractor", HeaderText = "پیمانکار", FillWeight = 7 });
            dgvParties.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsClient", DataPropertyName = "IsClient", HeaderText = "کارفرما", FillWeight = 8 });

            dgvParties.CellFormatting += DgvParties_CellFormatting;
            dgvParties.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenEditForm(); };
            dgvParties.SelectionChanged += (s, e) => UpdateButtonsState();

            // تجهیز پنل چپ (جستجو)
            var lblSearch = new Label { Text = "🔍 جستجو:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(0, 10, 5, 0) };
            txtSearch = new TextBox { Width = 300, Font = UIHelper.PersianFont(11f), PlaceholderText = "نام، شرکت یا کد ملی...", Margin = new Padding(0, 6, 0, 0) };

            _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await LoadPartiesSafeAsync(); };
            txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

            topLeftPanel.Controls.Add(lblSearch);
            topLeftPanel.Controls.Add(txtSearch);

            // دکمه‌های عملیات (پایین)
            btnAdd = UIHelper.CreateButton("➕ افزودن شخص", UIHelper.ActionGreen, 140, 42);
            btnAdd.Click += (s, e) => { using var f = new PartyEditForm(_connectionString); if (f.ShowDialog(this) == DialogResult.OK) _ = LoadPartiesSafeAsync(); };

            btnEdit = UIHelper.CreateButton("✏️ ویرایش", UIHelper.ActionBlue, 110, 42);
            btnEdit.Enabled = false;
            btnEdit.Click += (s, e) => OpenEditForm();

            btnDelete = UIHelper.CreateButton("🗑️ حذف", UIHelper.ActionRed, 100, 42);
            btnDelete.Enabled = false;
            btnDelete.Click += BtnDelete_Click;

            btnBankAcc = UIHelper.CreateButton("🏦 حساب‌های بانکی", UIHelper.WarmSecondary, 150, 42);
            btnBankAcc.Enabled = false;
            btnBankAcc.Click += BtnBankAcc_Click;

            btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی", UIHelper.SectionGray, 120, 42);
            btnRefresh.Click += async (s, e) => await LoadPartiesSafeAsync();

            bottomPanel.Controls.Add(btnAdd);
            bottomPanel.Controls.Add(btnEdit);
            bottomPanel.Controls.Add(btnDelete);
            bottomPanel.Controls.Add(btnBankAcc);
            bottomPanel.Controls.Add(btnRefresh);
        }

        private void UpdateButtonsState()
        {
            if (IsDisposed) return;
            bool hasSelection = dgvParties.CurrentRow != null;
            btnEdit.Enabled = hasSelection;
            btnDelete.Enabled = hasSelection;
            btnBankAcc.Enabled = hasSelection;
        }

        private async Task LoadPartiesSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                string search = txtSearch.Text.Trim();

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    string query = @"
                        SELECT Id, PartyType, IsForeign, IsContractor, IsClient, NationalCode, 
                               FirstName, LastName, CompanyName, NationalId, EconomicCode, Phone, Address,
                               CASE 
                                   WHEN PartyType = N'حقیقی' THEN ISNULL(FirstName, N'') + N' ' + ISNULL(LastName, N'')
                                   ELSE ISNULL(CompanyName, N'')
                               END AS DisplayName
                        FROM Parties 
                        WHERE 1=1";

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        query += " AND (FirstName LIKE @s OR LastName LIKE @s OR CompanyName LIKE @s OR NationalCode LIKE @s OR NationalId LIKE @s)";
                    }
                    query += " ORDER BY Id DESC";

                    using var conn = new SqlConnection(_connectionString);
                    await conn.OpenAsync();
                    using var cmd = new SqlCommand(query, conn);
                    if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@s", $"%{search}%");

                    using var reader = await cmd.ExecuteReaderAsync();
                    var dt = new DataTable();
                    dt.Load(reader);

                    if (IsDisposed) return;
                    dgvParties.DataSource = dt;

                }, "در حال بارگذاری لیست...");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PartyManagementForm.LoadPartiesSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
            }
            finally { try { _loadLock.Release(); } catch { } }
        }

        private void DgvParties_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvParties.Columns[e.ColumnIndex].Name;

            // ✅ فانتزی کردن ستون‌های بولی (پیمانکار و کارفرما)
            if (colName == "IsContractor" || colName == "IsClient")
            {
                if (e.Value != null && e.Value != DBNull.Value)
                {
                    bool isTrue = false;
                    try { isTrue = Convert.ToInt32(e.Value) == 1; }
                    catch { try { isTrue = Convert.ToBoolean(e.Value); } catch { } }

                    if (isTrue)
                    {
                        e.Value = "✔️"; // تیک سبز
                        e.CellStyle.ForeColor = UIHelper.ActionGreen;
                        e.CellStyle.Font = new Font("Segoe UI Emoji", 11f, FontStyle.Bold);
                    }
                    else
                    {
                        e.Value = "➖"; // خط تیره
                        e.CellStyle.ForeColor = Color.LightGray;
                        e.CellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
                    }
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    e.FormattingApplied = true;
                }
                return;
            }

            // بررسی نوع شخص و کدهای مربوطه
            string partyType = dgvParties.Rows[e.RowIndex].Cells["PartyType"].Value?.ToString() ?? "";
            if (colName == "NationalCode" && PartyTypeHelper.IsLegal(partyType)) { e.Value = "---"; e.FormattingApplied = true; }
            else if ((colName == "NationalId") && PartyTypeHelper.IsNatural(partyType)) { e.Value = "---"; e.FormattingApplied = true; }
        }

        private void OpenEditForm()
        {
            if (dgvParties.CurrentRow == null) return;
            int id = Convert.ToInt32(dgvParties.CurrentRow.Cells["Id"].Value);
            using var f = new PartyEditForm(_connectionString, id);
            if (f.ShowDialog(this) == DialogResult.OK) _ = LoadPartiesSafeAsync();
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (dgvParties.CurrentRow == null) return;
            int id = Convert.ToInt32(dgvParties.CurrentRow.Cells["Id"].Value);

            if (!ModernMessageBox.AskConfirm("آیا از حذف این شخص مطمئن هستید؟", "حذف")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var tx = conn.BeginTransaction();

                try
                {
                    var references = new List<string>();
                    void CheckRef(string table, string name)
                    {
                        using var cmd = new SqlCommand($"SELECT COUNT(1) FROM {table} WHERE PartyId = @id", conn, tx);
                        cmd.Parameters.AddWithValue("@id", id);
                        if (Convert.ToInt32(cmd.ExecuteScalar()) > 0) references.Add(name);
                    }

                    CheckRef("ContractorContracts", "قرارداد پیمانکار");
                    CheckRef("ClientContracts", "قرارداد کارفرما");
                    CheckRef("ProjectExpenses", "فاکتور هزینه");
                    CheckRef("CashChecks", "چک بانکی");

                    if (references.Count > 0)
                        throw new InvalidOperationException("این شخص در موارد زیر ارجاع داده شده و قابل حذف نیست:\n\n• " + string.Join("\n• ", references));

                    using (var cmd = new SqlCommand("DELETE FROM PartyBankAccounts WHERE PartyId = @id", conn, tx)) { cmd.Parameters.AddWithValue("@id", id); await cmd.ExecuteNonQueryAsync(); }
                    using (var cmd = new SqlCommand("DELETE FROM Parties WHERE Id = @id", conn, tx)) { cmd.Parameters.AddWithValue("@id", id); await cmd.ExecuteNonQueryAsync(); }

                    await tx.CommitAsync();
                }
                catch { await tx.RollbackAsync(); throw; }
            }, "در حال حذف...");

            if (success && !IsDisposed) { ToastHelper.Show("با موفقیت حذف شد.", true); await LoadPartiesSafeAsync(); }
        }

        private void BtnBankAcc_Click(object? sender, EventArgs e)
        {
            if (dgvParties.CurrentRow == null) return;
            int id = Convert.ToInt32(dgvParties.CurrentRow.Cells["Id"].Value);
            string name = dgvParties.CurrentRow.Cells["DisplayName"].Value?.ToString() ?? "";
            using var f = new PartyBankAccountsForm(_connectionString, id, name);
            f.ShowDialog(this);
        }
    }
}