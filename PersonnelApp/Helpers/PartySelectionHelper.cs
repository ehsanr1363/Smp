using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using PersonnelApp.Forms;

namespace PersonnelApp.Helpers
{
    public static class PartySelectionHelper
    {
        /// <summary>
        /// پنجره انتخاب طرف قرارداد (پیمانکار یا کارفرما) را باز می‌کند.
        /// </summary>
        /// <param name="connectionString">رشته اتصال به دیتابیس</param>
        /// <param name="isContractor">true = پیمانکاران، false = کارفرمایان</param>
        /// <param name="owner">فرم والدی که این پنجره روی آن باز می‌شود</param>
        /// <returns>تاپل (Id, Name) — در صورت انصراف (0, "")</returns>
        public static (int Id, string Name) ShowSelectionDialog(
            string connectionString,
            bool isContractor,
            IWin32Window owner = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("رشته اتصال خالی است.", nameof(connectionString));

            int selectedId = 0;
            string selectedName = "";

            using var form = new Form
            {
                Text = isContractor ? "انتخاب پیمانکار" : "انتخاب کارفرما",
                Size = new Size(900, 620),
                MinimumSize = new Size(700, 500),
                StartPosition = owner != null
                    ? FormStartPosition.CenterParent
                    : FormStartPosition.CenterScreen,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Font = UIHelper.PersianFont(12f),
                MaximizeBox = false,
                MinimizeBox = false,
                FormBorderStyle = FormBorderStyle.Sizable,
                BackColor = UIHelper.WarmBackground,
                ShowInTaskbar = false
            };

            // ==================================================
            // پنل بالا: جستجو + دکمه مدیریت
            // ==================================================
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                Padding = new Padding(12, 12, 12, 8),
                BackColor = Color.FromArgb(241, 245, 249)
            };

            var btnManage = new Button
            {
                Text = "➕ مدیریت / تعریف جدید",
                Dock = DockStyle.Right,
                Width = 190,
                BackColor = UIHelper.SectionBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnManage.FlatAppearance.BorderSize = 0;

            var txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(12f),
                PlaceholderText = "جستجو بر اساس نام یا کد ملی / شناسه ملی...",
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0)
            };

            var txtWrap = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 4, 12, 4)
            };
            txtWrap.Controls.Add(txtSearch);

            pnlTop.Controls.Add(txtWrap);
            pnlTop.Controls.Add(btnManage);

            // ==================================================
            // جدول
            // ==================================================
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                RightToLeft = RightToLeft.Yes,
                RowTemplate = { Height = 36 },
                ColumnHeadersHeight = 42,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };

            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(226, 232, 240);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);
            dgv.ColumnHeadersDefaultCellStyle.Font = UIHelper.PersianFont(11.5f, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dgv.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 41, 59);
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);

            // ==================================================
            // پنل پایین: دکمه‌های انتخاب/انصراف
            // ==================================================
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                Padding = new Padding(12, 10, 12, 10),
                BackColor = Color.FromArgb(249, 250, 251)
            };

            var btnSelect = new Button
            {
                Text = "✔ انتخاب",
                DialogResult = DialogResult.OK,
                Dock = DockStyle.Left,
                Width = 140,
                BackColor = UIHelper.ActionGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnSelect.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "انصراف",
                DialogResult = DialogResult.Cancel,
                Dock = DockStyle.Left,
                Width = 110,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(80, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Font = UIHelper.PersianFont(11f),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);

            pnlBottom.Controls.Add(btnCancel);
            pnlBottom.Controls.Add(btnSelect);

            // ==================================================
            // بارگذاری داده
            // ==================================================
            DataTable dt = null;

            void LoadData()
            {
                try
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();

                    string filterColumn = isContractor ? "IsContractor = 1" : "IsClient = 1";
                    using var cmd = new SqlCommand(
                        $"SELECT Id, PartyType, NationalCode, FirstName, LastName, " +
                        $"       CompanyName, NationalId " +
                        $"FROM Parties WHERE {filterColumn} " +
                        $"ORDER BY COALESCE(CompanyName, FirstName + ' ' + LastName);",
                        conn);

                    var loaded = new DataTable();
                    using (var reader = cmd.ExecuteReader())
                        loaded.Load(reader);

                    // ✅ FIX P1: پشتیبانی از PartyType با «ی» فارسی و «ي» عربی
                    // و همچنین مقادیر قدیمی (natural / legal / حقیقی / حقوقی)
                    loaded.Columns.Add("DisplayName", typeof(string),
                        "IIF(" +
                        "    PartyType = 'حقیقی' OR PartyType = 'حقيقي' " +
                        "    OR PartyType = 'natural' OR PartyType = 'Natural', " +
                        "    (ISNULL(FirstName, '') + ' ' + ISNULL(LastName, '')).Trim(), " +
                        "    ISNULL(CompanyName, '')" +
                        ")");

                    dt = loaded;
                    dgv.DataSource = dt;

                    // پیکربندی ستون‌ها
                    if (dgv.Columns["Id"] != null) dgv.Columns["Id"].Visible = false;
                    if (dgv.Columns["FirstName"] != null) dgv.Columns["FirstName"].Visible = false;
                    if (dgv.Columns["LastName"] != null) dgv.Columns["LastName"].Visible = false;
                    if (dgv.Columns["CompanyName"] != null) dgv.Columns["CompanyName"].Visible = false;

                    if (dgv.Columns["PartyType"] != null)
                    {
                        dgv.Columns["PartyType"].HeaderText = "نوع";
                        dgv.Columns["PartyType"].FillWeight = 60;
                    }
                    if (dgv.Columns["DisplayName"] != null)
                    {
                        dgv.Columns["DisplayName"].HeaderText = "نام / شرکت";
                        dgv.Columns["DisplayName"].DisplayIndex = 0;
                        dgv.Columns["DisplayName"].FillWeight = 200;
                    }
                    if (dgv.Columns["NationalCode"] != null)
                    {
                        dgv.Columns["NationalCode"].HeaderText = "کد ملی";
                        dgv.Columns["NationalCode"].FillWeight = 100;
                    }
                    if (dgv.Columns["NationalId"] != null)
                    {
                        dgv.Columns["NationalId"].HeaderText = "شناسه ملی";
                        dgv.Columns["NationalId"].FillWeight = 100;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "PartySelectionHelper.LoadData");
                    ModernMessageBox.ShowError("خطا در بارگذاری لیست طرفین قرارداد:\n" + ex.Message);
                }
            }

            // ==================================================
            // جستجوی زنده — با Escape Wildcardها
            // ==================================================
            void ApplyFilter()
            {
                if (dt == null) return;

                string search = (txtSearch.Text ?? "").Trim();
                if (string.IsNullOrEmpty(search))
                {
                    dt.DefaultView.RowFilter = "";
                    return;
                }

                // نرمال‌سازی فارسی + Escape کاراکترهای خاص DataView RowFilter
                string safe = StringHelper.NormalizePersianText(search)
                    .Replace("'", "''")
                    .Replace("[", "[[]")
                    .Replace("%", "[%]")
                    .Replace("*", "[*]");

                try
                {
                    dt.DefaultView.RowFilter =
                        $"DisplayName LIKE '%{safe}%' " +
                        $"OR NationalCode LIKE '%{safe}%' " +
                        $"OR NationalId LIKE '%{safe}%'";
                }
                catch (Exception ex)
                {
                    // اگر ستونی وجود نداشت یا فیلتر خراب شد، فیلتر را پاک کن
                    LogHelper.LogWarning("RowFilter error: " + ex.Message, "PartySelectionHelper");
                    dt.DefaultView.RowFilter = "";
                }
            }

            // ==================================================
            // انتخاب ردیف جاری
            // ==================================================
            bool TrySelectCurrentRow()
            {
                var row = dgv.CurrentRow;
                if (row == null || row.Index < 0) return false;

                var idVal = row.Cells["Id"]?.Value;
                if (idVal == null || idVal == DBNull.Value) return false;

                selectedId = Convert.ToInt32(idVal);
                selectedName = row.Cells["DisplayName"]?.Value?.ToString() ?? "";
                return true;
            }

            // ==================================================
            // رویدادها
            // ==================================================
            txtSearch.TextChanged += (s, e) => ApplyFilter();

            dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && TrySelectCurrentRow())
                {
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                }
            };

            dgv.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    if (TrySelectCurrentRow())
                    {
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    }
                }
            };

            btnSelect.Click += (s, e) =>
            {
                if (!TrySelectCurrentRow())
                {
                    // جلوگیری از بستن فرم با DialogResult.OK بدون انتخاب
                    form.DialogResult = DialogResult.None;
                    ModernMessageBox.ShowWarning("لطفاً یک طرف قرارداد را انتخاب کنید.");
                }
            };

            btnManage.Click += (s, e) =>
            {
                try
                {
                    using var f = new PartyManagementForm(connectionString);
                    f.ShowDialog(form);
                    LoadData();  // آپدیت لیست پس از بستن فرم مدیریت
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "PartySelectionHelper.Manage");
                    ModernMessageBox.ShowError("خطا در باز کردن فرم مدیریت طرفین:\n" + ex.Message);
                }
            };

            // ==================================================
            // سرهم‌بندی
            // ==================================================
            form.Controls.Add(dgv);
            form.Controls.Add(pnlTop);
            form.Controls.Add(pnlBottom);
            form.AcceptButton = btnSelect;
            form.CancelButton = btnCancel;

            LoadData();

            return form.ShowDialog(owner) == DialogResult.OK && selectedId > 0
                ? (selectedId, selectedName)
                : (0, "");
        }
    }
}