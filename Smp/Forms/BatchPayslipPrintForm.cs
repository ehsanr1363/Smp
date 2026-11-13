using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class BatchPayslipPrintForm : Form
    {
        private readonly string _connectionString;

        private ComboBox cmbYear = null!, cmbMonth = null!;
        private Button btnLoad = null!, btnPrint = null!;
        private CheckBox chkSelectAll = null!;
        private DataGridView dgvEmployees = null!;
        private Label lblCount = null!;

        private BindingList<PayrollPrintRow> _rows = new();

        private PrintDocument? _printDoc;
        private int _printIndex;
        private List<PayrollPrintRow> _printQueue = new();

        // ✅ FIX P2: جلوگیری از تداخل و کرش
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public BatchPayslipPrintForm(string connectionString)
        {
            _connectionString = connectionString;

            BuildUI();
            this.ApplyStandardHeader("چاپ گروهی فیش‌های حقوقی", new Size(1050, 720));
            ShowInTaskbar = false;

            LoadDefaults();
            ShortcutHelper.ApplyStandardShortcuts(this);

            FormClosed += (s, e) => _printDoc?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void LoadDefaults()
        {
            try
            {
                var pc = new PersianCalendar();
                int y = pc.GetYear(DateTime.Now);
                int m = pc.GetMonth(DateTime.Now);

                if (cmbYear.Items.Contains(y)) cmbYear.SelectedItem = y;
                else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;

                if (cmbMonth.Items.Contains(m)) cmbMonth.SelectedItem = m;
                else if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
            }
            catch
            {
                if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;
                if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
            }
        }

        private void BuildUI()
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
            var topFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(5) };

            cmbYear = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 15, 5) };
            for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);

            cmbMonth = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 15, 5) };
            for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);

            btnLoad = UIHelper.CreateButton("نمایش لیست", UIHelper.SectionBlue, 130, 35);
            btnLoad.Margin = new Padding(5, 3, 5, 5);
            btnLoad.Click += async (s, e) => await BtnLoad_Click(s, e);

            chkSelectAll = new CheckBox { Text = "انتخاب / لغو همه", AutoSize = true, Font = UIHelper.PersianFont(11f), ForeColor = UIHelper.WarmText, Margin = new Padding(15, 13, 5, 5) };
            chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;

            topFlow.Controls.Add(new Label { Text = "سال:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbYear);
            topFlow.Controls.Add(new Label { Text = "ماه:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbMonth);
            topFlow.Controls.Add(btnLoad);
            topFlow.Controls.Add(chkSelectAll);
            topPanel.Controls.Add(topFlow);

            dgvEmployees = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.CellSelect };
            dgvEmployees.ApplyModernStyle();
            dgvEmployees.ReadOnly = false;

            dgvEmployees.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", DataPropertyName = "Selected", HeaderText = "چاپ", Width = 60 });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonnelCode", DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", ReadOnly = true, Width = 120 });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام و نام خانوادگی", ReadOnly = true, Width = 300 });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "NetPay", DataPropertyName = "NetPay", HeaderText = "خالص پرداختی (ریال)", ReadOnly = true, Width = 200 });

            dgvEmployees.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgvEmployees.Columns[e.ColumnIndex].Name == "NetPay" && e.Value is decimal d)
                {
                    e.Value = NumberHelper.FormatThousands(d, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            };

            dgvEmployees.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvEmployees.IsCurrentCellDirty && dgvEmployees.CurrentCell is DataGridViewCheckBoxCell)
                {
                    dgvEmployees.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(10) };

            btnPrint = UIHelper.CreateButton("🖨️ چاپ فیش‌های انتخاب شده", UIHelper.ActionGreen, 260, 40);
            btnPrint.Dock = DockStyle.Right;
            btnPrint.Enabled = false;
            btnPrint.Click += BtnPrint_Click;
            bottomPanel.Controls.Add(btnPrint);

            lblCount = new Label { Dock = DockStyle.Left, AutoSize = true, ForeColor = Color.Gray, Font = UIHelper.PersianFont(9.5f), Padding = new Padding(5, 12, 0, 0) };
            bottomPanel.Controls.Add(lblCount);

            Controls.Add(dgvEmployees);
            Controls.Add(bottomPanel);
            Controls.Add(topPanel);
        }

        private async Task BtnLoad_Click(object? sender, EventArgs e)
        {
            if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null || IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                int year = Convert.ToInt32(cmbYear.SelectedItem);
                int month = Convert.ToInt32(cmbMonth.SelectedItem);

                var payrollRepo = new PayrollRepository(_connectionString);
                bool hasSaved = await payrollRepo.HasPayrollsForMonthAsync(year, month);
                if (!hasSaved)
                {
                    ModernMessageBox.ShowWarning(
                        "برای این ماه هیچ فیش حقوقی ذخیره نشده است.\n\n" +
                        "ابتدا از منوی «پردازش حقوق»، فیش‌ها را محاسبه و ذخیره کنید، سپس دوباره تلاش کنید.");
                    return;
                }

                List<PayrollPrintRow>? newRows = null;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var settingsRepo = new SalarySettingsRepository(_connectionString);
                    var settings = await settingsRepo.GetByYearAsync(year);

                    if (settings == null)
                        throw new InvalidOperationException($"تنظیمات حقوق سال {year} یافت نشد.");

                    var empRepo = new EmployeeRepository(_connectionString);
                    var employees = await empRepo.GetEmployeesWithDetailsAsync("", false);

                    var contractRepo = new EmployeeContractRepository(_connectionString);
                    var activeContracts = new Dictionary<int, EmployeeContract>();
                    foreach (var emp in employees)
                    {
                        if (await contractRepo.GetActiveContractByEmployeeIdAsync(emp.Id) is EmployeeContract contract)
                        {
                            activeContracts[emp.Id] = contract;
                        }
                    }

                    var attRepo = new AttendanceRepository(_connectionString);
                    var attendances = await attRepo.GetAttendanceForMonthAsync(year, month);

                    var payrollsDt = await payrollRepo.GetSavedPayrollsAsync(year, month);

                    if (IsDisposed) return; // ✅ FIX: جلوگیری از کرش

                    var rows = new List<PayrollPrintRow>();
                    foreach (DataRow row in payrollsDt.Rows)
                    {
                        rows.Add(new PayrollPrintRow
                        {
                            Selected = true,
                            EmployeeId = Convert.ToInt32(row["EmployeeId"]),
                            PersonnelCode = row["PersonnelCode"].ToString() ?? string.Empty,
                            EmployeeName = row["EmployeeName"].ToString() ?? string.Empty,
                            NetPay = Convert.ToDecimal(row["NetPay"]),
                            PayrollId = Convert.ToInt32(row["Id"])
                        });
                    }

                    newRows = rows;
                }, "در حال بارگذاری اطلاعات...");

                // ✅ FIX: بررسی IsDisposed بعد از await
                if (success && newRows != null && !IsDisposed)
                {
                    _rows = new BindingList<PayrollPrintRow>(newRows);
                    dgvEmployees.DataSource = _rows;
                    UpdateCountLabel();
                    btnPrint.Enabled = _rows.Any();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "BatchPayslipPrintForm.BtnLoad_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private void ChkSelectAll_CheckedChanged(object? sender, EventArgs e)
        {
            if (_rows == null || _rows.Count == 0 || IsDisposed) return;

            foreach (var row in _rows)
            {
                row.Selected = chkSelectAll.Checked;
            }

            dgvEmployees.Refresh();
            UpdateCountLabel();
        }

        private void BtnPrint_Click(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            var selectedRows = _rows.Where(r => r.Selected).ToList();
            if (selectedRows.Count == 0)
            {
                ModernMessageBox.ShowWarning("هیچ کارمندی برای چاپ انتخاب نشده است.");
                return;
            }

            _printQueue = selectedRows;
            _printIndex = 0;

            _printDoc = new PrintDocument();
            _printDoc.PrintPage += PrintDocument_PrintPage;
            _printDoc.BeginPrint += (s, args) => _printIndex = 0;

            using (var printDialog = new PrintDialog())
            {
                printDialog.Document = _printDoc;
                if (printDialog.ShowDialog() == DialogResult.OK)
                {
                    _printDoc.Print();
                }
            }
        }

        private void PrintDocument_PrintPage(object? sender, PrintPageEventArgs e)
        {
            if (_printIndex >= _printQueue.Count) return;

            var row = _printQueue[_printIndex];
            var g = e.Graphics;
            if (g == null) return;

            float yPos = 50f;
            float leftMargin = 50f;
            float rightMargin = (float)e.MarginBounds.Right - 50f;
            float centerX = (float)e.MarginBounds.Width / 2f;

            using var fontTitle = new Font("Tahoma", 16, FontStyle.Bold);
            using var fontText = new Font("Tahoma", 12);

            var format = new StringFormat { Alignment = StringAlignment.Near, FormatFlags = StringFormatFlags.DirectionRightToLeft };

            var titlePoint = new PointF(centerX - 40f, yPos);
            g.DrawString("فیش حقوقی", fontTitle, Brushes.Black, titlePoint);

            yPos += 40f;

            g.DrawString($"کد پرسنلی: {row.PersonnelCode}", fontText, Brushes.Black, new PointF(rightMargin, yPos), format);
            g.DrawString($"نام و نام خانوادگی: {row.EmployeeName}", fontText, Brushes.Black, new PointF(rightMargin - 200f, yPos), format);

            yPos += 30f;
            g.DrawString($"خالص پرداختی: {NumberHelper.FormatThousands(row.NetPay, usePersianDigits: true)} ریال", fontText, Brushes.Black, new PointF(rightMargin, yPos), format);

            g.DrawLine(Pens.Gray, leftMargin, yPos + 20f, rightMargin, yPos + 20f);

            _printIndex++;
            e.HasMorePages = _printIndex < _printQueue.Count;
        }

        private void UpdateCountLabel()
        {
            if (lblCount == null || IsDisposed) return;
            int selectedCount = _rows.Count(r => r.Selected);
            lblCount.Text = $"تعداد کل: {_rows.Count} | انتخاب شده: {selectedCount}";
        }
    }

    public class PayrollPrintRow : INotifyPropertyChanged
    {
        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set { _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected))); }
        }

        public int EmployeeId { get; set; }
        public int PayrollId { get; set; }
        public string PersonnelCode { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public decimal NetPay { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}