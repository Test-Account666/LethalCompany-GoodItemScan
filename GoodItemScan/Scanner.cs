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

    private static readonly HashSet<ScanNodeProperties> _ScanNodesToRemove = [
    ];

    private static readonly Dictionary<ScanNodeProperties, int> _ScanNodes = [
    ];

    private static readonly List<ScannedNode> _ScannedNodes = [
    ];

    private static int _scrapScannedAmount;
    private static int _scrapScannedValue;

    public static void FillInScanNodes(RectTransform originalRect) {
        DisableAllScanElements();
        _ScannedNodes.Clear();

        _CachedFovValues.Clear();

        var maxSize = ConfigManager.scanNodesHardLimit.Value;

        for (var index = 0; index < maxSize; index++) {
            var rectTransform = Object.Instantiate(originalRect, originalRect.position, originalRect.rotation, originalRect.parent);

            var texts = rectTransform.gameObject.GetComponentsInChildren<TextMeshProUGUI>();

            var header = texts[0];

            var footer = texts[1];

            var subTextBox = footer.transform.parent.Find("SubTextBox").gameObject;

            var scannedNode = new ScannedNode(rectTransform, header, footer, subTextBox, index);

            _ScannedNodes.Add(scannedNode);
        }

        _nodeVisibilityCheckCoroutine = null;

        var objectScannerObject = GameObject.Find("Systems/UI/Canvas/ObjectScanner");

        if (objectScannerObject != null && objectScannerObject) objectScannerObject.transform.SetSiblingIndex(3);

        var totalScanInfoObject = GameObject.Find("Systems/UI/Canvas/ObjectScanner/GlobalScanInfo");

        if (totalScanInfoObject != null && totalScanInfoObject) totalScanInfoObject.transform.SetAsLastSibling();
    }

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

        var foundScanNodes = Object.FindObjectsByType<ScanNodeProperties>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        GoodItemScan.LogDebug($"Got '{foundScanNodes.Length}' nodes!");
        GoodItemScan.LogDebug($"Got '{_ComponentCache.Count}' nodes in cache!");

        _scanCoroutine = hudManager.StartCoroutine(ScanNodes(localPlayer, foundScanNodes));
    }

    private static void ResetScanState(HUDManager hudManager) {
        if (_scanCoroutine != null) hudManager.StopCoroutine(_scanCoroutine);

        if (ConfigManager.alwaysRescan.Value) {
            DisableAllScanElements();
            _scrapScannedAmount = 0;
            _scrapScannedValue = 0;
        }


        hudManager.playerPingingScan = 0.3f;
    }

    public static void DisableAllScanElements() {
        foreach (var (_, index) in _ScanNodes) {
            var scannedNode = _ScannedNodes[index];

            var rectTransform = scannedNode.rectTransform;

            if (rectTransform) rectTransform.gameObject.SetActive(false);
            scannedNode.ScanNodeProperties = null;
        }

        _ScanNodes.Clear();

        var hudManager = HUDManager.Instance;

        if (_nodeVisibilityCheckCoroutine != null && hudManager != null) hudManager.StopCoroutine(_nodeVisibilityCheckCoroutine);

        _nodeVisibilityCheckCoroutine = null;
    }

    private static IEnumerator ScanNodes(PlayerControllerB localPlayer, ScanNodeProperties?[] scanNodes) {
        yield return null;

        var currentScanNodeCount = 0;

        var playerLocation = localPlayer.transform.position;

        var processedNodesThisFrame = 0;

        if (ConfigManager.preferClosestNodes.Value)
            scanNodes = scanNodes.Where(node => node != null).Select(node => node!)
                                 .OrderBy(node => Vector3.Distance(playerLocation, node.transform.position)).ToArray();


        foreach (var scanNodeProperties in scanNodes) {
            if (processedNodesThisFrame >= ConfigManager.maxScanNodesToProcessPerFrame.Value) {
                yield return null;
                processedNodesThisFrame = 0;
            }

            processedNodesThisFrame += 1;

            if (scanNodeProperties == null) continue;

            var scanNodePosition = scanNodeProperties.transform.position;

            var viewPoint = GameNetworkManager.Instance.localPlayerController.gameplayCamera.WorldToViewportPoint(scanNodePosition);

            var onScreen = viewPoint is {
                x: >= 0 and <= 1,
                y: >= 0 and <= 1,
            };

            if (!onScreen) continue;

            var distance = viewPoint.z;

            if (distance > scanNodeProperties.maxRange
              + (scanNodeProperties.nodeType == 1? CheatsAPI.additionalEnemyDistance : CheatsAPI.additionalDistance)) continue;
            if (distance < scanNodeProperties.minRange) continue;

            if (distance > CheatsAPI.noLineOfSightDistance)
                if (!HasLineOfSight(scanNodeProperties, localPlayer))
                    continue;

            if (!IsScanNodeValid(scanNodeProperties)) continue;

            currentScanNodeCount += 1;

            if (currentScanNodeCount > ConfigManager.scanNodesHardLimit.Value) {
                GoodItemScan.LogDebug($"Hard Limit of {ConfigManager.scanNodesHardLimit.Value} reached!");
                yield break;
            }

            localPlayer.StartCoroutine(AddScanNodeToUI(scanNodeProperties, viewPoint, currentScanNodeCount));
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
            isBigDoor: true, isDoorOpen: true, isPoweredOn: true,
        };
    }

    private static readonly Dictionary<GameObject, (GrabbableObject?, EnemyAI?, TerminalAccessibleObject?)> _ComponentCache = [
    ];

    private static bool IsScanNodeValid(ScannedNode scannedNode) {
        var parent = scannedNode.scanNodeParent;

        return IsScanNodeValid(parent);
    }

    private static bool IsScanNodeValid(ScanNodeProperties scanNodeProperties) {
        var parent = scanNodeProperties.transform.parent.gameObject;

        return IsScanNodeValid(parent);
    }

    private static bool IsScanNodeValid(GameObject parent) {
        if (!parent || parent == null) return false;

        GetComponents(parent, out var cachedComponents);

        var (grabbableObject, enemyAI, terminalAccessibleObject) = cachedComponents;

        return IsScanNodeValid(grabbableObject, enemyAI, terminalAccessibleObject);
    }

    private static void GetComponents(GameObject parent, out (GrabbableObject?, EnemyAI?, TerminalAccessibleObject?) cachedComponents) {
        if (!ConfigManager.useDictionaryCache.Value) {
            GetUncachedComponents(parent, out cachedComponents);
            return;
        }

        if (_ComponentCache.TryGetValue(parent, out cachedComponents)) return;

        GetUncachedComponents(parent, out cachedComponents);
        _ComponentCache[parent] = cachedComponents;
    }

    private static void GetUncachedComponents(GameObject parent, out (GrabbableObject?, EnemyAI?, TerminalAccessibleObject?) cachedComponents) {
        var grabbableObjectFound = parent.TryGetComponent<GrabbableObject>(out var grabbableObject);
        var enemyAIFound = parent.TryGetComponent<EnemyAI>(out var enemyAI);
        var terminalAccessibleObjectFound = parent.TryGetComponent<TerminalAccessibleObject>(out var terminalAccessibleObject);

        cachedComponents = (grabbableObjectFound? grabbableObject : null,
                            enemyAIFound? enemyAI : null,
                            terminalAccessibleObjectFound? terminalAccessibleObject : null);
    }


    private static IEnumerator AddScanNodeToUI(ScanNodeProperties scanNodeProperties, Vector3 viewPoint, int currentScanNodeCount) {
        yield return new WaitForSeconds(ConfigManager.scanNodeDelay.Value / 100F * currentScanNodeCount);
        yield return null;

        var localPlayer = StartOfRound.Instance.localPlayerController;

        if (localPlayer == null) yield break;

        var hudManager = HUDManager.Instance;

        if (hudManager == null) yield break;

        if (scanNodeProperties == null) yield break;

        GoodItemScan.LogDebug($"Scanning node '{currentScanNodeCount}'!");

        var scannedNode = AssignNodeToUIElement(scanNodeProperties);
        if (scannedNode == null) yield break;

        scannedNode.viewPoint = viewPoint;

        ActivateScanElement(hudManager, scannedNode);
    }

    public static ScannedNode? AssignNodeToUIElement(ScanNodeProperties node) {
        var hudManager = HUDManager.Instance;

        if (hudManager == null) return null;

        if (_ScanNodes.ContainsKey(node)) return null;

        ScannedNode? foundScannedNode = null;

        foreach (var scannedNode in _ScannedNodes.Where(scannedNode => !scannedNode.hasScanNode)) {
            scannedNode.ScanNodeProperties = node;

            foundScannedNode = scannedNode;

            _ScanNodes.Add(node, scannedNode.index);
            break;
        }

        return foundScannedNode;
    }

    public static bool IsScanNodeVisible(ScannedNode scannedNode) {
        var node = scannedNode.ScanNodeProperties;

        if (node == null) return false;

        var localPlayer = StartOfRound.Instance.localPlayerController;

        var viewPoint = scannedNode.viewPoint;

        var onScreen = viewPoint is {
            x: >= 0 and <= 1,
            y: >= 0 and <= 1,
        };

        if (!onScreen) return false;

        var distance = viewPoint.z;

        if (distance > node.maxRange + (node.nodeType == 1? CheatsAPI.additionalEnemyDistance : CheatsAPI.additionalDistance)) return false;

        if (distance < node.minRange) return false;

        if (!IsScanNodeValid(scannedNode)) return false;

        return !ConfigManager.alwaysCheckForLineOfSight.Value || distance <= CheatsAPI.noLineOfSightDistance || HasLineOfSight(node, localPlayer);
    }

    public static bool IsScanNodeOnScreen(ScanNodeProperties node, Vector3 scanNodePosition) {
        if (!node.gameObject.activeSelf) return false;

        var localPlayer = StartOfRound.Instance.localPlayerController;
        if (localPlayer == null) return false;

        var camera = localPlayer.gameplayCamera;

        var direction = scanNodePosition - camera.transform.position;
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

    private static readonly HashSet<ScannedNode> _ScanNodesToUpdate = [
    ];


    private static float _updateTimer = 0.05F;

    public static void UpdateScanNodes() {
        var hudManager = HUDManager.Instance;
        if (hudManager == null) return;

        foreach (var scanNodeProperties in _ScanNodesToRemove) _ScanNodes.Remove(scanNodeProperties);
        _ScanNodesToRemove.Clear();

        UpdateScrapTotalValue(hudManager);

        if (_ScanNodes.Count <= 0) return;

        _updateTimer -= Time.deltaTime;

        if (_updateTimer > 0) return;

        _updateTimer = ConfigManager.updateTimer.Value / 100F;

        var updatingThisFrame = _nodeVisibilityCheckCoroutine == null;


        foreach (var (scanNodeProperties, index) in _ScanNodes) {
            var scannedNode = _ScannedNodes[index];

            if (scanNodeProperties == null || !scanNodeProperties) {
                HandleMissingNode(hudManager, scannedNode);
                continue;
            }

            if (updatingThisFrame) _ScanNodesToUpdate.Add(scannedNode);

            UpdateScanNodePosition(scannedNode);
        }

        if (!updatingThisFrame || _ScanNodesToUpdate.Count <= 0) return;

        _nodeVisibilityCheckCoroutine = HUDManager.Instance.StartCoroutine(UpdateScanNodes(hudManager));
    }

    private static IEnumerator UpdateScanNodes(HUDManager hudManager) {
        yield return null;

        var processedNodesThisFrame = 0;

        foreach (var scannedNode in _ScanNodesToUpdate) {
            if (processedNodesThisFrame >= ConfigManager.maxScanNodesToProcessPerFrame.Value) {
                yield return null;
                processedNodesThisFrame = 0;
            }

            processedNodesThisFrame += 1;

            if (IsScanNodeVisible(scannedNode)) continue;

            HandleMissingNode(hudManager, scannedNode);
        }

        _ScanNodesToUpdate.Clear();

        _nodeVisibilityCheckCoroutine = null;
    }

    private static void HandleMissingNode(HUDManager hudManager, ScannedNode scannedNode) {
        if (scannedNode == null!) return;

        var node = scannedNode.ScanNodeProperties;

        if (node != null) _ScanNodesToRemove.Add(node);

        scannedNode.ScanNodeProperties = null;

        scannedNode.rectTransform.gameObject.SetActive(false);

        if (node == null || node.nodeType != 2 || node.scrapValue <= 0) return;

        --_scrapScannedAmount;

        _scrapScannedValue = Mathf.Clamp(_scrapScannedValue - node.scrapValue, 0, 10000);
    }

    private static void ActivateScanElement(HUDManager hudManager, ScannedNode scannedNode) {
        var node = scannedNode.ScanNodeProperties;

        if (node == null) return;

        if (node is {
                nodeType: 2, scrapValue: > 0,
            }) {
            ++_scrapScannedAmount;
            _scrapScannedValue += node.scrapValue;
        }

        var scanElement = scannedNode.rectTransform;
        if (scanElement.gameObject.activeSelf) return;

        scanElement.gameObject.SetActive(true);

        var hasAnimator = scanElement.TryGetComponent<Animator>(out var animator);
        if (hasAnimator) animator.SetInteger(_ColorNumberAnimatorHash, node.nodeType);

        scannedNode.header.text = node.headerText;
        scannedNode.footer.text = node.subText;

        if (node.creatureScanID != -1) hudManager.AttemptScanNewCreature(node.creatureScanID);

        if (!ConfigManager.hideEmptyScanNodeSubText.Value) return;
        scannedNode.subTextBox.SetActive(!string.IsNullOrWhiteSpace(node.subText));
    }

    private static RectTransform _screenRectTransform = null!;

    private static void UpdateScanNodePosition(ScannedNode scannedNode) {
        var scanElement = scannedNode.rectTransform;
        var node = scannedNode.ScanNodeProperties!;

        if (node == null || !node) return;

        if (!_screenRectTransform) {
            var playerScreen = HUDManager.Instance.playerScreenShakeAnimator.gameObject;
            _screenRectTransform = playerScreen.GetComponent<RectTransform>();
        }

        var rect = _screenRectTransform.rect;

        var scanNodePosition = node.transform.position;

        var viewPoint = GameNetworkManager.Instance.localPlayerController.gameplayCamera.WorldToViewportPoint(scanNodePosition);

        scannedNode.viewPoint = viewPoint;

        var screenPoint = new Vector3(rect.xMin + rect.width * viewPoint.x, rect.yMin + rect.height * viewPoint.y, viewPoint.z);

        scanElement.anchoredPosition = screenPoint;
    }

    private static int _scrapScannedValueDisplayed;
    private static float _addToDisplayTotalInterval = 0.35F;

    private static void UpdateScrapTotalValue(HUDManager hudManager) {
        if (_scrapScannedAmount <= 0 || _ScanNodes.Count <= 0) {
            hudManager.totalValueText.text = "$0";
            _scrapScannedAmount = 0;
            _scrapScannedValue = 0;
            _scrapScannedValueDisplayed = 0;
            _addToDisplayTotalInterval = 0.35f;

            hudManager.scanInfoAnimator.SetBool(_DisplayAnimatorHash, false);
            return;
        }

        hudManager.scanInfoAnimator.SetBool(_DisplayAnimatorHash, _scrapScannedValue != 0);

        const int maxDisplayedValue = 10000;

        if (_scrapScannedValueDisplayed >= _scrapScannedValue || _scrapScannedValueDisplayed >= maxDisplayedValue) return;

        _addToDisplayTotalInterval -= Time.deltaTime;

        if (_addToDisplayTotalInterval > 0) return;

        _addToDisplayTotalInterval = 0.08525F * (ConfigManager.totalAddWaitMultiplier.Value / 100F);

        _scrapScannedValueDisplayed = (int) Mathf.Clamp(Mathf.MoveTowards(_scrapScannedValueDisplayed, _scrapScannedValue,
                                                                          (1500f + (_scrapScannedValue * ConfigManager.totalAddWaitMultiplier.Value
                                                                                  / 100F)) * Time.deltaTime), 0f, maxDisplayedValue);
        hudManager.totalValueText.text = $"${_scrapScannedValueDisplayed}";

        if (_scrapScannedValueDisplayed < _scrapScannedValue && _scrapScannedValueDisplayed < maxDisplayedValue)
            hudManager.UIAudio.PlayOneShot(hudManager.addToScrapTotalSFX);
        else
            hudManager.UIAudio.PlayOneShot(hudManager.finishAddingToTotalSFX);
    }
}