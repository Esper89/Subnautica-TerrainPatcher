# Terrain Patcher v1.2.5

Terrain Patcher is a Subnautica and Below Zero library mod that allows anyone to modify the game's
terrain.

Terrain Patcher loads terrain patches containing terrain to replace (stored as `.optoctreepatch`
files) and makes the game load that terrain instead of its vanilla terrain.

## Installation

To install Terrain Patcher, place the `TerrainPatcher` folder (containing `TerrainPatcher.dll`) in
your `BepInEx/plugins` folder. To install a `.optoctreepatch` file manually, place it in the
`TerrainPatcher/patches` folder. You can also place a terrain patch anywhere in the `BepInEx` folder
or any subfolder and it will still be loaded.

## Configuration

Patch load order can be configured in `load-order.txt`. Each line should be the name of the patch to
reorder. If you have multiple terrain patches that conflict with each other, changing the load order
usually won't fix broken terrain—the terrain patches are likely just incompatible.

## Repository

Terrain Patcher's source code and documentation can be found in its [GitHub
repository](https://github.com/Esper89/Subnautica-TerrainPatcher).

## Contributors

- Esper Thomson ([`@Esper89`](https://github.com/Esper89))

- Metious ([`@Metious`](https://github.com/Metious))

- Jbeast ([`@jbeast291`](https://github.com/jbeast291))

- Aerith Butler ([`@jonahnm`](https://github.com/jonahnm))

## License

Copyright © 2021, 2023–2026 Esper Thomson

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
