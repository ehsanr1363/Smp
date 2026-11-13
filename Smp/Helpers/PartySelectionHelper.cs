using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using SMP.Forms;

namespace SMP.Helpers
{
    public static class PartySelectionHelper
    {
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

            DataTable dt = null;

            void LoadData()
            {
                try
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();

                    string filterColumn = isContractor ? "IsContractor = 1" : "IsClient = 1";

                    // ✅ محاسبه DisplayName مستقیماً در SQL Server برای جلوگیری از خطای پارسر DataTable
                    string sql = $@"
                        SELECT 
                            Id, 
                            PartyType, 
                            NationalCode, 
                            FirstName, 
                            LastName, 
                            CompanyName, 
                            NationalId,
                            CASE 
                                WHEN PartyType IN (N'حقیقی', N'حقيقي', N'natural', N'Natural') 
                                THEN ISNULL(FirstName, N'') + N' ' + ISNULL(LastName, N'')
                                ELSE ISNULL(CompanyName, N'')
                            END AS DisplayName
                        FROM Parties 
                        WHERE {filterColumn} 
                        ORDER BY DisplayName";

                    using var cmd = new SqlCommand(sql, conn);

                    var loaded = new DataTable();
                    using (var reader = cmd.ExecuteReader())
                        loaded.Load(reader);

                    dt = loaded;
                    dgv.DataSource = dt;

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

            void ApplyFilter()
            {
                if (dt == null) return;

                string search = (txtSearch.Text ?? "").Trim();
                if (string.IsNullOrEmpty(search))
                {
                    dt.DefaultView.RowFilter = "";
                    return;
                }

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
                    LogHelper.LogWarning("RowFilter error: " + ex.Message, "PartySelectionHelper");
                    dt.DefaultView.RowFilter = "";
                }
            }

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
                    LoadData();
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "PartySelectionHelper.Manage");
                    ModernMessageBox.ShowError("خطا در باز کردن فرم مدیریت طرفین:\n" + ex.Message);
                }
            };

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