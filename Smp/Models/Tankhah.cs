using System;

namespace Smp.Models;

public class Tankhah
{
    public int Id { get; set; }
    public string OwnerName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
