using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;

namespace Smp.Forms;

public class TankhahListEditForm : Form
{
    private readonly BankAccountRepository _repo;
    private readonly CompanyBankAccount _list;

    private TextBox txtTitle = null!;
    private TextBox txtInitialBalance = null!;
    private TextBox txtCreatedDate = null!; // ✅ فیلد تاریخ
    private ModernCheckBox chkIsActive = null!;

    public TankhahListEditForm(string connectionString, CompanyBankAccount list)
    {
        _repo = new BankAccountRepository(connectionString);
        _list = list;

        BuildUI();
        this.ShowInTaskbar = false;
        ShortcutHelper.ApplyStandardShortcuts(this);
    }

    private void BuildUI()
    {
        int fh = ModernUI.FieldHeight;

        ModernTextBox Box(bool center = false)
        {
            var b = ModernUI.Input();
            if (center) b.TextAlign = HorizontalAlignment.Center;
            return b;
        }

        // ───── فیلدها ─────
        var titleBox = Box(); txtTitle = titleBox.TextBox;
        txtTitle.Text = _list.Title;

        var balanceBox = Box(true); txtInitialBalance = balanceBox.TextBox;
        txtInitialBalance.Text = NumberHelper.FormatThousands(_list.InitialBalance, usePersianDigits: true);
        SmartInputHelper.EnableSmartAmountTextBox(txtInitialBalance);

        // ✅ فیلد تاریخ
        var dateBox = Box(true); txtCreatedDate = dateBox.TextBox;
        DateHelper.ApplyDateMask(txtCreatedDate);
        txtCreatedDate.Text = DateHelper.ToPersianDate(_list.CreatedAt);

        chkIsActive = ModernUI.Check("لیست فعال است", ModernTheme.Success);
        chkIsActive.Checked = _list.IsActive;

        // ───── کارت ─────
        var card = new ModernCard("مشخصات لیست", ModernTheme.Primary, 1, fh, fh, fh, 44);
        card.Body.Controls.Add(ModernUI.Field("عنوان لیست", titleBox), 0, 0);
        card.Body.Controls.Add(ModernUI.Field("موجودی اولیه", balanceBox), 0, 1);
        card.Body.Controls.Add(ModernUI.Field("تاریخ ثبت / شارژ لیست", dateBox), 0, 2);
        card.Body.Controls.Add(ModernUI.CenterInCell(chkIsActive, 12), 0, 3);

        // دکمه ذخیره
        var btnSave = ModernButton.Success("💾 ذخیره تغییرات", 180, 40);
        btnSave.Click += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                ModernMessageBox.ShowWarning("عنوان لیست الزامی است.");
                return;
            }

            DateTime? parsedDate = DateHelper.ParsePersianDate(txtCreatedDate.Text);
            if (parsedDate is null)
            {
                ModernMessageBox.ShowWarning("تاریخ وارد شده معتبر نیست.");
                return;
            }

            _list.Title = txtTitle.Text.Trim();
            _list.InitialBalance = NumberHelper.ParseFormatted(txtInitialBalance.Text);
            _list.CreatedAt = parsedDate.Value; // ✅ ثبت تاریخ جدید
            _list.IsActive = chkIsActive.Checked;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repo.UpdateAsync(_list);
            }, "در حال ذخیره...");

            if (success)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        // دکمه انصراف
        var btnCancel = ModernButton.Cancel();
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var bar = ModernForm.CreateButtonBar(btnSave, btnCancel);
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var page = new ModernPage();
        page.AddCard(card, 8);

        string title = $"ویرایش مشخصات {_list.Title}";
        SuspendLayout();
        ModernForm.Setup(this, title, new Size(550, 560), isDialog: true);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(ModernForm.CreateHeader(title, "ویرایش عنوان، موجودی اولیه و وضعیت لیست"), 0, 0);
        root.Controls.Add(page, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
        ResumeLayout();
    }
}
