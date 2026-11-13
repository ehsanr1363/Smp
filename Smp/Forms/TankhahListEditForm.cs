using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;

namespace SMP.Forms
{
    public class TankhahListEditForm : Form
    {
        private readonly BankAccountRepository _repo;
        private readonly CompanyBankAccount _list;

        private TextBox txtTitle = null!;
        private TextBox txtInitialBalance = null!;
        private CheckBox chkIsActive = null!;

        public TankhahListEditForm(string connectionString, CompanyBankAccount list)
        {
            _repo = new BankAccountRepository(connectionString);
            _list = list;

            // 🔴 ارتفاع فرم از 320 به 450 افزایش یافت تا اسکرول حذف شود
            BuildUI();
            this.ShowInTaskbar = false;
            ShortcutHelper.ApplyStandardShortcuts(this);
        }

        private void BuildUI()
        {
            // 🔴 اندازه فرم اینجا تنظیم می‌شود
            var (formLayout, btnPanel) = StandardFormHelper.SetupStandardLayout(
                this, $"ویرایش مشخصات لیست {_list.Title}", new Size(550, 450));

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "عنوان لیست:", txtTitle = new TextBox(), 0, 0, 1, 3);
            txtTitle.Text = _list.Title;

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "موجودی اولیه:", txtInitialBalance = new TextBox(), 1, 0, 1, 3);
            txtInitialBalance.Text = NumberHelper.FormatThousands(_list.InitialBalance, usePersianDigits: true);
            SmartInputHelper.EnableSmartAmountTextBox(txtInitialBalance);

            StandardFormHelper.AddRow(formLayout, 50F);
            chkIsActive = new CheckBox { Text = "لیست فعال است", Checked = _list.IsActive, AutoSize = true };
            UIHelper.StyleToggle(chkIsActive);
            formLayout.Controls.Add(chkIsActive, 1, 2);

            // دکمه ذخیره
            var btnSave = UIHelper.CreateButton("💾 ذخیره تغییرات", UIHelper.ActionGreen, 180, 45);
            btnSave.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                {
                    ModernMessageBox.ShowWarning("عنوان لیست الزامی است.");
                    return;
                }

                _list.Title = txtTitle.Text.Trim();
                _list.InitialBalance = NumberHelper.ParseFormatted(txtInitialBalance.Text);
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
            var btnCancel = UIHelper.CreateButton("انصراف", UIHelper.SectionGray, 100, 45);
            btnCancel.Click += (s, e) => Close();

            btnPanel.Controls.Add(btnSave);
            btnPanel.Controls.Add(btnCancel);
        }
    }
}