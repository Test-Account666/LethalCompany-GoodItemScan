using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using MonoMod.Utils;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoodItemScan;

public class Scanner {
    private static readonly int _ColorNumberAnimatorHash = Animator.StringToHash("colorNumber");
    private static readonly int _DisplayAnimatorHash = Animator.StringToHash("display");
    private static readonly int _ScanAnimatorHash = Animator.StringToHash("scan");
    private Coroutine? _scanCoroutine;
    private Coroutine? _nodeVisibilityCheckCoroutine;

    private readonly HashSet<ScanNodeProperties> _scanNodesToRemove = [
    ];

    private readonly Dictionary<ScanNodeProperties, int> _tempScanNodesHolder = [
    ];

    private readonly Dictionary<ScanNodeProperties, int> _scanNodes = [
    ];

    private readonly List<ScannedNode> _scannedNodes = [
    ];

    private int _scrapScannedAmount;
    private int _scrapScannedValue;

    public void FillInScanNodes(RectTransform originalRect) {
        DisableAllScanElements();
        _scannedNodes.Clear();

        var maxSize = ConfigManager.scanNodesHardLimit.Value;

        for (var index = 0; index < maxSize; index++) {
            var rectTransform = Object.Instantiate(originalRect, originalRect.position, originalRect.rotation, originalRect.parent);

            Object.DontDestroyOnLoad(rectTransform);

            var texts = rectTransform.gameObject.GetComponentsInChildren<TextMeshProUGUI>();

            var header = texts[0];

            var footer = texts[1];

            var subTextBox = footer.transform.parent.Find("SubTextBox").gameObject;

            var scannedNode = new ScannedNode(rectTransform, header, footer, subTextBox, index);

            _scannedNodes.Add(scannedNode);
        }

        _nodeVisibilityCheckCoroutine = null;

        var objectScannerObject = GameObject.Find("Systems/UI/Canvas/ObjectScanner");

        if (objectScannerObject != null && objectScannerObject) objectScannerObject.transform.SetSiblingIndex(3);

        var totalScanInfoObject = GameObject.Find("Systems/UI/Canvas/ObjectScanner/GlobalScanInfo");

        if (totalScanInfoObject != null && totalScanInfoObject) totalScanInfoObject.transform.SetAsLastSibling();
    }

    public void Scan() {
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
        GoodItemScan.LogDebug($"Got '{_componentCache.Count}' nodes in cache!");

        _scanCoroutine = hudManager.StartCoroutine(ScanNodes(localPlayer, foundScanNodes));
    }

    private void ResetScanState(HUDManager hudManager) {
        if (_scanCoroutine != null) hudManager.StopCoroutine(_scanCoroutine);

        if (ConfigManager.alwaysRescan.Value) {
            DisableAllScanElements();
            _scrapScannedAmount = 0;
            _scrapScannedValue = 0;
        }


        hudManager.playerPingingScan = 0.3f;
    }

    public void DisableAllScanElements() {
        foreach (var (_, index) in _scanNodes) {
            var scannedNode = _scannedNodes[index];

            var rectTransform = scannedNode.rectTransform;

            if (rectTransform) rectTransform.gameObject.SetActive(false);
            scannedNode.ScanNodeProperties = null;
        }

        _scanNodes.Clear();

        var hudManager = HUDManager.Instance;

        if (_nodeVisibilityCheckCoroutine != null && hudManager != null) hudManager.StopCoroutine(_nodeVisibilityCheckCoroutine);

        _nodeVisibilityCheckCoroutine = null;
    }

    private IEnumerator ScanNodes(PlayerControllerB localPlayer, ScanNodeProperties?[] scanNodes) {
        yield return null;

        var currentScanNodeCount = 0;

        var processedNodesThisFrame = 0;

        if (ConfigManager.preferClosestNodes.Value)
            scanNodes = scanNodes.Where(node => node != null).Select(node => node!)
                                 .OrderBy(node => Vector3.Distance(localPlayer.transform.position, node.transform.position)).ToArray();


        foreach (var scanNodeProperties in scanNodes) {
            if (processedNodesThisFrame >= ConfigManager.maxScanNodesToProcessPerFrame.Value) {
                yield return null;
                processedNodesThisFrame = 0;
            }

            processedNodesThisFrame += 1;

            if (scanNodeProperties == null) continue;

            var scanNodePosition = scanNodeProperties.transform.position;

            var viewPoint = localPlayer.gameplayCamera.WorldToViewportPoint(scanNodePosition);

            var onScreen = viewPoint is {
                x: >= 0 and <= 1,
                y: >= 0 and <= 1,
                z: > 0,
            };

            if (!onScreen) continue;

            var cameraPosition = localPlayer.gameplayCamera.transform.position;

            var distance = CalculateDistance(viewPoint, scanNodePosition, cameraPosition);

            var maxRange = scanNodeProperties.maxRange
                         + (scanNodeProperties.nodeType == 1? CheatsAPI.additionalEnemyDistance : CheatsAPI.additionalDistance);

            if (maxRange < scanNodeProperties.maxRange) maxRange = int.MaxValue;

            if (distance > maxRange) continue;
            if (distance < scanNodeProperties.minRange) continue;

            if (distance > CheatsAPI.noLineOfSightDistance)
                if (!HasLineOfSight(scanNodeProperties, cameraPosition))
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

    public static float CalculateDistance(Vector3 viewPoint, Vector3 objectPosition, Vector3 cameraPosition) {
        return ConfigManager.calculateDistance.Value switch {
            false => viewPoint.z,
            true => Vector3.Distance(objectPosition, cameraPosition),
        };
    }

    private static bool HasLineOfSight(ScanNodeProperties scanNodeProperties, Vector3 cameraPosition) {
        if (!scanNodeProperties.requiresLineOfSight) return true;

        var hasBoxCollider = TryGetOrAddBoxCollider(scanNodeProperties, out var boxCollider);

        if (!hasBoxCollider) return false;

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

        if (terminalAccessibleObject is not {
                isBigDoor: true,
            }) return true;

        return terminalAccessibleObject is {
            isDoorOpen: false, isPoweredOn: true,
        };
    }

    private readonly Dictionary<GameObject, (GrabbableObject?, EnemyAI?, TerminalAccessibleObject?)> _componentCache = [
    ];

    private bool IsScanNodeValid(ScannedNode scannedNode) {
        var parent = scannedNode.scanNodeParent;

        return IsScanNodeValid(parent);
    }

    private bool IsScanNodeValid(ScanNodeProperties scanNodeProperties) {
        var parent = scanNodeProperties.transform.parent.gameObject;

        return IsScanNodeValid(parent);
    }

    private bool IsScanNodeValid(GameObject parent) {
        if (!parent || parent == null) return false;

        GetComponents(parent, out var cachedComponents);

        var (grabbableObject, enemyAI, terminalAccessibleObject) = cachedComponents;

        return IsScanNodeValid(grabbableObject, enemyAI, terminalAccessibleObject);
    }

    private void GetComponents(GameObject parent, out (GrabbableObject?, EnemyAI?, TerminalAccessibleObject?) cachedComponents) {
        if (!ConfigManager.useDictionaryCache.Value) {
            GetUncachedComponents(parent, out cachedComponents);
            return;
        }

        if (_componentCache.TryGetValue(parent, out cachedComponents)) return;

        GetUncachedComponents(parent, out cachedComponents);
        _componentCache[parent] = cachedComponents;
    }

    private static void GetUncachedComponents(GameObject parent, out (GrabbableObject?, EnemyAI?, TerminalAccessibleObject?) cachedComponents) {
        var grabbableObjectFound = parent.TryGetComponent<GrabbableObject>(out var grabbableObject);
        var enemyAIFound = parent.TryGetComponent<EnemyAI>(out var enemyAI);
        var terminalAccessibleObjectFound = parent.TryGetComponent<TerminalAccessibleObject>(out var terminalAccessibleObject);

        cachedComponents = (grabbableObjectFound? grabbableObject : null,
                            enemyAIFound? enemyAI : null,
                            terminalAccessibleObjectFound? terminalAccessibleObject : null);
    }


    private IEnumerator AddScanNodeToUI(ScanNodeProperties scanNodeProperties, Vector3 viewPoint, int currentScanNodeCount) {
        yield return new WaitForSeconds(ConfigManager.scanNodeDelay.Value / 100F * currentScanNodeCount);
        yield return null;

        var hudManager = HUDManager.Instance;

        if (hudManager == null) yield break;

        if (scanNodeProperties == null) yield break;

        GoodItemScan.LogDebug($"Scanning node '{currentScanNodeCount}'!");

        var scannedNode = AssignNodeToUIElement(scanNodeProperties);
        if (scannedNode == null) yield break;

        scannedNode.viewPoint = viewPoint;

        ActivateScanElement(hudManager, scannedNode);
    }

    public ScannedNode? AssignNodeToUIElement(ScanNodeProperties node) {
        var hudManager = HUDManager.Instance;

        if (hudManager == null) return null;

        if (_scanNodes.ContainsKey(node)) return null;

        ScannedNode? foundScannedNode = null;

        foreach (var scannedNode in _scannedNodes.Where(scannedNode => !scannedNode.hasScanNode)) {
            scannedNode.ScanNodeProperties = node;

            foundScannedNode = scannedNode;

            _scanNodes.Add(node, scannedNode.index);
            break;
        }

        return foundScannedNode;
    }

    public bool IsScanNodeVisible(ScannedNode scannedNode) {
        var node = scannedNode.ScanNodeProperties;

        if (node == null) return false;

        var localPlayer = StartOfRound.Instance.localPlayerController;

        var viewPoint = scannedNode.viewPoint;

        var onScreen = viewPoint is {
            x: >= 0 and <= 1,
            y: >= 0 and <= 1,
            z: > 0,
        };

        if (!onScreen) return false;

        var cameraPosition = localPlayer.gameplayCamera.transform.position;

        var distance = CalculateDistance(viewPoint, node.transform.position, cameraPosition);

        var maxRange = node.maxRange
                     + (node.nodeType == 1? CheatsAPI.additionalEnemyDistance : CheatsAPI.additionalDistance);

        if (maxRange < node.maxRange) maxRange = int.MaxValue;

        if (distance > maxRange) return false;

        if (distance < node.minRange) return false;

        if (!IsScanNodeValid(scannedNode)) return false;

        return !ConfigManager.alwaysCheckForLineOfSight.Value || distance <= CheatsAPI.noLineOfSightDistance || HasLineOfSight(node, cameraPosition);
    }

    private readonly HashSet<ScannedNode> _scanNodesToUpdate = [
    ];


    private float _updateTimer = 0.05F;

    public void UpdateScanNodes() {
        var hudManager = HUDManager.Instance;
        if (hudManager == null) return;

        // Oh lord...
        if (_scanNodesToRemove.Count > 0) {
            foreach (var (scanNodeProperties, index) in _scanNodes) {
                if (scanNodeProperties == null || !scanNodeProperties) continue;
                if (_scanNodesToRemove.Contains(scanNodeProperties)) continue;

                _tempScanNodesHolder.Add(scanNodeProperties, index);
            }


            _scanNodes.Clear();
            _scanNodes.AddRange(_tempScanNodesHolder);

            _tempScanNodesHolder.Clear();
            _scanNodesToRemove.Clear();
        }

        UpdateScrapTotalValue(hudManager);

        if (_scanNodes.Count <= 0) return;

        _updateTimer -= Time.deltaTime;

        if (_updateTimer > 0) return;

        _updateTimer = ConfigManager.updateTimer.Value / 100F;

        var updatingThisFrame = _nodeVisibilityCheckCoroutine == null;


        foreach (var (scanNodeProperties, index) in _scanNodes) {
            var scannedNode = _scannedNodes[index];

            if (scanNodeProperties == null || !scanNodeProperties) {
                HandleMissingNode(scannedNode);
                continue;
            }

            if (updatingThisFrame) _scanNodesToUpdate.Add(scannedNode);

            UpdateScanNodePosition(scannedNode);
        }

        if (!updatingThisFrame || _scanNodesToUpdate.Count <= 0) return;

        _nodeVisibilityCheckCoroutine = HUDManager.Instance.StartCoroutine(UpdateScanNodesRoutine());
    }

    private IEnumerator UpdateScanNodesRoutine() {
        yield return null;

        var processedNodesThisFrame = 0;

        foreach (var scannedNode in _scanNodesToUpdate) {
            if (processedNodesThisFrame >= ConfigManager.maxScanNodesToProcessPerFrame.Value) {
                yield return null;
                processedNodesThisFrame = 0;
            }

            processedNodesThisFrame += 1;

            if (!IsScanNodeVisible(scannedNode)) {
                HandleMissingNode(scannedNode);
                continue;
            }

            if (!ConfigManager.updateScanNodeText.Value) continue;

            var node = scannedNode.ScanNodeProperties;

            if (node == null) continue;

            scannedNode.header.text = node.headerText;
            scannedNode.footer.text = node.subText;

            if (!ConfigManager.hideEmptyScanNodeSubText.Value) continue;
            scannedNode.subTextBox.SetActive(!string.IsNullOrWhiteSpace(node.subText));
        }

        _scanNodesToUpdate.Clear();

        _nodeVisibilityCheckCoroutine = null;
    }

    private void HandleMissingNode(ScannedNode scannedNode) {
        if (scannedNode == null!) return;

        var node = scannedNode.ScanNodeProperties;

        _scanNodesToRemove.Add(node!);

        scannedNode.ScanNodeProperties = null;

        scannedNode.rectTransform.gameObject.SetActive(false);

        if (node == null || node.nodeType != 2 || node.scrapValue <= 0) return;

        --_scrapScannedAmount;

        _scrapScannedValue = Mathf.Clamp(_scrapScannedValue - node.scrapValue, 0, 10000);
    }

    private void ActivateScanElement(HUDManager hudManager, ScannedNode scannedNode) {
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

    private RectTransform _screenRectTransform = null!;

    private void UpdateScanNodePosition(ScannedNode scannedNode) {
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

    private int _scrapScannedValueDisplayed;
    private float _addToDisplayTotalInterval = 0.35F;

    private void UpdateScrapTotalValue(HUDManager hudManager) {
        if (_scrapScannedAmount <= 0 || _scanNodes.Count <= 0) {
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