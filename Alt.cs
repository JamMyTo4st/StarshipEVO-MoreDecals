using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VoxelEngine;

namespace MoreDecals
{
    // Variant B: keep the SAME MAIN_TEX object (replacing it is proven to break rendering) and GPU-copy the glyphs
    // into layers of it that no material/brick references. No Apply(), no SetGlobalTexture: the binding the
    // shaders and caches hold never changes. Modded decals then use textureId = that layer, exactly like v0.1.x
    // but without a new array. Changes are lost on restart (re-done on every launch).
    internal static class Alt
    {
        public const string Tag = "b";
        static bool ready;

        static void Use(HashSet<int> s, int v) { if (v >= 0) s.Add(v); }

        public static void Run()
        {
            try
            {
                var arr = ResourcesLoader.VOXEL_DIFFUSE;
                Plugin.Log.LogInfo($"[Inject-B] MAIN_TEX {arr.name} {arr.width}x{arr.height} layers={arr.depth} format={arr.format} gfx={arr.graphicsFormat} mips={arr.mipmapCount} readable={arr.isReadable} masterTextureLimit={QualitySettings.masterTextureLimit} copySupport={SystemInfo.copyTextureSupport}");
                if (Shader.GetGlobalTexture("MAIN_TEX") != arr) { Abort("MAIN_TEX global is not VOXEL_DIFFUSE"); return; }
                if (!Encoder.Supported(arr.format)) { Abort($"format {arr.format} not supported"); return; }
                if ((SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.DifferentTypes) == 0) { Abort("GPU cannot copy Texture2D -> Texture2DArray"); return; }

                // Every id any material could hand to a shader. Conservative: ids of all kinds (texture, border,
                // normal, specular) count as "used" even though normal/specular index other arrays.
                var used = new HashSet<int> { 0 };
                int mats = 0;
                var all = VoxelMaterialResources.materialParamDatas ?? new VoxelMaterial[0];
                foreach (var m in all) { mats++; Use(used, m.TextureID); Use(used, m.BorderId); Use(used, m.normalID); Use(used, m.specularID); Use(used, m.borderNormalID); Use(used, m.borderSpecularID); }
                int bricks = 0;
                foreach (var b in Brick.BRICK_INDEX)
                {
                    if (b?.VoxelMaterials == null) continue; bricks++;
                    foreach (var m in b.VoxelMaterials) { Use(used, m.TextureID); Use(used, m.BorderId); Use(used, m.normalID); Use(used, m.specularID); Use(used, m.borderNormalID); Use(used, m.borderSpecularID); }
                }
                var free = Enumerable.Range(1, arr.depth - 1).Where(l => !used.Contains(l)).ToList();
                // Prefer layers whose pixels are blank/uniform (truly spare slots), then the highest ids.
                Func<int, bool> blank = l =>
                {
                    if (!arr.isReadable) return false;
                    try { var px = arr.GetPixels32(l, Math.Min(4, arr.mipmapCount - 1)); var p0 = px[0]; return px.All(p => Math.Abs(p.r - p0.r) + Math.Abs(p.g - p0.g) + Math.Abs(p.b - p0.b) + Math.Abs(p.a - p0.a) < 12); }
                    catch { return false; }
                };
                var blanks = free.Where(blank).ToList();
                var order = blanks.Concat(free.Except(blanks).OrderByDescending(x => x)).ToList();
                Plugin.Log.LogInfo($"[Inject-B] scanned {mats} materials + {bricks} bricks: {used.Count} referenced ids, {free.Count} unreferenced layers ({blanks.Count} blank): [{string.Join(",", free)}]");

                var list = Glyphs.ById.Where(g => g != null).ToList();
                int count = Math.Min(list.Count, order.Count);
                if (count == 0) { Abort("no free MAIN_TEX layers found"); return; }
                if (count < list.Count) Plugin.Log.LogWarning($"[Inject-B] only {count} free layers for {list.Count} decals; the first {count} (A, B, C...) get images, the rest show the placeholder.");

                var flags = arr.mipmapCount > 1 ? UnityEngine.Experimental.Rendering.TextureCreationFlags.MipChain : UnityEngine.Experimental.Rendering.TextureCreationFlags.None;
                int done = 0;
                for (int i = 0; i < count; i++)
                {
                    var g = list[i];
                    var raw = new List<byte>();
                    for (int m = 0; m < arr.mipmapCount; m++)
                    {
                        int sz = Math.Max(1, 256 >> m);
                        Color32[] px = m < g.Tex.mipmapCount ? g.Tex.GetPixels32(m) : new Color32[sz * sz];
                        raw.AddRange(Encoder.Encode(px, sz, arr.format));
                    }
                    var tmp = new Texture2D(256, 256, arr.graphicsFormat, arr.mipmapCount, flags);
                    try
                    {
                        if (tmp.format != arr.format || tmp.mipmapCount != arr.mipmapCount) { Abort($"temp texture {tmp.format}/{tmp.mipmapCount}"); return; }
                        if (raw.Count != tmp.GetRawTextureData<byte>().Length) { Abort("encoded size mismatch"); return; }
                        tmp.LoadRawTextureData(raw.ToArray()); tmp.Apply(false, true); // GPU only, non-readable -> CopyTexture is GPU-only
                        Graphics.CopyTexture(tmp, 0, arr, order[i]);
                        g.Layer = order[i]; done++;
                        if (i < 6) Plugin.V($"[Inject-B] glyph {g.Id} '{g.Name}' -> MAIN_TEX layer {order[i]}");
                    }
                    finally { UnityEngine.Object.Destroy(tmp); }
                }
                var loader = UnityEngine.Object.FindObjectOfType<ResourcesLoader>();
                ready = true;
                Plugin.Log.LogInfo($"[Inject-B] OK: copied {done} glyphs in place into MAIN_TEX layers [{string.Join(",", list.Take(done).Select(x => x.Layer))}]; MAIN_TEX object unchanged (still {Shader.GetGlobalTexture("MAIN_TEX")?.name}).");
            }
            catch (Exception e) { Abort("exception: " + e); }
        }

        static void Abort(string why)
        {
            ready = false;
            foreach (var g in Glyphs.ById) if (g != null) g.Layer = -1;
            Plugin.Log.LogWarning("[Inject-B] ABORTED, world textures left vanilla, modded decals show the Faction placeholder. Reason: " + why);
        }

        static int applied;
        public static bool Apply(ref DecalRendererResources.DecalData d, Glyph g, BrickInstanceData bd)
        {
            if (!ready || g.Layer < 0) return false;
            int old = d.textureId; d.textureId = g.Layer;
            if (applied++ < 12) Plugin.Log.LogInfo($"[Render-B] glyph {g.Id} '{g.Name}' brick {bd.brickId} transform {d.transformId}: textureId {old}->{d.textureId} normalId {d.normalId}");
            return true;
        }
    }
}
