using System.Windows;
using System.Windows.Controls;

namespace Smp
{
    public partial class ContractTemplateWindow : Window
    {
        public ContractTemplateWindow()
        {
            InitializeComponent();
        }

        // رویدادهای مربوط به تب‌ها (ویرایش / پیش‌نمایش)
        private void Tab_Changed(object sender, RoutedEventArgs e)
        {
            // کدهای مربوط به تغییر تب را اینجا بنویسید
        }

        // رویداد دکمه بازگشت به آخرین ذخیره
        private void Revert_Click(object sender, RoutedEventArgs e)
        {
            // کدهای مربوط به بازگشت را اینجا بنویسید
        }

        // رویداد دکمه واگرد
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            // کدهای مربوط به واگرد را اینجا بنویسید
        }

        // رویداد تغییر متن در ویرایشگر
        private void EditorBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // کدهای مربوط به تغییر متن را اینجا بنویسید
        }

        // رویداد دکمه ذخیره متن قرارداد
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // کدهای مربوط به ذخیره‌سازی را اینجا بنویسید
        }

        // رویداد دکمه ایمپورت از اکسل
        private void Import_Click(object sender, RoutedEventArgs e)
        {
            // کدهای مربوط به ایمپورت را اینجا بنویسید
        }
    }
}