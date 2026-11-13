using ClosedXML.Excel;
using SMP.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class ExcelComparisonForm : Form
    {
        private TextBox txtFile1 = null!;
        private TextBox txtFile2 = null!;
        private Button btnBrowse1 = null!;
        private Button btnBrowse2 = null!;
        private Button btnLoadFiles = null!;
        private Button btnCompare = null!;
        private Button btnExport = null!;
        private Button btnAutoMap = null!;

        private TabControl tabControl = null!;
        private DataGridView dgvMapping = null!;
        private DataGridView dgvLeft = null!;
        private DataGridView dgvRight = null!;

        private Label lblStatus = null!;
        private ComboBox cmbCompareMode = null!;
        private CheckBox chkSmartCompare = null!;

        private DataTable _dtLeft = null!;
        private DataTable _dtRight = null!;

        private Dictionary<int, int> _columnMappings = new();
        private int _totalDifferences = 0;

        private int _currentMode = 0;
        private HashSet<int> _excessRowsLeft = new();
        private HashSet<int> _excessRowsRight = new();
        private Dictionary<string, List<int>> _keyRowsLeft = new();
        private Dictionary<string, List<int>> _keyRowsRight = new();

        private List<DifferenceRecord> _differencesList = new();
        private List<SmartMatchRecord> _smartMatchDiffs = new();

        private class DifferenceRecord
        {
            public string Date { get; set; } = "";
            public int Sequence { get; set; }
            public string ColumnName { get; set; } = "";
            public string Val1 { get; set; } = "";
            public string Val2 { get; set; } = "";
            public string Note { get; set; } = "";
        }

        private class SmartMatchRecord
        {
            public string Date { get; set; } = "";
            public string Status { get; set; } = "";
            public DataRow? Row1 { get; set; }
            public DataRow? Row2 { get; set; }
        }

        private class RowItem
        {
            public DataRow Row { get; set; } = null!;
            public int Index { get; set; }
            public bool Matched { get; set; }
        }

        public ExcelComparisonForm()
        {
            BuildUI();
            this.ApplyStandardHeader("مقایسه پیشرفته و هوشمند دو فایل اکسل", new Size(1250, 850));
            ShowInTaskbar = false;
            ShortcutHelper.ApplyStandardShortcuts(this);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            var pnlTop = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
            pnlTop.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            pnlTop.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            pnlTop.Controls.Add(new Label { Text = "فایل اول:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold) }, 0, 0);
            txtFile1 = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Margin = new Padding(5, 8, 5, 5) };
            pnlTop.Controls.Add(txtFile1, 1, 0);
            btnBrowse1 = UIHelper.CreateButton("📁 انتخاب", UIHelper.SectionBlue, 110, 30);
            btnBrowse1.Margin = new Padding(5, 6, 5, 5);
            btnBrowse1.Click += (s, e) => BrowseFile(txtFile1);
            pnlTop.Controls.Add(btnBrowse1, 2, 0);

            pnlTop.Controls.Add(new Label { Text = "فایل دوم:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold) }, 0, 1);
            txtFile2 = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Margin = new Padding(5, 5, 5, 8) };
            pnlTop.Controls.Add(txtFile2, 1, 1);
            btnBrowse2 = UIHelper.CreateButton("📁 انتخاب", UIHelper.SectionBlue, 110, 30);
            btnBrowse2.Margin = new Padding(5, 5, 5, 8);
            btnBrowse2.Click += (s, e) => BrowseFile(txtFile2);
            pnlTop.Controls.Add(btnBrowse2, 2, 1);

            btnLoadFiles = UIHelper.CreateButton("📥 بارگذاری فایل‌ها", UIHelper.ActionAmber, 170, 60);
            btnLoadFiles.Margin = new Padding(5);
            btnLoadFiles.Dock = DockStyle.Fill;
            btnLoadFiles.Click += async (s, e) => await LoadFilesAndMapAsync();
            pnlTop.Controls.Add(btnLoadFiles, 3, 0);
            pnlTop.SetRowSpan(btnLoadFiles, 2);

            mainLayout.Controls.Add(pnlTop, 0, 0);

            tabControl = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
            ModernLayoutHelper.StyleModernTabControl(tabControl);

            var tabMapping = new TabPage("⚙️ تنظیمات ستون‌ها");
            var mapLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(5) };
            mapLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            mapLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var mapToolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            btnAutoMap = UIHelper.CreateButton("✨ تطبیق خودکار ستون‌های هم‌نام", UIHelper.SectionTeal, 220, 35);
            btnAutoMap.Click += (s, e) => AutoMapColumns();
            mapToolbar.Controls.Add(btnAutoMap);
            mapToolbar.Controls.Add(new Label { Text = "برای «تطبیق هوشمند در تاریخ»، فقط ستون «تاریخ» و ستون‌های مبلغ (بدهکار/بستانکار) را تیک بزنید:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Margin = new Padding(10, 10, 10, 0), ForeColor = UIHelper.WarmDanger });

            dgvMapping = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false };
            dgvMapping.ApplyModernStyle();
            dgvMapping.ReadOnly = false;
            dgvMapping.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsSelected", HeaderText = "مقایسه شود؟", Width = 100, ReadOnly = false });
            dgvMapping.Columns.Add(new DataGridViewComboBoxColumn { Name = "Col1", HeaderText = "ستون در فایل اول", Width = 300, FlatStyle = FlatStyle.Flat });
            dgvMapping.Columns.Add(new DataGridViewComboBoxColumn { Name = "Col2", HeaderText = "ستون در فایل دوم", Width = 300, FlatStyle = FlatStyle.Flat });

            mapLayout.Controls.Add(mapToolbar, 0, 0);
            mapLayout.Controls.Add(dgvMapping, 0, 1);
            tabMapping.Controls.Add(mapLayout);

            var tabPreview = new TabPage("📊 پیش‌نمایش داده‌ها");
            var splitLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            dgvLeft = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true };
            dgvLeft.ApplyModernStyle();
            dgvRight = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true };
            dgvRight.ApplyModernStyle();

            dgvLeft.CellFormatting += Dgv_CellFormatting;
            dgvRight.CellFormatting += Dgv_CellFormatting;

            // ✅ FIX: جلوگیری از خطای ArgumentOutOfRangeException در اسکرول همزمان
            dgvLeft.Scroll += (s, e) =>
            {
                if (dgvLeft.RowCount > 0 && dgvLeft.FirstDisplayedScrollingRowIndex >= 0)
                {
                    int targetIndex = dgvLeft.FirstDisplayedScrollingRowIndex;
                    if (dgvRight.RowCount > 0 && targetIndex < dgvRight.RowCount)
                    {
                        dgvRight.FirstDisplayedScrollingRowIndex = targetIndex;
                    }
                    else if (dgvRight.RowCount > 0)
                    {
                        dgvRight.FirstDisplayedScrollingRowIndex = dgvRight.RowCount - 1;
                    }
                }
            };

            splitLayout.Controls.Add(dgvLeft, 0, 0);
            splitLayout.Controls.Add(dgvRight, 1, 0);
            tabPreview.Controls.Add(splitLayout);

            tabControl.TabPages.Add(tabMapping);
            tabControl.TabPages.Add(tabPreview);
            mainLayout.Controls.Add(tabControl, 0, 1);

            var pnlBottom = new Panel { Dock = DockStyle.Fill };

            btnCompare = UIHelper.CreateButton("🔄 شروع مقایسه", UIHelper.ActionGreen, 150, 40);
            btnCompare.Dock = DockStyle.Left;
            btnCompare.Enabled = false;
            btnCompare.Click += async (s, e) => await CompareFilesAsync();

            btnExport = UIHelper.CreateButton("📤 خروجی مغایرت‌ها", UIHelper.SectionPurple, 180, 40);
            btnExport.Dock = DockStyle.Left;
            btnExport.Enabled = false;
            btnExport.Margin = new Padding(10, 0, 0, 0);
            btnExport.Click += (s, e) => ExportDifferences();

            cmbCompareMode = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 380,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                Margin = new Padding(20, 0, 0, 0)
            };
            cmbCompareMode.Items.AddRange(new object[] {
                "۱. حالت تجمیعی (مقایسه تعداد بر اساس ستون‌های کلیدی)",
                "۲. حالت ردیفی (مقایسه تک‌به‌تک کل سطرها)",
                "۳. تطبیق هوشمند در تاریخ (پیدا کردن ردیف‌های متناظر بدون توجه به ترتیب)"
            });
            cmbCompareMode.SelectedIndex = 2;

            var pnlMode = new Panel { Dock = DockStyle.Left, Width = 390, Padding = new Padding(10, 12, 0, 0) };
            pnlMode.Controls.Add(cmbCompareMode);

            chkSmartCompare = new CheckBox
            {
                Text = "نرمال‌سازی هوشمند (تبدیل خالی به صفر)",
                Checked = true,
                AutoSize = true,
                Dock = DockStyle.Left,
                Font = UIHelper.PersianFont(10.5f, FontStyle.Bold),
                ForeColor = UIHelper.SectionBlue,
                Margin = new Padding(20, 0, 0, 0)
            };
            UIHelper.StyleToggle(chkSmartCompare);
            var pnlSmartChk = new Panel { Dock = DockStyle.Left, Width = 300, Padding = new Padding(10, 14, 0, 0) };
            pnlSmartChk.Controls.Add(chkSmartCompare);

            lblStatus = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(pnlSmartChk);
            pnlBottom.Controls.Add(pnlMode);
            pnlBottom.Controls.Add(btnExport);
            pnlBottom.Controls.Add(btnCompare);

            mainLayout.Controls.Add(pnlBottom, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void BrowseFile(TextBox targetTextBox)
        {
            using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل" };
            if (ofd.ShowDialog(this) == DialogResult.OK) targetTextBox.Text = ofd.FileName;
        }

        private async Task LoadFilesAndMapAsync()
        {
            if (string.IsNullOrWhiteSpace(txtFile1.Text) || string.IsNullOrWhiteSpace(txtFile2.Text))
            {
                ModernMessageBox.ShowWarning("لطفاً هر دو فایل اکسل را انتخاب کنید.");
                return;
            }

            btnLoadFiles.Enabled = false;
            lblStatus.Text = "در حال خواندن فایل‌ها...";

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                try
                {
                    _dtLeft = await Task.Run(() => ReadExcelToDataTable(txtFile1.Text));
                    _dtRight = await Task.Run(() => ReadExcelToDataTable(txtFile2.Text));
                }
                catch (IOException)
                {
                    throw new Exception("یکی از فایل‌های اکسل در حال حاضر توسط برنامه دیگری (مثلاً خود Microsoft Excel) باز است.\n\nلطفاً فایل‌ها را بسته و دوباره تلاش کنید.");
                }
                catch (Exception ex)
                {
                    throw new Exception("خطا در خواندن فایل‌های اکسل.\n\nممکن است فایل‌ها خراب باشند یا فرمت آن‌ها xlsx نباشد.\n\nجزئیات: " + ex.Message);
                }

                if (_dtLeft == null || _dtRight == null) throw new Exception("داده‌ای در فایل‌های اکسل یافت نشد.");
                if (IsDisposed) return;

                dgvLeft.DataSource = _dtLeft;
                dgvRight.DataSource = _dtRight;

                SetupMappingGrid();
                tabControl.SelectedIndex = 0;
                btnCompare.Enabled = true;
                btnExport.Enabled = false;

            }, "در حال پردازش فایل‌های اکسل...");

            if (success) lblStatus.Text = "فایل‌ها بارگذاری شدند. لطفاً ستون‌ها را تنظیم کنید.";
            btnLoadFiles.Enabled = true;
        }

        // استفاده از FileStream برای جلوگیری از خطای قفل بودن فایل در اکسل
        private DataTable ReadExcelToDataTable(string filePath)
        {
            var dt = new DataTable();

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var workbook = new XLWorkbook(stream))
            {
                var worksheet = workbook.Worksheet(1);
                var range = worksheet.RangeUsed();
                if (range == null) return dt;

                int colCount = range.ColumnCount();
                int rowCount = range.RowCount();
                if (rowCount == 0) return dt;

                for (int c = 1; c <= colCount; c++)
                {
                    string header = worksheet.Cell(1, c).GetFormattedString().Trim();
                    if (string.IsNullOrWhiteSpace(header)) header = $"ستون_{c}";
                    int suffix = 1;
                    string originalHeader = header;
                    while (dt.Columns.Contains(header)) header = $"{originalHeader}_{suffix++}";
                    dt.Columns.Add(header);
                }

                for (int r = 2; r <= rowCount; r++)
                {
                    var row = dt.NewRow();
                    for (int c = 1; c <= colCount; c++)
                    {
                        var cell = worksheet.Cell(r, c);
                        row[c - 1] = cell.IsEmpty() ? "" : cell.GetFormattedString().Trim();
                    }
                    dt.Rows.Add(row);
                }
            }

            return dt;
        }

        private void SetupMappingGrid()
        {
            dgvMapping.Rows.Clear();
            _columnMappings.Clear();

            var col1Combo = (DataGridViewComboBoxColumn)dgvMapping.Columns["Col1"];
            var col2Combo = (DataGridViewComboBoxColumn)dgvMapping.Columns["Col2"];
            col1Combo.Items.Clear();
            col2Combo.Items.Clear();

            foreach (DataColumn col in _dtLeft.Columns) col1Combo.Items.Add(col.ColumnName);
            foreach (DataColumn col in _dtRight.Columns) col2Combo.Items.Add(col.ColumnName);

            int maxCols = Math.Max(_dtLeft.Columns.Count, _dtRight.Columns.Count);
            for (int i = 0; i < maxCols; i++)
            {
                string c1 = i < _dtLeft.Columns.Count ? _dtLeft.Columns[i].ColumnName : "";
                string c2 = i < _dtRight.Columns.Count ? _dtRight.Columns[i].ColumnName : "";
                dgvMapping.Rows.Add(true, c1, c2);
            }
            AutoMapColumns();
        }

        private void AutoMapColumns()
        {
            if (_dtLeft == null || _dtRight == null) return;
            foreach (DataGridViewRow row in dgvMapping.Rows)
            {
                string c1 = row.Cells["Col1"].Value?.ToString() ?? "";
                string c2 = row.Cells["Col2"].Value?.ToString() ?? "";

                bool match = false;
                if (!string.IsNullOrEmpty(c1) && !string.IsNullOrEmpty(c2))
                {
                    if (string.Equals(c1, c2, StringComparison.OrdinalIgnoreCase)) match = true;
                    else if (c1.Contains("تاریخ") && c2.Contains("تاریخ")) match = true;
                    else if (c1.Contains("بدهکار") && c2.Contains("بدهکار")) match = true;
                    else if (c1.Contains("بستانکار") && c2.Contains("بستانکار")) match = true;
                    else if (c1.Contains("Debit") && c2.Contains("Debit")) match = true;
                    else if (c1.Contains("Credit") && c2.Contains("Credit")) match = true;
                }
                row.Cells["IsSelected"].Value = match;
            }
        }

        private async Task CompareFilesAsync()
        {
            if (_dtLeft == null || _dtRight == null) return;

            _columnMappings.Clear();
            foreach (DataGridViewRow row in dgvMapping.Rows)
            {
                if (Convert.ToBoolean(row.Cells["IsSelected"].Value) != true) continue;
                string c1 = row.Cells["Col1"].Value?.ToString() ?? "";
                string c2 = row.Cells["Col2"].Value?.ToString() ?? "";
                if (string.IsNullOrEmpty(c1) || string.IsNullOrEmpty(c2)) continue;

                int idx1 = _dtLeft.Columns.IndexOf(c1);
                int idx2 = _dtRight.Columns.IndexOf(c2);
                if (idx1 >= 0 && idx2 >= 0) _columnMappings[idx1] = idx2;
            }

            if (_columnMappings.Count == 0)
            {
                ModernMessageBox.ShowWarning("لطفاً حداقل یک جفت ستون را برای مقایسه تیک بزنید.");
                return;
            }

            btnCompare.Enabled = false;
            _currentMode = cmbCompareMode.SelectedIndex;
            _excessRowsLeft.Clear();
            _excessRowsRight.Clear();
            _keyRowsLeft.Clear();
            _keyRowsRight.Clear();
            _differencesList.Clear();
            _smartMatchDiffs.Clear();

            lblStatus.Text = "در حال مقایسه...";

            await Task.Run(() =>
            {
                bool useSmart = chkSmartCompare.Checked;

                if (_currentMode == 2) CompareByDateSmartMatch(useSmart);
                else if (_currentMode == 0) CompareAggregate(useSmart);
                else CompareRowByRow(useSmart);

                this.Invoke(new Action(() =>
                {
                    tabControl.SelectedIndex = 1;
                    dgvLeft.Refresh();
                    dgvRight.Refresh();

                    if (_totalDifferences == 0)
                    {
                        lblStatus.Text = "✅ دو فایل از نظر ستون‌های انتخابی کاملاً یکسان هستند.";
                        btnExport.Enabled = false;
                    }
                    else
                    {
                        lblStatus.Text = $"⚠️ تعداد {NumberHelper.FormatThousands(_totalDifferences, usePersianDigits: true)} مغایرت یافت شد.";
                        btnExport.Enabled = true;
                    }
                }));
            });

            btnCompare.Enabled = true;
        }

        private void CompareByDateSmartMatch(bool useSmart)
        {
            int dateCol1 = -1, dateCol2 = -1;
            foreach (var map in _columnMappings)
            {
                string colName1 = _dtLeft.Columns[map.Key].ColumnName;
                string colName2 = _dtRight.Columns[map.Value].ColumnName;
                if (colName1.Contains("تاریخ") || colName2.Contains("تاریخ"))
                {
                    dateCol1 = map.Key;
                    dateCol2 = map.Value;
                    break;
                }
            }

            if (dateCol1 == -1 || dateCol2 == -1)
            {
                this.Invoke(new Action(() => ModernMessageBox.ShowWarning("برای این حالت، باید حتماً ستون «تاریخ» را در جدول تنظیمات تیک بزنید.")));
                return;
            }

            var groupA = new Dictionary<string, List<RowItem>>();
            for (int i = 0; i < _dtLeft.Rows.Count; i++)
            {
                string key = NormalizeValueForComparison(_dtLeft.Rows[i][dateCol1]?.ToString(), useSmart);
                if (!groupA.ContainsKey(key)) groupA[key] = new List<RowItem>();
                groupA[key].Add(new RowItem { Row = _dtLeft.Rows[i], Index = i, Matched = false });
            }

            var groupB = new Dictionary<string, List<RowItem>>();
            for (int i = 0; i < _dtRight.Rows.Count; i++)
            {
                string key = NormalizeValueForComparison(_dtRight.Rows[i][dateCol2]?.ToString(), useSmart);
                if (!groupB.ContainsKey(key)) groupB[key] = new List<RowItem>();
                groupB[key].Add(new RowItem { Row = _dtRight.Rows[i], Index = i, Matched = false });
            }

            var allDates = groupA.Keys.Union(groupB.Keys).OrderBy(d => d).ToList();
            int diffCount = 0;

            foreach (var date in allDates)
            {
                var listA = groupA.ContainsKey(date) ? groupA[date] : new List<RowItem>();
                var listB = groupB.ContainsKey(date) ? groupB[date] : new List<RowItem>();

                for (int i = 0; i < listA.Count; i++)
                {
                    if (listA[i].Matched) continue;

                    for (int j = 0; j < listB.Count; j++)
                    {
                        if (listB[j].Matched) continue;

                        bool isMatch = true;
                        foreach (var map in _columnMappings)
                        {
                            if (map.Key == dateCol1) continue;

                            string val1 = NormalizeValueForComparison(listA[i].Row[map.Key]?.ToString(), useSmart);
                            string val2 = NormalizeValueForComparison(listB[j].Row[map.Value]?.ToString(), useSmart);

                            if (val1 != val2) { isMatch = false; break; }
                        }

                        if (isMatch)
                        {
                            listA[i].Matched = true;
                            listB[j].Matched = true;
                            break;
                        }
                    }
                }

                foreach (var itemA in listA)
                {
                    if (!itemA.Matched)
                    {
                        _excessRowsLeft.Add(itemA.Index);
                        diffCount++;
                        _smartMatchDiffs.Add(new SmartMatchRecord { Date = date, Status = "فقط در فایل ۱", Row1 = itemA.Row, Row2 = null });
                    }
                }

                foreach (var itemB in listB)
                {
                    if (!itemB.Matched)
                    {
                        _excessRowsRight.Add(itemB.Index);
                        diffCount++;
                        _smartMatchDiffs.Add(new SmartMatchRecord { Date = date, Status = "فقط در فایل ۲", Row1 = null, Row2 = itemB.Row });
                    }
                }
            }

            _totalDifferences = diffCount;
        }

        private void CompareRowByRow(bool useSmart)
        {
            int diffCount = 0;
            int maxRows = Math.Max(_dtLeft.Rows.Count, _dtRight.Rows.Count);

            for (int r = 0; r < maxRows; r++)
            {
                bool rowHasDiff = false;
                foreach (var map in _columnMappings)
                {
                    string val1 = (r < _dtLeft.Rows.Count && map.Key < _dtLeft.Columns.Count) ? _dtLeft.Rows[r][map.Key]?.ToString() ?? "" : "";
                    string val2 = (r < _dtRight.Rows.Count && map.Value < _dtRight.Columns.Count) ? _dtRight.Rows[r][map.Value]?.ToString() ?? "" : "";

                    string n1 = NormalizeValueForComparison(val1, useSmart);
                    string n2 = NormalizeValueForComparison(val2, useSmart);

                    if (n1 != n2)
                    {
                        rowHasDiff = true;
                        diffCount++;
                        _differencesList.Add(new DifferenceRecord
                        {
                            Date = $"(سطر {r + 1})",
                            Sequence = r + 1,
                            ColumnName = _dtLeft.Columns[map.Key].ColumnName,
                            Val1 = string.IsNullOrWhiteSpace(n1) ? "0" : n1,
                            Val2 = string.IsNullOrWhiteSpace(n2) ? "0" : n2,
                            Note = "مقدار متفاوت"
                        });
                    }
                }
                if (rowHasDiff) { _excessRowsLeft.Add(r); _excessRowsRight.Add(r); }
            }
            _totalDifferences = diffCount;
        }

        private void CompareAggregate(bool useSmart)
        {
            for (int r = 0; r < _dtLeft.Rows.Count; r++)
            {
                string key = GetCompositeKey(_dtLeft.Rows[r], true, useSmart);
                if (!_keyRowsLeft.ContainsKey(key)) _keyRowsLeft[key] = new List<int>();
                _keyRowsLeft[key].Add(r);
            }

            for (int r = 0; r < _dtRight.Rows.Count; r++)
            {
                string key = GetCompositeKey(_dtRight.Rows[r], false, useSmart);
                if (!_keyRowsRight.ContainsKey(key)) _keyRowsRight[key] = new List<int>();
                _keyRowsRight[key].Add(r);
            }

            var allKeys = _keyRowsLeft.Keys.Union(_keyRowsRight.Keys).ToList();
            int diffCount = 0;

            foreach (var key in allKeys)
            {
                var listA = _keyRowsLeft.GetValueOrDefault(key) ?? new List<int>();
                var listB = _keyRowsRight.GetValueOrDefault(key) ?? new List<int>();

                if (listA.Count > listB.Count)
                {
                    int diff = listA.Count - listB.Count;
                    diffCount += diff;
                    for (int i = listB.Count; i < listA.Count; i++) _excessRowsLeft.Add(listA[i]);
                }
                else if (listB.Count > listA.Count)
                {
                    int diff = listB.Count - listA.Count;
                    diffCount += diff;
                    for (int i = listA.Count; i < listB.Count; i++) _excessRowsRight.Add(listB[i]);
                }
            }
            _totalDifferences = diffCount;
        }

        private string NormalizeValueForComparison(string? val, bool useSmart)
        {
            if (!useSmart) return val?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(val)) return "0";

            val = StringHelper.ConvertToEnglishDigits(val).Trim();
            string numStr = val.Replace(",", "").Replace("٬", "").Replace("٫", ".").Trim();

            if (Regex.IsMatch(numStr, @"^-?\d+(\.\d+)?$"))
            {
                if (decimal.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal num))
                {
                    return num.ToString("0.#########################", CultureInfo.InvariantCulture);
                }
            }

            var pd = DateHelper.ParsePersianDate(val);
            if (pd.HasValue) return pd.Value.ToString("yyyy-MM-dd");

            if (DateTime.TryParse(val, out DateTime dt)) return dt.ToString("yyyy-MM-dd");

            val = StringHelper.NormalizePersianText(val);
            return Regex.Replace(val, @"\s+", " ").ToLowerInvariant();
        }

        private string GetCompositeKey(DataRow row, bool isLeft, bool useSmart)
        {
            var sb = new StringBuilder();
            foreach (var map in _columnMappings)
            {
                int colIdx = isLeft ? map.Key : map.Value;
                string val = colIdx < row.Table.Columns.Count ? row[colIdx]?.ToString() ?? "" : "";
                sb.Append(NormalizeValueForComparison(val, useSmart)).Append("|");
            }
            return sb.ToString();
        }

        private void ExportDifferences()
        {
            if (_totalDifferences == 0) return;

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = $"گزارش_مغایرت_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                Title = "ذخیره فایل مغایرت‌ها"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("مغایرت‌ها");
                ws.RightToLeft = true;

                if (_currentMode == 2)
                {
                    int debitCol1 = -1, creditCol1 = -1, debitCol2 = -1, creditCol2 = -1;
                    int dateCol1 = -1, dateCol2 = -1;

                    foreach (var map in _columnMappings)
                    {
                        string c1 = _dtLeft.Columns[map.Key].ColumnName;
                        string c2 = _dtRight.Columns[map.Value].ColumnName;

                        if (c1.Contains("تاریخ") || c2.Contains("تاریخ")) { dateCol1 = map.Key; dateCol2 = map.Value; }
                        if (c1.Contains("بدهکار") || c1.Contains("Debit")) { debitCol1 = map.Key; debitCol2 = map.Value; }
                        if (c1.Contains("بستانکار") || c1.Contains("Credit")) { creditCol1 = map.Key; creditCol2 = map.Value; }
                    }

                    string[] headers = { "تاریخ", "وضعیت", "بدهکار (فایل ۱)", "بستانکار (فایل ۱)", "بدهکار (فایل ۲)", "بستانکار (فایل ۲)", "توضیحات" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = ws.Cell(1, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    for (int r = 0; r < _smartMatchDiffs.Count; r++)
                    {
                        var diff = _smartMatchDiffs[r];
                        ws.Cell(r + 2, 1).Value = diff.Date;
                        ws.Cell(r + 2, 2).Value = diff.Status;

                        decimal d1 = 0, c1_val = 0, d2 = 0, c2_val = 0;
                        string note = "";

                        if (diff.Row1 != null)
                        {
                            if (debitCol1 != -1) d1 = NumberHelper.ParseFormatted(diff.Row1[debitCol1]?.ToString() ?? "0");
                            if (creditCol1 != -1) c1_val = NumberHelper.ParseFormatted(diff.Row1[creditCol1]?.ToString() ?? "0");
                            note = "این ردیف در فایل اول وجود دارد.";
                        }

                        if (diff.Row2 != null)
                        {
                            if (debitCol2 != -1) d2 = NumberHelper.ParseFormatted(diff.Row2[debitCol2]?.ToString() ?? "0");
                            if (creditCol2 != -1) c2_val = NumberHelper.ParseFormatted(diff.Row2[creditCol2]?.ToString() ?? "0");
                            note = "این ردیف در فایل دوم وجود دارد.";
                        }

                        ws.Cell(r + 2, 3).Value = d1;
                        ws.Cell(r + 2, 4).Value = c1_val;
                        ws.Cell(r + 2, 5).Value = d2;
                        ws.Cell(r + 2, 6).Value = c2_val;
                        ws.Cell(r + 2, 7).Value = note;

                        ws.Cell(r + 2, 3).Style.NumberFormat.Format = "#,##0";
                        ws.Cell(r + 2, 4).Style.NumberFormat.Format = "#,##0";
                        ws.Cell(r + 2, 5).Style.NumberFormat.Format = "#,##0";
                        ws.Cell(r + 2, 6).Style.NumberFormat.Format = "#,##0";

                        if (diff.Row1 == null)
                        {
                            ws.Cell(r + 2, 5).Style.Font.FontColor = XLColor.Red;
                            ws.Cell(r + 2, 6).Style.Font.FontColor = XLColor.Red;
                        }
                        if (diff.Row2 == null)
                        {
                            ws.Cell(r + 2, 3).Style.Font.FontColor = XLColor.Red;
                            ws.Cell(r + 2, 4).Style.Font.FontColor = XLColor.Red;
                        }
                    }
                }
                else if (_currentMode == 0)
                {
                    string[] headers = { "مقدار کلیدی (ترکیب ستون‌ها)", "تعداد در فایل ۱", "تعداد در فایل ۲", "نوع مغایرت" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = ws.Cell(1, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    int rowIndex = 2;
                    var allKeys = _keyRowsLeft.Keys.Union(_keyRowsRight.Keys).ToList();
                    foreach (var key in allKeys)
                    {
                        int countA = _keyRowsLeft.ContainsKey(key) ? _keyRowsLeft[key].Count : 0;
                        int countB = _keyRowsRight.ContainsKey(key) ? _keyRowsRight[key].Count : 0;

                        if (countA != countB)
                        {
                            string[] keyParts = key.Split('|');
                            var displayParts = new List<string>();
                            int pIdx = 0;
                            foreach (var map in _columnMappings)
                            {
                                if (pIdx < keyParts.Length) displayParts.Add($"{_dtLeft.Columns[map.Key].ColumnName}: {keyParts[pIdx]}");
                                pIdx++;
                            }

                            ws.Cell(rowIndex, 1).Value = string.Join(" | ", displayParts);
                            ws.Cell(rowIndex, 2).Value = countA;
                            ws.Cell(rowIndex, 3).Value = countB;
                            ws.Cell(rowIndex, 4).Value = countA > countB ? $"⚠️ {countA - countB} ردیف در فایل ۱ بیشتر است" : $"⚠️ {countB - countA} ردیف در فایل ۲ بیشتر است";
                            ws.Cell(rowIndex, 4).Style.Font.FontColor = XLColor.Red;
                            rowIndex++;
                        }
                    }
                }
                else
                {
                    string[] headers = { "ردیف", "نام ستون", "مقدار در فایل ۱", "مقدار در فایل ۲" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = ws.Cell(1, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    for (int r = 0; r < _differencesList.Count; r++)
                    {
                        var diff = _differencesList[r];
                        ws.Cell(r + 2, 1).Value = diff.Sequence;
                        ws.Cell(r + 2, 2).Value = diff.ColumnName;
                        ws.Cell(r + 2, 3).Value = diff.Val1;
                        ws.Cell(r + 2, 4).Value = diff.Val2;
                        ws.Cell(r + 2, 3).Style.Font.FontColor = XLColor.Red;
                        ws.Cell(r + 2, 4).Style.Font.FontColor = XLColor.Red;
                    }
                }

                var dataRange = ws.Range(1, 1, ws.LastRowUsed()!.RowNumber(), ws.LastColumnUsed()!.ColumnNumber());
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Columns().AdjustToContents();
                ws.SheetView.FreezeRows(1);
                workbook.SaveAs(sfd.FileName);

                ToastHelper.Show("فایل مغایرت‌ها با موفقیت ذخیره شد.", true);

                if (ModernMessageBox.AskConfirm("آیا می‌خواهید فایل را باز کنید؟", "باز کردن فایل"))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.ShowError("خطا در ذخیره فایل اکسل:\n" + ex.Message);
            }
        }

        private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || sender is not DataGridView dgv) return;
            if (dgv.DataSource == null || _columnMappings.Count == 0) return;

            int r = e.RowIndex;
            bool isMatch = true;

            if (_currentMode == 0 || _currentMode == 2)
            {
                if (dgv == dgvLeft && _excessRowsLeft.Contains(r)) isMatch = false;
                else if (dgv == dgvRight && _excessRowsRight.Contains(r)) isMatch = false;
            }
            else
            {
                int c = e.ColumnIndex;
                int mappedLeftCol = -1, mappedRightCol = -1;
                bool isMapped = false;

                if (dgv == dgvLeft && _columnMappings.TryGetValue(c, out int rc))
                {
                    isMapped = true; mappedLeftCol = c; mappedRightCol = rc;
                }
                else if (dgv == dgvRight)
                {
                    foreach (var kvp in _columnMappings) if (kvp.Value == c) { isMapped = true; mappedLeftCol = kvp.Key; mappedRightCol = c; break; }
                }

                if (isMapped)
                {
                    string val1 = (r < _dtLeft.Rows.Count && mappedLeftCol < _dtLeft.Columns.Count) ? _dtLeft.Rows[r][mappedLeftCol]?.ToString() ?? "" : "";
                    string val2 = (r < _dtRight.Rows.Count && mappedRightCol < _dtRight.Columns.Count) ? _dtRight.Rows[r][mappedRightCol]?.ToString() ?? "" : "";

                    string n1 = NormalizeValueForComparison(val1, chkSmartCompare.Checked);
                    string n2 = NormalizeValueForComparison(val2, chkSmartCompare.Checked);

                    if (n1 != n2) isMatch = false;
                }
            }

            if (!isMatch)
            {
                e.CellStyle.BackColor = Color.FromArgb(255, 200, 200);
                e.CellStyle.ForeColor = Color.DarkRed;
                e.CellStyle.Font = new Font(dgv.Font, FontStyle.Bold);
            }
            else
            {
                e.CellStyle.BackColor = dgv.DefaultCellStyle.BackColor;
                e.CellStyle.ForeColor = dgv.DefaultCellStyle.ForeColor;
                e.CellStyle.Font = dgv.DefaultCellStyle.Font;
            }
        }
    }
}