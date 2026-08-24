using System.Collections;
using System.Security.Cryptography;

namespace TerrainPatcher;

static class TerrainPatching {
    internal static void ApplyTerrainPatch(string patchName, Stream patchFile, bool forceOriginal) {
        try {
            string message = $"Loading terrain patch '{patchName}'";
            if (patchFile.CanSeek) {
                long position = patchFile.Position;
                byte[] hash = MD5.Create().ComputeHash(patchFile);
                patchFile.Seek(position, SeekOrigin.Begin);

                string hex = BitConverter.ToString(hash).Replace("-", "");
                message += $" (MD5: {hex})";
            }
            Plugin.LogInfo(message);

            ApplyPatchFile(patchName, patchFile, forceOriginal);
        } catch (InvalidDataException ex) {
            Plugin.LogError($"Patch '{patchName}' is broken or contains errors: {ex.Message}");
            Plugin.DisplayError($"Error in terrain patch '{patchName}'");
        } catch (Exception ex) {
            Plugin.LogError($"Unexpected error applying patch '{patchName}': {ex}");
            Plugin.DisplayError($"Unexpected error applying terrain patch '{patchName}'");

            if (
                ex is IOException &&
                ex.Message.IndexOf("sharing violation", StringComparison.OrdinalIgnoreCase) >= 0
            ) {
                Plugin.LogInfo("Your antivirus may be preventing Terrain Patcher from working");
                Plugin.DisplayError("Your antivirus may be preventing Terrain Patcher from working");
            }
        }
    }

    internal static readonly Dictionary<Int3, PatchedBatch> patchedBatches = new();


    private static void ApplyPatchFile(string patchName, Stream patchFile, bool forceOriginal) {
        BinaryReader reader = new(patchFile);

        uint version;
        try { version = reader.ReadUInt32(); }
        catch (EndOfStreamException) {
            throw new InvalidDataException("patch is not large enough");
        }

        if (version == uint.MaxValue) {
            Plugin.LogWarning(
                $"Skipping application of patch '{patchName}' because of invalid version"
            );
            return;
        }

        if (version != 0) throw new InvalidDataException($"unknown patch version {version}");

        for (;;) {
            try {
                Int3? batchId = ReadBatchId(reader);
                if (batchId is null) break;
                var id = batchId.Value;

                Plugin.LogDebug($"Patching batch [{id.x}, {id.y}, {id.z}] for patch '{patchName}'");

                ApplyBatchPatch(patchName, reader, id, forceOriginal);
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

    private static void ApplyBatchPatch(
        string patchName,
        BinaryReader patch,
        Int3 batchId,
        bool forceOriginal
    ) {
        if (!patchedBatches.ContainsKey(batchId)) {
            CreateNewPatchedBatch(batchId);
        } else if (forceOriginal) {
            Plugin.LogInfo(
                $"Patch '{patchName}' forcefully resetting batch " +
                $"[{batchId.x}, {batchId.y}, {batchId.z}]"
            );

            CreateNewPatchedBatch(batchId);
        }

        PatchBatch(patchName, batchId, patch);
    }

    private static void CreateNewPatchedBatch(Int3 batchId) {
        string newPath = Path.Combine(
            PatchesDir.Path!,
            $"compiled-batch-{batchId.x}-{batchId.y}-{batchId.z}.optoctrees"
        );

        foreach (string dir in PatchesDir.ORIG_BATCH_DIRS) {
            string origPath = Path.Combine(
                SNUtils.InsideUnmanaged(dir),
                "CompiledOctreesCache",
                $"compiled-batch-{batchId.x}-{batchId.y}-{batchId.z}.optoctrees"
            );

            if (!File.Exists(origPath)) continue;
            
            using FileStream file = File.OpenRead(origPath);
            
            BinaryReader reader = new(file);

            if (reader.ReadUInt32() != 4u) continue;
                
            File.Copy(origPath, newPath, overwrite: true);
            patchedBatches[batchId] = new PatchedBatch(newPath);
            return;
        }

        using (FileStream file = File.Create(newPath)) {
            BinaryWriter writer = new(file);

            writer.Write(4u);
            for (int i = 0; i < 125; i++) {
                writer.Write((ushort)1);
                writer.Write(0u);
            }

            patchedBatches[batchId] = new PatchedBatch(newPath);
        }
    }

    private static void PatchBatch(string patchName, Int3 batchId, BinaryReader patch) {
        string path = patchedBatches[batchId].path;

        byte[] origBytes = File.ReadAllBytes(path);
        var original = new BinaryReader(new MemoryStream(buffer: origBytes, writable: false));
        original.ReadUInt32();

        BitArray patchedOctrees;

        using var targetFile = File.Open(path, FileMode.Create);
        BinaryWriter target = new(targetFile);

        target.Write(4u);
        try { patchedOctrees = PatchOctrees(target, original, patch); }
        catch (Exception) {
            targetFile.Seek(0, SeekOrigin.Begin);
            targetFile.Write(origBytes, 0, origBytes.Length);
            throw;
        }

        List<string>?[] octreePatchNames = patchedBatches[batchId].octreePatchNames;
        for (int i = 0; i < 125; i++)
        {
            if (!patchedOctrees[i]) continue;
            octreePatchNames[i] ??= new();
            List<string>? patches = octreePatchNames[i]!;

            if (patches.Count > 0) {
                string warning = $"patch '{patchName}' overrides ";
                warning += patches.Count == 1 ? "patch " : "patches [";
                warning += string.Join(",", patches.Select(patch => $"'{patch}'"));
                if (patches.Count > 1) { warning += "]"; }
                warning += $" in batch [{batchId.x}, {batchId.y}, {batchId.z}]";
                warning += $" at octree #{i}";

                Plugin.LogWarning(warning);
            }

            patches.Add(patchName);
        }
    }

    private static BitArray PatchOctrees(BinaryWriter target, BinaryReader original, BinaryReader patch) {
        BitArray patchedOctrees = new(125);
        byte patchedOctreeCount = patch.ReadByte();

        if (patchedOctreeCount > 125) throw new InvalidDataException("patch contains more octrees than the batch can contain");

        byte[]?[] octrees = new byte[125][];

        for (int i = 0; i < 125; i++) {
            try {
                octrees[i] = original.ReadBytes(original.ReadUInt16() * 4);
            } catch (EndOfStreamException) { break; }
        }

        for (int i = 0; i < patchedOctreeCount; i++) {
            try {
                byte octree = patch.ReadByte();
                byte[] bytes = patch.ReadBytes(patch.ReadUInt16() * 4);

                patchedOctrees[octree] = true;
                octrees[octree] = bytes;
            } catch (EndOfStreamException ex) {
                throw new InvalidDataException("patch ends too early", ex);
            } catch (Exception ex)
                when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) {
                throw new InvalidDataException("patch contains an octree outside the bounds of the batch it applies to", ex);
            }
        }

        foreach (byte[]? t in octrees)
        {
            if (t != null) {
                target.Write((ushort)(t.Length / 4));
                target.Write(t, 0, t.Length);
            } else {
                target.Write((ushort)1);
                target.Write(0u);
            }
        }

        return patchedOctrees;
    }
}

internal struct PatchedBatch {
    internal PatchedBatch(string path) {
        this.path = path;
        octreePatchNames = new List<string>?[125];
    }

    internal readonly string path;
    internal readonly List<string>?[] octreePatchNames;
}

internal static class PatchesDir {
    internal static readonly string[] ORIG_BATCH_DIRS = ["Build18", "Expansion"];
    
    static PatchesDir() {
        foreach (string origDirName in ORIG_BATCH_DIRS) {
            string? origDir = SNUtils.InsideUnmanaged(origDirName);

            if (!Directory.Exists(origDir)) continue;
            
            Path = System.IO.Path.Combine(origDir, "CompiledOctreesCache", "patches");
            Directory.CreateDirectory(Path);
            //TODO: Arguably the clearing of the old patches dir should be more explicit, within its own method than some automatic process
            //   Otherwise tho the checking of game versions is fine to do in a static constructor
            foreach (string? path in Directory.EnumerateFiles(Path)) {
                if (System.IO.Path.GetExtension(path) != ".optoctrees") continue;
                File.Delete(path);
            }
            break;
        }
    }
    
    internal static readonly string? Path;
}