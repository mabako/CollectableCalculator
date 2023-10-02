using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Internal;
using Dalamud.Plugin.Services;

namespace CollectableCalculator;

internal sealed class IconCache : IDisposable
{
    private readonly ITextureProvider _textureProvider;
    private readonly Dictionary<uint, IDalamudTextureWrap?> _textureWraps = new();

    public IconCache(ITextureProvider textureProvider)
    {
        _textureProvider = textureProvider;
    }

    public IDalamudTextureWrap? GetIcon(uint iconId)
    {
        if (_textureWraps.TryGetValue(iconId, out var textureWrap))
            return textureWrap;

        var iconTex = _textureProvider.GetIcon(iconId);
        if (iconTex != null)
        {
            if (iconTex.ImGuiHandle != nint.Zero) {
                _textureWraps[iconId] = iconTex;
                return iconTex;
            }

            iconTex.Dispose();
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
