using System.Collections;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoodItemScan;

public static class Scanner {
    private static readonly int _ScanAnimatorHash = Animator.StringToHash("scan");

    public static void Scan() {
        var localPlayer = StartOfRound.Instance.localPlayerController;

        if (localPlayer is null) return;

        var hudManager = HUDManager.Instance;

        if (!hudManager.CanPlayerScan() || hudManager.playerPingingScan > -1.0) return;

        hudManager.playerPingingScan = 0.3f;
        hudManager.scanEffectAnimator.transform.position = localPlayer.gameplayCamera.transform.position;
        hudManager.scanEffectAnimator.SetTrigger(_ScanAnimatorHash);
        hudManager.UIAudio.PlayOneShot(hudManager.scanSFX);

        GoodItemScan.LogDebug("Scan Initiated!");

        var scanNodes = Object.FindObjectsOfType<ScanNodeProperties>(false);

        scanNodes ??= [
        ];

        GoodItemScan.LogDebug($"Got '{scanNodes.Length}' nodes!");

        var currentScanNodeCount = 0L;

        var playerLocation = localPlayer.transform.position;

        if (ConfigManager.preferClosestNodes.Value)
            scanNodes = scanNodes.OrderBy(node => Vector3.Distance(playerLocation, node.transform.position)).ToArray();

        foreach (var scanNodeProperties in scanNodes) {
            if (scanNodeProperties is null) continue;

            var scanNodePosition = scanNodeProperties.transform.position;

            var distance = Vector3.Distance(scanNodePosition, playerLocation);

            if (distance > scanNodeProperties.maxRange) continue;

            if (distance < scanNodeProperties.minRange) continue;

            if (scanNodeProperties.requiresLineOfSight) {
                var isLineOfSightBlocked = Physics.Linecast(localPlayer.gameplayCamera.transform.position, scanNodePosition, 256,
                                                            QueryTriggerInteraction.Ignore);

                if (isLineOfSightBlocked) continue;
            }

            if (!IsScanNodeValid(scanNodeProperties)) continue;


            currentScanNodeCount += 1;

            if (currentScanNodeCount > ConfigManager.scanNodesHardLimit.Value) {
                GoodItemScan.LogDebug($"Hard Limit of {ConfigManager.scanNodesHardLimit.Value} reached!");
                return;
            }

            localPlayer.StartCoroutine(AddScanNodeToUI(scanNodeProperties, currentScanNodeCount));
        }
    }

    private static bool IsScanNodeValid(ScanNodeProperties scanNodeProperties) {
        var parent = scanNodeProperties.transform.parent;

        if (parent is null) return false;

        var foundGrabbableObject = parent.TryGetComponent<GrabbableObject>(out var grabbableObject);
        if (foundGrabbableObject && (grabbableObject.isHeld || grabbableObject.isHeldByEnemy || grabbableObject.deactivated)) return false;

        var foundEnemyAI = parent.TryGetComponent<EnemyAI>(out var enemyAI);
        if (foundEnemyAI && enemyAI.isEnemyDead) return false;

        if (ConfigManager.showOpenedBlastDoorScanNode.Value) return true;

        var foundAccessibleObject = parent.TryGetComponent<TerminalAccessibleObject>(out var terminalAccessibleObject);
        if (!foundAccessibleObject) return true;

        return !terminalAccessibleObject.isBigDoor || !terminalAccessibleObject.isDoorOpen;
    }


    private static IEnumerator AddScanNodeToUI(ScanNodeProperties scanNodeProperties, long currentScanNodeCount) {
        yield return new WaitForSeconds((ConfigManager.scanNodeDelay.Value / 100F) * currentScanNodeCount);

        var localPlayer = StartOfRound.Instance.localPlayerController;

        if (localPlayer is null) yield break;

        var hudManager = HUDManager.Instance;

        if (hudManager is null) yield break;

        GoodItemScan.LogDebug($"Scanning node '{currentScanNodeCount}'!");

        if (scanNodeProperties.nodeType == 2) ++hudManager.scannedScrapNum;
        if (!hudManager.nodesOnScreen.Contains(scanNodeProperties)) hudManager.nodesOnScreen.Add(scanNodeProperties);

        hudManager.AssignNodeToUIElement(scanNodeProperties);
    }

    public static bool IsScanNodeVisible(ScanNodeProperties node) {
        if (!node.gameObject.activeSelf) return false;

        var localPlayer = StartOfRound.Instance.localPlayerController;

        if (localPlayer is null) return false;

        var camera = localPlayer.gameplayCamera;

        var direction = node.transform.position - camera.transform.position;

        direction.Normalize();

        var cosHalfFOV = Mathf.Cos(camera.fieldOfView * 0.6f * Mathf.Deg2Rad);

        if (Vector3.Dot(direction, camera.transform.forward) < cosHalfFOV) return false;

        return IsScanNodeValid(node);
    }
}