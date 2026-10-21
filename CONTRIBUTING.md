# Contributing

Start with the [build guide](docs/BUILDING.md) and [source map](docs/ARCHITECTURE.md). Changes should preserve installed Android apps and saved data, explain observable behavior, and include appropriate validation.

## Report a problem

Open a [GitHub issue](https://github.com/MiMat82/APK-Runner/issues) with launcher version, Windows build, CPU architecture, performance/orientation mode, package format and ABI if known, reproduction steps, and relevant activity-log messages. Explain what you expected and what happened.

Remove private paths, account information, tokens, and app content before posting. Do not upload commercial APKs, virtual-device disks, or personal data. For a potentially exploitable issue, read [SECURITY.md](SECURITY.md).

## Submit a change

1. Fork the repository and create a branch for one focused change.
2. Build the launcher and run relevant checks.
3. Describe the behavior change and validation in your pull request. State when Android behavior was only simulated or not exercised.
4. Update guides if controls, requirements, or setup behavior change.

The current interface uses English. Keep control labels consistent and explain changes in plain language. Localization improvements are welcome.

Use disposable fixtures for runtime/storage work. Tests must not silently reuse installed runtimes, register file associations, or delete a contributor's Android data. Commands that install tools, contact GitHub, change compression, or modify virtual devices must be explicit.

Do not commit runtime data, analysis, artifacts, downloaded binaries, signing configuration, certificates, keys, or personal logs. Contributions are made available under the MIT License. Preserve third-party notices.
