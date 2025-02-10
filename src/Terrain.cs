using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace TerrainPatcher
{
    /// <summary>The global registry of terrain patches.</summary>
    public static class TerrainRegistry
    {
        /// <summary>Applies a terrain patch file to the game's terrain. Deprecated</summary>
        /// <param name="patchName">The name of the patch file to apply.</param>
        /// <param name="patchFile">The patch file to apply.</param>
        /// <param name="forceOriginal">Force-overwrites batches in this patch, resetting them to
        /// their original states before applying patches.</param>
        [Obsolete(
            "This method is deprecated; instead, load terrain patches by distributing them as"
            + " individual files alongside your mod."
        )]
        public static void PatchTerrain(
            string patchName,
            Stream patchFile,
            bool forceOriginal = false
        )
        {
            if (patchFile is null)
            {
                throw new ArgumentNullException($"Argument '{nameof(patchFile)}' must not be null");
            }

            ApplyTerrainPatch(patchName, patchFile, forceOriginal);
        }

        internal static void ApplyTerrainPatch(
            string patchName,
            Stream patchFile,
            bool forceOriginal
        )
        {
            try
            {
                string message = $"Applying patch '{patchName}'";

                if (patchFile.CanSeek)
                {
                    long position = patchFile.Position;
                    byte[] hash = MD5.Create().ComputeHash(patchFile);
                    patchFile.Seek(position, SeekOrigin.Begin);

                    string hex = BitConverter.ToString(hash).Replace("-", "");
                    message += $" (MD5: {hex})";
                }

                Mod.LogInfo(message);

                ApplyPatchFile(patchName, patchFile, forceOriginal);
            }
            catch (InvalidDataException ex)
            {
                Mod.LogError($"Patch '{patchName}' is broken or contains errors: {ex.Message}");
                Mod.DisplayError($"Error in terrain patch '{patchName}'!");
            }
            catch (Exception ex)
            {
                Mod.LogError(
                    $"Unexpected error applying patch '{patchName}', please report this bug: {ex}"
                );

                Mod.DisplayError(
                    $"Unexpected error applying terrain patch '{patchName}', please report this"
                    + " bug!"
                );
            }
        }

        // Applies a terrain patch.
        private static void ApplyPatchFile(
            string patchName,
            Stream patchFile,
            bool forceOriginal
        )
        {
            var reader = new BinaryReader(patchFile);

            uint version;

            try { version = reader.ReadUInt32(); }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("Patch is not large enough");
            }

            if (version == Constants.SKIP_VERSION)
            {
                Mod.LogWarning(
                    $"Skipping application of patch '{patchName}' because of invalid version!"
                );
                return;
            }

            if (version != Constants.PATCH_VERSION)
            {
                throw new InvalidDataException(
                    $"Unknown patch version {version}. Allowed version is:"
                    + $" {Constants.PATCH_VERSION}"
                );
            }

            while (true)
            {
                try
                {
                    Int3? batchId = ReadBatchId(reader);
                    if (batchId is null) break;
                    Int3 id = batchId.Value;

                    // Lists all batches as they are patched.
                    Mod.LogInfo($"Patching batch [{id.x}, {id.y}, {id.z}] for patch '{patchName}'");

                    ApplyBatchPatch(patchName, reader, id, forceOriginal);
                }
                catch (EndOfStreamException ex)
                {
                    throw new InvalidDataException("Patch ends too early", ex);
                }
            }

            // Returns the batch ID of the next batch, or null if at EOF.
            static Int3? ReadBatchId(BinaryReader patch)
            {
                byte first;
                try { first = patch.ReadByte(); } catch (EndOfStreamException) { return null; }

                return new Int3(
                    first | (patch.ReadSByte() << 8),
                    patch.ReadInt16(),
                    patch.ReadInt16()
                );
            }
        }

        // All batches that have been patched.
        // The Int3 is the ID of the batch.
        internal static readonly Dictionary<Int3, PatchedBatch> patchedBatches =
            new Dictionary<Int3, PatchedBatch> { };

        // A batch that has been modified.
        internal struct PatchedBatch
        {
            public PatchedBatch(string fileName)
            {
                this.fileName = fileName;
                this.octreePatches = new List<string>?[Constants.OCTREES_PER_BATCH];
            }

            // The name of the new batch file.
            public string fileName;

            // A list of the patch names that have been applied for each octree.
            public List<string>?[] octreePatches;
        }

        // Loads a batch into patchedBatches if necessary, then applies the patch.
        private static void ApplyBatchPatch(
            string patchName,
            BinaryReader patch,
            Int3 batchId,
            bool forceOriginal
        )
        {
            lock (patchedBatches)
            {
                if (!patchedBatches.ContainsKey(batchId))
                {
                    RegisterNewBatch(batchId);
                }
                else if (forceOriginal)
                {
                    Mod.LogWarning(
                        $"Patch '{patchName}' forcefully resetting batch "
                        + $"[{batchId.x}, {batchId.y}, {batchId.z}]!"
                    );

                    RegisterNewBatch(batchId);
                }
            }

            PatchOctrees(patchName, batchId, patch);
        }

        // Creates a new tempfile for a batch and stores it in patchedBatches.
        // Copies the original batch to the tempfile if it exists.
        private static void RegisterNewBatch(Int3 batchId)
        {
            string newPath = Path.Combine(
                PatchesDir.Path,
                $"compiled-batch-{batchId.x}-{batchId.y}-{batchId.z}.optoctrees"
            );

            string GetPath(string origDir) => Path.Combine(
                SNUtils.InsideUnmanaged(origDir),
                "CompiledOctreesCache",
                $"compiled-batch-{batchId.x}-{batchId.y}-{batchId.z}.optoctrees"
            );

            // Search each directory.
            foreach (string dir in Constants.ORIG_BATCH_DIRS)
            {
                string origPath = GetPath(dir);

                if (File.Exists(origPath))
                {
                    using (FileStream file = File.OpenRead(origPath))
                    {
                        var reader = new BinaryReader(file);

                        if (reader.ReadUInt32() == Constants.BATCH_VERSION)
                        {
                            File.Copy(origPath, newPath, overwrite: true);

                            patchedBatches[batchId] = new PatchedBatch(newPath);
                            return;
                        }
                    }
                }
            }

            // Vanilla file was not found, creating empty batch.
            using (FileStream file = File.Create(newPath))
            {
                var writer = new BinaryWriter(file);

                writer.Write((uint)Constants.BATCH_VERSION);

                for (int i = 0; i < Constants.OCTREES_PER_BATCH; i++)
                {
                    // Single-node empty octree.
                    writer.Write((ushort)1);
                    writer.Write((uint)0);
                }

                patchedBatches[batchId] = new PatchedBatch(newPath);
            }
        }

        // Patches the contents of a batch, assuming it's already in patchedBatches.
        private static void PatchOctrees(string patchName, Int3 batchId, BinaryReader patch)
        {
            string fileName = patchedBatches[batchId].fileName;

            // Copy the contents of the original file into memory.
            byte[] origBytes = File.ReadAllBytes(fileName);
            var original = new BinaryReader(new MemoryStream(buffer: origBytes, writable: false));

            original.ReadUInt32(); // Discard version bytes.

            BitArray patchedOctrees;

            // Apply the patch to the target file.
            using (FileStream targetFile = File.Open(fileName, FileMode.Create))
            {
                var target = new BinaryWriter(targetFile);

                target.WriteUInt32(Constants.BATCH_VERSION); // Prepend version bytes.

                // Call the terrain patcher.
                try { patchedOctrees = TerrainPatching.Patch(target, original, patch); }
                catch (Exception)
                {
                    targetFile.Seek(0, SeekOrigin.Begin);
                    targetFile.Write(origBytes, 0, origBytes.Length);
                    throw;
                }
            }

            var octreePatches = patchedBatches[batchId].octreePatches;
            for (int i = 0; i < Constants.OCTREES_PER_BATCH; i++)
            {
                if (patchedOctrees[i])
                {
                    octreePatches[i] ??= new List<string>();
                    var patches = octreePatches[i]!;

                    if (patches.Count > 0)
                    {
                        var warning = $"Patch '{patchName}' is overriding ";
                        warning += patches.Count == 1 ? "patch " : "patches [";
                        warning += string.Join(",", patches.Select(patch => $"'{patch}'"));
                        if (patches.Count > 1) { warning += "]"; }
                        warning += $" in batch [{batchId.x}, {batchId.y}, {batchId.z}]";
                        warning += $" at octree #{i}!";

                        Mod.LogWarning(warning);
                    }

                    patches.Add(patchName);
                }
            }
        }
    }

    // The directory containing patched batch files.
    internal class PatchesDir
    {
        private PatchesDir()
        {
            foreach (var origDirName in Constants.ORIG_BATCH_DIRS)
            {
                var origDir = SNUtils.InsideUnmanaged(origDirName);

                if (Directory.Exists(origDir))
                {
                    this.path = System.IO.Path.Combine(origDir, "CompiledOctreesCache", "patches");
                    Directory.CreateDirectory(this.path);

                    foreach (string path in Directory.EnumerateFiles(this.path))
                    {
                        if (System.IO.Path.GetExtension(path) == ".optoctrees")
                        {
                            File.Delete(path);
                        }
                    }

                    break;
                }
            }
        }

        private string? path;

        private static PatchesDir? instance;
        public static string? Path
        {
            get
            {
                PatchesDir.instance ??= new PatchesDir();
                return PatchesDir.instance.path;
            }
        }
    }

    internal static class TerrainPatching
    {
        // Patches a target file, using an original file and a patch file. Returns an array
        // indicating which octrees were modified.
        public static BitArray Patch(BinaryWriter target, BinaryReader original, BinaryReader patch)
        {
            var patchedOctrees = new BitArray(Constants.OCTREES_PER_BATCH);
            int patchedOctreeCount = patch.ReadByte();

            if (patchedOctreeCount > Constants.OCTREES_PER_BATCH)
            {
                throw new InvalidDataException(
                    "Patch contains more octrees than the batch can contain"
                );
            }

            // An array of byte arrays. Each byte array is the binary node data for one octree.
            byte[][] octrees = new byte[Constants.OCTREES_PER_BATCH][];

            // Copy the original batch into the octrees array.
            for (int i = 0; i < Constants.OCTREES_PER_BATCH; i++)
            {
                try
                {
                    octrees[i] = original.ReadBytes(original.ReadUInt16() * 4);
                }
                catch (EndOfStreamException) { break; }
            }

            // Apply patches on top of the octrees array.
            for (int i = 0; i < patchedOctreeCount; i++)
            {
                try
                {
                    int octree = patch.ReadByte();
                    byte[] bytes = patch.ReadBytes(patch.ReadUInt16() * 4);

                    patchedOctrees[octree] = true;
                    octrees[octree] = bytes;
                }
                catch (EndOfStreamException ex)
                {
                    throw new InvalidDataException("Patch ends too early", ex);
                }
                catch (Exception ex)
                when (ex is IndexOutOfRangeException || ex is ArgumentOutOfRangeException)
                {
                    throw new InvalidDataException(
                        "Patch contains an octree outside the bounds of the batch it applies to",
                        ex
                    );
                }
            }

            // Write the octrees array to the target stream.
            for (int i = 0; i < octrees.Length; i++)
            {
                if (octrees[i] is object)
                {
                    target.Write((ushort)(octrees[i].Length / 4));
                    target.Write(octrees[i], 0, octrees[i].Length);
                }
                else
                {
                    // Single-node empty octree.
                    target.Write((ushort)1);
                    target.Write((uint)0);
                }
            }

            return patchedOctrees;
        }
    }
}
