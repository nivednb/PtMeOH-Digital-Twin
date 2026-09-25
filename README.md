# Power-to-Methanol Digital Twin

This project is an interactive Unity 6 visualization of a complete
Power-to-Methanol (PtM) plant. The aim was to connect the 3D plant with a
simplified process model so that changing an operating parameter also changes
the values and visual flow seen in the application.

The final application includes water treatment and electrolysis, CO₂ capture,
compression, methanol synthesis, cooling and separation, recycle,
distillation, storage, reactor visualization, warnings, analytics and
equipment-level controls.

> The project was developed as an educational master's project. The process
> calculations are simplified and are intended to show process relationships
> and behaviour. They should not be treated as a replacement for CFD, a
> rigorous process simulator, plant-control software or process-safety
> calculations.

## Submission version

- Branch: `submission_final`
- Unity version: `6000.4.7f1`
- Render pipeline: URP `17.4.0`
- Main scene: `Assets/Scenes/SampleScene.unity`
- Platform: Windows desktop
- Final Windows build folder: `Builds/Daylight/`

The final development cycle included the reactor-flow fix, water-treatment
integration, analytics updates, camera constraints and the final UI/build
fixes. A Windows build and runtime check were completed before the final
project state was pushed.

Build files are not tracked in Git. A new Windows build can be created from
Unity using **Tools → Power-to-Methanol → Build Windows Application**.

## Documentation

- [Final project report](docs/FINAL_PROJECT_REPORT.md)
- [Implementation and engineering reference](docs/IMPLEMENTATION_REFERENCE.md)
- [Development screenshots](docs/progress-screenshots.md)

## Main features

- Full 3D PtM plant with the major process units and pipe network
- One shared steady-state process model used by the dashboard and visual systems
- CO₂/H₂ recycle-and-purge calculation with limiting-reactant handling
- Water-treatment unit connected to the electrolyzer
- Continuous flow through active pipes using stream-specific colours
- Separate visual treatment for gas, liquid, mixed and two-phase streams
- Transparent reactor and catalyst bed with contained internal flow
- Operating controls for the main process units
- Live KPIs, warnings and equipment information
- Analytics window with recorded points, curves and CSV export
- Welcome screen and tutorial
- Orbit, pan, zoom, overview and module-focus camera controls
- Lightweight industrial surroundings around the plant

## Open and run

1. Open Unity Hub.
2. Add this repository folder.
3. Open the project with Unity `6000.4.7f1`.
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
|-- Editor/                     # Validation and Windows build tools
|-- Materials_N/                # Process-stream and equipment materials
|-- Resources/                  # Fonts and reactor materials used in builds
|-- Scenes/SampleScene.unity    # Integrated plant scene
|-- Scripts_N/                  # Simulation, UI, analytics, flow and warnings
|-- Settings/                   # URP configuration
|-- Shaders/                    # Runtime flow shader assets
|-- *.fbx                       # Plant, pipe and water-treatment assets
docs/
|-- FINAL_PROJECT_REPORT.md
|-- IMPLEMENTATION_REFERENCE.md
`-- progress-screenshots.md
Packages/
ProjectSettings/
```

Unity-generated folders such as `Library`, `Temp`, `Logs`,
`UserSettings`, `.vs`, `obj` and exported builds are excluded from Git.
