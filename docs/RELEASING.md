# Releasing Lunatic Battery

Releases go to GitHub Releases, built by [Velopack](https://docs.velopack.io). Installed apps check those releases
for updates a minute after starting and every 6 hours, download a newer one silently and apply it when the user
picks "Salir" (or on the next start). The tray menu item "Buscar actualizaciones" checks on demand and, once an
update is downloaded, turns into "Reiniciar para actualizar a X".

## Cutting a release

1. Bump `<Version>` in `Directory.Build.props` (the only version in the repo) and commit.
2. Tag that commit with the same version and push the tag:
   `git tag v1.2.0` then `git push origin v1.2.0`.
3. `.github/workflows/release.yml` runs: checks that the tag matches the version, runs the tests, publishes a
   self-contained win-x64 build, packs it with `vpk` and uploads a **draft** release.
4. Review the draft on GitHub and publish it. Installed apps only see published releases.

Release assets: `LunaticBattery-win-Setup.exe` (installer for new users), `LunaticBattery-win-Portable.zip`, the
`.nupkg` packages and `releases.win.json` (the feed the app reads). Do not rename or delete them.

## Before the first real release

- **Repository**: create it on GitHub and set `<RepositoryUrl>` in `Directory.Build.props` to
  `https://github.com/<owner>/<repo>`. While it is empty the app never checks for updates and the workflow refuses
  to build. The workflow itself takes the repository from GitHub, so nothing else needs it.
- **About screen**: `SettingsViewModel.RepositoryUrl` still holds a placeholder; point it at
  `Updates/AppRepository.Url` so the repository lives in one place.
- **Code signing** (optional, but without it SmartScreen warns on the installer): the workflow is ready for Azure
  Artifact Signing (formerly Trusted Signing). Add the secrets `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`,
  `AZURE_CLIENT_SECRET` (an app registration with the Certificate Profile Signer role on the signing account) and the
  variables `SIGN_ENDPOINT`, `SIGN_ACCOUNT`, `SIGN_PROFILE`. Without them the release is built unsigned, with a
  warning. Check the first signed run: vpk 1.2 signs through signtool plus the Azure signing library.
- **Icon**: `src/BatteryDisplay.App/Assets/lunatic-battery.ico` (10 sizes, 16 to 256). The csproj embeds it as
  `ApplicationIcon` and the workflow passes it to `vpk pack --icon`; replace that one file to change both.
- **`PACK_ID`** in the workflow (`LunaticBattery`) is permanent: it names the install folder and links every update
  to the installs out there.

## Microsoft Store

Two ways in for a Win32 app. Both need a Partner Center account (free for individual developers since September
2025).

| | EXE installer by URL | MSIX |
|---|---|---|
| What is submitted | The versioned URL of `Setup.exe` from a GitHub release, installed with `--silent` | An `.msix` built for the Store |
| Signing | Ours, with a CA in the Microsoft Trusted Root Program (Azure Artifact Signing, about USD 10 a month) | Microsoft signs it, free |
| Updates | Ours (Velopack). Each version means a new URL in Partner Center | The Store's |
| Extra work | None in the app; one submission per version | A second build without Velopack, "Start with Windows" through a `StartupTask` instead of the Run key, and tray icon promotion keyed on the packaged path |

**Recommendation: the EXE route**, once signing is in place. It ships the same `Setup.exe` that GitHub users get,
keeps one update path, and the signature also removes the SmartScreen warning for direct downloads. MSIX is worth it
only if paying for signing is not an option, at the cost of maintaining a second build flavor.
