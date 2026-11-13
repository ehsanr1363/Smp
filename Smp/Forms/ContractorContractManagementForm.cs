using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Models;
using SMP.Helpers;
using Timer = System.Windows.Forms.Timer;

namespace SMP.Forms
{
    public class ContractorContractManagementForm : Form
    {
        private readonly string _connectionString;
        private readonly ContractorContractRepository _repo;

        private DataGridView dgv = null!;
        private TextBox txtSearch = null!;
        private Label lblCount = null!;
        private Timer _searchTimer = null!;

        private Button btnAdd = null!;
        private Button btnEdit = null!;
        private Button btnStatements = null!;
        private Button btnDelete = null!;
        private Button btnRefresh = null!;
        private Button btnDownloadTemplate = null!;
        private Button btnImportExcel = null!;

        // ✅ FIX P2: جلوگیری از تداخل
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public ContractorContractManagementForm(string connectionString)
        {
            _connectionString = connectionString;
            _repo = new ContractorContractRepository(connectionString);

            BuildUI();

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);
            ShowInTaskbar = false;

            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        // ✅ FIX P2: Dispose تایمر و قفل
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _searchTimer?.Dispose(); } catch { }
                try { _loadLock?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, "مدیریت قراردادهای پیمانکاری", new Size(1300, 750));
            this.MinimumSize = new Size(1000, 600);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(15, 10, 15, 10), BorderStyle = BorderStyle.FixedSingle };

            lblCount = new Label { Text = "", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            UIHelper.StyleLabel(lblCount);
            lblCount.Font = UIHelper.PersianFont(12f, FontStyle.Bold);

            var txtWrap = new Panel { Dock = DockStyle.Right, Width = 300, BackColor = UIHelper.WarmInputBackground, Padding = new Padding(10, 0, 10, 0), BorderStyle = BorderStyle.FixedSingle };
            txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = UIHelper.WarmInputBackground,
                ForeColor = UIHelper.WarmText,
                Font = UIHelper.PersianFont(11f),
                Dock = DockStyle.Fill,
                PlaceholderText = "جستجو..."
            };

            _searchTimer = new Timer { Interval = 300 };
            _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await LoadDataSafeAsync(); };
            txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

            txtWrap.Controls.Add(txtSearch);

            pnlTop.Controls.Add(lblCount);
            pnlTop.Controls.Add(txtWrap);

            dgv = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(10) };
            dgv.ApplyModernStyle();

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "PartyType", HeaderText = "نوع", DataPropertyName = "PartyType", FillWeight = 8 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Identifier", HeaderText = "شناسه (کد/شناسه ملی)", DataPropertyName = "Identifier", FillWeight = 14 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "DisplayName", HeaderText = "نام / شرکت", DataPropertyName = "DisplayName", FillWeight = 18 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractNumber", HeaderText = "شماره قرارداد", DataPropertyName = "ContractNumber", FillWeight = 12 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Subject", HeaderText = "موضوع", DataPropertyName = "Subject", FillWeight = 24 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "مبلغ (ریال)", DataPropertyName = "Amount", FillWeight = 12 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractDate", HeaderText = "تاریخ قرارداد", DataPropertyName = "ContractDate", FillWeight = 12 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndDate", HeaderText = "تاریخ پایان", DataPropertyName = "EndDate", FillWeight = 12 });

            dgv.CellFormatting += Dgv_CellFormatting;
            dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) btnEdit.PerformClick(); };
            dgv.SelectionChanged += (s, e) => UpdateButtonsState();

            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(10) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };

            btnAdd = UIHelper.CreateButton("➕ افزودن قرارداد", UIHelper.WarmPrimary, 160);
            btnAdd.Click += (s, e) => { using var f = new ContractorContractEditForm(_connectionString); if (f.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync(); };

            btnEdit = UIHelper.CreateButton("✏️ ویرایش", UIHelper.WarmSecondary, 130);
            btnEdit.Click += (s, e) =>
            {
                if (dgv.CurrentRow?.DataBoundItem is not ContractorContract c) return;
                using var f = new ContractorContractEditForm(_connectionString, c);
                if (f.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync();
            };

            btnStatements = UIHelper.CreateButton("📄 صورت وضعیت‌ها", UIHelper.SectionBlue, 150);
            btnStatements.Click += (s, e) =>
            {
                if (dgv.CurrentRow?.DataBoundItem is not ContractorContract c) return;
                using var form = new ContractorStatementReportForm(_connectionString, c);
                form.ShowDialog(this);
            };

            btnDelete = UIHelper.CreateButton("🗑️ حذف", UIHelper.WarmDanger, 110);
            btnDelete.Click += BtnDelete_Click;

            btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی", UIHelper.WarmSecondary, 130);
            btnRefresh.Click += async (s, e) => await LoadDataSafeAsync();

            btnDownloadTemplate = UIHelper.CreateButton("📥 دانلود قالب اکسل", UIHelper.WarmSecondary, 160);
            btnDownloadTemplate.Click += (s, e) => ExcelTemplateGenerator.GenerateContractorContractsTemplate();

            btnImportExcel = UIHelper.CreateButton("📤 ایمپورت از اکسل", UIHelper.WarmSecondary, 160);
            btnImportExcel.Click += BtnImportExcel_Click;

            flow.Controls.Add(btnAdd);
            flow.Controls.Add(btnEdit);
            flow.Controls.Add(btnStatements);
            flow.Controls.Add(btnDelete);
            flow.Controls.Add(btnRefresh);
            flow.Controls.Add(btnDownloadTemplate);
            flow.Controls.Add(btnImportExcel);
            pnlBottom.Controls.Add(flow);

            Controls.Add(dgv);
            Controls.Add(pnlBottom);
            Controls.Add(pnlTop);

            UpdateButtonsState();
        }

        private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string colName = dgv.Columns[e.ColumnIndex].DataPropertyName;

            if (colName == "ContractDate" || colName == "EndDate")
            {
                if (e.Value is DateTime dt)
                {
                    e.Value = DateHelper.ToPersianDate(dt);
                    e.FormattingApplied = true;
                }
                else if (e.Value == null || e.Value == DBNull.Value)
                {
                    e.Value = "";
                    e.FormattingApplied = true;
                }
            }
            else if (colName == "Amount" && e.Value is decimal amount)
            {
                e.Value = NumberHelper.FormatThousands(amount, usePersianDigits: true);
                e.FormattingApplied = true;
            }
        }

        private void UpdateButtonsState()
        {
            if (IsDisposed) return;
            bool hasSelection = dgv.CurrentRow != null;
            btnEdit.Enabled = hasSelection;
            btnStatements.Enabled = hasSelection;
            btnDelete.Enabled = hasSelection;
        }

        private async Task LoadDataSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return; // ✅ FIX

            try
            {
                string search = txtSearch.Text.Trim();

                var (success, list) = await AsyncHelper.RunWithLoading(
                    this,
                    () => Task.Run(() => _repo.GetAll(search)),
                    "در حال بارگذاری لیست قراردادها...");

                if (!success || list == null || IsDisposed) return;

                dgv.DataSource = list;
                if (!IsDisposed) lblCount.Text = $"مجموع: {NumberHelper.FormatThousands(list.Count, usePersianDigits: true)} قرارداد";
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ContractorContractManagementForm.LoadDataSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (dgv.CurrentRow?.DataBoundItem is not ContractorContract c || IsDisposed) return;

            if (!ModernMessageBox.AskConfirm($"آیا از حذف قرارداد «{c.ContractNumber}» مطمئن هستید؟", "حذف قرارداد"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() => _repo.Delete(c.Id));
            }, "در حال حذف قرارداد...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("قرارداد با موفقیت حذف شد.", isSuccess: true);
                await LoadDataSafeAsync();
            }
        }

        private async void BtnImportExcel_Click(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل قراردادهای پیمانکاری" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            DataTable? dt = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                dt = await ExcelImportHelper.ParseContractorContractsExcelAsync(ofd.FileName);
            }, "در حال خواندن فایل اکسل...");

            if (!success || dt == null || IsDisposed) return;

            if (dt.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("داده‌ای در فایل اکسل یافت نشد.");
                return;
            }

            ShowContractorContractsPreviewForm(dt);
        }

        private void ShowContractorContractsPreviewForm(DataTable dt)
        {
            var previewForm = new Form();
            UIHelper.SetupForm(previewForm, "پیش‌نمایش قراردادهای پیمانکاری", new Size(1400, 700));
            previewForm.ShowInTaskbar = false;

            var dgvPreview = new DataGridView { Dock = DockStyle.Fill, DataSource = dt };
            dgvPreview.ApplyModernStyle();

            dgvPreview.DataBindingComplete += (s, e) => {
                foreach (DataGridViewRow row in dgvPreview.Rows)
                {
                    var statusVal = row.Cells["وضعیت"].Value?.ToString();

                    if (statusVal != "معتبر")
                    {
                        row.DefaultCellStyle.BackColor = UIHelper.WarmErrorBackground;
                        row.DefaultCellStyle.ForeColor = UIHelper.WarmErrorText;
                    }
                    else
                    {
                        row.DefaultCellStyle.BackColor = UIHelper.WarmSuccessBackground;
                    }
                }
            };

            var btnSave = UIHelper.CreateButton("انتقال قراردادهای معتبر به دیتابیس", UIHelper.WarmPrimary, 300, 50);
            btnSave.Dock = DockStyle.Bottom;

            btnSave.Click += async (s, ev) =>
            {
                var validRows = dt.Rows.Cast<DataRow>().Where(r => r["وضعیت"].ToString() == "معتبر").CopyToDataTable();

                if (validRows.Rows.Count == 0)
                {
                    ModernMessageBox.ShowWarning("هیچ ردیف معتبری برای ثبت وجود ندارد.");
                    return;
                }

                if (!ModernMessageBox.AskConfirm($"آیا از ثبت {NumberHelper.FormatThousands(validRows.Rows.Count, usePersianDigits: true)} قرارداد معتبر مطمئن هستید؟", "تایید ایمپورت"))
                    return;

                string msg = "";
                bool saveSuccess = await AsyncHelper.RunWithLoading(previewForm, async () =>
                {
                    msg = await ExcelImportHelper.SaveContractorContractsToDbAsync(validRows);
                }, "در حال ثبت قراردادها در دیتابیس...");

                if (saveSuccess && !IsDisposed)
                {
                    ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                    ToastHelper.Show("ایمپورت با موفقیت انجام شد.", isSuccess: true);
                    previewForm.Close();
                    await LoadDataSafeAsync();
                }
            };

            previewForm.Controls.Add(dgvPreview);
            previewForm.Controls.Add(btnSave);
            previewForm.ShowDialog(this);
        }
    }
}