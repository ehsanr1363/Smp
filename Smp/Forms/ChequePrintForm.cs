using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class ChequeSetting
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float FontSize { get; set; }
    }

    public class ChequePrintForm : Form
    {
        private readonly CashCheck _check;
        private readonly CompanyBankAccount _account;

        private PrintDocument printDoc = null!;
        private PrintPreviewControl previewControl = null!;
        private Button btnPrint = null!, btnReset = null!, btnSave = null!;

        private ComboBox cmbFields = null!;
        private NumericUpDown numX = null!, numY = null!, numSize = null!;

        private bool _isUpdatingUI = false;
        private string _selectedField = "تاریخ (سال)";
        private bool _isActualPrinting = false;

        private readonly string _settingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SMP", "cheque_layout.txt");

        // ✅ مختصات پیش‌فرض — بدون تغییر نسبت به نسخه قبلی
        private readonly Dictionary<string, ChequeSetting> _settings = new()
        {
            { "تاریخ (سال)", new ChequeSetting { X = 130, Y = 6, FontSize = 12 } },
            { "تاریخ (ماه)", new ChequeSetting { X = 145, Y = 6, FontSize = 12 } },
            { "تاریخ (روز)", new ChequeSetting { X = 160, Y = 6, FontSize = 12 } },
            { "تاریخ (حروف)", new ChequeSetting { X = 150, Y = 16, FontSize = 10 } },
            { "نام گیرنده و بابت", new ChequeSetting { X = 150, Y = 27, FontSize = 14 } },
            { "کد ملی", new ChequeSetting { X = 55, Y = 27, FontSize = 14 } },
            { "مبلغ (حروف)", new ChequeSetting { X = 150, Y = 42, FontSize = 12 } },
            { "مبلغ (مقطعی)", new ChequeSetting { X = 10, Y = 54, FontSize = 10 } },
            { "مبلغ (گرافیکی)", new ChequeSetting { X = 22, Y = 62, FontSize = 11 } }
        };

        public ChequePrintForm(CashCheck check, CompanyBankAccount account)
        {
            _check = check;
            _account = account;

            // ✅ FIX: LoadSettings ایمن است — فایل خراب کرش نمی‌کند
            LoadSettings();
            BuildUI();

            this.ApplyStandardHeader("چاپ چک بانکی (کالیبراسیون پیشرفته)", new Size(1150, 750));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { printDoc?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var pnlControls = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 15, 10, 10), WrapContents = true };

            var lblField = new Label { Text = "انتخاب آیتم:", AutoSize = true, Font = UIHelper.PersianFont(10f, FontStyle.Bold), Margin = new Padding(5, 8, 5, 0) };
            cmbFields = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(10f) };
            cmbFields.Items.AddRange(_settings.Keys.ToArray());
            cmbFields.SelectedIndex = 0;

            var lblX = new Label { Text = "X (افقی):", AutoSize = true, Font = UIHelper.PersianFont(10f, FontStyle.Bold), Margin = new Padding(15, 8, 5, 0) };
            numX = new NumericUpDown { Width = 60, Minimum = -20, Maximum = 200, DecimalPlaces = 1, Font = UIHelper.PersianFont(10f) };

            var lblY = new Label { Text = "Y (عمودی):", AutoSize = true, Font = UIHelper.PersianFont(10f, FontStyle.Bold), Margin = new Padding(10, 8, 5, 0) };
            numY = new NumericUpDown { Width = 60, Minimum = -20, Maximum = 150, DecimalPlaces = 1, Font = UIHelper.PersianFont(10f) };

            var lblSize = new Label { Text = "سایز فونت:", AutoSize = true, Font = UIHelper.PersianFont(10f, FontStyle.Bold), Margin = new Padding(10, 8, 5, 0) };
            numSize = new NumericUpDown { Width = 60, Minimum = 6, Maximum = 24, DecimalPlaces = 1, Font = UIHelper.PersianFont(10f) };

            btnSave = UIHelper.CreateButton("💾 ذخیره تنظیمات", UIHelper.ActionGreen, 140, 30);
            btnSave.Click += (s, e) =>
            {
                SaveSettings();
                ToastHelper.Show("تنظیمات چک با موفقیت ذخیره شد.", true);
            };

            btnReset = UIHelper.CreateButton("🔄 بازگشت به پیش‌فرض", Color.Gray, 140, 30);
            btnReset.Click += (s, e) =>
            {
                InitializeDefaults();
                UpdateUIFromSetting();
                previewControl.InvalidatePreview();
            };

            pnlControls.Controls.Add(lblField);
            pnlControls.Controls.Add(cmbFields);
            pnlControls.Controls.Add(lblX);
            pnlControls.Controls.Add(numX);
            pnlControls.Controls.Add(lblY);
            pnlControls.Controls.Add(numY);
            pnlControls.Controls.Add(lblSize);
            pnlControls.Controls.Add(numSize);
            pnlControls.Controls.Add(btnSave);
            pnlControls.Controls.Add(btnReset);

            cmbFields.SelectedIndexChanged += (s, e) =>
            {
                if (cmbFields.SelectedItem == null) return;
                _selectedField = cmbFields.SelectedItem.ToString()!;
                UpdateUIFromSetting();
            };

            numX.ValueChanged += UpdateSetting;
            numY.ValueChanged += UpdateSetting;
            numSize.ValueChanged += UpdateSetting;

            printDoc = new PrintDocument();
            printDoc.PrintPage += PrintDoc_PrintPage;

            SetPaperSize(landscape: true);

            previewControl = new PrintPreviewControl
            {
                Dock = DockStyle.Fill,
                Document = printDoc,
                AutoZoom = true,
                UseAntiAlias = true,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            btnPrint = UIHelper.CreateButton("🖨️ چاپ روی چک", UIHelper.ActionGreen, 200, 45);
            var btnPanel = new Panel { Dock = DockStyle.Fill };
            btnPrint.Anchor = AnchorStyles.None;
            btnPanel.Controls.Add(btnPrint);
            btnPanel.Resize += (s, e) => { btnPrint.Left = (btnPanel.Width - btnPrint.Width) / 2; btnPrint.Top = (btnPanel.Height - btnPrint.Height) / 2; };

            btnPrint.Click += (s, e) =>
            {
                var printDialog = new PrintDialog { Document = printDoc };
                if (printDialog.ShowDialog() == DialogResult.OK)
                {
                    _isActualPrinting = true;

                    try
                    {
                        SetPaperSize(landscape: false);
                        printDoc.Print();
                    }
                    finally
                    {
                        _isActualPrinting = false;
                        SetPaperSize(landscape: true);
                    }
                }
            };

            mainLayout.Controls.Add(pnlControls, 0, 0);
            mainLayout.Controls.Add(previewControl, 0, 1);
            mainLayout.Controls.Add(btnPanel, 0, 2);
            this.Controls.Add(mainLayout);

            UpdateUIFromSetting();
        }

        private void SetPaperSize(bool landscape)
        {
            if (landscape)
            {
                int widthInHundredths = (int)(165 * 100 / 25.4);
                int heightInHundredths = (int)(75 * 100 / 25.4);
                printDoc.DefaultPageSettings.PaperSize = new PaperSize("ChequeLandscape", widthInHundredths, heightInHundredths);
            }
            else
            {
                int widthInHundredths = (int)(75 * 100 / 25.4);
                int heightInHundredths = (int)(165 * 100 / 25.4);
                printDoc.DefaultPageSettings.PaperSize = new PaperSize("ChequePortrait", widthInHundredths, heightInHundredths);
            }
            printDoc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        }

        // ═══════════════════════════════════════════════════════════════
        // ⚠️ هیچ تغییری در PrintDoc_PrintPage داده نشده — عیناً همان قبلی
        // ═══════════════════════════════════════════════════════════════
        private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics!;
            g.PageUnit = GraphicsUnit.Millimeter;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            if (_isActualPrinting)
            {
                g.TranslateTransform(0, 165);
                g.RotateTransform(270);
            }

            Brush brush = Brushes.Black;

            using StringFormat sfRight = new StringFormat
            {
                Alignment = StringAlignment.Near,
                FormatFlags = StringFormatFlags.DirectionRightToLeft
            };
            using StringFormat sfLeftLTR = new StringFormat { Alignment = StringAlignment.Near };
            using StringFormat sfCenterLTR = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            string dateStr = DateHelper.ToPersianDate(_check.DueDate);
            var dateParts = dateStr.Split('/');

            if (dateParts.Length >= 3)
            {
                var setYear = _settings["تاریخ (سال)"];
                var setMonth = _settings["تاریخ (ماه)"];
                var setDay = _settings["تاریخ (روز)"];

                using (Font fontYear = new Font("B Nazanin", setYear.FontSize, FontStyle.Bold))
                    g.DrawString(dateParts[0], fontYear, brush, setYear.X, setYear.Y, sfRight);

                using (Font fontMonth = new Font("B Nazanin", setMonth.FontSize, FontStyle.Bold))
                    g.DrawString(dateParts[1], fontMonth, brush, setMonth.X, setMonth.Y, sfRight);

                using (Font fontDay = new Font("B Nazanin", setDay.FontSize, FontStyle.Bold))
                    g.DrawString(dateParts[2], fontDay, brush, setDay.X, setDay.Y, sfRight);
            }

            var setDateW = _settings["تاریخ (حروف)"];
            using (Font fontSmall = new Font("B Nazanin", setDateW.FontSize, FontStyle.Regular))
            {
                g.DrawString(DateToWords(_check.DueDate), fontSmall, brush, setDateW.X, setDateW.Y, sfRight);
            }

            var setName = _settings["نام گیرنده و بابت"];
            using (Font fontLarge = new Font("B Nazanin", setName.FontSize, FontStyle.Bold))
            {
                string combinedName = _check.RecipientName ?? "";
                if (!string.IsNullOrWhiteSpace(_check.Description))
                {
                    combinedName += $"   بابت {_check.Description}";
                }
                g.DrawString(combinedName, fontLarge, brush, setName.X, setName.Y, sfRight);
            }

            var setNatId = _settings["کد ملی"];
            using (Font fontNatId = new Font("B Nazanin", setNatId.FontSize, FontStyle.Bold))
            {
                g.DrawString(_check.RecipientNationalId ?? "", fontNatId, brush, setNatId.X, setNatId.Y, sfRight);
            }

            var setAmtW = _settings["مبلغ (حروف)"];
            using (Font fontNormal = new Font("B Nazanin", setAmtW.FontSize, FontStyle.Regular))
            {
                g.DrawString(NumberHelper.ToPersianWords(_check.Amount) + " ریال", fontNormal, brush, setAmtW.X, setAmtW.Y, sfRight);
            }

            var setGrid = _settings["مبلغ (مقطعی)"];
            using (Font fontMonoGrid = new Font("Courier New", setGrid.FontSize, FontStyle.Bold))
            {
                string paddedAmount = ((long)_check.Amount).ToString().PadLeft(16, 'X');
                string spacedAmount = string.Join(" ", paddedAmount.ToCharArray());
                g.DrawString(spacedAmount, fontMonoGrid, brush, setGrid.X, setGrid.Y, sfLeftLTR);
            }

            var setAmtG = _settings["مبلغ (گرافیکی)"];
            using (Font fontMonoAmt = new Font("Courier New", setAmtG.FontSize, FontStyle.Bold))
            using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(220, 220, 220)))
            using (Pen borderPen = new Pen(Color.Black, 0.4f))
            {
                string amountStr = $"[{_check.Amount:N0} RL]";

                SizeF textSize = g.MeasureString(amountStr, fontMonoAmt);
                float paddingX = 2f;
                float paddingY = 1f;
                float boxW = textSize.Width + (paddingX * 2);
                float boxH = textSize.Height + (paddingY * 2);

                g.FillRectangle(fillBrush, setAmtG.X, setAmtG.Y, boxW, boxH);
                g.DrawRectangle(borderPen, setAmtG.X, setAmtG.Y, boxW, boxH);
                g.DrawString(amountStr, fontMonoAmt, Brushes.Black, new RectangleF(setAmtG.X, setAmtG.Y, boxW, boxH), sfCenterLTR);
            }
        }

        // ✅ FIX: استفاده از InvariantCulture + try/catch برای نوشتن
        private void SaveSettings()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                StringBuilder sb = new StringBuilder();
                foreach (var kvp in _settings)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}|{1}|{2}|{3}",
                        kvp.Key,
                        kvp.Value.X,
                        kvp.Value.Y,
                        kvp.Value.FontSize));
                }
                File.WriteAllText(_settingsFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ModernMessageBox.ShowError("خطا در ذخیره تنظیمات:\n" + ex.Message);
            }
        }

        // ✅ FIX: try/catch برای هر خط + TryParse (نه Parse) → فایل خراب کرش نمی‌کند
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath)) return;

                var lines = File.ReadAllLines(_settingsFilePath, Encoding.UTF8);
                int loadedCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var parts = line.Split('|');
                        if (parts.Length != 4) continue;
                        if (!_settings.ContainsKey(parts[0])) continue;

                        if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float size))
                        {
                            // اعتبارسنجی محدوده‌ها (همان محدوده‌های کنترل‌های UI)
                            x = Math.Max(-20f, Math.Min(200f, x));
                            y = Math.Max(-20f, Math.Min(150f, y));
                            size = Math.Max(6f, Math.Min(24f, size));

                            _settings[parts[0]].X = x;
                            _settings[parts[0]].Y = y;
                            _settings[parts[0]].FontSize = size;
                            loadedCount++;
                        }
                    }
                    catch
                    {
                        // خط معیوب را نادیده بگیر و به بعدی برو
                        continue;
                    }
                }

                if (loadedCount == 0)
                {
                    // هیچ خط سالمی خوانده نشد — از پیش‌فرض استفاده کن
                    InitializeDefaults();
                }
            }
            catch
            {
                // فایل خراب است یا دسترسی ندارد — پیش‌فرض‌ها حفظ می‌شوند (بی‌صدا)
                InitializeDefaults();
            }
        }

        private void InitializeDefaults()
        {
            _settings["تاریخ (سال)"] = new ChequeSetting { X = 130, Y = 6, FontSize = 12 };
            _settings["تاریخ (ماه)"] = new ChequeSetting { X = 145, Y = 6, FontSize = 12 };
            _settings["تاریخ (روز)"] = new ChequeSetting { X = 160, Y = 6, FontSize = 12 };
            _settings["تاریخ (حروف)"] = new ChequeSetting { X = 150, Y = 16, FontSize = 10 };
            _settings["نام گیرنده و بابت"] = new ChequeSetting { X = 150, Y = 27, FontSize = 14 };
            _settings["کد ملی"] = new ChequeSetting { X = 55, Y = 27, FontSize = 14 };
            _settings["مبلغ (حروف)"] = new ChequeSetting { X = 150, Y = 42, FontSize = 12 };
            _settings["مبلغ (مقطعی)"] = new ChequeSetting { X = 10, Y = 54, FontSize = 10 };
            _settings["مبلغ (گرافیکی)"] = new ChequeSetting { X = 22, Y = 62, FontSize = 11 };
        }

        private void UpdateUIFromSetting()
        {
            if (!_settings.TryGetValue(_selectedField, out var setting)) return;
            _isUpdatingUI = true;
            try
            {
                numX.Value = (decimal)setting.X;
                numY.Value = (decimal)setting.Y;
                numSize.Value = (decimal)setting.FontSize;
            }
            finally { _isUpdatingUI = false; }
        }

        private void UpdateSetting(object? sender, EventArgs e)
        {
            if (_isUpdatingUI) return;
            if (_settings.TryGetValue(_selectedField, out var setting))
            {
                setting.X = (float)numX.Value;
                setting.Y = (float)numY.Value;
                setting.FontSize = (float)numSize.Value;
                previewControl.InvalidatePreview();
            }
        }

        private string DateToWords(DateTime date)
        {
            var pc = new System.Globalization.PersianCalendar();
            int day = pc.GetDayOfMonth(date);
            int month = pc.GetMonth(date);
            int year = pc.GetYear(date);

            string[] monthNames = { "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

            string dayStr = GetDayWord(day);
            string yearStr = NumberHelper.ToPersianWords(year);

            return $"{dayStr} {monthNames[month]} ماه سال {yearStr}";
        }

        private string GetDayWord(int day)
        {
            if (day == 1) return "یکم";
            if (day == 2) return "دوم";
            if (day == 3) return "سوم";
            if (day == 30) return "سی ام";
            if (day == 31) return "سی و یکم";
            return NumberHelper.ToPersianWords(day) + "م";
        }
    }
}