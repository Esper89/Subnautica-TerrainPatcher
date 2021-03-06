# Building

Bleeding-edge builds can be downloaded from [GitHub actions](https://github.com/Esper89/Subnautica-TerrainPatcher/actions), or built yourself.

TerrainPatcher's build system uses `msbuild` and `make`. If you do not have `make` installed, you can check [`Makefile`](../Makefile) to see which `msbuild` commands correspond to which `make` targets.

There is no need to copy any Subnautica assemblies, or to change the project's references. The necessary reference assemblies are in [`build/refasm`](./refasm).

First, clone the repository.
```bash
git clone https://github.com/Esper89/Subnautica-TerrainPatcher.git
cd Subnautica-TerrainPatcher
```

To build TerrainPatcher normally, run

```bash
make
```

or

```bash
make release
```

The result will be in [`build/target`](./target).

To build TerrainPatcher and ExampleMod, run

```bash
make example
```

To build TerrainPatcher and ExampleMod in debug mode, you first need to type the full path to your Subnautica QMods folder into a new text file at [`build/sn-mods-dir`](./sn-mods-dir), and the path to your Below Zero QMods folder into [`build/bz-mods-dir`](./bz-mods-dir).

Then, run

```bash
make debug
```

TerrainPatcher and ExampleMod (along with their `mod.json` files) will be automatically copied to your Subnautica and Below Zero QMods folders.
