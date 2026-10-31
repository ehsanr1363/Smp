namespace PersonnelApp.Models
{
    public class EmployeeBankAccount
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string AccountNumber { get; set; } = "";
        public string ShebaNumber { get; set; } = "";
        public string BankCode { get; set; } = "";
        public string BankName { get; set; } = ""; // برای نمایش در جدول
        public bool IsMainAccount { get; set; } = false;
    }
}