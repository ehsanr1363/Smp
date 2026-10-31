using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class ImageHelper
    {
        /// <summary>
        /// انتخاب عکس، حفظ نسبت تصویر (Letterbox)، و تبدیل به بایت JPEG.
        /// </summary>
        public static byte[]? SelectAndResizeImage(int targetWidth = 200, int targetHeight = 250)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "انتخاب عکس پرسنل",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return null;

            try
            {
                using var original = Image.FromFile(ofd.FileName);
                return ResizeWithLetterbox(original, targetWidth, targetHeight);
            }
            catch (Exception ex)
            {
                ModernMessageBox.ShowError("خطا در بارگذاری عکس:\n" + ex.Message);
                return null;
            }
        }

        /// <summary>تغییر اندازه با حفظ نسبت (Letterbox روی پس‌زمینه سفید).</summary>
        public static byte[] ResizeWithLetterbox(Image original, int targetWidth, int targetHeight)
        {
            using var output = new Bitmap(targetWidth, targetHeight);
            using (var g = Graphics.FromImage(output))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                double ratio = Math.Min((double)targetWidth / original.Width,
                                        (double)targetHeight / original.Height);
                int newW = (int)Math.Round(original.Width * ratio);
                int newH = (int)Math.Round(original.Height * ratio);
                int x = (targetWidth - newW) / 2;
                int y = (targetHeight - newH) / 2;

                g.DrawImage(original, x, y, newW, newH);
            }

            using var ms = new MemoryStream();
            output.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }

        /// <summary>
        /// تبدیل بایت به Image. خروجی یک Bitmap مستقل است که به Stream وابسته نیست
        /// (برای جلوگیری از خطای GDI+ هنگام بسته شدن MemoryStream).
        /// </summary>
        public static Image? ByteArrayToImage(byte[]? data)
        {
            if (data == null || data.Length == 0) return null;
            try
            {
                using var ms = new MemoryStream(data);
                using var img = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false);
                return new Bitmap(img);
            }
            catch
            {
                return null;
            }
        }
    }
}