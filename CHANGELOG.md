# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-11-08

### Added

- *Unlock all Steam achievements* action, in a new Steam section of the Progression tab: unlocks every Steam
  achievement of the game at once, through the game's own Steam connection. It is permanent, so it is marked as risky
  and asks for confirmation.

### Fixed

- The menu could be made so narrow that some cheat names were cut off and lost their risky tag. Its minimum width now
  fits every name, whatever the language and the menu size.
- The in-game self-test no longer changes the trainer settings, which turned the remembered cheats off, and takes its
  screenshots in a 1920×1080 window whatever the screen resolution.

## [1.0.0] - 2026-11-08

First public release, for Planetbase 1.4.2.

### Added

- In-game menu, opened with <kbd>F9</kbd>, with 48 cheats in 8 categories: colonists, construction, resources, base,
  trade, threats, time and progression. Favorites and Settings tabs.
- A hotkey for every cheat, rebindable from the menu, with <kbd>Ctrl</kbd> / <kbd>Shift</kbd> / <kbd>Alt</kbd>
  combinations, extra mouse buttons and automatic conflict resolution. <kbd>Home</kbd> turns every cheat off.
- Accent-insensitive search, an active cheats HUD with 5 positions, notifications, 6 accent colors, an automatic or
  manual menu size, and an English and French interface.
- Protection of Steam achievements while cheating, on by default, and optional protection of profile progression.
- Confirmation for the most drastic actions, and clear messages when an action needs a landing pad, a starport,
  storage space or a planet that has the requested disaster.
- Patches applied group by group: a game update only disables the cheats whose patches no longer match.
- Workaround for the BepInEx 5 crash when quitting 32-bit Unity 2022.3 games
  ([BepInEx#398](https://github.com/BepInEx/BepInEx/issues/398)).
- All-in-one package with BepInEx 5.4.23.5 x86, and SHA-256 checksums for every release file.
- Developer tools: static patch verifier, in-game end-to-end self-test, BepInEx install, uninstall and release scripts.

[Unreleased]: https://github.com/LASCAMPIA67/PlanetbaseTrainer/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/LASCAMPIA67/PlanetbaseTrainer/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/LASCAMPIA67/PlanetbaseTrainer/releases/tag/v1.0.0
