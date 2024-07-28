using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoodItemScan;

public static class Scanner {
    private static readonly int _ColorNumberAnimatorHash = Animator.StringToHash("colorNumber");
    private static readonly int _DisplayAnimatorHash = Animator.StringToHash("display");
    private static readonly int _ScanAnimatorHash = Animator.StringToHash("scan");
    private static Coroutine? _scanCoroutine;
    private static Coroutine? _nodeVisibilityCheckCoroutine;

    public static void Scan() {
        var localPlayer = StartOfRound.Instance.localPlayerController;

        if (localPlayer == null) return;

        var hudManager = HUDManager.Instance;

        if (!hudManager.CanPlayerScan() || hudManager.playerPingingScan > -1.0) return;

        ResetScanState(hudManager);

        hudManager.scanEffectAnimator.transform.position = localPlayer.gameplayCamera.transform.position;
        hudManager.scanEffectAnimator.SetTrigger(_ScanAnimatorHash);
        hudManager.UIAudio.PlayOneShot(hudManager.scanSFX);

        GoodItemScan.LogDebug("Scan Initiated!");

        var scanNodes = Object.FindObjectsByType<ScanNodeProperties>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        GoodItemScan.LogDebug($"Got '{scanNodes.Length}' nodes!");
        GoodItemScan.LogDebug($"Got '{_ComponentCache.Count}' nodes in cache!");

        _scanCoroutine = hudManager.StartCoroutine(ScanNodes(localPlayer, scanNodes));
    }

    private static void ResetScanState(HUDManager hudManager) {
        if (_scanCoroutine != null) hudManager.StopCoroutine(_scanCoroutine);

        if (ConfigManager.alwaysRescan.Value) {
            hudManager.DisableAllScanElements();
            hudManager.scanNodes.Clear();
            hudManager.scannedScrapNum = 0;
            hudManager.totalScrapScanned = 0;
            hudManager.totalScrapScannedDisplayNum = 0;
        }


        hudManager.playerPingingScan = 0.3f;
    }

    private static IEnumerator ScanNodes(PlayerControllerB localPlayer, ScanNodeProperties?[] scanNodes) {
        yield return new WaitForEndOfFrame();

        var currentScanNodeCount = 0L;

        var playerLocation = localPlayer.transform.position;

        var processedNodesThisFrame = 0;

        if (ConfigManager.preferClosestNodes.Value)
            scanNodes = scanNodes.Where(node => node != null).Select(node => node!)
                                 .OrderBy(node => Vector3.Distance(playerLocation, node.transform.position)).ToArray();


        foreach (var scanNodeProperties in scanNodes) {
            if (processedNodesThisFrame >= ConfigManager.maxScanNodesToProcessPerFrame.Value) {
                yield return null;
                yield return new WaitForEndOfFrame();
                processedNodesThisFrame = 0;
            }

            processedNodesThisFrame += 1;

            if (scanNodeProperties == null) continue;

            var scanNodePosition = scanNodeProperties.transform.position;

            var distance = Vector3.Distance(scanNodePosition, playerLocation);

            if (distance > scanNodeProperties.maxRange) continue;

            if (distance < scanNodeProperties.minRange) continue;

            if (!IsScanNodeOnScreen(scanNodeProperties)) continue;

            if (!HasLineOfSight(scanNodeProperties, localPlayer)) continue;

            if (!IsScanNodeValid(scanNodeProperties)) continue;

            currentScanNodeCount += 1;

            if (currentScanNodeCount > ConfigManager.scanNodesHardLimit.Value) {
                GoodItemScan.LogDebug($"Hard Limit of {ConfigManager.scanNodesHardLimit.Value} reached!");
                yield break;
            }

            localPlayer.StartCoroutine(AddScanNodeToUI(scanNodeProperties, currentScanNodeCount));
        }
    }

    private static bool HasLineOfSight(ScanNodeProperties scanNodeProperties, PlayerControllerB localPlayer) {
        if (!scanNodeProperties.requiresLineOfSight) return true;

        var hasBoxCollider = TryGetOrAddBoxCollider(scanNodeProperties, out var boxCollider);

        if (!hasBoxCollider) return false;

        var cameraPosition = localPlayer.gameplayCamera.transform.position;

        var minPosition = boxCollider.bounds.min;

        var maxPosition = boxCollider.bounds.max;

        var corners = new[] {
            maxPosition, new(maxPosition.x, maxPosition.y, minPosition.z), new(maxPosition.x, minPosition.y, maxPosition.z),
            new(maxPosition.x, minPosition.y, minPosition.z), new(minPosition.x, maxPosition.y, maxPosition.z),
            new(minPosition.x, maxPosition.y, minPosition.z), new(minPosition.x, minPosition.y, maxPosition.z), minPosition,
        };

        return corners.Any(corner => !Physics.Linecast(cameraPosition, corner, 256, QueryTriggerInteraction.Ignore));
    }

    private static bool TryGetOrAddBoxCollider(ScanNodeProperties scanNodeProperties, out BoxCollider boxCollider) {
        var hasBoxCollider = scanNodeProperties.TryGetComponent(out boxCollider);
        if (hasBoxCollider) return true;

        GoodItemScan.Logger.LogError($"{scanNodeProperties.headerText} has no BoxCollider!");

        if (!ConfigManager.addBoxCollidersToInvalidScanNodes.Value) return false;

        GoodItemScan.Logger.LogError("Adding a BoxCollider!");
        boxCollider = scanNodeProperties.gameObject.AddComponent<BoxCollider>();
        return true;
    }

    private static bool IsScanNodeValid(GrabbableObject? grabbableObject, EnemyAI? enemyAI, TerminalAccessibleObject? terminalAccessibleObject) {
        if (grabbableObject != null && (grabbableObject.isHeld || grabbableObject.isHeldByEnemy || grabbableObject.deactivated)) return false;

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

        if (parent == null) return false;

        GetComponents(parent, out var cachedComponents);

        var (grabbableObject, enemyAI, terminalAccessibleObject) = cachedComponents;

        return IsScanNodeValid(grabbableObject, enemyAI, terminalAccessibleObject);
    }

    private static void GetComponents(Transform parent, out (GrabbableObject?, EnemyAI?, TerminalAccessibleObject?) cachedComponents) {
        if (!ConfigManager.useDictionaryCache.Value) {
            GetUncachedComponents(parent, out cachedComponents);
            return;
        }

        if (_ComponentCache.TryGetValue(parent, out cachedComponents)) return;

        GetUncachedComponents(parent, out cachedComponents);
        _ComponentCache[parent] = cachedComponents;
    }

    private static void GetUncachedComponents(Transform parent, out (GrabbableObject?, EnemyAI?, TerminalAccessibleObject?) cachedComponents) {
        var grabbableObjectFound = parent.TryGetComponent<GrabbableObject>(out var grabbableObject);
        var enemyAIFound = parent.TryGetComponent<EnemyAI>(out var enemyAI);
        var terminalAccessibleObjectFound = parent.TryGetComponent<TerminalAccessibleObject>(out var terminalAccessibleObject);

        cachedComponents = (grabbableObjectFound? grabbableObject : null,
                            enemyAIFound? enemyAI : null,
                            terminalAccessibleObjectFound? terminalAccessibleObject : null);
    }


    private static IEnumerator AddScanNodeToUI(ScanNodeProperties scanNodeProperties, long currentScanNodeCount) {
        yield return new WaitForSeconds(ConfigManager.scanNodeDelay.Value / 100F * currentScanNodeCount);
        yield return new WaitForEndOfFrame();

        var localPlayer = StartOfRound.Instance.localPlayerController;

        if (localPlayer == null) yield break;

        var hudManager = HUDManager.Instance;

        if (hudManager == null) yield break;

        GoodItemScan.LogDebug($"Scanning node '{currentScanNodeCount}'!");

        if (scanNodeProperties.nodeType == 2) ++hudManager.scannedScrapNum;
        if (!hudManager.nodesOnScreen.Contains(scanNodeProperties)) hudManager.nodesOnScreen.Add(scanNodeProperties);

        var elementIndex = AssignNodeToUIElement(scanNodeProperties);

        if (elementIndex == -1) yield break;

        ActivateScanElement(hudManager, elementIndex, scanNodeProperties);
    }

    public static int AssignNodeToUIElement(ScanNodeProperties node) {
        var hudManager = HUDManager.Instance;

        if (hudManager == null) return -1;

        hudManager.AssignNodeToUIElement(node);

        if (hudManager.scanNodes.ContainsValue(node)) return -1;

        var elementIndex = -1;

        for (var index = 0; index < hudManager.scanElements.Length; ++index) {
            if (!hudManager.scanNodes.TryAdd(hudManager.scanElements[index], node)) continue;

            elementIndex = index;

            if (node.nodeType != 2) break;

            hudManager.totalScrapScanned += node.scrapValue;
            hudManager.addedToScrapCounterThisFrame = true;
            break;
        }

        return elementIndex;
    }

    public static bool IsScanNodeVisible(ScanNodeProperties node) {
        if (node == null) return false;

        if (!IsScanNodeOnScreen(node)) return false;

        if (!IsScanNodeValid(node)) return false;

        var localPlayer = StartOfRound.Instance.localPlayerController;

        return !ConfigManager.alwaysCheckForLineOfSight.Value || HasLineOfSight(node, localPlayer);
    }

    public static bool IsScanNodeOnScreen(ScanNodeProperties node) {
        if (!node.gameObject.activeSelf) return false;

        var localPlayer = StartOfRound.Instance.localPlayerController;
        if (localPlayer == null) return false;

        var camera = localPlayer.gameplayCamera;

        var direction = node.transform.position - camera.transform.position;
        direction.Normalize();

        var cosHalfAdjustedFOV = GetCosHalfAdjustedFov(camera);

        return Vector3.Dot(direction, camera.transform.forward) >= cosHalfAdjustedFOV;
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

    private static readonly HashSet<(ScanNodeProperties, RectTransform)> _ScanNodesToUpdate = [
    ];

    public static void UpdateScanNodes(PlayerControllerB playerScript) {
        var hudManager = HUDManager.Instance;
        if (hudManager == null) return;

        UpdateScrapTotalValue(hudManager);

        var nodesToProcess = hudManager.scanNodes.Count;

        if (nodesToProcess <= 0) return;

        var processed = 0;

        var updatingThisFrame = _nodeVisibilityCheckCoroutine == null;

        foreach (var scanElement in hudManager.scanElements) {
            if (processed >= nodesToProcess) break;

            var foundNode = hudManager.scanNodes.TryGetValue(scanElement, out var node);

            if (hudManager.scanNodes.Count <= 0 || !foundNode) {
                HandleMissingNode(hudManager, scanElement, foundNode, node);
                continue;
            }

            processed += 1;

            if (node == null) continue;

            if (updatingThisFrame) _ScanNodesToUpdate.Add((node, scanElement));

            UpdateScanNodePosition(scanElement, node, playerScript);
        }

        if (!updatingThisFrame || _ScanNodesToUpdate.Count <= 0) return;

        _nodeVisibilityCheckCoroutine = HUDManager.Instance.StartCoroutine(UpdateScanNodes(hudManager, _ScanNodesToUpdate));
    }

    private static IEnumerator UpdateScanNodes(HUDManager hudManager, HashSet<(ScanNodeProperties, RectTransform)> scanNodesToUpdate) {
        yield return new WaitForEndOfFrame();

        var processedNodesThisFrame = 0;

        foreach (var (node, scanElement) in scanNodesToUpdate) {
            if (processedNodesThisFrame >= ConfigManager.maxScanNodesToProcessPerFrame.Value) {
                yield return null;
                yield return new WaitForEndOfFrame();
                processedNodesThisFrame = 0;
            }

            processedNodesThisFrame += 1;

            if (IsScanNodeVisible(node)) continue;

            HandleMissingNode(hudManager, scanElement, true, node);
        }

        _ScanNodesToUpdate.Clear();

        _nodeVisibilityCheckCoroutine = null;
    }

    private static void HandleMissingNode(HUDManager hudManager, RectTransform scanElement, bool foundNode, ScanNodeProperties? node) {
        hudManager.scanNodes.Remove(scanElement);
        scanElement.gameObject.SetActive(false);

        if (!foundNode || node == null || node.nodeType != 2) return;

        --hudManager.scannedScrapNum;

        hudManager.totalScrapScanned = Mathf.Clamp(hudManager.totalScrapScanned - node.scrapValue, 0, 100000);
    }

    private static void ActivateScanElement(HUDManager hudManager, int elementIndex, ScanNodeProperties node) {
        var scanElement = hudManager.scanElements[elementIndex];
        if (scanElement.gameObject.activeSelf) return;

        scanElement.gameObject.SetActive(true);

        var hasAnimator = scanElement.TryGetComponent<Animator>(out var animator);
        if (hasAnimator) animator.SetInteger(_ColorNumberAnimatorHash, node.nodeType);

        hudManager.scanElementText = scanElement.gameObject.GetComponentsInChildren<TextMeshProUGUI>();
        if (hudManager.scanElementText.Length > 1) {
            hudManager.scanElementText[0].text = node.headerText;
            hudManager.scanElementText[1].text = node.subText;
        }

        if (node.creatureScanID != -1) hudManager.AttemptScanNewCreature(node.creatureScanID);

        if (!ConfigManager.hideEmptyScanNodeSubText.Value) return;
        hudManager.scanElementText[1].transform.parent.Find("SubTextBox").gameObject.SetActive(!string.IsNullOrWhiteSpace(node.subText));
    }

    private static void UpdateScanNodePosition(RectTransform scanElement, ScanNodeProperties node, PlayerControllerB playerScript) {
        var screenPoint = playerScript.gameplayCamera.WorldToScreenPoint(node.transform.position);
        const float offsetX = 439.48f;
        const float offsetY = 244.8f;
        scanElement.anchoredPosition = new(screenPoint.x - offsetX, screenPoint.y - offsetY);
    }

    private static void UpdateScrapTotalValue(HUDManager hudManager) {
        if (hudManager.scannedScrapNum <= 0 || hudManager.scanNodes.Count <= 0) {
            hudManager.totalScrapScanned = 0;
            hudManager.totalScrapScannedDisplayNum = 0;
            hudManager.addToDisplayTotalInterval = 0.35f;
        }

        hudManager.scanInfoAnimator.SetBool(_DisplayAnimatorHash, hudManager.scannedScrapNum > 1 && hudManager.scanNodes.Count > 1);
    }
}