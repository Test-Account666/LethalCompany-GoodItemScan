using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using GoodItemScan.Patches;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace GoodItemScan;

[BepInDependency("HDLethalCompany", BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class GoodItemScan : BaseUnityPlugin {
    public static readonly List<Hook> Hooks = [
    ];

    public static GoodItemScan Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }
    public static RectTransform originalRectTransform = null!;

    private void Awake() {
        Logger = base.Logger;
        Instance = this;

        ConfigManager.Initialize(Config);

        ConfigManager.scanNodesHardLimit.SettingChanged += (_, _) => SetIncreasedMaximumScanNodes(HUDManager.Instance);

        Patch();

        HUDManagerPatch.InitMonoMod();

        UnpatchHdLethalCompany();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    private static void UnpatchHdLethalCompany() {
        var updateScanNodesMethod = AccessTools.DeclaredMethod(typeof(HUDManager), nameof(HUDManager.UpdateScanNodes));

        var patches = Harmony.GetPatchInfo(updateScanNodesMethod);

        if (patches == null) return;

        foreach (var postfix in (Patch?[]) [
                     ..patches.Postfixes,
                 ]) {
            if (postfix == null) continue;

            if (!postfix.owner.ToLower().Contains("hdlethalcompany")) continue;

            Harmony?.Unpatch(updateScanNodesMethod, HarmonyPatchType.Postfix, postfix.owner);

            Logger.LogInfo("Found HDLethalCompany patch!");
            Logger.LogInfo($"Unpatched {updateScanNodesMethod} method!");
        }
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

        foreach (var hook in Hooks) hook.Undo();

        LogDebug("Finished unpatching!");
    }

    internal static void SetIncreasedMaximumScanNodes(HUDManager? hudManager) {
        if (hudManager == null) return;

        foreach (var scanNode in hudManager.scanElements) {
            hudManager.scanNodes.Remove(scanNode);
            scanNode.gameObject.SetActive(false);
        }

        if (hudManager.scanElements.Length > 0) originalRectTransform = hudManager.scanElements[0];

        if (originalRectTransform == null) {
            Logger.LogFatal("An error occured while trying to increase maximum scan nodes!");
            return;
        }

        hudManager.scanNodesHit = [
        ];

        hudManager.scanElements = [
        ];

        Scanner.FillInScanNodes(originalRectTransform);
    }

    internal static void LogDebug(object data) {
        if (!ConfigManager.sendDebugMessages.Value) return;
        Logger.LogInfo(data);
    }
}