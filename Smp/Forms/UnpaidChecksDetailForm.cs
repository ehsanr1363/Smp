using SMP.Helpers;
using SMP.Models;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class UnpaidChecksDetailForm : Form
    {
        public UnpaidChecksDetailForm(System.Collections.Generic.List<CashCheck> checks)
        {
            BuildUI(checks);
            this.ApplyStandardHeader("لیست چک‌های پاس‌نشده (در جریان / برگشتی)", new Size(900, 600));
        }

        private void BuildUI(System.Collections.Generic.List<CashCheck> checks)
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                RightToLeft = RightToLeft.Yes,
                RowHeadersVisible = false,
                Font = UIHelper.PersianFont(11f)
            };
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = UIHelper.WarmPanelBackground;
            dgv.ColumnHeadersDefaultCellStyle.Font = UIHelper.PersianFont(11f, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 40;

            var displayData = checks.OrderBy(c => c.DueDate).Select(c => new
            {
                شماره_چک = c.CheckNumber,
                نام_گیرنده = c.RecipientName ?? "---",
                بانک = c.BankName ?? "---",
                تاریخ_سررسید = DateHelper.ToPersianDate(c.DueDate),
                مبلغ = c.Amount.ToString("N0"),
                وضعیت = c.Status,
                توضیحات = c.Description ?? "---"
            }).ToList();

            dgv.DataSource = displayData;
            this.Controls.Add(dgv);
        }
    }
}