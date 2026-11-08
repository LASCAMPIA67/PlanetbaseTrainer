# Security policy

## Supported versions

Only the [latest release](https://github.com/LASCAMPIA67/PlanetbaseTrainer/releases/latest) receives fixes.

## Reporting a vulnerability

Please **do not open a public issue**. Report it privately with GitHub's
[private vulnerability reporting](https://github.com/LASCAMPIA67/PlanetbaseTrainer/security/advisories/new)
(**Security** tab › **Report a vulnerability**), and include the steps to reproduce it and the affected version.

You should receive an answer within 7 days. Once the issue is confirmed, a fixed release is published as soon as
possible and the advisory is made public, crediting you unless you prefer otherwise.

## Scope

Planetbase Trainer is a BepInEx plugin that runs inside the game, with the game's permissions. It makes no network
connection and only writes its own configuration file. Relevant reports include, for example:

- a release file that doesn't match the `SHA256SUMS.txt` of its release;
- a way to run code through a crafted configuration file;
- a flaw in the scripts of the `tools` folder, such as the download verification of BepInEx.

Cheating in the game itself is the purpose of the project, not a vulnerability. Issues in BepInEx, HarmonyX or the
game should be reported to their own projects.

## Verifying a download

Every release publishes the SHA-256 checksums of its files in `SHA256SUMS.txt`. Compare them with:

```powershell
Get-FileHash .\PlanetbaseTrainer-with-BepInEx.zip -Algorithm SHA256
```
