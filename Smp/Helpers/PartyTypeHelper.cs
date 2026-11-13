namespace SMP.Helpers
{
    /// <summary>
    /// ✅ FIX P1: مقادیر استاندارد PartyType — یک منبع واحد حقیقت
    /// </summary>
    public static class PartyTypeHelper
    {
        public const string Natural = "حقیقی";
        public const string Legal = "حقوقی";

        public static bool IsNatural(string? partyType) =>
            !string.IsNullOrEmpty(partyType) && partyType.Trim() == Natural;

        public static bool IsLegal(string? partyType) =>
            !string.IsNullOrEmpty(partyType) && partyType.Trim() == Legal;

        public static bool IsNaturalOrLegal(string? partyType) =>
            IsNatural(partyType) || IsLegal(partyType);
    }
}