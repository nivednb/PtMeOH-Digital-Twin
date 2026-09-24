# Power-to-Methanol Digital Twin

This university group project presents an educational steady-state Power-to-Methanol process model through an interactive Unity application. The dashboard connects process inputs, species flows, recycle, product recovery, storage warnings and one-factor-at-a-time studies.

## Open and rebuild

Use Unity 6000.4.7f1 with Windows Build Support. Open this folder in Unity Hub and allow the first asset import to finish. The startup scene is `Assets/Scenes/SampleScene.unity`. Package versions are pinned in `Packages/manifest.json` and `Packages/packages-lock.json`; the initial import requires access to the Unity package registry.

The project uses URP 17.4.0, Input System 1.19.0, uGUI 2.0.0 and Test Framework 1.6.0.

Run **Tools > Power-to-Methanol > Validate Final Submission** for numerical, scene and CSV checks. Run **Tools > Power-to-Methanol > Build Windows Application** for a Windows x86_64 build. The default output is `Builds/Windows/PtMeOH-DigitalTwin.exe`. Keep the complete player folder together when distributing or launching it.

## Use

Follow the first-launch tutorial or choose **HELP > START TUTORIAL** to replay it. Use OVERVIEW, PLANT PROCESS and REACTOR to inspect and control the plant. Drag the mouse to orbit, Shift-drag to pan and use the wheel to zoom. ANALYTICS contains five automatic OFAT studies. CSV export records the current synthesis-loop state.

## Documentation

- `docs/SCIENTIFIC_VALIDATION.md`: scientific basis, independent equations and model limitations.
- `docs/IMPLEMENTATION_REFERENCE.md`: implementation and operating assumptions.
- `docs/NUMERICAL_RESULTS.md`: numerical sensitivity results.
- `docs/VALIDATION_RESULTS.md`: verification of this source snapshot and Windows build.
- `docs/MANUAL_TEST_CHECKLIST.md`: student checks before submission.
- `docs/REFERENCES_AND_ASSET_PROVENANCE.md`: references, licences and unresolved asset provenance.

The model is not calibrated to an industrial plant. Material-balance closure demonstrates numerical consistency, not experimental predictive accuracy. Energy balances and measured learning effectiveness are outside the project scope. The accompanying academic report contains the project contribution and assistance disclosures.
