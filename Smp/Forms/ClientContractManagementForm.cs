using System;
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
    public class ClientContractManagementForm : Form
    {
        private readonly ClientContractRepository _repo;
        private DataGridView dgv = null!;
        private TextBox txtSearch = null!;
        private Button btnAdd = null!, btnEdit = null!, btnReport = null!, btnDelete = null!, btnRefresh = null!;
        private Timer _searchTimer = null!;
        private Label lblCount = null!;

        // ✅ FIX P2: جلوگیری از تداخل
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public ClientContractManagementForm(string connectionString)
        {
            _repo = new ClientContractRepository(connectionString);

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
            UIHelper.SetupForm(this, "مدیریت قراردادهای کارفرما", new Size(1200, 700));
            this.MinimumSize = new Size(1000, 600);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));

            var pnlTop = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(15, 10, 15, 10) };

            lblCount = new Label { Text = "", Dock = DockStyle.Left, Width = 200, TextAlign = ContentAlignment.MiddleLeft, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            var searchFlow = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent };
            var lblSearch = new Label { Text = "🔍 جستجو:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(0, 8, 10, 0) };

            txtSearch = new TextBox
            {
                Width = 300,
                Font = UIHelper.PersianFont(11f),
                BackColor = UIHelper.WarmInputBackground,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "شماره قرارداد، نام کارفرما یا موضوع...",
                Margin = new Padding(0, 5, 0, 5)
            };

            _searchTimer = new Timer { Interval = 300 };
            _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await LoadDataSafeAsync(); };
            txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

            searchFlow.Controls.Add(lblSearch);
            searchFlow.Controls.Add(txtSearch);

            pnlTop.Controls.Add(lblCount);
            pnlTop.Controls.Add(searchFlow);

            dgv = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
            dgv.ApplyModernStyle();

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractNumber", HeaderText = "شماره قرارداد", FillWeight = 12 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractRow", HeaderText = "ردیف", FillWeight = 8 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DisplayName", HeaderText = "نام کارفرما", FillWeight = 25 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Subject", HeaderText = "موضوع", FillWeight = 25 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractDate", HeaderText = "تاریخ قرارداد", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EndDate", HeaderText = "تاریخ پایان", FillWeight = 15 });

            // 👈 اصلاح فرمت‌دهی اعداد در این بخش
            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                string colName = dgv.Columns[e.ColumnIndex].Name;
                string dataProp = dgv.Columns[e.ColumnIndex].DataPropertyName;

                if (dataProp == "ContractDate" || dataProp == "EndDate")
                {
                    if (e.Value is DateTime dt)
                    {
                        e.Value = DateHelper.ToPersianDate(dt);
                        e.FormattingApplied = true;
                    }
                    else if (e.Value == null || e.Value == DBNull.Value)
                    {
                        e.Value = "---";
                        e.FormattingApplied = true;
                    }
                }
                else if (dataProp == "Amount") // 👈 بررسی ستون مبلغ
                {
                    if (e.Value != null && e.Value != DBNull.Value)
                    {
                        // تبدیل ایمن هر نوع عددی به decimal و سپس فرمت‌دهی
                        if (decimal.TryParse(e.Value.ToString(), out decimal dec))
                        {
                            e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                            e.FormattingApplied = true;
                        }
                    }
                }
            };

            dgv.SelectionChanged += (s, e) => UpdateButtonsState();
            dgv.CellDoubleClick += (s, e) => BtnEdit_Click(null, EventArgs.Empty);

            var pnlBottom = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(15) };

            btnAdd = UIHelper.CreateButton("➕ افزودن قرارداد", UIHelper.WarmPrimary, 150, 40);
            btnAdd.Click += (s, e) => { using var form = new ClientContractEditForm(AppConfig.ConnectionString); if (form.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync(); };

            btnEdit = UIHelper.CreateButton("✏️ ویرایش", UIHelper.WarmSecondary, 120, 40);
            btnEdit.Click += BtnEdit_Click;

            btnReport = UIHelper.CreateButton("📊 گزارش مالی", UIHelper.SectionBlue, 140, 40);
            btnReport.Click += BtnReport_Click;

            btnDelete = UIHelper.CreateButton("🗑️ حذف", UIHelper.WarmDanger, 100, 40);
            btnDelete.Click += BtnDelete_Click;

            btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی", UIHelper.SectionGray, 120, 40);
            btnRefresh.Click += async (s, e) => await LoadDataSafeAsync();

            flow.Controls.Add(btnAdd);
            flow.Controls.Add(btnEdit);
            flow.Controls.Add(btnReport);
            flow.Controls.Add(btnDelete);
            flow.Controls.Add(btnRefresh);

            pnlBottom.Controls.Add(flow);

            mainLayout.Controls.Add(pnlTop, 0, 0);
            mainLayout.Controls.Add(dgv, 0, 1);
            mainLayout.Controls.Add(pnlBottom, 0, 2);

            Controls.Add(mainLayout);
        }

        private async Task LoadDataSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return; // ✅ FIX: جلوگیری از بارگذاری موازی هنگام تایپ سریع

            try
            {
                string search = txtSearch.Text.Trim();

                var (success, list) = await AsyncHelper.RunWithLoading(
                    this,
                    () => Task.Run(() => _repo.GetAll(search)),
                    "در حال بارگذاری لیست...");

                if (!success || list == null || IsDisposed) return;

                dgv.DataSource = list;
                UpdateButtonsState();

                if (lblCount != null && !IsDisposed)
                    lblCount.Text = $"تعداد: {NumberHelper.FormatThousands(list.Count, usePersianDigits: true)} قرارداد";
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClientContractManagementForm.LoadDataSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }

        private void UpdateButtonsState()
        {
            if (IsDisposed) return;
            bool hasSelection = dgv.CurrentRow != null;
            btnEdit.Enabled = hasSelection;
            btnReport.Enabled = hasSelection;
            btnDelete.Enabled = hasSelection;
        }

        private void BtnEdit_Click(object? sender, EventArgs e)
        {
            if (dgv.CurrentRow?.DataBoundItem is not ClientContract contract) return;
            using var form = new ClientContractEditForm(AppConfig.ConnectionString, contract);
            if (form.ShowDialog(this) == DialogResult.OK) _ = LoadDataSafeAsync();
        }

        private void BtnReport_Click(object? sender, EventArgs e)
        {
            if (dgv.CurrentRow?.DataBoundItem is not ClientContract contract) return;
            using var form = new ClientContractReportForm(AppConfig.ConnectionString, contract);
            form.ShowDialog(this);
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (dgv.CurrentRow?.DataBoundItem is not ClientContract contract || IsDisposed) return;

            if (!ModernMessageBox.AskConfirm($"آیا از حذف قرارداد «{contract.ContractNumber}» مطمئن هستید؟", "حذف قرارداد"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() => _repo.Delete(contract.Id));
            }, "در حال حذف...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("قرارداد با موفقیت حذف شد.", isSuccess: true);
                await LoadDataSafeAsync();
            }
        }
    }
}