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
        readonly LinkLabel updateLink = new LinkLabel { Text = "Søg efter opdateringer", AutoSize = true,
            LinkColor = FlightTheme.Cyan, ActiveLinkColor = FlightTheme.Amber, VisitedLinkColor = FlightTheme.Cyan,
            Font = FlightTheme.Body(9), Margin = new Padding(14, 5, 0, 0), AccessibleName = "Søg efter APK Runner-opdateringer" };
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

        async Task CheckForUpdates(bool manual)
        {
            if (updates == null || updateCheckInProgress || applyingUpdate) return;
            updateCheckInProgress = updatePolicy.Checking = true;
            updateLink.Enabled = false;
            SetUpdateStatus("Kontrollerer opdateringer …");
            try {
                await updates.Check();
                if (IsDisposed) return;
                if (updatePolicy.Required != null && updatePolicy.Required > updatePolicy.Current) {
                    SetUpdateStatus("Opdatering " + updatePolicy.Required + " kræves");
                    updateLink.Text = "Opdatér og genstart";
                    Log("En ny APK Runner-version kræves. Gem dit arbejde og luk Android, før du opdaterer. Allerede åbne Android-apps fortsætter.");
                    string version = updatePolicy.Required.ToString();
                    updateStatus.ForeColor = FlightTheme.Amber;
                    if (manual || (notifiedVersion != version && !busy)) {
                        notifiedVersion = version;
                        MessageBox.Show(this, "APK Runner " + version + " er tilgængelig og skal installeres før nye apps kan startes. Gem dit arbejde, luk Android med Stop Android, og vælg Opdatér og genstart.", "APK Runner - opdatering kræves", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                } else {
                    SetUpdateStatus("Seneste version installeret"); updateLink.Text = "Søg efter opdateringer";
                }
            } catch (Exception error) {
                if (IsDisposed) return;
                SetUpdateStatus(updatePolicy.BlocksNewApps && updatePolicy.Required != null ? "Påkrævet opdatering afventer netværk" : "Opdateringstjek ikke tilgængeligt");
                Log("Opdateringstjek: " + error.Message);
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
                    throw new InvalidOperationException("Gem dit arbejde i Android-apps, og vælg Stop Android, før du opdaterer. Ingen Android-session er blevet afsluttet.");
                Directory.CreateDirectory(runtime.Root);
                // The ordinary app launch/install path takes this same lock.
                using (new FileStream(Path.Combine(runtime.Root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
                    await updates.Download(progress => {
                        if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { SetUpdateStatus("Henter opdatering: " + progress + "%"); });
                    });
                    if (await Task.Run(() => AppUpdates.AndroidIsRunning(runtime.Root)))
                        throw new InvalidOperationException("Android blev startet under download. Gem og luk Android, og prøv igen.");
                    SetUpdateStatus("Installerer verificeret opdatering …");
                    updates.Apply(pending);
                }
            } catch (Exception error) {
                if (!IsDisposed) { Log("Opdatering ikke anvendt: " + error.Message); SetUpdateStatus("Opdatering afventer - prøv igen"); MessageBox.Show(this, error.Message, Text); }
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
