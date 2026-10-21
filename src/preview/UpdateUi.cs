using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace APKRunner
{
    internal partial class Window
    {
        AppUpdates updates;
        UpdatePolicy updatePolicy = new UpdatePolicy();
        volatile bool applyingUpdate;
        readonly LinkLabel updateLink = new LinkLabel { Text = "Check for updates", AutoSize = true,
            LinkColor = FlightTheme.Cyan, ActiveLinkColor = FlightTheme.Amber, VisitedLinkColor = FlightTheme.Cyan,
            Font = FlightTheme.Body(9), Margin = new Padding(14, 5, 0, 0), AccessibleName = "Check for APK Runner updates" };
        readonly Label updateStatus = new Label { Text = "", AutoEllipsis = true, Dock = DockStyle.Fill,
            ForeColor = FlightTheme.Muted, Font = FlightTheme.Body(9), TextAlign = ContentAlignment.MiddleRight };
        readonly Timer updateTimer = new Timer { Interval = 6 * 60 * 60 * 1000 };
        bool updateCheckInProgress;
        string notifiedVersion;

        void InitializeUpdates(bool preview)
        {
            if (!preview) { updates = new AppUpdates(); updatePolicy = updates.Policy; updatePolicy.Checking = true; }
            updateLink.LinkClicked += async delegate { await UpdateClicked(); };
            updateTimer.Tick += async delegate { await CheckForUpdates(false); };
            FormClosed += delegate { updateTimer.Stop(); updateTimer.Dispose(); };
        }

        void SetUpdateStatus(string message)
        {
            updateStatus.Text = message;
            updateStatus.AccessibleName = message;
        }

        void NotifyRequiredUpdate(bool manual)
        {
            if (IsDisposed || updatePolicy.Required == null || updatePolicy.Current == null || updatePolicy.Required <= updatePolicy.Current) return;
            string version = updatePolicy.Required.ToString();
            if (manual || (notifiedVersion != version && !busy)) {
                notifiedVersion = version;
                MessageBox.Show(this, "APK Runner " + version + " is available and must be installed before you can start more apps. Save your work, select Stop Android, then select Update and restart.", "APK Runner - update required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        async Task CheckForUpdates(bool manual)
        {
            if (updates == null || updateCheckInProgress || applyingUpdate) return;
            updateCheckInProgress = updatePolicy.Checking = true;
            updateLink.Enabled = false;
            updateStatus.ForeColor = FlightTheme.Muted;
            SetUpdateStatus("Checking for updates …");
            try {
                await updates.Check();
                if (IsDisposed) return;
                if (updatePolicy.Required != null && updatePolicy.Required > updatePolicy.Current) {
                    SetUpdateStatus("Update " + updatePolicy.Required + " required");
                    updateLink.Text = "Update and restart";
                    Log("A new APK Runner version is required. Save your work and stop Android before updating. Android apps that are already open will keep running.");
                    updateStatus.ForeColor = FlightTheme.Amber;
                    NotifyRequiredUpdate(manual);
                } else {
                    updateStatus.ForeColor = FlightTheme.Muted;
                    SetUpdateStatus("You're up to date"); updateLink.Text = "Check for updates";
                }
            } catch (Exception error) {
                if (IsDisposed) return;
                updateStatus.ForeColor = FlightTheme.Amber;
                SetUpdateStatus(updatePolicy.BlocksNewApps && updatePolicy.Required != null ? "Required update waiting for network" : "Update check unavailable");
                Log("Update check: " + error.Message);
                if (manual) MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            } finally {
                updatePolicy.Checking = false; updateCheckInProgress = false;
                if (!IsDisposed) {
                    updateLink.Enabled = true;
                    open.Enabled = !busy && !updatePolicy.BlocksNewApps && open.Tag is bool && (bool)open.Tag;
                }
            }
        }

        async Task UpdateClicked()
        {
            if (updates == null || applyingUpdate || busy || updateCheckInProgress) return;
            if (updates.Available == null) { await CheckForUpdates(true); return; }
            applyingUpdate = true;
            updateLink.Enabled = false;
            open.Enabled = install.Enabled = refresh.Enabled = associate.Enabled = stop.Enabled = storage.Enabled = orientation.Enabled = false;
            try {
                if (await Task.Run(() => AppUpdates.AndroidIsRunning(runtime.Root)))
                    throw new InvalidOperationException("Save your work in Android apps and select Stop Android before updating. Your Android sessions are still running.");
                Directory.CreateDirectory(runtime.Root);
                // The ordinary app launch/install path takes this same lock.
                using (new FileStream(Path.Combine(runtime.Root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
                    await updates.Download(progress => {
                        if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { SetUpdateStatus("Downloading update: " + progress + "%"); });
                    });
                    if (await Task.Run(() => AppUpdates.AndroidIsRunning(runtime.Root)))
                        throw new InvalidOperationException("Android was started during the download. Save your work, stop Android and try again.");
                    SetUpdateStatus("Installing verified update …");
                    updates.Apply(pending);
                }
            } catch (Exception error) {
                if (!IsDisposed) { Log("Update not applied: " + error.Message); SetUpdateStatus("Update pending - try again"); MessageBox.Show(this, error.Message, Text); }
            } finally {
                applyingUpdate = false;
                if (!IsDisposed) {
                    updateLink.Enabled = true;
                    install.Enabled = consent.Checked;
                    refresh.Enabled = associate.Enabled = stop.Enabled = storage.Enabled = orientation.Enabled = true;
                    open.Enabled = !updatePolicy.BlocksNewApps && open.Tag is bool && (bool)open.Tag;
                }
            }
        }
    }
}
