using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Low-cost reactor cutaway: transparent vessel wall plus bounded feed,
/// conversion and product tracers. It deliberately stays below 260 live
/// particles and exists only in Play mode.
/// </summary>
public sealed class LightweightReactorVisual : MonoBehaviour
{
    const string RootName = "Lightweight Reactor Cutaway";

    public static void Configure(GameObject owner)
    {
        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Renderer shell = null, top = null, bottom = null, catalyst = null, inlet = null, outlet = null;
        foreach (GameObject obj in objects)
        {
            if (!obj.scene.IsValid()) continue;
            if (obj.name.Equals("Reactor_Shell", StringComparison.OrdinalIgnoreCase)) shell = obj.GetComponent<Renderer>();
            else if (obj.name.Equals("Cap_Top", StringComparison.OrdinalIgnoreCase)) top = obj.GetComponent<Renderer>();
            else if (obj.name.Equals("Cap_Bottom", StringComparison.OrdinalIgnoreCase)) bottom = obj.GetComponent<Renderer>();
            else if (obj.name.Equals("Catalyst_Bed", StringComparison.OrdinalIgnoreCase)) catalyst = obj.GetComponent<Renderer>();
            else if (obj.name.Equals("Nozzle_Feed_Inlet", StringComparison.OrdinalIgnoreCase)) inlet = obj.GetComponent<Renderer>();
            else if (obj.name.Equals("Nozzle_Product_Outlet", StringComparison.OrdinalIgnoreCase)) outlet = obj.GetComponent<Renderer>();
        }
        if (shell == null || catalyst == null) return;
        MakeTransparent(shell, .16f);
        MakeTransparent(top, .18f);
        MakeTransparent(bottom, .18f);

        GameObject existing = GameObject.Find(RootName);
        if (existing != null) UnityEngine.Object.Destroy(existing);
        GameObject root = new(RootName);
        // Bounds below are already measured in world space. Keeping this root
        // outside the imported reactor hierarchy prevents its 3x model scale
        // from multiplying the particle volume and displacing it below the bed.
        root.transform.SetParent(null, false);
        root.transform.position = catalyst.bounds.center;

        Bounds bed = catalyst.bounds;
        Bounds vessel = shell.bounds;
        // Use the smaller of the catalyst and shell cross-sections, leaving room for the
        // billboard size. The imported bed is cylindrical; a square emitter's corners
        // reached beyond its wall even when its centre and nominal radius were inside.
        float radius = Mathf.Min(bed.extents.x, bed.extents.z, vessel.extents.x, vessel.extents.z) * .72f;
        float half = bed.extents.y;
        Vector3 side = inlet == null ? Vector3.right : inlet.bounds.center - bed.center;
        side.y = 0f;
        side = side.sqrMagnitude < .0001f ? Vector3.right : side.normalized;
        float feedY = inlet == null ? -half * .35f :
            Mathf.Clamp(inlet.bounds.center.y - bed.center.y, -half * .65f, half * .15f);
        float topY = Mathf.Min(vessel.max.y, outlet == null ? vessel.max.y : outlet.bounds.center.y)
            - bed.center.y - .12f;
        topY = Mathf.Max(half * .70f, topY);
        Color h2 = PlantStreamLegend.WaterHydrogen;
        Color co2 = PlantStreamLegend.AmineCapturedCo2;
        Color syngas = PlantStreamLegend.CompressedSyngas;
        Color crude = PlantStreamLegend.CrudeMethanol;
        Color hot = PlantStreamLegend.HotReactorEffluent;

        // Short radial entry at the actual side nozzle, followed by upflow inside the bed.
        Vector3 sideStart = side * (radius * .70f) + Vector3.up * feedY;
        Vector3 sideEnd = side * (radius * .10f) + Vector3.up * feedY;
        CreateStream(root.transform, "H2 from side inlet", Fade(h2, .95f),
            sideStart, sideEnd, radius * .09f, 22f, .064f, 1.05f);
        CreateStream(root.transform, "CO2 from side inlet", Fade(co2, .95f),
            sideStart, sideEnd, radius * .09f, 19f, .066f, 1.0f);
        CreateStream(root.transform, "Recycle from side inlet", Fade(syngas, .88f),
            sideStart, sideEnd, radius * .08f, 10f, .061f, .96f);
        CreateStream(root.transform, "Catalyst conversion", Fade(hot, .92f),
            Vector3.up * feedY, Vector3.up * (half * .70f), radius * .66f, 48f, .080f, .72f);
        CreateStream(root.transform, "Methanol vapour to top outlet", Fade(crude, .95f),
            Vector3.up * (half * .55f), Vector3.up * topY, radius * .37f, 18f, .073f, .90f);
        CreateStream(root.transform, "Water vapour to top outlet", Fade(h2, .92f),
            Vector3.up * (half * .55f), Vector3.up * topY, radius * .35f, 14f, .069f, .86f);
        CreateStream(root.transform, "Unreacted gas to top outlet", Fade(syngas, .62f),
            Vector3.up * (half * .55f), Vector3.up * topY, radius * .32f, 8f, .057f, .94f);
    }

    static Color Fade(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

    static void MakeTransparent(Renderer renderer, float alpha)
    {
        if (renderer == null) return;
        Material source = renderer.sharedMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material material = source != null ? new Material(source) : new Material(shader);
        material.name = "ReactorCutaway_Transparent";
        Color color = new(.70f, .88f, 1f, alpha);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    /// <summary>
    /// A straight tracer stream inside the vessel, with a circular emitter perpendicular
    /// to its direction. Lifetime is based on the available distance along that direction.
    /// </summary>
    static void CreateStream(Transform parent, string name, Color color, Vector3 start,
        Vector3 end, float radius, float rate, float size, float velocity)
    {
        Vector3 direction = (end - start).normalized;
        float usableTravel = Mathf.Max(.05f, Vector3.Distance(start, end) - size * .7f);

        GameObject child = new(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = start;
        child.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, direction);
        ParticleSystem ps = child.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = usableTravel / velocity;
        main.startSpeed = velocity;
        main.startSize = new ParticleSystem.MinMaxCurve(size*.72f, size*1.35f);
        main.startColor = color;
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader != null)
        {
            Material material = new(particleShader) { name = $"ReactorTracer_{name}" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            renderer.sharedMaterial = material;
        }
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
    }
}
