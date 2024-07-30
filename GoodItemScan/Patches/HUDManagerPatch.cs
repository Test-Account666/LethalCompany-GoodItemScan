using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace GoodItemScan.Patches;

[HarmonyPatch(typeof(HUDManager))]
public static class HUDManagerPatch {
    [HarmonyPatch(nameof(HUDManager.AssignNodeToUIElement))]
    [HarmonyPrefix]
    private static bool DontAssignNodeToUIElement() => false;

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
    private static bool NodeIsAlwaysVisible(ref bool __result) {
        __result = false;
        return false;
    }

    [HarmonyPatch(nameof(HUDManager.DisableAllScanElements))]
    [HarmonyPrefix]
    private static bool RedirectDisableAllScanElements() {
        Scanner.DisableAllScanElements();
        return false;
    }

    [HarmonyPatch(nameof(HUDManager.UpdateScanNodes))]
    [HarmonyPrefix]
    private static bool UpdateScanNodes(PlayerControllerB playerScript) {
        Scanner.UpdateScanNodes(playerScript);
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