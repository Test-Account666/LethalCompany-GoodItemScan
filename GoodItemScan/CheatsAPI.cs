using UnityEngine;

namespace GoodItemScan;

[Tooltip("This class is for mods like LGU that want to modify the scanner")]
public static class CheatsAPI {
    public static int additionalDistance = 0;
    public static int additionalEnemyDistance = 0;
    public static int noLineOfSightDistance = 0;
}