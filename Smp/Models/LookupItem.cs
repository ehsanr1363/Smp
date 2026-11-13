namespace SMP.Models
{
    public class LookupItem
    {
        public int Id { get; set; }
        public string Category { get; set; } = "";
        public string Value { get; set; } = "";

        // این متد جادو می‌کند و متن را به کومبو باکس می‌دهد
        public override string ToString()
        {
            return Value;
        }
    }
}