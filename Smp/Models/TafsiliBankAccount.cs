namespace Smp.Models;

public class TafsiliBankAccount
{
    public int Id { get; set; }
    public int TafsiliId { get; set; }
    public string ShebaNumber { get; set; } = "";
    public string BankName { get; set; } = "";
    public bool IsMainAccount { get; set; }
}
