using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace APKRunner
{
    internal partial class Window
    {
        Label statusSummary = FlightTheme.Label("AWAITING CHECK", FlightTheme.Muted, FlightTheme.Mono(9));
        Label activityLabel = FlightTheme.Label("STANDBY", FlightTheme.Cyan, FlightTheme.Mono(9));
        Label emptyState = new Label { Text = "Check the runtime to see component status.",
            ForeColor = FlightTheme.Muted, BackColor = FlightTheme.Surface, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter, Font = FlightTheme.Body(11), AccessibleName = "Component status" };

        void InitializeFlightDeck(bool preview)
        {
            Text = "APK Runner";
            Branding.Apply(this);
            Rectangle openingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            Size openingSize = WindowSizing.InitialSize(openingArea);
            MinimumSize = new Size(Math.Min(640, openingSize.Width), Math.Min(520, openingSize.Height));
            Size = openingSize;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(openingArea.Left + (openingArea.Width - openingSize.Width) / 2, openingArea.Top + (openingArea.Height - openingSize.Height) / 2);
            Font = FlightTheme.Body(10);
            BackColor = FlightTheme.Background;
            ForeColor = FlightTheme.Text;
            runtime = new Runtime(preview ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime") : AppUpdates.RuntimeRoot);
            runtime.Log = Log;
            InitializeUpdates(preview);

            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                Margin = new Padding(0), Padding = new Padding(0), BackColor = FlightTheme.Background };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 25));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(shell);
            shell.Controls.Add(new FlightRail(), 0, 0);
            var viewport = new Panel { Name = "ContentViewport", Dock = DockStyle.Fill, Margin = new Padding(0) };
            var body = new TableLayoutPanel { ColumnCount = 1, RowCount = 7, MinimumSize = new Size(720, 680),
                Padding = new Padding(22, 20, 26, 10), Margin = new Padding(0), BackColor = FlightTheme.Background };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 63));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 37));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            shell.Controls.Add(viewport, 1, 0);
            viewport.Controls.Add(body);
            bool fittingViewport = false;
            viewport.Resize += delegate {
                if (fittingViewport) return;
                fittingViewport = true;
                try {
                    bool fits = viewport.Width >= body.MinimumSize.Width && viewport.Height >= body.MinimumSize.Height;
                    viewport.AutoScrollMinSize = fits ? Size.Empty : body.MinimumSize;
                    viewport.AutoScroll = !fits;
                    body.Dock = fits ? DockStyle.Fill : DockStyle.None;
                    if (!fits) body.Size = new Size(Math.Max(body.MinimumSize.Width, viewport.ClientSize.Width), Math.Max(body.MinimumSize.Height, viewport.ClientSize.Height));
                } finally { fittingViewport = false; }
            };
            body.Controls.Add(new FlightHeader(), 0, 0);

            var statusHeader = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            var statusTitle = FlightTheme.Label("SYSTEM STATUS", FlightTheme.Cyan, FlightTheme.Strong(9));
            statusTitle.Location = new Point(0, 7);
            statusHeader.Controls.Add(statusTitle);
            statusHeader.Controls.Add(statusSummary);
            statusHeader.Resize += delegate { statusSummary.Location = new Point(statusHeader.Width - statusSummary.PreferredWidth, 7); };
            statusSummary.TextChanged += delegate { statusSummary.Location = new Point(statusHeader.Width - statusSummary.PreferredWidth, 7); };
            body.Controls.Add(statusHeader, 0, 1);

            var listPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), BackColor = FlightTheme.Line, Padding = new Padding(1) };
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.Columns.Add("Component", 190);
            list.Columns.Add("Status", 110);
            list.Columns.Add("Details", 450);
            list.AccessibleName = "Runtime components and status";
            list.TabIndex = 0;
            FlightTheme.StyleList(list);
            listPanel.Controls.Add(list);
            listPanel.Controls.Add(emptyState);
            body.Controls.Add(listPanel, 0, 2);

            var controls = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 3,
                Margin = new Padding(0, 14, 0, 0), Padding = new Padding(0) };
            controls.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            controls.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            controls.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.Controls.Add(controls, 0, 3);
            var display = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, MinimumSize = new Size(0, 38), Margin = new Padding(0), WrapContents = true };
            var displayLabel = FlightTheme.Label("APP WINDOW", FlightTheme.Muted, FlightTheme.Strong(8));
            displayLabel.Margin = new Padding(0, 9, 15, 0);
            display.Controls.Add(displayLabel);
            orientation.Width = 211;
            orientation.Items.AddRange(new object[] { "Landscape", "Portrait" });
            orientation.SelectedIndex = 0;
            orientation.SelectedIndexChanged += delegate { runtime.Portrait = orientation.SelectedIndex == 1; };
            orientation.Margin = new Padding(0, 0, 18, 0);
            orientation.TabIndex = 1;
            display.Controls.Add(orientation);
            var dropHint = FlightTheme.Label("Drop an APK or XAPK file here", FlightTheme.Muted, FlightTheme.Body(9));
            dropHint.Margin = new Padding(0, 7, 0, 0);
            display.Controls.Add(dropHint);
            controls.Controls.Add(display, 0, 0);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, MinimumSize = new Size(0, 51), Margin = new Padding(0), WrapContents = true };
            open.Enabled = false;
            install.Enabled = false;
            ((FlightButton)open).Primary = true;
            open.Width = 151; install.Width = 158; refresh.Width = 119; stop.Width = 129; storage.Width = 130;
            Add(actions, open, "Open APK / XAPK", Choose);
            Add(actions, install, "Install runtime", delegate { if (consent.Checked) Work(delegate { runtime.Install(UpdateStatus); RunPending(); }); });
            Add(actions, refresh, "Check runtime", delegate { Work(delegate { UpdateStatus(runtime.Check()); RunPending(); }); });
            Add(actions, stop, "Stop Android", delegate { Work(delegate { runtime.Stop(); }); });
            Add(actions, storage, "Storage usage", delegate { Work(delegate {
                string report = StorageReport.Describe(StorageReport.Scan(runtime.Root));
                Invoke((Action)delegate { StorageReport.Show(this, report, runtime.Root); });
            }); });
            controls.Controls.Add(actions, 0, 1);
            int tab = 2;
            foreach (Control control in actions.Controls) { control.TabIndex = tab++; control.AccessibleName = control.Text; }

            var license = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, MinimumSize = new Size(0, 45), Margin = new Padding(0), WrapContents = true };
            consent.Text = "I accept the Android SDK terms";
            consent.ForeColor = FlightTheme.Muted;
            consent.AutoSize = true;
            consent.Margin = new Padding(0, 13, 16, 0);
            consent.TabIndex = 7;
            consent.CheckedChanged += delegate { install.Enabled = !busy && consent.Checked; };
            license.Controls.Add(consent);
            var terms = new LinkLabel { Text = "Read terms", AutoSize = true, Font = FlightTheme.Body(9),
                LinkColor = FlightTheme.Cyan, ActiveLinkColor = FlightTheme.Amber, VisitedLinkColor = FlightTheme.Cyan,
                Margin = new Padding(0, 14, 18, 0), TabIndex = 8 };
            terms.LinkClicked += delegate { System.Diagnostics.Process.Start("https://developer.android.com/studio#terms"); };
            license.Controls.Add(terms);
            associate.Width = 182;
            Add(license, associate, "Enable double-click", Register);
            associate.TabIndex = 9;
            controls.Controls.Add(license, 0, 2);

            var logHeader = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            var logTitle = FlightTheme.Label("ACTIVITY LOG", FlightTheme.Cyan, FlightTheme.Strong(9));
            logTitle.Location = new Point(0, 6);
            logHeader.Controls.Add(logTitle);
            logHeader.Controls.Add(activityLabel);
            logHeader.Resize += delegate { activityLabel.Location = new Point(logHeader.Width - activityLabel.PreferredWidth, 6); };
            activityLabel.TextChanged += delegate { activityLabel.Location = new Point(logHeader.Width - activityLabel.PreferredWidth, 6); };
            body.Controls.Add(logHeader, 0, 4);
            log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical;
            log.Dock = DockStyle.Fill; log.BorderStyle = BorderStyle.None;
            log.BackColor = FlightTheme.Surface; log.ForeColor = FlightTheme.Muted; log.Font = FlightTheme.Mono(9);
            log.Margin = new Padding(0); log.TabIndex = 10; log.AccessibleName = "Activity log";
            var logWell = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(12, 10, 10, 10), BackColor = FlightTheme.Surface };
            logWell.Controls.Add(log);
            body.Controls.Add(logWell, 0, 5);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0, 2, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(FlightTheme.Label("LOCAL / WINDOWS x64", FlightTheme.Muted, FlightTheme.Mono(8)), 0, 0);
            footer.Controls.Add(updateStatus, 1, 0);
            footer.Controls.Add(updateLink, 2, 0);
            body.Controls.Add(footer, 0, 6);

            HandleCreated += delegate { FlightTheme.DarkTitle(this); };
            if (!preview) Shown += async delegate {
                try { ShellBranding.RepairShortcuts(AppUpdates.StableLauncher, RuntimePaths.SettingsDirectory); }
                catch (Exception error) { Log("Could not update the shortcut icon: " + error.Message); }
                AskPerformanceMode();
                // Local readiness does not depend on GitHub being reachable. Keep one
                // busy scope until both checks finish so a queued APK cannot race them.
                await RunFlightOperationAsync(async delegate {
                    Task updateCheck = CheckForUpdates(false);
                    await Task.WhenAll(Task.Run(delegate { runtime.RepairAvdPointers(); UpdateStatus(runtime.Check()); }), updateCheck);
                    if (!IsDisposed) await Task.Run((Action)RunPending);
                });
                if (IsDisposed) return;
                NotifyRequiredUpdate(false);
                updateTimer.Start();
            };
            AllowDrop = true;
            DragEnter += delegate(object sender, DragEventArgs e) {
                if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            DragDrop += delegate(object sender, DragEventArgs e) {
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null && paths.Length == 1 && !busy && !applyingUpdate && !updatePolicy.BlocksNewApps) {
                    pending = paths[0]; Work(delegate { UpdateStatus(runtime.Check()); RunPending(); });
                }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (busy || applyingUpdate) { e.Cancel = true; MessageBox.Show(this, "Wait for the current operation to finish.", Text); }
            };
        }

        void AddFlightButton(FlowLayoutPanel parent, Button button, string text, Action action)
        {
            button.Text = text;
            button.AccessibleName = text;
            button.AutoSize = false;
            button.Height = 42;
            button.Margin = new Padding(0, 3, 10, 3);
            button.Click += delegate { action(); };
            parent.Controls.Add(button);
        }

        async void RunFlightOperation(Action action)
        {
            await RunFlightOperationAsync(() => Task.Run(action));
        }

        async Task RunFlightOperationAsync(Func<Task> action)
        {
            if (busy || applyingUpdate) return;
            busy = true;
            foreach (Control control in new Control[] { install, open, refresh, associate, stop, storage, orientation }) control.Enabled = false;
            activityLabel.Text = "WORKING";
            activityLabel.ForeColor = FlightTheme.Amber;
            if (lastStatus == null) emptyState.Text = "Checking runtime components …";
            bool failed = false;
            try { await action(); }
            catch (Exception ex) {
                failed = true; Log(ex.Message);
                if (lastStatus == null) emptyState.Text = "The check could not finish. See the activity log and try again.";
                MessageBox.Show(this, ex.Message, "APK Runner", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally {
                busy = false;
                install.Enabled = consent.Checked;
                refresh.Enabled = associate.Enabled = stop.Enabled = storage.Enabled = orientation.Enabled = true;
                open.Enabled = !updatePolicy.BlocksNewApps && open.Tag is bool && (bool)open.Tag;
                activityLabel.Text = failed ? "ACTION REQUIRED" : "STANDBY";
                activityLabel.ForeColor = failed ? FlightTheme.Amber : FlightTheme.Cyan;
            }
        }
    }
}
