using Microsoft.Data.SqlClient;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class PartyEditForm : Form
    {
        private readonly string _connectionString;
        private readonly int? _partyId;
        private readonly bool _isEdit;

        private RadioButton rbNatural = null!, rbLegal = null!;
        private CheckBox chkForeign = null!, chkContractor = null!, chkClient = null!;
        private TextBox txtNationalCode = null!, txtFirstName = null!, txtLastName = null!;
        private TextBox txtCompanyName = null!, txtNationalId = null!, txtEconomicCode = null!;
        private TextBox txtPhone = null!, txtAddress = null!;
        private Button btnSave = null!;

        private TableLayoutPanel formLayout = null!;
        private readonly SemaphoreSlim _actionLock = new(1, 1);
        private readonly TafsiliRepository _tafsiliRepo;

        public PartyEditForm(string connectionString, int? partyId = null)
        {
            _connectionString = connectionString;
            _tafsiliRepo = new TafsiliRepository(connectionString);
            _partyId = partyId;
            _isEdit = partyId.HasValue && partyId.Value > 0;

            BuildUI();
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            Shown += async (s, e) =>
            {
                if (_isEdit) await LoadDataAsync();
                else ToggleFields();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _actionLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var (contentLayout, btnPanel) = StandardFormHelper.SetupStandardLayout(
                this, _isEdit ? "ویرایش طرف قرارداد" : "افزودن طرف قرارداد", new Size(850, 700));

            formLayout = contentLayout;

            // ─── ردیف ۰: نوع شخصیت ───
            StandardFormHelper.AddRow(formLayout, 50F);

            var pnlType = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            rbNatural = new RadioButton { Text = "شخص حقیقی", Checked = true, AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(0, 10, 15, 0) };
            rbLegal = new RadioButton { Text = "شخص حقوقی", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(0, 10, 15, 0) };
            chkForeign = new CheckBox { Text = "اتباع غیرایرانی", AutoSize = true, Font = UIHelper.PersianFont(10f), ForeColor = UIHelper.WarmDanger, Margin = new Padding(0, 10, 0, 0) };

            rbNatural.CheckedChanged += (s, e) => ToggleFields();
            rbLegal.CheckedChanged += (s, e) => ToggleFields();
            chkForeign.CheckedChanged += (s, e) => ToggleForeignMode();

            pnlType.Controls.Add(rbNatural);
            pnlType.Controls.Add(rbLegal);
            pnlType.Controls.Add(chkForeign);
            UIHelper.AddField(formLayout, "نوع شخص:", pnlType, 0, 0, 1, 3);

            // ─── ردیف ۱: نقش‌ها ───
            StandardFormHelper.AddRow(formLayout, 50F);
            var pnlRoles = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            chkContractor = new CheckBox { Text = "پیمانکار", Checked = true, AutoSize = true, Font = UIHelper.PersianFont(11f), ForeColor = UIHelper.SectionBlue, Margin = new Padding(0, 10, 20, 0) };
            chkClient = new CheckBox { Text = "کارفرما", Checked = true, AutoSize = true, Font = UIHelper.PersianFont(11f), ForeColor = UIHelper.ActionGreen, Margin = new Padding(0, 10, 0, 0) };
            pnlRoles.Controls.Add(chkContractor);
            pnlRoles.Controls.Add(chkClient);
            UIHelper.AddField(formLayout, "نقش در سیستم:", pnlRoles, 1, 0, 1, 3);

            // ─── ردیف ۲: کد ملی + دکمه ذره‌بین | نام ───
            StandardFormHelper.AddRow(formLayout);

            // ساخت پنل اختصاصی برای فیلد کد ملی به همراه دکمه جستجو
            var pnlNationalCode = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            pnlNationalCode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlNationalCode.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40F));

            txtNationalCode = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 5, 0) };
            var btnFetchFromTafsili = new Button
            {
                Text = "🔍",
                Dock = DockStyle.Fill,
                BackColor = UIHelper.SectionBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Margin = new Padding(0),
                Cursor = Cursors.Hand
            };
            btnFetchFromTafsili.FlatAppearance.BorderSize = 0;
            btnFetchFromTafsili.Click += BtnFetchFromTafsili_Click;

            pnlNationalCode.Controls.Add(txtNationalCode, 0, 0);
            pnlNationalCode.Controls.Add(btnFetchFromTafsili, 1, 0);

            txtFirstName = new TextBox();
            UIHelper.AddField(formLayout, "کد ملی / پاسپورت:", pnlNationalCode, 2, 0, 1);
            UIHelper.AddField(formLayout, "نام:", txtFirstName, 2, 2, 3);

            // ─── ردیف ۳: نام خانوادگی و نام شرکت ───
            StandardFormHelper.AddRow(formLayout);
            txtLastName = new TextBox();
            txtCompanyName = new TextBox();
            UIHelper.AddField(formLayout, "نام خانوادگی:", txtLastName, 3, 0, 1);
            UIHelper.AddField(formLayout, "نام شرکت:", txtCompanyName, 3, 2, 3);

            // ─── ردیف ۴: شناسه ملی و کد اقتصادی ───
            StandardFormHelper.AddRow(formLayout);
            txtNationalId = new TextBox();
            txtEconomicCode = new TextBox();
            UIHelper.AddField(formLayout, "شناسه ملی شرکت:", txtNationalId, 4, 0, 1);
            UIHelper.AddField(formLayout, "کد اقتصادی:", txtEconomicCode, 4, 2, 3);

            // ─── ردیف ۵: تلفن ───
            StandardFormHelper.AddRow(formLayout);
            txtPhone = new TextBox();
            UIHelper.AddField(formLayout, "تلفن تماس:", txtPhone, 5, 0, 1);

            // ─── ردیف ۶: آدرس ───
            StandardFormHelper.AddRow(formLayout, 80F);
            txtAddress = new TextBox { Multiline = true, Dock = DockStyle.Fill };
            UIHelper.AddField(formLayout, "آدرس:", txtAddress, 6, 0, 1, 3);

            // ─── دکمه ذخیره ───
            btnSave = UIHelper.CreateButton("💾 ذخیره", UIHelper.ActionGreen, 200, 45);
            btnSave.Font = UIHelper.PersianFont(12f, FontStyle.Bold);
            btnSave.Click += async (s, e) => await SaveSafeAsync();
            btnPanel.Controls.Add(btnSave);
        }

        // ✅ متد جدید برای فراخوانی از تفصیلی‌ها
        private async void BtnFetchFromTafsili_Click(object? sender, EventArgs e)
        {
            using var form = new TafsiliSelectionForm(_connectionString);
            if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed)
            {
                var tafsili = await _tafsiliRepo.GetByIdAsync(form.SelectedId);
                if (tafsili == null) return;

                if (tafsili.PersonType.Contains("حقوقی"))
                {
                    rbLegal.Checked = true;
                    txtCompanyName.Text = tafsili.CompanyName;
                    txtNationalId.Text = tafsili.NationalId;
                    txtNationalCode.Text = "";
                    txtFirstName.Text = "";
                    txtLastName.Text = "";
                }
                else
                {
                    rbNatural.Checked = true;
                    txtFirstName.Text = tafsili.FirstName;
                    txtLastName.Text = tafsili.LastName;
                    txtNationalCode.Text = tafsili.NationalCode;
                    txtCompanyName.Text = "";
                    txtNationalId.Text = "";
                }

                txtEconomicCode.Text = tafsili.EconomicCode;

                if (tafsili.Type == "پیمانکار")
                {
                    chkContractor.Checked = true;
                    chkClient.Checked = false;
                }
                else if (tafsili.Type == "کارفرما")
                {
                    chkClient.Checked = true;
                    chkContractor.Checked = false;
                }

                ToggleFields();
                ToastHelper.Show("اطلاعات شخص از تفصیلی‌ها فراخوانی شد. لطفاً تلفن و آدرس را تکمیل کنید.", isSuccess: true);
            }
        }

        private void ToggleFields()
        {
            bool isNatural = rbNatural.Checked;
            txtNationalCode.Enabled = isNatural;
            txtFirstName.Enabled = isNatural;
            txtLastName.Enabled = isNatural;
            txtCompanyName.Enabled = !isNatural;
            txtNationalId.Enabled = !isNatural;
            txtEconomicCode.Enabled = !isNatural;
        }

        private void ToggleForeignMode()
        {
            var lbl = formLayout.GetControlFromPosition(0, 2) as Label;
            if (lbl != null)
            {
                lbl.Text = chkForeign.Checked ? "شماره پاسپورت:" : "کد ملی:";
                lbl.ForeColor = chkForeign.Checked ? UIHelper.WarmDanger : UIHelper.WarmText;
            }
        }

        private async Task LoadDataAsync()
        {
            if (!_partyId.HasValue) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                string query = "SELECT * FROM Parties WHERE Id = @id";
                var dt = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, query, new SqlParameter("@id", _partyId.Value)));

                if (dt.Rows.Count == 0) throw new InvalidOperationException("رکورد یافت نشد.");
                var row = dt.Rows[0];

                if (IsDisposed) return;

                string type = row["PartyType"].ToString()!;
                rbNatural.Checked = type == PartyTypeHelper.Natural;
                rbLegal.Checked = type == PartyTypeHelper.Legal;

                chkForeign.Checked = Convert.ToInt32(row["IsForeign"]) == 1;
                chkContractor.Checked = Convert.ToInt32(row["IsContractor"]) == 1;
                chkClient.Checked = Convert.ToInt32(row["IsClient"]) == 1;

                txtNationalCode.Text = row["NationalCode"]?.ToString();
                txtFirstName.Text = row["FirstName"]?.ToString();
                txtLastName.Text = row["LastName"]?.ToString();
                txtCompanyName.Text = row["CompanyName"]?.ToString();
                txtNationalId.Text = row["NationalId"]?.ToString();
                txtEconomicCode.Text = row["EconomicCode"]?.ToString();
                txtPhone.Text = row["Phone"]?.ToString();
                txtAddress.Text = row["Address"]?.ToString();

                ToggleForeignMode();
            }, "در حال بارگذاری اطلاعات...");

            if (!success) this.Close();
        }

        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (!chkContractor.Checked && !chkClient.Checked)
                {
                    ModernMessageBox.ShowWarning("شخص باید حداقل در یکی از نقش‌ها (پیمانکار یا کارفرما) انتخاب شود.");
                    return;
                }

                if (rbNatural.Checked)
                {
                    if (!chkForeign.Checked && !NationalCodeHelper.IsValid(txtNationalCode.Text)) { ModernMessageBox.ShowWarning("کد ملی نامعتبر است."); return; }
                    if (string.IsNullOrWhiteSpace(txtFirstName.Text) || string.IsNullOrWhiteSpace(txtLastName.Text)) { ModernMessageBox.ShowWarning("نام و نام خانوادگی الزامی است."); return; }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(txtCompanyName.Text)) { ModernMessageBox.ShowWarning("نام شرکت الزامی است."); return; }
                    if (!chkForeign.Checked && !string.IsNullOrWhiteSpace(txtNationalId.Text) && !Regex.IsMatch(txtNationalId.Text.Trim(), @"^\d{11}$"))
                    { ModernMessageBox.ShowWarning("شناسه ملی باید یک عدد ۱۱ رقمی باشد."); return; }
                }

                int currentId = _partyId ?? 0;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    if (!string.IsNullOrWhiteSpace(txtNationalCode.Text))
                    {
                        string checkNc = "SELECT COUNT(1) FROM Parties WHERE NationalCode = @nc AND Id != @id";
                        if (Convert.ToInt32(await Task.Run(() => DbHelper.ExecuteScalar(_connectionString, checkNc, new SqlParameter("@nc", txtNationalCode.Text.Trim()), new SqlParameter("@id", currentId)))) > 0)
                            throw new InvalidOperationException("این کد ملی قبلاً ثبت شده است.");
                    }
                    if (!string.IsNullOrWhiteSpace(txtNationalId.Text))
                    {
                        string checkNid = "SELECT COUNT(1) FROM Parties WHERE NationalId = @nid AND Id != @id";
                        if (Convert.ToInt32(await Task.Run(() => DbHelper.ExecuteScalar(_connectionString, checkNid, new SqlParameter("@nid", txtNationalId.Text.Trim()), new SqlParameter("@id", currentId)))) > 0)
                            throw new InvalidOperationException("این شناسه ملی قبلاً ثبت شده است.");
                    }

                    string query = _isEdit
                        ? @"UPDATE Parties SET PartyType=@type, IsForeign=@isForeign, IsContractor=@isContractor, IsClient=@isClient, NationalCode=@nc, FirstName=@fn, LastName=@ln, CompanyName=@cn, NationalId=@nid, EconomicCode=@ec, Phone=@phone, Address=@addr WHERE Id=@id;"
                        : @"INSERT INTO Parties (PartyType, IsForeign, IsContractor, IsClient, NationalCode, FirstName, LastName, CompanyName, NationalId, EconomicCode, Phone, Address) VALUES (@type, @isForeign, @isContractor, @isClient, @nc, @fn, @ln, @cn, @nid, @ec, @phone, @addr);";

                    var parameters = new List<SqlParameter>
                    {
                        new SqlParameter("@type", rbNatural.Checked ? PartyTypeHelper.Natural : PartyTypeHelper.Legal),
                        new SqlParameter("@isForeign", chkForeign.Checked ? 1 : 0),
                        new SqlParameter("@isContractor", chkContractor.Checked ? 1 : 0),
                        new SqlParameter("@isClient", chkClient.Checked ? 1 : 0),
                        new SqlParameter("@nc", string.IsNullOrWhiteSpace(txtNationalCode.Text) ? DBNull.Value : txtNationalCode.Text.Trim()),
                        new SqlParameter("@fn", string.IsNullOrWhiteSpace(txtFirstName.Text) ? DBNull.Value : txtFirstName.Text.Trim()),
                        new SqlParameter("@ln", string.IsNullOrWhiteSpace(txtLastName.Text) ? DBNull.Value : txtLastName.Text.Trim()),
                        new SqlParameter("@cn", string.IsNullOrWhiteSpace(txtCompanyName.Text) ? DBNull.Value : txtCompanyName.Text.Trim()),
                        new SqlParameter("@nid", string.IsNullOrWhiteSpace(txtNationalId.Text) ? DBNull.Value : txtNationalId.Text.Trim()),
                        new SqlParameter("@ec", string.IsNullOrWhiteSpace(txtEconomicCode.Text) ? DBNull.Value : txtEconomicCode.Text.Trim()),
                        new SqlParameter("@phone", string.IsNullOrWhiteSpace(txtPhone.Text) ? DBNull.Value : txtPhone.Text.Trim()),
                        new SqlParameter("@addr", string.IsNullOrWhiteSpace(txtAddress.Text) ? DBNull.Value : txtAddress.Text.Trim())
                    };

                    if (_isEdit) parameters.Add(new SqlParameter("@id", _partyId.Value));

                    await Task.Run(() => DbHelper.ExecuteNonQuery(_connectionString, query, parameters.ToArray()));
                }, "در حال ذخیره اطلاعات...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("اطلاعات با موفقیت ذخیره شد.", true);
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PartyEditForm.SaveSafeAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message);
            }
            finally { try { _actionLock.Release(); } catch { } }
        }
    }
}