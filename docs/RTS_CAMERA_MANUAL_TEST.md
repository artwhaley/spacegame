# RTS camera manual test

## Setup

1. Open Assets/bobandfriends_modular.unity in Unity.
2. Select RTSCameraRig in the Hierarchy and confirm that its controller references Main Camera and CommandCenter.
3. Confirm the Inspector defaults: 40 degree pitch, 18 initial zoom distance, 8 minimum distance, 36 maximum distance, pan speed 12 at reference distance 18, rotation speed 110 degrees per second, pan acceleration 90, pan deceleration 120, rotation acceleration 1200, rotation deceleration 1600, zoom speed 18, zoom smoothing 0.12 seconds, and recenter smoothing 0.25 seconds.
4. Open the Console, clear existing entries, enter Play mode, and keep the Game view focused while using the keyboard.

## Controls and checks

1. Hold W briefly. The focus rig should pan in the forward direction currently shown by the camera, toward the top of the playfield. Hold S; it should pan the opposite way toward the bottom.
2. Hold A and D separately. They should pan left and right on screen. Release each key and confirm the rig stops quickly.
3. From a repeatable position, pan with W alone for a measured short interval, then repeat with W+D for the same interval. The diagonal should cover approximately the same distance, rather than moving faster.
4. Hold Q and then E. The camera should rotate continuously and smoothly around the colony. After each turn, use W and D to confirm movement follows the updated yaw.
5. Hold R to move closer to the colony, then hold F to move farther away. The camera field of view should stay at 60 degrees while its distance from the rig changes.
6. Keep holding R until zoom stops, then hold F until it stops. The camera should clamp at the configured minimum distance of 8 and maximum distance of 36 without overshooting.
7. During panning, rotation, and zoom, inspect Main Camera under RTSCameraRig. Its local pitch should remain 40 degrees and its local roll should remain zero.
8. Pan and rotate away from CommandCenter, then tap V. The focus should move smoothly back over CommandCenter. Compare before and after: rig yaw and camera distance should be unchanged.
9. Repeat panning at the closest and farthest zoom settings. Movement should remain responsive, slower when close and faster when distant.
10. Use the Console during normal movement, rotation, zoom, and recentering. No new errors or warnings should appear.

## Expected controls

- W / A / S / D: pan relative to the current yaw
- Q / E: rotate left / right around world up
- R / F: zoom in / out
- V: recenter over CommandCenter while preserving yaw and zoom
