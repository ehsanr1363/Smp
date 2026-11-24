using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Smp.Helpers;
using Smp.Models;
using Smp.Data;

namespace Smp.Forms.Controls;

public class ucEmployeeSalary : UserControl
{
    private DataGridView dgvSalary = null!;
    private Label lblGrandTotal = null!;
    private DataTable _dataTable = null!;

    private Employee? _currentEmployee;
    private SalarySettings? _currentSettings;

    // خلاصه‌ی آخرین قرارداد (بالای تب)
    private Label lblContractNumber = null!, lblContractStart = null!, lblContractEnd = null!, lblContractStatus = null!;

    private static readonly Color CardHeaderBack = ModernTheme.GridHover;
    private static readonly Color MutedText = Color.FromArgb(120, 113, 108);
    private static readonly Color OkGreen = Color.FromArgb(22, 101, 52);
    private static readonly Color WarnOrange = Color.FromArgb(194, 94, 12);
    private static readonly Color DangerRed = Color.FromArgb(185, 28, 28);

    public ucEmployeeSalary()
    {
        this.Dock = DockStyle.Fill;
        this.RightToLeft = RightToLeft.Yes;
        BuildUI();
    }

    // نوع پرسنلی که کاربر در تب «اطلاعات شغلی» (هنوز ذخیره‌نشده) انتخاب کرده؛ null = همان نوع ذخیره‌شده‌ی کارمند
    private string? _typeOverride;

    private enum SalaryKind { Monthly, Daily, Hourly }

    private static SalaryKind ResolveKind(string? personnelType)
    {
        string t = StringHelper.NormalizePersianText(personnelType ?? "").Replace(" ", "");
        if (t == "روزمزد") return SalaryKind.Daily;
        if (t == "ساعتی") return SalaryKind.Hourly;
        return SalaryKind.Monthly;
    }

    public void LoadData(Employee e, SalarySettings? settings = null)
    {
        _currentEmployee = e;
        _currentSettings = settings;
        _typeOverride = null;
        Render();
    }

    /// <summary>وقتی نوع پرسنلی در تب شغلی عوض شد (قبل از ذخیره)، آیتم‌های این تب را متناسب با آن نشان می‌دهد.</summary>
    public void SetPersonnelType(string? personnelType)
    {
        if (_currentEmployee is null || string.IsNullOrWhiteSpace(personnelType)) return;
        if (ResolveKind(personnelType) == ResolveKind(_typeOverride ?? _currentEmployee.PersonnelType)) return;
        _typeOverride = personnelType;
        Render();
    }

    private void Render()
    {
        var e = _currentEmployee;
        if (e is null) return;
        var settings = _currentSettings;

        UpdateContractSummary(e);
        _dataTable.Rows.Clear();

        switch (ResolveKind(_typeOverride ?? e.PersonnelType))
        {
            case SalaryKind.Daily:
            {
                // کارگر روزمزد: فقط دستمزد روزانه‌ی قرارداد (آیتم‌های ماهانه/قراردادی نمایش داده نمی‌شود)
                AddRow("DailyWage", "دستمزد روزانه", e.DailyWage, settings?.TaxIncludeBaseSalary ?? true, true);
                string cat = string.IsNullOrWhiteSpace(e.DailyWageCategory) ? "" : $"نوع کارگر: {e.DailyWageCategory}   |   ";
                SetTotalText(cat + "دستمزد روزانه: ", e.DailyWage);
                return;
            }
            case SalaryKind.Hourly:
            {
                // پرسنل ساعتی: فقط مبلغ ساعتی قرارداد
                AddRow("HourlyWage", "مبلغ ساعتی", e.HourlyWage, settings?.TaxIncludeBaseSalary ?? true, true);
                SetTotalText("مبلغ ساعتی: ", e.HourlyWage);
                return;
            }
        }

        // پرسنل قراردادی / ماهانه
        decimal housing = e.HousingAllowance;
        decimal grocery = e.GroceryAllowance;
        decimal marriage = e.MarriageAllowance;
        decimal child = e.ChildAllowance;

        if (settings is not null)
        {
            housing = settings.HousingAllowance;
            grocery = settings.FoodAllowance;

            string marital = StringHelper.NormalizePersianText(e.MaritalStatus ?? "");
            marriage = (marital.Contains("متاهل") || marital.Contains("متأهل")) ? settings.MarriageAllowance : 0;

            decimal childRate = settings.ChildAllowanceRate > 1 ? settings.ChildAllowanceRate / 100m : settings.ChildAllowanceRate;
            child = Math.Round(settings.MinimumWage * childRate * e.ChildrenCount, 0);
        }

        AddRow("BaseSalary", "حقوق پایه", e.BaseSalary, settings?.TaxIncludeBaseSalary ?? true, true);
        AddRow("YearsBonus", "پایه سنوات", e.YearsBonus, settings?.TaxIncludeYearsBonus ?? true, true);
        AddRow("AttractionAllowance", "حق جذب", e.AttractionAllowance, settings?.TaxIncludeAttraction ?? true, true);
        AddRow("ResponsibilityAllowance", "حق مسئولیت", e.ResponsibilityAllowance, settings?.TaxIncludeResponsibility ?? true, true);
        AddRow("ManagementAllowance", "حق مدیریت", e.ManagementAllowance, settings?.TaxIncludeManagement ?? true, true);
        AddRow("TechnicalAllowance", "حق فنی", e.TechnicalAllowance, settings?.TaxIncludeTechnical ?? true, true);
        AddRow("TransportationAllowance", "حق ایاب و ذهاب", e.TransportationAllowance, settings?.TaxIncludeTransportation ?? false, false);

        AddRow("HousingAllowance", "حق مسکن", housing, settings?.TaxIncludeHousing ?? false, true);
        AddRow("GroceryAllowance", "بن و خواربار", grocery, settings?.TaxIncludeGrocery ?? false, true);
        AddRow("ChildAllowance", "حق اولاد", child, settings?.TaxIncludeChild ?? false, false);
        AddRow("MarriageAllowance", "حق تاهل", marriage, settings?.TaxIncludeMarriage ?? true, false);
        AddRow("OtherAllowances", "سایر مزایا", e.OtherAllowances, settings?.TaxIncludeOtherAllowances ?? true, true);

        CalculateTotals();
    }

    private void SetTotalText(string prefix, decimal amount)
    {
        lblGrandTotal.Text = prefix + NumberHelper.FormatThousands(amount, usePersianDigits: true) + " ریال";
    }

    // 👈 متد SaveData که باعث خطا شده بود، دوباره اضافه شد (خالی است چون جدول فقط خواندنی است)
    public void SaveData(Employee e)
    {
        // چون فرم ReadOnly شده، دیگر نیازی به ذخیره از اینجا نیست
    }

    private void AddRow(string key, string title, decimal amount, bool isTaxable, bool isInsurable)
    {
        _dataTable.Rows.Add(key, title, amount, isTaxable, isInsurable);
    }

    private void BuildUI()
    {
        this.BackColor = ModernTheme.Background;

        // ─── کارت «آخرین قرارداد» ───
        var summaryCard = BuildSummaryCard();

        // ─── جدول آیتم‌ها ───
        _dataTable = new DataTable();
        _dataTable.Columns.Add("Key", typeof(string));
        _dataTable.Columns.Add("Title", typeof(string));
        _dataTable.Columns.Add("Amount", typeof(decimal));
        _dataTable.Columns.Add("IsTaxable", typeof(bool));
        _dataTable.Columns.Add("IsInsurable", typeof(bool));

        dgvSalary = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            TabStop = false
        };
        dgvSalary.ApplyModernStyle();
        dgvSalary.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvSalary.UseAiryLook(46);
        dgvSalary.BorderStyle = BorderStyle.None;

        dgvSalary.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Title", HeaderText = "شرح آیتم", FillWeight = 34 });
        dgvSalary.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "Amount",
            HeaderText = "مبلغ (ریال)",
            FillWeight = 30,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N0", Font = ModernTheme.GetFont(12f, FontStyle.Bold) }
        });
        dgvSalary.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsInsurable", HeaderText = "مشمول بیمه", FillWeight = 18 });
        dgvSalary.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsTaxable", HeaderText = "مشمول مالیات", FillWeight = 18 });

        dgvSalary.DataSource = _dataTable;

        // جدول فقط نمایشی است؛ هایلایت انتخاب مزاحم است. مبلغ‌های صفر کم‌رنگ نشان داده می‌شوند.
        dgvSalary.SelectionChanged += (s, e) => dgvSalary.ClearSelection();
        dgvSalary.DataBindingComplete += (s, e) => dgvSalary.ClearSelection();
        dgvSalary.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 1) return;
            if (e.Value is decimal d && d == 0) e.CellStyle.ForeColor = MutedText;
        };

        var gridCard = new ModernCard("💰  اقلام حقوق و مزایا", ModernTheme.Teal, 1);
        gridCard.Margin = new Padding(0, 0, 0, 12);
        gridCard.Body.Controls.Add(dgvSalary, 0, 0);

        // ─── نوار جمع ───
        var totalsCard = new ModernCard("", ModernTheme.Success, 1, 46)
        {
            FillColor = Color.FromArgb(240, 253, 244),
            BorderColor = Color.FromArgb(187, 247, 208)
        };
        lblGrandTotal = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(14f, FontStyle.Bold),
            ForeColor = OkGreen,
            BackColor = Color.Transparent,
            Text = "جمع کل حقوق و مزایا: 0 ریال"
        };
        totalsCard.Body.Controls.Add(lblGrandTotal, 0, 0);

        // ─── دکمه‌ها ───
        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = Color.Transparent
        };

        var btnManageContract = ModernButton.Create("📝  تنظیم مبالغ قرارداد", ModernTheme.Primary, 220, 44);
        btnManageContract.Click += BtnManageContract_Click;

        var btnArchive = ModernButton.Create("📂  آرشیو قراردادها", ModernTheme.Teal, 190, 44, ModernButtonStyle.Soft);
        btnArchive.Click += BtnArchive_Click;

        pnlButtons.Controls.Add(btnManageContract);
        pnlButtons.Controls.Add(btnArchive);

        var mainLayout = new BufferedTable
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(20, 14, 20, 6),
            BackColor = ModernTheme.Background
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, summaryCard.Height + 12));   // خلاصه‌ی آخرین قرارداد
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));                        // جدول آیتم‌ها
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, totalsCard.Height + 6));     // جمع
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));                        // دکمه‌ها

        summaryCard.Dock = DockStyle.Fill;
        summaryCard.Margin = new Padding(0, 0, 0, 12);
        gridCard.Dock = DockStyle.Fill;
        totalsCard.Dock = DockStyle.Fill;
        totalsCard.Margin = new Padding(0, 0, 0, 6);

        mainLayout.Controls.Add(summaryCard, 0, 0);
        mainLayout.Controls.Add(gridCard, 0, 1);
        mainLayout.Controls.Add(totalsCard, 0, 2);
        mainLayout.Controls.Add(pnlButtons, 0, 3);

        this.Controls.Add(mainLayout);
    }

    /// <summary>کارت بالای تب: شماره، تاریخ شروع، تاریخ پایان و وضعیت آخرین قرارداد.</summary>
    private ModernCard BuildSummaryCard()
    {
        var card = new ModernCard("📄  آخرین قرارداد", ModernTheme.Primary, 4, 42);
        card.Body.Controls.Add(MakeChip("شماره قرارداد", out lblContractNumber, drawSeparator: true), 0, 0);
        card.Body.Controls.Add(MakeChip("تاریخ شروع", out lblContractStart, drawSeparator: true), 1, 0);
        card.Body.Controls.Add(MakeChip("تاریخ پایان", out lblContractEnd, drawSeparator: true), 2, 0);
        card.Body.Controls.Add(MakeChip("وضعیت", out lblContractStatus, drawSeparator: false), 3, 0);
        return card;
    }

    /// <summary>برچسب با رسم دستی: عنوان و مقدار هر دو دقیقاً با یک لبه‌ی راست و یک روش تراز رسم می‌شوند</summary>
    private sealed class ChipText : Label
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            const TextFormatFlags flags = TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.VerticalCenter |
                                          TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, flags);
        }
    }

    private static Panel MakeChip(string caption, out Label value, bool drawSeparator)
    {
        var chip = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = new Padding(0, 0, 16, 0), RightToLeft = RightToLeft.Yes };
        if (drawSeparator)
        {
            chip.Paint += (s, e) =>
            {
                using var pen = new Pen(ModernTheme.Border, 1f);
                e.Graphics.DrawLine(pen, 0, 8, 0, chip.Height - 8);
            };
        }

        var cap = new ChipText
        {
            Dock = DockStyle.Top,
            Height = 17,
            Text = caption,
            Font = ModernTheme.GetFont(9f),
            ForeColor = MutedText,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes
        };
        value = new ChipText
        {
            Dock = DockStyle.Fill,
            Text = "—",
            Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
            ForeColor = ModernTheme.Text,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes
        };
        chip.Controls.Add(value);   // Fill (اول)
        chip.Controls.Add(cap);     // Top (بعد)
        return chip;
    }

    private static string ShowDate(DateTime? d)
    {
        if (!d.HasValue || d.Value.Year <= 1900) return "—";
        return StringHelper.ConvertToPersianDigits(DateHelper.ToPersianDate(d.Value));
    }

    private void UpdateContractSummary(Employee e)
    {
        string number = string.IsNullOrWhiteSpace(e.ContractNumber) ? "—" : StringHelper.ConvertToPersianDigits(e.ContractNumber.Trim());
        bool hasStart = e.ContractStartDate.HasValue && e.ContractStartDate.Value.Year > 1900;
        bool hasEnd = e.ContractEndDate.HasValue && e.ContractEndDate.Value.Year > 1900;

        // شماره قرارداد را در یک بلوک چپ‌به‌راست قرار می‌دهیم تا ترتیب حروف/اعداد جابجا نشود؛ ولی تراز مثل بقیه‌ی چیپ‌ها راست می‌ماند
        lblContractNumber.Text = number == "—" ? number : "\u202A" + number + "\u202C";
        lblContractStart.Text = ShowDate(e.ContractStartDate);
        lblContractEnd.Text = hasEnd ? ShowDate(e.ContractEndDate) : (hasStart ? "بدون تاریخ پایان" : "—");
        lblContractEnd.ForeColor = hasEnd ? ModernTheme.Text : MutedText;

        if (!hasStart)
        {
            lblContractStatus.Text = "قراردادی ثبت نشده";
            lblContractStatus.ForeColor = MutedText;
            return;
        }

        if (!hasEnd)
        {
            lblContractStatus.Text = "فعال";
            lblContractStatus.ForeColor = OkGreen;
            return;
        }

        int daysLeft = (e.ContractEndDate!.Value.Date - DateTime.Today).Days;
        string daysText = StringHelper.ConvertToPersianDigits(Math.Abs(daysLeft).ToString());
        if (daysLeft < 0)
        {
            lblContractStatus.Text = $"پایان‌یافته ({daysText} روز پیش)";
            lblContractStatus.ForeColor = DangerRed;
        }
        else if (daysLeft <= 30)
        {
            lblContractStatus.Text = daysLeft == 0 ? "امروز پایان می‌یابد" : $"{daysText} روز تا پایان";
            lblContractStatus.ForeColor = WarnOrange;
        }
        else
        {
            lblContractStatus.Text = $"فعال ({daysText} روز مانده)";
            lblContractStatus.ForeColor = OkGreen;
        }
    }

    private void BtnManageContract_Click(object? sender, EventArgs e)
    {
        if (_currentEmployee is null || _currentEmployee.Id <= 0)
        {
            ModernMessageBox.ShowWarning("ابتدا اطلاعات هویتی کارمند را ذخیره کنید.");
            return;
        }

        using var form = new EmployeeContractForm(AppConfig.ConnectionString, _currentEmployee);
        if (form.ShowDialog(this.FindForm()) == DialogResult.OK)
        {
            var empRepo = new EmployeeRepository(AppConfig.ConnectionString);
            var updatedEmp = empRepo.GetById(_currentEmployee.Id);
            if (updatedEmp is not null)
            {
                LoadData(updatedEmp, _currentSettings);
            }
        }
    }

    private void BtnArchive_Click(object? sender, EventArgs e)
    {
        if (_currentEmployee is null || _currentEmployee.Id <= 0) return;
        using var f = new ContractArchiveViewForm(AppConfig.ConnectionString, _currentEmployee.Id, _currentEmployee.FullName);
        f.ShowDialog(this.FindForm());
    }

    private void CalculateTotals()
    {
        decimal grandTotal = 0;
        foreach (DataRow row in _dataTable.Rows)
        {
            grandTotal += Convert.ToDecimal(row["Amount"]);
        }
        lblGrandTotal.Text = "جمع کل حقوق و مزایا: " + NumberHelper.FormatThousands(grandTotal, usePersianDigits: true) + " ریال";
    }
}
