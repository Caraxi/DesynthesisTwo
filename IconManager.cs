using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Logging;
using Dalamud.Utility;
using ImGuiScene;

namespace DesynthesisTwo; 

public class IconManager : IDisposable {
    private bool disposed;
    private readonly Dictionary<uint, TextureWrap?> iconTextures = new();

    public void Dispose() {
        disposed = true;
        var c = 0;
        PluginLog.Log("Disposing icon textures");
        foreach (var texture in iconTextures.Values.Where(texture => texture != null)) {
            c++;
            texture?.Dispose();
        }

        PluginLog.Log($"Disposed {c} icon textures.");
        iconTextures.Clear();
    }
        
    private void LoadIconTexture(uint iconId) {
        Task.Run(() => {
            try {
                var iconTex = Plugin.Data.GetIcon(iconId);
                if (iconTex == null) return;
                
                var tex = Plugin.PluginInterface.UiBuilder.LoadImageRaw(iconTex.GetRgbaImageData(), iconTex.Header.Width, iconTex.Header.Height, 4);

                if (tex.ImGuiHandle != IntPtr.Zero) {
                    this.iconTextures[iconId] = tex;
                } else {
                    tex.Dispose();
                }
            } catch (Exception ex) {
                PluginLog.LogError($"Failed loading texture for icon {iconId} - {ex.Message}");
            }
        });
    }
    
    public TextureWrap? GetIconTexture(uint iconId) {
        if (this.disposed) return null;
        if (this.iconTextures.ContainsKey(iconId)) return this.iconTextures[iconId];
        this.iconTextures.Add(iconId, null);
        LoadIconTexture(iconId);
        return this.iconTextures[iconId];
    }
}