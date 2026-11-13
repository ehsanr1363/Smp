using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class LoginForm : Form
    {
        private readonly UserRepository _userRepository;
        private TextBox txtUsername = null!, txtPassword = null!;
        private Button btnLogin = null!, btnCancel = null!;
        private Label lblError = null!;

        public User? AuthenticatedUser { get; private set; }

        public LoginForm(string connectionString)
        {
            _userRepository = new UserRepository(connectionString);
            BuildUI();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                if (ActiveControl == txtUsername) { txtPassword.Focus(); return true; }
                else if (ActiveControl == txtPassword) { BtnLogin_Click(null, EventArgs.Empty); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, "ورود به سمپ", new Size(450, 480)); // ارتفاع را کمی زیاد کردم تا لوگو جا شود
            this.StartPosition = FormStartPosition.CenterScreen;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(30, 20, 30, 20) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F)); // ردیف لوگو
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));  // ردیف عنوان
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // ردیف فرم
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));  // ردیف دکمه‌ها

            // ═══ ۱. نمایش لوگو ═══
            var picLogo = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };

            try
            {
                // تلاش برای خواندن لوگو از پوشه برنامه
                string logoPath = Path.Combine(Application.StartupPath, "logo.png");
                if (File.Exists(logoPath))
                {
                    picLogo.Image = Image.FromFile(logoPath);
                }
            }
            catch { /* اگر لوگو پیدا نشد برنامه کرش نکند */ }

            layout.Controls.Add(picLogo, 0, 0);

            // ═══ ۲. عنوان ═══
            var titlePanel = new Panel { Dock = DockStyle.Fill };
            var lblTitle = new Label
            {
                Text = "سمپ",
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(22f, FontStyle.Bold),
                ForeColor = UIHelper.SectionBlue
            };
            var lblSubTitle = new Label
            {
                Text = "سامانه مدیریت پیمان و پروژه",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopCenter,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText
            };
            titlePanel.Controls.Add(lblSubTitle);
            titlePanel.Controls.Add(lblTitle);
            layout.Controls.Add(titlePanel, 0, 1);

            // ═══ ۳. فرم ═══
            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));

            txtUsername = new TextBox();
            txtPassword = new TextBox { UseSystemPasswordChar = true };
            UIHelper.AddField(fields, "نام کاربری:", txtUsername, 0);
            UIHelper.AddField(fields, "رمز عبور:", txtPassword, 1);

            lblError = new Label { Text = "", ForeColor = UIHelper.WarmErrorText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            fields.Controls.Add(lblError, 0, 2);
            fields.SetColumnSpan(lblError, 2);
            layout.Controls.Add(fields, 0, 2);

            // ═══ ۴. دکمه‌ها ═══
            var btns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 15, 0, 0) };
            btnLogin = UIHelper.CreateButton("ورود", UIHelper.ActionGreen, 120, 45);
            btnLogin.Click += BtnLogin_Click;
            btnCancel = UIHelper.CreateButton("انصراف", UIHelper.ActionRed, 100, 45);
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            btns.Controls.Add(btnLogin);
            btns.Controls.Add(btnCancel);
            layout.Controls.Add(btns, 0, 3);

            this.Controls.Add(layout);
        }

        private async void BtnLogin_Click(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            lblError.Text = "";
            if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                lblError.Text = "اطلاعات را کامل وارد کنید.";
                return;
            }

            try
            {
                btnLogin.Enabled = false;
                var user = await _userRepository.AuthenticateAsync(txtUsername.Text.Trim(), txtPassword.Text);
                if (IsDisposed) return;

                if (user == null) { lblError.Text = "نام کاربری یا رمز اشتباه است."; return; }
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
                if (!IsDisposed) btnLogin.Enabled = true;
            }
        }
    }
}