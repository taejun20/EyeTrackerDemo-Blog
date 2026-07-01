# Meta Quest Pro Eye Tracking Demo Setup

This project mirrors the FOVE demo using Meta Quest Pro eye tracking.

## What the script already does

`MetaQuestProEyeDemoController` creates at runtime:

- three sphere targets: left, center, right
- green highlight for the currently gazed sphere
- a thin gaze center ray line
- a large dark background wall
- a world-space live label showing gaze ray and blink state

## Manual Unity setup needed

1. Install/import Meta XR SDK / Oculus Integration or the Movement SDK pieces that provide `OVRCameraRig`, `OVRManager`, and `OVREyeGaze`.
2. In the scene, add an `OVRCameraRig`.
3. On `OVRCameraRig`, configure `OVRManager` for Quest Pro eye tracking support and permission/request flow according to Meta docs.
4. Create two child GameObjects under the tracking/camera rig:
   - `Left Eye Gaze`
   - `Right Eye Gaze`
5. Add `OVREyeGaze` to both GameObjects.
6. Set each `OVREyeGaze` component to the matching eye:
   - `Left Eye Gaze` -> Left eye
   - `Right Eye Gaze` -> Right eye
7. Create an empty GameObject named `Meta Quest Pro Eye Demo Controller`.
8. Add `MetaQuestProEyeDemoController` to it.
9. Drag the `Left Eye Gaze` and `Right Eye Gaze` transforms into the controller fields.

If you keep the exact names `Left Eye Gaze` and `Right Eye Gaze`, the script also tries to find them automatically.

## Notes

- The demo computes the center gaze ray by averaging the positions and forward directions of the two `OVREyeGaze` transforms.
- Blink display uses Unity XR `Eyes` open amount if the active XR provider exposes it. If it says `unknown`, the ray demo can still work from `OVREyeGaze` transforms.
- If the label says `Assign Left/Right OVREyeGaze transforms`, the controller cannot find the two gaze GameObjects yet.