# TerrainPatcher

TerrainPatcher is a Subnautica and Below Zero library mod that allows modders and players to modify the game's terrain.

## Usage (Mod)

If you want to use a mod that requires TerrainPatcher, just extract the zip file and place the TerrainPatcher folder into your QMods folder.

To install a `.optoctreepatch` file, just place it anywhere in your QMods folder or any subfolder.

You can specify a custom load order for `.optoctreepatch` files by writing the file names (without the extensions) into the `load-order` file.

### Releases

You can download TerrainPatcher from the [releases page](https://github.com/Esper89/Subnautica-TerrainPatcher/releases/latest) (below the changelog).

## Usage (Library)

The following is for modders who want to use TerrainPatcher in your mod. Keep in mind that if your mod uses TerrainPatcher, anyone using your mod needs to have TerrainPatcher installed.

There are two ways that you can make your mod use TerrainPatcher.

### Separate Patch

One method is just distributing a `.optoctreepatch` file along with your mod and letting TerrainPatcher find it. This is less direct and may be more likely to cause confusion, but it's the easiest way of doing it.

### Embedded File

The other method is to add your `.optoctreepatch` file to your project, and set it as an embedded resource. You can then use `Assembly.GetManifestResourceStream` (as demonstrated in [`src/ExampleMod/EntryPoint.cs`](./src/ExampleMod/EntryPoint.cs)) to load the resource. If you pass this stream to `TerrainRegistry.PatchTerrain`, TerrainPatcher will apply your patch file. Below is an example of how to do this.

```cs
var asm = System.Reflection.Assembly.GetExecutingAssembly();
var patch = asm.GetManifestResourceStream("MyModName.my-file-name.optoctreepatch");
TerrainPatcher.TerrainRegistry.PatchTerrain(patch);
```

### mod.json

Regardless of which method you use, you'll need to add TerrainPatcher to your mod's `mod.json` file (as shown in [`src/ExampleMod/mod.json`](./src/ExampleMod/mod.json)). To do this, just add a list called `Dependencies`, and add `TerrainPatcher` as an item in the list. See below for an example.

```json
{
  "Id": "MyModName",
  "DisplayName": "My Mod's Display Name",
  "Author": "Esper89",
  "Version": "1.0",
  "Enable": true,
  "Game": "Subnautica",
  "AssemblyName": "MyModAssembly.dll",
  "Dependencies": [ "TerrainPatcher" ]
}
```

If your mod can function without TerrainPatcher (despite using terrain patches) then you don't need to add it as a dependency.

## Patch Format

The Subnautica Terrain Patch Format (the `optoctreepatch` format) is documented at [`doc/Subnautica Terrain Patch Format.pdf`](./doc/Subnautica%20Terrain%20Patch%20Format.pdf). The format is designed to be as similar to the game's native terrain format as possible, while still allowing for proper patching of terrain. The patch format also allows as much data as necessary to be stored in one file, for easier distribution.

Patch files can be generated using [Reef Editor](https://www.nexusmods.com/subnautica/mods/728), but any files conforming to the specification will work.

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

Patching biomes or entities is not planned for TerrainPatcher — that would be well outside it's scope. These features may be implemented in the future as a separate mod.

## Contributing

Contributions of any kind — issues, pull requests, feature requests — are all welcome. You can submit suggestions and bug reports [as an issue](https://github.com/Esper89/Subnautica-TerrainPatcher/issues/new/choose), or code contributions [as a pull request](https://github.com/Esper89/Subnautica-TerrainPatcher/pulls).

### Building

See [`build`](./build) for building TerrainPatcher.

## License

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
