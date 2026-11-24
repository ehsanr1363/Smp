using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class PartyEditForm : Form
{
    private readonly string _connectionString;
    private readonly int? _partyId;
    private readonly bool _isEdit;

    private ModernRadioButton rbNatural = null!, rbLegal = null!;
    private ModernCheckBox chkForeign = null!, chkContractor = null!, chkClient = null!;
    private ModernTextBox txtNationalCode = null!, txtFirstName = null!, txtLastName = null!;
    private ModernTextBox txtCompanyName = null!, txtNationalId = null!, txtEconomicCode = null!;
    private ModernTextBox txtPhone = null!, txtAddress = null!;
    private ModernButton btnSave = null!;

    private Label lblNationalCode = null!;
    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly TafsiliRepository _tafsiliRepo;
    private readonly PartyRepository _repo;

    public PartyEditForm(string connectionString, int? partyId = null)
    {
        _connectionString = connectionString;
        _tafsiliRepo = new TafsiliRepository(connectionString);
        _repo = new PartyRepository(connectionString);
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
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PartyEditForm"); } }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        int rh = ModernUI.RowHeight;

        // ───── کارت ۱: نوع و نقش ─────
        rbNatural = ModernUI.Radio("شخص حقیقی"); rbNatural.Checked = true;
        rbLegal = ModernUI.Radio("شخص حقوقی");
        chkForeign = ModernUI.Check("اتباع غیرایرانی", ModernTheme.Danger);
        chkContractor = ModernUI.Check("پیمانکار", ModernTheme.Primary); chkContractor.Checked = true;
        chkClient = ModernUI.Check("کارفرما", ModernTheme.Success); chkClient.Checked = true;

        rbNatural.CheckedChanged += (s, e) => ToggleFields();
        rbLegal.CheckedChanged += (s, e) => ToggleFields();
        chkForeign.CheckedChanged += (s, e) => ToggleForeignMode();

        var cardType = new ModernCard("نوع و نقش", ModernTheme.Primary, 3, rh, rh);
        cardType.Body.Controls.Add(ModernUI.CenterInCell(rbNatural, 12), 0, 0);
        cardType.Body.Controls.Add(ModernUI.CenterInCell(rbLegal, 12), 1, 0);
        cardType.Body.Controls.Add(ModernUI.CenterInCell(chkForeign, 12), 2, 0);
        cardType.Body.Controls.Add(ModernUI.CenterInCell(chkContractor, 12), 0, 1);
        cardType.Body.Controls.Add(ModernUI.CenterInCell(chkClient, 12), 1, 1);

        // ───── کارت ۲: مشخصات ─────
        txtNationalCode = ModernUI.Input(ModernInputKind.Digits);
        txtFirstName = ModernUI.Input();
        txtLastName = ModernUI.Input();
        txtCompanyName = ModernUI.Input();
        txtNationalId = ModernUI.Input(ModernInputKind.Digits);
        txtEconomicCode = ModernUI.Input(ModernInputKind.Digits);

        var btnFetch = ModernButton.Soft("🔍", ModernTheme.Primary, 44, ModernUI.InputHeight);
        btnFetch.Click += BtnFetchFromTafsili_Click;

        var pnlNc = new BufferedTable { Margin = Padding.Empty, BackColor = Color.Transparent, ColumnCount = 2, RowCount = 1 };
        pnlNc.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pnlNc.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52F));
        pnlNc.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        txtNationalCode.Dock = DockStyle.Fill;
        btnFetch.Anchor = AnchorStyles.None;
        pnlNc.Controls.Add(txtNationalCode, 0, 0);
        pnlNc.Controls.Add(btnFetch, 1, 0);

        var fldNc = ModernUI.Field("کد ملی", pnlNc);
        lblNationalCode = (Label)((TableLayoutPanel)fldNc).GetControlFromPosition(0, 0)!;

        int fh = ModernUI.FieldHeight;
        var cardInfo = new ModernCard("مشخصات", ModernTheme.Teal, 2, fh, fh, fh);
        cardInfo.Body.Controls.Add(fldNc, 0, 0);
        cardInfo.Body.Controls.Add(ModernUI.Field("نام", txtFirstName), 1, 0);
        cardInfo.Body.Controls.Add(ModernUI.Field("نام خانوادگی", txtLastName), 0, 1);
        cardInfo.Body.Controls.Add(ModernUI.Field("نام شرکت", txtCompanyName), 1, 1);
        cardInfo.Body.Controls.Add(ModernUI.Field("شناسه ملی شرکت", txtNationalId), 0, 2);
        cardInfo.Body.Controls.Add(ModernUI.Field("کد اقتصادی", txtEconomicCode), 1, 2);

        // ───── کارت ۳: تماس ─────
        txtPhone = ModernUI.Input(ModernInputKind.Digits);
        txtAddress = ModernUI.Memo(70);

        var cardContact = new ModernCard("اطلاعات تماس", ModernTheme.Purple, 1, fh, fh + 50);
        cardContact.Body.Controls.Add(ModernUI.Field("تلفن تماس", txtPhone), 0, 0);
        cardContact.Body.Controls.Add(ModernUI.Row("آدرس", txtAddress), 0, 1);

        // ───── دکمه‌ها ─────
        btnSave = ModernButton.Success(_isEdit ? "💾 ذخیره تغییرات" : "💾 ذخیره", 200, 40);
        btnSave.Click += async (s, e) => await SaveSafeAsync();
        var btnCancel = ModernButton.Cancel();
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        var bar = ModernListKit.BuildActionBar(new Control[] { btnSave, btnCancel }, new Control[0]);

        ModernListKit.ComposeRows(this,
            _isEdit ? "ویرایش طرف قرارداد" : "افزودن طرف قرارداد",
            "ثبت مشخصات پیمانکار / کارفرما",
            new Size(850, 760),
            (cardType, cardType.Height + 8),
            (cardInfo, cardInfo.Height + 8),
            (cardContact, cardContact.Height + 8),
            (bar, 64));
    }

    // ✅ متد جدید برای فراخوانی از تفصیلی‌ها
    private async void BtnFetchFromTafsili_Click(object? sender, EventArgs e)
    {
        using var form = new TafsiliSelectionForm(_connectionString);
        if (form.ShowDialog(this) == DialogResult.OK && !IsDisposed)
        {
            var tafsili = await _tafsiliRepo.GetByIdAsync(form.SelectedId);
            if (tafsili is null) return;

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
        lblNationalCode.Text = chkForeign.Checked ? "شماره پاسپورت" : "کد ملی";
        lblNationalCode.ForeColor = chkForeign.Checked ? ModernTheme.Danger : ModernTheme.Text;
        txtNationalCode.Kind = chkForeign.Checked ? ModernInputKind.Text : ModernInputKind.Digits;
    }

    private async Task LoadDataAsync()
    {
        if (!_partyId.HasValue) return;

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            var party = await _repo.GetByIdAsync(_partyId.Value)
                        ?? throw new InvalidOperationException("رکورد یافت نشد.");

            if (IsDisposed) return;

            string type = party.PartyType;
            rbNatural.Checked = type == PartyTypeHelper.Natural;
            rbLegal.Checked = type == PartyTypeHelper.Legal;

            chkForeign.Checked = party.IsForeign;
            chkContractor.Checked = party.IsContractor;
            chkClient.Checked = party.IsClient;

            txtNationalCode.Text = party.NationalCode;
            txtFirstName.Text = party.FirstName;
            txtLastName.Text = party.LastName;
            txtCompanyName.Text = party.CompanyName;
            txtNationalId.Text = party.NationalId;
            txtEconomicCode.Text = party.EconomicCode;
            txtPhone.Text = party.Phone;
            txtAddress.Text = party.Address;

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
                await _repo.SaveAsync(new PartyRecord
                {
                    Id = currentId,
                    PartyType = rbNatural.Checked ? PartyTypeHelper.Natural : PartyTypeHelper.Legal,
                    IsForeign = chkForeign.Checked,
                    IsContractor = chkContractor.Checked,
                    IsClient = chkClient.Checked,
                    NationalCode = txtNationalCode.Text,
                    FirstName = txtFirstName.Text,
                    LastName = txtLastName.Text,
                    CompanyName = txtCompanyName.Text,
                    NationalId = txtNationalId.Text,
                    EconomicCode = txtEconomicCode.Text,
                    Phone = txtPhone.Text,
                    Address = txtAddress.Text
                });
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
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PartyEditForm"); } }
    }
}
