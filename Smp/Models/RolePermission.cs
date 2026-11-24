namespace Smp.Models;

public class RolePermission
{
    public int Id { get; set; }
    public string RoleName { get; set; } = "";
    public string MenuKey { get; set; } = "";
    public bool IsAllowed { get; set; }
}
