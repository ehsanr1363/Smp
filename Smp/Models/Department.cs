namespace Smp.Models;

public class Department
{
    public int Id { get; set; }
    public string? Code { get; set; } // 🌟 کد واحد سازمانی
    public string Name { get; set; } = ""; // عنوان واحد
    public string? Address { get; set; } // آدرس واحد
}
