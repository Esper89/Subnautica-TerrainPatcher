using BepInEx;
using TerrainPatcher;

namespace ExampleMod
{
    [BepInPlugin("YourName.ExampleMod", "Example Mod", "0.0.0")]
    [BepInDependency("Esper89.TerrainPatcher")]
    internal class Mod : BaseUnityPlugin
    {
        private void Awake()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var patch = asm.GetManifestResourceStream("ExampleMod.example.optoctreepatch");
            TerrainRegistry.PatchTerrain("example", patch);
        }
    }
}
