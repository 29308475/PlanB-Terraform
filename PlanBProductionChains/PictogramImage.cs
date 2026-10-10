using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace PlanBProductionChains
{
    // Render the approved export from the shared SVG master. Mipmaps keep its
    // thin black outline smooth at the small HUD size, independently of font/UI scale.
    internal abstract class PictogramImage : Image
    {
        private static readonly Dictionary<string, Sprite> assets = new Dictionary<string, Sprite>();
        protected abstract string AssetName { get; }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!assets.TryGetValue(AssetName, out var asset))
            {
                using (var stream = typeof(Plugin).Assembly.GetManifestResourceStream("PlanBProductionChains.Icons." + AssetName + ".png"))
                {
                    if (stream == null) throw new InvalidOperationException("Piktogramm fehlt: " + AssetName);
                    using (var bytes = new MemoryStream())
                    {
                        stream.CopyTo(bytes);
                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                        texture.name = AssetName + "Pictogram";
                        texture.filterMode = FilterMode.Trilinear;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        // The game's Unity 6 image module targets netstandard 2.1;
                        // access its loader at runtime from this net48 BepInEx plugin.
                        var conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", true);
                        var loader = conversion.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
                        if (loader == null || !(bool)loader.Invoke(null, new object[] { texture, bytes.ToArray(), true }))
                        { Destroy(texture); throw new InvalidOperationException("Piktogramm konnte nicht geladen werden: " + AssetName); }
                        asset = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
                        assets.Add(AssetName, asset);
                    }
                }
            }
            sprite = asset; type = Type.Simple; preserveAspect = true;
            color = Color.white; raycastTarget = false;
        }

        internal static void ReleaseAssets()
        {
            foreach (var asset in assets.Values)
            {
                if (asset == null) continue;
                var texture = asset.texture;
                Destroy(asset); if (texture != null) Destroy(texture);
            }
            assets.Clear();
        }
    }
}
