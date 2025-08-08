using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace PowerCommander
{
    /// <summary>
    /// The main application form used when Power Commander is running interactively.
    /// </summary>
    public partial class MainForm : Form
    {
        #region Fields

        private PowerCommanderSettings settings;
        private ServiceForm serviceFormInstance;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs MainForm and hooks into the Load event.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
            this.Load += MainForm_Load;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Button event to manually show the ServiceForm from the UI.
        /// </summary>
        private void btnService_Click(object sender, EventArgs e)
        {
            if (serviceFormInstance == null || serviceFormInstance.IsDisposed)
            {
                serviceFormInstance = new ServiceForm();
                serviceFormInstance.FormClosed += (_, __) => serviceFormInstance = null;
                serviceFormInstance.Show();
            }
            else
            {
                serviceFormInstance.BringToFront();
            }
        }

        /// <summary>
        /// Handles post-load logic, including conditional redirect to ServiceForm.
        /// </summary>
        private void MainForm_Load(object sender, EventArgs e)
        {
            settings = LoadSettings();

            if (settings?.LoadService == true)
            {
                // Hide MainForm immediately
                this.Opacity = 0;
                this.ShowInTaskbar = false;
                this.WindowState = FormWindowState.Minimized;

                // Open ServiceForm in tray mode
                if (serviceFormInstance == null || serviceFormInstance.IsDisposed)
                {
                    serviceFormInstance = new ServiceForm();
                    serviceFormInstance.FormClosed += (_, __) => Application.Exit();
                    serviceFormInstance.Show();
                }

                this.Hide();
            }
            else
            {
                // Remain in MainForm
                this.Opacity = 1;
                this.ShowInTaskbar = true;
                this.WindowState = FormWindowState.Normal;
                this.Show();
            }
        }

        #endregion

        #region Settings Loader

        /// <summary>
        /// Loads the app settings from settings.json or creates a default config if missing.
        /// </summary>
        /// <returns>A populated PowerCommanderSettings instance.</returns>
        private PowerCommanderSettings LoadSettings()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

            var defaultSettings = new PowerCommanderSettings
            {
                LoadService = true,
                ShutdownTimes = new List<string> { "19:53" }
            };

            if (!File.Exists(path))
            {
                string defaultJson = JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, defaultJson);

                return defaultSettings;
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<PowerCommanderSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? defaultSettings;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load settings: {ex.Message}", "Power Commander", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return defaultSettings;
            }
        }

        #endregion
    }
}
