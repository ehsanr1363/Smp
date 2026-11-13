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
    public class CashCheckEditForm : Form
    {
        private readonly CashCheckRepository _repository;
        private readonly BankAccountRepository _bankRepo;
        private readonly LookupRepository _lookupRepo;
        private readonly TafsiliRepository _tafsiliRepo;
        private readonly string _connectionString;

        private TextBox txtCheckNumber = null!, txtBankName = null!, txtCheckbookInfo = null!;
        private TextBox txtSayadPrefix = null!, txtSayadSequence = null!;
        private TextBox txtDueDate = null!, txtAmount = null!, txtDescription = null!;
        private ComboBox cmbType = null!, cmbAccount = null!;

        private Panel pnlScopePanel = null!;
        private RadioButton rbAdmin = null!, rbDirectPurchase = null!, rbInformalContractor = null!, rbContractorContract = null!;
        private ComboBox cmbClientContract = null!, cmbContractorContract = null!;
        private TextBox txtAutoContractDisplay = null!;
        private Panel pnlClientContract = null!, pnlContractorContract = null!;
        private Panel pnlCostType = null!;

        private ComboBox cmbCostType = null!;
        private TextBox txtRecipientName = null!, txtRecipientId = null!;
        private Button btnSelectRecipient = null!;
        private CheckBox chkLegalEntity = null!;
        private TextBox txtAmountInWords = null!;
        private Button btnSave = null!, btnAttachments = null!;

        private bool _isInitializing = false;
        private int? _checkId = null;
        private int? _originalAccountId = null;

        private readonly SemaphoreSlim _actionLock = new(1, 1);
        private TableLayoutPanel formLayout = null!;

        private class ComboItem
        {
            public int Id { get; set; }
            public string Display { get; set; } = "";
            public int? ClientContractId { get; set; }
            public int PartyId { get; set; }
        }

        public CashCheckEditForm(string connectionString, int? checkId = null)
        {
            _connectionString = connectionString;
            _repository = new CashCheckRepository(connectionString);
            _bankRepo = new BankAccountRepository(connectionString);
            _lookupRepo = new LookupRepository(connectionString);
            _tafsiliRepo = new TafsiliRepository(connectionString);
            _checkId = checkId;

            BuildUI();
            string title = _checkId.HasValue ? "ویرایش چک" : "ثبت چک جدید";
            this.ApplyStandardHeader(title, new Size(950, 850));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            cmbType.SelectedIndexChanged += async (s, e) =>
            {
                if (_isInitializing) return;
                try { await OnCheckTypeChangedAsync(); }
                catch (Exception ex) { LogHelper.LogException(ex, "CashCheckEditForm.OnCheckTypeChanged"); }
            };
            cmbAccount.SelectedIndexChanged += async (s, e) =>
            {
                if (_isInitializing) return;
                try { await OnAccountSelectedAsync(); }
                catch (Exception ex) { LogHelper.LogException(ex, "CashCheckEditForm.OnAccountSelected"); }
            };

            rbAdmin.CheckedChanged += (s, e) => OnScopeChanged();
            rbDirectPurchase.CheckedChanged += (s, e) => OnScopeChanged();
            rbInformalContractor.CheckedChanged += (s, e) => OnScopeChanged();
            rbContractorContract.CheckedChanged += (s, e) => OnScopeChanged();

            cmbContractorContract.SelectedIndexChanged += (s, e) => OnContractorContractSelected();

            Shown += async (s, e) =>
            {
                try
                {
                    await LoadCostTypesAsync();
                    await LoadContractsAsync();
                    await ReloadAccountsAsync();
                    if (_checkId.HasValue)
                    {
                        await LoadCheckDataAsync(_checkId.Value);
                        btnAttachments.Enabled = true;
                    }
                    else
                    {
                        await OnCheckTypeChangedAsync();
                        btnAttachments.Enabled = false;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "CashCheckEditForm.Shown");
                    if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری فرم:\n" + ex.Message);
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
            // ═══════════ الگوی استاندارد: لایه اصلی ═══════════
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F)); // نوار دکمه‌ها

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

            // ═══════════ الگوی استاندارد: لایه فرم ═══════════
            formLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                AutoSize = true,
                Padding = new Padding(20, 15, 20, 15),
                RightToLeft = RightToLeft.Yes
            };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F)); // Label 1
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));   // Field 1
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F)); // Label 2
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));   // Field 2

            // ✅ تعریف دقیق تمام ردیف‌ها با ارتفاع ثابت (کلید حل مشکل کشیدگی)
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 0: نوع چک و حساب
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 1: مشخصات دسته چک
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 2: شماره چک و تاریخ
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 3: صیاد و بانک
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 4: مبلغ
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 5: نام گیرنده
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F)); // 6: کد ملی گیرنده
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));  // 7: پنل نوع پرداخت
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));  // 8: قرارداد کارفرما
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));  // 9: قرارداد پیمانکار
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));  // 10: ماهیت هزینه
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F)); // 11: توضیحات

            // ─── ردیف ۰ ───
            cmbType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbType.Items.AddRange(new[] { DirectionTypes.Incoming, DirectionTypes.Outgoing });
            cmbType.SelectedIndex = 0;
            cmbAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, ValueMember = "Id", FormattingEnabled = true };
            cmbAccount.Format += (s, e) => { if (e.ListItem is CompanyBankAccount acc) e.Value = $"{acc.BankName} - {acc.AccountNumber}"; };

            UIHelper.AddField(formLayout, "نوع چک:", cmbType, 0, 0, 1);
            UIHelper.AddField(formLayout, "حساب بانکی:", cmbAccount, 0, 2, 3);

            // ─── ردیف ۱ ───
            txtCheckbookInfo = new TextBox { ReadOnly = true, BackColor = UIHelper.WarmWarningBackground, ForeColor = UIHelper.WarmWarningText, Font = UIHelper.PersianFont(11f, FontStyle.Bold), TextAlign = HorizontalAlignment.Center };
            UIHelper.AddField(formLayout, "مشخصات دسته چک:", txtCheckbookInfo, 1, 0, 1, 3);

            // ─── ردیف ۲ ───
            txtCheckNumber = new TextBox();
            txtDueDate = new TextBox(); DateHelper.ApplyDateMask(txtDueDate); txtDueDate.Text = DateHelper.ToPersianDate(DateTime.Now);
            UIHelper.AddField(formLayout, "شماره چک:", txtCheckNumber, 2, 0, 1);
            UIHelper.AddField(formLayout, "تاریخ سررسید:", txtDueDate, 2, 2, 3);

            // ─── ردیف ۳ ───
            var pnlSayad = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0), RightToLeft = RightToLeft.Yes };
            pnlSayad.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            pnlSayad.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            pnlSayad.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            pnlSayad.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // فضای خالی

            txtSayadPrefix = new TextBox { Dock = DockStyle.Fill, MaxLength = 4, TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(11f), Margin = new Padding(0) };
            var lblDash = new Label { Text = "-", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Tahoma", 12, FontStyle.Bold) };
            txtSayadSequence = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, TextAlign = HorizontalAlignment.Center, BackColor = UIHelper.WarmInputBackground, Font = UIHelper.PersianFont(11f), Margin = new Padding(0) };

            pnlSayad.Controls.Add(txtSayadPrefix, 0, 0);
            pnlSayad.Controls.Add(lblDash, 1, 0);
            pnlSayad.Controls.Add(txtSayadSequence, 2, 0);

            txtBankName = new TextBox();
            UIHelper.AddField(formLayout, "شناسه صیاد:", pnlSayad, 3, 0, 1);
            UIHelper.AddField(formLayout, "بانک صادرکننده:", txtBankName, 3, 2, 3);

            // ─── ردیف ۴ ───
            txtAmount = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
            txtAmount.TextChanged += (s, e) => { decimal amount = NumberHelper.ParseFormatted(txtAmount.Text); txtAmountInWords.Text = amount > 0 ? NumberHelper.ToPersianWords(amount) + " ریال" : ""; };
            txtAmountInWords = new TextBox { ReadOnly = true, BackColor = UIHelper.WarmInputBackground, ForeColor = UIHelper.ActionGreen, Font = UIHelper.PersianFont(10f, FontStyle.Bold) };

            UIHelper.AddField(formLayout, "مبلغ (ریال):", txtAmount, 4, 0, 1);
            UIHelper.AddField(formLayout, "مبلغ به حروف:", txtAmountInWords, 4, 2, 3);

            // ─── ردیف ۵ ───
            var pnlRecipient = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0), RightToLeft = RightToLeft.Yes };
            pnlRecipient.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F)); // TextBox
            pnlRecipient.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40F));  // Button
            pnlRecipient.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // Empty Space

            txtRecipientName = new TextBox { ReadOnly = true, BackColor = UIHelper.WarmInputBackground, TextAlign = HorizontalAlignment.Center, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 5, 0) };
            btnSelectRecipient = new Button { Text = "🔍", Dock = DockStyle.Fill, BackColor = UIHelper.SectionBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Margin = new Padding(0), Cursor = Cursors.Hand };
            btnSelectRecipient.FlatAppearance.BorderSize = 0;
            btnSelectRecipient.Click += BtnSelectRecipient_Click;

            pnlRecipient.Controls.Add(txtRecipientName, 0, 0);
            pnlRecipient.Controls.Add(btnSelectRecipient, 1, 0);
            UIHelper.AddField(formLayout, "نام گیرنده چک:", pnlRecipient, 5, 0, 1, 3);

            // ─── ردیف ۶ ───
            var pnlRecipientId = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0), RightToLeft = RightToLeft.Yes };
            pnlRecipientId.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F)); // TextBox
            pnlRecipientId.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F)); // CheckBox
            pnlRecipientId.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // Empty Space

            txtRecipientId = new TextBox { Dock = DockStyle.Fill, MaxLength = 10, TextAlign = HorizontalAlignment.Center, Margin = new Padding(0, 0, 5, 0) };
            chkLegalEntity = new CheckBox { Text = "شخص حقوقی", Dock = DockStyle.Fill, AutoSize = true, Font = UIHelper.PersianFont(10f), Margin = new Padding(5, 0, 0, 0) };
            chkLegalEntity.CheckedChanged += (s, e) => { if (chkLegalEntity.Checked) { chkLegalEntity.Text = "شخص حقوقی (۱۱ رقمی)"; txtRecipientId.MaxLength = 11; } else { chkLegalEntity.Text = "شخص حقیقی (۱۰ رقمی)"; txtRecipientId.MaxLength = 10; } if (!_checkId.HasValue && !_isInitializing) txtRecipientId.Text = ""; };

            pnlRecipientId.Controls.Add(txtRecipientId, 0, 0);
            pnlRecipientId.Controls.Add(chkLegalEntity, 1, 0);
            UIHelper.AddField(formLayout, "کد/شناسه ملی گیرنده:", pnlRecipientId, 6, 0, 1, 3);

            // ─── ردیف ۷: پنل نوع پرداخت ───
            pnlScopePanel = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(15, 10, 15, 10), Visible = false };
            pnlScopePanel.Paint += (s, e) => ControlPaint.DrawBorder(e.Graphics, pnlScopePanel.ClientRectangle, UIHelper.SectionBlue, 2, ButtonBorderStyle.Solid, UIHelper.SectionBlue, 2, ButtonBorderStyle.Solid, UIHelper.SectionBlue, 2, ButtonBorderStyle.Solid, UIHelper.SectionBlue, 2, ButtonBorderStyle.Solid);

            var scopeLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Margin = new Padding(0), RightToLeft = RightToLeft.Yes };
            scopeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); scopeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var lblScope = new Label { Text = "📌 بابت چه کاری است؟", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11.5f, FontStyle.Bold), ForeColor = UIHelper.SectionBlue, TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(0, 0, 0, 8) };
            scopeLayout.Controls.Add(lblScope, 0, 0); scopeLayout.SetColumnSpan(lblScope, 2);

            void SetupRadio(RadioButton rb) { rb.AutoSize = true; rb.Font = UIHelper.PersianFont(11f); rb.Anchor = AnchorStyles.Right; rb.Margin = new Padding(5, 8, 5, 8); }
            rbAdmin = new RadioButton { Text = "🔹 اداری — هزینه‌های عمومی شرکت" }; SetupRadio(rbAdmin);
            rbInformalContractor = new RadioButton { Text = "👷 پیمانکار بدون قرارداد" }; SetupRadio(rbInformalContractor);
            rbDirectPurchase = new RadioButton { Text = "🛒 خرید مستقیم برای پروژه" }; SetupRadio(rbDirectPurchase);
            rbContractorContract = new RadioButton { Text = "📋 پیمانکار دارای قرارداد" }; SetupRadio(rbContractorContract);

            scopeLayout.Controls.Add(rbAdmin, 0, 1); scopeLayout.Controls.Add(rbInformalContractor, 0, 2);
            scopeLayout.Controls.Add(rbDirectPurchase, 1, 1); scopeLayout.Controls.Add(rbContractorContract, 1, 2);
            pnlScopePanel.Controls.Add(scopeLayout);

            formLayout.Controls.Add(pnlScopePanel, 0, 7);
            formLayout.SetColumnSpan(pnlScopePanel, 4);

            // ─── ردیف ۸: قرارداد کارفرما ───
            pnlClientContract = new Panel { Dock = DockStyle.Fill, Visible = false };
            var ccLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), RightToLeft = RightToLeft.Yes };
            ccLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F)); ccLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            cmbClientContract = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DisplayMember = "Display", ValueMember = "Id" };
            var ccLabel = new Label { Text = "قرارداد کارفرما:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmErrorText };
            ccLayout.Controls.Add(ccLabel, 0, 0); ccLayout.Controls.Add(cmbClientContract, 1, 0);
            pnlClientContract.Controls.Add(ccLayout);
            formLayout.Controls.Add(pnlClientContract, 0, 8); formLayout.SetColumnSpan(pnlClientContract, 4);

            // ─── ردیف ۹: قرارداد پیمانکار ───
            pnlContractorContract = new Panel { Dock = DockStyle.Fill, Visible = false };
            var conLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(0), RightToLeft = RightToLeft.Yes };
            conLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F)); conLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            cmbContractorContract = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DisplayMember = "Display", ValueMember = "Id" };
            txtAutoContractDisplay = new TextBox { ReadOnly = true, BackColor = UIHelper.WarmInputBackground, ForeColor = UIHelper.SectionBlue, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center };

            conLayout.Controls.Add(new Label { Text = "قرارداد پیمانکار:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmErrorText }, 0, 0);
            conLayout.Controls.Add(cmbContractorContract, 1, 0);
            conLayout.Controls.Add(new Label { Text = "کارفرمای مرتبط:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(10f), ForeColor = Color.Gray }, 0, 1);
            conLayout.Controls.Add(txtAutoContractDisplay, 1, 1);
            pnlContractorContract.Controls.Add(conLayout);
            formLayout.Controls.Add(pnlContractorContract, 0, 9); formLayout.SetColumnSpan(pnlContractorContract, 4);

            // ─── ردیف ۱۰: ماهیت هزینه ───
            pnlCostType = new Panel { Dock = DockStyle.Fill, Visible = false };
            var costLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), RightToLeft = RightToLeft.Yes };
            costLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F)); costLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            cmbCostType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f) };
            costLayout.Controls.Add(new Label { Text = "ماهیت هزینه:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmErrorText }, 0, 0);
            costLayout.Controls.Add(cmbCostType, 1, 0);
            pnlCostType.Controls.Add(costLayout);
            formLayout.Controls.Add(pnlCostType, 0, 10); formLayout.SetColumnSpan(pnlCostType, 4);

            // ─── ردیف ۱۱: توضیحات ───
            txtDescription = new TextBox { Multiline = true, Dock = DockStyle.Fill };
            UIHelper.AddField(formLayout, "توضیحات:", txtDescription, 11, 0, 1, 3);

            scroll.Controls.Add(formLayout);

            // ═══════════ الگوی استاندارد: دکمه‌های پایین ═══════════
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(15, 10, 15, 10), BackColor = UIHelper.WarmPanelBackground };

            btnSave = UIHelper.CreateButton(_checkId.HasValue ? "💾 ذخیره تغییرات" : "💾 ثبت چک", UIHelper.ActionGreen, 200, 45);
            btnSave.Click += async (s, e) => await SaveSafeAsync();

            btnAttachments = UIHelper.CreateButton("📎 پیوست اسناد (تصویر چک/فاکتور)", UIHelper.SectionTeal, 230, 45);
            btnAttachments.Click += (s, e) => { if (_checkId.HasValue) { using var form = new AttachmentsForm(_connectionString, "CashCheck", _checkId.Value, "چک بانکی"); form.ShowDialog(this); } };

            btnPanel.Controls.Add(btnSave);
            btnPanel.Controls.Add(btnAttachments);

            mainLayout.Controls.Add(scroll, 0, 0);
            mainLayout.Controls.Add(btnPanel, 0, 1);
            this.Controls.Add(mainLayout);
        }

        private async Task OnCheckTypeChangedAsync()
        {
            if (_checkId.HasValue) return;
            bool isPayout = cmbType.Text == DirectionTypes.Outgoing;

            if (isPayout)
            {
                formLayout.RowStyles[7].Height = 140F;
                formLayout.RowStyles[10].Height = 45F;
            }
            else
            {
                formLayout.RowStyles[7].Height = 0F;
                formLayout.RowStyles[8].Height = 45F;
                formLayout.RowStyles[9].Height = 0F;
                formLayout.RowStyles[10].Height = 0F;
            }

            await ReloadAccountsAsync();
        }

        private void OnScopeChanged()
        {
            if (cmbType.Text != DirectionTypes.Outgoing) return;

            bool showClientContract = rbDirectPurchase.Checked || rbInformalContractor.Checked;
            bool showContractorContract = rbContractorContract.Checked;

            formLayout.RowStyles[8].Height = showClientContract ? 45F : 0F;
            formLayout.RowStyles[9].Height = showContractorContract ? 75F : 0F;
            formLayout.RowStyles[10].Height = (showClientContract || showContractorContract) ? 45F : 0F;

            if (rbAdmin.Checked)
            {
                cmbClientContract.SelectedIndex = -1;
                cmbContractorContract.SelectedIndex = -1;
                txtAutoContractDisplay.Text = "";
                cmbCostType.SelectedItem = "سایر";
            }
            else if (rbDirectPurchase.Checked || rbInformalContractor.Checked)
            {
                cmbContractorContract.SelectedIndex = -1;
                txtAutoContractDisplay.Text = "";
                cmbCostType.SelectedItem = "خرید کالا";
            }
            else
            {
                cmbCostType.SelectedItem = "خرید کالا";
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
                            txtRecipientName.Text = name;
                            if (type.Contains("حقوقی"))
                            {
                                chkLegalEntity.Checked = true;
                                txtRecipientId.Text = nid ?? "";
                            }
                            else
                            {
                                chkLegalEntity.Checked = false;
                                txtRecipientId.Text = nc ?? "";
                            }
                        }
                    }));
                }
            });
        }

        private void BtnSelectRecipient_Click(object? sender, EventArgs e)
        {
            using var form = new TafsiliSelectionForm(_connectionString);
            if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed)
            {
                txtRecipientName.Text = form.SelectedName;
                Task.Run(async () =>
                {
                    var allTafsilis = await _tafsiliRepo.GetAllAsync();
                    return allTafsilis.FirstOrDefault(t => t.Code == form.SelectedCode);
                }).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully && t.Result != null && !IsDisposed)
                    {
                        this.Invoke(new Action(() =>
                        {
                            _isInitializing = true;
                            try
                            {
                                if (t.Result.PersonType == "حقوقی" || t.Result.PersonType == "شخص حقوقی")
                                {
                                    chkLegalEntity.Checked = true; txtRecipientId.Text = t.Result.NationalId ?? "";
                                }
                                else
                                {
                                    chkLegalEntity.Checked = false; txtRecipientId.Text = t.Result.NationalCode ?? "";
                                }
                            }
                            finally { _isInitializing = false; }
                        }));
                    }
                });
            }
        }

        private async Task LoadCostTypesAsync()
        {
            try
            {
                var items = await _lookupRepo.GetByCategoryAsync("CostType");
                if (IsDisposed) return;
                var costTypes = items.Select(i => i.Value).ToList();
                if (costTypes.Count == 0) costTypes.AddRange(new[] { "خرید کالا", "خرید خدمات", "سایر" });
                cmbCostType.DataSource = costTypes;
                if (cmbCostType.Items.Count > 0) cmbCostType.SelectedIndex = 0;
            }
            catch (Exception ex) { LogHelper.LogException(ex, "CashCheckEditForm.LoadCostTypes"); }
        }

        private async Task LoadContractsAsync()
        {
            try
            {
                var cRepo = new ClientContractRepository(_connectionString);
                var cList = await Task.Run(() => cRepo.GetAll());
                cmbClientContract.DataSource = cList.Select(c => new ComboItem { Id = c.Id, Display = $"{c.ContractNumber} - {c.Subject}" }).ToList();

                var conRepo = new ContractorContractRepository(_connectionString);
                var conList = await Task.Run(() => conRepo.GetAll());
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
            catch (Exception ex) { LogHelper.LogException(ex, "CashCheckEditForm.LoadContracts"); }
        }

        private async Task LoadCheckDataAsync(int id)
        {
            var check = await _repository.GetByIdAsync(id);
            if (check == null || IsDisposed)
            {
                if (!IsDisposed) ModernMessageBox.ShowError("چک یافت نشد.");
                this.Close();
                return;
            }

            _isInitializing = true;
            try
            {
                _originalAccountId = check.AccountId;
                cmbType.SelectedItem = check.CheckType;

                bool accountFound = false;
                if (check.AccountId.HasValue)
                {
                    for (int i = 0; i < cmbAccount.Items.Count; i++)
                    {
                        if (cmbAccount.Items[i] is CompanyBankAccount acc && acc.Id == check.AccountId.Value)
                        {
                            cmbAccount.SelectedIndex = i; accountFound = true; break;
                        }
                    }
                }

                if (check.AccountId.HasValue && !accountFound)
                {
                    var allAccounts = await _bankRepo.GetAllAsync();
                    var storedAcc = allAccounts.FirstOrDefault(a => a.Id == check.AccountId.Value);

                    if (storedAcc != null)
                    {
                        var displayAcc = new CompanyBankAccount { Id = storedAcc.Id, Title = $"{storedAcc.Title} (غیرفعال)", BankName = storedAcc.BankName, AccountNumber = storedAcc.AccountNumber, IsActive = false, IsCashDesk = storedAcc.IsCashDesk };
                        var currentList = (cmbAccount.DataSource as List<CompanyBankAccount>) ?? cmbAccount.Items.Cast<CompanyBankAccount>().ToList();
                        currentList.Add(displayAcc);
                        cmbAccount.DataSource = null; cmbAccount.DataSource = currentList; cmbAccount.SelectedValue = displayAcc.Id;
                        txtCheckbookInfo.Text = "⚠️ حساب اصلی غیرفعال شده است"; txtCheckbookInfo.ForeColor = UIHelper.WarmDanger;
                    }
                    else
                    {
                        txtCheckbookInfo.Text = "❌ حساب بانکی یافت نشد."; txtCheckbookInfo.ForeColor = UIHelper.WarmDanger;
                    }
                }

                txtCheckNumber.Text = check.CheckNumber;
                txtBankName.Text = check.BankName;
                txtDueDate.Text = DateHelper.ToPersianDate(check.DueDate);
                txtAmount.Text = check.Amount.ToString("N0");
                txtDescription.Text = check.Description;
                txtRecipientName.Text = check.RecipientName;
                txtRecipientId.Text = check.RecipientNationalId;
                chkLegalEntity.Checked = check.IsLegalEntity;

                if (!string.IsNullOrEmpty(check.SayadNumber) && check.SayadNumber.Length == 16)
                {
                    txtSayadPrefix.Text = check.SayadNumber.Substring(0, 4);
                    txtSayadSequence.Text = check.SayadNumber.Substring(4, 12);
                }

                if (!string.IsNullOrEmpty(check.CostType)) cmbCostType.SelectedItem = check.CostType;

                if (check.CheckType == DirectionTypes.Outgoing)
                {
                    formLayout.RowStyles[7].Height = 140F;
                    formLayout.RowStyles[10].Height = 45F;
                    string scope = check.PaymentScope ?? (check.ContractorContractId.HasValue ? PaymentScopes.ContractorContract : (check.ContractId.HasValue ? PaymentScopes.DirectPurchase : PaymentScopes.Administrative));
                    switch (scope)
                    {
                        case PaymentScopes.DirectPurchase: rbDirectPurchase.Checked = true; SelectComboItem(cmbClientContract, check.ContractId); break;
                        case PaymentScopes.InformalContractor: rbInformalContractor.Checked = true; SelectComboItem(cmbClientContract, check.ContractId); break;
                        case PaymentScopes.ContractorContract: rbContractorContract.Checked = true; SelectComboItem(cmbContractorContract, check.ContractorContractId); break;
                        default: rbAdmin.Checked = true; break;
                    }
                }
                else
                {
                    formLayout.RowStyles[7].Height = 0F;
                    formLayout.RowStyles[8].Height = 45F;
                    SelectComboItem(cmbClientContract, check.ContractId);
                }

                cmbType.Enabled = false; cmbAccount.Enabled = false; txtCheckNumber.ReadOnly = true; txtSayadPrefix.ReadOnly = true; txtBankName.ReadOnly = true;
                txtCheckbookInfo.Text = "در حال ویرایش چک (شماره و صیاد قفل است)"; txtCheckbookInfo.ForeColor = UIHelper.ActionAmber;
            }
            finally
            {
                _isInitializing = false;
                OnScopeChanged(); // به‌روزرسانی ارتفاع ردیف‌ها
            }
        }

        private void SelectComboItem(ComboBox cmb, int? id)
        {
            if (!id.HasValue) return;
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (cmb.Items[i] is ComboItem item && item.Id == id.Value) { cmb.SelectedIndex = i; return; }
            }
        }

        private async Task ReloadAccountsAsync()
        {
            if (IsDisposed) return;
            _isInitializing = true;
            try
            {
                string checkType = cmbType.SelectedItem?.ToString() ?? DirectionTypes.Incoming;
                List<CompanyBankAccount>? accounts = null;
                await AsyncHelper.RunWithLoading(this, async () =>
                {
                    accounts = checkType == DirectionTypes.Outgoing ? await _bankRepo.GetWithCheckbookAsync() : await _bankRepo.GetActiveAsync();
                }, "در حال بارگذاری حساب‌ها...");

                if (accounts != null && !IsDisposed)
                {
                    cmbAccount.DataSource = null; cmbAccount.DataSource = accounts;
                    if (accounts.Count > 0 && !_checkId.HasValue) await OnAccountSelectedAsync();
                }
            }
            finally { _isInitializing = false; }
        }

        private async Task OnAccountSelectedAsync()
        {
            if (_checkId.HasValue || IsDisposed) return;
            if (cmbAccount.SelectedItem is not CompanyBankAccount acc) return;

            if (cmbType.Text == DirectionTypes.Outgoing)
            {
                txtBankName.Text = acc.BankName ?? ""; txtBankName.ReadOnly = true; txtBankName.BackColor = UIHelper.WarmInputBackground;
                var (nextCheck, sayadSeq) = await _repository.GetNextCheckAndSayadAsync(acc.Id);
                if (IsDisposed) return;

                if (string.IsNullOrWhiteSpace(nextCheck))
                {
                    txtCheckbookInfo.Text = "⚠️ دسته چکی ثبت نشده"; txtCheckbookInfo.ForeColor = UIHelper.WarmDanger;
                    txtCheckNumber.Text = ""; txtSayadPrefix.Text = ""; txtSayadSequence.Text = "";
                }
                else
                {
                    txtCheckNumber.Text = nextCheck; txtSayadPrefix.Text = ""; txtSayadSequence.Text = sayadSeq;
                    txtCheckbookInfo.Text = "✅ شماره چک و صیاد تنظیم شد"; txtCheckbookInfo.ForeColor = UIHelper.ActionGreen;
                }
            }
            else
            {
                txtBankName.Text = ""; txtBankName.ReadOnly = false; txtBankName.BackColor = Color.White;
                txtCheckbookInfo.Text = "---"; txtCheckbookInfo.ForeColor = UIHelper.WarmWarningText;
                txtCheckNumber.Text = ""; txtSayadPrefix.Text = ""; txtSayadSequence.Text = "";
            }
        }

        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (string.IsNullOrWhiteSpace(txtCheckNumber.Text)) { ModernMessageBox.ShowWarning("شماره چک الزامی است."); return; }

                int? effectiveAccountId;
                if (_checkId.HasValue)
                {
                    effectiveAccountId = _originalAccountId;
                    if (!effectiveAccountId.HasValue) { ModernMessageBox.ShowWarning("حساب بانکی مشخص نیست."); return; }
                }
                else
                {
                    if (cmbAccount.SelectedValue == null) { ModernMessageBox.ShowWarning("حساب بانکی را انتخاب کنید."); return; }
                    effectiveAccountId = (int)cmbAccount.SelectedValue;
                }

                decimal amount = NumberHelper.ParseFormatted(txtAmount.Text);
                if (amount <= 0) { ModernMessageBox.ShowWarning("مبلغ باید بزرگتر از صفر باشد."); return; }
                DateTime? date = DateHelper.ParsePersianDate(txtDueDate.Text);
                if (date == null) { ModernMessageBox.ShowWarning("تاریخ سررسید معتبر نیست."); return; }
                if (string.IsNullOrWhiteSpace(txtRecipientName.Text)) { ModernMessageBox.ShowWarning("نام گیرنده الزامی است."); return; }

                int? contractId = null; int? contractorContractId = null;
                string contractTitle = ""; string paymentScope = "";

                if (cmbType.Text == DirectionTypes.Outgoing)
                {
                    if (rbAdmin.Checked) { paymentScope = PaymentScopes.Administrative; contractTitle = "هزینه‌های اداری"; }
                    else if (rbDirectPurchase.Checked || rbInformalContractor.Checked)
                    {
                        paymentScope = rbDirectPurchase.Checked ? PaymentScopes.DirectPurchase : PaymentScopes.InformalContractor;
                        if (cmbClientContract.SelectedItem is not ComboItem clientItem || clientItem.Id <= 0) { ModernMessageBox.ShowWarning("لطفاً قرارداد کارفرما را انتخاب کنید."); return; }
                        contractId = clientItem.Id; contractTitle = clientItem.Display;
                        if (cmbCostType.Text == "سایر") { ModernMessageBox.ShowWarning("ماهیت هزینه را انتخاب کنید."); return; }
                    }
                    else
                    {
                        paymentScope = PaymentScopes.ContractorContract;
                        if (cmbContractorContract.SelectedItem is not ComboItem conItem || conItem.Id <= 0) { ModernMessageBox.ShowWarning("لطفاً قرارداد پیمانکار را انتخاب کنید."); return; }
                        contractorContractId = conItem.Id; contractId = conItem.ClientContractId; contractTitle = conItem.Display;
                        if (cmbCostType.Text == "سایر") { ModernMessageBox.ShowWarning("ماهیت هزینه را انتخاب کنید."); return; }
                    }
                }
                else
                {
                    paymentScope = "ClientContract";
                    if (cmbClientContract.SelectedItem is ComboItem clientItem && clientItem.Id > 0) { contractId = clientItem.Id; contractTitle = clientItem.Display; }
                }

                string bankName;
                if (cmbAccount.SelectedItem is CompanyBankAccount selectedAcc) bankName = string.IsNullOrWhiteSpace(txtBankName.Text) ? selectedAcc.BankName : txtBankName.Text.Trim();
                else bankName = txtBankName.Text.Trim();

                string prefix = txtSayadPrefix.Text.Trim(); string seq = txtSayadSequence.Text.Trim();
                string fullSayad = string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(seq) ? null : prefix + seq;

                var check = new CashCheck
                {
                    Id = _checkId ?? 0,
                    CheckNumber = txtCheckNumber.Text.Trim(),
                    BankName = bankName,
                    DueDate = date.Value,
                    Amount = amount,
                    CheckType = cmbType.Text,
                    Status = "در جریان",
                    AccountId = effectiveAccountId,
                    Description = txtDescription.Text.Trim(),
                    SayadNumber = fullSayad,
                    RecipientName = txtRecipientName.Text.Trim(),
                    RecipientNationalId = txtRecipientId.Text.Trim(),
                    IsLegalEntity = chkLegalEntity.Checked,
                    ContractId = contractId,
                    ContractorContractId = contractorContractId,
                    ContractTitle = contractTitle,
                    CostType = cmbCostType.Text,
                    PaymentScope = paymentScope
                };

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    if (_checkId.HasValue) await _repository.UpdateAsync(check);
                    else await _repository.AddAsync(check);
                }, _checkId.HasValue ? "در حال ذخیره..." : "در حال ثبت...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show(_checkId.HasValue ? "تغییرات ذخیره شد." : "چک ثبت شد.", true);
                    if (!_checkId.HasValue) { _checkId = check.Id; _originalAccountId = check.AccountId; btnAttachments.Enabled = true; btnSave.Text = "💾 ذخیره تغییرات"; ToastHelper.Show("حالا می‌توانید پیوست کنید.", true); }
                    else { DialogResult = DialogResult.OK; Close(); }
                }
            }
            catch (Exception ex) { LogHelper.LogException(ex, "CashCheckEditForm.SaveSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
            finally { try { _actionLock.Release(); } catch { } }
        }
    }
}