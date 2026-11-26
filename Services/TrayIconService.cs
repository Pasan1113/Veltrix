using System;
using System.Drawing;
using Forms = System.Windows.Forms;

namespace Veltrix.Services
{
    public sealed class TrayIconService : IDisposable
    {
        private readonly Forms.NotifyIcon _notifyIcon;

        private readonly Forms.ContextMenuStrip _contextMenu;

        private readonly Forms.ToolStripMenuItem _miniMeterItem;

        private bool _disposed;

        public event Action? OpenDashboardRequested;

        public event Action? ToggleMiniMeterRequested;

        public event Action? ExitRequested;

        public TrayIconService()
        {
            _contextMenu = new Forms.ContextMenuStrip
            {
                BackColor = Color.FromArgb(21, 30, 48),
                ForeColor = Color.White,
                ShowImageMargin = false,
                Renderer = new DarkMenuRenderer()
            };

            var openItem = new Forms.ToolStripMenuItem(
                "Open Dashboard");

            openItem.Click += (_, _) =>
                OpenDashboardRequested?.Invoke();

            _miniMeterItem = new Forms.ToolStripMenuItem(
                "Enable Mini Speed Meter");

            _miniMeterItem.Click += (_, _) =>
                ToggleMiniMeterRequested?.Invoke();

            var exitItem = new Forms.ToolStripMenuItem(
                "Exit Veltrix");

            exitItem.ForeColor = Color.FromArgb(
                248, 113, 113);

            exitItem.Click += (_, _) =>
                ExitRequested?.Invoke();

            _contextMenu.Items.Add(openItem);

            _contextMenu.Items.Add(
                new Forms.ToolStripSeparator());

            _contextMenu.Items.Add(_miniMeterItem);

            _contextMenu.Items.Add(
                new Forms.ToolStripSeparator());

            _contextMenu.Items.Add(exitItem);

            _notifyIcon = new Forms.NotifyIcon
            {
                Text = "Veltrix - Network Monitor",

                Icon = new System.Drawing.Icon(
    System.Windows.Application.GetResourceStream(
        new System.Uri(
            "pack://application:,,,/Assets/Veltrix.ico"
        )
    ).Stream
),

                ContextMenuStrip = _contextMenu,

                Visible = true
            };

            _notifyIcon.DoubleClick += (_, _) =>
                OpenDashboardRequested?.Invoke();
        }

        public void SetMiniMeterEnabled(bool enabled)
        {
            _miniMeterItem.Text = enabled
                ? "Disable Mini Speed Meter"
                : "Enable Mini Speed Meter";
        }

        public void ShowBackgroundNotification()
        {
            _notifyIcon.ShowBalloonTip(
                3000,
                "Veltrix is running",
                "Network monitoring continues in the background.",
                Forms.ToolTipIcon.Info);
        }

        public void UpdateSpeed(
            string download,
            string upload)
        {
            string text =
                $"Veltrix\nDL: {download}\nUL: {upload}";

            _notifyIcon.Text = text.Length > 63
                ? text.Substring(0, 63)
                : text;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _notifyIcon.Visible = false;

            _notifyIcon.Dispose();

            _contextMenu.Dispose();
        }

        private class DarkMenuRenderer
            : Forms.ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer()
                : base(new DarkColorTable())
            {
            }
        }

        private class DarkColorTable
            : Forms.ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground =>
                Color.FromArgb(21, 30, 48);

            public override Color MenuItemSelected =>
                Color.FromArgb(38, 52, 73);

            public override Color MenuItemBorder =>
                Color.FromArgb(51, 65, 85);

            public override Color MenuBorder =>
                Color.FromArgb(51, 65, 85);

            public override Color SeparatorDark =>
                Color.FromArgb(51, 65, 85);

            public override Color SeparatorLight =>
                Color.FromArgb(51, 65, 85);

            public override Color ImageMarginGradientBegin =>
                Color.FromArgb(21, 30, 48);

            public override Color ImageMarginGradientMiddle =>
                Color.FromArgb(21, 30, 48);

            public override Color ImageMarginGradientEnd =>
                Color.FromArgb(21, 30, 48);
        }
    }
}