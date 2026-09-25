using UnityEngine;

/// <summary>
/// Builds the world outside the immediate PtMeOH site so elevated/orbit cameras
/// see a continuous industrial landscape all the way to an atmospheric horizon.
///
/// This layer is intentionally procedural and low-cost:
/// - the existing PlantEnvironmentBuilder remains the detailed near-site layer;
/// - PtMeOHSiteAssetEnvironment remains the close CC0 detail layer;
/// - this class owns only the mid-distance and far-distance world.
///
/// Everything is parented under the generated plant environment, so the normal
/// rebuild/clear workflow removes it automatically.
/// </summary>
public static class PtMeOHHorizonEnvironment
{
    private const string RootName = "Extended Horizon Surroundings";

    // The visible fog ends before the actual ground geometry ends, hiding the map edge.
    private const float WorldSize = 2400f;
    private const float FogStart = 270f;
    private const float FogEnd = 1050f;

    private static Material fieldMaterial;
    private static Material industrialGroundMaterial;
    private static Material asphaltMaterial;
    private static Material buildingMaterial;
    private static Material roofMaterial;
    private static Material steelMaterial;
    private static Material treeLineMaterial;
    private static Material paleConcreteMaterial;

    public static void Build(Transform environmentRoot, Vector3 center,
        float siteWidth, float siteDepth, float baseY)
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

        BuildContinuousGround(root, center, siteWidth, siteDepth, baseY);
        BuildOuterRoadNetwork(root, center, siteWidth, siteDepth, baseY);
        BuildMidDistanceIndustrialEstate(root, center, siteWidth, siteDepth, baseY);
        BuildFarIndustrialSkyline(root, center, baseY);
        BuildFarLandscapeBelts(root, center, baseY);
        ConfigureAtmosphere();

        MarkStatic(rootObject);

        if (Application.isPlaying)
        {
            StaticBatchingUtility.Combine(rootObject);
        }
    }

    private static void BuildContinuousGround(Transform root, Vector3 center,
        float siteWidth, float siteDepth, float baseY)
    {
        GameObject world = GameObject.CreatePrimitive(PrimitiveType.Plane);
        world.name = "Continuous Horizon Ground";
        world.transform.SetParent(root, false);
        world.transform.position = new Vector3(center.x, baseY - 0.32f, center.z);

        // Unity's built-in plane is 10 x 10 units.
        float planeScale = WorldSize / 10f;
        world.transform.localScale = new Vector3(planeScale, 1f, planeScale);
        ApplyMaterial(world, fieldMaterial);
        RemoveCollider(world);

        // Broad transition apron. It starts underneath the near-site geometry and extends
        // well beyond the perimeter so there is no abrupt concrete -> empty-world change.
        CreateCube("Industrial Estate Ground", root,
            new Vector3(center.x, baseY - 0.24f, center.z),
            new Vector3(
                Mathf.Max(siteWidth + 300f, 420f),
                0.08f,
                Mathf.Max(siteDepth + 250f, 350f)),
            industrialGroundMaterial);
    }

    private static void BuildOuterRoadNetwork(Transform root, Vector3 center,
        float siteWidth, float siteDepth, float baseY)
    {
        float y = baseY - 0.16f;
        float halfW = Mathf.Max(siteWidth * 0.5f + 48f, 105f);
        float halfD = Mathf.Max(siteDepth * 0.5f + 52f, 88f);

        // Main industrial-estate loop immediately outside the current generated site.
        CreateCube("Outer Estate Road North", root,
            new Vector3(center.x, y, center.z + halfD),
            new Vector3(halfW * 2f + 18f, 0.05f, 8f), asphaltMaterial);
        CreateCube("Outer Estate Road South", root,
            new Vector3(center.x, y, center.z - halfD),
            new Vector3(halfW * 2f + 18f, 0.05f, 8f), asphaltMaterial);
        CreateCube("Outer Estate Road East", root,
            new Vector3(center.x + halfW, y, center.z),
            new Vector3(8f, 0.05f, halfD * 2f + 18f), asphaltMaterial);
        CreateCube("Outer Estate Road West", root,
            new Vector3(center.x - halfW, y, center.z),
            new Vector3(8f, 0.05f, halfD * 2f + 18f), asphaltMaterial);

        // A few long regional roads make high-angle views read as a larger industrial zone.
        CreateCube("Regional Road North", root,
            new Vector3(center.x + 20f, y - 0.02f, center.z + 275f),
            new Vector3(760f, 0.04f, 9f), asphaltMaterial);
        CreateCube("Regional Road West", root,
            new Vector3(center.x - 315f, y - 0.02f, center.z - 20f),
            new Vector3(9f, 0.04f, 700f), asphaltMaterial);
        CreateCube("Regional Road Southeast", root,
            new Vector3(center.x + 300f, y - 0.02f, center.z - 210f),
            new Vector3(520f, 0.04f, 8f), asphaltMaterial,
            Quaternion.Euler(0f, 23f, 0f));
    }

    private static void BuildMidDistanceIndustrialEstate(Transform root, Vector3 center,
        float siteWidth, float siteDepth, float baseY)
    {
        float minimumRadius = Mathf.Max(siteWidth, siteDepth) * 0.72f + 55f;

        // 32 low-cost building groups around the full 360 degrees. The small deterministic
        // variation avoids an obvious perfect circle while keeping rebuilds reproducible.
        for (int i = 0; i < 32; i++)
        {
            float angle = i * (360f / 32f) + (Hash01(i, 11, 301) - 0.5f) * 8f;
            float radius = minimumRadius + 30f + Hash01(i, 17, 307) * 155f;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 p = center + direction * radius;

            float width = 10f + Hash01(i, 23, 313) * 18f;
            float depth = 7f + Hash01(i, 29, 317) * 13f;
            float height = 4.5f + Hash01(i, 31, 331) * 8.5f;

            // Keep some sectors lower/open so the scene resembles an industrial park
            // surrounded by fields, not a dense city.
            if (i % 7 == 2 || i % 9 == 5)
                height *= 0.62f;

            CreateIndustrialBuilding("Mid Estate Building " + (i + 1), root,
                new Vector3(p.x, baseY, p.z),
                width, depth, height, angle + 90f);

            if (i % 4 == 0)
            {
                float stackHeight = height + 8f + Hash01(i, 37, 337) * 10f;
                CreateCylinder("Mid Estate Stack " + (i + 1), root,
                    new Vector3(
                        p.x + Mathf.Sin(angle * Mathf.Deg2Rad) * width * 0.3f,
                        baseY + stackHeight * 0.5f,
                        p.z + Mathf.Cos(angle * Mathf.Deg2Rad) * depth * 0.3f),
                    0.45f + Hash01(i, 41, 347) * 0.45f,
                    stackHeight,
                    steelMaterial);
            }
        }

        BuildMidTankClusters(root, center, baseY);
    }

    private static void BuildMidTankClusters(Transform root, Vector3 center, float baseY)
    {
        // Three compact storage/utility clusters are enough to visually identify the
        // surroundings as an industrial estate without confusing them with modeled process units.
        Vector3[] clusterCenters =
        {
            center + new Vector3(210f, 0f, 120f),
            center + new Vector3(-185f, 0f, 175f),
            center + new Vector3(225f, 0f, -145f)
        };

        for (int c = 0; c < clusterCenters.Length; c++)
        {
            Vector3 cluster = clusterCenters[c];

            CreateCube("Mid Tank Bund " + c, root,
                new Vector3(cluster.x, baseY - 0.10f, cluster.z),
                new Vector3(26f, 0.08f, 19f), paleConcreteMaterial);

            for (int i = 0; i < 5; i++)
            {
                float x = cluster.x + (i % 3 - 1) * 7f;
                float z = cluster.z + (i / 3 - 0.5f) * 7.5f;
                float h = 4.2f + Hash01(i, c, 401) * 3.3f;
                float r = 1.7f + Hash01(i, c, 409) * 0.8f;

                CreateCylinder("Mid Storage Tank " + c + "_" + i, root,
                    new Vector3(x, baseY + h * 0.5f, z),
                    r, h, steelMaterial);
                CreateCylinder("Mid Tank Roof " + c + "_" + i, root,
                    new Vector3(x, baseY + h + 0.10f, z),
                    r * 1.03f, 0.20f, roofMaterial);
            }
        }
    }

    private static void BuildFarIndustrialSkyline(Transform root, Vector3 center, float baseY)
    {
        // Beyond ~430 m, silhouette and height variation matter much more than mesh detail.
        // Primitive geometry here is substantially cheaper than importing dozens of distant FBXs.
        for (int i = 0; i < 56; i++)
        {
            float angle = i * (360f / 56f) + (Hash01(i, 53, 503) - 0.5f) * 5f;
            float radius = 430f + Hash01(i, 59, 509) * 360f;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 p = center + direction * radius;

            float width = 15f + Hash01(i, 61, 521) * 34f;
            float depth = 9f + Hash01(i, 67, 523) * 20f;
            float height = 5f + Hash01(i, 71, 541) * 14f;

            // Sparse gaps make the skyline believable and allow the landscape belt to show through.
            if (i % 8 == 3)
                height *= 0.55f;

            CreateCube("Far Industrial Silhouette " + (i + 1), root,
                new Vector3(p.x, baseY + height * 0.5f, p.z),
                new Vector3(width, height, depth),
                buildingMaterial,
                Quaternion.Euler(0f, angle + 90f, 0f));

            if (i % 6 == 0)
            {
                float stackHeight = height + 18f + Hash01(i, 73, 547) * 20f;
                CreateCylinder("Far Chimney " + (i + 1), root,
                    new Vector3(p.x + width * 0.28f, baseY + stackHeight * 0.5f, p.z),
                    0.7f + Hash01(i, 79, 557) * 0.65f,
                    stackHeight,
                    steelMaterial);
            }
        }
    }

    private static void BuildFarLandscapeBelts(Transform root, Vector3 center, float baseY)
    {
        // Broad, irregular low blocks read as distant tree lines once fogged. This gives
        // the horizon vegetation coverage without hundreds of individual tree renderers.
        for (int i = 0; i < 52; i++)
        {
            float angle = i * (360f / 52f) + 2.5f;
            float radius = 650f + Hash01(i, 83, 601) * 210f;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 p = center + direction * radius;

            float width = 34f + Hash01(i, 89, 607) * 58f;
            float depth = 12f + Hash01(i, 97, 613) * 20f;
            float height = 4.0f + Hash01(i, 101, 617) * 5.5f;

            CreateCube("Far Landscape Belt " + (i + 1), root,
                new Vector3(p.x, baseY + height * 0.5f - 0.35f, p.z),
                new Vector3(width, height, depth),
                treeLineMaterial,
                Quaternion.Euler(0f, angle + 90f, 0f));
        }
    }

    private static void CreateIndustrialBuilding(string name, Transform root,
        Vector3 basePosition, float width, float depth, float height, float yaw)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(root, false);

        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

        CreateCube(name + " Body", group.transform,
            new Vector3(basePosition.x, basePosition.y + height * 0.5f, basePosition.z),
            new Vector3(width, height, depth), buildingMaterial, rotation);

        CreateCube(name + " Roof", group.transform,
            new Vector3(basePosition.x, basePosition.y + height + 0.16f, basePosition.z),
            new Vector3(width + 0.6f, 0.30f, depth + 0.6f), roofMaterial, rotation);

        // A small annex breaks up the box shape and makes the estate look less procedural.
        float annexHeight = height * 0.45f;
        Vector3 side = rotation * Vector3.right;
        Vector3 annexPosition = basePosition + side * (width * 0.58f);
        CreateCube(name + " Annex", group.transform,
            new Vector3(annexPosition.x, basePosition.y + annexHeight * 0.5f, annexPosition.z),
            new Vector3(width * 0.28f, annexHeight, depth * 0.55f),
            buildingMaterial, rotation);
    }

    private static void ConfigureAtmosphere()
    {
        Color horizonColor = new Color(0.50f, 0.61f, 0.72f, 1f);

        // Preserve the PlantEnvironmentBuilder's robust solid-colour sky approach. The
        // previous procedural sky had a problematic dark lower hemisphere from elevated views.
        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Camera camera in cameras)
        {
            if (camera == null || camera.targetTexture != null) continue;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = horizonColor;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 1800f);
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = horizonColor;
        RenderSettings.fogStartDistance = FogStart;
        RenderSettings.fogEndDistance = FogEnd;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.57f, 0.65f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.40f, 0.45f, 0.47f);
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.22f, 0.20f);
    }

    private static void CreateMaterials()
    {
        fieldMaterial = MakeMaterial("Horizon Fields", new Color(0.20f, 0.28f, 0.17f), 0.03f);
        industrialGroundMaterial = MakeMaterial("Industrial Estate Ground", new Color(0.17f, 0.22f, 0.17f), 0.03f);
        asphaltMaterial = MakeMaterial("Outer Estate Asphalt", new Color(0.10f, 0.11f, 0.12f), 0.06f);
        buildingMaterial = MakeMaterial("Distant Industrial Building", new Color(0.33f, 0.38f, 0.40f), 0.12f);
        roofMaterial = MakeMaterial("Distant Industrial Roof", new Color(0.19f, 0.22f, 0.24f), 0.20f);
        steelMaterial = MakeMaterial("Distant Industrial Steel", new Color(0.44f, 0.47f, 0.48f), 0.18f);
        treeLineMaterial = MakeMaterial("Distant Tree Line", new Color(0.10f, 0.18f, 0.10f), 0.02f);
        paleConcreteMaterial = MakeMaterial("Distant Concrete", new Color(0.42f, 0.43f, 0.42f), 0.08f);
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

    private static GameObject CreateCube(string name, Transform parent,
        Vector3 position, Vector3 scale, Material material)
    {
        return CreateCube(name, parent, position, scale, material, Quaternion.identity);
    }

    private static GameObject CreateCube(string name, Transform parent,
        Vector3 position, Vector3 scale, Material material, Quaternion rotation)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.position = position;
        cube.transform.rotation = rotation;
        cube.transform.localScale = scale;
        ApplyMaterial(cube, material);
        RemoveCollider(cube);
        return cube;
    }

    private static GameObject CreateCylinder(string name, Transform parent,
        Vector3 position, float radius, float height, Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.position = position;
        cylinder.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        ApplyMaterial(cylinder, material);
        RemoveCollider(cylinder);
        return cylinder;
    }

    private static void ApplyMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
    }

    private static void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider == null) return;

        if (Application.isPlaying) Object.Destroy(collider);
        else Object.DestroyImmediate(collider);
    }

    private static void MarkStatic(GameObject root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
            child.gameObject.isStatic = true;
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 69069);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215f;
        }
    }
}
