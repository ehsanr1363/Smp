using System;
using System.Drawing;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════
// جابه‌جایی بین چند نما (مثلاً قراردادی / روزمزد / ساعتی):
// دکمه‌های گرد بالا + کارت سفید که هر نما در آن قرار می‌گیرد.
// ═══════════════════════════════════════════════════════════════
public sealed class PillSwitcher : Panel
{
    private readonly string[] _titles;
    private readonly ModernButton[] _buttons;
    private readonly Panel[] _views;
    private readonly Color _accent;
    private int _selected = -1;

    public event EventHandler? SelectedIndexChanged;

    public int SelectedIndex { get => _selected; set => Select(value); }
    public string SelectedTitle => _selected >= 0 ? _titles[_selected] : string.Empty;
    public int Count => _titles.Length;

    public PillSwitcher(string[] titles, Color accent)
    {
        _titles = titles;
        _accent = accent;
        BackColor = Color.Transparent;

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            FlowDirection = FlowDirection.LeftToRight,   // فرم Mirror: اولین دکمه در راست
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 6),
            BackColor = Color.Transparent
        };

        var card = new ModernCard("", accent, new[] { new ColumnStyle(SizeType.Percent, 100F) });
        var stack = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = Padding.Empty };
        card.Body.Controls.Add(stack, 0, 0);

        _buttons = new ModernButton[titles.Length];
        _views = new Panel[titles.Length];
        for (int i = 0; i < titles.Length; i++)
        {
            int idx = i;
            var btn = ModernButton.Create(titles[i], ModernTheme.Neutral, 150, 34, ModernButtonStyle.Outline);
            btn.Margin = new Padding(0, 0, 8, 0);
            btn.Click += (s, e) => Select(idx);
            _buttons[i] = btn;
            bar.Controls.Add(btn);
            FitButton(i);

            var view = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Visible = false };
            _views[i] = view;
            stack.Controls.Add(view);
        }

        Controls.Add(card);   // Fill (اول)
        Controls.Add(bar);    // Top
        Select(0);
    }

    public void AddToView(int index, Control c)
    {
        c.Dock = DockStyle.Fill;
        _views[index].Controls.Add(c);
    }

    /// <summary>تعداد ردیف‌ها کنار عنوان دکمه نمایش داده می‌شود</summary>
    public void SetCount(int index, int count)
    {
        _buttons[index].Text = $"{_titles[index]}  ({NumberHelper.FormatThousands(count, usePersianDigits: true)})";
        FitButton(index);
    }

    private void FitButton(int i)
    {
        _buttons[i].Width = Math.Max(110, TextRenderer.MeasureText(_buttons[i].Text, _buttons[i].Font).Width + 44);
    }

    private void Select(int index)
    {
        if (index < 0 || index >= _views.Length || index == _selected) return;
        _selected = index;
        for (int i = 0; i < _views.Length; i++)
        {
            bool on = i == index;
            _views[i].Visible = on;
            if (on) _views[i].BringToFront();
            _buttons[i].Accent = on ? _accent : ModernTheme.Neutral;
            _buttons[i].Variant = on ? ModernButtonStyle.Filled : ModernButtonStyle.Outline;
        }
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }
}
