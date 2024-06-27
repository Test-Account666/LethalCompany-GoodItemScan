using BepInEx.Configuration;

namespace GoodItemScan;

public static class ConfigManager {
    public static ConfigEntry<bool> preferClosestNodes = null!;

    public static ConfigEntry<int> scanNodesHardLimit = null!;
    public static ConfigEntry<float> scanNodeDelay = null!;

    public static ConfigEntry<bool> sendDebugMessages = null!;

    internal static void Initialize(ConfigFile configFile) {
        preferClosestNodes = configFile.Bind("General", "Prefer Closest Nodes", true,
                                             "If true, will prefer scanning the closest nodes first. "
                                           + "This might cause performance issues.");

        scanNodesHardLimit = configFile.Bind("General", "Scan Nodes Hard Limit", 666,
                                             new ConfigDescription("Defines the maximum amount of scan nodes on screen. "
                                                                 + "If you feel like your screen is cluttered, try lowering this value.",
                                                                   new AcceptableValueRange<int>(30, 666)));

        scanNodeDelay = configFile.Bind("General", "Scan Node Delay", 0.1F,
                                        new ConfigDescription("Defines the delay between each scan node being added to the UI. "
                                                            + "This will look stupid if set too high. "
                                                            + "This value is divided by 100.",
                                                              new AcceptableValueRange<float>(0, 1F)));

        sendDebugMessages = configFile.Bind("Debug", "Send Debug Messages", false,
                                            "If set to true, will spam your log with debug messages.");
    }
}