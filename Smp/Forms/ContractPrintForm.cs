using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Helpers;
using Smp.Models;

namespace Smp.Forms;

/// <summary>پیش‌نمایش و چاپ قرارداد یک پرسنل (دو صفحه‌ی A4).</summary>
public partial class ContractPrintForm : Form
{
    private readonly Employee _employee;
    private WebBrowser browser = null!;
    private ModernButton btnPrint = null!;
    private Label lblStatus = null!;

    private bool _previewShown;   // پیش‌نمایش خودکار فقط یک بار بعد از بارگذاری باز می‌شود

    public ContractPrintForm(Employee employee)
    {
        _employee = employee ?? throw new ArgumentNullException(nameof(employee));

        BuildUI();

        this.ApplyStandardHeader($"پیش‌نمایش و چاپ قرارداد - {employee.FullName}", new Size(1100, 800), isDialog: false);
        MinimumSize = new Size(900, 600);
        ShowInTaskbar = false;
        WindowState = FormWindowState.Maximized;
        ShortcutHelper.ApplyStandardShortcuts(this);

        Shown += async (s, e) => await GenerateHtmlAsync();
    }

    private void BuildUI()
    {
        browser = new WebBrowser
        {
            Dock = DockStyle.Fill,
            IsWebBrowserContextMenuEnabled = false,
            WebBrowserShortcutsEnabled = false,
            AllowWebBrowserDrop = false,
            ScriptErrorsSuppressed = true
        };
        browser.DocumentCompleted += Browser_DocumentCompleted;

        lblStatus = ModernUI.Caption("در حال آماده‌سازی قرارداد...", 10.5f, true, ModernTheme.TextMuted);
        lblStatus.Dock = DockStyle.Fill;
        lblStatus.TextAlign = ContentAlignment.MiddleCenter;

        btnPrint = ModernButton.Success("پیش‌نمایش و چاپ", 200, 40);
        btnPrint.Enabled = false;
        btnPrint.Click += (s, e) => ContractHtmlBuilder.ShowPrintPreview(browser);

        var btnClose = ModernButton.Cancel("بستن", 110, 40);
        btnClose.Click += (s, e) => Close();

        var buttonBar = ModernForm.CreateButtonBar(btnPrint, btnClose);
        buttonBar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, buttonBar.Width, 0);
        };

        // قرارداد داخل یک قاب سفید با حاشیه‌ی هم‌رنگ پس‌زمینه‌ی برنامه
        var browserHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 8, 14, 6),
            BackColor = ModernTheme.Background
        };
        browserHost.Controls.Add(browser);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            BackColor = ModernTheme.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(lblStatus, 0, 0);
        root.Controls.Add(browserHost, 0, 1);
        root.Controls.Add(buttonBar, 0, 2);
        Controls.Add(root);
    }

    private async Task GenerateHtmlAsync()
    {
        try
        {
            string logoHtml = ContractHtmlBuilder.LoadLogoHtml();
            string customText = await ContractHtmlBuilder.LoadCustomTextAsync();
            string pages = await ContractHtmlBuilder.BuildPagesAsync(_employee, logoHtml, customText);

            if (IsDisposed) return;
            browser.DocumentText = ContractHtmlBuilder.WrapDocument(pages);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ContractPrintForm.GenerateHtmlAsync");
            if (IsDisposed) return;
            lblStatus.Text = "خطا در آماده‌سازی قرارداد.";
            lblStatus.ForeColor = ModernTheme.Danger;
            ModernMessageBox.ShowError("آماده‌سازی قرارداد با خطا مواجه شد: " + ex.Message);
        }
    }

    private void Browser_DocumentCompleted(object? sender, WebBrowserDocumentCompletedEventArgs e)
    {
        // سند خالی اولیه‌ی WebBrowser را نادیده بگیر
        if (IsDisposed || browser.Document?.Body is null || browser.DocumentText.Length < 100) return;

        browser.Document.Body.Style = "zoom: 100%;";
        lblStatus.Text = "قرارداد آماده‌ی چاپ است.";
        lblStatus.ForeColor = ModernTheme.Success;
        btnPrint.Enabled = true;

        if (_previewShown) return;
        _previewShown = true;
        ContractHtmlBuilder.ShowPrintPreview(browser);
    }
}
