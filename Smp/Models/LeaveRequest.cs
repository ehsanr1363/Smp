using System;

namespace SMP.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } // برای نمایش در گرید

        public string LeaveType { get; set; } // استحقاقی، استعلاجی، بدون حقوق
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DaysCount { get; set; } // تعداد روزهای مرخصی

        public string Description { get; set; }
        public string Status { get; set; } // در انتظار، تایید شده، رد شده
    }
}