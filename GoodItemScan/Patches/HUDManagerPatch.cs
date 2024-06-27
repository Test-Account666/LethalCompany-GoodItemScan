using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoodItemScan.Patches;

[HarmonyPatch(typeof(HUDManager))]
public static class HUDManagerPatch {
    [HarmonyPatch(nameof(HUDManager.AssignNewNodes))]
    [HarmonyPrefix]
    private static bool DontAssignNewNodes() => false;

    [HarmonyPatch(nameof(HUDManager.MeetsScanNodeRequirements))]
    [HarmonyPrefix]
    // ReSharper disable once InconsistentNaming
    private static bool MeetsScanNodeRequirements(ref bool __result) {
        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(HUDManager.NodeIsNotVisible))]
    [HarmonyPrefix]
    // ReSharper disable once InconsistentNaming
    private static bool NodeIsNotVisible(ScanNodeProperties node, int elementIndex, ref bool __result) {
        var hudManager = HUDManager.Instance;

        var scanElement = hudManager?.scanElements[elementIndex];

        if (hudManager is null || scanElement is null) return false;

        var localPlayer = StartOfRound.Instance.localPlayerController;

        if (localPlayer is null) return false;

        if (Scanner.IsScanNodeVisible(node)) {
            __result = false;
            return false;
        }


        if (node.nodeType == 2) hudManager.totalScrapScanned = Mathf.Clamp(hudManager.totalScrapScanned - node.scrapValue, 0, 100000);

        scanElement.gameObject.SetActive(false);
        hudManager.scanNodes.Remove(scanElement);

        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(HUDManager.PingScan_performed))]
    [HarmonyPrefix]
    private static bool PingScanPerformed(ref InputAction.CallbackContext context) {
        if (!context.performed) return false;

        Scanner.Scan();
        return false;
    }

    [HarmonyPatch(nameof(HUDManager.Awake))]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    private static void AfterHudManagerAwake(HUDManager __instance) => GoodItemScan.SetIncreasedMaximumScanNodes(__instance);
}