# Optional Site Surroundings (CC0)

This branch adds a lightweight environment layer around the existing PtMeOH plant. It does **not** change the process model, process equipment, stream routing, analytics, or numerical calculations.

## Behaviour

`PlantEnvironmentBuilder` still creates the existing lightweight industrial site from Unity primitives. After that, `PtMeOHSiteAssetEnvironment` adds only perimeter/background detail:

- two non-process service/maintenance buildings;
- modular industrial facade and roof details;
- pallets, crates and barrels in the service yard;
- road lights and entrance barriers;
- sparse trees outside the process fence.

All added objects are children of `Generated_Plant_Environment_N/CC0 Site Surroundings`, so rebuilding the environment removes and recreates them cleanly.

The external FBX assets are placed under:

`Assets/Resources/PtMeOHEnvironment/`

and are loaded with `Resources.Load`. If an asset is missing, the application continues with the original primitive environment.

## Asset provenance

### ModKit — Modular Building Kit & Prop Pack

Source: https://github.com/JaronKBragg7337/asset-pack-ue-threejs-blender-unity

Used assets:
- SM_Wall_Reinforced_4m
- SM_Wall_Doorway_4m
- SM_Wall_Window_4m
- SM_Roof_Flat_4m
- SM_Pillar_3m
- SM_Railing_4m
- SM_Crate_Stack
- SM_Barrel
- SM_Pallet
- SM_Vent_Wall

License: **CC0 1.0 Universal**.

### Kenney City Kit (Roads)

Original source: https://kenney.nl/assets/city-kit-roads

Mirror used for binary retrieval:
https://github.com/petroulacl/fps-buildings-env-kit

Used assets:
- light-square
- construction-barrier
- colormap

License: **CC0 1.0 Universal**.

### Kenney City Kit (Suburban)

Original source: https://kenney.nl/assets/city-kit-suburban

Mirror used for binary retrieval:
https://github.com/petroulacl/fps-buildings-env-kit

Used assets:
- tree-large
- tree-small
- colormap

License: **CC0 1.0 Universal**.

## Design intent

These models are decorative site context only. They deliberately sit outside the process layout so that a viewer does not confuse them with modeled PtMeOH process units.

The application should still be described as an educational process visualization rather than an exact industrial site layout.
