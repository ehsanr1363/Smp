using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Models;
using PersonnelApp.Helpers;
using PersonnelApp.Data;

namespace PersonnelApp.Forms
{
    public class ContractorContractEditForm : Form
    {
        private readonly string _connectionString;
        private readonly ContractorContract? _contract;
        private readonly bool _isEdit;

        private TextBox txtPartyName = null!;
        private int _selectedPartyId = 0;
        private ComboBox cmbClientContract = null!;

        private TextBox txtContractNumber = null!, txtContractRow = null!, txtSubject = null!, txtAmount = null!, txtContractDate = null!, txtEndDate = null!;
        private TextBox txtDurationMonths = null!;
        private TextBox txtGuarantee = null!, txtRetention = null!, txtInsurance = null!, txtAdvancePercent = null!, txtTotalAdvance = null!, txtVat = null!;
        private Button btnSave = null!;

        public ContractorContractEditForm(string connectionString, ContractorContract? contract = null)
        {
            _connectionString = connectionString;
            _contract = contract;
            _isEdit = contract != null;

            BuildUI();

            // فعال‌سازی میانبرهای Ctrl+S (ذخیره) و Esc (بستن)
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            ShowInTaskbar = false;

            // بارگذاری اطلاعات پس از نمایش فرم (جلوگیری از فریز شدن)
            Shown += async (s, e) =>
            {
                await LoadClientContractsAsync();
                if (_isEdit && _contract != null)
                    await LoadDataAsync();
            };
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 9, AutoSize = true, Padding = new Padding(15) };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            for (int i = 0; i < 9; i++)
                formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));

            // --- ردیف ۰: انتخاب پیمانکار ---
            var lblParty = new Label { Text = "پیمانکار:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
            UIHelper.StyleLabel(lblParty);

            txtPartyName = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, TextAlign = HorizontalAlignment.Center };
            txtPartyName.BackColor = UIHelper.WarmInputBackground;
            txtPartyName.BorderStyle = BorderStyle.FixedSingle;
            txtPartyName.Font = UIHelper.PersianFont(12f);
            txtPartyName.Margin = new Padding(5, 8, 5, 5);

            var btnSelect = UIHelper.CreateButton("🔍 انتخاب", UIHelper.WarmSecondary, 80, 35);
            btnSelect.Margin = new Padding(5, 8, 5, 5);
            btnSelect.Click += BtnSelect_Click;

            formLayout.Controls.Add(lblParty, 0, 0);
            formLayout.Controls.Add(txtPartyName, 1, 0);
            formLayout.SetColumnSpan(txtPartyName, 2);
            formLayout.Controls.Add(btnSelect, 3, 0);

            // --- ردیف ۱: انتخاب قرارداد کارفرما ---
            var lblClientContract = new Label { Text = "قرارداد کارفرما:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
            UIHelper.StyleLabel(lblClientContract);

            cmbClientContract = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmInputBackground };
            cmbClientContract.Margin = new Padding(5, 8, 5, 5);

            formLayout.Controls.Add(lblClientContract, 0, 1);
            formLayout.Controls.Add(cmbClientContract, 1, 1);
            formLayout.SetColumnSpan(cmbClientContract, 3);

            // --- بقیه فیلدها ---
            txtContractNumber = new TextBox();
            txtContractRow = new TextBox();
            txtSubject = new TextBox();
            txtAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount); // 👈 فرمت سه‌رقمی خودکار

            txtContractDate = new TextBox();
            DateHelper.ApplyDateMask(txtContractDate);

            txtEndDate = new TextBox();
            DateHelper.ApplyDateMask(txtEndDate);

            txtDurationMonths = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Center };

            UIHelper.AddField(formLayout, "شماره قرارداد:", txtContractNumber, 2, labelCol: 0, controlCol: 1);
            UIHelper.AddField(formLayout, "ردیف پیمان:", txtContractRow, 2, labelCol: 2, controlCol: 3);
            UIHelper.AddField(formLayout, "موضوع:", txtSubject, 3, labelCol: 0, controlCol: 1, controlSpan: 3);
            UIHelper.AddField(formLayout, "مبلغ (ریال):", txtAmount, 4, labelCol: 0, controlCol: 1);
            UIHelper.AddField(formLayout, "تاریخ قرارداد:", txtContractDate, 4, labelCol: 2, controlCol: 3);
            UIHelper.AddField(formLayout, "مدت (ماه):", txtDurationMonths, 5, labelCol: 0, controlCol: 1);
            UIHelper.AddField(formLayout, "تاریخ پایان:", txtEndDate, 5, labelCol: 2, controlCol: 3);

            txtGuarantee = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Center };
            txtRetention = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Center };
            txtInsurance = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Center };
            txtAdvancePercent = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Center };
            txtTotalAdvance = new TextBox { Text = "0", ReadOnly = true, TextAlign = HorizontalAlignment.Center, BackColor = UIHelper.WarmInputBackground };
            txtVat = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Center };

            UIHelper.AddField(formLayout, "درصد تضمین (%):", txtGuarantee, 6, labelCol: 0, controlCol: 1);
            UIHelper.AddField(formLayout, "درصد حسن انجام کار (%):", txtRetention, 6, labelCol: 2, controlCol: 3);
            UIHelper.AddField(formLayout, "درصد بیمه (%):", txtInsurance, 7, labelCol: 0, controlCol: 1);
            UIHelper.AddField(formLayout, "درصد پیش‌پرداخت (%):", txtAdvancePercent, 7, labelCol: 2, controlCol: 3);
            UIHelper.AddField(formLayout, "مبلغ کل پیش‌پرداخت:", txtTotalAdvance, 8, labelCol: 0, controlCol: 1);
            UIHelper.AddField(formLayout, "درصد ارزش افزوده (%):", txtVat, 8, labelCol: 2, controlCol: 3);

            // رویدادهای محاسباتی
            txtAmount.TextChanged += CalculateTotalAdvance;
            txtAdvancePercent.TextChanged += CalculateTotalAdvance;
            txtContractDate.Leave += CalculateEndDate;
            txtDurationMonths.TextChanged += CalculateEndDate;

            // --- دکمه ذخیره ---
            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.WarmPrimary, 250, 45);
            btnSave.Font = UIHelper.PersianFont(14f, FontStyle.Bold);

            var btnPanel = new Panel { Dock = DockStyle.Fill };
            btnSave.Anchor = AnchorStyles.None;
            btnPanel.Controls.Add(btnSave);
            btnPanel.Resize += (s, e) => {
                btnSave.Left = (btnPanel.Width - btnSave.Width) / 2;
                btnSave.Top = (btnPanel.Height - btnSave.Height) / 2;
            };
            btnSave.Click += BtnSave_Click;

            mainLayout.Controls.Add(formLayout, 0, 0);
            mainLayout.Controls.Add(btnPanel, 0, 1);

            this.Controls.Add(mainLayout);

            // اعمال هدر استاندارد (حذف نیاز به ProcessCmdKey)
            this.ApplyStandardHeader(_isEdit ? "ویرایش قرارداد پیمانکار" : "افزودن قرارداد پیمانکار", new Size(850, 750));
        }

        // ==========================================================
        // 📥 بارگذاری لیست قراردادهای کارفرما (Async)
        // ==========================================================
        private async Task LoadClientContractsAsync()
        {
            var (success, list) = await AsyncHelper.RunWithLoading(
                this,
                () => Task.Run(() => new ClientContractRepository(_connectionString).GetAll()),
                "در حال بارگذاری لیست قراردادها...");

            if (!success || list == null)
            {
                list = new List<ClientContract>();
            }

            // 👈 اصلاح شد: ساخت لیست ناشناس برای اینکه DisplayMember به درستی کار کند
            var displayList = list.Select(c => new
            {
                c.Id,
                DisplayInfo = string.IsNullOrWhiteSpace(c.Subject) ? c.ContractNumber : $"{c.ContractNumber} - {c.Subject}"
            }).ToList();

            if (!displayList.Any(c => c.Id == 0))
            {
                displayList.Insert(0, new { Id = 0, DisplayInfo = "--- بدون ارتباط ---" });
            }

            cmbClientContract.DataSource = null;
            cmbClientContract.DisplayMember = "DisplayInfo";
            cmbClientContract.ValueMember = "Id";
            cmbClientContract.DataSource = displayList;
        }

        // ==========================================================
        // 📅 محاسبات
        // ==========================================================
        private void CalculateEndDate(object? sender, EventArgs e)
        {
            if (int.TryParse(txtDurationMonths.Text, out int months) && months > 0)
            {
                DateTime? startDate = DateHelper.ParsePersianDate(txtContractDate.Text);
                if (startDate.HasValue)
                {
                    DateTime endDate = startDate.Value.AddMonths(months);
                    txtEndDate.Text = DateHelper.ToPersianDate(endDate);
                }
            }
        }

        private void CalculateTotalAdvance(object? sender, EventArgs e)
        {
            // 👈 استفاده از NumberHelper برای پشتیبانی از ارقام فارسی
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            decimal percent = NumberHelper.ParseFormatted(txtAdvancePercent.Text);

            if (amount > 0 && percent > 0)
            {
                decimal total = (amount * percent) / 100;
                txtTotalAdvance.Text = NumberHelper.FormatThousands(total, usePersianDigits: true);
            }
            else
            {
                txtTotalAdvance.Text = "0";
            }
        }

        // ==========================================================
        // 👤 انتخاب پیمانکار
        // ==========================================================
        private void BtnSelect_Click(object? sender, EventArgs e)
        {
            var result = PartySelectionHelper.ShowSelectionDialog(_connectionString, isContractor: true, owner: this);
            if (result.Id > 0)
            {
                _selectedPartyId = result.Id;
                txtPartyName.Text = result.Name;
            }
        }

        // ==========================================================
        // 📥 بارگذاری اطلاعات در حالت ویرایش (Async)
        // ==========================================================
        private async Task LoadDataAsync()
        {
            if (_contract == null) return;

            _selectedPartyId = _contract.PartyId;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                // دریافت نام پیمانکار
                string query = "SELECT PartyType, FirstName, LastName, CompanyName FROM Parties WHERE Id = @id";
                var dt = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, query, new Microsoft.Data.SqlClient.SqlParameter("@id", _selectedPartyId)));

                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    string type = row["PartyType"].ToString()!;
                    txtPartyName.Text = type == "حقیقی" ? $"{row["FirstName"]} {row["LastName"]}" : row["CompanyName"].ToString();
                }
            }, "در حال دریافت اطلاعات پیمانکار...");

            if (!success) return;

            // پیدا کردن آیتم در لیست کامبوباکس
            if (_contract.ClientContractId.HasValue && _contract.ClientContractId.Value > 0)
            {
                bool found = false;
                for (int i = 0; i < cmbClientContract.Items.Count; i++)
                {
                    var item = cmbClientContract.Items[i];
                    var idProp = item.GetType().GetProperty("Id");
                    if (idProp != null && (int)idProp.GetValue(item)! == _contract.ClientContractId.Value)
                    {
                        cmbClientContract.SelectedIndex = i;
                        found = true;
                        break;
                    }
                }
                if (!found && cmbClientContract.Items.Count > 0) cmbClientContract.SelectedIndex = 0;
            }
            else
            {
                if (cmbClientContract.Items.Count > 0) cmbClientContract.SelectedIndex = 0;
            }

            // پر کردن فیلدها (اعداد با ارقام فارسی)
            txtContractNumber.Text = _contract.ContractNumber;
            txtContractRow.Text = _contract.ContractRow;
            txtSubject.Text = _contract.Subject;
            txtAmount.Text = NumberHelper.FormatThousands(_contract.Amount, usePersianDigits: true);

            txtContractDate.Text = DateHelper.ToPersianDate(_contract.ContractDate);
            txtEndDate.Text = DateHelper.ToPersianDate(_contract.EndDate);

            if (_contract.ContractDate.HasValue && _contract.EndDate.HasValue)
            {
                int monthDiff = ((_contract.EndDate.Value.Year - _contract.ContractDate.Value.Year) * 12) + _contract.EndDate.Value.Month - _contract.ContractDate.Value.Month;
                txtDurationMonths.Text = monthDiff.ToString();
            }

            txtGuarantee.Text = _contract.GuaranteePercentage.ToString();
            txtRetention.Text = _contract.RetentionPercentage.ToString();
            txtInsurance.Text = _contract.InsurancePercentage.ToString();
            txtAdvancePercent.Text = _contract.AdvancePaymentPercentage.ToString();
            txtTotalAdvance.Text = NumberHelper.FormatThousands(_contract.TotalAdvanceAmount, usePersianDigits: true);
            txtVat.Text = _contract.VatPercentage.ToString();
        }

        // ==========================================================
        // 💾 ذخیره اطلاعات (Async)
        // ==========================================================
        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_selectedPartyId == 0)
            {
                ModernMessageBox.ShowWarning("لطفاً یک پیمانکار را از لیست انتخاب کنید.");
                return;
            }

            if (!DateHelper.IsValidPersianDate(txtContractDate.Text) || (!string.IsNullOrWhiteSpace(txtEndDate.Text) && !DateHelper.IsValidPersianDate(txtEndDate.Text)))
            {
                ModernMessageBox.ShowWarning("فرمت تاریخ وارد شده معتبر نیست.");
                return;
            }

            // 👈 استفاده از NumberHelper برای خواندن مقادیر
            decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
            decimal guarantee = NumberHelper.ParseFormatted(txtGuarantee.Text);
            decimal retention = NumberHelper.ParseFormatted(txtRetention.Text);
            decimal insurance = NumberHelper.ParseFormatted(txtInsurance.Text);
            decimal advancePercent = NumberHelper.ParseFormatted(txtAdvancePercent.Text);
            decimal totalAdvance = NumberHelper.ParseFormatted(txtTotalAdvance.Text);
            decimal vatPercent = NumberHelper.ParseFormatted(txtVat.Text);

            int? selectedClientContractId = null;
            if (cmbClientContract.SelectedItem != null)
            {
                var idProp = cmbClientContract.SelectedItem.GetType().GetProperty("Id");
                if (idProp != null)
                {
                    int id = (int)idProp.GetValue(cmbClientContract.SelectedItem)!;
                    if (id > 0) selectedClientContractId = id;
                }
            }

            var c = new ContractorContract
            {
                Id = _isEdit && _contract != null ? _contract.Id : 0,
                PartyId = _selectedPartyId,
                ClientContractId = selectedClientContractId,
                ContractNumber = txtContractNumber.Text.Trim(),
                ContractRow = txtContractRow.Text.Trim(),
                Subject = txtSubject.Text.Trim(),
                Amount = amount,
                ContractDate = DateHelper.ParsePersianDate(txtContractDate.Text),
                EndDate = DateHelper.ParsePersianDate(txtEndDate.Text),

                GuaranteePercentage = guarantee,
                RetentionPercentage = retention,
                InsurancePercentage = insurance,
                AdvancePaymentPercentage = advancePercent,
                TotalAdvanceAmount = totalAdvance,
                VatPercentage = vatPercent
            };

            // مهر زمانی سیستم
            if (_isEdit) DateStampHelper.StampUpdated(c);
            else DateStampHelper.StampCreated(c);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new ContractorContractRepository(_connectionString);
                if (_isEdit)
                    await Task.Run(() => repo.Update(c));
                else
                    await Task.Run(() => repo.Add(c));
            }, "در حال ذخیره قرارداد...");

            if (success)
            {
                ToastHelper.Show("قرارداد پیمانکار با موفقیت ذخیره شد.", isSuccess: true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}