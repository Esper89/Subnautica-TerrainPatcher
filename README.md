# TerrainPatcher

TerrainPatcher is a Subnautica and Below Zero library mod that allows modders and players to modify the game's terrain.

## Usage

### End Users

If you want to use a mod that requires TerrainPatcher, just extract the zip file and place the TerrainPatcher folder into your QMods folder.

To install a `.optoctreepatch` file, just place it anywhere in your QMods folder or any subfolder.

#### Releases

You can download TerrainPatcher from the [releases page](https://github.com/Esper89/Subnautica-TerrainPatcher/releases/latest) (below the changelog).

You can also download TerrainPatcher from the [Subnautica Nexus](https://www.nexusmods.com/subnautica/mods/823?tab=files) page or the [Below Zero Nexus](https://www.nexusmods.com/subnauticabelowzero/mods/236?tab=files) page.

### Modders

The following is for modders who want to use TerrainPatcher in your mod. Keep in mind that if your mod uses TerrainPatcher, anyone using your mod needs to have TerrainPatcher installed.

There are two ways that you can make your mod use TerrainPatcher.

#### Separate Patch

One method is just distributing a `.optoctreepatch` file along with your mod and letting TerrainPatcher pick up on it. This is less direct and may be more likely to cause confusion, but it's the easiest way of doing it.

#### Embedded File

The other method is to add your `.optoctreepatch` file to your project, and set it as an embedded resource. You can then use `Assembly.GetManifestResourceStream` (as demonstrated in [`src/ExampleMod/EntryPoint.cs`](./src/ExampleMod/EntryPoint.cs)) to load the resource. If you pass this stream to `TerrainRegistry.PatchTerrain`, TerrainPatcher will apply your patch file. Below is an example of how to do this.

```cs
Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
Stream patch = asm.GetManifestResourceStream("MyModName.my-file-name.optoctreepatch");
TerrainPatcher.TerrainRegistry.PatchTerrain(patch);
```

#### mod.json

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
  "Dependencies": [ "TerrainPatcher" ],
}
```

If your mod can function without TerrainPatcher (despite using terrain patches) then you don't need to add it as a dependency.

#### Nexus

If your mod uses TerrainPatcher and you publish your mod on nexus, it is recommended to add TerrainPatcher as a required mod (in the "Requirements and mirrors" tab of the mod creation page), the same way you would add QModManager. This will notify people downloading your mod that they need to download this mod as well.

## Patch Format

The Subnautica Terrain Patch Format, or the `optoctreepatch` format, is documented at `docs/Subnautica Terrain Patch Format.pdf`. The format is designed to be as similar to the game's native terrain format as possible, while still allowing for proper patching of terrain. The patch format also allows as much data as necessary to be stored in one file, for easier distribution.

Patch files can be generated using [Reef Editor](https://www.nexusmods.com/subnautica/mods/728), but any files conforming to the specification will work.

## Features

 - Modifying the game's terrain in a modular way.

 - Replacing some parts of a batch without replacing the whole batch.

 - As many world modifications as necessary can fit in one file.

 - No actual changes to game files; uses temporary files to store patched terrain.

 - Multiple patches can modify the same batch without causing problems.

 - Support for Subnautica and Below Zero.

 - Cool rocks.
 
### Planned Features
 
 - Extending the current edge of the world to allow for more distant terrain.

 - (Hopefully) Custom biomes and entities to go with the terrain modifications.

## Contributing

Contributions of any kind - issues, pull requests, feature requests - are all welcome. You can submit suggestions and bug reports [as an issue](https://github.com/Esper89/Subnautica-TerrainPatcher/issues/new/choose), or code contributions [as a pull request](https://github.com/Esper89/Subnautica-TerrainPatcher/pulls).

### Scope

TerrainPatcher's scope is not necessarily limited to terrain; as mentioned above, hopefully custom biomes and entities could be added as well.

TerrainPatcher is a library mod, so it shouldn't change the game unless the player or another mod tells it to.

### Building

See [`build/README.md`](./build/README.md) for building TerrainPatcher.

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
