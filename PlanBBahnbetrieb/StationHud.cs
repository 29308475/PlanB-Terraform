using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PlanBBahnbetrieb
{
    internal sealed class StationHud : IDisposable
    {
        private readonly GameObject[] rows = new GameObject[4];
        private readonly CUiItem[] fields = new CUiItem[4];
        private GameObject template;
        internal int PendingSlot = -1;
        internal int2 PendingCoords;
        internal void Ensure() { }
        internal void Show(int2 coords) { }
        internal void Refresh()
        {
            var hud = SSceneSingleton<SSceneHud_Selection>.Inst;
            if (hud == null) return;
            var selection = SSingleton<SSelection>.Inst.selection;
            bool selected = selection.Count == 1 && selection[0].coords.Positive &&
                SSingleton<SWorld>.Inst.GetContent(selection[0].coords) is FreightStation && GScene3D.actionMode == GScene3D.ActionModes.None;
            if (!selected) { foreach (var row in rows) if (row != null) row.SetActive(false); return; }
            var ui = hud.uiSelection;
            if (ui.item.rootGO == null) return;
            if (template != ui.item.rootGO) { Dispose(); template = ui.item.rootGO; }
            var coords = selection[0].coords;
            var station = (FreightStation)SSingleton<SWorld>.Inst.GetContent(coords);
            var stacks = station.GetStacks(coords);
            for (var i = 0; i < 4; i++)
            {
                if (rows[i] == null)
                {
                    var slot = i;
                    rows[i] = Object.Instantiate(template, template.transform.parent, false);
                    rows[i].name = "Bahnhof_Ware_" + (i + 1);
                    rows[i].transform.SetSiblingIndex(template.transform.GetSiblingIndex() + i + 1);
                    fields[i] = rows[i].GetComponentInChildren<CUiItem>(true);
                    fields[i].SetCallback(_ => Pick(slot));
                    foreach (var text in rows[i].GetComponentsInChildren<Text>(true))
                        if (text.name == ui.item.shortcut.name) text.gameObject.SetActive(false);
                }
                rows[i].SetActive(true);
                var field = fields[i]; var stack = stacks.stacks[i];
                field.SetItem(stack.item); field.button.interactable = true;
                if (stack.item == null) field.textName.text = "WARE " + (i + 1) + " AUSWÄHLEN";
                field.textAdditional.text = "Warenpuffer " + (i + 1) + " · Gelagert " + stack.nb + "/" + stack.nbMax;
            }
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)template.transform.parent);
        }
        private void Pick(int slot)
        {
            var selection = SSingleton<SSelection>.Inst.selection;
            if (selection.Count != 1 || !(selection[0].item is FreightStation)) return;
            PendingSlot = slot; PendingCoords = selection[0].coords;
            SSceneSingleton<SScenePopup>.Inst.ShowItemsPickUp(PendingCoords);
        }
        internal void Choose(CItem item)
        {
            var slot = PendingSlot; var coords = PendingCoords; PendingSlot = -1;
            if (slot < 0 || !(SSingleton<SWorld>.Inst.GetContent(coords) is FreightStation station)) return;
            var stocks = station.GetStacks(coords);
            if (stocks.stacks[slot].item != item)
            {
                SSingleton<SDrones>.Inst.OnDepotDestroyed(coords);
                var old = stocks.stacks[slot];
                if (old.item != null && old.nb > 0) SSingleton<SItems>.Inst.DumpContainersAround(coords, old.item, old.nb);
                stocks.stacks[slot].SetItem(item); station.RefreshStacksInfos(coords, stocks);
                SSingleton<SDrones>.Inst.OnDepotBuilt(station, coords);
            }
            SSceneSingleton<SScenePopup>.Inst.ClosePopup(true);
            SSceneSingleton<SSceneHud_Selection>.Inst.RefreshSelectionPanel(); Refresh();
        }
        internal void Visible(bool visible) { if (!visible) foreach (var row in rows) if (row != null) row.SetActive(false); }
        internal void Hide() { PendingSlot = -1; foreach (var row in rows) if (row != null) row.SetActive(false); }
        public void Dispose()
        {
            for (var i = 0; i < rows.Length; i++) { if (rows[i] != null) Object.Destroy(rows[i]); rows[i] = null; fields[i] = null; }
            template = null; PendingSlot = -1;
        }
    }
    [HarmonyPatch(typeof(SSceneHud_Selection), nameof(SSceneHud_Selection.RefreshSelectionPanel))]
    internal static class StationNativeSelectionPatch
    { private static void Postfix() => Plugin.Instance?.Hud.Refresh(); }
    [HarmonyPatch(typeof(CUiBarElement), nameof(CUiBarElement.Refresh))]
    internal static class StationNativeBuildButtonPatch
    {
        private static void Postfix(CUiBarElement __instance)
        {
            if (!(__instance.Item is FreightStation)) return;
            foreach (var peer in __instance.transform.parent.GetComponentsInChildren<CUiBarElement>(true))
            {
                if (peer == __instance || peer.Item == null || peer.Item is FreightStation || peer._background == null) continue;
                __instance._background.gameObject.SetActive(true);
                __instance._background.sprite = peer._background.sprite;
                __instance._background.type = peer._background.type;
                __instance._background.color = peer._background.color;
                if (__instance._backgroundOn != null && peer._backgroundOn != null)
                { __instance._backgroundOn.sprite = peer._backgroundOn.sprite; __instance._backgroundOn.type = peer._backgroundOn.type; }
                var target = __instance._icon.rectTransform; var source = peer._icon.rectTransform;
                target.anchorMin = source.anchorMin; target.anchorMax = source.anchorMax; target.pivot = source.pivot;
                target.anchoredPosition = source.anchoredPosition; target.sizeDelta = source.sizeDelta;
                target.localScale = source.localScale * .78f;
                __instance._icon.preserveAspect = true;
                break;
            }
        }
    }
}
