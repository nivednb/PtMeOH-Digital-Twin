using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Adds a lightweight CC0 asset layer around the generated PtMeOH site.
///
/// The process equipment remains untouched. This helper only decorates the perimeter with
/// maintenance buildings, service-yard props, road lights and sparse vegetation so the plant
/// reads as part of a larger industrial site rather than an isolated model.
///
/// All external models are loaded from Resources at runtime. Missing assets are treated as
/// optional and simply fall back to the existing primitive PlantEnvironmentBuilder scene.
/// </summary>
public static class PtMeOHSiteAssetEnvironment
{
    private const string RootName = "CC0 Site Surroundings";

    private const string WallReinforced = "PtMeOHEnvironment/ModKit/SM_Wall_Reinforced_4m";
    private const string WallDoorway = "PtMeOHEnvironment/ModKit/SM_Wall_Doorway_4m";
    private const string WallWindow = "PtMeOHEnvironment/ModKit/SM_Wall_Window_4m";
    private const string RoofFlat = "PtMeOHEnvironment/ModKit/SM_Roof_Flat_4m";
    private const string Pillar = "PtMeOHEnvironment/ModKit/SM_Pillar_3m";
    private const string Railing = "PtMeOHEnvironment/ModKit/SM_Railing_4m";
    private const string CrateStack = "PtMeOHEnvironment/ModKit/SM_Crate_Stack";
    private const string Barrel = "PtMeOHEnvironment/ModKit/SM_Barrel";
    private const string Pallet = "PtMeOHEnvironment/ModKit/SM_Pallet";
    private const string Vent = "PtMeOHEnvironment/ModKit/SM_Vent_Wall";

    private const string RoadLight = "PtMeOHEnvironment/KenneyRoads/light-square";
    private const string RoadBarrier = "PtMeOHEnvironment/KenneyRoads/construction-barrier";
    private const string RoadColorMap = "PtMeOHEnvironment/KenneyRoads/colormap";

    private const string TreeLarge = "PtMeOHEnvironment/KenneySite/tree-large";
    private const string TreeSmall = "PtMeOHEnvironment/KenneySite/tree-small";
    private const string SiteColorMap = "PtMeOHEnvironment/KenneySite/colormap";

    private static Material wallMaterial;
    private static Material roofMaterial;
    private static Material shellMaterial;
    private static Material propMaterial;
    private static Material roadAssetMaterial;
    private static Material siteAssetMaterial;

    public static void Build(Transform environmentRoot, Vector3 center, float siteWidth, float siteDepth, float baseY)
    {
        if (environmentRoot == null) return;

        Transform old = environmentRoot.Find(RootName);
        if (old != null)
        {
            if (Application.isPlaying) Object.Destroy(old.gameObject);
            else Object.DestroyImmediate(old.gameObject);
        }

        CreateMaterials();

        GameObject rootObject = new GameObject(RootName);
        Transform root = rootObject.transform;
        root.SetParent(environmentRoot, false);

        // Keep all decorative structures outside the educational process layout. The rear
        // buildings read as a service/maintenance compound and never imply extra process units.
        float rearZ = center.z + siteDepth * 0.64f;
        BuildServiceBuilding(root, new Vector3(center.x - siteWidth * 0.28f, baseY, rearZ),
            12f, 8f, 4.2f, "Maintenance Warehouse", true);
        BuildServiceBuilding(root, new Vector3(center.x + siteWidth * 0.30f, baseY, rearZ + 1.2f),
            8f, 6f, 3.7f, "Electrical / Utility Workshop", false);

        BuildRoadsideDetails(root, center, siteWidth, siteDepth, baseY);
        BuildLandscapeBuffer(root, center, siteWidth, siteDepth, baseY);
    }

    private static void BuildServiceBuilding(Transform parent, Vector3 basePosition,
        float width, float depth, float height, string name, bool doorway)
    {
        GameObject building = new GameObject(name);
        building.transform.SetParent(parent, false);

        // A simple opaque shell guarantees the building reads correctly from every camera
        // angle. CC0 modular pieces are then used as the visible facade and roof detailing.
        GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shell.name = name + " Shell";
        shell.transform.SetParent(building.transform, false);
        shell.transform.position = basePosition + Vector3.up * (height * 0.5f);
        shell.transform.localScale = new Vector3(width - 0.18f, height - 0.08f, depth - 0.18f);
        ApplyMaterial(shell, shellMaterial);
        RemoveCollider(shell);

        float frontZ = basePosition.z - depth * 0.5f - 0.03f;
        int facadeModules = Mathf.Max(1, Mathf.RoundToInt(width / 4f));
        float facadeStartX = basePosition.x - (facadeModules - 1) * 2f;

        for (int i = 0; i < facadeModules; i++)
        {
            string asset = WallReinforced;
            if (doorway && i == facadeModules / 2) asset = WallDoorway;
            else if (!doorway && i == facadeModules / 2) asset = WallWindow;

            Spawn(asset, name + " Facade " + i, building.transform,
                new Vector3(facadeStartX + i * 4f, basePosition.y, frontZ),
                Quaternion.identity, Vector3.one, wallMaterial);
        }

        // Roof tiles give a stronger silhouette than a plain box when the camera is elevated.
        int roofX = Mathf.Max(1, Mathf.CeilToInt(width / 4f));
        int roofZ = Mathf.Max(1, Mathf.CeilToInt(depth / 4f));
        float roofStartX = basePosition.x - width * 0.5f;
        float roofStartZ = basePosition.z - depth * 0.5f;
        for (int x = 0; x < roofX; x++)
        {
            for (int z = 0; z < roofZ; z++)
            {
                Spawn(RoofFlat, name + " Roof " + x + "_" + z, building.transform,
                    new Vector3(roofStartX + x * 4f, basePosition.y + height + 0.02f, roofStartZ + z * 4f),
                    Quaternion.identity, Vector3.one, roofMaterial);
            }
        }

        // Vertical structural accents.
        Vector3[] corners =
        {
            new Vector3(basePosition.x - width * 0.5f, basePosition.y, basePosition.z - depth * 0.5f),
            new Vector3(basePosition.x + width * 0.5f, basePosition.y, basePosition.z - depth * 0.5f),
            new Vector3(basePosition.x - width * 0.5f, basePosition.y, basePosition.z + depth * 0.5f),
            new Vector3(basePosition.x + width * 0.5f, basePosition.y, basePosition.z + depth * 0.5f),
        };
        foreach (Vector3 corner in corners)
            Spawn(Pillar, name + " Corner Post", building.transform, corner,
                Quaternion.identity, new Vector3(1f, height / 3f, 1f), propMaterial);

        // Service-yard details intentionally stay small and outside the process area.
        Spawn(Pallet, name + " Pallet", building.transform,
            basePosition + new Vector3(width * 0.38f, 0f, -depth * 0.64f),
            Quaternion.Euler(0f, 15f, 0f), Vector3.one, propMaterial);
        Spawn(CrateStack, name + " Crates", building.transform,
            basePosition + new Vector3(width * 0.27f, 0f, -depth * 0.68f),
            Quaternion.Euler(0f, -10f, 0f), Vector3.one, propMaterial);
        Spawn(Barrel, name + " Barrel A", building.transform,
            basePosition + new Vector3(-width * 0.36f, 0f, -depth * 0.68f),
            Quaternion.identity, Vector3.one, propMaterial);
        Spawn(Barrel, name + " Barrel B", building.transform,
            basePosition + new Vector3(-width * 0.30f, 0f, -depth * 0.68f),
            Quaternion.identity, Vector3.one, propMaterial);
        Spawn(Vent, name + " Vent", building.transform,
            basePosition + new Vector3(width * 0.18f, height * 0.55f, -depth * 0.51f),
            Quaternion.identity, Vector3.one, propMaterial);

        // A short railing implies a pedestrian/service edge without enclosing the plant.
        Spawn(Railing, name + " Service Railing", building.transform,
            basePosition + new Vector3(-width * 0.28f, 0f, -depth * 0.70f),
            Quaternion.identity, Vector3.one, propMaterial);
    }

    private static void BuildRoadsideDetails(Transform root, Vector3 center,
        float siteWidth, float siteDepth, float baseY)
    {
        float roadZ = center.z - siteDepth * 0.37f;
        float[] xs =
        {
            center.x - siteWidth * 0.34f,
            center.x - siteWidth * 0.12f,
            center.x + siteWidth * 0.12f,
            center.x + siteWidth * 0.34f,
        };

        foreach (float x in xs)
        {
            Spawn(RoadLight, "CC0 Yard Light", root,
                new Vector3(x, baseY + 0.03f, roadZ - 2.4f),
                Quaternion.identity, Vector3.one * 1.6f, roadAssetMaterial);
        }

        // A small controlled entrance near the left access road.
        float gateX = center.x - siteWidth * 0.43f;
        for (int i = 0; i < 3; i++)
        {
            Spawn(RoadBarrier, "CC0 Entry Barrier " + i, root,
                new Vector3(gateX + 1.0f, baseY + 0.03f, roadZ - 1.5f + i * 1.2f),
                Quaternion.Euler(0f, 90f, 0f), Vector3.one * 1.3f, roadAssetMaterial);
        }
    }

    private static void BuildLandscapeBuffer(Transform root, Vector3 center,
        float siteWidth, float siteDepth, float baseY)
    {
        // Sparse vegetation sits outside the process fence. It breaks the empty horizon but
        // never overlaps process equipment or suggests a park-like setting inside the plant.
        Vector3[] positions =
        {
            new Vector3(center.x - siteWidth * 0.56f, baseY, center.z - siteDepth * 0.48f),
            new Vector3(center.x - siteWidth * 0.58f, baseY, center.z + siteDepth * 0.10f),
            new Vector3(center.x - siteWidth * 0.55f, baseY, center.z + siteDepth * 0.48f),
            new Vector3(center.x + siteWidth * 0.56f, baseY, center.z - siteDepth * 0.46f),
            new Vector3(center.x + siteWidth * 0.58f, baseY, center.z + siteDepth * 0.06f),
            new Vector3(center.x + siteWidth * 0.55f, baseY, center.z + siteDepth * 0.47f),
            new Vector3(center.x - siteWidth * 0.20f, baseY, center.z + siteDepth * 0.72f),
            new Vector3(center.x + siteWidth * 0.18f, baseY, center.z + siteDepth * 0.74f),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            bool large = i % 3 != 1;
            Spawn(large ? TreeLarge : TreeSmall, "CC0 Perimeter Tree " + i, root,
                positions[i],
                Quaternion.Euler(0f, (i * 47f) % 360f, 0f),
                Vector3.one * (large ? 2.2f : 1.8f), siteAssetMaterial);
        }
    }

    private static GameObject Spawn(string resourcePath, string objectName, Transform parent,
        Vector3 position, Quaternion rotation, Vector3 scale, Material overrideMaterial)
    {
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"PtMeOH environment asset missing: Resources/{resourcePath}");
            return null;
        }

        GameObject instance = Object.Instantiate(prefab, position, rotation, parent);
        instance.name = objectName;
        instance.transform.localScale = scale;

        if (overrideMaterial != null)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                renderer.sharedMaterial = overrideMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
            Object.Destroy(collider);

        return instance;
    }

    private static void CreateMaterials()
    {
        wallMaterial = MakeMaterial("CC0 Surrounding Wall", new Color(0.44f, 0.48f, 0.51f), 0.20f);
        roofMaterial = MakeMaterial("CC0 Surrounding Roof", new Color(0.20f, 0.24f, 0.27f), 0.28f);
        shellMaterial = MakeMaterial("CC0 Building Shell", new Color(0.31f, 0.36f, 0.39f), 0.18f);
        propMaterial = MakeMaterial("CC0 Yard Props", new Color(0.42f, 0.45f, 0.47f), 0.16f);

        roadAssetMaterial = MakeTexturedMaterial("CC0 Kenney Road Assets", RoadColorMap, 0.18f);
        siteAssetMaterial = MakeTexturedMaterial("CC0 Kenney Site Assets", SiteColorMap, 0.12f);
    }

    private static Material MakeMaterial(string name, Color color, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    private static Material MakeTexturedMaterial(string name, string textureResourcePath, float smoothness)
    {
        Material material = MakeMaterial(name, Color.white, smoothness);
        Texture2D colorMap = Resources.Load<Texture2D>(textureResourcePath);
        if (colorMap != null)
        {
            material.mainTexture = colorMap;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", colorMap);
        }
        return material;
    }

    private static void ApplyMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
    }

    private static void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }
    }
}
