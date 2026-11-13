using ClosedXML.Excel;
using SMP.Data;
using SMP.Helpers;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class ContractTemplateEditForm : Form
    {
        private readonly ContractTemplateRepository _repo;
        private RichTextBox txtContent = null!;

        public ContractTemplateEditForm(string connectionString)
        {
            _repo = new ContractTemplateRepository(connectionString);
            BuildUI();
            this.ApplyStandardHeader("ویرایش متن قرارداد پرسنلی", new Size(900, 700));

            Shown += async (s, e) => await LoadTemplateAsync();
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65F));

            var lblHint = new Label
            {
                Text = "💡 راهنما: می‌توانید متن را از اکسل کپی کرده و اینجا پیست (Ctrl+V) کنید یا از دکمه «ایمپورت از اکسل» استفاده کنید.\n" +
                       "کدهای خودکار: [نام کارمند] - [کد ملی] - [تاریخ تولد] - [شماره شناسنامه] - [تاریخ شروع] - [تاریخ پایان]\n" +
                       "[حقوق پایه] - [پایه سنوات] - [حق مسکن] - [حق خواربار] - [حق اولاد] - [سایر مزایا] - [جمع کل]",
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UIHelper.PersianFont(9.5f, FontStyle.Bold),
                ForeColor = UIHelper.SectionBlue,
                BackColor = Color.FromArgb(240, 248, 255),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10)
            };

            txtContent = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(12f),
                RightToLeft = RightToLeft.Yes,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };

            // جلوگیری از پیست شدن فرمت‌های پیچیده اکسل (فقط متن خالص پیست شود)
            txtContent.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.V)
                {
                    e.Handled = true;
                    if (Clipboard.ContainsText())
                    {
                        txtContent.SelectedText = Clipboard.GetText();
                    }
                }
            };

            var pnlBottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };

            // 👈 دکمه جدید ایمپورت از اکسل اضافه شد
            var btnImport = UIHelper.CreateButton("📥 ایمپورت از اکسل", UIHelper.SectionTeal, 160, 45);
            btnImport.Click += BtnImportExcel_Click;

            var btnSave = UIHelper.CreateButton("💾 ذخیره متن قرارداد", UIHelper.ActionGreen, 200, 45);
            btnSave.Click += async (s, e) => await SaveTemplateAsync();

            pnlBottom.Controls.Add(btnSave);
            pnlBottom.Controls.Add(btnImport);

            var wrapper = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            wrapper.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));
            wrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            wrapper.Controls.Add(lblHint, 0, 0);
            wrapper.Controls.Add(txtContent, 0, 1);

            mainLayout.Controls.Add(wrapper, 0, 0);
            mainLayout.Controls.Add(pnlBottom, 0, 1);

            this.Controls.Add(mainLayout);
        }

        // 👈 متد مربوط به خواندن فایل اکسل
        private void BtnImportExcel_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls",
                Title = "انتخاب فایل اکسل متن قرارداد"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var sb = new StringBuilder();

                // استفاده از FileStream برای جلوگیری از خطای قفل بودن فایل در اکسل
                using var stream = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed();

                foreach (var row in rows)
                {
                    // خواندن متن تمام سلول‌های پرشده در این ردیف
                    var cellValues = row.CellsUsed()
                        .Select(c => c.GetFormattedString().Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();

                    if (cellValues.Any())
                    {
                        // چسباندن متن سلول‌ها با یک فاصله یا Tab
                        sb.AppendLine(string.Join("\t", cellValues));
                    }
                    else
                    {
                        sb.AppendLine(); // اگر ردیف خالی بود، خط خالی بگذار
                    }
                }

                if (sb.Length > 0)
                {
                    txtContent.Text = sb.ToString();
                    ToastHelper.Show("متن قرارداد با موفقیت از اکسل خوانده شد.", true);
                }
                else
                {
                    ModernMessageBox.ShowWarning("فایل اکسل انتخاب شده خالی است.");
                }
            }
            catch (IOException)
            {
                ModernMessageBox.ShowWarning("فایل اکسل در حال حاضر باز است. لطفاً آن را ببندید و دوباره تلاش کنید.");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ContractTemplateEditForm.ImportExcel");
                ModernMessageBox.ShowError("خطا در خواندن فایل اکسل:\n" + ex.Message);
            }
        }

        private async Task LoadTemplateAsync()
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var content = await _repo.GetActiveTemplateContentAsync();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    txtContent.Text = content;
                }
                else
                {
                    txtContent.Text = GetDefaultTemplateText();
                }
            }, "در حال بارگذاری متن قرارداد...");
        }

        private string GetDefaultTemplateText()
        {
            return @"بسمه تعالی
قرارداد کار (مدت موقت)

این قرارداد بین شرکت (کارفرما) و [نام کارمند] فرزند .......... به شماره شناسنامه [شماره شناسنامه] متولد [تاریخ تولد] به کد ملی [کد ملی] (کارپذیر) منعقد می‌گردد.

ماده ۱) موضوع قرارداد:
کارپذیر متعهد می‌گردد در سمت توافق شده، خدمات و وظایف محوله را به نحو مطلوب انجام دهد.

ماده ۲) مدت قرارداد:
مدت این قرارداد از تاریخ [تاریخ شروع] لغایت [تاریخ پایان] می‌باشد.

ماده ۳) محل انجام کار:
محل انجام کار مطابق نظر کارفرما خواهد بود.

ماده ۴) حقوق و مزایا:
حقوق پایه: [حقوق پایه] ریال
پایه سنوات: [پایه سنوات] ریال
حق مسکن: [حق مسکن] ریال
حق خواربار: [حق خواربار] ریال
حق اولاد: [حق اولاد] ریال
سایر مزایا: [سایر مزایا] ریال
جمع کل حقوق و مزایا: [جمع کل] ریال

ماده ۵) این قرارداد با رعایت کامل مقررات قانون کار تنظیم گردیده و طرفین به مفاد آن متعهد می‌باشند.

مهر و امضای کارفرما:
مهر و امضای کارپذیر:";
        }

        private async Task SaveTemplateAsync()
        {
            if (string.IsNullOrWhiteSpace(txtContent.Text))
            {
                ModernMessageBox.ShowWarning("متن قرارداد نمی‌تواند خالی باشد.");
                return;
            }

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repo.SaveTemplateAsync(txtContent.Text);
            }, "در حال ذخیره متن قرارداد...");

            if (success)
            {
                ToastHelper.Show("متن قرارداد با موفقیت ذخیره شد.", true);
                this.Close();
            }
        }
    }
}