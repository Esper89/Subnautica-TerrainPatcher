using System;
using System.Collections.Generic;
using System.IO;

namespace TerrainPatcher
{
    /// <summary>The global registry of terrain patches.</summary>
    public static class TerrainRegistry
    {
        /// <summary>Applies a terrain patch file to the game's terrain.</summary>
        /// <param name="patchName">The name of the patch file to apply.</param>
        /// <param name="patchFile">The patch file to apply.</param>
        /// <param name="forceOriginal">Force-overwrites batches in this patch, resetting them to
        /// their original states before applying patches.</param>
        public static void PatchTerrain(
            string patchName,
            Stream patchFile,
            bool forceOriginal = false
        )
        {
            try
            {
                Mod.LogInfo($"Applying patch '{patchName}'");
                ApplyPatchFile(patchFile, forceOriginal);
            }
            catch (Exception ex)
            {
                Mod.LogError($"Problem applying '{patchName}': {ex}");
            }
        }

        // Applies a terrain patch.
        internal static void ApplyPatchFile(Stream patchFile, bool forceOriginal = false)
        {
            if (patchFile is null)
            {
                throw new ArgumentNullException(
                    $"Argument '{nameof(patchFile)}' must not be null."
                );
            }

            var reader = new BinaryReader(patchFile);

            uint version;

            try
            {
                version = reader.ReadUInt32();
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("Provided patch file is not large enough.");
            }

            if (version != Constants.PATCH_VERSION)
            {
                throw new InvalidDataException(
                    "Provided patch file does not have the correct version. The correct version " +
                    $"is {Constants.PATCH_VERSION}, this patch file has version {version}."
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
                    Mod.LogInfo($"- Patching batch ({id.x}, {id.y}, {id.z})");

                    ApplyBatchPatch(reader, id, forceOriginal);
                }
                catch (EndOfStreamException ex)
                {
                    throw new InvalidDataException("File ended too early.", ex);
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
        // The Int3 is the ID of the batch and the string is the file name.
        internal static readonly Dictionary<Int3, string> patchedBatches =
            new Dictionary<Int3, string> { };

        // Loads a batch into patchedBatches if necessary, then applies the patch.
        private static void ApplyBatchPatch(BinaryReader patch, Int3 batchId, bool forceOriginal)
        {
            lock (patchedBatches)
            {
                if (!patchedBatches.ContainsKey(batchId) || forceOriginal)
                {
                    RegisterNewBatch(batchId);
                }
            }

            PatchOctrees(batchId, patch);
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
            string[] dirs = Constants.ORIG_BATCH_DIRS;
            for (int i = 0; i < dirs.Length; i++)
            {
                string origPath = GetPath(dirs[i]);

                if (File.Exists(origPath))
                {
                    using (FileStream file = File.OpenRead(origPath))
                    {
                        var reader = new BinaryReader(file);

                        if (reader.ReadUInt32() == Constants.BATCH_VERSION)
                        {
                            File.Copy(origPath, newPath, overwrite: true);

                            patchedBatches[batchId] = newPath;
                            return;
                        }
                    }
                }
            }

            // Vanilla file was not found, creating empty batch.
            using (FileStream file = File.Open(newPath, FileMode.Create))
            {
                var writer = new BinaryWriter(file);

                writer.Write((uint)Constants.BATCH_VERSION);

                for (int i = 0; i < Constants.OCTREES_PER_BATCH; i++)
                {
                    // Single-node empty octree.
                    writer.Write((ushort)1);
                    writer.Write((uint)0);
                }

                patchedBatches[batchId] = newPath;
            }
        }

        // Patches the contents of a batch, assuming it's already in patchedBatches.
        private static void PatchOctrees(Int3 batchId, BinaryReader patch)
        {
            // Copy the contents of the original file into memory.
            byte[] bytes = File.ReadAllBytes(patchedBatches[batchId]);
            var original = new BinaryReader(new MemoryStream(buffer: bytes, writable: false));

            original.ReadUInt32(); // Discard version bytes.

            // Apply the patch to the target file.
            using (FileStream file = File.Open(patchedBatches[batchId], FileMode.Create))
            {
                var target = new BinaryWriter(file);

                target.WriteUInt32(Constants.BATCH_VERSION); // Prepend version bytes.

                // Call the terrain patcher.
                TerrainPatching.Patch(target, original, patch);
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
                        if (System.IO.Path.GetExtension(path) == ".optoctrees" &&
                            !TerrainRegistry.patchedBatches.ContainsValue(path))
                        {
                            File.Delete(path);
                        }
                    }
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
        // Patches a target file, using an original file and a patch file.
        public static void Patch(BinaryWriter target, BinaryReader original, BinaryReader patch)
        {
            int patchedOctreeCount = patch.ReadByte();

            if (patchedOctreeCount > Constants.OCTREES_PER_BATCH)
            {
                Mod.LogWarning("Patch contains more octrees than the batch can contain.");
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
                catch (EndOfStreamException)
                {
                    break;
                }
            }

            // Apply patches on top of the octrees array.
            for (int i = 0; i < patchedOctreeCount; i++)
            {
                try
                {
                    octrees[patch.ReadByte()] = patch.ReadBytes(patch.ReadUInt16() * 4);
                }
                catch (EndOfStreamException)
                {
                    Mod.LogWarning("Patch ended too early.");
                    break;
                }
                catch (IndexOutOfRangeException)
                {
                    Mod.LogWarning(
                        "Patch contains an octree outside the bounds of the batch it applies to."
                    );
                    continue;
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
        }
    }
}
