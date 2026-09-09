using System.Collections;
using System.Security.Cryptography;

namespace TerrainPatcher.TerrainPatching;

internal static class PatchTerrain {
    private const int OCTREES_PER_BATCH = 125;

    internal static readonly Dictionary<Int3, PatchedBatch> patchedBatches = new();

    internal readonly struct PatchedBatch {
        internal PatchedBatch(string path) {
            this.path = path;
            octreePatchNames = new List<string>?[OCTREES_PER_BATCH];
        }

        internal readonly string path;
        internal readonly List<string>?[] octreePatchNames;
    }

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
                Plugin.DisplayError(
                    "Your antivirus may be preventing Terrain Patcher from working"
                );
            }
        }
    }

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

        while (TryReadBatchId(out Int3 id)) {
            try {
                Plugin.LogDebug($"Patching batch [{id.x}, {id.y}, {id.z}] for patch '{patchName}'");
                ApplyBatchPatch(patchName, reader, id, forceOriginal);
            } catch (EndOfStreamException ex) {
                throw new InvalidDataException("patch ends too early", ex);
            }
        }

        bool TryReadBatchId(out Int3 id) {
            try {
                id = new Int3(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
                return true;
            } catch (EndOfStreamException) {
                id = default;
                return false;
            }
        }
    }

    private static void ApplyBatchPatch(
        string patchName, BinaryReader patch, Int3 batchId, bool forceOriginal
    ) {
        if (!patchedBatches.ContainsKey(batchId)) CreateNewPatchedBatch(batchId);
        else if (forceOriginal) {
            Plugin.LogInfo(
                $"Patch '{patchName}' forcefully resetting batch [{batchId.x}, {batchId.y}, " +
                $"{batchId.z}]"
            );
            CreateNewPatchedBatch(batchId);
        }

        PatchBatch(patchName, batchId, patch);
    }

    private static void CreateNewPatchedBatch(Int3 batchId) {
        string fileName = $"compiled-batch-{batchId.x}-{batchId.y}-{batchId.z}.optoctrees";
        string newPath = Path.Combine(OptoctreesDirs.PatchesPath, fileName);
        string origPath = Path.Combine(OptoctreesDirs.OriginalPath, fileName);

        if (!CopyBaseGame()) WriteEmpty();

        bool CopyBaseGame() {
            if (!File.Exists(origPath)) return false;
            using FileStream origfile = File.OpenRead(origPath);

            BinaryReader reader = new(origfile);
            if (reader.ReadUInt32() != 4u) return false;

            File.Copy(origPath, newPath, overwrite: true);
            patchedBatches[batchId] = new PatchedBatch(newPath);
            return true;
        }

        void WriteEmpty() {
            using FileStream newFile = File.Create(newPath);
            BinaryWriter writer = new(newFile);
            writer.Write(4u);
            for (int i = 0; i < OCTREES_PER_BATCH; i++) {
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

        using var targetFile = File.Open(path, FileMode.Create);
        BinaryWriter target = new(targetFile);
        target.Write(4u);

        BitArray patchedOctrees;
        try { patchedOctrees = PatchOctrees(target, original, patch); }
        catch (Exception) {
            targetFile.Seek(0, SeekOrigin.Begin);
            targetFile.Write(origBytes, 0, origBytes.Length);
            throw;
        }

        List<string>?[] octreePatchNames = patchedBatches[batchId].octreePatchNames;
        for (int i = 0; i < OCTREES_PER_BATCH; i++) {
            if (!patchedOctrees[i]) continue;
            octreePatchNames[i] ??= new();
            List<string> patches = octreePatchNames[i]!;

            if (patches.Count > 0) {
                string warning = $"patch '{patchName}' overrides ";
                warning += patches.Count == 1 ? "patch " : "patches [";
                warning += string.Join(",", patches.Select(patchName => $"'{patchName}'"));
                if (patches.Count > 1) { warning += "]"; }
                warning += $" in batch [{batchId.x}, {batchId.y}, {batchId.z}]";
                warning += $" at octree #{i}";
                Plugin.LogWarning(warning);
            }
            patches.Add(patchName);
        }
    }

    private static BitArray PatchOctrees(
        BinaryWriter target, BinaryReader original, BinaryReader patch
    ) {
        BitArray patchedOctrees = new(OCTREES_PER_BATCH);
        byte patchedOctreeCount = patch.ReadByte();

        if (patchedOctreeCount > OCTREES_PER_BATCH) {
            throw new InvalidDataException(
                "patch contains more octrees than the batch can contain"
            );
        }

        byte[]?[] octrees = new byte[OCTREES_PER_BATCH][];

        const int OCTREE_NODE_SIZE = 4;

        for (int i = 0; i < OCTREES_PER_BATCH; i++) {
            try { octrees[i] = original.ReadBytes(original.ReadUInt16() * OCTREE_NODE_SIZE); }
            catch (EndOfStreamException) { break; }
        }

        for (int i = 0; i < patchedOctreeCount; i++) {
            try {
                byte octree = patch.ReadByte();
                byte[] bytes = patch.ReadBytes(patch.ReadUInt16() * OCTREE_NODE_SIZE);

                patchedOctrees[octree] = true;
                octrees[octree] = bytes;
            } catch (EndOfStreamException ex) {
                throw new InvalidDataException("patch ends too early", ex);
            } catch (Exception ex)
                when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) {
                throw new InvalidDataException(
                    "patch contains an octree outside the bounds of the batch it applies to", ex
                );
            }
        }

        foreach (byte[]? t in octrees) {
            if (t != null) {
                target.Write((ushort)(t.Length / OCTREE_NODE_SIZE));
                target.Write(t, 0, t.Length);
            } else {
                target.Write((ushort)1);
                target.Write(0u);
            }
        }

        return patchedOctrees;
    }
}
