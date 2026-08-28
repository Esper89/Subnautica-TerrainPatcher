namespace TerrainPatcher;

internal struct PatchedBatch {
    internal PatchedBatch(string path) {
        this.path = path;
        octreePatchNames = new List<string>?[125];
    }
    
    internal readonly string path;
    internal readonly List<string>?[] octreePatchNames;
}