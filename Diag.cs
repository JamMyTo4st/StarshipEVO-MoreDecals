using System;
using UnityEngine;

namespace MoreDecals
{
    // build c: read a glyph layer back from the GPU and compare it with the source PNG (normal and mirrored),
    // to diagnose orientation and transparency in game.
    internal static class Diag
    {
        public static Color32[] GpuRead(Texture2DArray arr, int layer, int mip)
        {
            int sz = Math.Max(1, arr.width >> mip);
            bool srgb = arr.graphicsFormat.ToString().Contains("SRGB");
            var tmp = new Texture2D(sz, sz, arr.format, false, !srgb);
            var rt = RenderTexture.GetTemporary(sz, sz, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            var read = new Texture2D(sz, sz, TextureFormat.RGBA32, false, !srgb);
            var prev = RenderTexture.active;
            try
            {
                tmp.Apply(false, true); // GPU only, so the copy below is GPU->GPU
                Graphics.CopyTexture(arr, layer, mip, tmp, 0, 0);
                Graphics.Blit(tmp, rt);
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, sz, sz), 0, 0); read.Apply();
                return read.GetPixels32();
            }
            finally { RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.Destroy(tmp); UnityEngine.Object.Destroy(read); }
        }

        public static void Glyph(string tag, Texture2DArray arr, Glyph g)
        {
            try
            {
                var gpu = GpuRead(arr, g.Layer, 0); var src = g.Tex.GetPixels32(0); int n = 256;
                double dN = 0, dM = 0, aG = 0, aS = 0;
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    var p = gpu[y * n + x]; var s = src[y * n + x]; var m = src[y * n + (n - 1 - x)];
                    dN += Math.Abs(p.a - s.a); dM += Math.Abs(p.a - m.a); aG += p.a; aS += s.a;
                }
                int c = n * n;
                Plugin.Log.LogInfo($"[{tag}] GPU check id {g.Id} '{g.Name}' layer {g.Layer}: alpha mean gpu={aG / c:F1} png={aS / c:F1}; alpha diff vs png={dN / c:F1} vs mirrored png={dM / c:F1} ({(dN <= dM ? "orientation OK" : "MIRRORED in texture")})");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[{tag}] GPU check skipped: {e.Message}"); }
        }

        // Compare every mip of a vanilla layer, GPU of b vs GPU of a.
        public static string CompareLayers(Texture2DArray a, Texture2DArray b, int layer)
        {
            var sb = new System.Text.StringBuilder();
            for (int m = 0; m < a.mipmapCount; m++)
            {
                var pa = GpuRead(a, layer, m); var pb = GpuRead(b, layer, m); double d = 0;
                for (int i = 0; i < pa.Length; i++) d += Math.Abs(pa[i].r - pb[i].r) + Math.Abs(pa[i].g - pb[i].g) + Math.Abs(pa[i].b - pb[i].b) + Math.Abs(pa[i].a - pb[i].a);
                sb.Append($"m{m}={d / (4.0 * pa.Length):F2} ");
            }
            return sb.ToString();
        }
    }
}
