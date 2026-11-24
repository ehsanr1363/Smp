using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class LoginForm : Form
{
    private readonly string _connectionString;
    private readonly UserRepository _userRepository;
    private readonly CompanyRepository _companyRepository;

    private TextBox txtUsername = null!, txtPassword = null!;
    private Button btnLogin = null!, btnCancel = null!;
    private Label lblError = null!;
    private PictureBox picLogo = null!;

    public User? AuthenticatedUser { get; private set; }

    // محدودسازی تلاش ناموفق ورود (در حافظه): بعد از ۵ خطا، ۳۰ ثانیه قفل
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(30);
    private int _failedAttempts;
    private DateTime _lockedUntil = DateTime.MinValue;

    public LoginForm(string connectionString)
    {
        _connectionString = connectionString;
        _userRepository = new UserRepository(connectionString);
        _companyRepository = new CompanyRepository(connectionString);

        BuildUI();

        // ✅ وسط‌چین کردن دقیق در زمان لود شدن فرم
        this.Load += (s, e) => CenterFormOnScreen();

        // لود کردن لوگو بعد از نمایش فرم
        this.Shown += async (s, e) => await LoadLogoAsync();
    }

    // ✅ متد اختصاصی برای وسط‌چین کردن دقیق
    private void CenterFormOnScreen()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        this.Location = new Point(
            screen.WorkingArea.Left + (screen.WorkingArea.Width - this.Width) / 2,
            screen.WorkingArea.Top + (screen.WorkingArea.Height - this.Height) / 2
        );
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter)
        {
            if (ActiveControl == txtUsername) { txtPassword.Focus(); return true; }
            else if (ActiveControl == txtPassword || ActiveControl == btnLogin) { BtnLogin_Click(null, EventArgs.Empty); return true; }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BuildUI()
    {
        // تنظیمات کلی فرم (بدون EnterNavigation عمومی؛ Enter در ProcessCmdKey مدیریت می‌شود)
        this.Text = "ورود به سمپ";
        this.Size = new Size(460, 620);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = ModernTheme.Background;
        this.Font = ModernTheme.GetFont(11f);
        this.RightToLeft = RightToLeft.Yes;
        this.RightToLeftLayout = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(44, 28, 44, 24),
            BackColor = ModernTheme.Background
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));                         // لوگو
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));                          // عنوان
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, ModernUI.FieldHeight + 14));    // نام کاربری
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, ModernUI.FieldHeight + 14));    // رمز عبور
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));                          // پیام خطا
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));                          // دکمه‌ها

        // ═══ ۱. لوگو ═══
        picLogo = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };
        mainLayout.Controls.Add(picLogo, 0, 0);

        // ═══ ۲. عنوان ═══
        var titlePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var lblTitle = new Label
        {
            Text = "سمپ",
            Dock = DockStyle.Top,
            Height = 50,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(26f, FontStyle.Bold),
            ForeColor = ModernTheme.Primary
        };
        var lblSubTitle = new Label
        {
            Text = "سامانه مدیریت پیمان و پروژه",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopCenter,
            Font = ModernTheme.GetFont(10.5f),
            ForeColor = ModernTheme.TextMuted
        };
        titlePanel.Controls.Add(lblSubTitle);
        titlePanel.Controls.Add(lblTitle);
        mainLayout.Controls.Add(titlePanel, 0, 1);

        // ═══ ۳. کادرهای ورودی (متن وسط‌چین) ═══
        txtUsername = new TextBox { TextAlign = HorizontalAlignment.Center };
        txtPassword = new TextBox { TextAlign = HorizontalAlignment.Center, UseSystemPasswordChar = true };

        var fldUser = ModernUI.Field("نام کاربری", txtUsername);
        var fldPass = ModernUI.Field("رمز عبور", txtPassword);
        fldUser.Margin = new Padding(0, 0, 0, 12);
        fldPass.Margin = new Padding(0, 0, 0, 12);
        mainLayout.Controls.Add(fldUser, 0, 2);
        mainLayout.Controls.Add(fldPass, 0, 3);

        // ═══ ۴. پیام خطا ═══
        lblError = new Label
        {
            Text = "",
            ForeColor = ModernTheme.DangerText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(10f, FontStyle.Bold)
        };
        mainLayout.Controls.Add(lblError, 0, 4);

        // ═══ ۵. دکمه‌ها ═══
        var btns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        btns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
        btns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));

        btnLogin = ModernButton.Save("ورود به سیستم", 150, 46);
        btnLogin.Dock = DockStyle.Fill;
        btnLogin.Margin = new Padding(0, 0, 6, 0);
        btnLogin.Click += BtnLogin_Click;

        btnCancel = ModernButton.Cancel("انصراف", 100, 46);
        btnCancel.Dock = DockStyle.Fill;
        btnCancel.Margin = new Padding(6, 0, 0, 0);
        btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

        btns.Controls.Add(btnLogin, 0, 0);
        btns.Controls.Add(btnCancel, 1, 0);
        mainLayout.Controls.Add(btns, 0, 5);

        this.Controls.Add(mainLayout);
    }

    private async Task LoadLogoAsync()
    {
        try
        {
            // تلاش برای خواندن لوگو از دیتابیس
            var company = await Task.Run(() => _companyRepository.GetAll().FirstOrDefault());

            if (company is not null && company.Logo is not null && company.Logo.Length > 0)
            {
                picLogo.Image = ImageHelper.ByteArrayToImage(company.Logo);
            }
            else
            {
                // اگر در دیتابیس لوگو نبود، تلاش می‌کند از فایل logo.png کنار برنامه بخواند
                string logoPath = Path.Combine(Application.StartupPath, "logo.png");
                if (File.Exists(logoPath))
                {
                    using var stream = new FileStream(logoPath, FileMode.Open, FileAccess.Read);
                    picLogo.Image = Image.FromStream(stream);
                }
            }
        }
        catch (Exception __ex) { /* اگر خطایی رخ داد، فرم بدون لوگو باز می‌شود (برنامه کرش نمی‌کند) */ Smp.Helpers.LogHelper.LogException(__ex, "LoginForm"); }
    }

    private async void BtnLogin_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        lblError.Text = "";

        if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
        {
            lblError.Text = "لطفاً نام کاربری و رمز عبور را وارد کنید.";
            return;
        }

        if (DateTime.Now < _lockedUntil)
        {
            int secs = (int)Math.Ceiling((_lockedUntil - DateTime.Now).TotalSeconds);
            lblError.Text = $"تلاش‌های ناموفق زیاد بود. {secs} ثانیه دیگر دوباره تلاش کنید.";
            return;
        }

        try
        {
            btnLogin.Enabled = false;
            btnLogin.Text = "در حال بررسی...";

            var user = await _userRepository.AuthenticateAsync(txtUsername.Text.Trim(), txtPassword.Text);

            if (IsDisposed) return;

            if (user is null)
            {
                if (++_failedAttempts >= MaxFailedAttempts)
                {
                    _failedAttempts = 0;
                    _lockedUntil = DateTime.Now + LockDuration;
                }
                lblError.Text = "نام کاربری یا رمز عبور اشتباه است.";
                _ = new AuditLogRepository(_connectionString).AddLogAsync(txtUsername.Text.Trim(), "ورود ناموفق", "احراز هویت", "نام کاربری یا رمز عبور اشتباه");
                return;
            }
            _failedAttempts = 0;

            AuthenticatedUser = user;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "Login");
            if (!IsDisposed) lblError.Text = "خطا در اتصال به دیتابیس.";
        }
        finally
        {
            if (!IsDisposed)
            {
                btnLogin.Enabled = true;
                btnLogin.Text = "ورود به سیستم";
            }
        }
    }
}
