using HarmonyLib;
using UnityEngine;

namespace GoodItemScan.Patches;

[HarmonyPatch(typeof(StoryLog))]
public class StoryLogPatch {
    [HarmonyPatch(nameof(StoryLog.RemoveLogCollectible))]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    public static void DisableScanNode(StoryLog __instance) {
        foreach (var collider in __instance.GetComponentsInChildren<BoxCollider>()) Object.Destroy(collider.gameObject);
    }
}