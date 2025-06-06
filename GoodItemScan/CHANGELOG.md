# Good Item Scan Mod

## 1.14.0

\+ Added v72 support<br>

## 1.13.0

\+ Added config option to prevent scanning exploded landmines<br>
\* Fixed scanning collected story logs<br>

## 1.12.0

\+ Added optional distance calculation<br>

## 1.11.1

\* Fixed null-keys not being handled correctly<br>

## 1.11.0

\* Fixed potential issue caused by integer overflow<br>
\# CheatsAPI now resets to default values everytime HUDManager is disabled<br>

## 1.10.0

\+ Added option to update the scan node text after scanning a node<br>

## 1.9.0

\# Made the Scanner non-static. Maybe fixes a few issues?<br>
\# Also added `DontDestroyOnLoad` on all RectTransforms<br>

## 1.8.3

\* Fixed Blast Doors being scannable while power is out<br>

## 1.8.2

\* Fixed me being an idiot<br>

## 1.8.1

\* Fixed weird scrap value shenanigans<br>

## 1.8.0

\+ Added CheatAPI to give mods like LGU control over the scanner<br>
\* Hopefully fixed another NullReferenceException<br>
\# Partially switched to Monomod to prevent other mods from patching the vanilla scan<br>

## 1.7.0

\+ Added distance checks in visibility check<br>
\+ Added configurable timer for updating all scan nodes<br>

## 1.6.0

\+ Added config option for total value update speed<br>

## 1.5.3

\* Fixed visibility check coroutine never executing after death<br>

## 1.5.2

\* Fixed InvalidOperationException<br>

## 1.5.1

\* Fixed re-hosting causing the scanner to break<br>

## 1.5.0

\# This mod now completely bypasses the vanilla scan system. Even the total animation.<br>

## 1.4.7

\+ Added even more null checks<br>
\* Fixed blast doors scannable after taking apparatus<br>
\# Reduced default `Scan Nodes Hard Limit` to `120`<br>
\# Replaced EndOfFrame wait with `null` wait<br>

## 1.4.6

\* Fix silly total value<br>

## 1.4.5

\* Fix the silly stuff<br>

## 1.4.4

\# Some micro-management for performance<br>

## 1.4.3

\* All NullReferenceExceptions should now be fixed<br>

## 1.4.2

\* Fixed scanner breaking by a deleted ScanNode<br>

## 1.4.1

\* Fixed total value resetting to 0 for no reason<br>

## 1.4.0

\+ Added option to hide empty subtext box<br>
\# Replaced vanilla's Update Scan Nodes method<br>

## 1.3.0

\+ Added config option to limit how many scan nodes will be scanned each frame<br>

## 1.2.4

\# Replaced closest point LOS check with corner LOS check - All scrap should now be scannable<br>

## 1.2.3

\+ Added config option to always check for LOS<br>
\# Improved LOS check. Should now be way more reliable and support weird ScanNode sizes and locations<br>

## 1.2.2

\# Performance Update. Hopefully this actually does things.<br>

## 1.2.1

\* Fixed fov calculation<br>

## 1.2.0

\+ Added config option to always force a rescan<br>
\* Fixed total scan value persisting<br>

## 1.1.0

\+ Added config option for scanning opened blast doors<br>

## 1.0.1

\* Fixed held items being scannable (Including enemy held items)<br>
\* Fixed dead enemies being scannable<br>
\* Fixed ScanNode not disappearing when picking up an item<br>
\* Fixed Easter Egg being scannable after exploding<br>

## 1.0.0

\+ Initial Release<br>