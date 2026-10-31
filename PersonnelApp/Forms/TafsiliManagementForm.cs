using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class TafsiliManagementForm : Form
    {
        private readonly TafsiliRepository _repository;

        private DataGridView dgvTafsili = null!;
        private ComboBox cmbFilterType = null!;

        private TextBox txtCode = null!, txtFirstName = null!, txtLastName = null!, txtCompanyName = null!, txtNationalCode = null!, txtNationalId = null!, txtEconomicCode = null!;
        private ComboBox cmbType = null!, cmbPersonType = null!;
        private CheckBox chkIsActive = null!;
        private Button btnAdd = null!, btnUpdate = null!, btnDelete = null!, btnImport = null!, btnClear = null!;
        private Button btnRebuildNames = null!, btnClearAll = null!;

        private int _editingId = 0;

        // 👇 نقش‌ها 👇
        private readonly string[] _roles = {
            "کارمند",
            "پیمانکار",
            "کارفرما",
            "تأمین‌کننده",
            "شخص وابسته",
            "سهامدار",
            "بانک",
            "صندوق",
            "سایر بدهکاران",
            "سایر بستانکاران",
            "سایر نقش‌ها"
        };

        public TafsiliManagementForm(string connectionString)
        {
            _repository = new TafsiliRepository(connectionString);

            BuildUI();

            this.ApplyStandardHeader("مدیریت تفصیلی‌های حسابداری", new Size(1150, 830));
            ShowInTaskbar = false;

            // فعال‌سازی میانبرهای Ctrl+S (ثبت/ویرایش) و Esc (بستن)
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnAdd);

            // بارگذاری پس از نمایش فرم برای جلوگیری از فریز
            Shown += async (s, e) => await LoadTafsilisSafeAsync();
        }

        private void BuildUI()
        {
            // ==========================================
            // ۱. پنل بالا (فیلتر و دکمه‌های عملیات)
            // ==========================================
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(10) };

            var topFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            cmbFilterType = new ComboBox
            {
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UIHelper.PersianFont(11f),
                Margin = new Padding(5, 8, 15, 5)
            };
            var filterItems = new List<string> { "همه" };
            filterItems.AddRange(_roles);
            cmbFilterType.Items.AddRange(filterItems.ToArray());
            cmbFilterType.SelectedIndex = 0;
            cmbFilterType.SelectedIndexChanged += async (s, e) => await LoadTafsilisSafeAsync();

            btnImport = UIHelper.CreateButton("📥 ایمپورت از اکسل", UIHelper.SectionTeal, 170, 35);
            btnImport.Margin = new Padding(5, 3, 5, 5);
            btnImport.Click += BtnImport_Click;

            btnRebuildNames = UIHelper.CreateButton("🔨 بازسازی نام", UIHelper.SectionOrange, 170, 35);
            btnRebuildNames.Margin = new Padding(5, 3, 5, 5);
            btnRebuildNames.Click += BtnRebuildNames_Click;

            btnClearAll = UIHelper.CreateButton("🗑️ پاکسازی کل جدول", UIHelper.ActionRed, 170, 35);
            btnClearAll.Margin = new Padding(5, 3, 5, 5);
            btnClearAll.Click += BtnClearAll_Click;

            var lblHint = new Label
            {
                Text = "💡 ساختار اکسل: کد | نوع شخص | نام | نام خانوادگی | نام شرکت | کد ملی | شناسه ملی | نقش",
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = UIHelper.PersianFont(9f),
                Margin = new Padding(5, 13, 5, 5)
            };

            topFlow.Controls.Add(new Label { Text = "فیلتر نقش:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbFilterType);
            topFlow.Controls.Add(btnImport);
            topFlow.Controls.Add(btnRebuildNames);
            topFlow.Controls.Add(btnClearAll);
            topFlow.Controls.Add(lblHint);

            topPanel.Controls.Add(topFlow);

            // ==========================================
            // ۲. پنل ورودی (فرم ثبت/ویرایش)
            // ==========================================
            var inputPanel = new Panel { Dock = DockStyle.Top, Height = 290, Padding = new Padding(10) };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

            txtCode = new TextBox();

            cmbType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbType.Items.AddRange(_roles);
            cmbType.SelectedIndex = 0;

            cmbPersonType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPersonType.Items.AddRange(new object[] { "شخص حقیقی", "شخص حقوقی" });
            cmbPersonType.SelectedIndex = 0;

            txtFirstName = new TextBox();
            txtLastName = new TextBox();
            txtCompanyName = new TextBox();
            txtNationalCode = new TextBox();
            txtNationalId = new TextBox();
            txtEconomicCode = new TextBox();
            chkIsActive = new CheckBox { Text = "فعال", Checked = true };
            UIHelper.StyleToggle(chkIsActive);

            UIHelper.AddField(layout, "کد تفصیلی:", txtCode, 0, 0, 1);
            UIHelper.AddField(layout, "نقش:", cmbType, 0, 2, 3);
            UIHelper.AddField(layout, "نوع شخص:", cmbPersonType, 1, 0, 1);
            UIHelper.AddField(layout, "نام:", txtFirstName, 2, 0, 1);
            UIHelper.AddField(layout, "نام خانوادگی:", txtLastName, 2, 2, 3);
            UIHelper.AddField(layout, "نام شرکت:", txtCompanyName, 3, 0, 3);
            UIHelper.AddField(layout, "کد ملی (حقیقی):", txtNationalCode, 4, 0, 1);
            UIHelper.AddField(layout, "شناسه ملی (حقوقی):", txtNationalId, 4, 2, 3);
            UIHelper.AddField(layout, "کد اقتصادی:", txtEconomicCode, 5, 0, 3);

            // دکمه‌های عملیات فرم
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft };

            btnAdd = UIHelper.CreateButton("➕ ثبت جدید", UIHelper.ActionGreen, 130, 40);
            btnAdd.Click += BtnAdd_Click;

            btnUpdate = UIHelper.CreateButton("💾 ذخیره ویرایش", UIHelper.SectionBlue, 140, 40);
            btnUpdate.Enabled = false;
            btnUpdate.Click += BtnUpdate_Click;

            btnClear = UIHelper.CreateButton("🔄 پاک‌سازی فرم", UIHelper.SectionGray, 130, 40);
            btnClear.Click += (s, e) => ClearForm();

            btnPanel.Controls.Add(btnAdd);
            btnPanel.Controls.Add(btnUpdate);
            btnPanel.Controls.Add(btnClear);

            inputPanel.Controls.Add(btnPanel);
            inputPanel.Controls.Add(layout);

            // ==========================================
            // ۳. جدول (DataGridView)
            // ==========================================
            dgvTafsili = new DataGridView { Dock = DockStyle.Fill };
            dgvTafsili.ApplyModernStyle();
            dgvTafsili.SelectionChanged += (s, e) => SelectRow();

            // فرمت‌دهی وضعیت فعال/غیرفعال در گرید
            dgvTafsili.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgvTafsili.Columns[e.ColumnIndex].Name == "IsActive" && e.Value != null)
                {
                    bool isActive = Convert.ToBoolean(e.Value);
                    e.Value = isActive ? "✔ فعال" : "✖ غیرفعال";
                    e.CellStyle.ForeColor = isActive ? UIHelper.ActionGreen : UIHelper.WarmDanger;
                    e.FormattingApplied = true;
                }
            };

            // ==========================================
            // ۴. دکمه‌های پایین
            // ==========================================
            var bottomPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(10), FlowDirection = FlowDirection.LeftToRight };

            btnDelete = UIHelper.CreateButton("🗑️ حذف انتخاب‌شده", UIHelper.ActionRed, 180, 40);
            btnDelete.Click += BtnDelete_Click;

            bottomPanel.Controls.Add(btnDelete);

            // ترتیب اضافه کردن کنترل‌ها
            this.Controls.Add(dgvTafsili);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(inputPanel);
            this.Controls.Add(topPanel);
        }

        // ==========================================================
        // 📥 بارگذاری داده‌ها (Async)
        // ==========================================================
        private async Task LoadTafsilisSafeAsync()
        {
            string typeFilter = cmbFilterType.SelectedItem?.ToString() ?? "همه";
            if (typeFilter == "همه") typeFilter = "";

            var (success, list) = await AsyncHelper.RunWithLoading(
                this,
                () => _repository.GetAllAsync(typeFilter),
                "در حال بارگذاری لیست تفصیلی‌ها...");

            if (!success || list == null) return;

            dgvTafsili.DataSource = list;

            if (dgvTafsili.Columns.Contains("Id")) dgvTafsili.Columns["Id"].Visible = false;
            if (dgvTafsili.Columns.Contains("CreatedAt")) dgvTafsili.Columns["CreatedAt"].Visible = false;
            if (dgvTafsili.Columns.Contains("DisplayText")) dgvTafsili.Columns["DisplayText"].Visible = false;
            if (dgvTafsili.Columns.Contains("Name")) dgvTafsili.Columns["Name"].HeaderText = "نام / شرکت";
            if (dgvTafsili.Columns.Contains("PersonType")) dgvTafsili.Columns["PersonType"].HeaderText = "نوع شخص";
            if (dgvTafsili.Columns.Contains("NationalCode")) dgvTafsili.Columns["NationalCode"].HeaderText = "کد ملی";
            if (dgvTafsili.Columns.Contains("NationalId")) dgvTafsili.Columns["NationalId"].HeaderText = "شناسه ملی";
        }

        private void SelectRow()
        {
            if (dgvTafsili.CurrentRow?.DataBoundItem is Tafsili t)
            {
                _editingId = t.Id;
                txtCode.Text = t.Code;

                if (t.PersonType == "حقوقی" || t.PersonType == "شخص حقوقی")
                    cmbPersonType.SelectedItem = "شخص حقوقی";
                else
                    cmbPersonType.SelectedItem = "شخص حقیقی";

                txtFirstName.Text = t.FirstName;
                txtLastName.Text = t.LastName;
                txtCompanyName.Text = t.CompanyName;
                cmbType.SelectedItem = t.Type;
                txtNationalCode.Text = t.NationalCode;
                txtNationalId.Text = t.NationalId;
                txtEconomicCode.Text = t.EconomicCode;
                chkIsActive.Checked = t.IsActive;

                btnAdd.Enabled = false;
                btnUpdate.Enabled = true;
            }
        }

        private void ClearForm()
        {
            _editingId = 0;
            txtCode.Text = "";
            txtFirstName.Text = "";
            txtLastName.Text = "";
            txtCompanyName.Text = "";
            txtNationalCode.Text = "";
            txtNationalId.Text = "";
            txtEconomicCode.Text = "";
            chkIsActive.Checked = true;
            cmbType.SelectedIndex = 0;
            cmbPersonType.SelectedIndex = 0;
            btnAdd.Enabled = true;
            btnUpdate.Enabled = false;

            if (dgvTafsili.Rows.Count > 0)
            {
                dgvTafsili.ClearSelection();
                dgvTafsili.CurrentCell = null;
            }
        }

        // ==========================================================
        // ➕ ثبت جدید
        // ==========================================================
        private async void BtnAdd_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCode.Text))
            {
                ModernMessageBox.ShowWarning("کد تفصیلی الزامی است.");
                txtCode.Focus();
                return;
            }

            string personType = cmbPersonType.SelectedItem?.ToString() ?? "شخص حقیقی";

            if (personType == "شخص حقوقی" && string.IsNullOrWhiteSpace(txtCompanyName.Text))
            {
                ModernMessageBox.ShowWarning("برای شخص حقوقی، نام شرکت الزامی است.");
                txtCompanyName.Focus();
                return;
            }
            if (personType == "شخص حقیقی" && string.IsNullOrWhiteSpace(txtFirstName.Text) && string.IsNullOrWhiteSpace(txtLastName.Text))
            {
                ModernMessageBox.ShowWarning("برای شخص حقیقی، حداقل نام یا نام خانوادگی الزامی است.");
                txtFirstName.Focus();
                return;
            }

            var t = new Tafsili
            {
                Code = txtCode.Text.Trim(),
                PersonType = personType,
                FirstName = txtFirstName.Text.Trim(),
                LastName = txtLastName.Text.Trim(),
                CompanyName = txtCompanyName.Text.Trim(),
                Type = cmbType.SelectedItem?.ToString() ?? "کارمند",
                NationalCode = txtNationalCode.Text.Trim(),
                NationalId = txtNationalId.Text.Trim(),
                EconomicCode = txtEconomicCode.Text.Trim(),
                IsActive = chkIsActive.Checked
            };

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repository.AddAsync(t);
            }, "در حال ثبت تفصیلی جدید...");

            if (success)
            {
                ToastHelper.Show($"تفصیلی جدید ثبت شد ({personType} - {t.Type}).", isSuccess: true);
                ClearForm();
                await LoadTafsilisSafeAsync();
            }
        }

        // ==========================================================
        // 💾 ذخیره ویرایش
        // ==========================================================
        private async void BtnUpdate_Click(object? sender, EventArgs e)
        {
            if (_editingId == 0) return;

            string personType = cmbPersonType.SelectedItem?.ToString() ?? "شخص حقیقی";

            var t = new Tafsili
            {
                Id = _editingId,
                Code = txtCode.Text.Trim(),
                PersonType = personType,
                FirstName = txtFirstName.Text.Trim(),
                LastName = txtLastName.Text.Trim(),
                CompanyName = txtCompanyName.Text.Trim(),
                Type = cmbType.SelectedItem?.ToString() ?? "کارمند",
                NationalCode = txtNationalCode.Text.Trim(),
                NationalId = txtNationalId.Text.Trim(),
                EconomicCode = txtEconomicCode.Text.Trim(),
                IsActive = chkIsActive.Checked
            };

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repository.UpdateAsync(t);
            }, "در حال ذخیره تغییرات...");

            if (success)
            {
                ToastHelper.Show("تغییرات با موفقیت ذخیره شد.", isSuccess: true);
                ClearForm();
                await LoadTafsilisSafeAsync();
            }
        }

        // ==========================================================
        // 🗑️ حذف
        // ==========================================================
        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (_editingId == 0)
            {
                ModernMessageBox.ShowWarning("لطفاً یک ردیف را انتخاب کنید.");
                return;
            }

            if (!ModernMessageBox.AskConfirm("آیا از حذف این تفصیلی مطمئن هستید؟", "حذف تفصیلی")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repository.DeleteAsync(_editingId);
            }, "در حال حذف...");

            if (success)
            {
                ToastHelper.Show("تفصیلی با موفقیت حذف شد.", isSuccess: true);
                ClearForm();
                await LoadTafsilisSafeAsync();
            }
        }

        // ==========================================================
        // 🔨 بازسازی نام‌ها
        // ==========================================================
        private async void BtnRebuildNames_Click(object? sender, EventArgs e)
        {
            if (!ModernMessageBox.AskConfirm(
                "این عملیات فیلد «نام» را برای همه رکوردها بازسازی می‌کند.\n\n" +
                "• برای اشخاص حقوقی → نام = نام شرکت\n" +
                "• برای اشخاص حقیقی → نام = نام + نام خانوادگی\n\n" +
                "⚠️ توجه: نوع شخص (حقیقی/حقوقی) تغییر نمی‌کند.\n\n" +
                "آیا ادامه می‌دهید؟", "تأیید بازسازی نام"))
                return;

            int count = 0;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                count = await _repository.RebuildNamesAsync();
            }, "در حال بازسازی نام‌ها...");

            if (success)
            {
                ToastHelper.Show($"نام {NumberHelper.FormatThousands(count, usePersianDigits: true)} رکورد بازسازی شد.", isSuccess: true);
                await LoadTafsilisSafeAsync();
            }
        }

        // ==========================================================
        // 🗑️ پاکسازی کل جدول
        // ==========================================================
        private async void BtnClearAll_Click(object? sender, EventArgs e)
        {
            if (!ModernMessageBox.AskConfirm(
                "⚠️ هشدار جدی!\n\n" +
                "این عملیات تمام تفصیلی‌های ثبت شده را برای همیشه حذف می‌کند.\n" +
                "آیا مطمئن هستید؟", "تأیید حذف کل"))
                return;

            var result = MessageBox.Show(
                "آخرین تأیید!\n\nبرای تأیید، عبارت «حذف» را تایپ کنید.",
                "تأیید نهایی", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

            if (result != DialogResult.OK) return;

            string userInput = Microsoft.VisualBasic.Interaction.InputBox(
                "عبارت «حذف» را تایپ کنید:", "تأیید نهایی", "");

            if (userInput.Trim() != "حذف")
            {
                ModernMessageBox.ShowWarning("عبارت تأیید صحیح نیست. عملیات لغو شد.");
                return;
            }

            int count = 0;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                count = await _repository.ClearAllAsync();
            }, "در حال پاکسازی جدول...");

            if (success)
            {
                ToastHelper.Show($"{NumberHelper.FormatThousands(count, usePersianDigits: true)} رکورد حذف شد.", isSuccess: true);
                ClearForm();
                await LoadTafsilisSafeAsync();
            }
        }

        // ==========================================================
        // 📥 ایمپورت از اکسل
        // ==========================================================
        private async void BtnImport_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل تفصیلی‌ها" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            string message =
                "آیا تفصیلی‌های این فایل ایمپورت شوند؟\n\n" +
                "📋 ساختار فایل اکسل (از ردیف دوم):\n" +
                "   1. کد تفصیلی\n" +
                "   2. نوع شخص (شخص حقیقی / شخص حقوقی)\n" +
                "   3. نام\n" +
                "   4. نام خانوادگی\n" +
                "   5. نام شرکت\n" +
                "   6. کد ملی\n" +
                "   7. شناسه ملی\n" +
                "   8. نقش\n\n" +
                "💡 نقش‌های مجاز: کارمند، پیمانکار، کارفرما، تأمین‌کننده، شخص وابسته، سهامدار، بانک، صندوق، سایر بدهکاران، سایر بستانکاران، سایر نقش‌ها.";

            if (!ModernMessageBox.AskConfirm(message, "تأیید ایمپورت")) return;

            (int Inserted, int Updated, List<string> Errors)? result = null;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                try
                {
                    result = await _repository.ImportFromExcelAsync(ofd.FileName);
                }
                catch (IOException)
                {
                    throw new IOException("فایل اکسل انتخابی در حال حاضر در برنامه دیگری (مانند Excel) باز است.\n\nلطفاً ابتدا فایل را ببندید و سپس دوباره تلاش کنید.");
                }
            }, "در حال ایمپورت از اکسل...");

            if (!success || result == null) return;

            var (inserted, updated, errors) = result.Value;

            string resultMessage =
                $"✅ ایمپورت انجام شد!\n\n" +
                $"➕ جدید: {NumberHelper.FormatThousands(inserted, usePersianDigits: true)}\n" +
                $"🔄 به‌روزرسانی: {NumberHelper.FormatThousands(updated, usePersianDigits: true)}";

            if (errors.Count > 0)
            {
                resultMessage += $"\n\n⚠️ {NumberHelper.FormatThousands(errors.Count, usePersianDigits: true)} خطا:\n";
                resultMessage += string.Join("\n", errors.Take(10));
                if (errors.Count > 10)
                    resultMessage += $"\n... و {NumberHelper.FormatThousands(errors.Count - 10, usePersianDigits: true)} خطای دیگر.";
            }

            if (inserted == 0 && updated == 0)
                ModernMessageBox.ShowWarning(resultMessage, "نتیجه ایمپورت");
            else
            {
                ModernMessageBox.ShowInfo(resultMessage, "نتیجه ایمپورت");
                ToastHelper.Show("ایمپورت با موفقیت انجام شد.", isSuccess: true);
            }

            await LoadTafsilisSafeAsync();
        }
    }
}