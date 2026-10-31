using System;

namespace PersonnelApp.Models
{
    public class ClientContract
    {
        public int Id { get; set; }
        public int PartyId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string ContractRow { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public decimal Amount { get; set; }

        // تاریخ‌ها به صورت DateTime (میلادی در حافظه)
        public DateTime? ContractDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? TerminationDate { get; set; }

        public string Status { get; set; } = "فعال";
        public DateTime? CreatedAt { get; set; }

        // 👈 این پراپرتی باید get و set داشته باشد
        public string DisplayName { get; set; } = string.Empty;
    }
}