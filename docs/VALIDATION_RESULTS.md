# Snapshot validation results

Source branch: final-submission, revision f90277903d6edae0284b4bbe212bc1706a3ba106. The application matches frozen source 9e3ea9c192d506e33e4227586efdd5cd5da994bd. The source package omits development records and caches. An obsolete last-project path was cleared from the vendor editor-layout metadata; runtime code, scene behaviour and equations were unchanged. Unity normalized line endings in several serialized files.

Validation executed on 24 September 2026 using Unity 6000.4.7f1. The independent source snapshot started without a Library cache. Startup scene: Assets/Scenes/SampleScene.unity.

| Check | New result |
|---|---|
| Numerical regression | 1,586 assertions passed |
| Scene | 480 objects, 35 required route segments; no missing scripts reported |
| Editor CSV export | Passed; external closure error 1.13699e-09% |
| Independent current-state CSV | 25% recycle, 240 C; product matches snapshot within 0.001 kg/h |
| Rounded exported stream residual | 0.0002 kg/h (0.000015773449105191949%); rounding is distinct from full-precision closure |
| Windows x86_64 build | Succeeded; 0 errors, 0 warnings |
| 1920 x 1080 player | 177 checks passed, 0 failed; short stability period 60.02468 seconds |
| 1280 x 720 player | 177 checks passed, 0 failed; short stability period 30.05698 seconds |
| Player CSV closure | 4.25878532E-09% at both resolutions |
| Runtime errors/exceptions | 0 observed in both player runs |

The visible-player suite exercised tutorial initialization, NEXT/PREVIOUS/SKIP, HELP replay, warning arrow/spotlight, synthetic camera input through Unity's input system, Process Map layout, dashboard state restoration and five automatic OFAT sweeps with 13 points each without live-state mutation. These automated checks are not physical mouse/keyboard or comprehensive visual sign-off. A new 15-minute soak was not performed for this package.

Initial import produced two transient ShaderGraph package-cache GUID compiler diagnostics. Unity's API Updater resolved them; compilation and validators subsequently succeeded and the editor exited with code 0. No application fix or package upgrade was made.

Each player run logged one URP warning that punctual-light shadow resolution was reduced by 2 to fit six maps in the 2048 x 2048 atlas. This is a runtime rendering warning, separate from the zero build-warning count.

The model remains an uncalibrated educational steady-state model. Complete the manual checklist and confirm asset permissions and university report requirements before submission.
