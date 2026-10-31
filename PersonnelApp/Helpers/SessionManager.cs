namespace PersonnelApp.Helpers
{
    public static class SessionManager
    {
        public static int UserId { get; set; }
        public static string Username { get; set; } = "";
        public static string FullName { get; set; } = "";
        public static string Role { get; set; } = ""; // Admin, HR, Viewer

        /// <summary>
        /// آیا کاربر جاری دسترسی ادمین دارد؟
        /// </summary>
        public static bool IsAdmin => Role == "Admin";

        public static void Logout()
        {
            UserId = 0;
            Username = "";
            FullName = "";
            Role = "";
        }
    }
}