using SMP.Data;
using SMP.Helpers;
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
    public class ExpensePickerForm : Form
    {
        private readonly string _connectionString;
        private readonly int _contractId;
        private readonly int? _statementId;

        private DataGridView dgvExpenses = null!;
        private TextBox txtProfitPercent = null!;
        private Label lblTotalExpenses = null!, lblProfitAmount = null!;
        private Button btnApply = null!;

        public decimal SelectedGrossAmount { get; private set; } = 0;

        public List<int> SelectedTransactionIds { get; } = new();
        public List<int> SelectedProjectExpenseIds { get; } = new();
        public List<int> SelectedCheckIds { get; } = new();

        private decimal _baseTotal = 0;

        public ExpensePickerForm(string connectionString, int contractId, int? statementId = null)
        {
            _connectionString = connectionString;
            _contractId = contractId;
            _statementId = statementId;

            BuildUI();
            string title = _statementId.HasValue ? "دستیار محاسبه صورت وضعیت (ویرایش)" : "دستیار محاسبه صورت وضعیت از روی هزینه‌ها";
            this.ApplyStandardHeader(title, new Size(950, 650));

            Shown += async (s, e) => await LoadExpensesAsync();
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            var lblHint = new Label
            {
                Text = _statementId.HasValue ? "هزینه‌های زیر شامل هزینه‌های قبلیِ این صورت وضعیت است. تیک‌ها را بر اساس نیاز تغییر دهید:" : "فقط هزینه‌هایی که در صورت وضعیت‌های قبلی استفاده نشده‌اند در اینجا نمایش داده می‌شوند. مواردی که باید در این صورت وضعیت لحاظ شوند را تیک بزنید:",
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(10.5f, FontStyle.Bold),
                ForeColor = UIHelper.WarmErrorText,
                TextAlign = ContentAlignment.MiddleRight
            };

            dgvExpenses = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                BackgroundColor = Color.White,
                RightToLeft = RightToLeft.Yes
            };
            dgvExpenses.ApplyModernStyle();
            dgvExpenses.ReadOnly = false;

            dgvExpenses.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "انتخاب", Width = 60, ReadOnly = false });
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "SourceType", Visible = false });
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "تاریخ", ReadOnly = true, FillWeight = 15 });
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "نوع هزینه", ReadOnly = true, FillWeight = 25 });
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "توضیحات", ReadOnly = true, FillWeight = 30 });
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "مبلغ (ریال)", ReadOnly = true, FillWeight = 15 });

            dgvExpenses.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvExpenses.IsCurrentCellDirty && dgvExpenses.CurrentCell is DataGridViewCheckBoxCell)
                    dgvExpenses.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            dgvExpenses.CellValueChanged += (s, e) => CalculateTotals();

            var pnlCalc = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
            pnlCalc.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            pnlCalc.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            pnlCalc.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));

            lblTotalExpenses = new Label { Text = "هزینه‌های انتخاب شده: 0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };

            var lblProfitTitle = new Label { Text = "درصد سود (%):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(11f) };
            txtProfitPercent = new TextBox { Text = "0", Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(12f, FontStyle.Bold), BackColor = UIHelper.WarmInputBackground };
            txtProfitPercent.TextChanged += (s, e) => CalculateTotals();

            lblProfitAmount = new Label { Text = "مبلغ نهایی: 0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(13f, FontStyle.Bold), ForeColor = UIHelper.ActionGreen };

            pnlCalc.Controls.Add(new Label { Text = "جمع هزینه‌ها:", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill }, 0, 0);
            pnlCalc.Controls.Add(lblTotalExpenses, 0, 1);
            pnlCalc.Controls.Add(lblProfitTitle, 1, 0);
            pnlCalc.Controls.Add(txtProfitPercent, 1, 1);
            pnlCalc.Controls.Add(new Label { Text = "مبلغ ناخالص صورت وضعیت:", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, ForeColor = UIHelper.ActionGreen }, 2, 0);
            pnlCalc.Controls.Add(lblProfitAmount, 2, 1);

            btnApply = UIHelper.CreateButton("✔ تایید و انتقال به صورت وضعیت", UIHelper.ActionGreen, 250, 45);
            btnApply.Anchor = AnchorStyles.None;
            btnApply.Click += (s, e) =>
            {
                if (_baseTotal == 0)
                {
                    ModernMessageBox.ShowWarning("لطفاً حداقل یک هزینه را انتخاب کنید.");
                    return;
                }
                SelectedGrossAmount = _baseTotal + (_baseTotal * (NumberHelper.ParseFormatted(txtProfitPercent.Text) / 100m));
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            mainLayout.Controls.Add(lblHint, 0, 0);
            mainLayout.Controls.Add(dgvExpenses, 0, 1);
            mainLayout.Controls.Add(pnlCalc, 0, 2);
            mainLayout.Controls.Add(btnApply, 0, 3);
            this.Controls.Add(mainLayout);
        }

        private async Task LoadExpensesAsync()
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT 'Tx' AS [SourceType], t.Id, t.TransactionDate AS [Date], 
                           CASE 
                               WHEN t.PaymentScope = N'DirectPurchase' THEN N'خرید مستقیم'
                               WHEN t.PaymentScope = N'InformalContractor' THEN N'پیمانکار (بدون قرارداد)'
                               WHEN t.PaymentScope = N'ContractorContract' THEN N'پیمانکار (رسمی)'
                               ELSE N'سایر'
                           END AS [Type],
                           ISNULL(t.Description, ISNULL(t.RecipientName, N'---')) AS [Description],
                           t.Amount,
                           CASE WHEN t.ClientStatementId IS NOT NULL THEN 1 ELSE 0 END AS IsCurrentlyLinked
                    FROM CashTransactions t
                    WHERE t.ContractId = @cid AND t.Direction = N'پرداختی' 
                      AND (t.ClientStatementId IS NULL OR t.ClientStatementId = @stId)
                    
                    UNION ALL
                    
                    SELECT 'Exp' AS [SourceType], e.Id, e.InvoiceDate AS [Date], 
                           N'فاکتور پروژه' AS [Type],
                           ISNULL(e.Description, N'---') AS [Description],
                           e.Amount,
                           CASE WHEN e.ClientStatementId IS NOT NULL THEN 1 ELSE 0 END AS IsCurrentlyLinked
                    FROM ProjectExpenses e
                    WHERE e.ClientContractId = @cid 
                      AND (e.ClientStatementId IS NULL OR e.ClientStatementId = @stId)

                    UNION ALL

                    SELECT 'Chk' AS [SourceType], c.Id, c.DueDate AS [Date], 
                           N'چک پرداختی' AS [Type],
                           ISNULL(c.Description, ISNULL(c.RecipientName, N'---')) AS [Description],
                           c.Amount,
                           CASE WHEN c.ClientStatementId IS NOT NULL THEN 1 ELSE 0 END AS IsCurrentlyLinked
                    FROM CashChecks c
                    WHERE c.ContractId = @cid AND c.CheckType = N'پرداختی'
                      AND c.Status NOT IN (N'باطل شده', N'برگشتی')
                      AND (c.ClientStatementId IS NULL OR c.ClientStatementId = @stId)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@cid", _contractId);
                cmd.Parameters.AddWithValue("@stId", (object?)_statementId ?? DBNull.Value);

                using var adapter = new Microsoft.Data.SqlClient.SqlDataAdapter(cmd);
                var dt = new System.Data.DataTable();
                await Task.Run(() => adapter.Fill(dt));

                if (IsDisposed) return;

                dgvExpenses.Rows.Clear();
                foreach (System.Data.DataRow row in dt.Rows)
                {
                    bool isCurrentlyLinked = Convert.ToInt32(row["IsCurrentlyLinked"]) == 1;
                    dgvExpenses.Rows.Add(
                        isCurrentlyLinked,
                        row["Id"].ToString(),
                        row["SourceType"].ToString(),
                        DateHelper.ToPersianDate(Convert.ToDateTime(row["Date"])),
                        row["Type"].ToString(),
                        row["Description"].ToString(),
                        Convert.ToDecimal(row["Amount"])
                    );
                }

                dgvExpenses.CellFormatting += (s, e) => {
                    if (e.RowIndex >= 0 && dgvExpenses.Columns[e.ColumnIndex].Name == "Amount" && e.Value is decimal d)
                    {
                        e.Value = NumberHelper.FormatThousands(d, usePersianDigits: true);
                        e.FormattingApplied = true;
                    }
                };

            }, "در حال دریافت هزینه‌های پروژه...");

            if (success) CalculateTotals();
        }

        private void CalculateTotals()
        {
            _baseTotal = 0;
            SelectedTransactionIds.Clear();
            SelectedProjectExpenseIds.Clear();
            SelectedCheckIds.Clear();

            foreach (DataGridViewRow row in dgvExpenses.Rows)
            {
                if (Convert.ToBoolean(row.Cells["Selected"].Value) == true)
                {
                    _baseTotal += Convert.ToDecimal(row.Cells["Amount"].Value);
                    int id = Convert.ToInt32(row.Cells["Id"].Value);
                    string sourceType = row.Cells["SourceType"].Value.ToString()!;

                    if (sourceType == "Tx") SelectedTransactionIds.Add(id);
                    else if (sourceType == "Exp") SelectedProjectExpenseIds.Add(id);
                    else if (sourceType == "Chk") SelectedCheckIds.Add(id);
                }
            }

            lblTotalExpenses.Text = NumberHelper.FormatThousands(_baseTotal, usePersianDigits: true) + " ریال";
            decimal profitPercent = NumberHelper.ParseFormatted(txtProfitPercent.Text);
            decimal finalAmount = _baseTotal + (_baseTotal * (profitPercent / 100m));
            lblProfitAmount.Text = NumberHelper.FormatThousands(finalAmount, usePersianDigits: true) + " ریال";
        }
    }
}