using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Helpers;

namespace Smp.Forms;

public class ContractImportForm : Form
{
    private readonly EmployeeContractRepository _repository;

    private Button btnDownloadTemplate = null!;
    private Button btnBrowse = null!;
    private Button btnImport = null!;
    private ModernTextBox txtFilePath = null!;
    private Label lblFileInfo = null!;

    // ✅ FIX P2: جلوگیری از تداخل
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public ContractImportForm(string connectionString)
    {
        _repository = new EmployeeContractRepository(connectionString);

        BuildUI();
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractImportForm"); } }
        base.Dispose(disposing);
    }

    // ✅ FIX: بازسازی کامل UI که قبلاً خالی بود
    private void BuildUI()
    {
        // ── راهنمای کلی ──
        var banner = ModernListKit.Banner(
            "ابتدا قالب اکسل را دانلود کنید، اطلاعات قراردادها را در آن وارد کنید\nو سپس فایل پرشده را انتخاب و ایمپورت نمایید.",
            ModernTheme.Primary);

        // ── مرحله ۱: دریافت قالب ──
        var lblTemplate = new Label
        {
            Text = "قالب استاندارد شامل همه‌ی ستون‌های لازم برای ایمپورت قراردادهای پرسنل است.",
            TextAlign = ContentAlignment.MiddleLeft,   // در فرم RTL یعنی راست‌چین
            Font = ModernTheme.GetFont(10f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent
        };
        btnDownloadTemplate = ModernButton.Soft("📥 دانلود قالب اکسل", ModernTheme.Primary, 200, 36);
        btnDownloadTemplate.Click += (s, e) => ExcelTemplateGenerator.GenerateContractsTemplate();
        var templateBar = ModernListKit.BuildToolbar((lblTemplate, -1), (btnDownloadTemplate, 210));

        var cardTemplate = new ModernCard("① دریافت قالب اکسل", ModernTheme.Primary, 1, 50);
        cardTemplate.Body.Controls.Add(templateBar, 0, 0);

        // ── مرحله ۲: انتخاب فایل ──
        txtFilePath = ModernUI.Input(placeholder: "فایلی انتخاب نشده است");
        txtFilePath.ReadOnly = true;
        btnBrowse = ModernButton.Soft("📁 انتخاب فایل...", ModernTheme.Teal, 160, 36);
        btnBrowse.Click += BtnBrowse_Click;
        var pickBar = ModernListKit.BuildToolbar((txtFilePath, -1), (btnBrowse, 170));

        lblFileInfo = new Label
        {
            Text = "بعد از انتخاب فایل، دکمه‌ی «شروع ایمپورت» فعال می‌شود.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ModernTheme.GetFont(10f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent
        };

        var cardFile = new ModernCard("② انتخاب فایل اکسل پرشده", ModernTheme.Teal, 1, 50, 30);
        cardFile.Body.Controls.Add(pickBar, 0, 0);
        cardFile.Body.Controls.Add(lblFileInfo, 0, 1);

        // ── مرحله ۳: دکمه‌ها ──
        btnImport = ModernButton.Success("📤 شروع ایمپورت", 190, 40);
        btnImport.Enabled = false;
        btnImport.Click += async (s, e) => await BtnImport_Click(s, e);
        var btnCancel = ModernButton.Cancel("انصراف", 110, 40);
        btnCancel.Click += (s, e) => Close();
        var actions = ModernListKit.BuildActionBar(new Control[] { btnImport }, new Control[] { btnCancel });

        ModernListKit.ComposeRows(this, "ایمپورت قراردادهای پرسنل از اکسل",
            "ورود گروهی قراردادها از روی فایل اکسل", new Size(800, 520),
            (banner, 72), (cardTemplate, cardTemplate.Height + 12), (cardFile, cardFile.Height + 12),
            (new Panel(), 0), (actions, 64));
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

            try
            {
                var fi = new FileInfo(ofd.FileName);
                string size = fi.Length >= 1024 * 1024
                    ? $"{fi.Length / 1024.0 / 1024.0:0.0} MB"
                    : $"{Math.Max(1, fi.Length / 1024)} KB";
                lblFileInfo.Text = $"📄 {fi.Name}  ·  {size}  —  آماده‌ی ایمپورت";
                lblFileInfo.ForeColor = ModernTheme.Success;
            }
            catch { lblFileInfo.Text = "فایل انتخاب شد."; }
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
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "ContractImportForm"); }
        }
    }
}
