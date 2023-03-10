using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Data;
using Dalamud.Plugin;
using Dalamud.Utility;
using ImGuiScene;

namespace CollectableCalculator
{
    internal sealed class IconCache : IDisposable
    {
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly DataManager _dataManager;
        private readonly Dictionary<uint, TextureWrap?> _textureWraps = new();

        public IconCache(DalamudPluginInterface pluginInterface, DataManager dataManager)
        {
            _pluginInterface = pluginInterface;
            _dataManager = dataManager;
        }

        public TextureWrap? GetIcon(uint iconId)
        {
            if (_textureWraps.TryGetValue(iconId, out var textureWrap))
                return textureWrap;

            var iconTex = _dataManager.GetIcon(iconId);
            if (iconTex != null)
            {
                var tex = _pluginInterface.UiBuilder.LoadImageRaw(iconTex.GetRgbaImageData(), iconTex.Header.Width,
                    iconTex.Header.Height, 4);
                if (tex.ImGuiHandle != IntPtr.Zero)
                {
                    _textureWraps[iconId] = tex;
                    return tex;
                }

                tex.Dispose();
            }

            _textureWraps[iconId] = null;
            return null;
        }

        public void Dispose()
        {
            foreach (var texture in _textureWraps.Values.Where(texture => texture != null))
                texture!.Dispose();

            _textureWraps.Clear();
        }
    }
}