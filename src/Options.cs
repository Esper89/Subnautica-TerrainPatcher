using Nautilus.Handlers;
using Nautilus.Options;

namespace TerrainPatcher
{
    internal static class Options
    {
        internal static void Register()
            => OptionsPanelHandler.RegisterModOptions(new NautilusOptions());

        private class NautilusOptions : ModOptions
        {
            internal NautilusOptions() : base("Terrain Patcher")
            {
                var includePatches = ModToggleOption.Create(
                    "include-patches", "Include Patches", Mod.Settings.IncludePatches
                );

                includePatches.OnChanged += (object sender, ToggleChangedEventArgs e)
                    => Mod.Settings.IncludePatches = e.Value;

                base.AddItem(includePatches);
            }
        }
    }
}
