namespace SmpApi
{
    public class EmployeeDto
    {
        public int Id { get; set; }
        public string PersonnelCode { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Position { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public bool IsActive { get; set; }
    }
}