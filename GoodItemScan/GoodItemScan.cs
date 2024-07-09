using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GoodItemScan;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class GoodItemScan : BaseUnityPlugin {
    public static GoodItemScan Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    private void Awake() {
        Logger = base.Logger;
        Instance = this;

        ConfigManager.Initialize(Config);

        ConfigManager.scanNodesHardLimit.SettingChanged += (_, _) => SetIncreasedMaximumScanNodes(HUDManager.Instance);

        Patch();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    internal static void Patch() {
        Harmony ??= new(MyPluginInfo.PLUGIN_GUID);

        LogDebug("Patching...");

        Harmony.PatchAll();

        LogDebug("Finished patching!");
    }

    internal static void Unpatch() {
        LogDebug("Unpatching...");

        Harmony?.UnpatchSelf();

        LogDebug("Finished unpatching!");
    }

    internal static void SetIncreasedMaximumScanNodes(HUDManager? hudManager) {
        if (hudManager is null) return;

        foreach (var scanNode in hudManager.scanElements) {
            hudManager.scanNodes.Remove(scanNode);
            scanNode.gameObject.SetActive(false);
        }


        hudManager.scanNodesHit = new RaycastHit[ConfigManager.scanNodesHardLimit.Value];

        var rectTransform = hudManager.scanElements[0];

        List<RectTransform> scanElementsList = [
        ];

        for (var index = 0; index < ConfigManager.scanNodesHardLimit.Value; index++)
            scanElementsList.Add(Instantiate(rectTransform, rectTransform.position, rectTransform.rotation, rectTransform.parent));

        hudManager.scanElements = scanElementsList.ToArray();
    }

    internal static void LogDebug(object data) {
        if (!ConfigManager.sendDebugMessages.Value) return;
        Logger.LogInfo(data);
    }
}