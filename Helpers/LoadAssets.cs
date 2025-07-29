using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace Instrumentarria.Helpers
{
    /// <summary>
    /// Static class to hold all assets used in the mod.
    /// Add a public static Asset<Texture2D> field for each asset you want to load.
    /// Then use them like Ass.EditorIcon.Value in your code.
    /// They should all be initialized automatically.
    /// </summary>
    public static class Ass
    {
        // Add assets here
        // public static Asset<Texture2D> Minesweeper;

        static Ass()
        {
            foreach (var field in typeof(Ass).GetFields())
                if (field.FieldType == typeof(Asset<Texture2D>))
                    field.SetValue(null, ModContent.Request<Texture2D>($"Instrumentarria/Assets/{field.Name}"));
        }
        public static bool Initialized { get; set; }
    }
    /// <summary>
    /// System that automatically initializes assets
    /// </summary>
    public class LoadAssets : ModSystem
    {
        public override void Load() => _ = Ass.Initialized;
    }
}
