using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;

namespace Smp.Forms.Controls;

/// <summary>
/// تب «اطلاعات شغلی» کارمند — طراحی کارتی در سه بخش:
/// ۱) اطلاعات سازمانی  ۲) شرایط خدمت  ۳) دستمزد روزمزد (فقط برای پرسنل روزمزد فعال است).
/// طبقه‌ی کارگری و دستمزد روزانه از «تنظیمات حقوق سالانه ← حقوق روزمزدها» خوانده می‌شود.
/// </summary>
public class ucEmployeeJob : UserControl
{
    private readonly CompanyService _companyService;
    private readonly DepartmentService _departmentService;
    private readonly LookupService _lookupService;

    private ComboBox cmbCompany = null!, cmbDepartment = null!;
    private TextBox txtDepartmentAddress = null!, txtPosition = null!;
    private ComboBox cmbPersonnelType = null!, cmbWorkLocation = null!;

    // بخش روزمزد
    private ComboBox cmbDailyWageCategory = null!;
    private TextBox txtDailyWage = null!;
    private Label lblDailyHint = null!;
    private TableLayoutPanel bodyDaily = null!;
    private Label headerDaily = null!;

    // نرخ‌های روزمزد (طبقه ← دستمزد روزانه) و سالی که از آن خوانده شده
    private int _ratesYear;
    private List<DailyWageRateItem> _rateItems = new();
    private readonly Dictionary<string, decimal> _rates = new();
    private bool _suppressCategoryEvent;

    private static readonly Color CardHeaderBack = ModernTheme.GridHover;
    private static readonly Color MutedText = Color.FromArgb(120, 113, 108);
    private static readonly Color OkGreen = Color.FromArgb(22, 101, 52);

    private static int CurrentPersianYear => new PersianCalendar().GetYear(DateTime.Now);

    public ucEmployeeJob(CompanyService companyService, DepartmentService departmentService, LookupService lookupService)
    {
        this.Dock = DockStyle.Fill;
        this.RightToLeft = RightToLeft.Yes;
        this.BackColor = ModernTheme.Background;

        _companyService = companyService;
        _departmentService = departmentService;
        _lookupService = lookupService;

        _ratesYear = CurrentPersianYear;
        BuildUI();
        ApplyDailyState();
    }

    /// <summary>نوع پرسنلیِ انتخاب‌شده‌ی فعلی (حتی اگر هنوز ذخیره نشده باشد).</summary>
    public string CurrentPersonnelType => cmbPersonnelType?.Text ?? "";

    // ═══════════════════════════ بارگذاری ═══════════════════════════
    public async Task LoadCombosAsync()
    {
        if (IsDisposed) return;

        await AsyncHelper.RunWithLoading(this.FindForm()!, async () =>
        {
            try
            {
                int year = CurrentPersianYear;

                var personnelTypeTask = _lookupService.GetByCategoryAsync("PersonnelType");
                var workLocationTask = _lookupService.GetByCategoryAsync("WorkLocation");
                var companiesTask = Task.Run(() => _companyService.GetAll());
                var departmentsTask = Task.Run(() => _departmentService.GetAll());
                var ratesTask = Task.Run(() => SafeGetRates(year));

                await Task.WhenAll(personnelTypeTask, workLocationTask, companiesTask, departmentsTask, ratesTask);

                if (IsDisposed) return;

                cmbPersonnelType.DisplayMember = "Value"; cmbPersonnelType.ValueMember = "Id"; cmbPersonnelType.DataSource = personnelTypeTask.Result;
                cmbWorkLocation.DisplayMember = "Value"; cmbWorkLocation.ValueMember = "Id"; cmbWorkLocation.DataSource = workLocationTask.Result;

                cmbCompany.DataSource = companiesTask.Result; cmbCompany.DisplayMember = "Name"; cmbCompany.ValueMember = "Id"; cmbCompany.SelectedIndex = -1;
                cmbDepartment.DataSource = departmentsTask.Result; cmbDepartment.DisplayMember = "Name"; cmbDepartment.ValueMember = "Id";

                SetRates(ratesTask.Result.Rates, ratesTask.Result.UsedYear);
                ApplyDailyState();
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ucEmployeeJob.LoadCombosAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات شغلی:\n" + ex.Message);
            }
        }, "در حال بارگذاری اطلاعات شغلی...");
    }

    private static (List<DailyWageRateItem> Rates, int UsedYear) SafeGetRates(int year)
    {
        try { return new DailyWageRateRepository(AppConfig.ConnectionString).GetRatesWithFallback(year); }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ucEmployeeJob.SafeGetRates");
            return (new List<DailyWageRateItem>(), year);
        }
    }

    private void SetRates(List<DailyWageRateItem> items, int usedYear)
    {
        _ratesYear = usedYear;
        _rateItems = items;
        _rates.Clear();
        foreach (var r in items)
        {
            string key = StringHelper.NormalizePersianText(r.Category ?? "").Trim();
            if (key.Length > 0 && r.Amount > 0) _rates[key] = r.Amount;
        }

        string? keep = cmbDailyWageCategory.SelectedItem as string;
        _suppressCategoryEvent = true;
        try
        {
            cmbDailyWageCategory.Items.Clear();
            cmbDailyWageCategory.Items.AddRange(
                items.Select(r => r.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Cast<object>().ToArray());
        }
        finally { _suppressCategoryEvent = false; }

        if (!string.IsNullOrWhiteSpace(keep)) SelectCategory(keep, addIfMissing: false);
    }

    public void LoadData(Employee e)
    {
        if (IsDisposed || e is null) return;

        if (e.CompanyId.HasValue && cmbCompany is not null) try { cmbCompany.SelectedValue = e.CompanyId.Value; } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ucEmployeeJob"); }
        if (e.DepartmentId.HasValue && cmbDepartment is not null) try { cmbDepartment.SelectedValue = e.DepartmentId.Value; } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ucEmployeeJob"); }

        txtPosition.Text = e.Position ?? "";

        SelectComboByText(cmbPersonnelType, e.PersonnelType);
        SelectComboByText(cmbWorkLocation, e.WorkLocation);

        // بخش روزمزد برای هر پرسنل روزمزد فعال می‌شود (حتی اگر هنوز طبقه‌ای انتخاب نشده باشد)
        ApplyDailyState();
        if (IsDailyType(cmbPersonnelType.Text) && !string.IsNullOrWhiteSpace(e.DailyWageCategory))
            SelectCategory(e.DailyWageCategory, addIfMissing: true);
        UpdateWageDisplay();
    }

    // ═══════════════════════════ ذخیره و اعتبارسنجی ═══════════════════════════
    public void SaveData(Employee emp)
    {
        if (IsDisposed || emp is null) return;

        emp.CompanyId = cmbCompany.SelectedValue is not null ? Convert.ToInt32(cmbCompany.SelectedValue) : (int?)null;
        emp.DepartmentId = GetSelectedDepartmentId();
        emp.Position = txtPosition.Text.Trim();
        emp.PersonnelType = cmbPersonnelType.Text;
        emp.WorkLocation = cmbWorkLocation.Text;

        bool isDaily = IsDailyType(cmbPersonnelType.Text);
        string cat = cmbDailyWageCategory.SelectedIndex >= 0 ? (cmbDailyWageCategory.Text?.Trim() ?? "") : "";
        emp.DailyWageCategory = (isDaily && cat.Length > 0) ? cat : null;

        // دستمزد روزانه‌ی طبقه (در صورت ثبت‌بودن نرخ) همراه کارمند ذخیره می‌شود؛ محاسبه‌ی حقوق همچنان نرخ سالِ حقوق را از تنظیمات می‌خواند
        if (emp.DailyWageCategory is not null && TryGetRate(emp.DailyWageCategory, out decimal rate))
            emp.DailyWage = rate;
    }

    /// <summary>پیام خطا برای فرم والد قبل از ذخیره؛ null یعنی مشکلی نیست.</summary>
    public string? ValidateJob()
    {
        if (IsDailyType(cmbPersonnelType.Text) && cmbDailyWageCategory.SelectedIndex < 0)
            return "برای پرسنل روزمزد باید «طبقه‌ی کارگری» انتخاب شود.\nاگر طبقه‌ای در فهرست نیست، ابتدا در «تنظیمات حقوق سالانه ← حقوق روزمزدها» تعریفش کنید.";
        return null;
    }

    // ═══════════════════════════ منطق بخش روزمزد ═══════════════════════════
    private static bool IsDailyType(string? type) =>
        StringHelper.NormalizePersianText(type ?? "").Replace(" ", "") == "روزمزد";

    private bool TryGetRate(string? category, out decimal rate)
    {
        rate = 0;
        string key = StringHelper.NormalizePersianText(category ?? "").Trim();
        return key.Length > 0 && _rates.TryGetValue(key, out rate) && rate > 0;
    }

    private void SelectCategory(string? text, bool addIfMissing)
    {
        if (string.IsNullOrWhiteSpace(text)) { cmbDailyWageCategory.SelectedIndex = -1; return; }
        string key = StringHelper.NormalizePersianText(text).Trim();

        for (int i = 0; i < cmbDailyWageCategory.Items.Count; i++)
        {
            string item = StringHelper.NormalizePersianText(Convert.ToString(cmbDailyWageCategory.Items[i]) ?? "").Trim();
            if (item == key) { cmbDailyWageCategory.SelectedIndex = i; return; }
        }

        if (addIfMissing)
        {
            // طبقه‌ی قبلاً ثبت‌شده‌ی کارمند که در فهرست فعلی نیست (مثلاً از سال قبل): نگه داشته می‌شود تا اطلاعات از بین نرود
            cmbDailyWageCategory.Items.Add(text.Trim());
            cmbDailyWageCategory.SelectedIndex = cmbDailyWageCategory.Items.Count - 1;
        }
        else cmbDailyWageCategory.SelectedIndex = -1;
    }

    /// <summary>بخش روزمزد فقط برای «نوع پرسنلی = روزمزد» فعال است.</summary>
    private void ApplyDailyState()
    {
        if (cmbDailyWageCategory is null || bodyDaily is null) return;
        bool isDaily = IsDailyType(cmbPersonnelType?.Text);

        bodyDaily.Enabled = isDaily;
        headerDaily.Text = isDaily ? "💼  دستمزد روزمزد" : "💼  دستمزد روزمزد  (فقط برای پرسنل روزمزد فعال می‌شود)";
        headerDaily.ForeColor = isDaily ? ModernTheme.Text : MutedText;
        headerDaily.Invalidate();

        if (!isDaily && cmbDailyWageCategory.SelectedIndex != -1)
        {
            _suppressCategoryEvent = true;
            try { cmbDailyWageCategory.SelectedIndex = -1; }
            finally { _suppressCategoryEvent = false; }
        }
        UpdateWageDisplay();
    }

    private void UpdateWageDisplay()
    {
        if (txtDailyWage is null) return;
        bool isDaily = IsDailyType(cmbPersonnelType?.Text);
        string yearText = StringHelper.ConvertToPersianDigits(_ratesYear.ToString());

        if (!isDaily)
        {
            txtDailyWage.Text = "—";
            txtDailyWage.ForeColor = MutedText;
            lblDailyHint.ForeColor = MutedText;
            lblDailyHint.Text = "این بخش فقط برای پرسنلی که «نوع پرسنلی» آن‌ها روزمزد است فعال می‌شود.";
            return;
        }

        if (cmbDailyWageCategory.Items.Count == 0)
        {
            txtDailyWage.Text = "—";
            lblDailyHint.ForeColor = ModernTheme.DangerText;
            lblDailyHint.Text = "⚠️ هنوز طبقه‌ی کارگری تعریف نشده است. ابتدا در «تنظیمات حقوق سالانه ← حقوق روزمزدها» طبقه‌ها و دستمزد روزانه را ثبت کنید.";
            return;
        }

        if (cmbDailyWageCategory.SelectedIndex < 0)
        {
            txtDailyWage.Text = "—";
            txtDailyWage.ForeColor = MutedText;
            lblDailyHint.ForeColor = MutedText;
            lblDailyHint.Text = "نوع کارگر (طبقه) را انتخاب کنید تا دستمزد روزانه‌ی آن نمایش داده شود.";
            return;
        }

        if (TryGetRate(cmbDailyWageCategory.Text, out decimal rate))
        {
            txtDailyWage.Text = NumberHelper.FormatThousands(rate, usePersianDigits: true);
            txtDailyWage.ForeColor = OkGreen;
            lblDailyHint.ForeColor = MutedText;
            lblDailyHint.Text = $"دستمزد روزانه از «تنظیمات حقوق سالانه ← حقوق روزمزدها» (سال {yearText}) خوانده می‌شود و با تغییر نرخ، در محاسبه‌ی حقوق خودکار اعمال می‌شود.";
        }
        else
        {
            txtDailyWage.Text = "—";
            txtDailyWage.ForeColor = ModernTheme.DangerText;
            lblDailyHint.ForeColor = ModernTheme.DangerText;
            lblDailyHint.Text = $"⚠️ برای این طبقه در سال {yearText} نرخی ثبت نشده است. نرخ را در «تنظیمات حقوق سالانه ← حقوق روزمزدها» وارد کنید.";
        }
    }

    // ═══════════════════════════ ساخت رابط کاربری ═══════════════════════════
    private void BuildUI()
    {
        const int F = ModernUI.FieldHeight;
        var page = new ModernPage();

        cmbCompany = ModernUI.Combo();
        cmbDepartment = ModernUI.Combo();
        cmbPersonnelType = ModernUI.Combo();
        cmbWorkLocation = ModernUI.Combo();
        cmbDailyWageCategory = ModernUI.Combo();

        txtDepartmentAddress = new TextBox
        {
            ReadOnly = true,
            TabStop = false,
            BackColor = Color.FromArgb(246, 244, 240),
            RightToLeft = RightToLeft.Yes,
            TextAlign = HorizontalAlignment.Right
        };
        txtPosition = new TextBox();
        txtDailyWage = new TextBox
        {
            ReadOnly = true,
            TabStop = false,
            BackColor = Color.FromArgb(246, 244, 240),
            Font = ModernTheme.GetFont(13f, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Center
        };

        // ─── کارت ۱: اطلاعات سازمانی ───
        var card1 = new ModernCard("🏢  اطلاعات سازمانی", ModernTheme.Primary, 2, F, F);
        card1.Body.Controls.Add(ModernUI.Field("شرکت", cmbCompany), 0, 0);
        card1.Body.Controls.Add(ModernUI.Field("واحد سازمانی", cmbDepartment), 1, 0);
        var fldDeptAddress = ModernUI.Field("آدرس واحد", txtDepartmentAddress);
        card1.Body.Controls.Add(fldDeptAddress, 0, 1);
        card1.Body.SetColumnSpan(fldDeptAddress, 2);

        cmbDepartment.SelectedIndexChanged += (s, e) =>
        {
            if (cmbDepartment.SelectedItem is Department dept) txtDepartmentAddress.Text = dept.Address ?? "";
            else txtDepartmentAddress.Text = "";
        };

        // ─── کارت ۲: شرایط خدمت ───
        var card2 = new ModernCard("🧑‍💼  شرایط خدمت", ModernTheme.Teal, 2, F, F);
        card2.Body.Controls.Add(ModernUI.Field("سمت", txtPosition), 0, 0);
        card2.Body.Controls.Add(ModernUI.Field("نوع پرسنلی", cmbPersonnelType), 1, 0);
        card2.Body.Controls.Add(ModernUI.Field("محل خدمت", cmbWorkLocation), 0, 1);

        cmbPersonnelType.SelectedIndexChanged += (s, e) => ApplyDailyState();
        cmbPersonnelType.TextChanged += (s, e) => ApplyDailyState();

        // ─── کارت ۳: دستمزد روزمزد ───
        var card3 = new ModernCard("💼  دستمزد روزمزد", ModernTheme.Brand, 2, F, 46);
        bodyDaily = card3.Body;
        headerDaily = card3.TitleLabel;

        bodyDaily.Controls.Add(ModernUI.Field("نوع کارگر (طبقه)", cmbDailyWageCategory), 0, 0);
        bodyDaily.Controls.Add(ModernUI.Field("دستمزد روزانه (ریال)", txtDailyWage), 1, 0);

        lblDailyHint = new Label
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            TextAlign = ContentAlignment.MiddleRight,
            Font = ModernTheme.GetFont(10f),
            ForeColor = MutedText,
            Margin = new Padding(8, 0, 8, 0)
        };
        bodyDaily.Controls.Add(lblDailyHint, 0, 1);
        bodyDaily.SetColumnSpan(lblDailyHint, 2);

        cmbDailyWageCategory.SelectedIndexChanged += (s, e) => { if (!_suppressCategoryEvent) UpdateWageDisplay(); };

        page.AddCard(card1);
        page.AddCard(card2);
        page.AddCard(card3);
        Controls.Add(page);
    }

    private int? GetSelectedDepartmentId()
    {
        object? val = cmbDepartment.SelectedValue;
        if (val is null) return null;
        if (val is int i) return i;
        try { return Convert.ToInt32(val); } catch { return null; }
    }

    /// <summary>
    /// انتخاب آیتم کمبو با متن. اول تطبیق «دقیق» (بعد از نرمال‌سازی)، بعد تطبیق بی‌حساس به فاصله؛
    /// فقط اگر هیچ‌کدام نبود، نزدیک‌ترین آیتمِ «شامل‌شونده» انتخاب می‌شود.
    /// (قبلاً اولین آیتمی که فقط بخشی از متن را داشت انتخاب می‌شد؛ مثلاً «پروژه جهان نما» به‌جای «پروژه جهان نما ۵»
    /// و با ذخیره‌ی بعدی، مقدار اشتباه جایگزین می‌شد.)
    /// </summary>
    private static void SelectComboByText(ComboBox cmb, string? text)
    {
        if (cmb is null || cmb.Items.Count == 0) return;
        if (string.IsNullOrWhiteSpace(text)) { cmb.SelectedIndex = -1; return; }

        string Clean(string t) => StringHelper.NormalizePersianText(t ?? "").Trim();
        string NoSpace(string t) => Clean(t).Replace(" ", "");

        string clean = Clean(text);
        string cleanNs = NoSpace(text);

        // ۱) تطبیق دقیق
        for (int i = 0; i < cmb.Items.Count; i++)
            if (Clean(cmb.GetItemText(cmb.Items[i])) == clean) { cmb.SelectedIndex = i; return; }

        // ۲) تطبیق دقیق بدون توجه به فاصله
        for (int i = 0; i < cmb.Items.Count; i++)
            if (NoSpace(cmb.GetItemText(cmb.Items[i])) == cleanNs) { cmb.SelectedIndex = i; return; }

        // ۳) آخرین راه: نزدیک‌ترین آیتم شامل‌شونده (کمترین اختلاف طول)
        int best = -1, bestDiff = int.MaxValue;
        for (int i = 0; i < cmb.Items.Count; i++)
        {
            string item = Clean(cmb.GetItemText(cmb.Items[i]));
            if (item.Length == 0) continue;
            if (item.Contains(clean) || clean.Contains(item))
            {
                int diff = Math.Abs(item.Length - clean.Length);
                if (diff < bestDiff) { bestDiff = diff; best = i; }
            }
        }
        cmb.SelectedIndex = best;
    }
}
