using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace MoreDecals
{
    // v1.2: make the game's VoxelDiffuse array bigger BEFORE anything binds it.
    // VoxelDiffuse is a prebuilt asset serialized on ResourcesLoader (field ta_voxelDiffuse). ResourcesLoader.Awake
    // calls Paths.Initialize() first, then VOXEL_DIFFUSE = ta_voxelDiffuse and Shader.SetGlobalTexture("MAIN_TEX", ...).
    // We postfix ONLY Common.SkyApplication.Paths.Initialize (never ResourcesLoader.Awake: patching that ran
    // TabSchematics..cctor too early in v0.1.0) and swap the field there, so the one and only object the game ever
    // publishes is the grown one. Custom layers are appended after the last vanilla layer (403+), never reused ids.
    internal static class Grow
    {
        public static bool Hit, Ok;
        public static int OrigDepth = -1, NewDepth = -1;
        public static Texture2DArray Grown, Orig;
        static Harmony h;

        public static void Install()
        {
            try
            {
                h = new Harmony(MyPluginInfo.PLUGIN_GUID + ".grow");
                var m = AccessTools.Method(typeof(Common.SkyApplication.Paths), "Initialize");
                if (m == null) { Plugin.Log.LogWarning("[Grow] Paths.Initialize not found; fallback to in-place free layers."); return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(Grow), nameof(Postfix)));
                Plugin.Log.LogInfo("[Grow] creation hook installed on Paths.Initialize (runs at the start of ResourcesLoader.Awake).");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[Grow] installing the creation hook FAILED, fallback will be used: " + e.Message); }
        }

        static void Postfix()
        {
            if (Hit) return;
            try
            {
                if (ResourcesLoader.VOXEL_DIFFUSE != null) { Plugin.V("[Grow] Paths.Initialize called after VOXEL_DIFFUSE was set; too late, ignoring."); Done(); return; }
                var loader = UnityEngine.Object.FindObjectOfType<ResourcesLoader>();
                if (loader == null || loader.ta_voxelDiffuse == null) { Plugin.V("[Grow] Paths.Initialize called without a ResourcesLoader (other caller); waiting."); return; }
                Hit = true; Done();
                Run(loader);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[Grow] FAILED (exception), fallback will be used: " + e); }
        }

        static void Done() { try { h?.UnpatchSelf(); } catch { } }

        static void Run(ResourcesLoader loader)
        {
            var src = loader.ta_voxelDiffuse;
            OrigDepth = src.depth;
            Plugin.Log.LogInfo($"[Grow] creation hook hit: VoxelDiffuse '{src.name}' {src.width}x{src.height} depth={src.depth} format={src.format} gfx={src.graphicsFormat} mips={src.mipmapCount} readable={src.isReadable}");
            var list = Glyphs.ById.Where(g => g != null).ToList();
            int extra = Math.Min(list.Count, Math.Max(0, Plugin.MaxExtraLayers.Value));
            string why = null;
            if (list.Count == 0) why = "no custom PNGs";
            else if (extra == 0) why = "MaxExtraLayers=0";
            else if (src.width != 256 || src.height != 256) why = "unexpected size";
            else if (!src.isReadable) why = "array not readable (CPU copy needed for Brick decal pixels)";
            else if (!Encoder.Supported(src.format)) why = $"format {src.format} not supported";
            else if (src.depth + extra > 2048) why = $"depth {src.depth + extra} > GPU max {2048}";
            if (why != null) { Plugin.Log.LogWarning("[Grow] not growing: " + why + "; fallback will be used."); return; }

            var flags = src.mipmapCount > 1 ? UnityEngine.Experimental.Rendering.TextureCreationFlags.MipChain : UnityEngine.Experimental.Rendering.TextureCreationFlags.None;
            Texture2DArray dst = null; var tmps = new List<Texture2D>();
            try
            {
                dst = new Texture2DArray(src.width, src.height, src.depth + extra, src.graphicsFormat, flags, src.mipmapCount)
                { name = src.name };
                CopyProps(src, dst);
                if (dst.format != src.format || dst.mipmapCount != src.mipmapCount || dst.graphicsFormat != src.graphicsFormat)
                { Plugin.Log.LogWarning($"[Grow] new array came out {dst.format}/{dst.graphicsFormat}/{dst.mipmapCount}; fallback will be used."); UnityEngine.Object.Destroy(dst); return; }
                for (int i = 0; i < extra; i++)
                {
                    var g = list[i]; var raw = new List<byte>();
                    for (int m = 0; m < src.mipmapCount; m++)
                    {
                        int sz = Math.Max(1, 256 >> m);
                        raw.AddRange(Encoder.Encode(m < g.Tex.mipmapCount ? g.Tex.GetPixels32(m) : new Color32[sz * sz], sz, src.format));
                    }
                    var tmp = new Texture2D(256, 256, src.graphicsFormat, src.mipmapCount, flags); tmps.Add(tmp);
                    if (raw.Count != tmp.GetRawTextureData<byte>().Length) throw new Exception("encoded size mismatch for " + g.Name);
                    tmp.LoadRawTextureData(raw.ToArray()); tmp.Apply(false, false);
                }
                // Both readable: CopyTexture copies GPU and CPU data (all mips). Apply re-uploads from CPU as the source of truth.
                // build c: 1) CPU data (both readable: CopyTexture also copies CPU) + Apply to upload PNG layers;
                // 2) THEN GPU-copy every vanilla layer/mip from the ORIGINAL's GPU texture over it, so the vanilla layers on
                // the GPU are bit-identical to what the game uploaded (build b's Apply re-upload caused blocky walls).
                for (int l = 0; l < src.depth; l++) for (int m = 0; m < src.mipmapCount; m++) Graphics.CopyTexture(src, l, m, dst, l, m);
                for (int i = 0; i < extra; i++) for (int m = 0; m < src.mipmapCount; m++) Graphics.CopyTexture(tmps[i], 0, m, dst, src.depth + i, m);
                dst.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                for (int l = 0; l < src.depth; l++) for (int m = 0; m < src.mipmapCount; m++) Graphics.CopyTexture(src, l, m, dst, l, m);
                for (int i = 0; i < extra; i++) for (int m = 0; m < src.mipmapCount; m++) Graphics.CopyTexture(tmps[i], 0, m, dst, src.depth + i, m);
                Plugin.Log.LogInfo($"[Grow] build c: CPU copy + Apply, then GPU CopyTexture of {src.depth} vanilla layers x {src.mipmapCount} mips from the original.");
                CopyProps(src, dst);
                Plugin.Log.LogInfo("[Grow] props after copy: " + Props(src, dst));
                // CPU sanity check: a vanilla layer must match the original.
                foreach (int l in new[] { 1, src.depth / 2, src.depth - 1 })
                {
                    var a = src.GetPixels32(l, 2); var b = dst.GetPixels32(l, 2);
                    for (int k = 0; k < a.Length; k += 97) if (!a[k].Equals(b[k])) throw new Exception($"CPU copy differs at layer {l}");
                }
                loader.ta_voxelDiffuse = dst; // ResourcesLoader.Awake now publishes this object everywhere
                Grown = dst; Orig = src; NewDepth = dst.depth; Ok = true;
                for (int i = 0; i < extra; i++) list[i].Layer = src.depth + i;
                Plugin.Log.LogInfo($"[Grow] OK: VoxelDiffuse depth {OrigDepth} -> {NewDepth} (+{extra}), swapped before VOXEL_DIFFUSE/MAIN_TEX were set.");
                for (int i = 0; i < extra; i++) Plugin.Log.LogInfo($"[Grow] layer {list[i].Layer} <- id {list[i].Id} '{list[i].Name}'");
                if (extra < list.Count) Plugin.Log.LogWarning($"[Grow] MaxExtraLayers={Plugin.MaxExtraLayers.Value}: {list.Count - extra} PNGs got no layer: {string.Join(", ", list.Skip(extra).Select(x => x.Name))}");
                dst = null;
            }
            catch (Exception e) { foreach (var g in list) g.Layer = -1; Plugin.Log.LogWarning("[Grow] FAILED, fallback will be used: " + e.Message); }
            finally { foreach (var t in tmps) UnityEngine.Object.Destroy(t); if (dst != null) UnityEngine.Object.Destroy(dst); }
        }

        static void CopyProps(Texture2DArray s, Texture2DArray d)
        {
            d.filterMode = s.filterMode; d.anisoLevel = s.anisoLevel; d.mipMapBias = s.mipMapBias;
            d.wrapModeU = s.wrapModeU; d.wrapModeV = s.wrapModeV; d.wrapModeW = s.wrapModeW;
        }

        static string P(string n, object a, object b) => $"{n} {a}->{b}{(Equals(a, b) ? "" : " (DIFF)")}; ";
        static string Props(Texture2DArray s, Texture2DArray d) =>
            P("filter", s.filterMode, d.filterMode) + P("aniso", s.anisoLevel, d.anisoLevel) + P("mipBias", s.mipMapBias, d.mipMapBias) +
            P("wrapU", s.wrapModeU, d.wrapModeU) + P("wrapV", s.wrapModeV, d.wrapModeV) + P("wrapW", s.wrapModeW, d.wrapModeW) +
            P("format", s.format, d.format) + P("gfx(sRGB/linear)", s.graphicsFormat, d.graphicsFormat) + P("mips", s.mipmapCount, d.mipmapCount) +
            P("size", s.width + "x" + s.height, d.width + "x" + d.height) + P("readable", s.isReadable, d.isReadable) + P("dimension", s.dimension, d.dimension) +
            $"depth {s.depth}->{d.depth}; quality: masterTextureLimit={QualitySettings.masterTextureLimit} anisotropicFiltering={QualitySettings.anisotropicFiltering}";

        // Called after load. Returns true when the grown array is live; logs diagnostics.
        public static bool Verify()
        {
            if (!Hit) { Plugin.Log.LogWarning("[Grow] creation hook was NOT hit before ResourcesLoader.Awake; fallback (in-place free layers) used."); return false; }
            if (!Ok) { Plugin.Log.LogWarning("[Grow] grow failed earlier; fallback (in-place free layers) used."); return false; }
            var g = Shader.GetGlobalTexture("MAIN_TEX");
            bool same = g == Grown && ResourcesLoader.VOXEL_DIFFUSE == Grown;
            int stale = 0;
            try
            {
                foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                    if (m != null && m.HasProperty("MAIN_TEX") && m.GetTexture("MAIN_TEX") is Texture2DArray t && t != Grown && t.name == Grown.name) { stale++; if (stale <= 5) Plugin.Log.LogWarning($"[Grow] material '{m.name}' holds the ORIGINAL VoxelDiffuse (custom layers {OrigDepth}+ will not show there)."); }
            }
            catch (Exception e) { Plugin.V("[Grow] material scan skipped: " + e.Message); }
            if (Orig != null && Grown != null)
            {
                Plugin.Log.LogInfo("[Grow] post-load props (original->grown): " + Props(Orig, Grown));
                CopyProps(Orig, Grown); // re-assert in case anything changed after creation
                foreach (int l in new[] { 1, OrigDepth / 2, OrigDepth - 1 })
                    try { Plugin.Log.LogInfo($"[Grow] GPU original vs grown, layer {l} mean diff per mip: {Diag.CompareLayers(Orig, Grown, l)}"); } catch (Exception e) { Plugin.Log.LogWarning("[Grow] GPU compare skipped: " + e.Message); }
                foreach (var gl in Glyphs.ById) if (gl != null && gl.Layer >= 0 && gl.Id <= 3) Diag.Glyph("Grow", Grown, gl);
                try { foreach (int mip in new[] { 0, 1, 3 }) if (mip < Orig.mipmapCount && !Inject.VerifyMip(Orig, Grown, OrigDepth, mip, out string w)) Plugin.Log.LogWarning("[Grow] GPU mip check: " + w); }
                catch (Exception e) { Plugin.Log.LogWarning("[Grow] GPU mip check skipped: " + e.Message); }
            }
            Plugin.Log.LogInfo($"[Grow] post-load check: MAIN_TEX is grown array={same} depth={(g as Texture2DArray)?.depth} materialsWithOriginal={stale}");
            if (!same) { foreach (var x in Glyphs.ById) if (x != null) x.Layer = -1; Plugin.Log.LogWarning("[Grow] MAIN_TEX is not the grown array; fallback used."); return false; }
            return true;
        }
    }
}
