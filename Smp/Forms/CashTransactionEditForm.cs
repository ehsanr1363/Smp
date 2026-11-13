using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace SMP.Forms
{
    public class CashTransactionEditForm : Form
    {
        private readonly CashTransactionRepository _repository;
        private readonly BankAccountRepository _bankRepo;
        private readonly LookupRepository _lookupRepo;
        private readonly TafsiliRepository _tafsiliRepo;
        private readonly string _connectionString;
        private int? _transactionId;

        private RadioButton rbAdmin = null!, rbDirectPurchase = null!, rbInformalContractor = null!, rbContractorContract = null!;

        private ComboBox cmbAccount = null!, cmbDirection = null!, cmbPaymentMethod = null!;
        private ComboBox cmbClientContract = null!, cmbContractorContract = null!;
        private ComboBox cmbCategory = null!, cmbCostType = null!, cmbTafsili = null!;
        private Button btnSelectTafsili = null!;
        private TextBox txtDate = null!, txtAmount = null!, txtAmountInWords = null!;
        private TextBox txtDescription = null!, txtRecipientSheba = null!;
        private TextBox txtAutoContractDisplay = null!;
        private Button btnSave = null!, btnAttachments = null!;

        private Panel pnlScopePanel = null!;
        private Panel pnlClientContractSection = null!;
        private Panel pnlContractorContractSection = null!;
        private TableLayoutPanel formLayout = null!;

        private readonly Dictionary<string, string> _tafsiliShebaDict = new();
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        private bool _isInitializing = true;

        private List<string> _projectCostTypes = new();
        private List<string> _adminCostTypes = new();

        private decimal _originalDiscount = 0m;
        private decimal _originalShipping = 0m;
        private decimal _originalVAT = 0m;
        private int? _originalAccountId = null;

        private class ComboItem
        {
            public int Id { get; set; }
            public string Display { get; set; } = "";
            public int? ClientContractId { get; set; }
            public int PartyId { get; set; }
        }

        public CashTransactionEditForm(string connectionString, int? transactionId = null)
        {
            _connectionString = connectionString;
            _repository = new CashTransactionRepository(connectionString);
            _bankRepo = new BankAccountRepository(connectionString);
            _lookupRepo = new LookupRepository(connectionString);
            _tafsiliRepo = new TafsiliRepository(connectionString);
            _transactionId = transactionId;

            BuildUI();
            string title = _transactionId.HasValue ? "ویرایش تراکنش" : "ثبت پرداخت / دریافت";
            this.ApplyStandardHeader(title, new Size(850, 850));
            ShowInTaskbar = false;

            cmbDirection.SelectedIndexChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };

            rbAdmin.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };
            rbDirectPurchase.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };
            rbInformalContractor.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };
            rbContractorContract.CheckedChanged += (s, e) => { if (!_isInitializing) OnScopeChanged(); };

            cmbContractorContract.SelectedIndexChanged += (s, e) => { if (!_isInitializing) OnContractorContractSelected(); };

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            Shown += async (s, e) =>
            {
                _isInitializing = true;
                try
                {
                    await LoadCategoriesAsync();
                    await LoadCostTypesAsync();
                    await LoadAccountsAsync();
                    await LoadTafsiliAccountsAsync();
                    await LoadClientContractsAsync();
                    await LoadContractorContractsAsync();

                    if (_transactionId.HasValue)
                    {
                        await LoadTransactionDataAsync(_transactionId.Value);
                        btnAttachments.Enabled = true;
                    }
                    else
                    {
                        rbAdmin.Checked = true;
                        btnAttachments.Enabled = false;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "CashTransactionEditForm.Shown");
                    if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری:\n" + ex.Message);
                }
                finally
                {
                    _isInitializing = false;
                    OnScopeChanged();
                }
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            // ✅ پنل اسکرول‌شونده برای فیلدهای ورودی
            var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            formLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                RowCount = 9,
                Padding = new Padding(20, 15, 20, 15),
                AutoSize = true
            };

            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F)); // لیبل راست
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));   // فیلد راست
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F)); // لیبل چپ
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));   // فیلد چپ

            // مقادیر اولیه ارتفاع ردیف‌ها (0 برای ردیف‌های داینامیک)
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 0
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 1
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));  // 2: Scope Panel
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));  // 3: Client Contract
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));  // 4: Contractor Contract
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 5: CostType/Category
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 6: Tafsili/Sheba
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 7: Amount/Words
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F)); // 8: Description

            // ─── ردیف 0: حساب و جهت ───
            cmbAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id" };
            cmbDirection = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDirection.Items.AddRange(new[] { DirectionTypes.Outgoing, DirectionTypes.Incoming });
            cmbDirection.SelectedIndex = 0;

            UIHelper.AddField(formLayout, "حساب / صندوق:", cmbAccount, 0, 0, 1);
            UIHelper.AddField(formLayout, "جهت تراکنش:", cmbDirection, 0, 2, 3);

            // ─── ردیف 1: روش پرداخت و تاریخ ───
            cmbPaymentMethod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPaymentMethod.Items.AddRange(new[] { "نقدی", "حواله", "کارت به کارت", "پایا / ساتنا" });
            cmbPaymentMethod.SelectedIndex = 0;

            txtDate = new TextBox();
            DateHelper.ApplyDateMask(txtDate);
            txtDate.Text = DateHelper.ToPersianDate(DateTime.Now);

            UIHelper.AddField(formLayout, "روش پرداخت:", cmbPaymentMethod, 1, 0, 1);
            UIHelper.AddField(formLayout, "تاریخ:", txtDate, 1, 2, 3);

            // ─── ردیف 2: پنل انتخاب نوع پرداخت (Scope) ───
            pnlScopePanel = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(15, 10, 15, 10), AutoSize = true };
            pnlScopePanel.Paint += (s, e) => ControlPaint.DrawBorder(e.Graphics, pnlScopePanel.ClientRectangle, UIHelper.SectionBlue, 2, ButtonBorderStyle.Solid, UIHelper.SectionBlue, 2, ButtonBorderStyle.Solid, UIHelper.SectionBlue, 2, ButtonBorderStyle.Solid, UIHelper.SectionBlue, 2, ButtonBorderStyle.Solid);

            var scopeLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, AutoSize = true };
            scopeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            scopeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var lblScope = new Label { Text = "📌 نوع پرداخت را انتخاب کنید:", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11.5f, FontStyle.Bold), ForeColor = UIHelper.SectionBlue, TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(0, 0, 0, 8) };
            scopeLayout.Controls.Add(lblScope, 0, 0);
            scopeLayout.SetColumnSpan(lblScope, 2);

            void SetupRadio(RadioButton rb) { rb.AutoSize = true; rb.Font = UIHelper.PersianFont(11f); rb.Anchor = AnchorStyles.Right; rb.Margin = new Padding(5, 8, 5, 8); }

            rbAdmin = new RadioButton { Text = "🔹 اداری — هزینه‌های عمومی شرکت" }; SetupRadio(rbAdmin);
            rbInformalContractor = new RadioButton { Text = "👷 پیمانکار بدون قرارداد" }; SetupRadio(rbInformalContractor);
            rbDirectPurchase = new RadioButton { Text = "🛒 خرید مستقیم برای پروژه" }; SetupRadio(rbDirectPurchase);
            rbContractorContract = new RadioButton { Text = "📋 پیمانکار دارای قرارداد" }; SetupRadio(rbContractorContract);

            scopeLayout.Controls.Add(rbAdmin, 0, 1);
            scopeLayout.Controls.Add(rbInformalContractor, 0, 2);
            scopeLayout.Controls.Add(rbDirectPurchase, 1, 1);
            scopeLayout.Controls.Add(rbContractorContract, 1, 2);

            pnlScopePanel.Controls.Add(scopeLayout);
            formLayout.Controls.Add(pnlScopePanel, 0, 2);
            formLayout.SetColumnSpan(pnlScopePanel, 4);

            // ─── ردیف 3: بخش قرارداد کارفرما ───
            pnlClientContractSection = new Panel { Dock = DockStyle.Fill, Visible = false };
            var ccLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
            ccLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            ccLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            cmbClientContract = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DisplayMember = "Display", ValueMember = "Id" };
            var lblClientContract = new Label { Name = "lblClientContract", Text = "قرارداد کارفرما:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmErrorText };

            ccLayout.Controls.Add(lblClientContract, 0, 0);
            ccLayout.Controls.Add(cmbClientContract, 1, 0);
            pnlClientContractSection.Controls.Add(ccLayout);
            formLayout.Controls.Add(pnlClientContractSection, 0, 3);
            formLayout.SetColumnSpan(pnlClientContractSection, 4);

            // ─── ردیف 4: بخش قرارداد پیمانکار ───
            pnlContractorContractSection = new Panel { Dock = DockStyle.Fill, Visible = false };
            var conLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, AutoSize = true };
            conLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            conLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            cmbContractorContract = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DisplayMember = "Display", ValueMember = "Id" };
            txtAutoContractDisplay = new TextBox { ReadOnly = true, BackColor = UIHelper.WarmInputBackground, ForeColor = UIHelper.SectionBlue, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center };

            conLayout.Controls.Add(new Label { Text = "قرارداد پیمانکار (اجباری):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmErrorText }, 0, 0);
            conLayout.Controls.Add(cmbContractorContract, 1, 0);
            conLayout.Controls.Add(new Label { Text = "قرارداد کارفرما (خودکار):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(10f), ForeColor = Color.Gray }, 0, 1);
            conLayout.Controls.Add(txtAutoContractDisplay, 1, 1);
            pnlContractorContractSection.Controls.Add(conLayout);
            formLayout.Controls.Add(pnlContractorContractSection, 0, 4);
            formLayout.SetColumnSpan(pnlContractorContractSection, 4);

            // ─── ردیف 5: ماهیت هزینه و دسته‌بندی ───
            cmbCostType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            cmbCategory = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            UIHelper.AddField(formLayout, "ماهیت هزینه (اجباری):", cmbCostType, 5, 0, 1);
            UIHelper.AddField(formLayout, "دسته‌بندی حسابداری:", cmbCategory, 5, 2, 3);

            // ─── ردیف 6: تفصیلی و شبا ───
            var pnlTafsili = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            pnlTafsili.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlTafsili.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40F));

            cmbTafsili = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DisplayMember = "DisplayText", ValueMember = "Code" };
            cmbTafsili.SelectedIndexChanged += (s, e) => AutoFillSheba();

            btnSelectTafsili = new Button { Text = "🔍", Dock = DockStyle.Fill, BackColor = UIHelper.SectionBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnSelectTafsili.Click += BtnSelectTafsili_Click;

            pnlTafsili.Controls.Add(cmbTafsili, 0, 0);
            pnlTafsili.Controls.Add(btnSelectTafsili, 1, 0);

            txtRecipientSheba = new TextBox { TextAlign = HorizontalAlignment.Left, ReadOnly = true, BackColor = UIHelper.WarmInputBackground };
            UIHelper.AddField(formLayout, "حساب تفصیلی (طرف حساب):", pnlTafsili, 6, 0, 1);
            UIHelper.AddField(formLayout, "شماره شبا گیرنده:", txtRecipientSheba, 6, 2, 3);

            // ─── ردیف 7: مبلغ و حروف ───
            txtAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
            txtAmountInWords = new TextBox { ReadOnly = true, BackColor = UIHelper.WarmInputBackground, ForeColor = UIHelper.ActionGreen, Font = UIHelper.PersianFont(10f, FontStyle.Bold) };
            txtAmount.TextChanged += (s, e) => { decimal amount = NumberHelper.ParseFormatted(txtAmount.Text); txtAmountInWords.Text = amount > 0 ? NumberHelper.ToPersianWords(amount) + " ریال" : ""; };

            UIHelper.AddField(formLayout, "مبلغ پایه (ریال):", txtAmount, 7, 0, 1);
            UIHelper.AddField(formLayout, "مبلغ به حروف:", txtAmountInWords, 7, 2, 3);

            // ─── ردیف 8: توضیحات ───
            txtDescription = new TextBox { PlaceholderText = "توضیحات...", Multiline = true, Height = 50 };
            UIHelper.AddField(formLayout, "توضیحات:", txtDescription, 8, 0, 1, 3);

            scrollPanel.Controls.Add(formLayout);

            // ─── دکمه‌های پایین ───
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(15, 10, 15, 10), BackColor = UIHelper.WarmPanelBackground };

            btnSave = UIHelper.CreateButton(_transactionId.HasValue ? "💾 ذخیره تغییرات" : "💾 ثبت تراکنش", UIHelper.ActionGreen, 200, 45);
            btnSave.Click += async (s, e) => await SaveSafeAsync();

            btnAttachments = UIHelper.CreateButton("📎 پیوست اسناد (رسید/فاکتور)", UIHelper.SectionTeal, 220, 45);
            btnAttachments.Click += (s, e) =>
            {
                if (_transactionId.HasValue)
                {
                    using var form = new AttachmentsForm(_connectionString, "CashTransaction", _transactionId.Value, "تراکنش مالی");
                    form.ShowDialog(this);
                }
            };

            btnPanel.Controls.Add(btnSave);
            btnPanel.Controls.Add(btnAttachments);

            mainLayout.Controls.Add(scrollPanel, 0, 0);
            mainLayout.Controls.Add(btnPanel, 0, 1);
            this.Controls.Add(mainLayout);
        }

        private void OnScopeChanged()
        {
            if (IsDisposed) return;

            if (cmbDirection.Text != DirectionTypes.Outgoing)
            {
                if (pnlScopePanel != null)
                {
                    pnlScopePanel.Visible = false;
                    formLayout.RowStyles[2].Height = 0F;
                }

                if (pnlContractorContractSection != null)
                {
                    pnlContractorContractSection.Visible = false;
                    formLayout.RowStyles[4].Height = 0F;
                }

                if (pnlClientContractSection != null)
                {
                    pnlClientContractSection.Visible = true;
                    formLayout.RowStyles[3].Height = 45F;

                    var lbl = pnlClientContractSection.Controls[0].Controls["lblClientContract"] as Label;
                    if (lbl != null) lbl.Text = "قرارداد کارفرما (اختیاری):";

                    cmbClientContract.Enabled = true;
                }

                if (cmbCostType != null)
                {
                    cmbCostType.Enabled = false;
                    cmbCostType.SelectedItem = "سایر";
                }
                return;
            }

            if (pnlScopePanel != null)
            {
                pnlScopePanel.Visible = true;
                formLayout.RowStyles[2].Height = 140F;
            }
            if (cmbCostType != null) cmbCostType.Enabled = true;

            bool showClientContract = rbDirectPurchase.Checked || rbInformalContractor.Checked;
            bool showContractorContract = rbContractorContract.Checked;

            if (pnlClientContractSection != null)
            {
                pnlClientContractSection.Visible = showClientContract;
                formLayout.RowStyles[3].Height = showClientContract ? 45F : 0F;
                var lbl = pnlClientContractSection.Controls[0].Controls["lblClientContract"] as Label;
                if (lbl != null) lbl.Text = "قرارداد کارفرما (اجباری):";
            }
            if (pnlContractorContractSection != null)
            {
                pnlContractorContractSection.Visible = showContractorContract;
                formLayout.RowStyles[4].Height = showContractorContract ? 75F : 0F;
            }

            if (!_isInitializing)
            {
                if (rbAdmin.Checked)
                {
                    cmbCostType.DataSource = _adminCostTypes.ToList();
                    cmbCostType.SelectedItem = "سایر";
                    cmbClientContract.SelectedIndex = -1;
                    cmbContractorContract.SelectedIndex = -1;
                    txtAutoContractDisplay.Text = "";
                }
                else
                {
                    cmbCostType.DataSource = _projectCostTypes.ToList();
                    if (string.IsNullOrEmpty(cmbCostType.Text) || cmbCostType.Text == "سایر")
                        cmbCostType.SelectedItem = "خرید کالا";

                    if (rbDirectPurchase.Checked || rbInformalContractor.Checked)
                    {
                        cmbContractorContract.SelectedIndex = -1;
                        txtAutoContractDisplay.Text = "";
                    }
                }
            }
        }

        private void OnContractorContractSelected()
        {
            if (cmbContractorContract.SelectedItem is not ComboItem selected || selected.Id == 0)
            {
                txtAutoContractDisplay.Text = "";
                return;
            }

            if (selected.ClientContractId.HasValue)
            {
                foreach (ComboItem item in cmbClientContract.Items)
                {
                    if (item.Id == selected.ClientContractId.Value)
                    {
                        txtAutoContractDisplay.Text = item.Display;
                        break;
                    }
                }
            }
            else
            {
                txtAutoContractDisplay.Text = "--- (بدون ارتباط) ---";
            }

            TryAutoFillRecipientFromContractor(selected.PartyId);
        }

        private void TryAutoFillRecipientFromContractor(int partyId)
        {
            Task.Run(() =>
            {
                try
                {
                    using var conn = new SqlConnection(_connectionString);
                    conn.Open();
                    using var cmd = new SqlCommand("SELECT NationalCode, NationalId, PartyType, FirstName, LastName, CompanyName FROM Parties WHERE Id = @id", conn);
                    cmd.Parameters.AddWithValue("@id", partyId);
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        string? nc = r.IsDBNull(0) ? null : r.GetString(0);
                        string? nid = r.IsDBNull(1) ? null : r.GetString(1);
                        string type = r.IsDBNull(2) ? "" : r.GetString(2);
                        string name = type == "حقیقی" ? $"{r["FirstName"]} {r["LastName"]}".Trim() : r["CompanyName"].ToString();
                        return (nc, nid, type, name);
                    }
                    return (null, null, "", "");
                }
                catch { return (null, null, "", ""); }
            }).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && !IsDisposed)
                {
                    this.Invoke(new Action(() =>
                    {
                        var (nc, nid, type, name) = t.Result;
                        if (!string.IsNullOrEmpty(name))
                        {
                            var match = cmbTafsili.Items.Cast<Tafsili>().FirstOrDefault(x => x.Name == name);
                            if (match != null) cmbTafsili.SelectedValue = match.Code;
                        }
                    }));
                }
            });
        }

        private void AutoFillSheba()
        {
            string code = cmbTafsili.SelectedValue?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(code) && _tafsiliShebaDict.TryGetValue(code, out string? sheba))
                txtRecipientSheba.Text = sheba;
            else
                txtRecipientSheba.Text = "";
        }

        private void BtnSelectTafsili_Click(object? sender, EventArgs e)
        {
            using var form = new TafsiliSelectionForm(_connectionString);
            if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed)
            {
                cmbTafsili.SelectedValue = form.SelectedCode;
            }
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var items = await _lookupRepo.GetByCategoryAsync("TransactionCategory");
                if (IsDisposed) return;
                var categories = items.Select(i => i.Value).Where(c => c.Trim() != "انتقال بین حساب‌ها").ToList();
                if (categories.Count == 0) categories.AddRange(new[] { "عملیاتی", "اداری", "پرسنلی", "هزینه بانکی", "سایر" });
                cmbCategory.DataSource = categories;
            }
            catch { }
        }

        private async Task LoadCostTypesAsync()
        {
            try
            {
                var projectItems = await _lookupRepo.GetByCategoryAsync("CostType");
                _projectCostTypes = projectItems.Select(i => i.Value).ToList();
                if (_projectCostTypes.Count == 0) _projectCostTypes.AddRange(new[] { "خرید کالا", "خرید خدمات", "سایر" });

                var adminItems = await _lookupRepo.GetByCategoryAsync("AdministrativeCostType");
                _adminCostTypes = adminItems.Select(i => i.Value).ToList();
                if (_adminCostTypes.Count == 0) _adminCostTypes.AddRange(new[] { "اجاره", "آب و برق", "تلفن و اینترنت", "لوازم مصرفی", "پذیرایی", "حقوق و دستمزد", "حمل و نقل", "تعمیر و نگهداری", "سایر" });
            }
            catch { }
        }

        private async Task LoadAccountsAsync()
        {
            try
            {
                var accounts = await _bankRepo.GetActiveAsync();
                if (IsDisposed) return;
                cmbAccount.DataSource = accounts;
                if (cmbAccount.Items.Count > 0) cmbAccount.SelectedIndex = 0;
            }
            catch { }
        }

        private async Task LoadTafsiliAccountsAsync()
        {
            try
            {
                var tafsilis = await _tafsiliRepo.GetAllAsync();
                if (IsDisposed) return;

                _tafsiliShebaDict.Clear();
                foreach (var t in tafsilis)
                {
                    if (!string.IsNullOrWhiteSpace(t.ShebaNumber) && !_tafsiliShebaDict.ContainsKey(t.Code))
                        _tafsiliShebaDict[t.Code] = t.ShebaNumber;
                }

                var list = new List<Tafsili>();
                list.Add(new Tafsili { Id = 0, Code = "--- بدون تفصیلی ---", FirstName = "", LastName = "", PersonType = "شخص حقیقی" });
                list.AddRange(tafsilis);

                cmbTafsili.DataSource = list;
                cmbTafsili.DisplayMember = "DisplayText";
                cmbTafsili.ValueMember = "Code";
            }
            catch { }
        }

        private async Task LoadClientContractsAsync()
        {
            try
            {
                var cRepo = new ClientContractRepository(_connectionString);
                var cList = await Task.Run(() => cRepo.GetAll());
                if (IsDisposed) return;
                cmbClientContract.DataSource = cList.Select(c => new ComboItem { Id = c.Id, Display = $"{c.ContractNumber} - {c.Subject}" }).ToList();
            }
            catch { }
        }

        private async Task LoadContractorContractsAsync()
        {
            try
            {
                var conRepo = new ContractorContractRepository(_connectionString);
                var conList = await Task.Run(() => conRepo.GetAll());
                if (IsDisposed) return;

                var conItems = new List<ComboItem> { new() { Id = 0, Display = "--- انتخاب کنید ---" } };
                conItems.AddRange(conList.Select(c => new ComboItem
                {
                    Id = c.Id,
                    Display = $"{c.DisplayName} - قرارداد {c.ContractNumber}",
                    ClientContractId = c.ClientContractId,
                    PartyId = c.PartyId
                }));
                cmbContractorContract.DataSource = conItems;
            }
            catch { }
        }

        private async Task LoadTransactionDataAsync(int id)
        {
            var trans = await _repository.GetByIdAsync(id);
            if (trans == null || IsDisposed) { this.Close(); return; }

            _originalDiscount = trans.Discount;
            _originalShipping = trans.Shipping;
            _originalVAT = trans.VAT;
            _originalAccountId = trans.AccountId;

            if (trans.AccountId.HasValue)
            {
                cmbAccount.SelectedValue = trans.AccountId.Value;
                bool selected = cmbAccount.SelectedValue != null && Convert.ToInt32(cmbAccount.SelectedValue) == trans.AccountId.Value;

                if (!selected)
                {
                    var allAccounts = await _bankRepo.GetAllAsync();
                    var storedAcc = allAccounts.FirstOrDefault(a => a.Id == trans.AccountId.Value);

                    CompanyBankAccount displayEntry;
                    if (storedAcc != null)
                        displayEntry = new CompanyBankAccount { Id = storedAcc.Id, Title = $"{storedAcc.Title} (غیرفعال)", BankName = storedAcc.BankName, AccountNumber = storedAcc.AccountNumber, IsActive = false, IsCashDesk = storedAcc.IsCashDesk };
                    else
                        displayEntry = new CompanyBankAccount { Id = trans.AccountId.Value, Title = $"❌ حساب حذف‌شده (#{trans.AccountId.Value})", BankName = "---", AccountNumber = "---", IsActive = false, IsCashDesk = false };

                    var currentList = new List<CompanyBankAccount>();
                    if (cmbAccount.DataSource is IEnumerable<CompanyBankAccount> existing) currentList.AddRange(existing);
                    currentList.Add(displayEntry);

                    cmbAccount.DataSource = null;
                    cmbAccount.DataSource = currentList;
                    cmbAccount.SelectedValue = displayEntry.Id;

                    ModernMessageBox.ShowWarning($"حساب بانکی اصلی این تراکنش {(storedAcc != null ? "غیرفعال شده است" : "از سیستم حذف شده است")}.\n\nحساب نمایش‌داده‌شده: {displayEntry.Title}\n\nاگر بدون تغییر حساب ذخیره کنید، تراکنش روی همان حساب اصلی باقی می‌ماند.");
                }
            }

            cmbDirection.SelectedItem = trans.Direction;
            cmbPaymentMethod.SelectedItem = trans.TransactionType;
            cmbCategory.SelectedItem = trans.Category;
            txtDate.Text = DateHelper.ToPersianDate(trans.TransactionDate);
            txtAmount.Text = NumberHelper.FormatThousands(trans.BaseAmount, usePersianDigits: true);
            txtDescription.Text = trans.Description;

            if (!string.IsNullOrWhiteSpace(trans.TafsiliCode)) cmbTafsili.SelectedValue = trans.TafsiliCode.Trim();
            else cmbTafsili.SelectedIndex = 0;

            string scope = trans.PaymentScope;
            if (string.IsNullOrWhiteSpace(scope))
            {
                if (trans.ContractorContractId.HasValue) scope = PaymentScopes.ContractorContract;
                else if (trans.ContractId.HasValue) scope = PaymentScopes.DirectPurchase;
                else scope = PaymentScopes.Administrative;
            }

            if (scope == PaymentScopes.DirectPurchase) rbDirectPurchase.Checked = true;
            else if (scope == PaymentScopes.InformalContractor) rbInformalContractor.Checked = true;
            else if (scope == PaymentScopes.ContractorContract) rbContractorContract.Checked = true;
            else rbAdmin.Checked = true;

            if (!string.IsNullOrWhiteSpace(trans.CostType)) cmbCostType.SelectedItem = trans.CostType;
            if (trans.ContractId.HasValue) SelectClientContract(trans.ContractId.Value);
            if (trans.ContractorContractId.HasValue) cmbContractorContract.SelectedValue = trans.ContractorContractId.Value;

            OnScopeChanged();
        }

        private void SelectClientContract(int id)
        {
            if (cmbClientContract.Items.Count == 0) return;
            for (int i = 0; i < cmbClientContract.Items.Count; i++)
            {
                if (cmbClientContract.Items[i] is ComboItem item && item.Id == id)
                {
                    cmbClientContract.SelectedIndex = i;
                    return;
                }
            }
        }

        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (cmbAccount.SelectedValue == null) { ModernMessageBox.ShowWarning("حساب/صندوق را انتخاب کنید."); return; }

                decimal baseAmount = NumberHelper.ParseFormatted(txtAmount.Text);
                if (baseAmount <= 0) { ModernMessageBox.ShowWarning("مبلغ معتبر نیست."); return; }
                DateTime? date = DateHelper.ParsePersianDate(txtDate.Text);
                if (date == null) { ModernMessageBox.ShowWarning("تاریخ معتبر نیست."); return; }

                string paymentScope = PaymentScopes.Administrative;
                int? clientContractId = null;
                int? contractorContractId = null;
                string contractTitle = "هزینه‌های اداری / متفرقه";
                string costType = cmbCostType.Text;

                if (cmbDirection.Text == DirectionTypes.Outgoing)
                {
                    if (string.IsNullOrWhiteSpace(costType)) { ModernMessageBox.ShowWarning("ماهیت هزینه را انتخاب کنید."); return; }

                    if (rbAdmin.Checked)
                    {
                        paymentScope = PaymentScopes.Administrative;
                        contractTitle = "هزینه‌های اداری / متفرقه";
                    }
                    else if (rbDirectPurchase.Checked || rbInformalContractor.Checked)
                    {
                        paymentScope = rbDirectPurchase.Checked ? PaymentScopes.DirectPurchase : PaymentScopes.InformalContractor;
                        if (cmbClientContract.SelectedItem is not ComboItem clientItem || clientItem.Id <= 0) { ModernMessageBox.ShowWarning("لطفاً قرارداد کارفرما را انتخاب کنید."); return; }
                        clientContractId = clientItem.Id;
                        contractTitle = clientItem.Display;
                    }
                    else
                    {
                        paymentScope = PaymentScopes.ContractorContract;
                        if (cmbContractorContract.SelectedItem is not ComboItem conItem || conItem.Id <= 0) { ModernMessageBox.ShowWarning("لطفاً قرارداد پیمانکار را انتخاب کنید."); return; }
                        contractorContractId = conItem.Id;
                        clientContractId = conItem.ClientContractId;
                        contractTitle = conItem.Display;
                    }
                }
                else
                {
                    paymentScope = null;
                    costType = "سایر";

                    if (cmbClientContract.SelectedItem is ComboItem clientItem && clientItem.Id > 0)
                    {
                        clientContractId = clientItem.Id;
                        contractTitle = clientItem.Display;
                    }
                    else
                    {
                        contractTitle = "دریافت متفرقه / بدون قرارداد";
                    }
                }

                var selectedAcc = (CompanyBankAccount)cmbAccount.SelectedItem!;
                string tafsiliCode = cmbTafsili.SelectedValue?.ToString() ?? "";
                string tafsiliTitle = cmbTafsili.Text;

                decimal finalAmount = baseAmount + _originalShipping - _originalDiscount + _originalVAT;

                var trans = new CashTransaction
                {
                    Id = _transactionId ?? 0,
                    PaymentScope = paymentScope,
                    ContractId = clientContractId,
                    ContractorContractId = contractorContractId,
                    ContractTitle = contractTitle,
                    TransactionDate = date.Value,
                    Amount = finalAmount,
                    Discount = _originalDiscount,
                    Shipping = _originalShipping,
                    VAT = _originalVAT,
                    TransactionType = cmbPaymentMethod.Text,
                    Direction = cmbDirection.Text,
                    AccountId = selectedAcc.Id,
                    AccountTitle = selectedAcc.Title,
                    Category = cmbCategory.Text,
                    CostType = costType,
                    TafsiliCode = string.IsNullOrWhiteSpace(tafsiliCode) ? null : tafsiliCode,
                    TafsiliTitle = string.IsNullOrWhiteSpace(tafsiliTitle) ? null : tafsiliTitle,
                    RecipientName = string.IsNullOrWhiteSpace(tafsiliTitle) ? null : tafsiliTitle,
                    RecipientSheba = string.IsNullOrWhiteSpace(txtRecipientSheba.Text) ? null : txtRecipientSheba.Text.Trim(),
                    Description = txtDescription.Text.Trim()
                };

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    if (_transactionId.HasValue)
                        await _repository.UpdateAsync(trans);
                    else
                        await _repository.AddAsync(trans);
                }, "در حال ذخیره...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("تراکنش با موفقیت ذخیره شد.", true);
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "CashTransactionEditForm.SaveSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }
    }
}