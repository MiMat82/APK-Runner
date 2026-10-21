APK Runner 12.11.0 makes the application English-only and improves startup and Android controls.

- Translates the interface, setup, status checks, activity messages, storage reports,
  errors, performance profiles, shortcuts and update notifications into English.
- Uses English application resource preferences on Windows configured for another language.
- Checks local runtime readiness alongside the online update check, so local component
  status is available without waiting for the network. Required updates still gate new launches.
- Stops either supported APK Runner Android runtime, with ownership checks before shutdown.
- Keeps the rounded icon, both orientation modes, installed apps and saved data.

The automatic update and compact-window features introduced in 12.10.0 remain:

- Checks for newer stable releases at startup, every six hours while open, and on request.
- Shows an update notification and an Update and restart action. A known newer stable
  release must be installed before starting additional Android apps.
- Existing Android sessions continue. Save work and stop Android before applying an update.
- Keeps the Android runtime, installed apps and saved data outside the replaceable app folder.
- Limits the initial window to 60% of the selected screen's working width and 92% of its
  working height, with a size cap on larger screens. Controls wrap and scroll on small displays.
- Retains the optimized rendering, portrait/landscape support and existing compatibility runtimes.

Download APKRunner-win-Setup.exe for the Windows x64 installer. This small launcher package
does not bundle Android system images or anyone's installed apps or saved data. First-time
users can install the Android runtime through the app after accepting the SDK terms.

This release is not Authenticode-signed. Package hashes are verified by the updater;
publisher code signing remains a separate distribution task.
