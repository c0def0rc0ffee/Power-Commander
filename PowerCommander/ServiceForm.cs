using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

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
        private PowerCommanderSettings settings;

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

            settings = LoadSettings();
            InitialiseTrayIcon();
            StartScheduleMonitor();
        }

        #endregion

        #region Tray Icon Setup

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

            // Show next scheduled time (read-only label)
            if (settings.ShowNextScheduledTime)
            {
                string nextTime = GetNextShutdownTime();
                var nextItem = new ToolStripMenuItem($"Next shutdown: {nextTime}")
                {
                    Enabled = false
                };
                trayMenu.Items.Add(nextItem);

                // Optional: refresh every minute
                var refreshTimer = new Timer { Interval = 60000 };
                refreshTimer.Tick += (s, e) =>
                {
                    nextItem.Text = $"Next shutdown: {GetNextShutdownTime()}";
                };
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
                trayMenu.Items.Add("Exit", null, (_, __) => Application.Exit());
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

            var upcomingTimes = settings.ShutdownTimes
                .Select(t => TimeSpan.TryParse(t, out var ts) ? DateTime.Today.Add(ts) : DateTime.MaxValue)
                .Where(dt => dt > now)
                .OrderBy(dt => dt)
                .ToList();

            return upcomingTimes.Count == 0 ? "None today" : upcomingTimes.First().ToString("HH:mm");
        }

        #endregion

        #region Schedule Monitoring

        /// <summary>
        /// Starts a timer that checks the current time against configured shutdown times.
        /// </summary>
        private void StartScheduleMonitor()
        {
            settings = LoadSettings();

            scheduleChecker = new Timer { Interval = 60000 }; // Every 1 minute
            scheduleChecker.Tick += (s, e) =>
            {
                var updatedSettings = LoadSettings();
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

        #region Settings Handling

        /// <summary>
        /// Loads shutdown schedule settings from a JSON file, or creates a default one.
        /// </summary>
        /// <returns>Populated settings object.</returns>
        private PowerCommanderSettings LoadSettings()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

            if (!File.Exists(path))
            {
                var defaultSettings = new PowerCommanderSettings
                {
                    LoadService = true,
                    ShutdownTimes = new List<string> { "21:00" }
                };

                string defaultJson = JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, defaultJson);

                return defaultSettings;
            }

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PowerCommanderSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        #endregion

        #region Shutdown Prompt

        /// <summary>
        /// Displays the shutdown confirmation prompt with a countdown.
        /// </summary>
        private void ShowShutdownPrompt()
        {
            var prompt = new Form
            {
                Width = 300,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Shutdown Pending",
                StartPosition = FormStartPosition.CenterScreen
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
                countdownTimer.Stop();
                prompt.Close();
                secondsLeft = DefaultCountdownSeconds;
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
                    countdownTimer.Stop();
                    prompt.Close();
                    ShutdownMachine();
                }
            };

            countdownTimer.Start();
            prompt.ShowDialog();
        }

        #endregion

        #region Shutdown Logic

        /// <summary>
        /// Immediately triggers system shutdown, bypassing the countdown.
        /// </summary>
        private void TriggerShutdownSequence()
        {
            if (MessageBox.Show("Are you sure you wish to shut down your PC?", "Confirm Shutdown", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                secondsLeft = 0;
                ShutdownMachine();
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
            trayIcon.Visible = false;
            base.OnFormClosing(e);
        }

        #endregion

        private void ServiceForm_Load(object sender, EventArgs e)
        {

        }
    }
}
