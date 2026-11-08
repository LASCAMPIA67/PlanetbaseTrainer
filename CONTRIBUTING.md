# Contributing to Planetbase Trainer

Thanks for your interest! Bug reports, cheat ideas, translations and pull requests are all welcome. English and
French are both fine in issues and discussions.

- **Questions:** ask in [Discussions](https://github.com/LASCAMPIA67/PlanetbaseTrainer/discussions).
- **Bugs and ideas:** open an [issue](https://github.com/LASCAMPIA67/PlanetbaseTrainer/issues/new/choose) with the
  matching form. For a bug, attach `BepInEx\LogOutput.log` from the game folder.
- **Security issues:** report them privately, as explained in [SECURITY.md](.github/SECURITY.md).

Everyone taking part is expected to follow the [code of conduct](.github/CODE_OF_CONDUCT.md).

## Development setup

You need:

- Windows, with Planetbase 1.4.x installed (Steam);
- the [.NET SDK](https://dotnet.microsoft.com/download) 10 or later;
- Windows PowerShell 5.1 or PowerShell 7 for the scripts in `tools/` (PowerShell 7.2 or later for `New-Release.ps1`).

```powershell
git clone https://github.com/LASCAMPIA67/PlanetbaseTrainer.git
cd PlanetbaseTrainer
.\tools\Install-BepInEx.ps1   # once: BepInEx 5.4.23.5 x86, checked against its SHA-256 hash
dotnet build                   # Debug build, copied into <game>\BepInEx\plugins\PlanetbaseTrainer
```

Then start the game from Steam: the plugin logs to `<game>\BepInEx\LogOutput.log`.

The game folder is found in this order: `-p:PlanetbaseDir=...`, the `PLANETBASE_DIR` environment variable, the parent
folder of the repository (when it is cloned inside the game folder), Steam's install record for Planetbase in the
registry, then the default Steam library. To pin it on your machine, create a `Directory.Build.user.props` file at the
root of the repository (it is ignored by Git):

```xml
<Project>
  <PropertyGroup>
    <PlanetbaseDir>D:\SteamLibrary\steamapps\common\Planetbase</PlanetbaseDir>
  </PropertyGroup>
</Project>
```

The game's assemblies are referenced from the install folder and are never committed or redistributed. Building the
plugin therefore needs the game, which is why CI only checks formatting, Markdown, PowerShell scripts, the patch
verifier and, on pull requests, dependency changes.

## Project layout

```text
src/PlanetbaseTrainer/
├── Plugin.cs                 BepInEx entry point
├── Core/                     settings, English/French texts, persisted options, hotkeys, tolerant reflection,
│                             workaround for the BepInEx crash at exit
├── Features/                 cheat model, registry of the 48 cheats, runtime loop (hotkeys, periodic tasks)
├── Game/                     safe access to the game, actions (spawning, resources, construction…), controllers
├── Patches/                  Harmony patches, grouped by feature and applied group by group
├── UI/                       IMGUI menu: theme, widgets, window, settings page, HUD, notifications
└── Diagnostics/SelfTest.cs   in-game end-to-end test (only when PBTRAINER_SELFTEST is set)
tests/PatchVerifier/          static check of every patch target against the installed game
tools/                        BepInEx install, uninstall, release packaging, documentation screenshots
docs/images/                  README screenshots (en, fr) and the repository's social preview
```

Things worth knowing about the game:

- Planetbase's `GameManager` constructor **destroys every root GameObject** not tagged `RootObject`. The plugin tags
  its own object, otherwise the menu would disappear at startup.
- Game singletons are created on demand by `getInstance()`. The trainer reads `Singleton<T>.mInstance` and checks that
  a game is actually running, so that it never creates one as a side effect.
- Instant construction goes through the game's own path (`startBuilding`, then `updateBuild`, which calls `onBuilt`).
  It skips sites a worker is busy with: the patched build speed completes those within a frame, which avoids a double
  `onBuilt`.
- Unlimited power and water cancel the consumption in `Grid.getGeneration`, so the grid balance stays positive without
  touching generators or batteries.

## Checks before opening a pull request

```powershell
dotnet build -c Release                                      # must succeed without warnings
dotnet run -c Release --project tests\PatchVerifier          # every patch target and reflected member must resolve
dotnet format whitespace --folder --verify-no-changes --exclude "**/obj/**" "**/bin/**"   # same check as CI
```

The **patch verifier** loads the installed `Assembly-CSharp.dll` by metadata only (the game isn't started) and checks
that every Harmony patch targets exactly one method, that injected parameters have the right names and types, and that
every member accessed by name through reflection exists. Run it after every game update.

### In-game self-test

When the `PBTRAINER_SELFTEST` environment variable points to a folder, the plugin runs an end-to-end test: it opens the
menu on every tab, starts a throw-away game, turns every cheat on, runs every action (except those that write to the
player profile), takes screenshots, turns everything off and quits. The game plays with a temporary profile created in
that folder, so your own profile and saves are never touched, and achievements and milestones stay locked during the
whole run.

```powershell
$game = .\tools\Resolve-PlanetbaseDir.ps1
Set-Content "$game\steam_appid.txt" 403190       # lets the game start outside Steam (Steam must be running)
$env:PBTRAINER_SELFTEST = "$env:TEMP\pbtrainer-selftest"
$env:PBTRAINER_SELFTEST_LANGS = 'en,fr'           # optional: in-game screenshots in each language
& "$game\Planetbase.exe" -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 | Out-Null
Remove-Item "$game\steam_appid.txt"
Select-String 'SelfTest\] done' "$game\BepInEx\LogOutput.log"   # expect "errors logged: 0"
```

The README screenshots come from such a run at 1920×1080, with `PBTRAINER_SELFTEST_LANGS = 'en,fr'`. To update them,
run `python tools\make_docs_images.py "$env:TEMP\pbtrainer-selftest" docs\images` (Python 3 with Pillow).

## Coding guidelines

- Follow the existing style; `.editorconfig` holds the formatting rules. Code, comments and logs are in English.
- Every text shown in game goes through `Core/Loc.cs`, in **both** English and French.
- A new patch goes in `Patches/`, in a `[PatchGroup]` named after the feature, and every cheat lists the groups it
  depends on. If a group fails to apply, its cheats are shown as unavailable instead of breaking the plugin.
- Access private game members through `Core/Ref.cs`, and add every name accessed this way to the patch verifier.
- Never create game objects or singletons outside a running game (`G.InGame`).

### Adding a cheat

1. Register it in `Features/Cheats.cs` with `Toggle(...)` or `Command(...)`: id, tab, section, default hotkey (or none)
   and patch groups.
2. Add its name (`c.<Id>`) and description (`c.<Id>.d`) in English and French to `Core/Loc.cs`.
3. Implement it: a Harmony patch reading the toggle's state, or a function in `Game/GameActions.cs` that returns an
   `ActionResult`.
4. Run the patch verifier and the self-test, then document the cheat in both READMEs and in `CHANGELOG.md`.

### Dependencies

NuGet packages are pinned, with their content hash, in the `packages.lock.json` file of each project. After adding or
updating a `PackageReference`, run `dotnet restore` and commit the updated lock file: CI and `New-Release.ps1` restore
in locked mode and fail when the lock file is out of date. Dependabot keeps the lock files up to date in its pull
requests.

## Commits and pull requests

- Write commit messages in the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) style:
  `feat: add a cheat to ...`, `fix: ...`, `docs: ...`.
- Keep pull requests focused on one change, explain how you tested it, and fill in the checklist of the template.
- Add user-visible changes to the *Unreleased* section of [CHANGELOG.md](CHANGELOG.md).

## Releasing

1. Update `<Version>` in `src/PlanetbaseTrainer/PlanetbaseTrainer.csproj`, following
   [Semantic Versioning](https://semver.org/), and move the *Unreleased* entries of `CHANGELOG.md` under the new version.
2. Commit, then build the packages from that commit: `pwsh .\tools\New-Release.ps1`. It produces, in `artifacts\`,
   `PlanetbaseTrainer.zip`, `PlanetbaseTrainer-with-BepInEx.zip` and `SHA256SUMS.txt`.
3. Tag and publish:

   ```powershell
   git tag -a v1.0.1 -m "Planetbase Trainer 1.0.1"
   git push origin v1.0.1
   gh release create v1.0.1 artifacts\PlanetbaseTrainer-with-BepInEx.zip artifacts\PlanetbaseTrainer.zip artifacts\SHA256SUMS.txt --verify-tag --title "Planetbase Trainer 1.0.1" --notes-file notes.md
   ```

The asset names carry no version number, so that the README's `releases/latest/download/...` links always point to the
latest release.
