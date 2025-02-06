# Terrain Patcher

Terrain Patcher is a Subnautica and Below Zero library mod that allows modders and players to modify
the game's terrain.

## Installation

This mod requires [BepInEx](https://github.com/toebeann/BepInEx.Subnautica) and
[Nautilus](https://github.com/SubnauticaModding/Nautilus). The Nautilus dependency is optional but
recommended.

You can download Terrain Patcher from the [releases
page](https://github.com/Esper89/Subnautica-TerrainPatcher/releases/latest), below the changelog.

To install Terrain Patcher, just extract the zip file and place the `TerrainPatcher` folder into
your `BepInEx/plugins` folder.

To install a `.optoctreepatch` file, place it in the `TerrainPatcher/patches` folder.

You can specify a custom load order for `.optoctreepatch` files by writing the file names (without
the extensions) into the `load-order.txt` file.

Loading patch files can be enabled/disabled from the in-game config menu if Nautilus is installed.

### Legacy Version

Some mods, such as [Sea To Sea](https://reikakalseki.github.io/subnautica/mods/seatosea.html), may
require using the legacy QModManager version of Terrain Patcher instead of the current BepInEx
version. The legacy version can be downloaded from the [releases
page](https://github.com/Esper89/Subnautica-TerrainPatcher/releases/tag/v0.4), below the changelog.
Installation and usage instructions for the legacy version can be found [in an older version of the
repository](https://github.com/Esper89/Subnautica-TerrainPatcher/tree/v0.4). Please do not submit
issues for bugs encountered on an old version of Terrain Patcher.

### Terrain Extender

Current versions of Terrain Patcher provide all the features that Terrain Extender provided and
satisfy other mods' dependencies on Terrain Extender. Terrain Extender is deprecated and you should
uninstall Terrain Extender if you have it installed.

## Library Usage

The following is for modders who want to use Terrain Patcher in your mod. Keep in mind that if your
mod uses Terrain Patcher, anyone using your mod needs to have Terrain Patcher installed.

### Patch Loading

There are two ways to load a terrain patch. The easiest way is to distribute your `.optoctreepatch`
file alongside your mod. Terrain Patcher will find and load all `.optoctreepatch` files placed
anywhere in the `BepInEx/plugins` folder or any subfolder.

Alternatively, you can add your `.optoctreepatch` file to your project as an embedded resource and
add `TerrainPatcher.dll` as a reference. You can then use `Assembly.GetManifestResourceStream` (as
demonstrated in [`ExampleMod.cs`](./examples/ExampleMod.cs)) to load the resource. If you pass this
stream to `TerrainRegistry.PatchTerrain`, Terrain Patcher will apply your patch file. Below is an
example of how to do this.

```cs
var asm = System.Reflection.Assembly.GetExecutingAssembly();
var patch = asm.GetManifestResourceStream("MyModName.my-file-name.optoctreepatch");
TerrainPatcher.TerrainRegistry.PatchTerrain("my-file-name", patch);
```

Using `TerrainRegistry.PatchTerrain` to load terrain patches may be deprecated in a future release.

### Dependency Registration

Regardless of which method you use to load terrain patches, you'll need to add Terrain Patcher as a
dependency of your mod.

If your mod **requires** Terrain Patcher to function, add the
`[BepInDependency("Esper89.TerrainPatcher")]` attribute to your mod's entry point (below the
`BepInPlugin` attribute), as shown here:

```cs
[BepInPlugin("YourName.ExampleMod", "Example Mod", "0.0.0")]
[BepInDependency("Esper89.TerrainPatcher")]
internal class Mod : BaseUnityPlugin { /* ... */ }
```

If your mod **does not require** Terrain Patcher but still uses it, add the
`[BepInDependency("Esper89.TerrainPatcher", BepInDependency.DependencyFlags.SoftDependency)]`
attribute to your mod's entry point (below the `BepInPlugin` attribute), as shown here:

```cs
[BepInPlugin("YourName.ExampleMod", "Example Mod", "0.0.0")]
[BepInDependency("Esper89.TerrainPatcher", BepInDependency.DependencyFlags.SoftDependency)]
internal class Mod : BaseUnityPlugin { /* ... */ }
```

### Licensing

Terrain Patcher is licensed under the GNU AGPL, which says that derivative works must also be
licensed under the GNU AGPL. If your mod directly interacts with Terrain Patcher (e.g. by
referencing `TerrainPatcher.dll` and calling `TerrainPatcher.TerrainRegistry.PatchTerrain`), it
might be considered a derivative work. To avoid any possible copyright issues, if your mod isn't
licensed under the GNU AGPL, you should avoid referencing `TerrainPatcher.dll` or otherwise
interacting with Terrain Patcher directly. Patches can still be loaded without referencing Terrain
Patcher by distributing them alongside your mod as separate `.optoctreepatch` files.

## Patch Format

The Subnautica Terrain Patch Format (the `optoctreepatch` format) is documented in [`Subnautica
Terrain Patch Format.pdf`](./doc/Subnautica%20Terrain%20Patch%20Format.pdf). The format
is designed to be as similar to the game's native terrain format as possible, while still allowing
for proper patching of terrain. The patch format also allows as much data as necessary to be stored
in one file, for easier distribution.

An example `optoctreepatch` file is included at
[`example.optoctreepatch`](./examples/example.optoctreepatch). Patches that demonstrate all valid
materials in-game can be found for Subnautica and Below Zero at
[`material-preview-sn.optoctreepatch`](./examples/material-preview-sn.optoctreepatch) and
[`material-preview-bz.optoctreepatch`](./examples/material-preview-bz.optoctreepatch).

Patch files can be created using [Reef Editor](https://github.com/eternaight/sn-terrain-edit). Any
files conforming to the specification will work.

Terrain Patcher places patched batches in `CompiledOctreesCache/patches`, using the same naming
system as the game. These patched batches can be loaded by external tools or other mods, if they
wish to support terrain patches. Anything using this feature should make it optional, as those files
aren't removed when Terrain Patcher is disabled or uninstalled.

## Features

- Modifying the game's terrain in a modular way.

- Replacing some parts of a batch without replacing the whole batch.

- As many world modifications as necessary can fit in one patch file.

- No actual changes to game files.

- Support for both Subnautica and Below Zero.

- Easily load patches without making a mod.

- Custom load order.

- Enabling and disabling patch loading in-game.

- Extending the current edge of the world to allow for more terrain.

- Extending the edge of the world where entities can spawn and save.

### Planned Features

- Patching the in-game seaglide and scanner room maps.

- Patching biomes and entities.

## Contributing

Contributions of any kind—issues, pull requests, feature requests—are all welcome. You can submit
suggestions and bug reports [as an
issue](https://github.com/Esper89/Subnautica-TerrainPatcher/issues/new/choose), or code
contributions [as a pull request](https://github.com/Esper89/Subnautica-TerrainPatcher/pulls).

### Building

To build Terrain Patcher, run `dotnet build` in the project's root directory. This will build in
debug mode, and the output will be placed in `target/Debug`. If you create a file in the project
root called `game-dirs.txt` and input the paths to your Subnautica and/or Below Zero installations
(one per line), the output of debug builds will be automatically installed into those game
directories for easier testing.

To build Terrain Patcher in release mode, run `dotnet build --configuration Release`. This will
also create a `dist.zip` file in `target` for easy distribution.

### Contributors

- Esper Thomson ([@Esper89](https://github.com/Esper89))

- Metious ([@Metious](https://github.com/Metious))

- Jonah Butler ([@jonahnm](https://github.com/jonahnm))

## License

Copyright © 2021, 2023–2025 Esper Thomson

This program is free software: you can redistribute it and/or modify it under the terms of version
3 of the GNU Affero General Public License as published by the Free Software Foundation.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without
even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero
General Public License for more details.

You should have received a copy of the GNU Affero General Public License along with this program.
If not, see <https://www.gnu.org/licenses>.

Additional permission under GNU AGPL version 3 section 7

If you modify this Program, or any covered work, by linking or combining it with Subnautica (or a
modified version of that program), containing parts covered by the terms of its license, the
licensors of this Program grant you additional permission to convey the resulting work.
