using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;

namespace Smp.Forms;

/// <summary>
/// انتخاب سال/ماه لیست حقوق و پیش‌نمایش جمع کارگران روزمزدِ «محل خدمت» قرارداد.
/// کارکرد = حقوق و اضافه‌کار + بیمه کارفرما | حق‌الزحمه = (حقوق و اضافه‌کار − بیمه ۷٪) × بالاسری ÷ مقسوم‌علیه
/// </summary>
public class PayrollPeriodPickerForm : Form
{
    private readonly string _connectionString;
    private readonly ClientContract _contract;

    private readonly ComboBox _cmbYear = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly ComboBox _cmbMonth = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly Button _btnPreview = new() { Text = "نمایش", Width = 90, Height = 30 };
    private readonly Button _btnOk = new() { Text = "تایید و انتقال به صورت وضعیت", Width = 230, Height = 34, Enabled = false };
    private readonly Label _lblResult = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopRight };

    public decimal SelectedGrossAmount { get; private set; }
    public decimal SelectedFeeAmount { get; private set; }

    private static readonly string[] MonthNames =
        { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

    public PayrollPeriodPickerForm(string connectionString, ClientContract contract, int defaultYear, int defaultMonth)
    {
        _connectionString = connectionString;
        _contract = contract;

        Text = "محاسبه از لیست حقوق";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        ClientSize = new Size(520, 360);

        for (int y = 1400; y <= 1410; y++) _cmbYear.Items.Add(y);
        foreach (var m in MonthNames) _cmbMonth.Items.Add(m);
        _cmbYear.SelectedItem = defaultYear;
        if (_cmbYear.SelectedIndex < 0) _cmbYear.SelectedIndex = 0;
        _cmbMonth.SelectedIndex = Math.Clamp(defaultMonth, 1, 12) - 1;

        var lblInfo = new Label
        {
            Dock = DockStyle.Top, Height = 54, TextAlign = ContentAlignment.MiddleRight,
            Text = $"محل خدمت: {_contract.WorkLocation}\nفقط کارگران «روزمزد» این محل خدمت محاسبه می‌شوند."
        };

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
        top.Controls.Add(new Label { Text = "سال", AutoSize = true, Margin = new Padding(6, 8, 6, 0) });
        top.Controls.Add(_cmbYear);
        top.Controls.Add(new Label { Text = "ماه", AutoSize = true, Margin = new Padding(14, 8, 6, 0) });
        top.Controls.Add(_cmbMonth);
        top.Controls.Add(_btnPreview);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
        var btnCancel = new Button { Text = "انصراف", Width = 90, Height = 34, DialogResult = DialogResult.Cancel };
        bottom.Controls.Add(_btnOk);
        bottom.Controls.Add(btnCancel);

        Controls.Add(_lblResult);
        Controls.Add(bottom);
        Controls.Add(top);
        Controls.Add(lblInfo);
        CancelButton = btnCancel;

        _cmbYear.SelectedIndexChanged += (s, e) => ResetPreview();
        _cmbMonth.SelectedIndexChanged += (s, e) => ResetPreview();
        _btnPreview.Click += async (s, e) => await PreviewAsync();
        _btnOk.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
    }

    private void ResetPreview()
    {
        _btnOk.Enabled = false;
        _lblResult.Text = "";
    }

    private async Task PreviewAsync()
    {
        try
        {
            int year = Convert.ToInt32(_cmbYear.SelectedItem);
            int month = _cmbMonth.SelectedIndex + 1;
            var repo = new ClientStatementRepository(_connectionString);
            var t = await Task.Run(() => repo.GetDailyWagePayrollTotals(year, month, _contract.WorkLocation));

            if (t.WorkerCount == 0)
            {
                string msg = "برای این ماه و محل خدمت، لیست حقوقِ کارگر روزمزد ثبت نشده است.\nابتدا حقوق ماه را محاسبه و ثبت کنید و مطمئن شوید «نوع پرسنلی» = روزمزد و «محل خدمت» درست است.";
                try
                {
                    var locs = await Task.Run(() => repo.GetDailyWageLocationsInPeriod(year, month));
                    if (locs.Count > 0)
                        msg += "\n\nمحل‌های خدمتی که در این ماه برایشان حقوق روزمزد ثبت شده:\n" +
                               string.Join("\n", locs.Select(l => $"• {l.Location}  ({StringHelper.ConvertToPersianDigits(l.Count.ToString())} نفر)"));
                    else
                        msg += "\n\nدر این ماه اصلاً حقوق روزمزدی ذخیره نشده است (از «ذخیره» در فرم محاسبه حقوق مطمئن شوید).";
                }
                catch (Exception ex2) { LogHelper.LogException(ex2, "PayrollPeriodPickerForm.LocationsHint"); }
                _lblResult.Text = msg;
                _btnOk.Enabled = false;
                return;
            }

            decimal fee = ClientFeeHelper.PayrollFee(t.TotalEarnings, _contract.OverheadPercentage, _contract.FeeDivisorPercentage);
            SelectedGrossAmount = t.Gross;
            SelectedFeeAmount = fee;

            string F(decimal v) => NumberHelper.FormatThousands(v, usePersianDigits: true);
            _lblResult.Text =
                $"تعداد کارگران: {StringHelper.ConvertToPersianDigits(t.WorkerCount.ToString())}\n" +
                $"جمع حقوق و اضافه‌کار: {F(t.TotalEarnings)}\n" +
                $"بیمه سهم کارفرما: {F(t.EmployerInsurance)}\n" +
                $"کارکرد (بدون ارزش افزوده): {F(t.Gross)}\n\n" +
                $"خالص پرداختی به پرسنل: {F(t.NetPay)}\n" +
                $"حق‌الزحمه = (حقوق و اضافه‌کار − بیمه ۷٪) × {StringHelper.ConvertToPersianDigits(ClientFeeHelper.OverheadText(_contract.OverheadPercentage))} ÷ {StringHelper.ConvertToPersianDigits(ClientFeeHelper.DivisorText(_contract.FeeDivisorPercentage))} = {F(fee)}";
            _btnOk.Enabled = true;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "PayrollPeriodPickerForm.PreviewAsync");
            MessageBox.Show("خطا در دریافت لیست حقوق:\n" + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
