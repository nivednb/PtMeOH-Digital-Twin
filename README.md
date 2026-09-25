# Power-to-Methanol Digital Twin

Interactive Unity 6 educational visualization of a complete Power-to-Methanol
(PtM) plant. The application combines a full industrial plant layout, water
treatment and electrolysis, CO₂ capture, recycle-aware methanol synthesis,
continuous process-stream visualization, interactive operating controls,
analytics, reactor/catalyst visualization, warnings, and equipment-focused
views around one shared steady-state process model.

> This is a master's-project educational digital-twin demonstrator. It is not a
> CFD model, a rigorous thermodynamic/kinetic simulator, a plant control system,
> or certified process-safety software.

## Final submission version

- Submission branch: `submission_final`
- Unity: `6000.4.7f1`
- Render pipeline: URP `17.4.0`
- Startup scene: `Assets/Scenes/SampleScene.unity`
- Platform: Windows desktop, windowed and resizable
- Final development build: produced in `Builds/Daylight/` after the final
  September 25 feature and build-fix commits
- Final development cycle included a Windows build and runtime test before the
  completed work was pushed

Build output is intentionally not tracked in Git. The project can be rebuilt
from the Unity editor using **Tools → Power-to-Methanol → Build Windows
Application**.

## Documentation

- [Final project report](docs/FINAL_PROJECT_REPORT.md)
- [Implementation and engineering reference](docs/IMPLEMENTATION_REFERENCE.md)
- [Progress screenshots](docs/progress-screenshots.md)

## Main systems

- Central process model and shared live operating snapshot
- Steady-state CO₂/H₂ recycle-and-purge mass-balance solver with limiting-reactant handling
- Water-treatment unit feeding the electrolyzer
- Electrolyzer, CO₂ capture, compression, synthesis, cooling, separation,
  distillation, recycle, and storage visualization
- Continuous process flow through every active pipe in stream-specific colors
- Liquid, gas, mixed-stream, and two-phase visual states coupled to live process values
- Transparent reactor/catalyst cutaway with contained conversion-dependent bubble visualization
- Daylight UI with Manrope typography, pill navigation, KPI cards, efficiency ring,
  bottom dock, and scrollable right-hand module drawers
- Analytics in a separate movable Windows window, including numbered recorded
  points, constant-condition background curves, traced point curves, and CSV export
- Welcome screen and guided tutorial on launch
- Educational operating warnings and live reactor reaction hover information
- Orbit, pan, zoom, overview, and module-focus camera controls with ground/horizon limits
- Lightweight procedurally generated industrial environment

## Open and run

1. Open Unity Hub.
2. Add this repository folder.
3. Open it with Unity `6000.4.7f1`.
4. Open `Assets/Scenes/SampleScene.unity`.
5. Enter Play Mode.

Camera controls:

| Input | Action |
| --- | --- |
| Arrow keys | Orbit |
| A / D | Pan left / right |
| W / S | Zoom |
| Shift + arrow keys | Cycle module focus |
| Home | Return to plant overview |

## Repository structure

```text
Assets/
|-- Editor/                     # Inventory, validation and Windows build tools
|-- Materials_N/                # Process-stream and equipment materials
|-- Resources/                  # Fonts and build-preserved reactor materials
|-- Scenes/SampleScene.unity    # Integrated plant scene
|-- Scripts_N/                  # Simulation, UI, analytics, flow, reactor, warnings
|-- Settings/                   # URP configuration
|-- Shaders/                    # Runtime flow shader assets
|-- *.fbx                       # Plant equipment, pipe and water-treatment assets
docs/
|-- FINAL_PROJECT_REPORT.md
|-- IMPLEMENTATION_REFERENCE.md
`-- progress-screenshots.md
Packages/
ProjectSettings/
```

Unity-generated folders (`Library`, `Temp`, `Logs`, `UserSettings`, `.vs`,
`obj`) and exported builds are excluded from Git.
