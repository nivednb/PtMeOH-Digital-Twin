using System.Collections;
using UnityEngine;

/// <summary>
/// Extends the PtMeOH scene beyond the immediate PlantEnvironmentBuilder site.
/// Creates a 360-degree mid-distance industrial estate, far skyline, fields,
/// tree belts and atmospheric horizon so no camera orbit sees a hard world edge.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1100)]
public sealed class PtMeOHHorizonEnvironment : MonoBehaviour
{
    private const string NearRoot = "Generated_Plant_Environment_N";
    private const string HorizonRoot = "PtMeOH_Horizon_World_N";

    // Geometry extends farther than fog so the actual map edge is never visible.
    private const float WorldHalfExtent = 1200f;
    private const float MidInner = 120f;
    private const float MidOuter = 310f;
    private const float FarInner = 430f;
    private const float FarOuter = 820f;

    private Material fieldMat;
    private Material asphaltMat;
    private Material distantBuildingMat;
    private Material distantSteelMat;
    private Material treeMat;
    private Material importedIndustrialMat;
    private Material skyMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<PtMeOHHorizonEnvironment>() != null)
            return;

        GameObject host = new GameObject("PtMeOH Horizon Environment (Auto)");
        DontDestroyOnLoad(host);
        host.AddComponent<PtMeOHHorizonEnvironment>();
    }

    private IEnumerator Start()
    {
        // Wait for the original near-site builder and its runtime-generated geometry.
        for (int i = 0; i < 45; i++)
        {
            if (GameObject.Find(NearRoot) != null)
                break;
            yield return null;
        }

        BuildHorizonWorld();
    }

    [ContextMenu("Build PtMeOH Horizon World")]
    public void BuildHorizonWorld()
    {
        ClearHorizonWorld();

        GameObject near = GameObject.Find(NearRoot);
        if (near == null)
        {
            Debug.LogWarning("PtMeOHHorizonEnvironment: near plant environment not found.");
            return;
        }

        Bounds site = CalculateBounds(near);
        float floorY = FindFloorY(near, site);
        Vector3 center = new Vector3(site.center.x, floorY, site.center.z);

        BuildMaterials();

        GameObject root = new GameObject(HorizonRoot);

        CreateContinuousGround(root.transform, center, floorY);
        CreateOuterRoadGrid(root.transform, center, floorY);
        CreateMidDistanceEstate(root.transform, center, floorY);
        CreateFarSkyline(root.transform, center, floorY);
        CreateTreeBelts(root.transform, center, floorY);
        ConfigureAtmosphere();

        MarkStatic(root);
        if (Application.isPlaying)
            StaticBatchingUtility.Combine(root);

        Debug.Log(
            "PtMeOHHorizonEnvironment: full 360-degree surroundings created. " +
            "Detailed plant remains near the origin; horizon geometry extends beyond the fog distance.");
    }

    [ContextMenu("Clear PtMeOH Horizon World")]
    public void ClearHorizonWorld()
    {
        GameObject existing = GameObject.Find(HorizonRoot);
        if (existing == null)
            return;

        if (Application.isPlaying)
            Destroy(existing);
        else
            DestroyImmediate(existing);
    }

    private void BuildMaterials()
    {
        Texture2D fieldTexture = MakeNoiseTexture(
            "PtMeOH Far Ground", new Color(0.27f, 0.32f, 0.20f), 0.18f, 133);
        Texture2D asphaltTexture = MakeNoiseTexture(
            "PtMeOH Outer Asphalt", new Color(0.12f, 0.13f, 0.14f), 0.10f, 177);

        fieldMat = CreateLit("PtMeOH Horizon Ground", fieldTexture,
            Color.white, 0.03f, 0f, new Vector2(170f, 170f));
        asphaltMat = CreateLit("PtMeOH Outer Roads", asphaltTexture,
            Color.white, 0.07f, 0f, new Vector2(30f, 2f));

        distantBuildingMat = CreateLit("PtMeOH Far Industrial",
            null, new Color(0.34f, 0.38f, 0.39f), 0.10f, 0.03f, Vector2.one);
        distantSteelMat = CreateLit("PtMeOH Far Steel",
            null, new Color(0.42f, 0.44f, 0.45f), 0.18f, 0.16f, Vector2.one);
        treeMat = CreateLit("PtMeOH Far Vegetation",
            null, new Color(0.12f, 0.19f, 0.12f), 0.02f, 0f, Vector2.one);

        Texture2D industrialPalette =
            Resources.Load<Texture2D>("PtMeOHEnv/Textures/KenneyIndustrial/colormap");
        importedIndustrialMat = CreateLit("PtMeOH Mid Industrial",
            industrialPalette, Color.white, 0.18f, 0.02f, Vector2.one);
    }

    private void CreateContinuousGround(Transform root, Vector3 center, float floorY)
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Horizon Ground 2400m";
        plane.transform.SetParent(root);
        plane.transform.position = new Vector3(center.x, floorY - 0.20f, center.z);
        // Unity plane = 10 m, so scale 240 produces a 2400 m square.
        plane.transform.localScale = new Vector3(WorldHalfExtent / 5f, 1f, WorldHalfExtent / 5f);
        plane.GetComponent<Renderer>().sharedMaterial = fieldMat;
        RemoveCollider(plane);

        // A broad industrial/grass transition apron prevents a visual jump immediately
        // outside the original 78-118 m near-site builder.
        CreateCube("Midground Apron", root,
            new Vector3(center.x, floorY - 0.14f, center.z),
            new Vector3(430f, 0.04f, 350f),
            CreateLit("PtMeOH Industrial Verge", null,
                new Color(0.19f, 0.25f, 0.18f), 0.03f, 0f, Vector2.one));
    }

    private void CreateOuterRoadGrid(Transform root, Vector3 c, float floorY)
    {
        float y = floorY - 0.09f;

        // Industrial-estate loop outside the detailed plant.
        CreateCube("Estate Road North", root, new Vector3(c.x, y, c.z + 125f),
            new Vector3(300f, 0.025f, 7f), asphaltMat);
        CreateCube("Estate Road South", root, new Vector3(c.x, y, c.z - 125f),
            new Vector3(300f, 0.025f, 7f), asphaltMat);
        CreateCube("Estate Road East", root, new Vector3(c.x + 147f, y, c.z),
            new Vector3(7f, 0.025f, 255f), asphaltMat);
        CreateCube("Estate Road West", root, new Vector3(c.x - 147f, y, c.z),
            new Vector3(7f, 0.025f, 255f), asphaltMat);

        // Regional roads visible from high/elevated camera angles.
        CreateCube("Regional Road North", root, new Vector3(c.x, y - 0.01f, c.z + 275f),
            new Vector3(820f, 0.02f, 9f), asphaltMat);
        CreateCube("Regional Road West", root, new Vector3(c.x - 330f, y - 0.01f, c.z),
            new Vector3(9f, 0.02f, 760f), asphaltMat);
    }

    private void CreateMidDistanceEstate(Transform root, Vector3 center, float floorY)
    {
        string[] models =
        {
            "PtMeOHEnv/Models/Industrial/building-a",
            "PtMeOHEnv/Models/Industrial/building-f",
            "PtMeOHEnv/Models/Industrial/building-m",
            "PtMeOHEnv/Models/Industrial/building-t"
        };

        // A complete 360-degree ring of recognizable low-poly industrial buildings.
        for (int i = 0; i < 32; i++)
        {
            float angle = i * (360f / 32f) + (Hash01(i, 1, 311) - 0.5f) * 7f;
            float radius = Mathf.Lerp(MidInner, MidOuter, 0.15f + 0.85f * Hash01(i, 2, 313));
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 p = center + dir * radius;

            float height = 5.5f + Hash01(i, 3, 317) * 10f;
            PlaceModel(models[i % models.Length], root,
                new Vector3(p.x, floorY, p.z),
                height, angle + 180f,
                "Mid Industrial Building " + (i + 1));
        }

        // Distributed tank storage.
        for (int i = 0; i < 12; i++)
        {
            float angle = 12f + i * 30f;
            float radius = 165f + Hash01(i, 4, 331) * 120f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 p = center + dir * radius;

            PlaceModel("PtMeOHEnv/Models/Industrial/detail-tank", root,
                new Vector3(p.x, floorY, p.z),
                3.5f + Hash01(i, 5, 337) * 4.5f,
                angle,
                "Mid Storage Tank " + (i + 1));
        }

        // Taller stacks and utility columns create depth and break up the roofline.
        for (int i = 0; i < 20; i++)
        {
            float angle = 5f + i * 18f;
            float radius = 155f + Hash01(i, 6, 347) * 145f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 p = center + dir * radius;
            float h = 9f + Hash01(i, 7, 349) * 17f;

            CreateCylinder("Mid Utility Stack " + (i + 1), root,
                new Vector3(p.x, floorY + h * 0.5f, p.z),
                0.45f + Hash01(i, 8, 353) * 0.9f,
                h, distantSteelMat);
        }
    }

    private void CreateFarSkyline(Transform root, Vector3 center, float floorY)
    {
        // Simplified distant factories are intentionally cheap. Beyond ~430 m,
        // silhouettes matter more than close-up mesh detail.
        for (int i = 0; i < 60; i++)
        {
            float angle = i * 6f + (Hash01(i, 9, 367) - 0.5f) * 4f;
            float radius = Mathf.Lerp(FarInner, FarOuter, Hash01(i, 10, 373));
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 p = center + dir * radius;

            float w = 14f + Hash01(i, 11, 379) * 38f;
            float d = 9f + Hash01(i, 12, 383) * 24f;
            float h = 5f + Hash01(i, 13, 389) * 17f;

            CreateCube("Far Factory " + (i + 1), root,
                new Vector3(p.x, floorY + h * 0.5f, p.z),
                new Vector3(w, h, d),
                distantBuildingMat,
                Quaternion.Euler(0f, angle + 90f, 0f));

            if (i % 5 == 0)
            {
                float sh = h + 17f + Hash01(i, 14, 397) * 24f;
                CreateCylinder("Far Chimney " + (i + 1), root,
                    new Vector3(p.x + w * 0.28f, floorY + sh * 0.5f, p.z),
                    0.7f + Hash01(i, 15, 401) * 0.9f,
                    sh, distantSteelMat);
            }
        }
    }

    private void CreateTreeBelts(Transform root, Vector3 center, float floorY)
    {
        // A second overlapping ring hides gaps between far factories and gives the
        // horizon a believable mix of industrial land and vegetation.
        for (int i = 0; i < 56; i++)
        {
            float angle = i * (360f / 56f) + 2.5f;
            float radius = 640f + Hash01(i, 16, 409) * 235f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 p = center + dir * radius;

            float w = 35f + Hash01(i, 17, 419) * 65f;
            float d = 11f + Hash01(i, 18, 421) * 21f;
            float h = 4.5f + Hash01(i, 19, 431) * 6f;

            CreateCube("Far Tree Belt " + (i + 1), root,
                new Vector3(p.x, floorY + h * 0.5f - 0.3f, p.z),
                new Vector3(w, h, d),
                treeMat,
                Quaternion.Euler(0f, angle + 90f, 0f));
        }
    }

    private void ConfigureAtmosphere()
    {
        Color fogColor = new Color(0.61f, 0.66f, 0.68f);

        Shader panoramic = Shader.Find("Skybox/Panoramic");
        if (panoramic != null)
        {
            skyMat = new Material(panoramic);
            skyMat.name = "PtMeOH Horizon Sky";
            skyMat.SetTexture("_MainTex", MakeSkyTexture());
            if (skyMat.HasProperty("_Exposure"))
                skyMat.SetFloat("_Exposure", 0.86f);
            RenderSettings.skybox = skyMat;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.57f, 0.65f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.43f, 0.47f, 0.49f);
        RenderSettings.ambientGroundColor = new Color(0.21f, 0.22f, 0.21f);

        // Geometry continues beyond this distance. Fog therefore removes the actual
        // world boundary and blends ground + skyline naturally into the sky.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = 270f;
        RenderSettings.fogEndDistance = 1050f;

        foreach (Camera cam in Camera.allCameras)
        {
            if (cam == null || cam.targetTexture != null)
                continue;

            if (skyMat != null)
                cam.clearFlags = CameraClearFlags.Skybox;

            cam.backgroundColor = fogColor;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 1800f);
        }
    }

    private GameObject PlaceModel(string resource, Transform parent, Vector3 ground,
                                  float targetHeight, float yaw, string name)
    {
        GameObject source = Resources.Load<GameObject>(resource);
        if (source == null)
        {
            Debug.LogWarning("PtMeOHHorizonEnvironment: missing resource " + resource);
            return null;
        }

        GameObject go = Instantiate(source, parent);
        go.name = name;
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one;

        Bounds b = CalculateBounds(go);
        if (b.size.y > 0.001f)
            go.transform.localScale = Vector3.one * (targetHeight / b.size.y);

        b = CalculateBounds(go);
        go.transform.position += new Vector3(
            ground.x - b.center.x,
            ground.y - b.min.y,
            ground.z - b.center.z);

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            if (mats.Length == 0)
            {
                r.sharedMaterial = importedIndustrialMat;
            }
            else
            {
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = importedIndustrialMat;
                r.sharedMaterials = mats;
            }
        }

        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
            DestroySafe(c);

        return go;
    }

    private GameObject CreateCube(string name, Transform parent, Vector3 pos,
                                  Vector3 scale, Material mat)
    {
        return CreateCube(name, parent, pos, scale, mat, Quaternion.identity);
    }

    private GameObject CreateCube(string name, Transform parent, Vector3 pos,
                                  Vector3 scale, Material mat, Quaternion rot)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.rotation = rot;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        RemoveCollider(go);
        return go;
    }

    private GameObject CreateCylinder(string name, Transform parent, Vector3 pos,
                                      float radius, float height, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        RemoveCollider(go);
        return go;
    }

    private Material CreateLit(string name, Texture2D tex, Color color,
                               float smoothness, float metallic, Vector2 tiling)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = name;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else
            mat.color = color;

        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", tiling);
            }
            else
            {
                mat.mainTexture = tex;
                mat.mainTextureScale = tiling;
            }
        }

        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", metallic);

        return mat;
    }

    private Texture2D MakeNoiseTexture(string name, Color baseColor, float variation, int seed)
    {
        const int size = 128;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGB24, true);
        t.name = name;
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fine = Hash01(x, y, seed) - 0.5f;
                float broad = Mathf.PerlinNoise((x + seed) * 0.05f, (y + seed) * 0.05f) - 0.5f;
                float k = 1f + fine * variation + broad * variation;
                pixels[y * size + x] = new Color(
                    Mathf.Clamp01(baseColor.r * k),
                    Mathf.Clamp01(baseColor.g * k),
                    Mathf.Clamp01(baseColor.b * k));
            }
        }

        t.SetPixels(pixels);
        t.Apply(true, false);
        return t;
    }

    private Texture2D MakeSkyTexture()
    {
        const int w = 512;
        const int h = 256;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGB24, false);
        t.name = "PtMeOH Industrial Horizon Sky";
        t.wrapMode = TextureWrapMode.Repeat;

        Color top = new Color(0.30f, 0.42f, 0.55f);
        Color horizon = new Color(0.66f, 0.70f, 0.72f);
        Color lower = new Color(0.35f, 0.38f, 0.39f);
        Color[] px = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float v = y / (h - 1f);
            Color baseSky = v >= 0.5f
                ? Color.Lerp(horizon, top, (v - 0.5f) * 2f)
                : Color.Lerp(lower, horizon, v * 2f);

            for (int x = 0; x < w; x++)
            {
                float cloud = Mathf.PerlinNoise(x * 0.012f, y * 0.025f);
                float mask = Mathf.SmoothStep(0.48f, 0.74f, cloud) * (0.04f + 0.10f * v);
                px[y * w + x] = Color.Lerp(baseSky, Color.white, mask);
            }
        }

        t.SetPixels(px);
        t.Apply(false, false);
        return t;
    }

    private Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        Bounds b = new Bounds(target.transform.position, Vector3.zero);

        foreach (Renderer r in renderers)
        {
            if (r == null)
                continue;

            if (!has)
            {
                b = r.bounds;
                has = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        return b;
    }

    private float FindFloorY(GameObject root, Bounds fallback)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r != null && r.gameObject.name == "Main Concrete Plant Slab")
                return r.bounds.max.y;
        }
        return fallback.min.y;
    }

    private void RemoveCollider(GameObject go)
    {
        Collider c = go.GetComponent<Collider>();
        if (c != null)
            DestroySafe(c);
    }

    private void DestroySafe(Object obj)
    {
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private void MarkStatic(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.isStatic = true;
    }

    private float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 69069);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215f;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Nived/PtMeOH Environment/Build Horizon Preview")]
    private static void BuildPreview()
    {
        PtMeOHHorizonEnvironment env = FindAnyObjectByType<PtMeOHHorizonEnvironment>();
        if (env == null)
        {
            GameObject host = new GameObject("PtMeOH Horizon Environment (Preview)");
            env = host.AddComponent<PtMeOHHorizonEnvironment>();
        }

        env.BuildHorizonWorld();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    [UnityEditor.MenuItem("Tools/Nived/PtMeOH Environment/Clear Horizon Preview")]
    private static void ClearPreview()
    {
        PtMeOHHorizonEnvironment env = FindAnyObjectByType<PtMeOHHorizonEnvironment>();
        if (env != null)
            env.ClearHorizonWorld();
    }
#endif
}
