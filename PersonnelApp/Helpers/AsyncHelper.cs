using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class AsyncHelper
    {
        private static bool CanShowOverlay(Form? form) => form != null && !form.IsDisposed && !form.Disposing;

        public static async Task<bool> RunWithLoading(
            Form? parentForm, Func<Task> action, string message = "لطفاً صبر کنید...")
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (!CanShowOverlay(parentForm))
            {
                try
                {
                    await action().ConfigureAwait(true);
                    return true;
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "AsyncHelper");
                    try { ModernMessageBox.ShowError($"خطایی رخ داد:\n{ex.Message}"); } catch { }
                    return false;
                }
            }

            using var overlay = CreateOverlay(parentForm!, message);
            overlay.Show(parentForm);
            parentForm!.Enabled = false;

            // ✅ این خط باعث می‌شود UI فرصت کند تا پروسیگ را رسم کند
            await Task.Yield();

            bool success = false;
            try
            {
                await action().ConfigureAwait(true);
                success = true;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AsyncHelper");
                try { ModernMessageBox.ShowError($"خطایی رخ داد:\n{ex.Message}"); } catch { }
            }
            finally
            {
                if (parentForm != null && !parentForm.IsDisposed) parentForm.Enabled = true;
                if (!overlay.IsDisposed) overlay.Close();
            }
            return success;
        }

        public static async Task<(bool Success, T? Result)> RunWithLoading<T>(
            Form? parentForm, Func<Task<T>> action, string message = "لطفاً صبر کنید...")
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (!CanShowOverlay(parentForm))
            {
                try
                {
                    T result = await action().ConfigureAwait(true);
                    return (true, result);
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "AsyncHelper");
                    return (false, default);
                }
            }

            using var overlay = CreateOverlay(parentForm!, message);
            overlay.Show(parentForm);
            parentForm!.Enabled = false;

            // ✅ این خط باعث می‌شود UI فرصت کند تا پروسیگ را رسم کند
            await Task.Yield();

            try
            {
                T result = await action().ConfigureAwait(true);
                return (true, result);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AsyncHelper");
                try { ModernMessageBox.ShowError($"خطایی رخ داد:\n{ex.Message}"); } catch { }
                return (false, default);
            }
            finally
            {
                if (parentForm != null && !parentForm.IsDisposed) parentForm.Enabled = true;
                if (!overlay.IsDisposed) overlay.Close();
            }
        }

        public static async Task<bool> RunCpuBound(
            Form? parentForm, Action action, string message = "لطفاً صبر کنید...")
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (!CanShowOverlay(parentForm))
            {
                try
                {
                    await Task.Run(action).ConfigureAwait(true);
                    return true;
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "AsyncHelper");
                    try { ModernMessageBox.ShowError($"خطایی رخ داد:\n{ex.Message}"); } catch { }
                    return false;
                }
            }

            using var overlay = CreateOverlay(parentForm!, message);
            overlay.Show(parentForm);
            parentForm!.Enabled = false;

            // ✅ این خط باعث می‌شود UI فرصت کند تا پروسیگ را رسم کند
            await Task.Yield();

            bool success = false;
            try
            {
                await Task.Run(action).ConfigureAwait(true);
                success = true;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "AsyncHelper");
                try { ModernMessageBox.ShowError($"خطایی رخ داد:\n{ex.Message}"); } catch { }
            }
            finally
            {
                if (parentForm != null && !parentForm.IsDisposed) parentForm.Enabled = true;
                if (!overlay.IsDisposed) overlay.Close();
            }
            return success;
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

            void sync(object? s, EventArgs e)
            {
                if (overlay.IsDisposed || parent.IsDisposed) return;
                try
                {
                    overlay.Size = parent.ClientSize;
                    overlay.Location = parent.PointToScreen(Point.Empty);
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
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                Location = new Point(60, 35),
                Size = new Size(240, 30),
                TextAlign = ContentAlignment.MiddleRight
            };

            pnlCenter.Controls.Add(picSpinner);
            pnlCenter.Controls.Add(lblMessage);
            overlay.Controls.Add(pnlCenter);
            return overlay;
        }
    }
}