using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Data;
using PersonnelApp.Helpers;

namespace PersonnelApp.Forms
{
    public class ContractImportForm : Form
    {
        private readonly EmployeeContractRepository _repository;

        private Button btnDownloadTemplate = null!;
        private Button btnBrowse = null!;
        private Button btnImport = null!;
        private TextBox txtFilePath = null!;

        public ContractImportForm(string connectionString)
        {
            _repository = new EmployeeContractRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("ایمپورت قراردادهای پرسنل از اکسل", new Size(900, 700));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this);
        }

        private void BuildUI()
        {
            // ... (کد رابط کاربری بدون تغییر باقی می‌ماند) ...
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls",
                Title = "انتخاب فایل اکسل قراردادها"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                txtFilePath.Text = ofd.FileName;
                btnImport.Enabled = true;
            }
        }

        private async void BtnImport_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFilePath.Text)) return;

            if (!File.Exists(txtFilePath.Text))
            {
                ModernMessageBox.ShowWarning("فایل انتخاب شده در مسیر ذکر شده وجود ندارد.");
                return;
            }

            // ✅ FIX CS8130: استفاده از متغیر با نوع مشخص به جای deconstruction
            var loadingResult = await AsyncHelper.RunWithLoading(
                this,
                async () =>
                {
                    try
                    {
                        return await _repository.ImportFromExcelAsync(txtFilePath.Text);
                    }
                    catch (IOException)
                    {
                        throw new IOException("فایل اکسل در حال حاضر باز است یا توسط برنامه دیگری قفل شده است.");
                    }
                },
                "در حال ایمپورت اطلاعات از اکسل...");

            // ✅ بررسی موفقیت‌آمیز بودن عملیات
            if (!loadingResult.Success) return;

            var importResult = loadingResult.Result;
            if (importResult == default) return;

            int inserted = importResult.inserted;
            int updated = importResult.updated;
            List<string> errors = importResult.errors;

            string msg = $"✅ ایمپورت انجام شد!\n\n" +
                         $"➕ قراردادهای جدید: {NumberHelper.FormatThousands(inserted, usePersianDigits: true)}\n" +
                         $"🔄 قراردادهای به‌روزرسانی شده: {NumberHelper.FormatThousands(updated, usePersianDigits: true)}";

            if (errors.Count > 0)
            {
                msg += $"\n\n⚠️ {errors.Count} خطا:\n" + string.Join("\n", errors.Take(10));
                if (errors.Count > 10)
                    msg += $"\n... و {errors.Count - 10} خطای دیگر.";
            }

            if (inserted == 0 && updated == 0)
            {
                ModernMessageBox.ShowWarning(msg, "نتیجه ایمپورت");
            }
            else
            {
                ModernMessageBox.ShowInfo(msg, "نتیجه ایمپورت");
                ToastHelper.Show("ایمپورت با موفقیت انجام شد.", isSuccess: true);
            }
        }
    }
}