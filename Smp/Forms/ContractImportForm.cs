using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Helpers;

namespace SMP.Forms
{
    public class ContractImportForm : Form
    {
        private readonly EmployeeContractRepository _repository;

        private Button btnDownloadTemplate = null!;
        private Button btnBrowse = null!;
        private Button btnImport = null!;
        private TextBox txtFilePath = null!;

        // ✅ FIX P2: جلوگیری از تداخل
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public ContractImportForm(string connectionString)
        {
            _repository = new EmployeeContractRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("ایمپورت قراردادهای پرسنل از اکسل", new Size(900, 700));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        // ✅ FIX: بازسازی کامل UI که قبلاً خالی بود
        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(20) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var lblTitle = new Label
            {
                Text = "لطفاً ابتدا قالب اکسل را دانلود کرده، اطلاعات قراردادها را در آن وارد کنید و سپس فایل را ایمپورت نمایید.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText
            };
            mainLayout.Controls.Add(lblTitle, 0, 0);

            btnDownloadTemplate = UIHelper.CreateButton("📥 دانلود قالب اکسل قراردادها", UIHelper.SectionBlue, 300, 45);
            btnDownloadTemplate.Dock = DockStyle.Right;
            btnDownloadTemplate.Click += (s, e) => ExcelTemplateGenerator.GenerateContractsTemplate();
            mainLayout.Controls.Add(btnDownloadTemplate, 0, 1);

            var pnlFile = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            pnlFile.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));
            pnlFile.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            txtFilePath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmInputBackground };
            btnBrowse = UIHelper.CreateButton("📁 انتخاب فایل...", UIHelper.WarmSecondary, 120, 45);
            btnBrowse.Dock = DockStyle.Fill;
            btnBrowse.Click += BtnBrowse_Click;

            pnlFile.Controls.Add(txtFilePath, 0, 0);
            pnlFile.Controls.Add(btnBrowse, 1, 0);
            mainLayout.Controls.Add(pnlFile, 0, 2);

            btnImport = UIHelper.CreateButton("📤 شروع ایمپورت", UIHelper.ActionGreen, 200, 50);
            btnImport.Enabled = false;
            btnImport.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnImport.Click += async (s, e) => await BtnImport_Click(s, e);
            mainLayout.Controls.Add(btnImport, 0, 3);

            this.Controls.Add(mainLayout);
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

        private async Task BtnImport_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFilePath.Text) || IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (!File.Exists(txtFilePath.Text))
                {
                    ModernMessageBox.ShowWarning("فایل انتخاب شده در مسیر ذکر شده وجود ندارد.");
                    return;
                }

                btnImport.Enabled = false;

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

                if (!loadingResult.Success || IsDisposed) return;

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
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ContractImportForm.BtnImport_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ایمپورت:\n" + ex.Message);
            }
            finally
            {
                if (!IsDisposed) btnImport.Enabled = true;
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}