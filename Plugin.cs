using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Unity.Collections;
using UnityEngine;
using VoxelEngine;

namespace MoreDecals
{
    // How it works (no new brick ids):
    // A modded decal is saved as one of the 4 vanilla "Faction custom" decal bricks (ids 309-312).
    // The chosen glyph is stored in the top 4 bits of BrickInstanceData.scale. Vanilla only uses bits 0-11
    // (BrickInstanceData.Scale / PackScale), but the full ushort is saved (MessagePack key 2) and synced (Mirror).
    // 4 bricks x 15 values = 60 variants: 1-48 built-in, 49-60 custom PNG slots.
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled, Verbose, InjectTextures;
        internal static ConfigEntry<KeyCode> PickerKey;
        internal static string PluginDir;

        private void Awake()
        {
            Log = Logger;
            PluginDir = Path.GetDirectoryName(Info.Location);
            Enabled = Config.Bind("Decals", "Enabled", true, "Load the extra decals, render them and allow placing them. Off: modded decals show the vanilla Faction placeholder.");
            PickerKey = Config.Bind("Decals", "PickerKey", KeyCode.F8, "Key that opens/closes the More Decals picker window.");
            InjectTextures = Config.Bind("Decals", "InjectTextures", true, "Add the decal images to the game's block texture array (after a strict self-check). Off: nothing texture related is touched and modded decals show the vanilla Faction placeholder.");
            Verbose = Config.Bind("Debug", "VerboseLogging", true, "Log loading, placement and rendering details to BepInEx/LogOutput.log.");
            Glyphs.LoadAll();
            if (!Enabled.Value) { Log.LogInfo("MoreDecals disabled in config."); return; }
            // v0.1.1: nothing is patched here. Patching ResourcesLoader.Awake in v0.1.0 made Mono run the static
            // constructor of TabSchematics when the patched Awake was compiled, i.e. BEFORE
            // GameResourcesManager.AllocateResources(). TabSchematics..cctor then threw a NullReferenceException
            // (ItemInstanceResources not allocated yet), every schematic/help type stayed broken, and saves loaded
            // into an empty "void". All patches are now applied only after the game finished loading its codex.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureRunner();
            Log.LogInfo($"MoreDecals {MyPluginInfo.PLUGIN_VERSION} loaded, waiting for game resources. Built-in={Glyphs.BuiltInCount} custom={Glyphs.CustomCount} PickerKey={PickerKey.Value} InjectTextures={InjectTextures.Value}");
        }

        static bool started;
        internal static bool Started => started;
        static readonly FieldInfo codexLoaded = AccessTools.Field(typeof(ResourcesLoader), "codexLoaded");

        static void StartWhenLoaded()
        {
            if (started) return;
            if (codexLoaded == null) { started = true; Log.LogError("[Start] ResourcesLoader.codexLoaded not found; MoreDecals stays inactive."); return; }
            bool codex = (bool)codexLoaded.GetValue(null), tex = ResourcesLoader.VOXEL_DIFFUSE != null;
            if (!codex || !tex)
            {
                if (Time.realtimeSinceStartup >= nextWaitLog) { nextWaitLog = Time.realtimeSinceStartup + 10f; Log.LogInfo($"[Start] waiting: codexLoaded={codex} VOXEL_DIFFUSE={(tex ? "set" : "null")}"); }
                return;
            }
            started = true;
            if (TabDecals.DECAL_FACTION_CUSTOM_1 == null || TabDecals.DECAL_FACTION_CUSTOM_1.ItemID != Glyphs.Bank0)
            { Log.LogError($"[Start] Faction custom decal 1 is not brick {Glyphs.Bank0}; MoreDecals stays inactive."); return; }
            var h = new Harmony(MyPluginInfo.PLUGIN_GUID);
            foreach (var t in new[] { typeof(Patch_SetDecal), typeof(Patch_BuildLeftClickUp), typeof(Patch_Symmetry), typeof(Patch_BuildOnSelection) })
            {
                try { h.CreateClassProcessor(t).Patch(); Log.LogInfo($"[Patch] {t.Name} applied."); }
                catch (Exception e) { Log.LogError($"[Patch] {t.Name} FAILED: {e.Message}"); }
            }
            if (!Patch_BuildLeftClickUp.Applied)
            {
                // v0.1.0 logged "IL Compile Error" here. Fallback: encode on the build action sent to the network.
                try { h.CreateClassProcessor(typeof(Patch_LocalVoxelAction)).Patch(); Log.LogInfo("[Patch] Patch_LocalVoxelAction applied (fallback)."); }
                catch (Exception e) { Log.LogError("[Patch] Patch_LocalVoxelAction FAILED, placing modded decals is unavailable: " + e.Message); }
            }
            int n = 0; foreach (var _ in h.GetPatchedMethods()) n++;
            Log.LogInfo($"[Start] game resources ready, patched {n} methods.");
            if (InjectTextures.Value) Inject.Run();
            else Log.LogInfo("[Inject] skipped (InjectTextures=false); modded decals show the vanilla Faction placeholder.");
        }

        internal static void V(string s) { if (Verbose.Value) Log.LogInfo(s); }

        // v0.1.2: the BepInEx manager GameObject does not get Update() in this game (v0.1.1 never got past
        // "waiting"), so polling runs on our own hidden, persistent GameObject, plus a sceneLoaded callback.
        static float nextWaitLog, nextErrLog;
        internal static void Tick()
        {
            if (!Enabled.Value || started) return;
            try { StartWhenLoaded(); }
            catch (Exception e) { if (Time.realtimeSinceStartup >= nextErrLog) { nextErrLog = Time.realtimeSinceStartup + 10f; Log.LogError("[Start] poller exception (will retry): " + e); } }
        }

        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene sc, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            V($"[Start] scene loaded: {sc.name}");
            EnsureRunner();
            Tick();
        }

        static GameObject runnerGo;
        internal static void EnsureRunner()
        {
            if (runnerGo != null) return;
            runnerGo = new GameObject("MoreDecals_Runner") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(runnerGo);
            runnerGo.AddComponent<Runner>();
            V("[Start] runner object created.");
        }
    }

    internal sealed class Runner : MonoBehaviour
    {
        bool loggedFirst;
        void Update()
        {
            if (!loggedFirst) { loggedFirst = true; Plugin.V("[Start] runner Update is running."); }
            Plugin.Tick();
            if (!Plugin.Enabled.Value) return;
            try
            {
                if (Input.GetKeyDown(Plugin.PickerKey.Value))
                {
                    if (!Plugin.Started) Plugin.Log.LogWarning("[Picker] F8 pressed but MoreDecals is not ready yet (see the [Start] waiting lines).");
                    else { Picker.Open = !Picker.Open; Plugin.Log.LogInfo(Picker.Open ? "[Picker] opened" : "[Picker] closed"); }
                }
            }
            catch (Exception e) { Plugin.Log.LogError("[Picker] key check failed: " + e.Message); }
        }
        void OnGUI() { if (Plugin.Enabled.Value && Plugin.Started) Picker.Draw(); }
        void OnDestroy() { Plugin.Log.LogWarning("[Start] runner object destroyed; it will be recreated on the next scene load."); }
    }

    internal sealed class Glyph
    {
        public int Id;            // 1..60, stored in the save
        public string Name;
        public Texture2D Tex;     // RGBA32, mip chain, readable
        public int Layer = -1;    // layer in MAIN_TEX once injected
    }

    internal static class Glyphs
    {
        public const int MaxId = 60, BuiltInMax = 48, CustomFirst = 49, CustomSlots = 12;
        static readonly string[] BuiltInOrder =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ".Select(c => "letter_" + c)
            .Concat("0123456789".Select(c => "digit_" + c))
            .Concat(new[] { "sym_plus", "sym_minus", "sym_star", "sym_chevron_double", "sym_crosshair", "sym_gear", "sym_wrench", "sym_anchor", "sym_skull", "sym_info", "sym_question", "sym_no_entry" })
            .ToArray();
        public static readonly Glyph[] ById = new Glyph[MaxId + 1];
        public static int BuiltInCount, CustomCount;
        public static int NormalId = int.MinValue; // decided at injection
        static readonly HashSet<int> warned = new HashSet<int>();

        public const int Bank0 = 309; // TabDecals.DECAL_FACTION_CUSTOM_1, verified at start (never touch TabDecals early)
        public static ushort BankBrick(int bank) => (ushort)(Bank0 + bank);
        public static bool IsBankBrick(int brickId) => brickId >= Bank0 && brickId < Bank0 + 4;
        public static int Encode(int id, out ushort brickId) { brickId = BankBrick((id - 1) / 15); return (id - 1) % 15 + 1; }
        public static int Decode(BrickInstanceData bd)
        {
            int local = bd.scale >> 12;
            if (local == 0 || !IsBankBrick(bd.brickId)) return 0;
            return (bd.brickId - Bank0) * 15 + local;
        }
        public static void WarnOnce(int id, string msg) { if (warned.Add(id)) Plugin.Log.LogWarning(msg); }

        static Texture2D Load(byte[] png, string name)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "MoreDecals_" + name };
            if (!t.LoadImage(png, false)) return null;
            if (t.width != 256 || t.height != 256)
            {
                var r = new Texture2D(256, 256, TextureFormat.RGBA32, true) { name = t.name };
                var px = new Color32[256 * 256];
                for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
                        px[y * 256 + x] = t.GetPixelBilinear((x + 0.5f) / 256f, (y + 0.5f) / 256f);
                r.SetPixels32(px); UnityEngine.Object.Destroy(t); t = r;
            }
            t.Apply(true, false);
            return t;
        }

        public static void LoadAll()
        {
            var asm = Assembly.GetExecutingAssembly();
            for (int i = 0; i < BuiltInOrder.Length; i++)
            {
                using (var s = asm.GetManifestResourceStream("MoreDecals.Glyphs." + BuiltInOrder[i] + ".png"))
                {
                    if (s == null) { Plugin.Log.LogError("Missing embedded glyph " + BuiltInOrder[i]); continue; }
                    var b = new byte[s.Length]; s.Read(b, 0, b.Length);
                    var t = Load(b, BuiltInOrder[i]);
                    if (t == null) { Plugin.Log.LogError("Bad embedded glyph " + BuiltInOrder[i]); continue; }
                    ById[i + 1] = new Glyph { Id = i + 1, Name = BuiltInOrder[i].Substring(BuiltInOrder[i].IndexOf('_') + 1).Replace('_', ' '), Tex = t };
                    BuiltInCount++;
                }
            }
            LoadCustom();
        }

        // Custom PNGs: slot = explicit "NN_" prefix (01-12) or a stable FNV-1a hash of the lower-case file name.
        // The id saved in the brick is 48 + slot, so the same file name gives the same id on every machine.
        static void LoadCustom()
        {
            string dir = Path.Combine(Plugin.PluginDir ?? Paths.PluginPath, "Custom");
            try { Directory.CreateDirectory(dir); } catch { }
            if (!Directory.Exists(dir)) return;
            var files = Directory.GetFiles(dir, "*.png").OrderBy(f => Path.GetFileName(f).ToLowerInvariant(), StringComparer.Ordinal).ToList();
            var explicitFirst = files.Where(f => ExplicitSlot(Path.GetFileName(f)) > 0).Concat(files.Where(f => ExplicitSlot(Path.GetFileName(f)) == 0));
            foreach (var f in explicitFirst)
            {
                string fn = Path.GetFileName(f);
                int slot = ExplicitSlot(fn);
                if (slot == 0) slot = (int)(Fnv(fn.ToLowerInvariant()) % CustomSlots) + 1;
                int id = BuiltInMax + slot;
                if (ById[id] != null) { Plugin.Log.LogWarning($"[Custom] REJECTED {fn}: slot {slot:00} already used by {ById[id].Name}. Rename it with a free 'NN_' prefix (01-12)."); continue; }
                Texture2D t = null;
                try { t = Load(File.ReadAllBytes(f), fn); } catch (Exception e) { Plugin.Log.LogWarning($"[Custom] REJECTED {fn}: {e.Message}"); continue; }
                if (t == null) { Plugin.Log.LogWarning($"[Custom] REJECTED {fn}: not a readable PNG."); continue; }
                ById[id] = new Glyph { Id = id, Name = Path.GetFileNameWithoutExtension(fn), Tex = t };
                CustomCount++;
                Plugin.Log.LogInfo($"[Custom] loaded {fn} -> slot {slot:00} (id {id}).");
            }
        }

        static int ExplicitSlot(string fn)
        {
            if (fn.Length > 3 && char.IsDigit(fn[0]) && char.IsDigit(fn[1]) && fn[2] == '_')
            { int n = (fn[0] - '0') * 10 + (fn[1] - '0'); if (n >= 1 && n <= CustomSlots) return n; }
            return 0;
        }

        static uint Fnv(string s) { uint h = 2166136261; foreach (char c in s) { h ^= c; h *= 16777619; } return h; }
    }

    // ---------- Texture injection: append the glyphs as extra layers of the voxel diffuse array (MAIN_TEX) ----------
    // Texture injection, run once after the game loaded. MAIN_TEX is bound once by ResourcesLoader.Awake
    // (Shader.SetGlobalTexture) and read by every block/decal shader; no material holds it directly
    // (ta_voxelDiffuse/VOXEL_DIFFUSE are the only other references and stay the vanilla array for CPU readers).
    // A bad copy would blank every block, so every property is checked first and any doubt aborts.
    internal static class Inject
    {
        static string Check(Texture2DArray src, Texture2DArray dst)
        {
            if (dst.width != src.width || dst.height != src.height) return "size differs";
            if (dst.format != src.format) return $"format {dst.format} != {src.format}";
            if (dst.graphicsFormat != src.graphicsFormat) return $"graphicsFormat {dst.graphicsFormat} != {src.graphicsFormat} (sRGB/linear)";
            if (dst.mipmapCount != src.mipmapCount) return $"mips {dst.mipmapCount} != {src.mipmapCount}";
            if (dst.filterMode != src.filterMode || dst.anisoLevel != src.anisoLevel || dst.wrapModeU != src.wrapModeU || dst.wrapModeV != src.wrapModeV || dst.wrapModeW != src.wrapModeW) return "sampler state differs";
            return null;
        }

        internal static void Run()
        {
            Texture2DArray dst = null;
            try
            {
                var src = ResourcesLoader.VOXEL_DIFFUSE;
                var loader = UnityEngine.Object.FindObjectOfType<ResourcesLoader>();
                Plugin.Log.LogInfo($"[Inject] MAIN_TEX {src.width}x{src.height} layers={src.depth} format={src.format} gfx={src.graphicsFormat} mips={src.mipmapCount} readable={src.isReadable} copySupport={SystemInfo.copyTextureSupport}");
                string why = null;
                if (Shader.GetGlobalTexture("MAIN_TEX") != src) why = "MAIN_TEX global is not VOXEL_DIFFUSE (another mod or game change)";
                else if (src.width != 256 || src.height != 256) why = "unexpected MAIN_TEX size";
                else if (!Encoder.Supported(src.format)) why = $"texture format {src.format} not supported by the encoder";
                else if ((SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.DifferentTypes) == 0) why = "GPU cannot copy Texture2D -> Texture2DArray";
                var list = Glyphs.ById.Where(g => g != null).ToList();
                int baseLayer = src.depth;
                if (why == null && baseLayer + list.Count > 2048) why = "too many layers";
                if (why == null && list.Count == 0) why = "no decals loaded";
                if (why != null) { Abort(why); return; }

                var flags = src.mipmapCount > 1 ? UnityEngine.Experimental.Rendering.TextureCreationFlags.MipChain : UnityEngine.Experimental.Rendering.TextureCreationFlags.None;
                dst = new Texture2DArray(src.width, src.height, baseLayer + list.Count, src.graphicsFormat, flags, src.mipmapCount)
                {
                    name = src.name + "_MoreDecals", filterMode = src.filterMode, anisoLevel = src.anisoLevel, mipMapBias = src.mipMapBias,
                    wrapModeU = src.wrapModeU, wrapModeV = src.wrapModeV, wrapModeW = src.wrapModeW
                };
                if ((why = Check(src, dst)) != null) { Abort(why); return; }

                // Build every glyph first (CPU), so nothing is half done if one fails.
                var tmps = new List<Texture2D>();
                int bytesPerLayer = -1;
                foreach (var g in list)
                {
                    var raw = new List<byte>();
                    for (int m = 0; m < src.mipmapCount; m++)
                    {
                        int sz = Math.Max(1, 256 >> m);
                        Color32[] px = m < g.Tex.mipmapCount ? g.Tex.GetPixels32(m) : new Color32[sz * sz];
                        raw.AddRange(Encoder.Encode(px, sz, src.format));
                    }
                    var tmp = new Texture2D(256, 256, src.graphicsFormat, src.mipmapCount, flags);
                    if (tmp.format != src.format || tmp.mipmapCount != src.mipmapCount) { tmps.Add(tmp); Cleanup(tmps); Abort($"glyph texture came out {tmp.format}/{tmp.mipmapCount} mips"); return; }
                    if (bytesPerLayer < 0) bytesPerLayer = tmp.GetRawTextureData<byte>().Length;
                    if (raw.Count != bytesPerLayer) { tmps.Add(tmp); Cleanup(tmps); Abort($"encoded size {raw.Count} != {bytesPerLayer} bytes for '{g.Name}'"); return; }
                    tmp.LoadRawTextureData(raw.ToArray());
                    tmp.Apply(false, false);
                    tmps.Add(tmp);
                }

                // GPU copies of every vanilla layer and mip, then the glyphs.
                for (int l = 0; l < baseLayer; l++) Graphics.CopyTexture(src, l, dst, l);
                for (int i = 0; i < tmps.Count; i++) Graphics.CopyTexture(tmps[i], 0, dst, baseLayer + i);
                Cleanup(tmps);

                // Verify on the GPU: read back the smallest mip of a few vanilla layers and compare with the CPU copy.
                if (src.isReadable && !VerifySample(src, dst, baseLayer, out why)) { Abort("verification failed: " + why); return; }

                for (int i = 0; i < list.Count; i++) list[i].Layer = baseLayer + i;
                Shader.SetGlobalTexture("MAIN_TEX", dst);
                if (loader != null) Glyphs.NormalId = PickFlatNormal(loader);
                Plugin.Log.LogInfo($"[Inject] OK: added {list.Count} decal layers ({baseLayer}..{baseLayer + list.Count - 1}), normalId={Glyphs.NormalId}.");
                dst = null;
            }
            catch (Exception e) { Abort("exception: " + e); }
            finally { if (dst != null) UnityEngine.Object.Destroy(dst); }
        }

        static void Cleanup(List<Texture2D> t) { foreach (var x in t) UnityEngine.Object.Destroy(x); t.Clear(); }

        static void Abort(string why)
        {
            foreach (var g in Glyphs.ById) if (g != null) g.Layer = -1;
            Plugin.Log.LogWarning("[Inject] ABORTED, world textures left vanilla, modded decals show the vanilla Faction placeholder. Reason: " + why);
        }

        // Compare a vanilla layer between src (CPU data) and dst (read back from the GPU via a RenderTexture).
        static bool VerifySample(Texture2DArray src, Texture2DArray dst, int baseLayer, out string why)
        {
            why = null;
            int mip = Math.Max(0, Math.Min(src.mipmapCount - 1, 4)); int sz = Math.Max(1, 256 >> mip);
            var rt = RenderTexture.GetTemporary(sz, sz, 0, RenderTextureFormat.ARGB32, src.graphicsFormat.ToString().Contains("SRGB") ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            var read = new Texture2D(sz, sz, TextureFormat.RGBA32, false, !src.graphicsFormat.ToString().Contains("SRGB"));
            var prev = RenderTexture.active;
            try
            {
                foreach (int l in new[] { 0, baseLayer / 2, baseLayer - 1 })
                {
                    var tmp2 = new Texture2D(sz, sz, src.format, false, !src.graphicsFormat.ToString().Contains("SRGB"));
                    Graphics.CopyTexture(dst, l, mip, tmp2, 0, 0);
                    Graphics.Blit(tmp2, rt);
                    UnityEngine.Object.Destroy(tmp2);
                    RenderTexture.active = rt;
                    read.ReadPixels(new Rect(0, 0, sz, sz), 0, 0); read.Apply();
                    var a = src.GetPixels32(l, mip); var b = read.GetPixels32();
                    long diff = 0; for (int i = 0; i < a.Length; i++) diff += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
                    double avg = diff / (3.0 * a.Length);
                    Plugin.V($"[Inject] verify layer {l} mip {mip}: mean diff {avg:F2}");
                    if (avg > 8) { why = $"layer {l} differs (mean {avg:F1})"; return false; }
                }
                return true;
            }
            finally { RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.Destroy(read); }
        }

        // Decals sample NORMAL_TEX too. Pick the flattest normal layer used by a vanilla decal, so glyphs don't show
        // another decal's relief. -1 if a vanilla decal already uses "no normal map".
        static int PickFlatNormal(ResourcesLoader loader)
        {
            var ids = Brick.BRICK_INDEX.Where(b => b != null && b.IsDecal && b.VoxelMaterials != null && b.VoxelMaterials.Length > 0)
                .Select(b => b.VoxelMaterials[0].normalID).Distinct().ToList();
            if (ids.Contains(-1)) return -1;
            int best = TabDecals.DECAL_FACTION_CUSTOM_1.VoxelMaterials[0].normalID; double bestVar = double.MaxValue; // only runs after load
            try
            {
                var n = loader.ta_voxelNormal;
                if (n == null || !n.isReadable) return best;
                int mip = Math.Min(3, n.mipmapCount - 1);
                foreach (int id in ids)
                {
                    if (id < 0 || id >= n.depth) continue;
                    var px = n.GetPixels32(id, mip);
                    double mr = 0, mg = 0, mb = 0, ma = 0;
                    foreach (var p in px) { mr += p.r; mg += p.g; mb += p.b; ma += p.a; }
                    int c = px.Length; mr /= c; mg /= c; mb /= c; ma /= c;
                    double v = 0;
                    foreach (var p in px) v += (p.r - mr) * (p.r - mr) + (p.g - mg) * (p.g - mg) + (p.b - mb) * (p.b - mb) + (p.a - ma) * (p.a - ma);
                    if (v < bestVar) { bestVar = v; best = id; }
                }
            }
            catch (Exception e) { Plugin.V("[Inject] normal scan skipped: " + e.Message); }
            return best;
        }
    }

    // ---------- Rendering: point modded decals at their glyph layer (main thread, no Burst) ----------
    [HarmonyPatch(typeof(DecalRendererResources), nameof(DecalRendererResources.SetDecal))]
    internal static class Patch_SetDecal
    {
        static void Postfix(BrickInstanceData brickData, int __result)
        {
            int id = Glyphs.Decode(brickData);
            if (id == 0) return;
            var g = id <= Glyphs.MaxId ? Glyphs.ById[id] : null;
            if (g == null || g.Layer < 0)
            {
                Glyphs.WarnOnce(id, $"[Render] decal variant {id} is not available on this machine (missing custom PNG or texture injection failed); showing the vanilla placeholder.");
                return;
            }
            var d = DecalRendererResources.datas[__result];
            d.textureId = g.Layer;
            if (Glyphs.NormalId != int.MinValue) d.normalId = Glyphs.NormalId;
            DecalRendererResources.datas[__result] = d;
            DecalRendererResources.SetMinMax(__result);
        }
    }

    // ---------- Placement: when a Faction custom decal is held and a glyph is selected, encode it ----------
    [HarmonyPatch(typeof(ActionBuild), nameof(ActionBuild.LeftClickUp))]
    internal static class Patch_BuildLeftClickUp
    {
        internal static bool Applied;
        static void Cleanup(Exception ex) { Applied = ex == null; }
        static void Prefix(ActionBuild __instance)
        {
            if (Picker.Selected == 0 || __instance.brick == null || !Glyphs.IsBankBrick((int)__instance.brick.ItemID)) return;
            if (!ActionBuild.isValid || ActionBuild.isColliding || ActionBuild.DidCancelScaling) return;
            int local = Glyphs.Encode(Picker.Selected, out ushort bank);
            var bd = ActionBuild.brickData;
            bd.brickId = bank;
            bd.scale = (ushort)((bd.scale & 0x0FFF) | (local << 12));
            ActionBuild.brickData = bd;
            Plugin.Log.LogInfo($"[Place] glyph {Picker.Selected} written to brick {bank} slot {local} (scale=0x{bd.scale:X4}).");
        }
    }

    [HarmonyPatch(typeof(NetworkedBuilder), nameof(NetworkedBuilder.LocalVoxelAction))]
    internal static class Patch_LocalVoxelAction
    {
        static void Prefix(ref VoxelActionData data, bool isMirror)
        {
            if (isMirror || Picker.Selected == 0 || data.type != VoxelActionType.build) return;
            var bd = data.brickData;
            if (!Glyphs.IsBankBrick(bd.brickId) || (bd.scale >> 12) != 0) return;
            int local = Glyphs.Encode(Picker.Selected, out ushort bank);
            bd.brickId = bank;
            bd.scale = (ushort)((bd.scale & 0x0FFF) | (local << 12));
            data.brickData = bd;
            Plugin.Log.LogInfo($"[Place] glyph {Picker.Selected} written to brick {bank} slot {local} (scale=0x{bd.scale:X4}, fallback).");
        }
    }

    [HarmonyPatch(typeof(ActionBuild), nameof(ActionBuild.OnSelection), new[] { typeof(Brick) })]
    internal static class Patch_BuildOnSelection
    {
        internal static Brick Held;
        static void Postfix(Brick brick) { Held = brick; }
    }

    // Symmetry copies rebuild scale with PackScale (bits 0-11 only); keep our glyph bits on the mirrored copy.
    [HarmonyPatch(typeof(EntitySymmetry), nameof(EntitySymmetry.ApplySymmetry))]
    internal static class Patch_Symmetry
    {
        static void Prefix(ref BrickInstanceData brickData, out int __state) { __state = brickData.scale & 0xF000; }
        static void Postfix(ref BrickInstanceData brickData, int __state)
        {
            if (__state != 0 && Glyphs.IsBankBrick(brickData.brickId)) brickData.scale = (ushort)((brickData.scale & 0x0FFF) | __state);
        }
    }

    // ---------- Picker window (IMGUI) ----------
    internal static class Picker
    {
        public static bool Open;
        public static int Selected;
        static Rect win = new Rect(40, 80, 620, 560);
        static Vector2 scroll;
        static CursorLockMode prevLock; static bool prevVisible, wasOpen;

        public static void Draw()
        {
            if (Open && !wasOpen) { prevLock = Cursor.lockState; prevVisible = Cursor.visible; }
            if (!Open && wasOpen) { Cursor.lockState = prevLock; Cursor.visible = prevVisible; }
            wasOpen = Open;
            if (Open)
            {
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                win = GUI.Window(0x4D44, win, DrawWindow, "More Decals  (" + Plugin.PickerKey.Value + " to close)");
            }
            else if (Selected != 0)
            {
                var held = Patch_BuildOnSelection.Held;
                bool ok = held != null && Glyphs.IsBankBrick((int)held.ItemID);
                GUI.Label(new Rect(20, Screen.height - 60, 700, 24), $"More Decals: {Glyphs.ById[Selected]?.Name ?? "?"}" + (ok ? "" : "  (hold any FACTION #1-#4 decal to place it)"));
            }
        }

        static void DrawWindow(int _)
        {
            GUILayout.Label("Pick a decal, then hold any Codex Decals > FACTION #1-#4 decal and build as usual.");
            if (GUILayout.Button(Selected == 0 ? "> None (vanilla Faction decal)" : "None (vanilla Faction decal)")) { Selected = 0; Plugin.Log.LogInfo("[Picker] selected none (vanilla)"); }
            scroll = GUILayout.BeginScrollView(scroll);
            Section("Built-in", 1, Glyphs.BuiltInMax);
            Section("Custom (BepInEx/plugins/MoreDecals/Custom)", Glyphs.CustomFirst, Glyphs.MaxId);
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        static void Section(string title, int from, int to)
        {
            GUILayout.Label(title);
            int col = 0; GUILayout.BeginHorizontal();
            for (int id = from; id <= to; id++)
            {
                var g = Glyphs.ById[id]; if (g == null) continue;
                var prev = GUI.backgroundColor;
                if (Selected == id) GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button(new GUIContent(g.Tex, g.Name), GUILayout.Width(64), GUILayout.Height(64))) { Selected = id; Plugin.Log.LogInfo($"[Picker] selected glyph {id} ({g.Name})"); }
                GUI.backgroundColor = prev;
                if (++col == 8) { col = 0; GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();
        }
    }

    // ---------- Block encoders for the glyph layers ----------
    internal static class Encoder
    {
        public static bool Supported(TextureFormat f) => f == TextureFormat.RGBA32 || f == TextureFormat.ARGB32 || f == TextureFormat.BGRA32 || f == TextureFormat.DXT5 || f == TextureFormat.BC7;

        public static byte[] Encode(Color32[] px, int s, TextureFormat f)
        {
            switch (f)
            {
                case TextureFormat.RGBA32: return Raw(px, 0, 1, 2, 3);
                case TextureFormat.ARGB32: return Raw(px, 3, 0, 1, 2);
                case TextureFormat.BGRA32: return Raw(px, 2, 1, 0, 3);
                case TextureFormat.DXT5: return Blocks(px, s, Dxt5Block);
                case TextureFormat.BC7: return Blocks(px, s, Bc7Block);
            }
            throw new NotSupportedException(f.ToString());
        }

        static byte[] Raw(Color32[] px, int ri, int gi, int bi, int ai)
        {
            var o = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++) { o[i * 4 + ri] = px[i].r; o[i * 4 + gi] = px[i].g; o[i * 4 + bi] = px[i].b; o[i * 4 + ai] = px[i].a; }
            return o;
        }

        delegate void BlockFn(Color32[] b, byte[] o, int off);
        static byte[] Blocks(Color32[] px, int s, BlockFn fn)
        {
            int bw = Math.Max(1, (s + 3) / 4);
            var o = new byte[bw * bw * 16]; var b = new Color32[16];
            for (int by = 0; by < bw; by++) for (int bx = 0; bx < bw; bx++)
                {
                    for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
                            b[y * 4 + x] = px[Math.Min(s - 1, by * 4 + y) * s + Math.Min(s - 1, bx * 4 + x)];
                    fn(b, o, (by * bw + bx) * 16);
                }
            return o;
        }

        static void Dxt5Block(Color32[] b, byte[] o, int off)
        {
            byte a0 = 0, a1 = 255;
            foreach (var p in b) { if (p.a > a0) a0 = p.a; if (p.a < a1) a1 = p.a; }
            o[off] = a0; o[off + 1] = a1;
            var pal = new int[8]; pal[0] = a0; pal[1] = a1;
            for (int i = 2; i < 8; i++) pal[i] = ((8 - i) * a0 + (i - 1) * a1) / 7;
            ulong ab = 0;
            for (int i = 0; i < 16; i++) { int best = 0, bd = 999; for (int k = 0; k < 8; k++) { int d = Math.Abs(pal[k] - b[i].a); if (d < bd) { bd = d; best = k; } } ab |= (ulong)best << (3 * i); }
            for (int i = 0; i < 6; i++) o[off + 2 + i] = (byte)(ab >> (8 * i));
            Color32 mn = new Color32(255, 255, 255, 0), mx = new Color32(0, 0, 0, 0);
            foreach (var p in b) { mn.r = Math.Min(mn.r, p.r); mn.g = Math.Min(mn.g, p.g); mn.b = Math.Min(mn.b, p.b); mx.r = Math.Max(mx.r, p.r); mx.g = Math.Max(mx.g, p.g); mx.b = Math.Max(mx.b, p.b); }
            ushort c0 = To565(mx), c1 = To565(mn);
            if (c0 < c1) { var t = c0; c0 = c1; c1 = t; }
            o[off + 8] = (byte)c0; o[off + 9] = (byte)(c0 >> 8); o[off + 10] = (byte)c1; o[off + 11] = (byte)(c1 >> 8);
            uint idx = 0;
            if (c0 != c1)
            {
                var p0 = From565(c0); var p1 = From565(c1);
                var cp = new[] { p0, p1, Lerp(p0, p1, 1, 3), Lerp(p0, p1, 2, 3) };
                for (int i = 0; i < 16; i++) { int best = 0, bd = int.MaxValue; for (int k = 0; k < 4; k++) { int d = Dist(cp[k], b[i]); if (d < bd) { bd = d; best = k; } } idx |= (uint)best << (2 * i); }
            }
            o[off + 12] = (byte)idx; o[off + 13] = (byte)(idx >> 8); o[off + 14] = (byte)(idx >> 16); o[off + 15] = (byte)(idx >> 24);
        }
        static ushort To565(Color32 c) => (ushort)(((c.r >> 3) << 11) | ((c.g >> 2) << 5) | (c.b >> 3));
        static int[] From565(ushort c) { int r = (c >> 11) & 31, g = (c >> 5) & 63, b = c & 31; return new[] { (r << 3) | (r >> 2), (g << 2) | (g >> 4), (b << 3) | (b >> 2) }; }
        static int[] Lerp(int[] a, int[] b, int n, int d) => new[] { (a[0] * (d - n) + b[0] * n) / d, (a[1] * (d - n) + b[1] * n) / d, (a[2] * (d - n) + b[2] * n) / d };
        static int Dist(int[] a, Color32 p) { int dr = a[0] - p.r, dg = a[1] - p.g, db = a[2] - p.b; return dr * dr + dg * dg + db * db; }

        // BC7 mode 6: one RGBA endpoint pair (7 bits + p-bit), 4-bit indices.
        static readonly int[] W4 = { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };
        static void Bc7Block(Color32[] b, byte[] o, int off)
        {
            int[] mn = { 255, 255, 255, 255 }, mx = { 0, 0, 0, 0 };
            foreach (var p in b) { int[] v = { p.r, p.g, p.b, p.a }; for (int c = 0; c < 4; c++) { mn[c] = Math.Min(mn[c], v[c]); mx[c] = Math.Max(mx[c], v[c]); } }
            int[] e0 = new int[4], e1 = new int[4]; int p0 = 0, p1 = 1;
            for (int c = 0; c < 4; c++) { e0[c] = mn[c] >> 1; e1[c] = mx[c] >> 1; }
            int[] idx = new int[16];
            int[] q0 = new int[4], q1 = new int[4];
            for (int c = 0; c < 4; c++) { q0[c] = (e0[c] << 1) | p0; q1[c] = (e1[c] << 1) | p1; }
            for (int i = 0; i < 16; i++)
            {
                int[] v = { b[i].r, b[i].g, b[i].b, b[i].a }; int best = 0; long bd = long.MaxValue;
                for (int k = 0; k < 16; k++) { long d = 0; for (int c = 0; c < 4; c++) { int r = ((64 - W4[k]) * q0[c] + W4[k] * q1[c] + 32) >> 6; d += (r - v[c]) * (r - v[c]); } if (d < bd) { bd = d; best = k; } }
                idx[i] = best;
            }
            if (idx[0] >= 8) { var t = e0; e0 = e1; e1 = t; var tp = p0; p0 = p1; p1 = tp; for (int i = 0; i < 16; i++) idx[i] = 15 - idx[i]; }
            var bits = new BitWriter(o, off);
            bits.Put(1 << 6, 7);
            for (int c = 0; c < 4; c++) { bits.Put(e0[c], 7); bits.Put(e1[c], 7); }
            bits.Put(p0, 1); bits.Put(p1, 1);
            bits.Put(idx[0], 3);
            for (int i = 1; i < 16; i++) bits.Put(idx[i], 4);
        }
        struct BitWriter
        {
            readonly byte[] o; readonly int off; int pos;
            public BitWriter(byte[] o, int off) { this.o = o; this.off = off; pos = 0; for (int i = 0; i < 16; i++) o[off + i] = 0; }
            public void Put(int v, int n) { for (int i = 0; i < n; i++, pos++) if (((v >> i) & 1) != 0) o[off + (pos >> 3)] |= (byte)(1 << (pos & 7)); }
        }
    }
}
