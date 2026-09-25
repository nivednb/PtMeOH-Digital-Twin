# Site and Horizon Surroundings

This branch adds a lightweight environment system around the existing PtMeOH plant. It does **not** change the process model, process equipment, stream routing, analytics, solver behaviour, or numerical calculations.

## Layered environment

The environment is intentionally split into three levels of detail.

### 1. Immediate process site

`PlantEnvironmentBuilder` remains responsible for the detailed near-site area around the actual process equipment:

- plant slab and equipment pads;
- service/maintenance roads;
- safety markings and perimeter fence;
- pipe racks and utility headers;
- utility/control zone;
- storage containment;
- nearby industrial background structures.

The builder still limits this detailed area to roughly the current 78-118 m site scale so it stays lightweight and readable.

### 2. Close CC0 surroundings

`PtMeOHSiteAssetEnvironment` decorates only the perimeter/background of the detailed plant with:

- two non-process service/maintenance buildings;
- modular industrial facade and roof details;
- pallets, crates and barrels in the service yard;
- road lights and entrance barriers;
- sparse trees outside the process fence.

These objects are children of:

`Generated_Plant_Environment_N/CC0 Site Surroundings`

The close CC0 layer can be disabled with `createCc0AssetSurroundings`.

### 3. Extended horizon world

`PtMeOHHorizonEnvironment` fills the world outside the immediate site so an elevated or orbiting camera does not see the edge of the generated plant environment.

It creates:

- a continuous approximately **2400 m x 2400 m** ground plane;
- a broad industrial-estate transition apron outside the near site;
- an outer industrial road loop and several longer regional roads;
- **32** low-cost mid-distance industrial building groups around the full 360 degrees;
- three distant storage/tank clusters;
- utility stacks mixed through the mid-distance estate;
- **56** simplified far industrial silhouettes and periodic chimneys;
- **52** broad landscape/tree-line belts that fill gaps in the far skyline.

The geometry continues beyond the visible atmospheric range. Linear fog begins at about **270 m** and reaches the background colour by about **1050 m**, while the main camera far clipping distance is raised to at least **1800 m**.

This means the real world geometry boundary is hidden by atmospheric perspective rather than being visible as a hard edge.

The extended layer is parented under:

`Generated_Plant_Environment_N/Extended Horizon Surroundings`

and can be disabled with `createExtendedHorizonSurroundings`.

## Why the far surroundings use simpler geometry

At hundreds of metres from the plant, detailed FBX geometry is not visually useful. The far layers therefore use simplified building, stack and vegetation silhouettes. This preserves the industrial context while keeping the educational process model and animated pipe flows as the visual focus.

The close perimeter uses the imported CC0 assets where their detail is actually visible.

## Camera/background strategy

The project keeps the existing solid blue-grey industrial backdrop instead of reintroducing Unity's procedural sky lower-hemisphere artefact.

The extended horizon matches atmospheric fog to that backdrop colour. Ground, distant buildings and vegetation therefore fade into the same horizon colour before the actual geometry ends.

## Asset provenance

The external FBX assets are placed under:

`Assets/Resources/PtMeOHEnvironment/`

and are loaded with `Resources.Load`. Missing optional assets fall back cleanly to the primitive near-site environment.

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

The surroundings are decorative site context only. They deliberately stay outside the modeled PtMeOH process layout so viewers do not confuse them with process units included in the digital twin.

The application should still be described as an educational process visualization rather than an exact industrial site layout.
