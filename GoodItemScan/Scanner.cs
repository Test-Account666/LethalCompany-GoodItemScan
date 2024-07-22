using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
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

        if (ConfigManager.alwaysRescan.Value) {
            hudManager.DisableAllScanElements();
            hudManager.scanNodes.Clear();
            hudManager.scannedScrapNum = 0;
            hudManager.totalScrapScanned = 0;
            hudManager.totalScrapScannedDisplayNum = 0;
        }

        hudManager.totalValueText.text = "$0";

        hudManager.playerPingingScan = 0.3f;
        hudManager.scanEffectAnimator.transform.position = localPlayer.gameplayCamera.transform.position;
        hudManager.scanEffectAnimator.SetTrigger(_ScanAnimatorHash);
        hudManager.UIAudio.PlayOneShot(hudManager.scanSFX);

        GoodItemScan.LogDebug("Scan Initiated!");

        var scanNodes = Object.FindObjectsByType<ScanNodeProperties>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        scanNodes ??= [
        ];

        GoodItemScan.LogDebug($"Got '{scanNodes.Length}' nodes!");
        GoodItemScan.LogDebug($"Got '{_ComponentCache.Count}' nodes in cache!");

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

            if (!HasLineOfSight(scanNodeProperties, localPlayer)) continue;

            if (!IsScanNodeValid(scanNodeProperties)) continue;


            currentScanNodeCount += 1;

            if (currentScanNodeCount > ConfigManager.scanNodesHardLimit.Value) {
                GoodItemScan.LogDebug($"Hard Limit of {ConfigManager.scanNodesHardLimit.Value} reached!");
                return;
            }

            localPlayer.StartCoroutine(AddScanNodeToUI(scanNodeProperties, currentScanNodeCount));
        }
    }

    private static bool HasLineOfSight(ScanNodeProperties scanNodeProperties, PlayerControllerB localPlayer) {
        if (!scanNodeProperties.requiresLineOfSight) return true;

        var hasBoxCollider = scanNodeProperties.TryGetComponent<BoxCollider>(out var boxCollider);

        if (!hasBoxCollider) {
            GoodItemScan.Logger.LogError($"{scanNodeProperties.headerText} has no BoxCollider!");

            if (!ConfigManager.addBoxCollidersToInvalidScanNodes.Value) return false;

            GoodItemScan.Logger.LogError("Adding a BoxCollider!");

            boxCollider = scanNodeProperties.gameObject.AddComponent<BoxCollider>();
        }

        var cameraPosition = localPlayer.gameplayCamera.transform.position;

        var minPosition = boxCollider.bounds.min;

        var maxPosition = boxCollider.bounds.max;

        var corners = new Vector3[8];
        corners[0] = maxPosition;
        corners[1] = new(maxPosition.x, maxPosition.y, minPosition.z);
        corners[2] = new(maxPosition.x, minPosition.y, maxPosition.z);
        corners[3] = new(maxPosition.x, minPosition.y, minPosition.z);
        corners[4] = new(minPosition.x, maxPosition.y, maxPosition.z);
        corners[5] = new(minPosition.x, maxPosition.y, minPosition.z);
        corners[6] = new(minPosition.x, minPosition.y, maxPosition.z);
        corners[7] = minPosition;

        return corners.Select(corner => !Physics.Linecast(cameraPosition, corner, 256, QueryTriggerInteraction.Ignore))
                      .Any(isInLineOfSight => isInLineOfSight);
    }

    private static bool IsScanNodeValid(GrabbableObject? grabbableObject, EnemyAI? enemyAI,
                                        TerminalAccessibleObject? terminalAccessibleObject) {
        if (grabbableObject is not null
         && (grabbableObject.isHeld || grabbableObject.isHeldByEnemy || grabbableObject.deactivated)) return false;

        if (enemyAI is {
                isEnemyDead: true,
            }) return false;

        if (ConfigManager.showOpenedBlastDoorScanNode.Value) return true;

        return terminalAccessibleObject is not {
            isBigDoor: true, isDoorOpen: true,
        };
    }

    private static readonly Dictionary<Transform, (GrabbableObject?, EnemyAI?, TerminalAccessibleObject?)> _ComponentCache = [
    ];

    private static bool IsScanNodeValid(ScanNodeProperties scanNodeProperties) {
        var parent = scanNodeProperties.transform.parent;

        if (parent is null) return false;

        GetComponents(parent, out var cachedComponents);

        var (grabbableObject, enemyAI, terminalAccessibleObject) = cachedComponents;

        return IsScanNodeValid(grabbableObject, enemyAI, terminalAccessibleObject);
    }

    private static void GetComponents(Transform parent,
                                      out (GrabbableObject?, EnemyAI?, TerminalAccessibleObject?) cachedComponents) {
        if (!ConfigManager.useDictionaryCache.Value) {
            GetUncachedComponents(parent, out cachedComponents);
            return;
        }

        if (_ComponentCache.TryGetValue(parent, out cachedComponents)) return;

        GetUncachedComponents(parent, out cachedComponents);
        _ComponentCache[parent] = cachedComponents;
    }

    private static void GetUncachedComponents(Transform parent,
                                              out (GrabbableObject?, EnemyAI?, TerminalAccessibleObject?) cachedComponents) {
        var grabbableObjectFound = parent.TryGetComponent<GrabbableObject>(out var grabbableObject);
        var enemyAIFound = parent.TryGetComponent<EnemyAI>(out var enemyAI);
        var terminalAccessibleObjectFound = parent.TryGetComponent<TerminalAccessibleObject>(out var terminalAccessibleObject);

        cachedComponents = (grabbableObjectFound? grabbableObject : null,
                            enemyAIFound? enemyAI : null,
                            terminalAccessibleObjectFound? terminalAccessibleObject : null);
    }


    private static IEnumerator AddScanNodeToUI(ScanNodeProperties scanNodeProperties, long currentScanNodeCount) {
        yield return new WaitForSeconds((ConfigManager.scanNodeDelay.Value / 100F) * currentScanNodeCount);
        yield return new WaitForEndOfFrame();

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

        var cosHalfAdjustedFOV = GetCosHalfAdjustedFov(camera);

        if (Vector3.Dot(direction, camera.transform.forward) < cosHalfAdjustedFOV) return false;

        if (!IsScanNodeValid(node)) return false;

        return !ConfigManager.alwaysCheckForLineOfSight.Value || HasLineOfSight(node, localPlayer);
    }

    // I don't think we actually need a dictionary as cache, but just to be sure...
    private static readonly Dictionary<Camera, float> _CachedFovValues = [
    ];

    private static float GetCosHalfAdjustedFov(Camera camera) {
        if (_CachedFovValues.TryGetValue(camera, out var cosHalfAdjustedFOV)) return cosHalfAdjustedFOV;

        var aspectRatio = camera.aspect;

        // This multiplier exists to move the scannable range even closer to the screen's border.
        // Haven't tested this on anything else than 4:3 and 16:9
        var multiplier = 0.5f + aspectRatio / 100;

        var adjustedFOV = Mathf.Atan(Mathf.Tan(camera.fieldOfView * multiplier * Mathf.Deg2Rad) / aspectRatio) * Mathf.Rad2Deg * 2;
        cosHalfAdjustedFOV = Mathf.Cos(adjustedFOV * Mathf.Deg2Rad);

        _CachedFovValues[camera] = cosHalfAdjustedFOV;

        return cosHalfAdjustedFOV;
    }
}