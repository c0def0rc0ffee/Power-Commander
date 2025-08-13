using System;
using System.Windows.Forms;
using PowerCommanderSettings = PowerCommander.PowerCommanderSettings;

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
            settings = SettingsLoader.Load();

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

    }
}
