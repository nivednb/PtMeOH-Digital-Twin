using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class PtMeOHEnvironmentDecorator : MonoBehaviour
{
    private const string GeneratedEnvironmentRoot = "Generated_Plant_Environment_N";
    private const string DecorationRoot = "External_PtMeOH_Environment_N";

    private Material concreteMaterial;
    private Material asphaltMaterial;
    private Material grassMaterial;
    private Material corrugatedMaterial;
    private Material kenneyIndustrialMaterial;
    private Material kenneyRoadMaterial;
    private Material skyMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<PtMeOHEnvironmentDecorator>() != null)
            return;

        GameObject host = new GameObject("PtMeOH Environment Decorator (Auto)");
        DontDestroyOnLoad(host);
        host.AddComponent<PtMeOHEnvironmentDecorator>();
    }

    private IEnumerator Start()
    {
        // PlantEnvironmentBuilder creates its root on the first runtime frame.
        // Wait briefly so the decorator never races the existing environment code.
        for (int i = 0; i < 30; i++)
        {
            if (GameObject.Find(GeneratedEnvironmentRoot) != null)
                break;
            yield return null;
        }

        DecorateNow();
    }

    [ContextMenu("Decorate PtMeOH Environment")]
    public void DecorateNow()
    {
        ClearDecoration();

        GameObject generated = GameObject.Find(GeneratedEnvironmentRoot);
        if (generated == null)
        {
            Debug.LogWarning("PtMeOHEnvironmentDecorator: generated plant environment was not found.");
            return;
        }

        BuildMaterials();
        RestyleGeneratedEnvironment(generated.transform);

        Bounds siteBounds = CalculateBounds(generated);
        float floorY = FindFloorY(generated, siteBounds);

        GameObject externalRoot = new GameObject(DecorationRoot);

        Texture2D industrialMap = Resources.Load<Texture2D>("PtMeOHEnv/Textures/KenneyIndustrial/colormap");
        Texture2D roadMap = Resources.Load<Texture2D>("PtMeOHEnv/Textures/KenneyRoads/colormap");
        kenneyIndustrialMaterial = CreateLitMaterial("PtMeOH Kenney Industrial", industrialMap, 0.20f, 0.02f);
        kenneyRoadMaterial = CreateLitMaterial("PtMeOH Kenney Road Props", roadMap, 0.18f, 0.02f);

        PlaceIndustrialBackground(externalRoot.transform, siteBounds, floorY);
        PlaceServiceRoadDetails(externalRoot.transform, siteBounds, floorY);
        ConfigureSky();

        Debug.Log("PtMeOHEnvironmentDecorator: industrial surroundings applied. Delete/disable '" +
                  DecorationRoot + "' to remove imported decoration.");
    }

    [ContextMenu("Clear PtMeOH Decoration")]
    public void ClearDecoration()
    {
        GameObject existing = GameObject.Find(DecorationRoot);
        if (existing == null)
            return;

        if (Application.isPlaying)
            Destroy(existing);
        else
            DestroyImmediate(existing);
    }

    private void BuildMaterials()
    {
        Texture2D concrete = CreateNoiseTexture("PtMeOH Concrete", new Color(0.46f, 0.47f, 0.47f), 0.10f, 41);
        Texture2D asphalt = CreateNoiseTexture("PtMeOH Asphalt", new Color(0.13f, 0.14f, 0.15f), 0.12f, 77);
        Texture2D grass = CreateNoiseTexture("PtMeOH Grass", new Color(0.20f, 0.29f, 0.20f), 0.15f, 103);
        Texture2D corrugated = CreateCorrugatedTexture();

        concreteMaterial = CreateLitMaterial("PtMeOH Concrete Material", concrete, 0.16f, 0.00f);
        asphaltMaterial = CreateLitMaterial("PtMeOH Asphalt Material", asphalt, 0.08f, 0.00f);
        grassMaterial = CreateLitMaterial("PtMeOH Grass Material", grass, 0.05f, 0.00f);
        corrugatedMaterial = CreateLitMaterial("PtMeOH Corrugated Metal", corrugated, 0.30f, 0.35f);
    }

    private void RestyleGeneratedEnvironment(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            string n = renderer.gameObject.name.ToLowerInvariant();
            Material source = null;

            if (n.Contains("grass apron"))
                source = grassMaterial;
            else if (n.Contains("road") && !n.Contains("lane dash"))
                source = asphaltMaterial;
            else if (n.Contains("concrete") || n.Contains("equipment pad") ||
                     n.Contains("containment floor") || n.Contains("plinth"))
                source = concreteMaterial;
            else if (n.Contains("process hall") || n.Contains("workshop") ||
                     n.Contains("compressor shelter") || n.Contains("control room") ||
                     n.Contains("utility skid") || n.Contains("boundary wall"))
                source = corrugatedMaterial;

            if (source == null)
                continue;

            Material instance = new Material(source);
            instance.name = source.name + " - " + renderer.gameObject.name;

            Bounds b = renderer.bounds;
            float tx = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) / 4f, 1f, 12f);
            float ty = Mathf.Clamp(Mathf.Max(b.size.y, Mathf.Min(b.size.x, b.size.z)) / 3f, 1f, 8f);
            SetTextureScale(instance, new Vector2(tx, ty));

            renderer.sharedMaterial = instance;
        }
    }

    private void PlaceIndustrialBackground(Transform parent, Bounds site, float floorY)
    {
        float w = site.size.x;
        float d = site.size.z;
        Vector3 c = site.center;

        PlaceModel("PtMeOHEnv/Models/Industrial/building-a", parent,
            new Vector3(c.x - w * 0.36f, floorY, c.z + d * 0.56f), 7.0f, 180f, kenneyIndustrialMaterial, "Imported Industrial Hall A");

        PlaceModel("PtMeOHEnv/Models/Industrial/building-f", parent,
            new Vector3(c.x - w * 0.13f, floorY, c.z + d * 0.60f), 5.8f, 180f, kenneyIndustrialMaterial, "Imported Industrial Hall B");

        PlaceModel("PtMeOHEnv/Models/Industrial/building-m", parent,
            new Vector3(c.x + w * 0.18f, floorY, c.z + d * 0.61f), 7.5f, 180f, kenneyIndustrialMaterial, "Imported Utility Building");

        PlaceModel("PtMeOHEnv/Models/Industrial/building-t", parent,
            new Vector3(c.x + w * 0.39f, floorY, c.z + d * 0.53f), 6.5f, 205f, kenneyIndustrialMaterial, "Imported Warehouse");

        PlaceModel("PtMeOHEnv/Models/Industrial/detail-tank", parent,
            new Vector3(c.x + w * 0.45f, floorY, c.z - d * 0.25f), 3.8f, 0f, kenneyIndustrialMaterial, "Imported Yard Tank 1");

        PlaceModel("PtMeOHEnv/Models/Industrial/detail-tank", parent,
            new Vector3(c.x + w * 0.45f, floorY, c.z - d * 0.36f), 3.2f, 0f, kenneyIndustrialMaterial, "Imported Yard Tank 2");
    }

    private void PlaceServiceRoadDetails(Transform parent, Bounds site, float floorY)
    {
        float frontZ = site.center.z - site.size.z * 0.47f;
        float span = site.size.x * 0.72f;

        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            float x = site.center.x - span * 0.5f + span * t;
            float yaw = i < 3 ? 90f : -90f;

            PlaceModel("PtMeOHEnv/Models/Roads/light-curved", parent,
                new Vector3(x, floorY, frontZ), 5.4f, yaw, kenneyRoadMaterial,
                "Imported Service Light " + (i + 1));
        }
    }

    private GameObject PlaceModel(string resourcePath, Transform parent, Vector3 groundPosition,
                                  float targetHeight, float yaw, Material material, string objectName)
    {
        GameObject source = Resources.Load<GameObject>(resourcePath);
        if (source == null)
        {
            Debug.LogWarning("PtMeOHEnvironmentDecorator: missing resource " + resourcePath);
            return null;
        }

        GameObject instance = Instantiate(source, parent);
        instance.name = objectName;
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        instance.transform.localScale = Vector3.one;

        Bounds before = CalculateBounds(instance);
        if (before.size.y > 0.001f)
        {
            float scale = targetHeight / before.size.y;
            instance.transform.localScale = Vector3.one * scale;
        }

        Bounds after = CalculateBounds(instance);
        instance.transform.position += new Vector3(
            groundPosition.x - after.center.x,
            groundPosition.y - after.min.y,
            groundPosition.z - after.center.z);

        ApplyMaterial(instance, material);
        return instance;
    }

    private void ApplyMaterial(GameObject target, Material material)
    {
        if (material == null)
            return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = material;
                continue;
            }

            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            renderer.sharedMaterials = materials;
        }
    }

    private void ConfigureSky()
    {
        Shader shader = Shader.Find("Skybox/Panoramic");
        if (shader == null)
            return;

        Texture2D skyTexture = CreateSkyTexture();
        skyMaterial = new Material(shader);
        skyMaterial.name = "PtMeOH Soft Industrial Sky";
        skyMaterial.SetTexture("_MainTex", skyTexture);
        if (skyMaterial.HasProperty("_Exposure"))
            skyMaterial.SetFloat("_Exposure", 0.85f);

        RenderSettings.skybox = skyMaterial;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.66f, 0.73f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.46f, 0.49f);
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.22f, 0.22f);

        foreach (Camera camera in Camera.allCameras)
        {
            if (camera != null && camera.targetTexture == null)
                camera.clearFlags = CameraClearFlags.Skybox;
        }
    }

    private Texture2D CreateNoiseTexture(string textureName, Color baseColor, float variation, int seed)
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, true);
        texture.name = textureName;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fine = Hash01(x, y, seed) - 0.5f;
                float broad = Mathf.PerlinNoise((x + seed) * 0.055f, (y + seed) * 0.055f) - 0.5f;
                float k = 1f + fine * variation + broad * variation * 0.9f;
                pixels[y * size + x] = new Color(
                    Mathf.Clamp01(baseColor.r * k),
                    Mathf.Clamp01(baseColor.g * k),
                    Mathf.Clamp01(baseColor.b * k),
                    1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);
        return texture;
    }

    private Texture2D CreateCorrugatedTexture()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, true);
        texture.name = "PtMeOH Corrugated Metal";
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Color baseColor = new Color(0.42f, 0.48f, 0.52f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float ridge = 0.86f + 0.14f * (0.5f + 0.5f * Mathf.Sin(x * Mathf.PI * 0.25f));
                float noise = (Hash01(x, y, 211) - 0.5f) * 0.05f;
                float k = ridge + noise;
                pixels[y * size + x] = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);
        return texture;
    }

    private Texture2D CreateSkyTexture()
    {
        const int width = 512;
        const int height = 256;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.name = "PtMeOH Overcast Industrial Sky";
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        Color top = new Color(0.28f, 0.40f, 0.54f);
        Color horizon = new Color(0.68f, 0.72f, 0.74f);
        Color lower = new Color(0.34f, 0.38f, 0.40f);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = y / (height - 1f);
            Color baseSky = v >= 0.5f
                ? Color.Lerp(horizon, top, (v - 0.5f) * 2f)
                : Color.Lerp(lower, horizon, v * 2f);

            for (int x = 0; x < width; x++)
            {
                float cloud = Mathf.PerlinNoise(x * 0.012f, y * 0.025f);
                float cloudMask = Mathf.SmoothStep(0.48f, 0.72f, cloud) * (0.05f + 0.09f * v);
                Color c = Color.Lerp(baseSky, Color.white, cloudMask);
                pixels[y * width + x] = c;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private Material CreateLitMaterial(string materialName, Texture2D texture, float smoothness, float metallic)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = materialName;

        if (texture != null)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            else
                material.mainTexture = texture;
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        else
            material.color = Color.white;

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);

        return material;
    }

    private void SetTextureScale(Material material, Vector2 scale)
    {
        if (material.HasProperty("_BaseMap"))
            material.SetTextureScale("_BaseMap", scale);
        else
            material.mainTextureScale = scale;
    }

    private Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(target.transform.position, Vector3.zero);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private float FindFloorY(GameObject environmentRoot, Bounds fallback)
    {
        foreach (Renderer renderer in environmentRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && renderer.gameObject.name == "Main Concrete Plant Slab")
                return renderer.bounds.max.y;
        }

        return fallback.min.y;
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
    [UnityEditor.MenuItem("Tools/Nived/PtMeOH Environment/Build Preview")]
    private static void BuildPreview()
    {
        PlantEnvironmentBuilder builder = FindAnyObjectByType<PlantEnvironmentBuilder>();
        if (builder != null)
            builder.BuildEnvironment();

        PtMeOHEnvironmentDecorator decorator = FindAnyObjectByType<PtMeOHEnvironmentDecorator>();
        if (decorator == null)
        {
            GameObject host = new GameObject("PtMeOH Environment Decorator (Preview)");
            decorator = host.AddComponent<PtMeOHEnvironmentDecorator>();
        }

        decorator.DecorateNow();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    [UnityEditor.MenuItem("Tools/Nived/PtMeOH Environment/Clear Preview")]
    private static void ClearPreview()
    {
        PtMeOHEnvironmentDecorator decorator = FindAnyObjectByType<PtMeOHEnvironmentDecorator>();
        if (decorator != null)
            decorator.ClearDecoration();
    }
#endif
}
