using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using PersonnelApp.Helpers;
using PersonnelApp.Models;

namespace PersonnelApp.Forms
{
    public class PartyManagementForm : Form
    {
        private readonly string _connectionString;

        private DataGridView dgvParties = null!;
        private TextBox txtNationalCode = null!, txtFirstName = null!, txtLastName = null!, txtCompanyName = null!, txtNationalId = null!, txtEconomicCode = null!, txtPhone = null!, txtAddress = null!;
        private CheckBox chkForeign = null!, chkContractor = null!, chkClient = null!;
        private Button btnManageBankAccounts = null!;
        private RadioButton rbNatural = null!, rbLegal = null!;
        private Button btnSave = null!, btnDelete = null!, btnNew = null!;

        private int? _editingId = null;
        private bool _isInitializing = false;
        private TableLayoutPanel formLayout = null!;

        public PartyManagementForm(string connectionString)
        {
            _connectionString = connectionString;
            _isInitializing = true;

            BuildUI();

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            ShowInTaskbar = false;

            this.Shown += async (s, e) =>
            {
                await LoadPartiesSafeAsync();

                this.BeginInvoke(new Action(() =>
                {
                    if (dgvParties.Rows.Count > 0)
                    {
                        dgvParties.ClearSelection();
                        dgvParties.CurrentCell = null;
                    }
                    _isInitializing = false;
                }));
            };
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, "مدیریت طرفین قرارداد (پیمانکاران و کارفرمایان)", new Size(1600, 900));

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            dgvParties = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = true };
            dgvParties.ApplyModernStyle();
            dgvParties.SelectionChanged += DgvParties_SelectionChanged;
            dgvParties.CellFormatting += DgvParties_CellFormatting;
            mainLayout.Controls.Add(dgvParties, 0, 0);

            var formPanel = UIHelper.CreateWarmPanel();

            formLayout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };

            rbNatural = new RadioButton { Text = "شخص حقیقی", Checked = true, AutoSize = true };
            UIHelper.StyleToggle(rbNatural);

            rbLegal = new RadioButton { Text = "شخص حقوقی", AutoSize = true };
            UIHelper.StyleToggle(rbLegal);

            rbNatural.CheckedChanged += (s, e) => ToggleFields();
            rbLegal.CheckedChanged += (s, e) => ToggleFields();

            chkForeign = new CheckBox { Text = "اتباع", AutoSize = true };
            UIHelper.StyleToggle(chkForeign);
            chkForeign.ForeColor = UIHelper.WarmDanger;
            chkForeign.CheckedChanged += (s, e) => ToggleForeignMode();

            chkContractor = new CheckBox { Text = "پیمانکار", Checked = true, AutoSize = true };
            UIHelper.StyleToggle(chkContractor);
            chkContractor.ForeColor = UIHelper.WarmSecondary;

            chkClient = new CheckBox { Text = "کارفرما", Checked = true, AutoSize = true };
            UIHelper.StyleToggle(chkClient);
            chkClient.ForeColor = UIHelper.WarmSecondary;

            var rbPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            rbPanel.Controls.Add(rbNatural);
            rbPanel.Controls.Add(rbLegal);
            rbPanel.Controls.Add(chkForeign);
            formLayout.Controls.Add(rbPanel, 0, 0);
            formLayout.SetColumnSpan(rbPanel, 2);
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            var rolePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            rolePanel.Controls.Add(chkContractor);
            rolePanel.Controls.Add(chkClient);
            formLayout.Controls.Add(rolePanel, 0, 1);
            formLayout.SetColumnSpan(rolePanel, 2);
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            AddRowToLayout(2, 40F);
            txtNationalCode = new TextBox(); UIHelper.AddField(formLayout, "کد ملی:", txtNationalCode, 2, 0, 1);

            AddRowToLayout(3, 40F);
            txtFirstName = new TextBox(); UIHelper.AddField(formLayout, "نام:", txtFirstName, 3, 0, 1);

            AddRowToLayout(4, 40F);
            txtLastName = new TextBox(); UIHelper.AddField(formLayout, "نام خانوادگی:", txtLastName, 4, 0, 1);

            AddRowToLayout(5, 40F);
            txtCompanyName = new TextBox(); UIHelper.AddField(formLayout, "نام شرکت:", txtCompanyName, 5, 0, 1);

            AddRowToLayout(6, 40F);
            txtNationalId = new TextBox(); UIHelper.AddField(formLayout, "شناسه ملی:", txtNationalId, 6, 0, 1);

            AddRowToLayout(7, 40F);
            txtEconomicCode = new TextBox(); UIHelper.AddField(formLayout, "کد اقتصادی:", txtEconomicCode, 7, 0, 1);

            AddRowToLayout(8, 40F);
            txtPhone = new TextBox(); UIHelper.AddField(formLayout, "تلفن تماس:", txtPhone, 8, 0, 1);

            AddRowToLayout(9, 85F);
            txtAddress = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 65, TextAlign = HorizontalAlignment.Left };
            UIHelper.AddField(formLayout, "آدرس:", txtAddress, 9, 0, 1);

            btnManageBankAccounts = UIHelper.CreateButton("🏦 مدیریت حساب‌های بانکی (شبا)", UIHelper.WarmSecondary, 250, 45);
            btnManageBankAccounts.Dock = DockStyle.Fill;
            btnManageBankAccounts.Enabled = false;
            btnManageBankAccounts.Margin = new Padding(0, 15, 0, 5);
            btnManageBankAccounts.Click += BtnManageBankAccounts_Click;
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            formLayout.Controls.Add(btnManageBankAccounts, 0, 10);
            formLayout.SetColumnSpan(btnManageBankAccounts, 2);

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50 };
            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.WarmPrimary, 100, 40);
            btnNew = UIHelper.CreateButton("➕ جدید", UIHelper.WarmSecondary, 100, 40);
            btnDelete = UIHelper.CreateButton("🗑️ حذف", UIHelper.WarmDanger, 100, 40);

            btnSave.Click += BtnSave_Click;
            btnNew.Click += (s, e) => ClearForm();
            btnDelete.Click += BtnDelete_Click;

            btnPanel.Controls.Add(btnSave);
            btnPanel.Controls.Add(btnNew);
            btnPanel.Controls.Add(btnDelete);

            formPanel.Controls.Add(formLayout);
            formPanel.Controls.Add(btnPanel);
            mainLayout.Controls.Add(formPanel, 1, 0);

            this.Controls.Add(mainLayout);
            ToggleFields();
        }

        private void AddRowToLayout(int rowIndex, float height)
        {
            if (formLayout.RowStyles.Count <= rowIndex)
                formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        }

        private void ToggleForeignMode()
        {
            var lblNationalCode = formLayout.GetControlFromPosition(0, 2) as Label;
            if (lblNationalCode != null)
            {
                lblNationalCode.Text = chkForeign.Checked ? "شماره پاسپورت:" : "کد ملی:";
                lblNationalCode.ForeColor = chkForeign.Checked ? UIHelper.WarmDanger : UIHelper.WarmText;
            }
            txtNationalCode.TextAlign = HorizontalAlignment.Center;
        }

        private void ToggleFields()
        {
            bool isNatural = rbNatural.Checked;
            txtNationalCode.Enabled = isNatural;
            txtFirstName.Enabled = isNatural;
            txtLastName.Enabled = isNatural;
            txtCompanyName.Enabled = !isNatural;
            txtNationalId.Enabled = !isNatural;
            txtEconomicCode.Enabled = !isNatural;

            chkForeign.Enabled = true;
        }

        private void BtnManageBankAccounts_Click(object? sender, EventArgs e)
        {
            if (!_editingId.HasValue) return;
            string name = rbNatural.Checked ? $"{txtFirstName.Text} {txtLastName.Text}" : txtCompanyName.Text;
            using var form = new PartyBankAccountsForm(_connectionString, _editingId.Value, name);
            form.ShowDialog(this);
        }

        private async Task LoadPartiesSafeAsync()
        {
            string query = @"
                SELECT Id, PartyType, IsForeign, IsContractor, IsClient, NationalCode, 
                       FirstName, LastName, CompanyName, NationalId, EconomicCode, Phone, Address,
                       CASE WHEN PartyType = N'حقیقی' THEN FirstName + N' ' + LastName ELSE CompanyName END AS DisplayName
                FROM Parties ORDER BY Id DESC";

            var (success, dt) = await AsyncHelper.RunWithLoading(
                this,
                () => Task.Run(() => DbHelper.ExecuteQuery(_connectionString, query)),
                "در حال بارگذاری لیست طرفین...");

            if (!success || dt == null) return;

            dgvParties.DataSource = dt;

            if (dgvParties.Columns["Id"] != null) dgvParties.Columns["Id"].Visible = false;
            if (dgvParties.Columns["Address"] != null) dgvParties.Columns["Address"].Visible = false;
            if (dgvParties.Columns["FirstName"] != null) dgvParties.Columns["FirstName"].Visible = false;
            if (dgvParties.Columns["LastName"] != null) dgvParties.Columns["LastName"].Visible = false;
            if (dgvParties.Columns["CompanyName"] != null) dgvParties.Columns["CompanyName"].Visible = false;
            if (dgvParties.Columns["IsForeign"] != null) dgvParties.Columns["IsForeign"].Visible = false;

            if (dgvParties.Columns["PartyType"] != null) dgvParties.Columns["PartyType"].HeaderText = "نوع";
            if (dgvParties.Columns["IsContractor"] != null) dgvParties.Columns["IsContractor"].HeaderText = "پیمانکار";
            if (dgvParties.Columns["IsClient"] != null) dgvParties.Columns["IsClient"].HeaderText = "کارفرما";
            if (dgvParties.Columns["NationalCode"] != null) dgvParties.Columns["NationalCode"].HeaderText = "کد ملی / پاسپورت";
            if (dgvParties.Columns["NationalId"] != null) dgvParties.Columns["NationalId"].HeaderText = "شناسه ملی";
            if (dgvParties.Columns["EconomicCode"] != null) dgvParties.Columns["EconomicCode"].HeaderText = "کد اقتصادی";
            if (dgvParties.Columns["Phone"] != null) dgvParties.Columns["Phone"].HeaderText = "تلفن";
            if (dgvParties.Columns["DisplayName"] != null)
            {
                dgvParties.Columns["DisplayName"].HeaderText = "نام / شرکت";
                dgvParties.Columns["DisplayName"].DisplayIndex = 1;
            }
        }

        private void DgvParties_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvParties.Columns[e.ColumnIndex] == null) return;

            string colName = dgvParties.Columns[e.ColumnIndex].Name;
            if (string.IsNullOrEmpty(colName)) colName = dgvParties.Columns[e.ColumnIndex].DataPropertyName;

            string partyType = dgvParties.Rows[e.RowIndex].Cells["PartyType"].Value?.ToString() ?? "";

            if (colName == "IsContractor" || colName == "IsClient")
            {
                if (e.Value != null && e.Value != DBNull.Value)
                {
                    bool isTrue = false;
                    try { isTrue = Convert.ToInt32(e.Value) == 1; }
                    catch { try { isTrue = e.Value.ToString()?.ToLower() == "true"; } catch { } }
                    e.Value = isTrue ? "✔" : "-";
                    e.FormattingApplied = true;
                }
                return;
            }

            // ✅ FIX P1: استفاده از PartyTypeHelper
            if (colName == "NationalCode" && PartyTypeHelper.IsLegal(partyType))
            {
                e.Value = "-";
                e.FormattingApplied = true;
                return;
            }

            if ((colName == "NationalId" || colName == "EconomicCode") && PartyTypeHelper.IsNatural(partyType))
            {
                e.Value = "-";
                e.FormattingApplied = true;
                return;
            }
        }

        private void DgvParties_SelectionChanged(object? sender, EventArgs e)
        {
            if (_isInitializing) return;
            if (dgvParties.CurrentRow == null) return;
            var row = dgvParties.CurrentRow;

            _editingId = Convert.ToInt32(row.Cells["Id"].Value);
            string type = row.Cells["PartyType"].Value?.ToString() ?? "";

            // ✅ FIX P1: استفاده از PartyTypeHelper
            if (PartyTypeHelper.IsNatural(type)) rbNatural.Checked = true; else rbLegal.Checked = true;

            bool isForeign = Convert.ToInt32(row.Cells["IsForeign"].Value) == 1;
            chkForeign.Checked = isForeign;
            ToggleForeignMode();

            chkContractor.Checked = Convert.ToInt32(row.Cells["IsContractor"].Value) == 1;
            chkClient.Checked = Convert.ToInt32(row.Cells["IsClient"].Value) == 1;

            txtNationalCode.Text = row.Cells["NationalCode"].Value?.ToString();
            txtFirstName.Text = row.Cells["FirstName"].Value?.ToString();
            txtLastName.Text = row.Cells["LastName"].Value?.ToString();
            txtCompanyName.Text = row.Cells["CompanyName"].Value?.ToString();
            txtNationalId.Text = row.Cells["NationalId"].Value?.ToString();
            txtEconomicCode.Text = row.Cells["EconomicCode"].Value?.ToString();
            txtPhone.Text = row.Cells["Phone"].Value?.ToString();
            txtAddress.Text = row.Cells["Address"].Value?.ToString();

            btnSave.Text = "💾 ویرایش";
            btnSave.BackColor = UIHelper.WarmPrimary;
            btnManageBankAccounts.Enabled = true;
        }

        private void ClearForm()
        {
            _editingId = null;
            txtNationalCode.Clear(); txtFirstName.Clear(); txtLastName.Clear();
            txtCompanyName.Clear(); txtNationalId.Clear(); txtEconomicCode.Clear();
            txtPhone.Clear(); txtAddress.Clear();
            rbNatural.Checked = true;
            chkForeign.Checked = false;
            chkContractor.Checked = true;
            chkClient.Checked = true;
            ToggleForeignMode();
            btnSave.Text = "💾 ذخیره";
            btnSave.BackColor = UIHelper.WarmPrimary;
            btnManageBankAccounts.Enabled = false;
            if (dgvParties.Rows.Count > 0) { dgvParties.ClearSelection(); dgvParties.CurrentCell = null; }
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (!chkContractor.Checked && !chkClient.Checked)
            {
                ModernMessageBox.ShowWarning("شخص باید حداقل در یکی از نقش‌ها (پیمانکار یا کارفرما) انتخاب شود.");
                return;
            }

            if (rbNatural.Checked)
            {
                if (chkForeign.Checked)
                {
                    if (string.IsNullOrWhiteSpace(txtNationalCode.Text)) { ModernMessageBox.ShowWarning("وارد کردن شماره پاسپورت الزامی است."); return; }
                }
                else
                {
                    if (!NationalCodeHelper.IsValid(txtNationalCode.Text)) { ModernMessageBox.ShowWarning("کد ملی نامعتبر است."); return; }
                }
                if (string.IsNullOrWhiteSpace(txtFirstName.Text) || string.IsNullOrWhiteSpace(txtLastName.Text)) { ModernMessageBox.ShowWarning("نام و نام خانوادگی الزامی است."); return; }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(txtCompanyName.Text)) { ModernMessageBox.ShowWarning("نام شرکت الزامی است."); return; }

                if (!chkForeign.Checked && !string.IsNullOrWhiteSpace(txtNationalId.Text) && !Regex.IsMatch(txtNationalId.Text.Trim(), @"^\d{11}$"))
                {
                    ModernMessageBox.ShowWarning("شناسه ملی باید یک عدد ۱۱ رقمی باشد.");
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(txtPhone.Text) && !Regex.IsMatch(txtPhone.Text.Trim(), @"^0\d{9,10}$"))
            {
                ModernMessageBox.ShowWarning("فرمت شماره تلفن صحیح نیست.");
                return;
            }

            int currentId = _editingId ?? 0;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (!string.IsNullOrWhiteSpace(txtNationalCode.Text))
                {
                    string checkNcSql = "SELECT COUNT(1) FROM Parties WHERE NationalCode = @nc AND Id != @id;";
                    var exists = Convert.ToInt32(await Task.Run(() => DbHelper.ExecuteScalar(_connectionString, checkNcSql, new SqlParameter("@nc", txtNationalCode.Text.Trim()), new SqlParameter("@id", currentId)))) > 0;
                    if (exists) throw new InvalidOperationException("این کد ملی (یا پاسپورت) قبلاً ثبت شده است.");
                }

                if (!string.IsNullOrWhiteSpace(txtNationalId.Text))
                {
                    string checkNidSql = "SELECT COUNT(1) FROM Parties WHERE NationalId = @nid AND Id != @id;";
                    var exists = Convert.ToInt32(await Task.Run(() => DbHelper.ExecuteScalar(_connectionString, checkNidSql, new SqlParameter("@nid", txtNationalId.Text.Trim()), new SqlParameter("@id", currentId)))) > 0;
                    if (exists) throw new InvalidOperationException("این شناسه ملی قبلاً ثبت شده است.");
                }

                if (!string.IsNullOrWhiteSpace(txtEconomicCode.Text))
                {
                    string checkEcSql = "SELECT COUNT(1) FROM Parties WHERE EconomicCode = @ec AND Id != @id;";
                    var exists = Convert.ToInt32(await Task.Run(() => DbHelper.ExecuteScalar(_connectionString, checkEcSql, new SqlParameter("@ec", txtEconomicCode.Text.Trim()), new SqlParameter("@id", currentId)))) > 0;
                    if (exists) throw new InvalidOperationException("این کد اقتصادی قبلاً ثبت شده است.");
                }

                string query = _editingId.HasValue
                    ? @"UPDATE Parties SET PartyType=@type, IsForeign=@isForeign, IsContractor=@isContractor, IsClient=@isClient, NationalCode=@nc, FirstName=@fn, LastName=@ln, CompanyName=@cn, NationalId=@nid, EconomicCode=@ec, Phone=@phone, Address=@addr WHERE Id=@id;"
                    : @"INSERT INTO Parties (PartyType, IsForeign, IsContractor, IsClient, NationalCode, FirstName, LastName, CompanyName, NationalId, EconomicCode, Phone, Address) VALUES (@type, @isForeign, @isContractor, @isClient, @nc, @fn, @ln, @cn, @nid, @ec, @phone, @addr);";

                var parameters = new List<SqlParameter>
                {
                    // ✅ FIX P1: استفاده از PartyTypeHelper
                    new SqlParameter("@type", rbNatural.Checked ? PartyTypeHelper.Natural : PartyTypeHelper.Legal),
                    new SqlParameter("@isForeign", chkForeign.Checked ? 1 : 0),
                    new SqlParameter("@isContractor", chkContractor.Checked ? 1 : 0),
                    new SqlParameter("@isClient", chkClient.Checked ? 1 : 0),
                    new SqlParameter("@nc", string.IsNullOrWhiteSpace(txtNationalCode.Text) ? DBNull.Value : txtNationalCode.Text.Trim()),
                    new SqlParameter("@fn", string.IsNullOrWhiteSpace(txtFirstName.Text) ? DBNull.Value : txtFirstName.Text.Trim()),
                    new SqlParameter("@ln", string.IsNullOrWhiteSpace(txtLastName.Text) ? DBNull.Value : txtLastName.Text.Trim()),
                    new SqlParameter("@cn", string.IsNullOrWhiteSpace(txtCompanyName.Text) ? DBNull.Value : txtCompanyName.Text.Trim()),
                    new SqlParameter("@nid", string.IsNullOrWhiteSpace(txtNationalId.Text) ? DBNull.Value : txtNationalId.Text.Trim()),
                    new SqlParameter("@ec", string.IsNullOrWhiteSpace(txtEconomicCode.Text) ? DBNull.Value : txtEconomicCode.Text.Trim()),
                    new SqlParameter("@phone", string.IsNullOrWhiteSpace(txtPhone.Text) ? DBNull.Value : txtPhone.Text.Trim()),
                    new SqlParameter("@addr", string.IsNullOrWhiteSpace(txtAddress.Text) ? DBNull.Value : txtAddress.Text.Trim())
                };

                if (_editingId.HasValue) parameters.Add(new SqlParameter("@id", _editingId.Value));

                await Task.Run(() => DbHelper.ExecuteNonQuery(_connectionString, query, parameters.ToArray()));
            }, "در حال ذخیره اطلاعات...");

            if (success)
            {
                ToastHelper.Show("اطلاعات با موفقیت ذخیره شد.", isSuccess: true);

                _isInitializing = true;
                await LoadPartiesSafeAsync();
                ClearForm();
                _isInitializing = false;
            }
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (!_editingId.HasValue) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                string checkSql = "SELECT (SELECT COUNT(1) FROM ContractorContracts WHERE PartyId = @id) + (SELECT COUNT(1) FROM ClientContracts WHERE PartyId = @id);";
                var count = Convert.ToInt32(await Task.Run(() => DbHelper.ExecuteScalar(_connectionString, checkSql, new SqlParameter("@id", _editingId.Value))));

                if (count > 0)
                {
                    throw new InvalidOperationException("این شخص دارای قرارداد ثبت‌شده است و قابل حذف نیست.");
                }

                string delBanksSql = "DELETE FROM PartyBankAccounts WHERE PartyId=@id;";
                await Task.Run(() => DbHelper.ExecuteNonQuery(_connectionString, delBanksSql, new SqlParameter("@id", _editingId.Value)));

                string delPartySql = "DELETE FROM Parties WHERE Id=@id;";
                await Task.Run(() => DbHelper.ExecuteNonQuery(_connectionString, delPartySql, new SqlParameter("@id", _editingId.Value)));
            }, "در حال حذف شخص...");

            if (success)
            {
                ToastHelper.Show("شخص با موفقیت حذف شد.", isSuccess: true);

                _isInitializing = true;
                await LoadPartiesSafeAsync();
                ClearForm();
                _isInitializing = false;
            }
        }
    }
}