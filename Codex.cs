using System;
using System.Collections.Generic;
using Game;
using HarmonyLib;
using UI;
using UI.Codex;
using UnityEngine;
using VoxelEngine;

namespace MoreDecals
{
    // v1.1: custom decals as real Codex entries (Decals tab, own "Custom Decals" family).
    // An entry is a FACTION #1-#4 brick item (309-312) whose ItemInstanceData.contextualDataId carries a marker
    // (0x4D440000 | glyph id). Vanilla ignores contextualDataId for bricks, so the item behaves like the faction
    // decal everywhere (equip, multiplayer select RPC, hotbar save); we only change its icon, name, merging and the
    // scale bits written at placement. No new brick ids.
    internal sealed class CustomDecalItem : Item
    {
        public readonly Glyph G; readonly Brick bank;
        public CustomDecalItem(Glyph g)
        {
            G = g; Glyphs.Encode(g.Id, out ushort b); bank = Brick.BRICK_INDEX[b];
            name = g.Name; description = "Custom decal (MoreDecals)."; SpriteName = "MoreDecals_" + g.Id;
        }
        public override WorldItemType WorldItemType => WorldItemType.brick;
        public override uint ItemID => bank.ItemID;
        public override ItemType ItemType => ItemType.brick;
        public override Bounds Bounds => bank.Bounds;
        public override string TranslatedName => G.Name;
    }

    internal static class CodexEntries
    {
        public const int Marker = 0x4D440000, MarkerMask = 0x7FFF0000;
        public static int MarkerOf(int glyph) => Marker | glyph;
        public static int GlyphOf(ItemInstanceData d)
        {
            if (d.itemType != WorldItemType.brick || !Glyphs.IsBankBrick(d.prefabId) || (d.contextualDataId & MarkerMask) != Marker) return 0;
            int id = d.contextualDataId & 0xFFFF;
            return id >= 1 && id <= Glyphs.MaxId ? id : 0;
        }
        public static bool Added;

        // Called from StartWhenLoaded (after codexLoaded + asset repos + injection). UI_Codex builds its slots in
        // Awake from UI_Codex.TABS_DECALS, so entries appear if the codex UI is created after this point.
        public static void Add()
        {
            if (Added) return;
            var list = new List<Glyph>();
            foreach (var g in Glyphs.ById) if (g != null && g.Layer >= 0) list.Add(g);
            if (list.Count == 0) { Plugin.Log.LogWarning("[Codex] no injected custom decals; no Codex entries added."); return; }
            var tab = new CodexTab("Custom Decals", UI_Codex.TABS_DECALS);
            foreach (var g in list) tab.items.Add(new CustomDecalItem(g));
            Added = true;
            bool early = UnityEngine.Object.FindObjectOfType<UI_Codex>() != null;
            Plugin.Log.LogInfo($"[Codex] added family 'Custom Decals' with {list.Count} entries to the Decals tab (tabs now {UI_Codex.TABS_DECALS.Count})." +
                (early ? " WARNING: UI_Codex already exists; entries show after the next world load." : ""));
        }

        public static int HeldGlyph()
        {
            try
            {
                var bar = Manager.Dependency.ItemBar;
                if (bar == null) return 0;
                return GlyphOf(bar.ItemSlotSelected.ItemData);
            }
            catch { return 0; }
        }

        // Glyph to place: Codex/hotbar entry wins; the Minus picker is the fallback when a plain faction decal is held.
        public static int Current()
        {
            if (Plugin.UseCodexEntries.Value) { int h = HeldGlyph(); if (h != 0) return h; }
            return Picker.Selected;
        }
    }

    [HarmonyPatch(typeof(ItemInstanceData), nameof(ItemInstanceData.FromItem))]
    internal static class Patch_FromItem
    {
        static void Postfix(Item item, ref ItemInstanceData __result)
        {
            if (!(item is CustomDecalItem c)) return;
            __result.contextualDataId = CodexEntries.MarkerOf(c.G.Id);
            ItemInstanceResources.SetItemData(__result, __result.id);
            Plugin.V($"[Codex] slot item {__result.id} = brick {__result.prefabId} glyph {c.G.Id} ({c.G.Name}).");
        }
    }

    [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.SetNewItem))]
    internal static class Patch_SlotIcon
    {
        static readonly HashSet<int> logged = new HashSet<int>();
        static void Postfix(ItemSlot __instance)
        {
            int id = CodexEntries.GlyphOf(__instance.ItemData);
            if (id == 0) return;
            var g = Glyphs.ById[id];
            if (g == null || g.Tex == null) return;
            if (__instance.decalImage != null) __instance.decalImage.enabled = false;
            if (__instance.toolImage == null) { Glyphs.WarnOnce(1000 + id, "[Codex] ItemSlot has no toolImage; icon stays the faction placeholder."); return; }
            __instance.toolImage.texture = g.Tex;
            __instance.toolImage.color = Color.white;
            __instance.toolImage.enabled = true;
            if (logged.Add(id)) Plugin.Log.LogInfo($"[Codex] icon set for glyph {id} ({g.Name}) on slot '{__instance.name}'.");
        }
    }

    [HarmonyPatch(typeof(ItemInstanceData), nameof(ItemInstanceData.Name))]
    internal static class Patch_Name
    {
        static void Postfix(ItemInstanceData __instance, ref string __result)
        {
            int id = CodexEntries.GlyphOf(__instance);
            if (id != 0 && Glyphs.ById[id] != null) __result = Glyphs.ById[id].Name;
        }
    }

    // Different glyphs (or glyph vs. plain faction decal) must not stack into one hotbar slot.
    [HarmonyPatch(typeof(ItemInstanceCommon), "CanMerge", new[] { typeof(ItemInstanceData), typeof(ItemInstanceData), typeof(StorageContext), typeof(bool) }, new[] { ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal })]
    internal static class Patch_CanMerge
    {
        static void Postfix(ref ItemInstanceData itemDataA, ItemInstanceData itemDataB, ref bool __result)
        {
            if (__result && itemDataA.itemType == WorldItemType.brick && CodexEntries.GlyphOf(itemDataA) != CodexEntries.GlyphOf(itemDataB)) __result = false;
        }
    }
}
