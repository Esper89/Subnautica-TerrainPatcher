# Building

Bleeding-edge builds can be downloaded from [GitHub actions](https://github.com/Esper89/Subnautica-TerrainPatcher/actions), or build yourself.

TerrainPatcher's build system uses `msbuild` and `make`. If you do not have `make` installed, you can check [`Makefile`](./MakeFile) to see which `msbuild` commands correspond to which `make` targets.

To build TerrainPatcher normally, run

```bash
make
```

or

```bash
make release
```

The result will be in [`build/target`](./build/target).

To build TerrainPatcher and ExampleMod, run

```bash
make example
```

To build TerrainPatcher and ExampleMod in debug mode, you first need to type the full path to your Subnautica QMods folder into a new text file at [`build/mods-dir`](./build/mods-dir).

Then, run

```bash
make debug
```

TerrainPatcher and ExampleMod (along with their `mod.json` files) will be automatically copied to your mods folder.

