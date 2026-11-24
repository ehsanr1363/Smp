using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Helpers;

public static class AsyncHelper
{
    private static readonly ConditionalWeakTable<Form, StrongBox<int>> _depthTracker = new();

    private static bool CanShowOverlay(Form? form) => form is not null && !form.IsDisposed && !form.Disposing;

    private static StrongBox<int> GetDepth(Form form) =>
        _depthTracker.GetValue(form, _ => new StrongBox<int>(0));

    public static async Task<bool> RunWithLoading(
        Form? parentForm, Func<Task> action, string message = "لطفاً صبر کنید...")
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        if (!CanShowOverlay(parentForm))
        {
            try { await action().ConfigureAwait(true); return true; }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AsyncHelper");
                try { ModernMessageBox.ShowError($"خطایی رخ داد:\n{ex.Message}"); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AsyncHelper"); }
                return false;
            }
        }

        var depth = GetDepth(parentForm!);

        if (depth.Value > 0)
        {
            try { await action().ConfigureAwait(true); return true; }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AsyncHelper");
                try { ModernMessageBox.ShowError($"خطایی رخ داد:\n{ex.Message}"); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AsyncHelper"); }
                return false;
            }
        }

        depth.Value = 1;
        Form? overlay = null;
        try
        {
            overlay = CreateOverlay(parentForm!, message);
            overlay.Show(parentForm);
            parentForm!.Enabled = false;
            await Task.Yield();

            try
            {
                await action().ConfigureAwait(true);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AsyncHelper");
                try { ModernMessageBox.ShowError($"خطایی رخ داد:\n{ex.Message}"); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AsyncHelper"); }
                return false;
            }
            finally
            {
                if (parentForm is not null && !parentForm.IsDisposed) parentForm.Enabled = true;
                if (overlay is not null && !overlay.IsDisposed) overlay.Close();
            }
        }
        finally
        {
            depth.Value = 0;
            if (overlay is not null && !overlay.IsDisposed)
            {
                try { overlay.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AsyncHelper"); }
            }
        }
    }

    public static async Task<(bool Success, T? Result)> RunWithLoading<T>(
        Form? parentForm, Func<Task<T>> action, string message = "لطفاً صبر کنید...")
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        if (!CanShowOverlay(parentForm))
        {
            try { T result = await action().ConfigureAwait(true); return (true, result); }
            catch (Exception ex) { LogHelper.LogException(ex, "AsyncHelper"); return (false, default); }
        }

        var depth = GetDepth(parentForm!);

        if (depth.Value > 0)
        {
            try { T result = await action().ConfigureAwait(true); return (true, result); }
            catch (Exception ex) { LogHelper.LogException(ex, "AsyncHelper"); return (false, default); }
        }

        depth.Value = 1;
        Form? overlay = null;
        try
        {
            overlay = CreateOverlay(parentForm!, message);
            overlay.Show(parentForm);
            parentForm!.Enabled = false;
            await Task.Yield();

            try
            {
                T result = await action().ConfigureAwait(true);
                return (true, result);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AsyncHelper");
                try { ModernMessageBox.ShowError($"خطایی رخ داد:\n{ex.Message}"); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AsyncHelper"); }
                return (false, default);
            }
            finally
            {
                if (parentForm is not null && !parentForm.IsDisposed) parentForm.Enabled = true;
                if (overlay is not null && !overlay.IsDisposed) overlay.Close();
            }
        }
        finally
        {
            depth.Value = 0;
            if (overlay is not null && !overlay.IsDisposed)
            {
                try { overlay.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AsyncHelper"); }
            }
        }
    }

    public static async Task<bool> RunCpuBound(
        Form? parentForm, Action action, string message = "لطفاً صبر کنید...")
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        if (!CanShowOverlay(parentForm))
        {
            try { await Task.Run(action).ConfigureAwait(true); return true; }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AsyncHelper");
                try { ModernMessageBox.ShowError($"خطایی رخ داد:\n{ex.Message}"); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AsyncHelper"); }
                return false;
            }
        }

        var depth = GetDepth(parentForm!);

        if (depth.Value > 0)
        {
            try { await Task.Run(action).ConfigureAwait(true); return true; }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AsyncHelper");
                try { ModernMessageBox.ShowError($"خطایی رخ داد:\n{ex.Message}"); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AsyncHelper"); }
                return false;
            }
        }

        depth.Value = 1;
        Form? overlay = null;
        try
        {
            overlay = CreateOverlay(parentForm!, message);
            overlay.Show(parentForm);
            parentForm!.Enabled = false;
            await Task.Yield();

            try
            {
                await Task.Run(action).ConfigureAwait(true);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AsyncHelper");
                try { ModernMessageBox.ShowError($"خطایی رخ داد:\n{ex.Message}"); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AsyncHelper"); }
                return false;
            }
            finally
            {
                if (parentForm is not null && !parentForm.IsDisposed) parentForm.Enabled = true;
                if (overlay is not null && !overlay.IsDisposed) overlay.Close();
            }
        }
        finally
        {
            depth.Value = 0;
            if (overlay is not null && !overlay.IsDisposed)
            {
                try { overlay.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "AsyncHelper"); }
            }
        }
    }

    private static Form CreateOverlay(Form parent, string message)
    {
        var overlay = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.Black,
            Opacity = 0.5,
            StartPosition = FormStartPosition.Manual,
            Size = parent.ClientSize,
            Location = parent.PointToScreen(Point.Empty),
            ShowInTaskbar = false,
            TopMost = true
        };

        var pnlCenter = new Panel
        {
            Size = new Size(320, 100),
            BackColor = Color.White,
            Location = new Point((overlay.Width - 320) / 2, (overlay.Height - 100) / 2)
        };
        pnlCenter.Paint += (s, e) =>
        {
            ControlPaint.DrawBorder(e.Graphics, pnlCenter.ClientRectangle,
                Color.FromArgb(59, 130, 246), 2, ButtonBorderStyle.Solid,
                Color.Empty, 0, ButtonBorderStyle.None,
                Color.Empty, 0, ButtonBorderStyle.None,
                Color.Empty, 0, ButtonBorderStyle.None);
        };

        var picSpinner = new PictureBox
        {
            Size = new Size(32, 32),
            Location = new Point(20, 34),
            Image = SystemIcons.Application.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom
        };

        var lblMessage = new Label
        {
            Text = message,
            Font = ModernTheme.GetFont(12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(60, 60, 60),
            Location = new Point(60, 35),
            Size = new Size(240, 30),
            TextAlign = ContentAlignment.MiddleRight
        };

        pnlCenter.Controls.Add(picSpinner);
        pnlCenter.Controls.Add(lblMessage);
        overlay.Controls.Add(pnlCenter);

        // ✅ FIX: به‌روزرسانی موقعیت پنل وسط در صورت تغییر سایز فرم
        void sync(object? s, EventArgs e)
        {
            if (overlay.IsDisposed || parent.IsDisposed) return;
            try
            {
                overlay.Size = parent.ClientSize;
                overlay.Location = parent.PointToScreen(Point.Empty);
                // محاسبه مجدد مرکز پنل لودینگ
                pnlCenter.Location = new Point((overlay.Width - 320) / 2, (overlay.Height - 100) / 2);
            }
            catch (ObjectDisposedException) { }
        }

        parent.Move += sync;
        parent.Resize += sync;

        parent.FormClosed += (s, e) =>
        {
            parent.Move -= sync;
            parent.Resize -= sync;
            if (!overlay.IsDisposed) overlay.Close();
        };

        overlay.FormClosed += (s, e) =>
        {
            parent.Move -= sync;
            parent.Resize -= sync;
        };

        return overlay;
    }
}
