# Terrain Patcher

Terrain Patcher is a Subnautica and Below Zero library mod that allows modders and players to modify
the game's terrain.

## Installation

This mod requires [BepInEx](https://github.com/toebeann/BepInEx.Subnautica).

You can download Terrain Patcher from the
[releases page](https://github.com/Esper89/Subnautica-TerrainPatcher/releases/latest) (below the
changelog), or from the [Subnautica](https://submodica.xyz/mods/sn1/240) and
[Below Zero](https://submodica.xyz/mods/sbz/241) Submodica pages.

To install Terrain Patcher, just extract the zip file and place the `TerrainPatcher` folder into
your `BepInEx/plugins` folder.

To install a `.optoctreepatch` file, place it in the `TerrainPatcher/patches` folder.

You can specify a custom load order for `.optoctreepatch` files by writing the file names (without
the extensions) into the `load-order.txt` file.

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
TerrainPatcher.TerrainRegistry.PatchTerrain(patch);
```

### Dependency Registration

If your mod can function without Terrain Patcher (despite including terrain patches) then you don't
need to add it as a dependency, and you can skip this section.

Regardless of which method you use to load patches, you'll need to add Terrain Patcher as a
dependency of your mod (as shown in [`ExampleMod.cs`](./examples/ExampleMod.cs)). To do this, just
add the `[BepInDependency("Esper89.TerrainPatcher")]` attribute to your mod's entry point, below the
`BepInPlugin` attribute, as shown here:

```cs
[BepInPlugin("YourName.ExampleMod", "Example Mod", "0.0.0")]
[BepInDependency("Esper89.TerrainPatcher")]
internal class Mod : BaseUnityPlugin { /* ... */ }
```

## Patch Format

The Subnautica Terrain Patch Format (the `optoctreepatch` format) is documented in
[`Subnautica Terrain Patch Format.pdf`](./doc/Subnautica%20Terrain%20Patch%20Format.pdf). The format
is designed to be as similar to the game's native terrain format as possible, while still allowing
for proper patching of terrain. The patch format also allows as much data as necessary to be stored
in one file, for easier distribution.

An example `optoctreepatch` file is included at
[`example.optoctreepatch`](./examples/example.optoctreepatch).

Patch files can be generated using [Reef Editor](https://github.com/eternaight/sn-terrain-edit), but
any files conforming to the specification will work.

## Features

- Modifying the game's terrain in a modular way.

- Replacing some parts of a batch without replacing the whole batch.

- As many world modifications as necessary can fit in one file.

- No actual changes to game files; uses temporary files to store patched terrain.

- Multiple patches can modify the same batch without causing problems.

- Support for Subnautica and Below Zero.

- Easily load patches without making a mod, or have more control by using a mod.

- Custom load order.

- Cool rocks.

### Planned Features

- Extending the current edge of the world to allow for more terrain.

- Patching biomes and entities.

## Contributing

Contributions of any kind—issues, pull requests, feature requests—are all welcome. You can submit
suggestions and bug reports
[as an issue](https://github.com/Esper89/Subnautica-TerrainPatcher/issues/new/choose), or code
contributions [as a pull request](https://github.com/Esper89/Subnautica-TerrainPatcher/pulls).

### Building

To build Terrain Patcher, run `msbuild` in the project's root directory. This will build in debug
mode, and the output will be placed in `target/Debug`. If you create a file in the project root
called `game-dirs.txt` and input the paths to your Subnautica and/or Below Zero installations (one
per line), the output of debug builds will be automatically installed into those game directories
for easier testing.

To build Terrain Patcher in release mode, run `msbuild -property:Configuration=Release`. This will
also create a `dist.zip` file in `target` for easy distribution.

## License

```
Copyright (c) 2021 Esper Thomson

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
```
