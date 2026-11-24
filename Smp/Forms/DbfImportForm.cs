using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Helpers;

namespace Smp.Forms;

public class DbfImportForm : Form
{
    private TextBox txtHeaderPath = null!;
    private TextBox txtDetailsPath = null!;
    private Button btnBrowseHeader = null!, btnBrowseDetails = null!;
    private Button btnImport = null!;
    private Button btnPreview = null!;
    private ModernCheckBox chkAbortOnError = null!;
    private Label lblStatus = null!;

    // ✅ FIX P2: جلوگیری از تداخل
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public DbfImportForm()
    {
        BuildUI();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch { } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        const int Gap = 12;

        // ───────────── کارت انتخاب فایل‌ها ─────────────
        var card = new ModernCard("📂 فایل‌های DBF بیمه", ModernTheme.Primary,
            new[] { new ColumnStyle(SizeType.Percent, 100F), new ColumnStyle(SizeType.Absolute, 210F) },
            ModernUI.RowHeight, ModernUI.RowHeight, ModernUI.RowHeight);

        var boxHeader = ModernUI.Input(); boxHeader.ReadOnly = true; txtHeaderPath = boxHeader.TextBox;
        var boxDetails = ModernUI.Input(); boxDetails.ReadOnly = true; txtDetailsPath = boxDetails.TextBox;

        ModernButton Browse(string text, TextBox target)
        {
            var b = ModernButton.Primary(text, 190, 36);
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(6, 5, 6, 5);
            b.Click += (s, e) => BrowseFile(target);
            return b;
        }
        var b1 = Browse("📁 انتخاب فایل ۱ (هدر)", txtHeaderPath); btnBrowseHeader = b1;
        var b2 = Browse("📁 انتخاب فایل ۲ (جزئیات)", txtDetailsPath); btnBrowseDetails = b2;

        card.Body.Controls.Add(ModernUI.Row("فایل هدر", boxHeader), 0, 0);
        card.Body.Controls.Add(b1, 1, 0);
        card.Body.Controls.Add(ModernUI.Row("فایل جزئیات", boxDetails), 0, 1);
        card.Body.Controls.Add(b2, 1, 1);

        // ✅ توقف کامل: اگر خطا یا پرسنل نیافته بود، هیچ داده‌ای ایمپورت نشود
        chkAbortOnError = ModernUI.Check("");
        chkAbortOnError.Checked = true;
        card.Body.Controls.Add(ModernUI.CheckRow("توقف کامل در صورت خطا", chkAbortOnError), 0, 2);
        // ✅ FIX: ارتفاع واقعی کارت (۲ ردیف) قبل از Dock=Fill ذخیره می‌شود؛ بعد از آن Panel میزبان ارتفاع را کوچک می‌کرد و ردیف دوم بریده می‌شد
        int cardHeight = card.Height;
        card.Dock = DockStyle.Fill;

        var cardHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, Gap), BackColor = Color.Transparent, Margin = Padding.Empty };
        cardHost.Controls.Add(card);

        // ───────────── راهنما و وضعیت ─────────────
        var lblHint = new Label
        {
            Text = "💡 راهنما: ابتدا فایل هدر (DSK) و سپس فایل جزئیات (DSW) را انتخاب کنید.\nتوجه: قبل از ایمپورت، مطمئن شوید کارگاه‌ها و کدهای ملی پرسنل در سیستم ثبت شده باشند.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,   // فرم Mirror شده: متن سمت راست
            Font = ModernTheme.GetFont(10f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent,
            Padding = new Padding(8, 0, 8, 0)
        };

        lblStatus = new Label
        {
            Text = "آماده برای ایمپورت...",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(11f, FontStyle.Bold),
            ForeColor = ModernTheme.Text,
            BackColor = Color.Transparent
        };

        // ───────────── نوار دکمه ─────────────
        var import = ModernButton.Success("📥 شروع ایمپورت به دیتابیس", 270, 42);
        import.Click += async (s, e) => await BtnImport_Click(s, e);
        btnImport = import;

        var preview = ModernButton.Outline("🔍 پیش‌نمایش (بدون ذخیره)", ModernTheme.Primary, 240, 42);
        preview.Click += async (s, e) => await RunImportAsync(true);
        btnPreview = preview;

        var bar = ModernForm.CreateButtonBar(import, preview);
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(20, 16, 20, 0), BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight + Gap));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(cardHost, 0, 0);
        root.Controls.Add(lblHint, 0, 1);
        root.Controls.Add(lblStatus, 0, 2);
        root.Controls.Add(bar, 0, 3);
        Controls.Add(root);

        this.ApplyStandardHeader("ایمپورت اطلاعات بیمه (فایل‌های DBF)", new Size(760, 480));
        ShowInTaskbar = false;
    }

    private void BrowseFile(TextBox targetTextBox)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "DBF Files|*.dbf|All Files|*.*",
            Title = "انتخاب فایل DBF"
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            targetTextBox.Text = ofd.FileName;
        }
    }

    private Task BtnImport_Click(object? sender, EventArgs e) => RunImportAsync(false);

    private async Task RunImportAsync(bool previewOnly)
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        bool proceedAfterPreview = false;
        try
        {
            if (string.IsNullOrWhiteSpace(txtHeaderPath.Text) || !File.Exists(txtHeaderPath.Text))
            {
                ModernMessageBox.ShowWarning("لطفاً فایل هدر (فایل ۱) را انتخاب کنید.");
                return;
            }

            if (string.IsNullOrWhiteSpace(txtDetailsPath.Text) || !File.Exists(txtDetailsPath.Text))
            {
                ModernMessageBox.ShowWarning("لطفاً فایل جزئیات (فایل ۲) را انتخاب کنید.");
                return;
            }

            btnImport.Enabled = false;
            btnPreview.Enabled = false;
            lblStatus.Text = "در حال پردازش فایل‌ها... لطفاً صبر کنید.";
            lblStatus.ForeColor = ModernTheme.WarningText;

            string resultMsg = "";
            bool rolledBack = false;
            DbfImportResult? previewResult = null;
            bool abortOnError = chkAbortOnError.Checked;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() =>
                {
                    var result = DbfImportHelper.ImportCombined(AppConfig.ConnectionString, txtHeaderPath.Text, txtDetailsPath.Text, abortOnError, previewOnly);

                    rolledBack = result.RolledBack;

                    string msg;
                    if (result.IsPreview)
                    {
                        previewResult = result;   // لیست پیش‌نمایش بعد از بسته‌شدن لودینگ نمایش داده می‌شود
                        return;
                    }
                    else if (result.RolledBack)
                    {
                        msg = "⛔ ایمپورت انجام نشد و هیچ داده‌ای ذخیره نشد.\n" +
                              "به‌دلیل گزینه‌ی «توقف کامل در صورت خطا»، چون مشکل زیر پیدا شد کل ایمپورت لغو شد.\n";
                    }
                    else
                    {
                        msg = $"✅ ایمپورت با موفقیت انجام شد!\n\n" +
                              $"📥 هدرهای جدید: {NumberHelper.FormatThousands(result.HeaderInsertCount, usePersianDigits: true)}\n" +
                              $"🔄 هدرهای آپدیت شده: {NumberHelper.FormatThousands(result.HeaderUpdateCount, usePersianDigits: true)}\n" +
                              $"📥 جزئیات جدید: {NumberHelper.FormatThousands(result.DetailsInsertCount, usePersianDigits: true)}\n" +
                              $"🔄 جزئیات آپدیت شده: {NumberHelper.FormatThousands(result.DetailsUpdateCount, usePersianDigits: true)}\n";
                    }

                    if (result.ErrorCount > 0 || result.MissingPersonnel.Count > 0)
                    {
                        msg += $"\n⚠️ خطاها: {result.ErrorCount}\n";
                        if (result.MissingPersonnel.Count > 0)
                        {
                            msg += $"پرسنل یافت نشده: {result.MissingPersonnel.Count} نفر\n";
                            msg += "اسامی: " + string.Join(", ", result.MissingPersonnel.Take(10)) + (result.MissingPersonnel.Count > 10 ? "..." : "");
                        }
                    }

                    // ✅ FIX هنگ: پیام اینجا نشان داده نمی‌شود (پنجره‌ی مودال زیر لودینگ گیر می‌کرد)؛ بعد از بسته‌شدن لودینگ نشان داده می‌شود
                    resultMsg = msg;
                });
            }, "در حال ایمپورت اطلاعات به دیتابیس...");

            if (IsDisposed) return;

            btnImport.Enabled = true;
            btnPreview.Enabled = true;
            if (success && previewOnly)
            {
                if (previewResult is not null)
                {
                    using var dlg = new DbfPreviewForm(previewResult, abortOnError);
                    proceedAfterPreview = dlg.ShowDialog(this) == DialogResult.OK;
                }
                lblStatus.Text = proceedAfterPreview
                    ? "در حال شروع ایمپورت..."
                    : "🔍 پیش‌نمایش انجام شد؛ هیچ داده‌ای ذخیره نشد.";
                lblStatus.ForeColor = ModernTheme.TextMuted;
            }
            else if (success && rolledBack)
            {
                if (!string.IsNullOrEmpty(resultMsg))
                    ModernMessageBox.ShowWarning(resultMsg);
                lblStatus.Text = "⛔ ایمپورت لغو شد؛ هیچ داده‌ای ذخیره نشد.";
                lblStatus.ForeColor = ModernTheme.Danger;
            }
            else if (success)
            {
                if (!string.IsNullOrEmpty(resultMsg))
                    ModernMessageBox.ShowInfo(resultMsg, "نتیجه ایمپورت");
                lblStatus.Text = "✅ ایمپورت با موفقیت به پایان رسید.";
                lblStatus.ForeColor = ModernTheme.Success;
                ToastHelper.Show("ایمپورت با موفقیت انجام شد.", isSuccess: true);
            }
            else
            {
                lblStatus.Text = "❌ خطا در ایمپورت. جزئیات در فایل لاگ ثبت شد.";
                lblStatus.ForeColor = ModernTheme.Danger;
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "DbfImportForm.BtnImport_Click");
            if (!IsDisposed)
            {
                lblStatus.Text = "❌ خطا در ایمپورت. جزئیات در فایل لاگ ثبت شد.";
                lblStatus.ForeColor = ModernTheme.Danger;
            }
        }
        finally
        {
            if (!IsDisposed) { btnImport.Enabled = true; btnPreview.Enabled = true; }
            try { _actionLock.Release(); } catch { }
        }

        // بعد از آزادشدن قفل: اگر کاربر در پیش‌نمایش «تأیید و ایمپورت» را زد، ایمپورت واقعی اجرا می‌شود
        if (proceedAfterPreview && !IsDisposed) await RunImportAsync(false);
    }
}
