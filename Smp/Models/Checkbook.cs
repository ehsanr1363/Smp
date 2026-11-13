using System;

namespace SMP.Models
{
    public class Checkbook
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public string AccountTitle { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public string StartSayadNumber { get; set; } = "";
        public string StartCheckNumber { get; set; } = "";
        public int PageCount { get; set; }
        public DateTime CreatedAt { get; set; }

        // محاسبه شماره آخرین چک
        public string EndCheckNumber
        {
            get
            {
                if (long.TryParse(StartCheckNumber, out long start) && PageCount > 0)
                    return (start + PageCount - 1).ToString();
                return "";
            }
        }

        // ✅ محاسبه شناسه صیاد آخرین چک
        public string EndSayadNumber
        {
            get
            {
                if (long.TryParse(StartSayadNumber, out long start) && PageCount > 0)
                    return (start + PageCount - 1).ToString("D12");
                return "";
            }
        }

        // ✅ محدوده نمایش داده شده
        public string RangeDisplay => $"{StartCheckNumber} تا {EndCheckNumber}";
        public string SayadRangeDisplay => $"{StartSayadNumber} تا {EndSayadNumber}";
    }
}