# Targets:
#	release         Build the mod in release mode.
#	example         Build the mod and the example project in release mode.
#	debug           Build the mod and the example project in debug mode.
#	clean           Empty out the target directory.
#	dist            Build the mod and zip up the result for distribution.

SHELL = /bin/sh

.PHONY: release example debug clean dist

release:
	msbuild src/TerrainPatcher -property:Configuration=Release

example:
	msbuild src/ExampleMod -property:Configuration=Release

debug:
	msbuild src/TerrainPatcher -property:Configuration=Debug
	msbuild src/ExampleMod -property:Configuration=Debug

clean:
	rm -r build/target/*

dist: clean release
	zip -r build-target.zip build/target/*
