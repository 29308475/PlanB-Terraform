using System;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PlanBProductionChains
{
    internal static class ModsMenuIntegration
    {
        private static readonly BepInEx.Logging.ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("Mods-Menü");
        [HarmonyPatch(typeof(SSceneMods), "RefreshModsUI"), HarmonyPostfix]
        private static void AddLoadedPlugins(SSceneMods __instance)
        {
            try
            {
                if (__instance.modsListParent == null || __instance.modPrefab == null) return;
                foreach (var plugin in Chainloader.PluginInfos.Values.OrderBy(x => x.Metadata.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    var row = UnityEngine.Object.Instantiate(__instance.modPrefab, __instance.modsListParent);
                    row.name = "BepInExMod_" + plugin.Metadata.GUID;
                    // This is a display row, never a Lua mod in GMods.mods. Native
                    // drag, upload, delete and activation handlers must not act on it.
                    row.enabled = false;
                    var loaded = plugin.Instance != null;
                    var directory = Path.GetDirectoryName(plugin.Location);
                    row.mod = new CMod { title = plugin.Metadata.Name, path = directory, active = loaded };
                    if (row.title != null)
                    { row.title.supportRichText = false; row.title.text = plugin.Metadata.Name; }
                    SetText(row.textId, "Version " + plugin.Metadata.Version);
                    SetText(row.textOwnerId, loaded ? "Geladen · BepInEx" : "Nicht geladen · BepInEx");
                    if (row.toggle != null)
                    {
                        row.toggle.onValueChanged = new Toggle.ToggleEvent();
                        row.toggle.group = null;
                        row.toggle.SetIsOnWithoutNotify(loaded);
                        row.toggle.interactable = false;
                    }
                    foreach (var button in row.GetComponentsInChildren<Button>(true))
                    { button.onClick = new Button.ButtonClickedEvent(); button.interactable = false; }
                    if (row.buttonStore != null) row.buttonStore.gameObject.SetActive(false);
                    if (row.buttonUpload != null) row.buttonUpload.gameObject.SetActive(false);
                    if (row.buttonDelete != null) row.buttonDelete.gameObject.SetActive(false);
                    if (row.ownerActions != null) row.ownerActions.SetActive(true);
                    if (row.buttonFolder != null && !string.IsNullOrEmpty(directory))
                    {
                        row.buttonFolder.gameObject.SetActive(true);
                        row.buttonFolder.interactable = true;
                        row.buttonFolder.onClick.AddListener(() => Application.OpenURL(new Uri(directory + Path.DirectorySeparatorChar).AbsoluteUri));
                    }
                    foreach (var tooltip in row.GetComponentsInChildren<CTooltipTarget>(true))
                    {
                        tooltip.text = plugin.Metadata.Name + " · " + plugin.Metadata.Version;
                        tooltip.textDesc = "Über BepInEx geladen. Aktivierung und Deaktivierung erfolgen außerhalb des laufenden Spiels; anschließend neu starten.";
                    }
                    if (row.buttonFolder != null)
                    {
                        var tooltip = row.buttonFolder.GetComponent<CTooltipTarget>();
                        if (tooltip != null) { tooltip.text = "Mod-Ordner öffnen"; tooltip.textDesc = directory; }
                    }
                    row.gameObject.SetActive(true);
                }
                log.LogInfo("Mods-Menü ergänzt: " + Chainloader.PluginInfos.Count + " BepInEx-Mods.");
            }
            catch (Exception error)
            { log.LogError(error); }
        }

        private static void SetText(Text label, string value)
        {
            if (label == null) return;
            label.supportRichText = false; label.text = value; label.gameObject.SetActive(true);
        }
    }
}
