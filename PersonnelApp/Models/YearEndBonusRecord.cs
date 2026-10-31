namespace PersonnelApp.Models
{
    public class YearEndBonusRecord
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public int Year { get; set; }

        public int MonthsWorked { get; set; } // ماه‌های کارکرد در آن سال
        public decimal BaseSalary { get; set; }
        public decimal BonusAmount { get; set; } // مبلغ عیدی
        public decimal Tax { get; set; }         // مالیات عیدی (در صورت وجود)
        public decimal NetBonus { get; set; }    // خالص عیدی
    }
}