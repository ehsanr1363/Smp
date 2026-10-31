using System;
using System.Reflection;

namespace PersonnelApp.Helpers
{
    /// <summary>
    /// مهر زمانی خودکار: CreatedAt / UploadedAt / UpdatedAt / ...
    /// اگر مقدار پیش‌فرض (یا null) بود، اکنون را می‌گذارد.
    /// </summary>
    public static class DateStampHelper
    {
        private static readonly string[] CreatedNames =
            { "CreatedAt", "UploadedAt", "InsertedAt", "RegisteredAt", "CreatedDate" };

        private static readonly string[] UpdatedNames =
            { "UpdatedAt", "ModifiedAt", "EditedAt", "LastModified" };

        /// <summary>قبل از INSERT: اگر CreatedAt/UploadedAt خالی بود، اکنون بگذار.</summary>
        public static void StampCreated(object entity) => Stamp(entity, CreatedNames);

        /// <summary>قبل از UPDATE: اگر UpdatedAt خالی بود، اکنون بگذار.</summary>
        public static void StampUpdated(object entity) => Stamp(entity, UpdatedNames);

        private static void Stamp(object entity, string[] names)
        {
            if (entity == null) return;
            var type = entity.GetType();

            foreach (var name in names)
            {
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanRead || !prop.CanWrite) continue;

                var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (underlying != typeof(DateTime)) continue;

                var current = prop.GetValue(entity);
                bool isEmpty = current == null
                            || (current is DateTime dt && (dt == default || dt.Year < 1900));

                if (isEmpty)
                {
                    prop.SetValue(entity, DateTime.Now);
                    return; // فقط اولین فیلد منطبق
                }
            }
        }
    }
}