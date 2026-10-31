using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using PersonnelApp.Data;
using PersonnelApp.Helpers;

namespace PersonnelApp.Forms
{
    public class LookupManagementForm : Form
    {
        private readonly string _connectionString;
        private readonly LookupRepository _repository;

        // دیکشنری برای نگاشت نام فارسی به کلید انگلیسی دیتابیس
        private readonly Dictionary<string, string> _categories = new()
        {
            { "Gender", "جنسیت" },
            { "Education", "تحصیلات" },
            { "PersonnelType", "نوع پرسنل" },
            { "WorkLocation", "محل خدمت" },
            { "MilitaryStatus", "وضعیت نظام وظیفه" },
            { "MaritalStatus", "وضعیت تاهل" },
            { "DailyWageCategory", "دسته‌بندی کارگر روزمزد" }
        };

        private TabControl tabControl = null!;

        public LookupManagementForm(string connectionString)
        {
            _connectionString = connectionString;
            _repository = new LookupRepository(connectionString);

            BuildModernUI();
            ShortcutHelper.ApplyStandardShortcuts(this);
            ShowInTaskbar = false;
        }

        private void BuildModernUI()
        {
            UIHelper.SetupForm(this, "مدیریت لیست‌های کشویی (Lookups)", new Size(750, 600));

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(160, 40)
            };

            // ساخت یک تب برای هر دسته‌بندی
            foreach (var category in _categories)
            {
                string dbKey = category.Key;
                string faName = category.Value;

                var tabPage = new TabPage(faName) { BackColor = UIHelper.WarmBackground };
                tabPage.Controls.Add(CreateLookupTab(dbKey, faName));
                tabControl.TabPages.Add(tabPage);
            }

            this.Controls.Add(tabControl);
        }

        private Control CreateLookupTab(string categoryKey, string categoryName)
        {
            // چیدمان اصلی تب (ورودی‌ها بالا، جدول پایین)
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F)); // بخش ورودی
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // جدول
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F)); // فاصله

            // ─── بخش ورودی ───
            var inputPanel = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(10) };

            var inputLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F)); // لیبل
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // تکست باکس
            inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F)); // دکمه

            var lblValue = new Label { Text = "مقدار جدید:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            var txtValue = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(12f), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(5) };

            var btnAdd = UIHelper.CreateButton("➕ افزودن", UIHelper.ActionGreen, 110, 35);
            btnAdd.Dock = DockStyle.Fill;
            btnAdd.Margin = new Padding(5);

            inputLayout.Controls.Add(lblValue, 0, 0);
            inputLayout.Controls.Add(txtValue, 1, 0);
            inputLayout.Controls.Add(btnAdd, 2, 0);

            inputPanel.Controls.Add(inputLayout);

            // ─── جدول ───
            var dgv = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, MultiSelect = false };
            dgv.ApplyModernStyle();
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", DataPropertyName = "Value", HeaderText = "مقدار / عنوان", FillWeight = 100, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });

            // ─── دکمه حذف ───
            var btnDelete = UIHelper.CreateButton("🗑️ حذف انتخاب‌شده", UIHelper.ActionRed, 150, 35);
            btnDelete.Enabled = false;
            btnDelete.Dock = DockStyle.Right;

            var bottomPanel = new Panel { Dock = DockStyle.Fill };
            bottomPanel.Controls.Add(btnDelete);

            // ─── رویدادها ───

            // لود کردن داده‌های اولیه
            async Task LoadData()
            {
                try
                {
                    var list = await Task.Run(() => _repository.GetByCategory(categoryKey));
                    dgv.DataSource = list;
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, $"LookupManagementForm.LoadData({categoryKey})");
                    ModernMessageBox.ShowError($"خطا در بارگذاری لیست {categoryName}:\n{ex.Message}");
                }
            }

            // رویداد افزودن
            btnAdd.Click += async (s, e) =>
            {
                string val = txtValue.Text.Trim();
                if (string.IsNullOrWhiteSpace(val))
                {
                    ModernMessageBox.ShowWarning("لطفاً مقداری برای افزودن وارد کنید.");
                    return;
                }

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    // بررسی تکراری نبودن
                    string checkQuery = "SELECT COUNT(1) FROM LookupItems WHERE Category = @cat AND Value = @val";
                    var exists = Convert.ToInt32(await Task.Run(() => DbHelper.ExecuteScalar(_connectionString, checkQuery, new SqlParameter("@cat", categoryKey), new SqlParameter("@val", val)))) > 0;

                    if (exists) throw new InvalidOperationException("این مقدار قبلاً در این دسته‌بندی ثبت شده است.");

                    _repository.Add(new Models.LookupItem { Category = categoryKey, Value = val });
                }, "در حال ذخیره...");

                if (success)
                {
                    ToastHelper.Show("مقدار با موفقیت اضافه شد.", isSuccess: true);
                    txtValue.Clear();
                    await LoadData();
                }
            };

            // رویداد حذف
            btnDelete.Click += async (s, e) =>
            {
                if (dgv.CurrentRow?.DataBoundItem is not Models.LookupItem item) return;

                if (!ModernMessageBox.AskConfirm($"آیا از حذف «{item.Value}» مطمئن هستید؟", "تأیید حذف")) return;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    await Task.Run(() => _repository.Delete(item.Id));
                }, "در حال حذف...");

                if (success)
                {
                    ToastHelper.Show("مقدار حذف شد.", isSuccess: true);
                    await LoadData();
                }
            };

            // انتخاب ردیف و فعال شدن دکمه حذف
            dgv.SelectionChanged += (s, e) => btnDelete.Enabled = dgv.CurrentRow != null;

            // با دابل کلیک روی ردیف، متن داخل تکست باکس کپی شود (برای راحتی ویرایش)
            dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgv.Rows[e.RowIndex].DataBoundItem is Models.LookupItem item)
                {
                    txtValue.Text = item.Value;
                    txtValue.Focus();
                }
            };

            layout.Controls.Add(inputPanel, 0, 0);
            layout.Controls.Add(dgv, 0, 1);
            layout.Controls.Add(bottomPanel, 0, 2);

            // لود کردن داده‌ها هنگام اولین نمایش تب
            tabControl.Selecting += async (s, e) =>
            {
                if (e.TabPage == tabControl.TabPages[categoryName] || e.TabPage.Text == categoryName)
                {
                    if (dgv.DataSource == null) await LoadData();
                }
            };

            // اجرای اولیه در صورت نیاز
            _ = LoadData();

            return layout;
        }
    }
}