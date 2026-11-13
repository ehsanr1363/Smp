using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace SMP.Forms
{
    public class BulkCashTransactionForm : Form
    {
        private readonly CashTransactionRepository _repository;
        private readonly BankAccountRepository _bankRepo;
        private readonly LookupRepository _lookupRepo;
        private readonly string _connectionString;

        private ComboBox cmbAccount = null!, cmbDirection = null!, cmbPaymentMethod = null!;
        private TextBox txtDate = null!;
        private DataGridView dgvBulk = null!;
        private Button btnSave = null!;

        // لیست‌های مورد نیاز برای کشویی‌ها
        private List<ComboItem> _clientContracts = new();
        private List<ComboItem> _contractorContracts = new();
        private List<string> _categoriesList = new();
        private List<string> _costTypesList = new();

        // ✅ دیکشنری برای پیدا کردن سریع تفصیلی بر اساس PartyId
        private readonly Dictionary<int, (string Code, string Name)> _partyToTafsiliMap = new();

        private readonly SemaphoreSlim _actionLock = new(1, 1);
        private bool _isUpdatingCells = false; // جلوگیری از حلقه بی‌نهایت در رویدادها

        private class ComboItem
        {
            public int Id { get; set; }
            public string Display { get; set; } = "";
            public int? ClientContractId { get; set; } // برای پیمانکار
            public int PartyId { get; set; } // برای پیدا کردن تفصیلی
        }

        public BulkCashTransactionForm(string connectionString)
        {
            _connectionString = connectionString;
            _repository = new CashTransactionRepository(connectionString);
            _bankRepo = new BankAccountRepository(connectionString);
            _lookupRepo = new LookupRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("ثبت گروهی حواله / تراکنش", new Size(1350, 780));

            Shown += async (s, e) =>
            {
                try
                {
                    await LoadContractsAsync();
                    await LoadTafsiliMapAsync(); // ✅ ساخت نقشه تفصیلی‌ها
                    await LoadAccountsAsync();
                    await LoadCategoriesAsync();
                    await LoadCostTypesAsync();
                    AddEmptyRows(10);
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "BulkCashTransactionForm.Shown");
                    if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری فرم:\n" + ex.Message);
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
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10), RightToLeft = RightToLeft.Yes };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            // ═══════ پنل بالای فرم ═══════
            var pnlTopContainer = new Panel { Dock = DockStyle.Fill };

            var tblTop = new TableLayoutPanel
            {
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                ColumnCount = 8,
                RowCount = 1,
                RightToLeft = RightToLeft.Yes,
                Padding = new Padding(0, 10, 20, 10)
            };

            tblTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
            tblTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            tblTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            tblTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            tblTop.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblAccount = new Label { Text = "حساب مبدا:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 5, 5) };
            cmbAccount = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 8, 20, 5) };

            var lblDirection = new Label { Text = "جهت:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 5, 5) };
            cmbDirection = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 8, 20, 5) };
            // ✅ استفاده از ثابت‌های DirectionTypes
            cmbDirection.Items.AddRange(new[] { DirectionTypes.Outgoing, DirectionTypes.Incoming });
            cmbDirection.SelectedIndex = 0;

            var lblMethod = new Label { Text = "روش:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 5, 5) };
            cmbPaymentMethod = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 8, 20, 5) };
            cmbPaymentMethod.Items.AddRange(new[] { "حواله", "نقدی", "پایا / ساتنا" });
            cmbPaymentMethod.SelectedIndex = 0;

            var lblDate = new Label { Text = "تاریخ:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 5, 5) };
            txtDate = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 5), TextAlign = HorizontalAlignment.Center };
            DateHelper.ApplyDateMask(txtDate);
            txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);

            tblTop.Controls.Add(lblAccount, 0, 0);
            tblTop.Controls.Add(cmbAccount, 1, 0);
            tblTop.Controls.Add(lblDirection, 2, 0);
            tblTop.Controls.Add(cmbDirection, 3, 0);
            tblTop.Controls.Add(lblMethod, 4, 0);
            tblTop.Controls.Add(cmbPaymentMethod, 5, 0);
            tblTop.Controls.Add(lblDate, 6, 0);
            tblTop.Controls.Add(txtDate, 7, 0);

            pnlTopContainer.Controls.Add(tblTop);

            // ═══════ گرید ثبت گروهی ═══════
            dgvBulk = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RightToLeft = RightToLeft.Yes,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                Font = UIHelper.PersianFont(11f)
            };
            dgvBulk.EnableHeadersVisualStyles = false;
            dgvBulk.ColumnHeadersDefaultCellStyle.BackColor = UIHelper.WarmPanelBackground;

            // ستون‌های گرید
            var colScope = new DataGridViewComboBoxColumn
            {
                Name = "PaymentScope",
                HeaderText = "نوع پرداخت",
                FillWeight = 18,
                FlatStyle = FlatStyle.Flat
            };
            colScope.Items.AddRange(new object[] {
                "اداری",
                "خرید مستقیم پروژه",
                "پیمانکار بدون قرارداد",
                "پیمانکار دارای قرارداد"
            });

            var colContract = new DataGridViewComboBoxColumn
            {
                Name = "Contract",
                HeaderText = "قرارداد / پروژه",
                DisplayMember = "Display",
                ValueMember = "Id",
                FillWeight = 22
            };

            var colCategory = new DataGridViewComboBoxColumn
            {
                Name = "Category",
                HeaderText = "دسته‌بندی",
                FillWeight = 12
            };

            var colCostType = new DataGridViewComboBoxColumn
            {
                Name = "CostType",
                HeaderText = "ماهیت هزینه",
                FillWeight = 12
            };

            var colTafsiliName = new DataGridViewTextBoxColumn
            {
                Name = "TafsiliName",
                HeaderText = "طرف حساب (تفصیلی)",
                ReadOnly = true,
                FillWeight = 18,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            var colSearch = new DataGridViewButtonColumn
            {
                Name = "SearchTafsili",
                HeaderText = "",
                Text = "🔍",
                UseColumnTextForButtonValue = true,
                Width = 40,
                FillWeight = 4
            };

            var colTafsiliCode = new DataGridViewTextBoxColumn { Name = "TafsiliCode", Visible = false };
            var colAmount = new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } };
            var colDesc = new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "بابت / شرح", FillWeight = 18, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } };

            dgvBulk.Columns.AddRange(new DataGridViewColumn[] { colScope, colContract, colCategory, colCostType, colTafsiliName, colSearch, colTafsiliCode, colAmount, colDesc });

            // رویدادها
            dgvBulk.CellContentClick += DgvBulk_CellContentClick;
            dgvBulk.CellEndEdit += DgvBulk_CellEndEdit;
            dgvBulk.CurrentCellDirtyStateChanged += DgvBulk_CurrentCellDirtyStateChanged;
            dgvBulk.CellValueChanged += DgvBulk_CellValueChanged;

            // ═══════ پنل پایین (دکمه ذخیره) ═══════
            var pnlBottom = new Panel { Dock = DockStyle.Fill };
            btnSave = UIHelper.CreateButton("💾 ذخیره همه ردیف‌ها", UIHelper.ActionGreen, 220, 45);
            btnSave.Anchor = AnchorStyles.None;
            pnlBottom.Controls.Add(btnSave);
            pnlBottom.Resize += (s, e) => { btnSave.Left = (pnlBottom.Width - btnSave.Width) / 2; btnSave.Top = (pnlBottom.Height - btnSave.Height) / 2; };
            btnSave.Click += async (s, e) => await BtnSave_Click(s, e);

            mainLayout.Controls.Add(pnlTopContainer, 0, 0);
            mainLayout.Controls.Add(dgvBulk, 0, 1);
            mainLayout.Controls.Add(pnlBottom, 0, 2);
            this.Controls.Add(mainLayout);
        }

        private void DgvBulk_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (dgvBulk.IsCurrentCellDirty && dgvBulk.CurrentCell is DataGridViewComboBoxCell)
            {
                dgvBulk.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ✅ رویداد کلیدی: وقتی مقدار سلولی عوض می‌شود (مثلاً انتخاب قرارداد)
        // ═══════════════════════════════════════════════════════════════
        private void DgvBulk_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _isUpdatingCells) return;

            var row = dgvBulk.Rows[e.RowIndex];
            string colName = dgvBulk.Columns[e.ColumnIndex].Name;

            // اگر ستون نوع پرداخت تغییر کرد
            if (colName == "PaymentScope")
            {
                var scopeCell = row.Cells["PaymentScope"];
                var contractCell = row.Cells["Contract"] as DataGridViewComboBoxCell;
                var costTypeCell = row.Cells["CostType"] as DataGridViewComboBoxCell;

                if (contractCell == null) return;

                string scope = scopeCell.Value?.ToString() ?? "";

                _isUpdatingCells = true;
                try
                {
                    contractCell.Value = null;
                    row.Cells["TafsiliCode"].Value = "";
                    row.Cells["TafsiliName"].Value = "";

                    if (scope == "اداری")
                    {
                        contractCell.DataSource = new List<string> { "---" };
                        contractCell.Value = "---";
                        contractCell.ReadOnly = true;
                        if (costTypeCell != null) costTypeCell.Style.BackColor = Color.White;
                    }
                    else if (scope == "خرید مستقیم پروژه")
                    {
                        contractCell.DataSource = _clientContracts;
                        contractCell.DisplayMember = "Display";
                        contractCell.ValueMember = "Id";
                        contractCell.ReadOnly = false;
                        if (costTypeCell != null) costTypeCell.Style.BackColor = UIHelper.WarmWarningBackground;
                    }
                    else if (scope == "پیمانکار بدون قرارداد")
                    {
                        contractCell.DataSource = _clientContracts; // چون به پروژه وصل است
                        contractCell.DisplayMember = "Display";
                        contractCell.ValueMember = "Id";
                        contractCell.ReadOnly = false;
                        if (costTypeCell != null) costTypeCell.Style.BackColor = UIHelper.WarmWarningBackground;
                    }
                    else if (scope == "پیمانکار دارای قرارداد")
                    {
                        contractCell.DataSource = _contractorContracts;
                        contractCell.DisplayMember = "Display";
                        contractCell.ValueMember = "Id";
                        contractCell.ReadOnly = false;
                        if (costTypeCell != null) costTypeCell.Style.BackColor = UIHelper.WarmWarningBackground;
                    }
                }
                finally
                {
                    _isUpdatingCells = false;
                }
            }
            // ✅ اگر ستون قرارداد تغییر کرد (پر کردن خودکار تفصیلی)
            else if (colName == "Contract")
            {
                string scope = row.Cells["PaymentScope"].Value?.ToString() ?? "";

                // فقط برای حالت‌هایی که طرف حساب مشخص است، تفصیلی را پر کن
                if (scope == "پیمانکار دارای قرارداد" || scope == "پیمانکار بدون قرارداد" || scope == "خرید مستقیم پروژه")
                {
                    if (row.Cells["Contract"].Value != null)
                    {
                        int contractId = Convert.ToInt32(row.Cells["Contract"].Value);
                        int? partyId = null;

                        if (scope == "پیمانکار دارای قرارداد")
                        {
                            partyId = _contractorContracts.FirstOrDefault(c => c.Id == contractId)?.PartyId;
                        }
                        else if (scope == "پیمانکار بدون قرارداد" || scope == "خرید مستقیم پروژه")
                        {
                            // برای خرید مستقیم و پیمانکار بدون قرارداد، طرف حساب دستی وارد می‌شود
                            partyId = null;
                        }

                        if (partyId.HasValue && _partyToTafsiliMap.TryGetValue(partyId.Value, out var tafsili))
                        {
                            _isUpdatingCells = true;
                            try
                            {
                                row.Cells["TafsiliCode"].Value = tafsili.Code;
                                row.Cells["TafsiliName"].Value = tafsili.Name;
                            }
                            finally
                            {
                                _isUpdatingCells = false;
                            }
                        }
                        else if (partyId.HasValue)
                        {
                            ToastHelper.Show("برای این پیمانکار، حساب تفصیلی متناظر پیدا نشد. لطفاً از دکمه 🔍 استفاده کنید.", false);
                        }
                    }
                }
            }
        }

        private void DgvBulk_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (dgvBulk.Columns[e.ColumnIndex].Name == "Amount")
            {
                string val = dgvBulk.Rows[e.RowIndex].Cells["Amount"].Value?.ToString() ?? "";
                decimal amount = NumberHelper.ParseFormatted(val);

                if (amount > 0)
                    dgvBulk.Rows[e.RowIndex].Cells["Amount"].Value = amount.ToString("N0");
                else
                    dgvBulk.Rows[e.RowIndex].Cells["Amount"].Value = "";
            }
        }

        private void DgvBulk_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvBulk.Columns[e.ColumnIndex].Name == "SearchTafsili")
            {
                using (var form = new TafsiliSelectionForm(_connectionString))
                {
                    if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed)
                    {
                        dgvBulk.Rows[e.RowIndex].Cells["TafsiliName"].Value = form.SelectedName;
                        dgvBulk.Rows[e.RowIndex].Cells["TafsiliCode"].Value = form.SelectedCode;
                    }
                }
            }
        }

        private void AddEmptyRows(int count)
        {
            if (IsDisposed) return;
            for (int i = 0; i < count; i++)
            {
                int rowIndex = dgvBulk.Rows.Add();
                var row = dgvBulk.Rows[rowIndex];
                row.Cells["PaymentScope"].Value = "اداری"; // پیش‌فرض
                row.Cells["Category"].Value = _categoriesList.FirstOrDefault() ?? "";
                row.Cells["CostType"].Value = _costTypesList.FirstOrDefault() ?? "";
                row.Cells["Amount"].Value = "";
                row.Cells["Description"].Value = "";
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // بارگذاری داده‌ها
        // ═══════════════════════════════════════════════════════════════
        private async Task LoadContractsAsync()
        {
            // ۱. قراردادهای کارفرما
            var cRepo = new ClientContractRepository(_connectionString);
            var cList = await Task.Run(() => cRepo.GetAll());
            _clientContracts = cList.Select(c => new ComboItem
            {
                Id = c.Id,
                Display = $"{c.ContractNumber} - {c.Subject}"
            }).ToList();

            // ۲. قراردادهای پیمانکار
            var conRepo = new ContractorContractRepository(_connectionString);
            var conList = await Task.Run(() => conRepo.GetAll());
            _contractorContracts = conList.Select(c => new ComboItem
            {
                Id = c.Id,
                Display = $"{c.DisplayName} - قرارداد {c.ContractNumber}",
                ClientContractId = c.ClientContractId,
                PartyId = c.PartyId
            }).ToList();
        }

        // ✅ ساخت نقشه تفصیلی برای پر کردن خودکار
        private async Task LoadTafsiliMapAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // دریافت تمام طرفین (Parties) و تفصیلی‌های (Tafsilis) مرتبط
                string query = @"
                    SELECT t.Code, t.Name, p.Id AS PartyId
                    FROM Tafsilis t
                    INNER JOIN Parties p ON (t.NationalCode = p.NationalCode AND p.NationalCode IS NOT NULL) 
                                         OR (t.NationalId = p.NationalId AND p.NationalId IS NOT NULL)";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                _partyToTafsiliMap.Clear();
                while (await reader.ReadAsync())
                {
                    string code = reader["Code"].ToString()!;
                    string name = reader["Name"].ToString()!;
                    int partyId = Convert.ToInt32(reader["PartyId"]);
                    _partyToTafsiliMap[partyId] = (code, name);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "BulkCashTransactionForm.LoadTafsiliMap");
            }
        }

        private async Task LoadAccountsAsync()
        {
            var accounts = (await _bankRepo.GetActiveAsync()).Where(a => !a.HasCheckbook).ToList();
            if (IsDisposed) return;

            cmbAccount.DataSource = accounts;
            cmbAccount.DisplayMember = "DisplayTitle";
            cmbAccount.ValueMember = "Id";
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var items = await _lookupRepo.GetByCategoryAsync("TransactionCategory");
                if (IsDisposed) return;

                _categoriesList = items.Select(i => i.Value).ToList();
                if (_categoriesList.Count == 0) _categoriesList.AddRange(new[] { "عملیاتی", "اداری", "پرسنلی", "هزینه بانکی", "سایر" });

                if (dgvBulk.Columns["Category"] is DataGridViewComboBoxColumn colCat)
                    colCat.DataSource = _categoriesList;
            }
            catch { }
        }

        private async Task LoadCostTypesAsync()
        {
            try
            {
                var items = await _lookupRepo.GetByCategoryAsync("CostType");
                if (IsDisposed) return;

                _costTypesList = items.Select(i => i.Value).ToList();
                if (_costTypesList.Count == 0) _costTypesList.AddRange(new[] { "خرید کالا", "خرید خدمات", "سایر" });

                if (dgvBulk.Columns["CostType"] is DataGridViewComboBoxColumn colCost)
                    colCost.DataSource = _costTypesList;
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════
        // ✅ ذخیره گروهی (اصلاح شده با استفاده از BulkInsertAsync)
        // ═══════════════════════════════════════════════════════════════
        private async Task BtnSave_Click(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (cmbAccount.SelectedValue == null) { ModernMessageBox.ShowWarning("حساب مبدا را انتخاب کنید."); return; }

                DateTime? commonDate = DateHelper.ParsePersianDate(txtDate.Text);
                if (commonDate == null) { ModernMessageBox.ShowWarning("تاریخ وارد شده معتبر نیست."); return; }

                var selectedAcc = (CompanyBankAccount)cmbAccount.SelectedItem!;
                var transactionsToSave = new List<CashTransaction>();
                var errors = new List<string>();

                foreach (DataGridViewRow row in dgvBulk.Rows)
                {
                    if (row.IsNewRow) continue;

                    string amountStr = row.Cells["Amount"].Value?.ToString() ?? "";
                    decimal amount = NumberHelper.ParseFormatted(amountStr);
                    if (amount <= 0) continue; // ردیف‌های خالی یا بدون مبلغ را نادیده بگیر

                    string scopeDisplay = row.Cells["PaymentScope"].Value?.ToString() ?? "اداری";
                    string tafsiliCode = row.Cells["TafsiliCode"].Value?.ToString() ?? "";
                    string tafsiliName = row.Cells["TafsiliName"].Value?.ToString() ?? "";
                    string costType = row.Cells["CostType"].Value?.ToString() ?? "سایر";
                    string category = row.Cells["Category"].Value?.ToString() ?? "";
                    string desc = row.Cells["Description"].Value?.ToString() ?? "";

                    if (string.IsNullOrWhiteSpace(tafsiliCode)) { errors.Add($"ردیف {row.Index + 1}: طرف حساب (تفصیلی) انتخاب نشده است."); continue; }

                    // تبدیل متن نمایشی به Scope اصلی
                    string paymentScope;
                    int? clientContractId = null;
                    int? contractorContractId = null;
                    string contractTitle = "هزینه‌های اداری / متفرقه";

                    if (scopeDisplay == "اداری")
                    {
                        paymentScope = PaymentScopes.Administrative;
                        costType = "سایر"; // برای اداری همیشه سایر
                    }
                    else if (scopeDisplay == "خرید مستقیم پروژه" || scopeDisplay == "پیمانکار بدون قرارداد")
                    {
                        paymentScope = scopeDisplay == "خرید مستقیم پروژه" ? PaymentScopes.DirectPurchase : PaymentScopes.InformalContractor;
                        if (row.Cells["Contract"].Value == null) { errors.Add($"ردیف {row.Index + 1}: قرارداد کارفرما انتخاب نشده است."); continue; }
                        if (costType == "سایر") { errors.Add($"ردیف {row.Index + 1}: ماهیت هزینه (کالا/خدمات) را انتخاب کنید."); continue; }

                        clientContractId = Convert.ToInt32(row.Cells["Contract"].Value);
                        contractTitle = _clientContracts.FirstOrDefault(c => c.Id == clientContractId)?.Display ?? "";
                    }
                    else // پیمانکار دارای قرارداد
                    {
                        paymentScope = PaymentScopes.ContractorContract;
                        if (row.Cells["Contract"].Value == null) { errors.Add($"ردیف {row.Index + 1}: قرارداد پیمانکار انتخاب نشده است."); continue; }
                        if (costType == "سایر") { errors.Add($"ردیف {row.Index + 1}: ماهیت هزینه (کالا/خدمات) را انتخاب کنید."); continue; }

                        contractorContractId = Convert.ToInt32(row.Cells["Contract"].Value);
                        var con = _contractorContracts.FirstOrDefault(c => c.Id == contractorContractId);
                        clientContractId = con?.ClientContractId;
                        contractTitle = con?.Display ?? "";
                    }

                    transactionsToSave.Add(new CashTransaction
                    {
                        AccountId = selectedAcc.Id,
                        AccountTitle = selectedAcc.Title,
                        PaymentScope = paymentScope,
                        ContractId = clientContractId,
                        ContractorContractId = contractorContractId,
                        ContractTitle = contractTitle,
                        TransactionDate = commonDate.Value,
                        Amount = amount,
                        Direction = cmbDirection.Text, // از مقادیر DirectionTypes استفاده می‌کند
                        TransactionType = cmbPaymentMethod.Text,
                        Category = category,
                        CostType = costType,
                        TafsiliCode = tafsiliCode,
                        TafsiliTitle = tafsiliName,
                        RecipientName = tafsiliName,
                        Description = desc
                    });
                }

                if (errors.Any())
                {
                    string errorMsg = "لطفاً خطاهای زیر را برطرف کنید:\n\n" + string.Join("\n", errors.Take(10));
                    if (errors.Count > 10) errorMsg += $"\n... و {errors.Count - 10} خطای دیگر.";
                    ModernMessageBox.ShowWarning(errorMsg, "خطای اعتبارسنجی");
                    return;
                }

                if (transactionsToSave.Count == 0)
                {
                    ModernMessageBox.ShowWarning("هیچ ردیف معتبری برای ذخیره وجود ندارد.");
                    return;
                }

                this.Enabled = false;
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    // ✅ استفاده از درج گروهی (Bulk Insert) برای سرعت بالا و اتمیک بودن
                    await _repository.BulkInsertAsync(transactionsToSave);
                }, "در حال ذخیره گروهی...");

                if (success && !IsDisposed)
                {
                    ModernMessageBox.ShowInfo($"تمامی {transactionsToSave.Count} ردیف با موفقیت ذخیره شد.", "موفق");
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "BulkCashTransactionForm.BtnSave_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره گروهی:\n" + ex.Message);
            }
            finally
            {
                if (!IsDisposed) this.Enabled = true;
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}