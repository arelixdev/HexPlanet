using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Gestionnaire d'éclairage de la planète.
/// — Soleil  : lumière directionnelle + sphère visuelle, orbite autour de la planète.
/// — Lune    : lumière directionnelle secondaire + sphère visuelle, orbite plus proche.
///
/// SETUP :
///  1. Ajouter ce script sur le même GameObject que HexPlanetGenerator.
///  2. Assigner PlanetTransform (= transform de la planète).
///  3. Laisser SunLight / MoonLight vides → créés automatiquement.
/// </summary>
[ExecuteAlways]
public class PlanetLightingManager : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    // REFERENCES
    // ══════════════════════════════════════════════════════════════
    [Header("Planet")]
    [Tooltip("Transform racine de la planète (même que HexPlanetGenerator).")]
    public Transform PlanetTransform;
    public float     PlanetRadius = 5f;

    // ══════════════════════════════════════════════════════════════
    // SOLEIL – LUMIÈRE
    // ══════════════════════════════════════════════════════════════
    [Header("Sun – Light")]
    [Tooltip("Laisser vide : créé automatiquement.")]
    public Light SunLight;
    [ColorUsage(true, true)] public Color SunColor     = new Color(1.05f, 0.95f, 0.80f, 1f);
    [Range(0.5f, 5f)]  public float SunIntensity       = 1.6f;
    [Range(5f, 80f)]   public float SunOrbitRadius     = 30f;
    [Range(0f, 360f)]  public float SunOrbitSpeed      = 6f;
    [Range(0f, 360f)]  public float SunInitialAngle    = 45f;
    [Range(-45f, 45f)] public float SunTiltAngle       = 20f;

    [Header("Sun – Shadows")]
    public LightShadows SunShadowType                  = LightShadows.Soft;
    [Range(0f,    1f)]    public float SunShadowStrength = 0.85f;
    [Range(0.001f,0.05f)] public float SunShadowBias    = 0.008f;
    [Range(0.001f,0.05f)] public float SunNormalBias    = 0.012f;

    // ══════════════════════════════════════════════════════════════
    // SOLEIL – SPHÈRE VISUELLE
    // ══════════════════════════════════════════════════════════════
    [Header("Sun – Visual Sphere")]
    [Range(0.2f, 6f)]  public float SunSphereRadius    = 1.8f;
    [ColorUsage(true, true)] public Color SunSphereColor = new Color(1.6f, 1.1f, 0.25f, 1f);
    [Tooltip("Optionnel : material custom Unlit. Sinon généré automatiquement.")]
    public Material SunSphereMaterial;

    // ══════════════════════════════════════════════════════════════
    // LUNE – LUMIÈRE
    // ══════════════════════════════════════════════════════════════
    [Header("Moon – Light")]
    [Tooltip("Laisser vide : créé automatiquement.")]
    public Light MoonLight;
    [ColorUsage(true, true)] public Color MoonColor    = new Color(0.35f, 0.42f, 0.62f, 1f);
    [Range(0f, 1f)]    public float MoonIntensity      = 0.18f;
    [Range(3f, 30f)]   public float MoonOrbitRadius    = 18f;
    [Range(0f, 360f)]  public float MoonOrbitSpeed     = 14f;
    [Range(0f, 360f)]  public float MoonInitialAngle   = 220f;
    [Range(-60f, 60f)] public float MoonTiltAngle      = -30f;

    [Header("Moon – Shadows")]
    public LightShadows MoonShadowType                 = LightShadows.None;
    [Range(0f, 1f)] public float MoonShadowStrength    = 0.25f;

    // ══════════════════════════════════════════════════════════════
    // LUNE – SPHÈRE VISUELLE
    // ══════════════════════════════════════════════════════════════
    [Header("Moon – Visual Sphere")]
    [Range(0.1f, 3f)]  public float MoonSphereRadius   = 0.7f;
    [ColorUsage(true, true)] public Color MoonSphereColor = new Color(0.75f, 0.80f, 1.00f, 1f);
    [Tooltip("Optionnel : material custom Unlit. Sinon généré automatiquement.")]
    public Material MoonSphereMaterial;

    // ══════════════════════════════════════════════════════════════
    // AMBIENT
    // ══════════════════════════════════════════════════════════════
    [Header("Ambient")]
    [ColorUsage(true, true)] public Color DayAmbientColor = new Color(0.22f, 0.28f, 0.42f, 1f);
    [Range(0f, 2f)] public float DayAmbientIntensity      = 0.20f;

    // ══════════════════════════════════════════════════════════════
    // DEBUG
    // ══════════════════════════════════════════════════════════════
    [Header("Debug")]
    public bool DrawGizmos = true;

    // ── Private state ──────────────────────────────────────────────
    private float      _sunAngle;
    private float      _moonAngle;
    private GameObject _sunSphere;
    private GameObject _moonSphere;
    // Matériaux gérés en interne (pour pouvoir les détruire proprement)
    private Material   _sunMatAuto;
    private Material   _moonMatAuto;

    private Color _savedAmbientColor;
    private float _savedAmbientIntensity;
    private bool  _ambientSaved;

    // ══════════════════════════════════════════════════════════════
    void OnEnable()
    {
        SaveAmbient();
        _sunAngle  = SunInitialAngle;
        _moonAngle = MoonInitialAngle;
        EnsureLights();
        EnsureSpheres();
        Apply();
    }

    void OnDisable()
    {
        RestoreAmbient();
        DestroySpheres();
    }

    void Update()
    {
        if (Application.isPlaying)
        {
            _sunAngle  += SunOrbitSpeed  * Time.deltaTime;
            _moonAngle += MoonOrbitSpeed * Time.deltaTime;
        }

        EnsureSpheres();
        Apply();
    }

    // ══════════════════════════════════════════════════════════════
    // CORE
    // ══════════════════════════════════════════════════════════════
    void Apply()
    {
        Vector3 planet = PlanetTransform != null ? PlanetTransform.position : Vector3.zero;

        // ── Soleil ─────────────────────────────────────────────────
        if (SunLight != null)
        {
            Vector3 sunPos = OrbitPosition(planet, _sunAngle, SunOrbitRadius, SunTiltAngle);
            SunLight.transform.position  = sunPos;
            SunLight.transform.LookAt(planet);
            SunLight.color            = SunColor;
            SunLight.intensity        = SunIntensity;
            SunLight.shadows          = SunShadowType;
            SunLight.shadowStrength   = SunShadowStrength;
            SunLight.shadowBias       = SunShadowBias;
            SunLight.shadowNormalBias = SunNormalBias;

            if (_sunSphere != null)
            {
                _sunSphere.transform.position  = sunPos;
                _sunSphere.transform.localScale = Vector3.one * SunSphereRadius * 2f;
                UpdateSphereColor(_sunSphere, SunSphereColor, SunSphereMaterial, ref _sunMatAuto);
            }
        }

        // ── Lune ───────────────────────────────────────────────────
        if (MoonLight != null)
        {
            Vector3 moonPos = OrbitPosition(planet, _moonAngle, MoonOrbitRadius, MoonTiltAngle);
            MoonLight.transform.position = moonPos;
            MoonLight.transform.LookAt(planet);
            MoonLight.color          = MoonColor;
            MoonLight.intensity      = MoonIntensity;
            MoonLight.shadows        = MoonShadowType;
            MoonLight.shadowStrength = MoonShadowStrength;

            if (_moonSphere != null)
            {
                _moonSphere.transform.position  = moonPos;
                _moonSphere.transform.localScale = Vector3.one * MoonSphereRadius * 2f;
                UpdateSphereColor(_moonSphere, MoonSphereColor, MoonSphereMaterial, ref _moonMatAuto);
            }
        }

        // ── Ambient fixe ───────────────────────────────────────────
        RenderSettings.ambientLight     = DayAmbientColor * DayAmbientIntensity;
        RenderSettings.ambientIntensity = DayAmbientIntensity;
    }

    // ══════════════════════════════════════════════════════════════
    // SPHÈRES
    // ══════════════════════════════════════════════════════════════
    void EnsureSpheres()
    {
        if (_sunSphere == null)
            _sunSphere = CreateSphere("_SunSphere", SunSphereRadius, SunSphereColor,
                                      SunSphereMaterial, ref _sunMatAuto);
        if (_moonSphere == null)
            _moonSphere = CreateSphere("_MoonSphere", MoonSphereRadius, MoonSphereColor,
                                       MoonSphereMaterial, ref _moonMatAuto);
    }

    /// <summary>Crée une sphère primitive Unlit, sans ombres ni collider.</summary>
    static GameObject CreateSphere(string goName, float radius, Color col,
                                   Material overrideMat, ref Material autoMat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = goName;

        // Pas d'ombres — purement décoratif
        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        // Pas de collider
        var col2 = go.GetComponent<Collider>();
        if (col2 != null) DestroyImmediate(col2);

        go.transform.localScale = Vector3.one * radius * 2f;

        if (overrideMat != null)
        {
            mr.sharedMaterial = overrideMat;
        }
        else
        {
            autoMat = MakeUnlitMat(col);
            mr.sharedMaterial = autoMat;
        }

#if UNITY_EDITOR
        go.hideFlags = HideFlags.DontSave;
#endif
        return go;
    }

    /// <summary>Applique la couleur en live si elle change dans l'inspector.</summary>
    static void UpdateSphereColor(GameObject sphere, Color col,
                                  Material overrideMat, ref Material autoMat)
    {
        var mr = sphere.GetComponent<MeshRenderer>();
        if (mr == null) return;

        if (overrideMat != null)
        {
            if (mr.sharedMaterial != overrideMat) mr.sharedMaterial = overrideMat;
            return;
        }

        // Material auto : recrée seulement si la couleur a changé
        if (autoMat == null || (Color)autoMat.GetColor("_BaseColor") != col)
        {
            if (autoMat != null) DestroyImmediate(autoMat);
            autoMat = MakeUnlitMat(col);
            mr.sharedMaterial = autoMat;
        }
    }

    /// <summary>Crée un material Unlit HDR compatible URP.</summary>
    static Material MakeUnlitMat(Color col)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                 ?? Shader.Find("Unlit/Color")
                 ?? Shader.Find("Standard");
        var mat = new Material(sh) { name = "AutoUnlit" };
        // URP Unlit utilise _BaseColor
        if (mat.HasProperty("_BaseColor"))   mat.SetColor("_BaseColor",   col);
        if (mat.HasProperty("_Color"))        mat.SetColor("_Color",       col);
        // Emission pour briller (surtout le soleil en HDR)
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", col);
        }
        mat.enableInstancing = true;
        return mat;
    }

    void DestroySpheres()
    {
        if (_sunSphere  != null) { DestroyImmediate(_sunSphere);  _sunSphere  = null; }
        if (_moonSphere != null) { DestroyImmediate(_moonSphere); _moonSphere = null; }
        if (_sunMatAuto  != null) { DestroyImmediate(_sunMatAuto);  _sunMatAuto  = null; }
        if (_moonMatAuto != null) { DestroyImmediate(_moonMatAuto); _moonMatAuto = null; }
    }

    // ══════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════
    static Vector3 OrbitPosition(Vector3 center, float angleDeg, float radius, float tiltDeg)
    {
        float rad  = angleDeg * Mathf.Deg2Rad;
        float tilt = tiltDeg  * Mathf.Deg2Rad;
        float x    = Mathf.Cos(rad) * radius;
        float z    = Mathf.Sin(rad) * radius;
        float y    = Mathf.Sin(tilt) * radius;
        float zT   = z * Mathf.Cos(tilt) - y * Mathf.Sin(tilt);
        float yT   = z * Mathf.Sin(tilt) + y * Mathf.Cos(tilt);
        return center + new Vector3(x, yT, zT);
    }

    void EnsureLights()
    {
        if (SunLight == null)
        {
            var go = new GameObject("SunLight");
            go.transform.SetParent(transform);
            SunLight = go.AddComponent<Light>();
            SunLight.type       = LightType.Directional;
            SunLight.renderMode = LightRenderMode.ForcePixel;
            SunLight.shadows    = LightShadows.Soft;
            Debug.Log("[PlanetLighting] SunLight créé automatiquement.");
        }
        if (MoonLight == null)
        {
            var go = new GameObject("MoonLight");
            go.transform.SetParent(transform);
            MoonLight = go.AddComponent<Light>();
            MoonLight.type       = LightType.Directional;
            MoonLight.renderMode = LightRenderMode.ForcePixel;
            MoonLight.shadows    = LightShadows.None;
            Debug.Log("[PlanetLighting] MoonLight créé automatiquement.");
        }
    }

    void SaveAmbient()
    {
        if (_ambientSaved) return;
        _savedAmbientColor     = RenderSettings.ambientLight;
        _savedAmbientIntensity = RenderSettings.ambientIntensity;
        _ambientSaved = true;
    }

    void RestoreAmbient()
    {
        if (!_ambientSaved) return;
        RenderSettings.ambientLight     = _savedAmbientColor;
        RenderSettings.ambientIntensity = _savedAmbientIntensity;
        _ambientSaved = false;
    }

    // ══════════════════════════════════════════════════════════════
    // GIZMOS
    // ══════════════════════════════════════════════════════════════
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!DrawGizmos) return;
        Vector3 planet = PlanetTransform != null ? PlanetTransform.position : Vector3.zero;
        DrawOrbitGizmo(planet, SunOrbitRadius,  SunTiltAngle,  new Color(1f, 0.7f, 0.1f, 0.35f));
        DrawOrbitGizmo(planet, MoonOrbitRadius, MoonTiltAngle, new Color(0.5f, 0.6f, 0.9f, 0.25f));
        if (SunLight != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawLine(SunLight.transform.position, planet);
        }
        if (MoonLight != null)
        {
            Gizmos.color = new Color(0.7f, 0.75f, 1f, 0.8f);
            Gizmos.DrawLine(MoonLight.transform.position, planet);
        }
        if (SunLight != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.12f);
            DrawCircleGizmo(planet, (planet - SunLight.transform.position).normalized,
                            PlanetRadius + 0.3f, 48);
        }
    }

    static void DrawOrbitGizmo(Vector3 center, float radius, float tilt, Color col)
    {
        Gizmos.color = col;
        Vector3 prev = Vector3.zero;
        for (int i = 0; i <= 64; i++)
        {
            Vector3 pos = OrbitPosition(center, i * 360f / 64f, radius, tilt);
            if (i > 0) Gizmos.DrawLine(prev, pos);
            prev = pos;
        }
    }

    static void DrawCircleGizmo(Vector3 center, Vector3 normal, float radius, int steps)
    {
        Vector3 up = Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.right;
        Vector3 t  = Vector3.Cross(normal, up).normalized;
        Vector3 b  = Vector3.Cross(normal, t).normalized;
        Vector3 prev = Vector3.zero;
        for (int i = 0; i <= steps; i++)
        {
            float   a   = i * Mathf.PI * 2f / steps;
            Vector3 pos = center + (t * Mathf.Cos(a) + b * Mathf.Sin(a)) * radius;
            if (i > 0) Gizmos.DrawLine(prev, pos);
            prev = pos;
        }
    }
#endif

    // ══════════════════════════════════════════════════════════════
    // CONTEXT MENUS
    // ══════════════════════════════════════════════════════════════
    [ContextMenu("Force Apply Now")]
    public void ForceApply()
    {
        EnsureLights(); EnsureSpheres(); Apply();
    }

    [ContextMenu("Reset Angles")]
    public void ResetAngles()
    {
        _sunAngle  = SunInitialAngle;
        _moonAngle = MoonInitialAngle;
        Apply();
    }

    [ContextMenu("Destroy Spheres (debug)")]
    public void DestroySpheresManual() => DestroySpheres();
}

// ──────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
[CustomEditor(typeof(PlanetLightingManager))]
public class PlanetLightingManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(8);
        var mgr = (PlanetLightingManager)target;
        if (GUILayout.Button("☀️  Force Apply Now", GUILayout.Height(32)))
        { mgr.ForceApply(); EditorUtility.SetDirty(mgr); }
        if (GUILayout.Button("🔄  Reset Angles", GUILayout.Height(28)))
        { mgr.ResetAngles(); EditorUtility.SetDirty(mgr); }
        GUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Les sphères soleil/lune sont créées automatiquement (Unlit, sans ombres).\n" +
            "Pour un soleil brillant : assigner un material 'Universal Render Pipeline/Unlit'\n" +
            "avec une couleur HDR (valeur > 1) dans SunSphereMaterial.",
            MessageType.Info);
    }
}
#endif