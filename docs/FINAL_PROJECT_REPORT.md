# Power-to-Methanol Digital Twin — Final Technical Report

## 1. Project overview

The purpose of this project was to develop an interactive visualization of a
complete Power-to-Methanol plant in Unity. Instead of using the 3D plant only
as a static model, the project connects it to a simplified process simulation.
This makes it possible to change operating parameters and directly see the
effect on process values, pipe flow, reactor behaviour, warnings and dashboard
outputs.

The process represented in the application follows the main PtM chain:

1. water treatment and demineralized-water supply;
2. water electrolysis and hydrogen production;
3. CO2 capture using an amine-based representation;
4. feed compression and mixing;
5. catalytic methanol synthesis with recycle and purge;
6. cooling, condensation and flash separation;
7. gas recycle and methanol purification; and
8. methanol storage.

The application was also designed to run on the development laptop, so the
simulation and visual effects were kept lightweight. One central process
snapshot is used by the dashboard, pipe-flow system, warnings, reactor visual
and catalyst colour. As a result, the different parts of the application react
to the same process state rather than working as separate animations.

## 2. Project scope

The final application contains a complete 3D plant scene with the major PtM
equipment and connecting pipe network. It includes operating controls, process
values, continuous flow visualization, a reactor cutaway, warnings, analytics,
camera navigation, water treatment and a lightweight industrial environment.

The process model is intended for education and visualization. It uses
steady-state mass balances and empirical response functions to show how the
main operating parameters influence the process. It is not intended to replace
a CFD model or a rigorous simulator such as Aspen Plus, Aspen HYSYS or DWSIM,
and it is not connected to real plant or PLC data. The warning limits in the
application are also used for demonstration and should not be treated as
certified plant safety limits.

This limitation is shown in the application itself as:

**EDUCATIONAL VISUALIZATION • SIMPLIFIED PROCESS VALUES**

## 3. Development environment

| Area | Implementation |
| --- | --- |
| Engine | Unity `6000.4.7f1` |
| Rendering | Universal Render Pipeline `17.4.0` |
| Programming | C# MonoBehaviours and a custom ShaderLab shader |
| Input | Unity Input System `1.19.0` |
| UI | Unity uGUI and TextMesh Pro |
| Platform | Windows desktop |
| Main scene | `Assets/Scenes/SampleScene.unity` |
| Version control | Git / GitHub |
| Submission branch | `submission_final` |

Runtime scripts compile into `Assembly-CSharp`, while the scripts under
`Assets/Editor` compile into the Unity editor assembly.

## 4. Process model

### 4.1 Electrolysis

The electrolyzer uses water availability and electrical load to calculate the
hydrogen production rate. Hydrogen production is therefore limited by both the
selected power input and the available feed water.

The oxygen by-product is calculated from the water-splitting mass ratio:

`m(O2) = 8 × m(H2)`

The design reference used in the project is 215 kg/h H2 and 1,935 kg/h water
at full design conditions.

### 4.2 CO2 capture

The CO2 capture section uses flue-gas throughput, amine circulation,
regeneration temperature and steam input. These inputs are combined in an
empirical capture-efficiency function.

The purpose of this part of the model is to reproduce the expected operating
trend: increasing solvent circulation generally improves capture, while poor
regeneration conditions reduce it. Very high regeneration temperature also
triggers a warning.

Detailed column stages, solvent degradation, mass-transfer coefficients and
rigorous vapour-liquid equilibrium are outside the scope of the project.

### 4.3 Feed preparation and methanol synthesis

Captured CO2, electrolytic H2 and recycled gas enter the synthesis-feed
network as separate species. The main reaction represented in the model is:

`CO2 + 3 H2 → CH3OH + H2O`

The mass basis used by the simulator is:

`6 kg H2 + 44 kg CO2 → 32 kg CH3OH + 18 kg H2O`

The theoretical methanol production is therefore limited by whichever reactant
is available in the smaller stoichiometric amount.

The displayed reactor yield is the single-pass CO2 conversion. Reactor
temperature has its best response around 240 °C, while pressure, H2/CO2 ratio
and GHSV also influence the calculated conversion. The temperature response
was made asymmetric so that operation below and above the optimum does not
produce the same behaviour.

After the single-pass conversion is calculated, the unreacted H2 and CO2 are
sent through a recycle/purge calculation. The solver repeatedly updates the
recycle stream until the change becomes sufficiently small and a steady value
is reached. It also checks which reactant is limiting.

The detailed equations and default values used in these calculations are
listed in [IMPLEMENTATION_REFERENCE.md](IMPLEMENTATION_REFERENCE.md).

### 4.4 Cooling, separation and purification

Cooling-water flow and temperature affect the condenser recovery. Separator
temperature is most favourable near the 34 °C reference used in the model.

Part of the unreacted synthesis gas is returned to the reactor feed through the
recycle loop. The remaining condensed product continues to the purification
section.

Reflux ratio and reboiler temperature affect the simplified distillation
recovery and methanol purity. Product water is calculated from the reaction
mass ratio:

`water rate = methanol rate × 18 / 32`

These calculations are simplified and are used to connect the operating
controls with the plant visualization rather than reproduce a full
thermodynamic separation model.

## 5. Application architecture

### 5.1 Shared process state

`PlantProcessSimulator` is the central process component. It receives the
operating inputs, recalculates the process and stores the latest
`PlantProcessSnapshot`.

```text
UI controls
    ↓
PlantProcessSimulator
    ↓
PlantProcessSnapshot
    ├── dashboard values
    ├── equipment values
    ├── warning states
    ├── pipe flow
    ├── reactor activity
    └── catalyst colour
```

This was important during integration because the visual systems had
originally been developed as separate parts. Using one process snapshot made
it possible for a change in a slider to affect both the displayed value and
the corresponding visualization.

### 5.2 Main runtime systems

| System | Main role |
| --- | --- |
| `PlantProcessSimulator` | Process inputs, equations and outputs |
| `RecycleMassBalanceEngine` | H2/CO2 recycle and purge calculation |
| `FinalPlantFlowRuntime` | Finds pipe routes and connects flow to the process |
| `PipeFlowAnimator` | Updates flow properties for each pipe segment |
| `PipeFlow.shader` | Draws the moving stream packets |
| `LightweightReactorVisual` | Internal reactor flow visualization |
| `CatalystBedColorAnimator` | Changes catalyst colour with operating state |
| `IcodosDashboardRuntime` | Main Daylight dashboard |
| `CorrelationGraphRuntime` | Analytics curves and recorded points |
| `InteractiveModulePanelRuntime` | Equipment controls and values |
| `SafetyWarningRuntime` | Educational process warnings |
| `OrbitCameraController` | Camera movement and equipment focus |
| `PlantEnvironmentBuilder` | Industrial surroundings |

The project uses one integrated scene. Several runtime components locate
equipment using established hierarchy and route names. This reduced the amount
of manual scene wiring needed during integration, although it also means those
names should remain stable.

## 6. Pipe-flow visualization

The pipe-flow system uses the existing pipe meshes together with a custom
transparent shader. Earlier approaches considered using many individual
particles, but this would have been unnecessarily heavy for the complete
plant.

Each pipe segment instead receives a `MaterialPropertyBlock` containing the
flow direction, speed, density, opacity, intensity and stream colours. This
allows the complete pipe network to remain active without creating a large
number of GameObjects.

`FinalPlantFlowRuntime` reads the process state every 0.08 seconds. The
normalized stream rate is then used to change flow speed, packet density,
visibility and intensity. Because of this, reducing a process flow also reduces
the visible activity in the corresponding pipe.

For mixed streams, H2, CO2 and recycle are kept as separate visual packets
instead of giving the complete mixture one colour. Their fractions are
estimated from the available mass-flow values and approximate molecular
weights.

Route direction is defined from the source equipment to the destination
equipment. Some imported meshes have the opposite internal ordering, so those
routes use a reverse flag to correct the shader direction. This changes only
the visualization direction and not the engineering process direction.

Curved fittings do not all have identical UV layouts, so packet spacing can
look slightly different on some bends. This remained as a visual limitation of
the imported geometry.

## 7. Reactor and catalyst visualization

The reactor shell and caps are semi-transparent so that the internal activity
and catalyst bed can be seen while keeping the reactor geometry visible.

Earlier reactor particle versions used a much denser population and caused
stability problems on the development laptop. Because of this, the final
version uses a maximum of 150 live bubbles.

The internal flow begins in the lower cylindrical part of the reactor near the
feed region, spreads through the catalyst-bed cross-section and moves upward
towards the top outlet. A fraction based on the live single-pass conversion
changes to the product visualization inside the catalyst region.

The bubble positions are constrained inside the straight cylindrical vessel
region so that the flow does not extend outside the reactor geometry. This
effect is used to explain the flow and conversion through the reactor; it is
not a CFD or molecular simulation.

The catalyst bed also changes colour according to the operating state:

- ochre for idle or low load;
- green for active operation;
- orange for high conversion; and
- red for overtemperature.

The colour is based on load, conversion and reactor temperature.

## 8. User interface and interaction

The final interface uses the Daylight design developed during the last stage of
the project. It contains a floating header, module navigation, process-stream
legend, plant-status information, efficiency ring, KPIs, bottom dock and
scrollable module controls.

A welcome screen and tutorial are shown when the application starts so that
the main controls can be understood before interacting with the plant.

The available controls include plant throughput, electrolyzer power and water,
capture conditions, compressor ratio, reactor temperature and pressure,
H2/CO2 ratio, GHSV, cooling conditions, separator temperature, recycle ratio,
reflux ratio and reboiler temperature.

All of these controls write back to `PlantProcessSimulator`. This means the
numerical values and the visual flow are updated from the same calculation.

The analytics section opens in a separate movable window in the Windows build.
Operating points can be recorded and are numbered in the order in which they
were created. The graph also shows background curves and can trace a model
curve through a selected recorded point. Mass-balance data can be exported as
CSV.

## 9. Camera and warnings

The camera can orbit the plant, pan laterally, zoom, return to the overview and
focus on individual process modules. Ground and horizon limits were added
after testing because unrestricted camera movement could easily place the view
below the site.

The warning system covers conditions such as insufficient electrolyzer water,
poor capture conditions, unsuitable regeneration settings, extreme compressor
ratio, low reactor temperature or pressure, unsuitable H2/CO2 ratio, high
GHSV, reactor overtemperature, poor condenser conditions, abnormal recycle,
low methanol purity and high storage level.

These warnings are intended to help the user understand how operating
conditions affect the process. They are not plant trip or safety-system
settings.

## 10. Plant environment

The surroundings are created at runtime by
`PlantEnvironmentBuilder`. They include roads, slab areas, safety markings,
fencing, pipe racks, utility areas, containment, tank-farm context, service
frames, control structures and equipment foundations.

A water-treatment area was also added with storage, pumping and
reverse-osmosis/polishing equipment. This provides the water source for the
electrolyzer instead of having the electrolyzer appear as an isolated unit.

The equipment models were kept relatively low-poly because maintaining stable
performance on the development laptop was more important than adding very
heavy environmental geometry.

## 11. Validation and final build

The repository contains editor tools for structural release validation,
mass-balance checks and Windows building.

During the final development cycle on September 25, a fresh Windows build was
created after the reactor build fix, water-treatment integration, graph and
label changes, camera constraints and final interface fixes. The completed
project state was then transferred into the `submission_final` branch.

The release validator checks the main scene, missing script references,
required pipe-route segments, catalyst and reactor renderers, the
`Custom/PipeFlow` shader and the main camera setup.

The project was tested as a Windows build and the final development cycle also
included runtime validation. The editor-side validation is useful for catching
structural problems, but it does not replace testing every possible operating
condition or camera angle.

The process results have also not been calibrated against an external rigorous
process simulator or real plant data.

## 12. Performance decisions and remaining limitations

Several implementation decisions were made mainly because the full plant,
dashboard and visual effects needed to run together on the same machine.

- Pipe flow is shader-based instead of creating one object for every packet.
- `MaterialPropertyBlock` is used so that pipe properties can change without
  unnecessary material duplication.
- Reactor bubbles are capped at 150 after the denser prototype caused
  instability.
- The industrial environment is built from lightweight reusable primitives.
- Process and flow updates are not recalculated on every rendered frame.

Some older controllers and waypoint prototypes are still present in the
project. Runtime object discovery also depends partly on hierarchy and route
names, and the different imported pipe meshes do not all have consistent UVs.

The process model remains steady-state and empirical. It does not include
transport delay, controller dynamics or live data. The UI is also generated
mainly at runtime, which makes some layout changes less direct than they would
be with a fully prefab-based interface.

## 13. Future improvements

If the project is continued, the first useful improvement would be to compare
the nominal process results with a documented Aspen, DWSIM or literature case.
This would provide an external reference for the simplified calculations.

The process model could also be supported by more automated calculation tests,
especially for stoichiometry, mass-balance closure and expected parameter
trends.

On the software side, older unused controllers could be removed, core runtime
systems could be separated into assembly definitions and more of the UI could
be converted to reusable prefabs. Additional PlayMode tests could then cover
the controls, warnings, camera and stream directions.

## 14. Conclusion

The final result is an interactive Power-to-Methanol plant visualization in
which the process model and the 3D scene work together. The project started
from separate plant, reactor, UI and flow components, and the main development
work was to integrate these parts into one application.

The shared process state makes it possible for operating changes to affect the
dashboard values, pipe flow, reactor activity, catalyst state and warnings at
the same time. This helps demonstrate the relationship between the different
parts of the PtM process rather than showing only a static plant model.

The application can therefore be used to present the overall process,
material-flow direction, reaction stoichiometry, recycle behaviour and the
effect of the main operating parameters, while keeping the simplified nature
of the process model clear.
