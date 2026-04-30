namespace TerrainPatcher;

public static class TerrainRegistry {
    [Obsolete(
        "This method is deprecated; instead, load terrain patches by distributing them as"
        + " individual files alongside your mod"
    )]
    public static void PatchTerrain(
        string? patchName,
        Stream? patchFile,
        bool forceOriginal = false
    ) {
        if (patchName is null) throw new ArgumentNullException(nameof(patchName));
        if (patchFile is null) throw new ArgumentNullException(nameof(patchFile));
        TerrainPatching.ApplyTerrainPatch(patchName, patchFile, forceOriginal);
    }
}
