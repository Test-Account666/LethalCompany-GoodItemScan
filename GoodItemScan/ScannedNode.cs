using TMPro;
using UnityEngine;

namespace GoodItemScan;

public class ScannedNode(RectTransform rectTransform, TextMeshProUGUI header, TextMeshProUGUI footer, GameObject subTextBox, int index) {
    private ScanNodeProperties? _scanNodeProperties;

    public ScanNodeProperties? ScanNodeProperties {
        get => _scanNodeProperties;
        set {
            _scanNodeProperties = value;

            hasScanNode = value != null;

            if (!hasScanNode) return;

            scanNodeParent = value!.transform.parent.gameObject;
        }
    }

    public Vector3 viewPoint;

    public GameObject scanNodeParent = null!;

    public readonly RectTransform rectTransform = rectTransform;
    public readonly TextMeshProUGUI header = header;
    public readonly TextMeshProUGUI footer = footer;
    public readonly GameObject subTextBox = subTextBox;
    public readonly int index = index;

    public bool hasScanNode;
}