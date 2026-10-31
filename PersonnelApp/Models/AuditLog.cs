using System;

namespace PersonnelApp.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string ActionType { get; set; } // مثلا: حذف، ویرایش، ثبت
        public string ModuleName { get; set; } // مثلا: پرسنل، حقوق، مرخصی
        public string Description { get; set; }
        public DateTime LogDate { get; set; }
    }
}