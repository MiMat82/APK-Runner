APK Runner 12.10.0 adds GitHub-based automatic updates and a smaller opening window.

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
