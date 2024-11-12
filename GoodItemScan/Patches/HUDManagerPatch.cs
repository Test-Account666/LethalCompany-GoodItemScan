using System;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace GoodItemScan.Patches;

[HarmonyPatch(typeof(HUDManager))]
public static class HUDManagerPatch {
    //create MonoMod Hooks
    internal static void InitMonoMod() {
        GoodItemScan.Hooks.Add(new(AccessTools.Method(typeof(HUDManager), nameof(HUDManager.AssignNodeToUIElement)),
                                   DontAssignNodeToUIElement,
                                   new() {
                                       Priority = -99,
                                   }));

        GoodItemScan.Hooks.Add(new(AccessTools.Method(typeof(HUDManager), nameof(HUDManager.AssignNewNodes)),
                                   DontAssignNewNodes,
                                   new() {
                                       Priority = -99,
                                   }));

        GoodItemScan.Hooks.Add(new(AccessTools.Method(typeof(HUDManager), nameof(HUDManager.MeetsScanNodeRequirements)),
                                   MeetsScanNodeRequirements,
                                   new() {
                                       Priority = -99,
                                   }));

        GoodItemScan.Hooks.Add(new(AccessTools.Method(typeof(HUDManager), nameof(HUDManager.NodeIsNotVisible)),
                                   NodeIsAlwaysVisible,
                                   new() {
                                       Priority = -99,
                                   }));

        GoodItemScan.Hooks.Add(new(AccessTools.Method(typeof(HUDManager), nameof(HUDManager.UpdateScanNodes)),
                                   UpdateScanNodes,
                                   new() {
                                       Priority = -99,
                                   }));
    }

    private static void DontAssignNodeToUIElement(Action<HUDManager, ScanNodeProperties> orig, HUDManager self, ScanNodeProperties node) {
    }

    private static void DontAssignNewNodes(Action<HUDManager, PlayerControllerB> orig, HUDManager self, PlayerControllerB playerScript) {
    }

    private static bool MeetsScanNodeRequirements(Func<HUDManager, ScanNodeProperties, PlayerControllerB, bool> orig, HUDManager self,
                                                  ScanNodeProperties node, PlayerControllerB playerScript) => true;

    private static bool NodeIsAlwaysVisible(Func<HUDManager, ScanNodeProperties, int, bool> orig, HUDManager self,
                                            ScanNodeProperties node, int elementIndex) => false;

    private static void UpdateScanNodes(Action<HUDManager, PlayerControllerB> orig, HUDManager self, PlayerControllerB playerScript) =>
        GoodItemScan.scanner?.UpdateScanNodes();

    [HarmonyPatch(nameof(HUDManager.DisableAllScanElements))]
    [HarmonyPrefix]
    private static bool RedirectDisableAllScanElements() {
        GoodItemScan.scanner?.DisableAllScanElements();
        return false;
    }

    [HarmonyPatch(nameof(HUDManager.PingScan_performed))]
    [HarmonyPrefix]
    private static bool PingScanPerformed(ref InputAction.CallbackContext context) {
        if (!context.performed) return false;

        GoodItemScan.scanner?.Scan();
        return false;
    }

    [HarmonyPatch(nameof(HUDManager.Start))]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    private static void AfterHudManagerAwake(HUDManager __instance) {
        GoodItemScan.scanner = new();
        GoodItemScan.SetIncreasedMaximumScanNodes(__instance);
    }

    [HarmonyPatch(nameof(HUDManager.OnDisable))]
    [HarmonyPostfix]
    private static void ResetCheatsAPI() {
        CheatsAPI.additionalEnemyDistance = 0;
        CheatsAPI.additionalEnemyDistance = 0;
        CheatsAPI.noLineOfSightDistance = 0;
    }
}