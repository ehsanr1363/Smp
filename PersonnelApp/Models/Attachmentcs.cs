using System;

namespace PersonnelApp.Models
{
    public class Attachment
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = ""; // مثلا "Employee" یا "Contract"
        public int EntityId { get; set; }
        public string FileName { get; set; } = "";
        public byte[] FileData { get; set; } = Array.Empty<byte>();
        public long FileSize { get; set; }
        public string Description { get; set; } = "";

        // 👇 فیلد زمان آپلود (هم CreatedAt و هم UploadedAt برای سازگاری) 👇
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UploadedAt
        {
            get => CreatedAt;
            set => CreatedAt = value;
        }
    }
}