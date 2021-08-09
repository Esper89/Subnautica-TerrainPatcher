# Targets:
#	release         Build the mod in release mode.
#	example         Build the mod and the example project in release mode.
#	debug           Build the mod and the example project in debug mode.
#	dist            Build the mod and zip up the result for distribution.

SHELL = /bin/sh

BUILD = mdtool build src/$(NAME).sln

.PHONY: all release debug dist

release:
	msbuild src/TerrainPatcher -property:Configuration=Release

example:
	msbuild src/ExampleMod -property:Configuration=Release

debug:
	msbuild src/TerrainPatcher -property:Configuration=Debug
	msbuild src/ExampleMod -property:Configuration=Debug

dist: release
	zip -r build-target.zip build/target/*
