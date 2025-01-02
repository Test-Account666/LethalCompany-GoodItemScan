using HarmonyLib;
using UnityEngine;

namespace GoodItemScan.Patches;

[HarmonyPatch(typeof(Landmine))]
public static class LandminePatch {
    [HarmonyPatch(nameof(Landmine.Detonate))]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    public static void DisableLandmineScanNode(Landmine __instance) {
        if (!ConfigManager.fixLandmineScanNode.Value) return;

        var scanNodeTransform = __instance.transform.parent.Find("ScanNode");

        if (!scanNodeTransform || !scanNodeTransform.gameObject) return;

        Object.Destroy(scanNodeTransform.gameObject);
    }
}