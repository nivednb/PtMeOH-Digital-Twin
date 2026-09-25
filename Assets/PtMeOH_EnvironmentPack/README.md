# PtMeOH Environment Pack

This folder is a drop-in surroundings layer for the current PtMeOH Digital Twin scene.

WHAT IT DOES
- Keeps the existing PlantEnvironmentBuilder and all process/simulation code intact.
- Re-skins generated concrete, asphalt, grass and industrial building blocks with lightweight procedural materials.
- Adds a small curated set of CC0 Kenney industrial buildings/tanks as distant background context.
- Adds CC0 Kenney service-road light poles along the front approach.
- Replaces the flat camera backdrop with a lightweight generated panoramic industrial sky when the Skybox/Panoramic shader is available.
- Creates everything under External_PtMeOH_Environment_N so imported decoration is easy to disable or remove.

HOW TO USE
1. Switch to the environment-pack branch, or copy Assets/PtMeOH_EnvironmentPack into your existing Unity project.
2. Open Assets/Scenes/SampleScene.unity.
3. Press Play. No scene wiring is required. The decorator waits for Generated_Plant_Environment_N, then applies the surroundings automatically.
4. For an edit-mode preview, use Tools > Nived > PtMeOH Environment > Build Preview.
5. Use Tools > Nived > PtMeOH Environment > Clear Preview to remove only the added decoration.

SCENE STRATEGY
The process plant remains the visual focus. Imported models are deliberately restricted to the perimeter/background so they do not cover the absorber, reactor, synthesis loop, separators, storage equipment, process pipes, animated flows or UI.

PERFORMANCE
The pack uses 7 small FBX model instances/resources, two compact palette textures from the CC0 source kits, tiny generated tiling textures, and no continuously running Update loop. No new realtime point/spot lights are created.

REMOVAL
Delete Assets/PtMeOH_EnvironmentPack or disable/remove the auto-created PtMeOH Environment Decorator object. The original project remains functional.
