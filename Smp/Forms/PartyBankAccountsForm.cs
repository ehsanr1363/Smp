using Smp.Data;
using Smp.Helpers;
using Smp.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class PartyBankAccountsForm : Form
{
    private readonly PartyRepository _repo;
    private readonly int _partyId;
    private readonly string _partyName;

    private DataGridView dgvBanks = null!;
    private ModernTextBox txtSheba = null!, txtBankName = null!;
    private ModernButton btnAdd = null!, btnDelete = null!, btnSetMain = null!;

    private readonly Dictionary<string, string> _bankCodes = new();

    // ✅ FIX P2: جلوگیری از تداخل
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public PartyBankAccountsForm(string connectionString, int partyId, string partyName)
    {
        _repo = new PartyRepository(connectionString);
        _partyId = partyId;
        _partyName = partyName;

        BuildUI();

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnAdd);
        ShowInTaskbar = false;

        Shown += async (s, e) => await LoadDataSafeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PartyBankAccountsForm"); }
            try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PartyBankAccountsForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── کارت ثبت شبا ──
        txtSheba = ModernUI.Input(ModernInputKind.Text, "IR000000000000000000000000");
        txtSheba.TextBox.TextChanged += TxtSheba_TextChanged;

        txtBankName = ModernUI.Input();
        txtBankName.ReadOnly = true;

        int rh = ModernUI.RowHeight;
        var card = new ModernCard("ثبت شماره شبای جدید", ModernTheme.Primary,
            new[] { new ColumnStyle(SizeType.Percent, 60F), new ColumnStyle(SizeType.Percent, 40F) }, rh);
        card.Body.Controls.Add(ModernUI.Row("شماره شبا (IR...)", txtSheba), 0, 0);
        card.Body.Controls.Add(ModernUI.Row("نام بانک", txtBankName), 1, 0);

        // ── جدول ──
        dgvBanks = ModernGrid.Create();
        dgvBanks.AutoGenerateColumns = false;
        dgvBanks.AllowUserToAddRows = false;
        dgvBanks.ReadOnly = true;
        dgvBanks.MultiSelect = false;
        dgvBanks.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvBanks.FillColumns();
        dgvBanks.UseAiryLook(44);
        dgvBanks.UseSelectionAccent(ModernTheme.Primary);

        dgvBanks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgvBanks.Columns.Add(new DataGridViewTextBoxColumn { Name = "ShebaNumber", DataPropertyName = "ShebaNumber", HeaderText = "شماره شبا", FillWeight = 40 });
        dgvBanks.Columns.Add(new DataGridViewTextBoxColumn { Name = "BankName", DataPropertyName = "BankName", HeaderText = "نام بانک", FillWeight = 30 });
        dgvBanks.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsMainAccount", DataPropertyName = "IsMainAccount", HeaderText = "وضعیت", FillWeight = 15 });
        ModernListKit.PinHeaders(dgvBanks);   // جلوگیری از جایگزینی هدرها با ترجمه‌ی عمومی ستون
        dgvBanks.CellFormatting += DgvBanks_CellFormatting;

        // ── دکمه‌ها ──
        btnAdd = ModernButton.Save("➕ ثبت و افزودن شماره شبا", 230, 38);
        btnAdd.Click += async (s, e) => await BtnAdd_Click(s, e);

        btnSetMain = ModernButton.Soft("⭐ تنظیم به عنوان حساب اصلی", ModernTheme.Secondary, 230, 38);
        btnSetMain.Click += async (s, e) => await BtnSetMain_Click(s, e);

        btnDelete = ModernButton.Soft("🗑️ حذف شماره شبا", ModernTheme.Danger, 170, 38);
        btnDelete.Click += async (s, e) => await BtnDelete_Click(s, e);

        var actions = ModernListKit.BuildActionBar(new Control[] { btnAdd, btnSetMain }, new Control[] { btnDelete });

        ModernListKit.ComposeRows(this, $"حساب‌های بانکی: {_partyName}",
            "ثبت، حذف و تعیین حساب اصلی طرف حساب", new Size(900, 650),
            (card, card.Height + 12), (ModernReport.WrapGrid(dgvBanks), 0), (actions, 64));
    }

    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;

        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var shebasTask = _repo.GetBankAccountsAsync(_partyId);

                await shebasTask;

                if (IsDisposed) return;

                _bankCodes.Clear();
                // لیست بانک‌ها از BankHelper خوانده می‌شود (وابسته به جدول Banks دیتابیس نیست)
                foreach (var kv in BankHelper.BankList)
                    if (!string.IsNullOrEmpty(kv.Key) && !_bankCodes.ContainsKey(kv.Key))
                        _bankCodes.Add(kv.Key, kv.Value);

                dgvBanks.DataSource = shebasTask.Result;
            }, "در حال بارگذاری اطلاعات...");

            if (!success && !IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات.");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "PartyBankAccountsForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally
        {
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PartyBankAccountsForm"); }
        }
    }

    private void TxtSheba_TextChanged(object? sender, EventArgs e)
    {
        string sheba = StringHelper.ConvertToEnglishDigits(txtSheba.Text).Trim().Replace(" ", "").ToUpper();

        string? bankCode = null;
        if (sheba.StartsWith("IR") && sheba.Length >= 7)
            bankCode = sheba.Substring(4, 3);
        else if (sheba.Length >= 5)
            bankCode = sheba.Substring(2, 3);

        if (!string.IsNullOrEmpty(bankCode) && _bankCodes.ContainsKey(bankCode))
            txtBankName.Text = _bankCodes[bankCode];
        else
            txtBankName.Text = "";
    }

    private void DgvBanks_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (dgvBanks.Columns[e.ColumnIndex].Name != "IsMainAccount") return;

        var rowView = dgvBanks.Rows[e.RowIndex].DataBoundItem as DataRowView;
        if (rowView is null) return;

        int isMain = Convert.ToInt32(rowView["IsMainAccount"]);
        if (isMain == 1)
        {
            e.Value = "⭐ حساب اصلی";
            e.CellStyle.ForeColor = ModernTheme.Secondary;
            e.CellStyle.Font = ModernTheme.GetFont(11f, FontStyle.Bold);
        }
        else
        {
            e.Value = "-";
            e.CellStyle.ForeColor = ModernTheme.Text;
        }
        e.FormattingApplied = true;
    }

    private async Task BtnAdd_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            string rawSheba = txtSheba.Text.Trim();
            string normalizedSheba = BankHelper.NormalizeSheba(rawSheba);

            if (normalizedSheba is null || normalizedSheba.Length != 24)
            {
                ModernMessageBox.ShowError("شماره شبا وارد شده معتبر نیست.");
                return;
            }

            string fullSheba = "IR" + normalizedSheba;

            if (!ShebaHelper.IsValid(fullSheba))
            {
                ModernMessageBox.ShowError("شماره شبا وارد شده از نظر چک‌سام معتبر نیست.");
                return;
            }

            string bankName = BankHelper.GetBankNameFromSheba(normalizedSheba);
            if (string.IsNullOrEmpty(bankName) || bankName.StartsWith("بانک ناشناخته"))
            {
                ModernMessageBox.ShowError("کد بانک در شماره شبا نامعتبر است یا در سیستم تعریف نشده است.");
                return;
            }

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repo.AddBankAccountAsync(_partyId, fullSheba, normalizedSheba.Substring(0, 3), bankName);
            }, "در حال ثبت شماره شبا...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("شماره شبا با موفقیت ثبت شد.", isSuccess: true);
                txtSheba.Text = string.Empty;
                txtBankName.Text = string.Empty;
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "PartyBankAccountsForm.BtnAdd_Click");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PartyBankAccountsForm"); }
        }
    }

    private async Task BtnSetMain_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (dgvBanks.CurrentRow?.DataBoundItem is not DataRowView rowView)
            {
                ModernMessageBox.ShowWarning("لطفاً یک شماره شبا را از جدول انتخاب کنید.");
                return;
            }

            int id = Convert.ToInt32(rowView["Id"]);
            int isMain = Convert.ToInt32(rowView["IsMainAccount"]);

            if (isMain == 1) return;

            if (!ModernMessageBox.AskConfirm("آیا این شماره شبا به عنوان حساب اصلی تنظیم شود؟", "تغییر حساب اصلی")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repo.SetMainBankAccountAsync(_partyId, id);
            }, "در حال تنظیم حساب اصلی...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("حساب اصلی با موفقیت تغییر یافت.", isSuccess: true);
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "PartyBankAccountsForm.BtnSetMain_Click");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PartyBankAccountsForm"); }
        }
    }

    private async Task BtnDelete_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;

        try
        {
            if (dgvBanks.CurrentRow?.DataBoundItem is not DataRowView rowView)
            {
                ModernMessageBox.ShowWarning("لطفاً یک شماره شبا را از جدول انتخاب کنید.");
                return;
            }

            int id = Convert.ToInt32(rowView["Id"]);
            string sheba = rowView["ShebaNumber"].ToString() ?? "";
            bool isMain = Convert.ToInt32(rowView["IsMainAccount"]) == 1;

            if (isMain)
            {
                ModernMessageBox.ShowWarning("شما نمی‌توانید حساب اصلی را حذف کنید. ابتدا یک حساب دیگر را به عنوان اصلی تنظیم کنید.");
                return;
            }

            if (!ModernMessageBox.AskConfirm($"آیا از حذف شماره شبا «{sheba}» مطمئن هستید؟", "حذف شماره شبا")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repo.DeleteBankAccountAsync(id);
            }, "در حال حذف...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("شماره شبا با موفقیت حذف شد.", isSuccess: true);
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "PartyBankAccountsForm.BtnDelete_Click");
            if (!IsDisposed) ModernMessageBox.ShowError(ex.Message);
        }
        finally
        {
            try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PartyBankAccountsForm"); }
        }
    }
}
