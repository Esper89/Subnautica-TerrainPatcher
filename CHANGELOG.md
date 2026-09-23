# Changelog

## Unreleased

- Terrain patches are now reflected in the seaglide and scanner room hologram maps.
- Visuals for the seaglide and scanner room maps have been modified slightly to support this.
- Missing batches underground will not appear on the seaglide and scanner room maps.
- Terrain patching now happens on a separate thread, allowing the game to continue loading.
- Added integration with [Nautilus] if it's installed.
- Improved error handling for errors raised while locating terrain patches.
- Improved in-game error display.
- Fixed terrain patches with a few trailing bytes loading without errors.
- Fixed some mostly harmless errors logged near the edges of the world.
- Improved Harmony patching code and patch code.
- Small logging changes.

## v1.2.5 (2026-05-01)

- Fixed entities not loading properly in negative batches.
- Fixed some issues with terrain and entities not loading properly outside the world bounds.
- Improved logging and Harmony patch code.
- Removed dependency on Nautilus and the associated options menu.

## v1.2.4 (2025-10-11)

- Fixed a bug where installing the source code alongside the actual mod would cause weird behavior
  and crash the world streamer.
- Changed Terrain Patcher's search directory from `BepInEx/plugins` to just `BepInEx`.
- Added a feature for ignoring terrain patches in a directory by creating a file named
  `.terrain-patcher-ignore` in that directory.

## v1.2.3 (2025-09-21)

- Fixed a bug with adding terrain near or past the north edge of the world.

## v1.2.2 (2025-02-10)

- Deprecated `TerrainRegistry.PatchTerrain` in anticipation of possible changes to the patching
  system.
  - Removed documentation explaining how to use this method. If you need that, check an older
    version of the repository.
- Fixed Terrain Patcher accidentally loading an example mod with small terrain changes.

## v1.2.1 (2025-02-06)

- Terrain Patcher now correctly satisfies other mods' dependencies on Terrain Extender.
- Terrain Extender is no longer loaded when Terrain Patcher is installed, preventing it from causing
  bugs.
- Removed the warning telling users to uninstall Terrain Extender.

## v1.2.0 (2025-02-02)

- Added the features of Terrain Extender. Specifically,
  - The edge of the world is now extended to allow for more terrain.
  - The edge of the world where entities can spawn and save is now extended.
- Improved logging and error handling.

## v1.1.0 (2024-03-15)

- Added an optional `forceOriginal` parameter to `TerrainRegistry.PatchTerrain`.
- Added warnings to the log output when a patch overrides another patch.

Breaks compatibility with [Terrain Extender], because it relied on a private field that was changed
in this release. For compatibility with that mod, use v1.0.2 instead until Terrain Extender is
updated or integrated into Terrain Patcher.

## v1.0.2 (2023-06-16)

- Added support for loading `.optoctreepatc` (sic) files.
- Made error handling slightly better.

## v1.0.1 (2023-06-14)

- Changed license from `GPL-3.0-or-later` to `AGPL-3.0-only`.
- Added an in-game config option to disable patch loading.
- Now has a (soft) dependency on [Nautilus].
- Patched batches are now placed in a stable location, `CompiledOctreesCache/patches`.

## v1.0.0 (2023-02-06)

- Switched modloader from QMM to BepInEx.
- Added support for the Living Large Update and What the Dock Update.
- Way more debug logging.
- Rewrote and restructured a lot of the code.

## v0.4 (2022-03-03)

- Removed connections to the Nexus pages.
- Better debug messages.

## v0.3 (2021-08-23)

- Better patch file loading.
- Custom load order specified in file.
- Error messages when loading a broken batch.
- Overall better error handling.

## v0.2 (2021-08-17)

- Added support for Below Zero.
- Fixed a bug that prevented new batches from being created.

## v0.1 (2021-08-09)

First pre-release.

[Nautilus]: https://github.com/SubnauticaModding/Nautilus
[Terrain Extender]: https://nexusmods.com/subnautica/mods/1454
