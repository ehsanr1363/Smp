using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

/// <summary>ثبت / ویرایش مال + اتصال به اسناد خرید (چک، تراکنش نقدی، هزینه پروژه)</summary>
public class AssetEditForm : Form
{
    private readonly string _connectionString;
    private readonly int? _assetId;
    private readonly bool _isEdit;
    private readonly AssetRepository _repo;

    private TextBox txtCode = null!, txtName = null!, txtSerial = null!, txtDate = null!, txtAmount = null!, txtLocation = null!, txtDescription = null!;
    private ComboBox cmbCategory = null!, cmbStatus = null!, cmbDept = null!, cmbEmp = null!;
    private DataGridView dgvDocs = null!;
    private Button btnSave = null!, btnAddDoc = null!, btnRemoveDoc = null!, btnAttach = null!;

    private List<AssetPurchaseLink> _links = new();
    private ModernPage formLayout = null!;
    private int _contentHeight;

    private readonly SemaphoreSlim _actionLock = new(1, 1);

    private const int FieldHeight = ModernUI.FieldHeight;
    private const int FooterHeight = 64;
    private const int HeaderHeight = 50;
    private const int CardGap = 8;

    public AssetEditForm(string connectionString, int? assetId = null)
    {
        _connectionString = connectionString;
        _assetId = assetId;
        _isEdit = assetId.HasValue;
        _repo = new AssetRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader(_isEdit ? "ویرایش مال" : "ثبت مال جدید", new Size(860, 700));
        ApplyLayoutHeight();
        ShowInTaskbar = false;
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        Load += async (s, e) => await LoadDataAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { try { _actionLock?.Dispose(); } catch (Exception ex) { LogHelper.LogException(ex, "AssetEditForm"); } }
        base.Dispose(disposing);
    }

    private void ApplyLayoutHeight()
    {
        if (formLayout is null) return;
        ClientSize = new Size(ClientSize.Width, HeaderHeight + FooterHeight + _contentHeight);
        var wa = (IsHandleCreated ? Screen.FromControl(this) : Screen.PrimaryScreen)?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        int maxHeight = wa.Height - 10;
        if (Height > maxHeight) Height = maxHeight;
    }

    private void BuildUI()
    {
        int FH = FieldHeight;

        // ───────────── کارت ۱: مشخصات مال ─────────────
        var cardMain = new ModernCard("🏷️ مشخصات مال", ModernTheme.Primary, 3, FH, FH, FH);

        var codeBox = ModernUI.Input(placeholder: "خودکار");
        codeBox.TextAlign = HorizontalAlignment.Center;
        txtCode = codeBox.TextBox;

        var nameBox = ModernUI.Input();
        txtName = nameBox.TextBox;

        cmbCategory = ModernUI.Combo();
        cmbCategory.DropDownStyle = ComboBoxStyle.DropDownList;   // فقط از لیست «مدیریت Lookups ← دسته‌بندی اموال»

        var serialBox = ModernUI.Input();
        serialBox.TextAlign = HorizontalAlignment.Center;
        txtSerial = serialBox.TextBox;

        cmbStatus = ModernUI.Combo();
        cmbStatus.Items.AddRange(AssetStatuses.All.Cast<object>().ToArray());
        cmbStatus.SelectedIndex = 0;

        var dateBox = ModernUI.Input();
        dateBox.TextAlign = HorizontalAlignment.Center;
        txtDate = dateBox.TextBox;
        DateHelper.ApplyDateMask(txtDate);

        var amountBox = ModernUI.Money();
        amountBox.Font = ModernTheme.GetFont(12f, FontStyle.Bold);
        txtAmount = amountBox.TextBox;

        cardMain.Body.Controls.Add(ModernUI.Field("کد اموال", codeBox), 0, 0);
        var fName = ModernUI.Field("نام مال", nameBox);
        cardMain.Body.Controls.Add(fName, 1, 0);
        cardMain.Body.SetColumnSpan(fName, 2);
        cardMain.Body.Controls.Add(ModernUI.Field("دسته‌بندی", cmbCategory), 0, 1);
        cardMain.Body.Controls.Add(ModernUI.Field("شماره سریال", serialBox), 1, 1);
        cardMain.Body.Controls.Add(ModernUI.Field("وضعیت", cmbStatus), 2, 1);
        cardMain.Body.Controls.Add(ModernUI.Field("تاریخ خرید", dateBox), 0, 2);
        cardMain.Body.Controls.Add(ModernUI.Field("مبلغ خرید (ریال)", amountBox), 1, 2);

        // ───────────── کارت ۲: محل و تحویل‌گیرنده ─────────────
        var cardHolder = new ModernCard("👤 واحد و تحویل‌گیرنده", ModernTheme.Orange, 3, FH);
        cmbDept = ModernUI.Combo();
        cmbEmp = ModernUI.Combo();
        var locBox = ModernUI.Input(placeholder: "مثلاً طبقه ۲ - اتاق ۲۰۵");
        txtLocation = locBox.TextBox;
        cardHolder.Body.Controls.Add(ModernUI.Field("واحد / بخش", cmbDept), 0, 0);
        cardHolder.Body.Controls.Add(ModernUI.Field("تحویل‌گیرنده", cmbEmp), 1, 0);
        cardHolder.Body.Controls.Add(ModernUI.Field("محل نگهداری", locBox), 2, 0);

        // ───────────── کارت ۳: اسناد خرید ─────────────
        var cardDocs = new ModernCard("🔗 اسناد خرید (چک / تراکنش نقدی / هزینه پروژه)", ModernTheme.Teal, 1, 150, 46);

        dgvDocs = ModernGrid.Create();
        dgvDocs.AutoGenerateColumns = false;
        dgvDocs.MultiSelect = true;
        dgvDocs.FillColumns();
        dgvDocs.UseAiryLook(38);
        dgvDocs.UseSelectionAccent(ModernTheme.Teal);
        dgvDocs.Columns.Add(new DataGridViewTextBoxColumn { Name = "DocTypeName", DataPropertyName = "DocTypeName", HeaderText = "نوع سند", FillWeight = 16, MinimumWidth = 90 });
        dgvDocs.Columns.Add(new DataGridViewTextBoxColumn { Name = "DocDisplay", DataPropertyName = "DocDisplay", HeaderText = "خلاصه‌ی سند", FillWeight = 84, MinimumWidth = 200 });
        dgvDocs.Columns["DocTypeName"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        var gridHost = ModernReport.WrapGrid(dgvDocs);
        gridHost.Dock = DockStyle.Fill;
        cardDocs.Body.Controls.Add(gridHost, 0, 0);

        btnAddDoc = ModernButton.Soft("➕ افزودن سند خرید", ModernTheme.Teal, 170, 36);
        btnAddDoc.Click += (s, e) => AddDocs();
        btnRemoveDoc = ModernButton.Soft("🗑 حذف انتخاب‌شده", ModernTheme.Danger, 150, 36);
        btnRemoveDoc.Click += (s, e) => RemoveDocs();
        var docButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Color.Transparent, WrapContents = false };
        docButtons.Controls.Add(btnAddDoc);
        docButtons.Controls.Add(btnRemoveDoc);
        cardDocs.Body.Controls.Add(docButtons, 0, 1);

        // ───────────── کارت ۴: توضیحات ─────────────
        var cardDesc = new ModernCard("📝 توضیحات", ModernTheme.Neutral, 1, ModernUI.InputHeight + 4);
        var memo = ModernUI.Input();
        memo.Dock = DockStyle.Fill;
        memo.Margin = new Padding(6, 2, 6, 2);
        txtDescription = memo.TextBox;
        cardDesc.Body.Controls.Add(memo, 0, 0);

        // ───────────── صفحه و نوار دکمه‌ها ─────────────
        var cards = new Control[] { cardMain, cardHolder, cardDocs, cardDesc };
        formLayout = new ModernPage();
        foreach (var c in cards) formLayout.AddCard(c, CardGap);
        _contentHeight = cards.Sum(c => c.Height + CardGap) + formLayout.Padding.Vertical;

        btnSave = ModernButton.Success(_isEdit ? "💾 ذخیره تغییرات" : "💾 ثبت مال", 190, 40);
        btnSave.Click += async (s, e) => await SaveSafeAsync();

        btnAttach = ModernButton.Outline("📎 پیوست‌ها", ModernTheme.Neutral, 130, 40);
        btnAttach.Click += (s, e) => OpenAttachments();
        btnAttach.Visible = _isEdit;   // پیوست نیازمند شناسه‌ی ذخیره‌شده است

        var cancel = ModernButton.Cancel();
        cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var bar = ModernForm.CreateButtonBar(btnSave, btnAttach, cancel);
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight));
        root.Controls.Add(formLayout, 0, 0);
        root.Controls.Add(bar, 0, 1);
        Controls.Add(root);
    }

    // ───────────── بارگذاری ─────────────
    private async Task LoadDataAsync()
    {
        if (IsDisposed) return;
        try
        {
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                var deptTask = _repo.GetDepartmentsAsync();
                var empTask = _repo.GetEmployeesAsync();
                var catTask = _repo.GetCategoriesAsync();
                await Task.WhenAll(deptTask, empTask, catTask);
                if (IsDisposed) return;

                var depts = new List<IdName> { new() { Id = 0, Name = "--- بدون واحد ---" } };
                depts.AddRange(deptTask.Result);
                cmbDept.DataSource = depts; cmbDept.DisplayMember = "Name"; cmbDept.ValueMember = "Id";

                var emps = new List<IdName> { new() { Id = 0, Name = "--- تحویل داده نشده ---" } };
                emps.AddRange(empTask.Result);
                cmbEmp.DataSource = emps; cmbEmp.DisplayMember = "Name"; cmbEmp.ValueMember = "Id";

                cmbCategory.Items.Clear();
                cmbCategory.Items.Add("");   // بدون دسته
                cmbCategory.Items.AddRange(catTask.Result.Cast<object>().ToArray());

                if (_isEdit && _assetId.HasValue)
                {
                    var a = await _repo.GetByIdAsync(_assetId.Value);
                    if (a is null || IsDisposed) return;
                    _links = await _repo.GetLinksAsync(a.Id);

                    txtCode.Text = a.AssetCode;
                    txtName.Text = a.Name;
                    // دسته‌ی قدیمی که در لیست Lookup نیست هم نمایش داده شود تا هنگام ذخیره از بین نرود
                    if (!string.IsNullOrWhiteSpace(a.Category) && !cmbCategory.Items.Contains(a.Category))
                        cmbCategory.Items.Add(a.Category);
                    cmbCategory.SelectedItem = string.IsNullOrWhiteSpace(a.Category) ? "" : a.Category;
                    txtSerial.Text = a.SerialNumber ?? "";
                    cmbStatus.SelectedItem = a.Status;
                    if (a.PurchaseDate.HasValue) txtDate.Text = DateHelper.ToPersianDate(a.PurchaseDate.Value);
                    if (a.PurchaseAmount > 0) txtAmount.Text = NumberHelper.FormatThousands(a.PurchaseAmount, usePersianDigits: true);
                    if (a.DepartmentId.HasValue) cmbDept.SelectedValue = a.DepartmentId.Value;
                    if (a.EmployeeId.HasValue) cmbEmp.SelectedValue = a.EmployeeId.Value;
                    txtLocation.Text = a.Location ?? "";
                    txtDescription.Text = a.Description ?? "";
                }
                RebindDocs();
            }, "در حال بارگذاری اطلاعات...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AssetEditForm.LoadDataAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    // ───────────── اسناد خرید ─────────────
    private void RebindDocs()
    {
        dgvDocs.DataSource = null;
        dgvDocs.DataSource = _links.ToList();
        dgvDocs.ClearSelection();
    }

    private static string BuildDisplay(PurchaseDocItem d)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(d.DocNumber)) parts.Add("شماره " + d.DocNumber);
        if (d.Date.HasValue) parts.Add(DateHelper.ToPersianDate(d.Date.Value));
        if (!string.IsNullOrWhiteSpace(d.Party)) parts.Add(d.Party);
        parts.Add(NumberHelper.FormatThousands(d.Amount, usePersianDigits: true) + " ریال");
        return string.Join("  |  ", parts);
    }

    private void AddDocs()
    {
        using var picker = new AssetPurchasePickerForm(_connectionString, _links);
        if (picker.ShowDialog(this) != DialogResult.OK) return;

        bool wasEmpty = _links.Count == 0;
        foreach (var d in picker.SelectedDocs)
            _links.Add(new AssetPurchaseLink { DocType = d.DocType, DocId = d.DocId, DocDisplay = BuildDisplay(d), Amount = d.Amount });
        RebindDocs();

        // اگر مبلغ/تاریخ خرید خالی است و فقط یک سند اضافه شد، از همان سند پر می‌شود
        if (wasEmpty && picker.SelectedDocs.Count == 1)
        {
            var d = picker.SelectedDocs[0];
            bool amountEmpty = !NumberHelper.TryParseFormatted(txtAmount.Text, out decimal cur) || cur <= 0;
            if (amountEmpty && d.Amount > 0) txtAmount.Text = NumberHelper.FormatThousands(d.Amount, usePersianDigits: true);
            if (!txtDate.Text.Any(char.IsDigit) && d.Date.HasValue) txtDate.Text = DateHelper.ToPersianDate(d.Date.Value);
        }
    }

    private void RemoveDocs()
    {
        var selected = dgvDocs.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.DataBoundItem as AssetPurchaseLink).Where(l => l is not null).ToList();
        if (selected.Count == 0) { ModernMessageBox.ShowWarning("سندی برای حذف انتخاب نشده است."); return; }
        foreach (var l in selected) _links.RemoveAll(x => x.DocType == l!.DocType && x.DocId == l.DocId);
        RebindDocs();
    }

    private void OpenAttachments()
    {
        if (!_assetId.HasValue) return;
        using var f = new AttachmentsForm(_connectionString, "Asset", _assetId.Value, $"پیوست‌های مال {txtCode.Text}");
        f.ShowDialog(this);
    }

    // ───────────── ذخیره ─────────────
    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            string name = txtName.Text.Trim();
            if (name.Length == 0) { ModernMessageBox.ShowWarning("نام مال الزامی است."); return; }

            string code = txtCode.Text.Trim();
            if (code.Length > 0 && await _repo.CodeExistsAsync(code, _assetId ?? 0))
            { ModernMessageBox.ShowWarning("این کد اموال قبلاً برای مال دیگری ثبت شده است."); return; }

            decimal amount = 0;
            if (!string.IsNullOrWhiteSpace(txtAmount.Text) && !NumberHelper.TryParseFormatted(txtAmount.Text, out amount))
            { ModernMessageBox.ShowWarning("مبلغ خرید نامعتبر است."); return; }

            DateTime? purchaseDate = null;
            if (txtDate.Text.Any(char.IsDigit))
            {
                purchaseDate = DateHelper.ParsePersianDate(txtDate.Text);
                if (purchaseDate is null) { ModernMessageBox.ShowWarning("تاریخ خرید نامعتبر است."); return; }
            }

            var dept = cmbDept.SelectedItem as IdName;
            var emp = cmbEmp.SelectedItem as IdName;

            var asset = new Asset
            {
                Id = _assetId ?? 0,
                AssetCode = code,
                Name = name,
                Category = (cmbCategory.SelectedItem as string)?.Trim() ?? "",
                SerialNumber = txtSerial.Text.Trim(),
                PurchaseDate = purchaseDate,
                PurchaseAmount = amount,
                Status = cmbStatus.SelectedItem?.ToString() ?? AssetStatuses.Active,
                DepartmentId = dept is not null && dept.Id > 0 ? dept.Id : null,
                DepartmentName = dept is not null && dept.Id > 0 ? dept.Name : null,
                EmployeeId = emp is not null && emp.Id > 0 ? emp.Id : null,
                EmployeeName = emp is not null && emp.Id > 0 ? emp.Name : null,
                Location = txtLocation.Text.Trim(),
                Description = txtDescription.Text.Trim()
            };

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repo.SaveAsync(asset, _links);
            }, "در حال ذخیره...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("مال با موفقیت ذخیره شد.", true);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AssetEditForm.SaveSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
        finally { try { _actionLock.Release(); } catch (Exception ex) { LogHelper.LogException(ex, "AssetEditForm"); } }
    }
}
