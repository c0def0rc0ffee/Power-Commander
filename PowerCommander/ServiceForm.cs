using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PowerCommanderSettings = PowerCommander.PowerCommanderSettings;

namespace PowerCommander
{
    /// <summary>
    /// The system tray-based background form responsible for scheduled shutdown logic.
    /// </summary>
    public partial class ServiceForm : Form
    {
        #region Fields

        private const int DefaultCountdownSeconds = 600;
        private int secondsLeft = DefaultCountdownSeconds;

        private NotifyIcon trayIcon;
        private Timer countdownTimer;
        private Timer scheduleChecker;
        private Timer refreshTimer;
        private PowerCommanderSettings settings;
        private bool isShutdownPromptVisible;

        private const string AppName = "Power Commander";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        #endregion

        #region Constructor

        /// <summary>
        /// Initialises the ServiceForm, sets up tray icon and schedule monitor.
        /// </summary>
        public ServiceForm()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;

            settings = SettingsLoader.Load();
            InitialiseTrayIcon();
            StartScheduleMonitor();
        }

        #endregion

        #region Tray Icon Setup
        private void SetRunOnStartup(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true) ??
                                   Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null)
                    {
                        Debug.WriteLine("Failed to access or create the Run registry key.");
                        return;
                    }

                    string exePath = $"\"{Application.ExecutablePath}\" --silent";

                    if (enable)
                        key.SetValue(AppName, exePath);
                    else
                        key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting Run on startup: {ex.Message}");
            }
        }

        private bool IsRunOnStartupEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false) ??
                                   Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null)
                    {
                        Debug.WriteLine("Failed to access or create the Run registry key.");
                        return false;
                    }

                    string exePath = $"\"{Application.ExecutablePath}\" --silent";
                    string value = key.GetValue(AppName) as string;
                    return value == exePath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking Run on startup: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Configures the system tray icon and context menu.
        /// </summary>
        private void InitialiseTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = Properties.Resources.powerYellow,
                Visible = true,
                Text = "Power Commander"
            };

            var trayMenu = new ContextMenuStrip();

            if (settings.EnableStartupToggle)
            {
                var runOnStartupItem = new ToolStripMenuItem("Run on startup")
                {
                    Checked = IsRunOnStartupEnabled(),
                    CheckOnClick = true
                };

                runOnStartupItem.Click += (_, __) =>
                {
                    SetRunOnStartup(runOnStartupItem.Checked);
                };

                trayMenu.Items.Add(runOnStartupItem);
            }
            
            // Show next scheduled time (read-only label)
            if (settings.ShowNextScheduledTime)
            {
                string nextTime = GetNextShutdownTime();
                var nextItem = new ToolStripMenuItem($"Next shutdown: {nextTime}")
                {
                    Enabled = false
                };
                trayMenu.Items.Add(nextItem);

                // Refresh the label every minute
                if (refreshTimer == null)
                {
                    refreshTimer = new Timer { Interval = 60000 };
                    refreshTimer.Tick += (s, e) =>
                    {
                        nextItem.Text = $"Next shutdown: {GetNextShutdownTime()}";
                    };
                }

                refreshTimer.Start();
            }

            // Optional manual shutdown
            if (settings.EnableManualShutdown)
            {
                trayMenu.Items.Add("Shutdown Now", null, (_, __) => TriggerShutdownSequence());
            }

            // Optional exit
            if (settings.EnableExitOption)
            {
                trayMenu.Items.Add("Exit", null, (_, __) => TriggerAppExit());
            }

            trayIcon.ContextMenuStrip = trayMenu;
        }

        #endregion

        #region Schedule Info

        /// <summary>
        /// Returns the next upcoming scheduled shutdown time, or "None" if none found.
        /// </summary>
        private string GetNextShutdownTime()
        {
            if (settings.ShutdownTimes == null || settings.ShutdownTimes.Count == 0)
                return "None";

            var now = DateTime.Now;

            var nextTime = settings.ShutdownTimes
                .Select(t => TimeSpan.TryParse(t, out var ts) ? DateTime.Today.Add(ts) : (DateTime?)null)
                .Where(dt => dt.HasValue)
                .Select(dt => dt.Value <= now ? dt.Value.AddDays(1) : dt.Value)
                .OrderBy(dt => dt)
                .FirstOrDefault();

            if (nextTime == default)
                return "None";

            var timeString = nextTime.ToString("HH:mm");
            return nextTime.Date > DateTime.Today ? $"{timeString} tomorrow" : timeString;
        }

        #endregion

        #region Schedule Monitoring

        /// <summary>
        /// Starts a timer that checks the current time against configured shutdown times.
        /// </summary>
        private void StartScheduleMonitor()
        {
            settings = SettingsLoader.Load();
            
            scheduleChecker = new Timer { Interval = 60000 }; // Every 1 minute
            scheduleChecker.Tick += (s, e) =>
            {
                var updatedSettings = SettingsLoader.Load();
                var now = DateTime.Now;

                bool shouldShutdown = updatedSettings.ShutdownTimes?.Any(t =>
                {
                    if (TimeSpan.TryParse(t, out var targetTime))
                    {
                        var scheduledTime = DateTime.Today.Add(targetTime);
                        return Math.Abs((now - scheduledTime).TotalSeconds) <= 30;
                    }
                    return false;
                }) == true;

                if (shouldShutdown)
                {
                    ShowShutdownPrompt();
                }

                settings = updatedSettings; // update reference for the rest of the form
            };

            scheduleChecker.Start();
        }


        #endregion

        #region Shutdown Prompt

        /// <summary>
        /// Displays the shutdown confirmation prompt with a countdown.
        /// </summary>
        private void ShowShutdownPrompt()
        {
            if (isShutdownPromptVisible)
                return;

            isShutdownPromptVisible = true;

            // Show tray notification
            trayIcon.BalloonTipTitle = "Shutdown Pending";
            trayIcon.BalloonTipText = $"System will shut down in {secondsLeft} seconds.";
            trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
            trayIcon.Visible = true;
            trayIcon.ShowBalloonTip(50000);

            var prompt = new Form
            {
                Width = 300,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Shutdown Pending",
                StartPosition = FormStartPosition.CenterScreen,
                TopMost = true
            };

            var label = new Label
            {
                Text = $"Shutting down in {secondsLeft} seconds...",
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10),
                Height = 40
            };

            var cancelButton = new Button
            {
                Text = "Cancel Shutdown",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            cancelButton.Click += (_, __) =>
            {
                prompt.Close();
            };

            prompt.Controls.Add(label);
            prompt.Controls.Add(cancelButton);

            countdownTimer = new Timer { Interval = 1000 };
            countdownTimer.Tick += (s, e) =>
            {
                secondsLeft--;
                label.Text = $"Shutting down in {secondsLeft} seconds...";
                if (secondsLeft <= 0)
                {
                    prompt.Close();
                    ShutdownMachine();
                }
            };

            countdownTimer.Start();

            prompt.FormClosed += (_, __) =>
            {
                if (countdownTimer != null)
                {
                    countdownTimer.Stop();
                    countdownTimer.Dispose();
                    countdownTimer = null;
                }
                secondsLeft = DefaultCountdownSeconds;
                isShutdownPromptVisible = false;
            };

            prompt.ShowDialog();
            prompt.Activate();
        }

        #endregion

        #region Shutdown Logic

        /// <summary>
        /// Displays a confirmation dialog with the provided message.
        /// </summary>
        private DialogResult ConfirmAction(string message)
        {
            return MessageBox.Show(message, "Confirm Action", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }

        /// <summary>
        /// Immediately triggers system shutdown, bypassing the countdown.
        /// </summary>
        private void TriggerShutdownSequence()
        {
            if (ConfirmAction("Are you sure you wish to shut down your PC?") == DialogResult.Yes)
            {
                secondsLeft = 0;
                ShutdownMachine();
            }
        }

        /// <summary>
        /// Immediately triggers system shutdown, bypassing the countdown.
        /// </summary>
        private void TriggerAppExit()
        {
            if (ConfirmAction("Are you sure you wish to exit?") == DialogResult.Yes)
            {
                Application.Exit();
            }
        }


        /// <summary>
        /// Executes the system shutdown command.
        /// </summary>
        private void ShutdownMachine()
        {
            try
            {
                Process.Start("shutdown", "/s /t 0");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Shutdown failed: " + ex.Message);
            }
        }

        #endregion

        #region Form Events

        /// <summary>
        /// Cleans up the tray icon on form close.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Ensure the tray icon is removed and disposed
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }

            // Dispose active timers to release resources
            if (countdownTimer != null)
            {
                countdownTimer.Stop();
                countdownTimer.Dispose();
                countdownTimer = null;
            }

            if (scheduleChecker != null)
            {
                scheduleChecker.Stop();
                scheduleChecker.Dispose();
                scheduleChecker = null;
            }

            if (refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer.Dispose();
                refreshTimer = null;
            }

            base.OnFormClosing(e);
        }

        #endregion

        private void ServiceForm_Load(object sender, EventArgs e)
        {

        }
    }
}
