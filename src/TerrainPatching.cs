using System.Collections;
using System.Security.Cryptography;

namespace TerrainPatcher;

static class TerrainPatching {
    internal static void ApplyTerrainPatch(string patchName, Stream patchFile, bool forceOriginal) {
        try {
            var message = $"Loading terrain patch '{patchName}'";
            if (patchFile.CanSeek) {
                var position = patchFile.Position;
                var hash = MD5.Create().ComputeHash(patchFile);
                patchFile.Seek(position, SeekOrigin.Begin);

                var hex = BitConverter.ToString(hash).Replace("-", "");
                message += $" (MD5: {hex})";
            }
            Mod.LogInfo(message);

            TerrainPatching.ApplyPatchFile(patchName, patchFile, forceOriginal);
        } catch (InvalidDataException ex) {
            Mod.LogError($"Patch '{patchName}' is broken or contains errors: {ex.Message}");
            Mod.DisplayError($"Error in terrain patch '{patchName}'");
        } catch (Exception ex) {
            Mod.LogError($"Unexpected error applying patch '{patchName}': {ex}");
            Mod.DisplayError($"Unexpected error applying terrain patch '{patchName}'");

            if (
                ex is IOException &&
                ex.Message.IndexOf("sharing violation", StringComparison.OrdinalIgnoreCase) >= 0
            ) {
                Mod.LogInfo("Your antivirus may be preventing Terrain Patcher from working");
                Mod.DisplayError("Your antivirus may be preventing Terrain Patcher from working");
            }
        }
    }

    internal static readonly Dictionary<Int3, PatchedBatch> patchedBatches = new();

    internal struct PatchedBatch {
        internal PatchedBatch(string path) {
            this.path = path;
            this.octreePatchNames = new List<string>?[125];
        }

        internal string path;
        internal List<string>?[] octreePatchNames;
    }

    static void ApplyPatchFile(string patchName, Stream patchFile, bool forceOriginal) {
        var reader = new BinaryReader(patchFile);

        uint version;
        try { version = reader.ReadUInt32(); }
        catch (EndOfStreamException) {
            throw new InvalidDataException("patch is not large enough");
        }

        if (version == uint.MaxValue) {
            Mod.LogWarning(
                $"Skipping application of patch '{patchName}' because of invalid version"
            );
            return;
        }

        if (version != 0) throw new InvalidDataException($"unknown patch version {version}");

        for (;;) {
            try {
                var batchId = ReadBatchId(reader);
                if (batchId is null) break;
                var id = batchId.Value;

                Mod.LogDebug($"Patching batch [{id.x}, {id.y}, {id.z}] for patch '{patchName}'");

                TerrainPatching.ApplyBatchPatch(patchName, reader, id, forceOriginal);
            } catch (EndOfStreamException ex) {
                throw new InvalidDataException("patch ends too early", ex);
            }
        }

        static Int3? ReadBatchId(BinaryReader patch) {
            byte first;
            try { first = patch.ReadByte(); } catch (EndOfStreamException) { return null; }

            return new Int3(
                first | (patch.ReadSByte() << 8),
                patch.ReadInt16(),
                patch.ReadInt16()
            );
        }
    }

    static void ApplyBatchPatch(
        string patchName,
        BinaryReader patch,
        Int3 batchId,
        bool forceOriginal
    ) {
        if (!TerrainPatching.patchedBatches.ContainsKey(batchId)) {
            TerrainPatching.CreateNewPatchedBatch(batchId);
        } else if (forceOriginal) {
            Mod.LogInfo(
                $"Patch '{patchName}' forcefully resetting batch " +
                $"[{batchId.x}, {batchId.y}, {batchId.z}]"
            );

            TerrainPatching.CreateNewPatchedBatch(batchId);
        }

        TerrainPatching.PatchBatch(patchName, batchId, patch);
    }

    static void CreateNewPatchedBatch(Int3 batchId) {
        var newPath = Path.Combine(
            TerrainPatching.PatchesDir.Path,
            $"compiled-batch-{batchId.x}-{batchId.y}-{batchId.z}.optoctrees"
        );

        foreach (var dir in TerrainPatching.ORIG_BATCH_DIRS) {
            var origPath = Path.Combine(
                SNUtils.InsideUnmanaged(dir),
                "CompiledOctreesCache",
                $"compiled-batch-{batchId.x}-{batchId.y}-{batchId.z}.optoctrees"
            );

            if (File.Exists(origPath)) {
                using (var file = File.OpenRead(origPath)) {
                    var reader = new BinaryReader(file);

                    if (reader.ReadUInt32() == 4) {
                        File.Copy(origPath, newPath, overwrite: true);
                        TerrainPatching.patchedBatches[batchId] = new PatchedBatch(newPath);
                        return;
                    }
                }
            }
        }

        using (var file = File.Create(newPath)) {
            var writer = new BinaryWriter(file);

            writer.Write((uint)4);
            for (var i = 0; i < 125; i++) {
                writer.Write((ushort)1);
                writer.Write((uint)0);
            }

            TerrainPatching.patchedBatches[batchId] = new PatchedBatch(newPath);
        }
    }

    static readonly string[] ORIG_BATCH_DIRS = [
        "Build18",
        "Expansion",
    ];

    sealed class PatchesDir {
        PatchesDir() {
            foreach (var origDirName in TerrainPatching.ORIG_BATCH_DIRS) {
                var origDir = SNUtils.InsideUnmanaged(origDirName);

                if (Directory.Exists(origDir)) {
                    this.path = System.IO.Path.Combine(origDir, "CompiledOctreesCache", "patches");
                    Directory.CreateDirectory(this.path);

                    foreach (var path in Directory.EnumerateFiles(this.path)) {
                        if (System.IO.Path.GetExtension(path) == ".optoctrees") {
                            File.Delete(path);
                        }
                    }

                    break;
                }
            }
        }

        string? path;

        static PatchesDir? instance;
        internal static string? Path {
            get {
                PatchesDir.instance ??= new PatchesDir();
                return PatchesDir.instance.path;
            }
        }
    }

    static void PatchBatch(string patchName, Int3 batchId, BinaryReader patch) {
        var path = TerrainPatching.patchedBatches[batchId].path;

        var origBytes = File.ReadAllBytes(path);
        var original = new BinaryReader(new MemoryStream(buffer: origBytes, writable: false));
        original.ReadUInt32();

        BitArray patchedOctrees;

        using (var targetFile = File.Open(path, FileMode.Create)) {
            var target = new BinaryWriter(targetFile);

            target.Write((uint)4);
            try { patchedOctrees = TerrainPatching.PatchOctrees(target, original, patch); }
            catch (Exception) {
                targetFile.Seek(0, SeekOrigin.Begin);
                targetFile.Write(origBytes, 0, origBytes.Length);
                throw;
            }
        }

        var octreePatchNames = TerrainPatching.patchedBatches[batchId].octreePatchNames;
        for (var i = 0; i < 125; i++) {
            if (patchedOctrees[i]) {
                octreePatchNames[i] ??= new List<string>();
                var patches = octreePatchNames[i]!;

                if (patches.Count > 0) {
                    var warning = $"patch '{patchName}' overrides ";
                    warning += patches.Count == 1 ? "patch " : "patches [";
                    warning += string.Join(",", patches.Select(patch => $"'{patch}'"));
                    if (patches.Count > 1) { warning += "]"; }
                    warning += $" in batch [{batchId.x}, {batchId.y}, {batchId.z}]";
                    warning += $" at octree #{i}";

                    Mod.LogWarning(warning);
                }

                patches.Add(patchName);
            }
        }
    }

    static BitArray PatchOctrees(BinaryWriter target, BinaryReader original, BinaryReader patch) {
        var patchedOctrees = new BitArray(125);
        var patchedOctreeCount = patch.ReadByte();

        if (patchedOctreeCount > 125) throw new InvalidDataException(
            "patch contains more octrees than the batch can contain"
        );

        var octrees = new byte[125][];

        for (var i = 0; i < 125; i++) {
            try {
                octrees[i] = original.ReadBytes(original.ReadUInt16() * 4);
            } catch (EndOfStreamException) { break; }
        }

        for (var i = 0; i < patchedOctreeCount; i++) {
            try {
                var octree = patch.ReadByte();
                var bytes = patch.ReadBytes(patch.ReadUInt16() * 4);

                patchedOctrees[octree] = true;
                octrees[octree] = bytes;
            } catch (EndOfStreamException ex) {
                throw new InvalidDataException("patch ends too early", ex);
            } catch (Exception ex)
                when (ex is IndexOutOfRangeException || ex is ArgumentOutOfRangeException) {
                throw new InvalidDataException(
                    "patch contains an octree outside the bounds of the batch it applies to",
                    ex
                );
            }
        }

        for (var i = 0; i < octrees.Length; i++) {
            if (octrees[i] is object) {
                target.Write((ushort)(octrees[i].Length / 4));
                target.Write(octrees[i], 0, octrees[i].Length);
            } else {
                target.Write((ushort)1);
                target.Write((uint)0);
            }
        }

        return patchedOctrees;
    }
}
