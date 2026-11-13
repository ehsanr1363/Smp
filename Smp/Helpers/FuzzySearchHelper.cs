using System;
using System.Collections.Generic;
using System.Linq;

namespace SMP.Helpers
{
    public static class FuzzySearchHelper
    {
        /// <summary>
        /// پیدا کردن کلماتی که بیشترین شباهت را به کلمه جستجو دارند.
        /// </summary>
        /// <param name="source">لیست کلمات (مثلاً لیست نام‌ها)</param>
        /// <param name="target">کلمه‌ای که کاربر تایپ کرده</param>
        /// <param name="maxDistance">حداکثر فاصله (۲ یا ۳ معمول است)</param>
        public static List<string> Search(List<string> source, string target, int maxDistance = 2)
        {
            if (string.IsNullOrEmpty(target)) return new List<string>();
            target = StringHelper.NormalizePersianText(target).ToLower();

            return source
                .Where(s => LevenshteinDistance(
                    StringHelper.NormalizePersianText(s).ToLower(), target) <= maxDistance)
                .ToList();
        }

        /// <summary>
        /// الگوریتم محاسبه فاصله Levenshtein
        /// </summary>
        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }
    }
}