# Implementation and Engineering Reference

This appendix records the current implementation at source-code level. Paths
are relative to the repository root.

## 1. Core file inventory

### 1.1 Integrated runtime

| File | Current role |
| --- | --- |
| `Assets/Scripts_N/PlantProcessSimulator.cs` | Central process inputs, equations, snapshot |
| `Assets/Scripts_N/RecycleMassBalanceEngine.cs` | Fixed-point CO2/H2 recycle/purge balance |
| `Assets/Scripts_N/FinalFlowSystem/FinalPlantFlowRuntime.cs` | Route discovery and process-to-flow coupling |
| `Assets/PipeFlowAnimator.cs` | Per-segment shader property animation |
| `Assets/PipeFlow.shader` | Transparent carrier and moving species packets |
| `Assets/Scripts_N/FinalFlowSystem/PlantFlowKind.cs` | Stream classifications |
| `Assets/Scripts_N/FinalFlowSystem/LightweightReactorVisual.cs` | Low-cost upflow reactor visual |
| `Assets/Scripts_N/FinalFlowSystem/CatalystBedColorAnimator.cs` | Catalyst operating-state color |
| `Assets/Scripts_N/IcodosDashboardRuntime.cs` | Daylight dashboard and analytics-window integration |
| `Assets/Scripts_N/CorrelationGraphRuntime.cs` | Recorded points, constant-condition curves and point tracing |
| `Assets/Scripts_N/ReactorReactionCard.cs` | Reactor hover card with live reaction rates |
| `Assets/Scripts_N/InteractiveModulePanelRuntime.cs` | Equipment panels and scrollable controls |
| `Assets/Scripts_N/SafetyWarningRuntime.cs` | Educational warnings |
| `Assets/OrbitCameraController.cs` | Camera navigation and focus |
| `Assets/Scripts_N/PlantEnvironmentBuilder.cs` | Runtime industrial environment |
| `Assets/Scripts_N/RuntimeValidationCapture.cs` | Automated runtime screenshot helper |

### 1.2 Editor and release tooling

| File | Role |
| --- | --- |
| `Assets/Editor/ProjectInventory.cs` | Deep hierarchy/asset inventory |
| `Assets/Editor/ReleaseValidation.cs` | Structural release validation |
| `Assets/Editor/WindowsBuild.cs` | Reproducible Windows build entry point |

### 1.3 Legacy and support files

| File | Status |
| --- | --- |
| `Assets/AbsorberController.cs` | Earlier absorber panel/controller |
| `Assets/ReactorController.cs` | Earlier reactor panel/controller |
| `Assets/OverviewPanelController.cs` | Earlier overview UI |
| `Assets/CameraController.cs` | Earlier camera support |
| `Assets/PlantPipeManager.cs` | Legacy primitive pipe generator; disabled by default |
| `Assets/ReactorPanelToggle.cs` | Legacy reactor-panel toggle |
| `Assets/Scripts_N/FlowPath.cs` | Waypoint path support/prototype |
| `Assets/Scripts_N/FlowFollower.cs` | Waypoint follower support/prototype |
| `Assets/Scripts_N/PipeWaypointGenerator.cs` | Waypoint generation support/prototype |

These files should not be described as the primary integrated architecture.

## 2. Nominal process inputs

| Variable | Nominal/default |
| --- | ---: |
| Plant throughput | 100% |
| Electrolyzer power | 75% |
| Water feed | 100% |
| Flue-gas feed | 100% |
| Amine circulation | 65% |
| Regeneration steam | 70% |
| Regenerator temperature | 105 °C |
| Compressor pressure ratio | 3 |
| Reactor temperature | 250 °C |
| Reactor pressure | 70 bar |
| H2/CO2 ratio | 3.0 |
| GHSV | 8,000 h⁻¹ |
| Reactor feed | 100% |
| Cooling rate | 70% |
| Cooling temperature | 24 °C |
| Separator temperature | 34 °C |
| Recycle | 65% |
| Reflux ratio | 3.2 |
| Reboiler temperature | 98 °C |

Design reference rates:

| Stream | Rate |
| --- | ---: |
| H2 | 215 kg/h |
| CO2 | 1,510 kg/h |
| Water feed | 1,935 kg/h |
| Methanol | 1,250 kg/h |
| Storage capacity | 12,000 kg |

## 3. Process equations

### 3.1 Electrolyzer

The final simulator limits hydrogen production independently by electrical
power and by available feed-water hydrogen mass:

```text
powerFactor = clamp01(electrolyzerPower / 100)
waterFactor = clamp01(waterFeedPercent / 100)
waterFeed = designWaterFeed × plantRamp × waterFactor

H2_from_power = designH2 × plantRamp × powerFactor
H2_from_water = waterFeed × (2.01588 / 18.01528)
H2 = min(H2_from_power, H2_from_water)

O2 = H2 × ((18.01528 - 2.01588) / 2.01588)
```

### 3.2 Capture

```text
amineFactor = clamp01(aminePercent / 100)^0.55
regenFactor = InverseLerp(82, 118, regenerationTemperature)
steamFactor = clamp01(steamPercent / 100)^0.45

captureEfficiency =
    clamp01(0.18
          + 0.46×amineFactor
          + 0.22×regenFactor
          + 0.14×steamFactor)

capturedCO2 = designCO2 × plantRamp × flueGasFactor × captureEfficiency
```

### 3.3 Reactor single-pass conversion

The dashboard reactor yield is the single-pass CO2 conversion. The response is
centered on 240 °C and is intentionally asymmetric so every 180–300 °C
temperature step produces a distinct curve:

```text
deltaT = temperatureK - 513.15

temperatureFactor =
    exp(-0.0004 × deltaT²)   when temperature < 240 °C
    exp(-0.0006 × deltaT²)   when temperature >= 240 °C

pressureFactor = (max(1, pressureBar) / 70)^0.35
velocityFactor = (8000 / max(1000, GHSV))^0.2
ratioFactor = 1 - clamp01(|H2CO2Ratio - 3| / 3) × 0.42

singlePassCO2Conversion =
    clamp(0.25 × temperatureFactor
               × pressureFactor
               × velocityFactor
               × ratioFactor,
          0.02, 0.35)
```

The selected H2/CO2 molar ratio and reactor-feed percentage determine the fresh
H2 and CO2 sent into the recycle calculation.

### 3.4 Steady-state recycle/purge balance

The recycle loop represents:

`CO2 + 3 H2 → CH3OH + H2O`

with methanol and water removed before gas recycle. The solver iterates the
unreacted gas recycle to a fixed point:

```text
reactorCO2 = freshCO2 + recycleCO2
reactorH2  = freshH2  + recycleH2

extent = min(reactorCO2, reactorH2 / 3) × singlePassCO2Conversion

unreactedCO2 = reactorCO2 - extent
unreactedH2  = reactorH2  - 3×extent

recycleCO2 = unreactedCO2 × recycleFraction
recycleH2  = unreactedH2  × recycleFraction

purgeCO2 = unreactedCO2 × (1 - recycleFraction)
purgeH2  = unreactedH2  × (1 - recycleFraction)
```

The implementation detects the limiting reactant, iterates until the recycle
change meets a tight tolerance, and reports external mass-balance closure.

### 3.5 Condensation, separation and purification

```text
coolingFactor =
    clamp01((coolingWaterFlow / 100) × InverseLerp(45, 8, coolingWaterTemperature))

condenserRecovery = Lerp(0.55, 0.98, coolingFactor)
separatorFactor = 1 - clamp01(|separatorTemperature - 34| / 35) × 0.16

distillationFactor =
    clamp01(0.72
          + 0.11×InverseLerp(0.5, 5, refluxRatio)
          + 0.17×InverseLerp(76, 105, reboilerTemperature))

methanolPurity =
    clamp(90
        + 7.2×InverseLerp(0.5, 5, refluxRatio)
        + 2.4×InverseLerp(78, 105, reboilerTemperature),
        88, 99.85)

reactorMethanol = converged recycle-balance methanol production
finalMethanol =
    min(designMethanol, reactorMethanol)
    × condenserRecovery
    × separatorFactor
    × distillationFactor

recycleGas = converged recycleCO2 + recycleH2
overallEfficiency = finalMethanol / theoreticalFreshFeedMethanol
```

## 4. Flow response

`FinalPlantFlowRuntime` refresh interval: 0.08 s.

```text
response = sqrt(normalizedMassFlow)
speed = baseSpeed × Lerp(0.35, 1.35, response)
density = baseDensity × Lerp(0.55, 1.30, normalizedMassFlow)
intensity = baseIntensity × Lerp(0.25, 1.25, response)
visible = normalizedMassFlow >= 0.005
```

Reference normalizations:

| Route family | Reference rate |
| --- | ---: |
| H2 | 215 kg/h |
| CO2 | 1,510 kg/h |
| Synthesis feed | 1,725 kg/h |
| Methanol | 1,250 kg/h |
| Crude condensate | 1,953.125 kg/h |
| Recycle | 450 kg/h |

## 5. Stream/species mapping

| Process stream | Visual species |
| --- | --- |
| Water-treatment route | water |
| H2 route | H2 |
| CO2 / amine routes | captured CO2 / solvent family |
| Mixed synthesis feed | calculated H2 + CO2 + recycle |
| Recycle gas | unconverted synthesis-gas visualization |
| Reactor effluent | hot product + remaining gas visualization |
| Crude condensed product | methanol/water visualization |
| Purified product | methanol |

Mixed-feed molar weighting:

```text
nH2 = H2 mass rate / 2.016
nCO2 = CO2 mass rate / 44.01
nRecycle = recycle mass rate / 12.5
species fraction = species molar estimate / total molar estimate
```

The recycle effective molecular weight and fixed effluent fractions are visual
approximations because the central model does not expose a complete
component-by-component stream table.

## 6. Stream colors

`PlantStreamLegend` is the single color authority used by both the legend and
the runtime flow system.

| Stream family | Hex | Meaning |
| --- | --- | --- |
| Water / H2 | `#38BDF8` | Raw-water and hydrogen family |
| Amine / captured CO2 | `#EC4899` | Pink solvent/captured-CO2 family |
| Syngas / mixed feed / recycle | `#F59E0B` | Synthesis-gas family |
| Reactor effluent | `#EF4444` | Hot reactor-effluent family |
| Crude methanol / water | `#A855F7` | Crude condensed-product family |
| Refined methanol | `#22C55E` | Final methanol-product family |

The recycle legend row is dashed to communicate that it returns gas upstream.

Catalyst states remain presentation states driven by load, conversion and
temperature rather than literal catalyst colors.

## 7. Route direction reference

| Route | Engineering direction |
| --- | --- |
| Raw water | Water-treatment unit → electrolyzer |
| Electrolyzer H2 | Electrolyzer → mixing T-junction |
| Captured CO2 | Capture/compression → mixing T-junction |
| Recycle gas | Separator/recycle loop → mixing T-junction |
| Mixed feed/syngas | T-junction → reactor feed preparation → reactor |
| Reactor internal visual | Lower cylindrical region → packed bed → top outlet |
| Reactor effluent | Reactor top outlet → condenser/separation |
| Crude methanol/water | Condenser/separator → purification |
| Methanol product | Purification → storage |

`reverse=true` on an imported route means the shader direction is corrected
against mesh ordering; it does not reverse the engineering process.

## 8. Reactor visual limits

- Maximum live population: 150 bubbles.
- Simulation space: world-oriented visual root to avoid inherited imported-model scaling.
- Shell alpha: approximately 0.14.
- Cap alpha: approximately 0.16.
- Catalyst-bed alpha: approximately 0.50.
- Bubble positions are analytically constrained inside the packed-bed radius
  and straight cylindrical vessel height.
- Entry visualization: lower cylindrical reactor region.
- Exit visualization: flow gathers toward the top outlet.
- A fraction equal to live single-pass conversion changes to product color in
  the catalyst bed.
- Reactor materials are stored under `Resources/ReactorVisual` so required
  shaders remain available in player builds.

## 9. Educational warning thresholds

| Module | Examples |
| --- | --- |
| Electrolyzer | Low water relative to power |
| Capture | Low capture; flue/amine mismatch |
| Regeneration | Insufficient steam/temperature; excessive temperature |
| Compressor | Ratio ≥90 warning, ≥98 critical on UI scale |
| Reactor | ≥285 °C critical; >270 °C sintering risk; ≤215 °C low rate |
| Reactor feed | Pressure <60 bar; ratio <2.5 H2-deficient; >4.5 H2-excess |
| Residence | GHSV >9,500 h⁻¹ |
| Condenser | Cooling temperature ≥38 °C; cooling rate ≤20%; recovery <70% |
| Recycle | <25% or >90% |
| Distillation | Purity <95%; reflux >4.2; reboiler ≥108 °C |
| Storage | ≥85% warning; ≥95% critical |

These are presentation/teaching thresholds, not certified trip limits.

## 10. Assets and equipment represented

The integrated project contains models for the absorber column, desorber
column, electrolyzer, compressor, condenser/heat exchanger, flash separator,
distillation column, reactor and reactor skirt, H2/CO2/methanol tanks, pipe
bends, T-junction, saddles, skirts, and structural/support elements.

Important reactor children include:

- `Reactor_Shell`;
- `Catalyst_Bed`;
- caps;
- feed/product nozzles;
- cooling-water nozzles/flanges; and
- skirt/support geometry.

## 11. Build and validation commands

Editor tooling supplies:

- deep inventory;
- release validation; and
- Windows build.

The final development cycle produced a fresh Windows build in
`Builds/Daylight/` after the September 25 reactor-build, water-treatment,
analytics-curve/label and camera fixes. The submission branch contains the
committed project state from that cycle. See `FINAL_PROJECT_REPORT.md` for the
scope and validation limitations.
