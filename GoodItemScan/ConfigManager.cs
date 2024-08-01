using BepInEx.Configuration;

namespace GoodItemScan;

public static class ConfigManager {
    public static ConfigEntry<bool> preferClosestNodes = null!;
    public static ConfigEntry<bool> alwaysRescan = null!;

    public static ConfigEntry<int> scanNodesHardLimit = null!;
    public static ConfigEntry<float> scanNodeDelay = null!;

    public static ConfigEntry<bool> useDictionaryCache = null!;

    public static ConfigEntry<bool> alwaysCheckForLineOfSight = null!;

    public static ConfigEntry<int> maxScanNodesToProcessPerFrame = null!;

    public static ConfigEntry<bool> sendDebugMessages = null!;

    public static ConfigEntry<bool> showOpenedBlastDoorScanNode = null!;

    public static ConfigEntry<bool> addBoxCollidersToInvalidScanNodes = null!;

    public static ConfigEntry<bool> hideEmptyScanNodeSubText = null!;

    public static ConfigEntry<int> totalAddWaitMultiplier = null!;


    internal static void Initialize(ConfigFile configFile) {
        preferClosestNodes = configFile.Bind("General", "Prefer Closest Nodes", true,
                                             "If true, will prefer scanning the closest nodes first. "
                                           + "This might cause performance issues.");

        alwaysRescan = configFile.Bind("General", "Always Rescan", true,
                                       "If true, will always start a fresh scan. "
                                     + "This removes all previously scanned nodes from the UI.");

        scanNodesHardLimit = configFile.Bind("General", "Scan Nodes Hard Limit", 120,
                                             new ConfigDescription("Defines the maximum amount of scan nodes on screen. "
                                                                 + "If you feel like your screen is cluttered, try lowering this value.",
                                                                   new AcceptableValueRange<int>(30, 666)));

        scanNodeDelay = configFile.Bind("General", "Scan Node Delay", 0.1F,
                                        new ConfigDescription("Defines the delay between each scan node being added to the UI. "
                                                            + "This will look stupid if set too high. "
                                                            + "This value is divided by 100.",
                                                              new AcceptableValueRange<float>(0, 1F)));

        useDictionaryCache = configFile.Bind("General", "Use Dictionary Cache", true,
                                             "May increase performance, at the cost of ram usage. "
                                           + "If true, will use a dictionary for caching. "
                                           + "If false, will not cache at all.");

        alwaysCheckForLineOfSight = configFile.Bind("General", "Always Check For Line Of Sight", false,
                                                    "If true, will check for line of sight every frame. "
                                                  + "Enabling this could cause performance issues. Vanilla value is true.");

        maxScanNodesToProcessPerFrame = configFile.Bind("General", "Max Scan Nodes To Process Per Frame", 32,
                                                        new ConfigDescription("This value defines how many scan nodes can be processed each frame."
                                                                            + "This value is used in updating and scanning nodes!",
                                                                              new AcceptableValueRange<int>(1, 666)));

        sendDebugMessages = configFile.Bind("Debug", "Send Debug Messages", false,
                                            "If set to true, will spam your log with debug messages.");

        showOpenedBlastDoorScanNode = configFile.Bind("Special Cases", "Show opened blast door scan node", true,
                                                      "If set to true, will allow you to scan nodes of opened blast doors (Vanilla value: false) "
                                                    + "Enabling this could improve performance.");

        addBoxCollidersToInvalidScanNodes = configFile.Bind("Special Cases", "Add BoxColliders To Invalid Scan Nodes", false,
                                                            "If true, will add BoxColliers to ScanNodes that do not have one. "
                                                          + "It is not recommended to enable this. "
                                                          + "This feature was also never tested, so it might not even work.");

        hideEmptyScanNodeSubText = configFile.Bind("Special Cases", "Hide Empty Scan Node Sub Text", true,
                                                   "I true, will hide the rectangle beneath the item name, if there's no text to be displayed.");

        totalAddWaitMultiplier = configFile.Bind("Special Cases", "Total Add Wait Multiplier", 100,
                                                 new ConfigDescription(
                                                     "This multiplier is used to define the wait time between adding more to the total displayed value. "
                                                   + "The lower this number, the faster the animation is updated."
                                                   + "The lower this number, the less will be added per updated."
                                                   + "For a vanilla-ish feeling, set this to 48.",
                                                     new AcceptableValueRange<int>(1, 100)));
    }
}