using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

/// <summary>تغییر رمز اجباری (اولین ورود یا رمز پیش‌فرض).</summary>
public class ChangePasswordForm : Form
{
    private readonly User _user;
    private readonly UserRepository _repo;

    private readonly ModernTextBox txtNew = ModernUI.Input(ModernInputKind.Password);
    private readonly ModernTextBox txtConfirm = ModernUI.Input(ModernInputKind.Password);
    private readonly Label lblError = ModernUI.Caption("", 10.5f, true, ModernTheme.Danger);
    private readonly ModernButton btnSave = ModernButton.Save("ذخیره رمز جدید", 200, 40);

    public ChangePasswordForm(User user, UserRepository repo)
    {
        _user = user;
        _repo = repo;

        BuildUI();

        btnSave.Click += async (s, e) => await SaveAsync();
        AcceptButton = btnSave;
        Shown += (s, e) => txtNew.Focus();
    }

    private void BuildUI()
    {
        SuspendLayout();

        ModernForm.Setup(this, "تغییر رمز عبور", new Size(480, 400), isDialog: true);
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ControlBox = false;   // کاربر نمی‌تواند بدون تغییر رمز ادامه دهد

        lblError.Dock = DockStyle.Fill;
        lblError.TextAlign = ContentAlignment.MiddleCenter;

        var info = ModernListKit.Banner(
            "رمز عبور شما پیش‌فرض است یا توسط مدیر تعیین شده است." + Environment.NewLine +
            "لطفاً رمز جدید انتخاب کنید (حداقل ۸ کاراکتر).",
            ModernTheme.Primary);

        int rh = ModernUI.RowHeight;
        var card = new ModernCard("رمز عبور جدید", ModernTheme.Brand, 1, rh, rh);
        card.Body.Controls.Add(ModernUI.Row("رمز جدید", txtNew, 150), 0, 0);
        card.Body.Controls.Add(ModernUI.Row("تکرار رمز جدید", txtConfirm, 150), 0, 1);

        var buttons = ModernForm.CreateButtonBar(btnSave);

        const int headerH = 58, infoH = 76, errorH = 34, buttonsH = 64;
        int cardH = card.Height + 12;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            BackColor = ModernTheme.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, headerH));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, infoH));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, cardH));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, errorH));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, buttonsH));

        root.Controls.Add(ModernForm.CreateHeader("تغییر رمز عبور", "برای ادامه کار، رمز عبور جدید تعیین کنید"), 0, 0);
        root.Controls.Add(Pad(info, new Padding(14, 6, 14, 0)), 0, 1);
        root.Controls.Add(Pad(card, new Padding(14, 6, 14, 6)), 0, 2);
        root.Controls.Add(Pad(lblError, new Padding(14, 0, 14, 0)), 0, 3);
        root.Controls.Add(buttons, 0, 4);

        Controls.Add(root);
        ClientSize = new Size(480, headerH + infoH + cardH + errorH + buttonsH);

        ResumeLayout();
    }

    private static Panel Pad(Control inner, Padding padding)
    {
        var p = new Panel { Dock = DockStyle.Fill, Padding = padding, BackColor = Color.Transparent, Margin = Padding.Empty };
        inner.Dock = DockStyle.Fill;
        p.Controls.Add(inner);
        return p;
    }

    private async Task SaveAsync()
    {
        lblError.Text = "";
        string p1 = txtNew.Text, p2 = txtConfirm.Text;

        string? problem = Validate(p1, p2);
        if (problem is not null)
        {
            lblError.Text = problem;
            (string.IsNullOrWhiteSpace(p1) || p1.Length < 8 || p1.ToLowerInvariant() == _user.Username.ToLowerInvariant() ? txtNew : txtConfirm).Focus();
            return;
        }

        try
        {
            btnSave.Enabled = false;
            await _repo.ChangeOwnPasswordAsync(_user.Id, p1);
            _user.MustChangePassword = false;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ChangePasswordForm");
            lblError.Text = "خطا در ذخیره رمز جدید.";
        }
        finally
        {
            if (!IsDisposed) btnSave.Enabled = true;
        }
    }

    private string? Validate(string p1, string p2)
    {
        if (string.IsNullOrWhiteSpace(p1) || p1.Length < 8) return "رمز عبور باید حداقل ۸ کاراکتر باشد.";
        if (p1 != p2) return "رمز و تکرار آن یکسان نیست.";
        string lower = p1.ToLowerInvariant();
        if (lower == _user.Username.ToLowerInvariant() || lower == "admin" || lower == "123456" || lower == "12345678" || lower == "password")
            return "این رمز بسیار ساده است؛ رمز دیگری انتخاب کنید.";
        return null;
    }
}
