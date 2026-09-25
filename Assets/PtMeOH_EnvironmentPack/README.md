# PtMeOH Environment Pack

This folder is a drop-in full-world surroundings layer for the current PtMeOH Digital Twin scene.

WHAT IT DOES
- Keeps the existing PlantEnvironmentBuilder and all process/simulation code intact.
- Treats PlantEnvironmentBuilder as the NEAR-SITE layer only.
- Re-skins generated concrete, asphalt, grass and industrial building blocks with lightweight procedural materials.
- Adds curated CC0 Kenney industrial buildings/tanks and service-road details around the plant.
- Adds a second automatic component, PtMeOHHorizonEnvironment, that continues the world far beyond the near site.
- Creates a roughly 2400 m x 2400 m ground plane, larger than the visible fog distance, so the map edge is never visible.
- Creates a 360-degree mid-distance industrial estate approximately 120-310 m from the plant.
- Creates a far industrial skyline, stacks and tree/vegetation belts approximately 430-875 m from the plant.
- Uses atmospheric fog to blend the far geometry into a generated industrial sky around 1 km from the plant.
- Raises the camera far-clip distance as needed so elevated/orbit views still see the surroundings.

HOW TO USE
1. Switch to the environment-pack branch, or copy Assets/PtMeOH_EnvironmentPack into your existing Unity project.
2. Open Assets/Scenes/SampleScene.unity.
3. Press Play. No scene wiring is required.
4. PlantEnvironmentBuilder creates the immediate process site.
5. PtMeOHEnvironmentDecorator improves the immediate/perimeter environment.
6. PtMeOHHorizonEnvironment then fills everything outside that area through the visible horizon.
7. For edit-mode preview:
   - Tools > Nived > PtMeOH Environment > Build Preview
   - Tools > Nived > PtMeOH Environment > Build Horizon Preview

SCENE ZONES
- NEAR: existing process plant and PlantEnvironmentBuilder site, roughly the current 78-118 m class site.
- MID: roads, industrial buildings, tanks and utility stacks surrounding the plant in all directions.
- FAR: simplified low-cost factories, chimneys and vegetation belts forming a complete 360-degree skyline.
- HORIZON: continuous ground extends beyond the fog end; fog and sky hide the actual geometry boundary.

The process plant remains the visual focus. Nothing in the horizon layer replaces or covers the absorber, reactor, synthesis loop, separators, storage equipment, process pipes, animated flows or UI.

PERFORMANCE
- Close/mid surroundings reuse a small set of low-poly FBX resources.
- Far buildings use simple primitive silhouettes because mesh detail is not visible at those distances.
- Horizon vegetation uses broad tree-belt silhouettes rather than hundreds of individual trees.
- No new realtime point/spot lights are created.
- Horizon objects are marked static and runtime static batching is requested.
- No continuously running Update loop is used by the horizon generator.

GENERATED ROOTS
- Generated_Plant_Environment_N : existing near-site builder.
- External_PtMeOH_Environment_N : local imported decoration/material improvements.
- PtMeOH_Horizon_World_N : complete mid/far/horizon environment.

REMOVAL
Delete Assets/PtMeOH_EnvironmentPack or disable/remove the auto-created PtMeOH environment components. The original project remains functional.
