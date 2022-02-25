# Makefile for TerrainPatcher.

.PHONY: release example debug clean dist

# Build the mod in release mode.
release:
	msbuild src/TerrainPatcher -property:Configuration=Release

# Build the mod and the example project in release mode.
example:
	msbuild src/ExampleMod -property:Configuration=Release

# Build the mod and the example project in debug mode.
debug:
	msbuild src/TerrainPatcher -property:Configuration=Debug
	msbuild src/ExampleMod -property:Configuration=Debug

# Empty out the target directory.
clean:
	rm -rf build/target/*

# Build the mod in release mode and zip up the result for distribution.
dist: clean release
	rm -f build-target.zip
	cd build/target; zip -r ../../build-target.zip *; cd ../..
