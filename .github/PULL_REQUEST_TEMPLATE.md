## Summary

<!-- What does this change, and why? Link the related issue, for example "Fixes #12". -->

## How was it tested?

<!-- In game? Which cheats, on which game version? Did you run the self-test? -->

## Checklist

- [ ] `dotnet build -c Release` succeeds without warnings.
- [ ] The patch verifier passes, if patches or reflected members changed: `dotnet run -c Release --project tests\PatchVerifier`.
- [ ] Every new or changed text exists in English and French (`src/PlanetbaseTrainer/Core/Loc.cs`).
- [ ] Both READMEs are updated if a cheat, a hotkey or a setting changed.
- [ ] User-visible changes are listed under *Unreleased* in `CHANGELOG.md`.
