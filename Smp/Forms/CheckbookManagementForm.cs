using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class CheckbookManagementForm : Form
    {
        private readonly string _connectionString;
        private readonly CompanyBankAccount _account;
        private readonly CheckbookRepository _repo;

        private TextBox txtSerial = null!, txtStartSayad = null!, txtStartCheck = null!;
        private NumericUpDown numPageCount = null!;
        private Label lblRange = null!;
        private DataGridView dgvCheckbooks = null!;
        private Button btnAdd = null!, btnDelete = null!;

        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public CheckbookManagementForm(string connectionString, CompanyBankAccount account)
        {
            _connectionString = connectionString;
            _account = account;
            _repo = new CheckbookRepository(connectionString);

            BuildUI();
            ShortcutHelper.ApplyStandardShortcuts(this);

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
            var (formLayout, grid, bottomPanel) = StandardFormHelper.SetupMasterDetailLayout(
                this, $"مدیریت دسته چک‌ها - {_account.Title}", new Size(850, 650));

            dgvCheckbooks = grid;
            dgvCheckbooks.AutoGenerateColumns = false; // ✅ تعریف دستی ستون‌ها
            dgvCheckbooks.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SerialNumber", HeaderText = "سریال", FillWeight = 25 });
            dgvCheckbooks.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SayadRangeDisplay", HeaderText = "محدوده صیاد", FillWeight = 35 });
            dgvCheckbooks.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RangeDisplay", HeaderText = "محدوده شماره چک‌ها", FillWeight = 25 });
            dgvCheckbooks.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PageCount", HeaderText = "تعداد برگ", FillWeight = 15 });

            // فیلدها
            txtSerial = new TextBox();
            txtStartSayad = new TextBox { MaxLength = 12, PlaceholderText = "مثلاً 050038890221" };
            txtStartCheck = new TextBox { PlaceholderText = "مثلاً 95901" };
            txtStartCheck.TextChanged += (s, e) => UpdateRangeDisplay();

            numPageCount = new NumericUpDown { Minimum = 1, Maximum = 500, Value = 100, TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmInputBackground };
            numPageCount.ValueChanged += (s, e) => UpdateRangeDisplay();

            lblRange = new Label { Text = "محدوده شماره چک: ---", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmPrimary };

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "سریال دسته چک:", txtSerial, 0, 0, 1);
            UIHelper.AddField(formLayout, "شناسه صیاد ۱۲ رقمی چک اول:", txtStartSayad, 0, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "شماره چک اول:", txtStartCheck, 1, 0, 1);
            UIHelper.AddField(formLayout, "تعداد برگ:", numPageCount, 1, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            formLayout.Controls.Add(lblRange, 0, 2);
            formLayout.SetColumnSpan(lblRange, 4);

            // دکمه‌ها
            btnAdd = UIHelper.CreateButton("➕ ثبت دسته چک جدید", UIHelper.ActionGreen, 180, 42);
            btnAdd.Click += async (s, e) => await SaveSafeAsync();

            btnDelete = UIHelper.CreateButton("🗑️ حذف انتخاب‌شده", UIHelper.ActionRed, 150, 42);
            btnDelete.Enabled = false;
            btnDelete.Click += async (s, e) => await DeleteSafeAsync();
            dgvCheckbooks.SelectionChanged += (s, e) => btnDelete.Enabled = dgvCheckbooks.CurrentRow != null;

            var btnClose = UIHelper.CreateButton("بستن", UIHelper.SectionGray, 100, 42);
            btnClose.Click += (s, e) => Close();

            var btnContainer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            btnContainer.Controls.Add(btnAdd);
            btnContainer.Controls.Add(btnDelete);
            btnContainer.Controls.Add(btnClose);
            bottomPanel.Controls.Add(btnContainer);
        }

        private void UpdateRangeDisplay()
        {
            if (long.TryParse(txtStartCheck.Text, out long start))
            {
                long end = start + (long)numPageCount.Value - 1;
                lblRange.Text = $"محدوده شماره چک: از {start} تا {end}";
            }
            else lblRange.Text = "محدوده شماره چک: ---";
        }

        private async Task LoadDataSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                var (success, list) = await AsyncHelper.RunWithLoading(this, () => _repo.GetByAccountIdAsync(_account.Id), "در حال بارگذاری دسته چک‌ها...");
                if (!success || list == null || IsDisposed) return;
                dgvCheckbooks.DataSource = list;
            }
            finally { try { _loadLock.Release(); } catch { } }
        }

        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (string.IsNullOrWhiteSpace(txtSerial.Text) || string.IsNullOrWhiteSpace(txtStartSayad.Text) || string.IsNullOrWhiteSpace(txtStartCheck.Text))
                {
                    ModernMessageBox.ShowWarning("پر کردن سریال، شناسه صیاد و شماره چک اول الزامی است."); return;
                }

                if (!long.TryParse(txtStartCheck.Text, out _)) { ModernMessageBox.ShowWarning("شماره چک اول باید عددی باشد."); return; }
                if (txtStartSayad.Text.Trim().Length != 12) { ModernMessageBox.ShowWarning("شناسه صیاد باید دقیقاً ۱۲ رقم باشد."); return; }

                var checkbook = new Checkbook
                {
                    AccountId = _account.Id,
                    SerialNumber = txtSerial.Text.Trim(),
                    StartSayadNumber = txtStartSayad.Text.Trim(),
                    StartCheckNumber = txtStartCheck.Text.Trim(),
                    PageCount = (int)numPageCount.Value
                };

                bool success = await AsyncHelper.RunWithLoading(this, async () => { await _repo.AddAsync(checkbook); }, "در حال ثبت...");
                if (success && !IsDisposed)
                {
                    ToastHelper.Show("دسته چک ثبت شد.", true);
                    txtSerial.Clear(); txtStartSayad.Clear(); txtStartCheck.Clear(); numPageCount.Value = 100; lblRange.Text = "محدوده شماره چک: ---";
                    await LoadDataSafeAsync();
                }
            }
            finally { try { _actionLock.Release(); } catch { } }
        }

        private async Task DeleteSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (dgvCheckbooks.CurrentRow?.DataBoundItem is not Checkbook selected) return;
                if (!ModernMessageBox.AskConfirm($"آیا از حذف دسته چک با سریال «{selected.SerialNumber}» مطمئن هستید؟", "تأیید حذف")) return;

                bool success = await AsyncHelper.RunWithLoading(this, async () => { await _repo.DeleteAsync(selected.Id); }, "در حال حذف...");
                if (success && !IsDisposed) { ToastHelper.Show("حذف شد.", true); await LoadDataSafeAsync(); }
            }
            finally { try { _actionLock.Release(); } catch { } }
        }
    }
}