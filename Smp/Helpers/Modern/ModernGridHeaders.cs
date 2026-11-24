using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Helpers;

public static class ModernGridHeaders
{
    /// <summary>
    /// ApplyModernStyle بعد از هر DataBindingComplete عنوان ستون‌ها را از TranslationHelper می‌گیرد و
    /// برای ستون‌های ناشناخته (مثل H10 یا MonthsCount) نام انگلیسی را جایگزین HeaderText فارسی می‌کند.
    /// این متد HeaderText فعلی ستون‌ها را ثبت می‌کند و بعد از هر بایند دوباره برمی‌گرداند.
    /// باید بعد از اضافه کردن همه‌ی ستون‌ها فراخوانی شود.
    /// </summary>
    public static void PinHeaderTexts(this DataGridView dgv)
    {
        var pinned = new Dictionary<string, string>();
        foreach (DataGridViewColumn c in dgv.Columns)
            if (!string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.HeaderText))
                pinned[c.Name] = c.HeaderText;

        // این هندلر بعد از هندلر ModernGrid ثبت می‌شود، پس آخرین کلمه با ماست
        dgv.DataBindingComplete += (s, e) =>
        {
            foreach (var kv in pinned)
                if (dgv.Columns.Contains(kv.Key))
                    dgv.Columns[kv.Key].HeaderText = kv.Value;
        };
    }
}
