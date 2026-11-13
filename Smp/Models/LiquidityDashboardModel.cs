namespace SMP.Models
{
    public class LiquidityDashboardModel
    {
        // ۱. کل موجودی نقدی (جمع موجودی تمام حساب‌ها و صندوق‌ها)
        public decimal TotalLiquidity { get; set; }

        // ۲. مجموع دریافت‌های دوره انتخاب شده (مثلاً این ماه)
        public decimal TotalIncome { get; set; }

        // ۳. مجموع پرداخت‌های دوره انتخاب شده
        public decimal TotalExpense { get; set; }

        // ۴. مجموع چک‌های دریافتی که هنوز پاس نشده‌اند (در جریان)
        public decimal TotalReceivableChecks { get; set; }

        // ۵. مجموع چک‌های پرداختی که هنوز پاس نشده‌اند (در جریان)
        public decimal TotalPayableChecks { get; set; }

        // ۶. جریان نقدی خالص (دریافتی منهای پرداختی در بازه زمانی)
        // این یک فیلد محاسباتی است و نیازی به ذخیره در دیتابیس ندارد
        public decimal NetCashFlow => TotalIncome - TotalExpense;
    }
}