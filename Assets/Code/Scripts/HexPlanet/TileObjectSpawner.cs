using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class TileObjectSpawner : MonoBehaviour
{
    [Header("Référence")]
    public HexPlanetGenerator Generator;

    // ══════════════════════════════════════════════════════════════
    // PINS (biome froid / haute latitude)
    // ══════════════════════════════════════════════════════════════
    [Header("Pins (haute latitude)")]
    public bool  SpawnFirTrees             = true;
    [Range(0f, 1f)]   public float FirDensity        = 0.6f;
    [Range(1, 20)]    public int   FirMaxPerTile      = 4;
    [Range(0.5f, 5f)] public float FirScale           = 1f;
    [Range(0f, 0.5f)] public float FirScaleRandom     = 0.25f;
    /// <summary>Material utilisant le shader Custom/FirTreeSnow.</summary>
    public Material FirMaterial;

    // ══════════════════════════════════════════════════════════════
    // DÔMES (biome chaud / basse latitude)
    // ══════════════════════════════════════════════════════════════
    [Header("Arbres dôme (basse latitude)")]
    public bool  SpawnDomeTrees            = true;
    [Range(0f, 1f)]   public float DomeDensity       = 0.6f;
    [Range(1, 20)]    public int   DomeMaxPerTile     = 3;
    [Range(0.5f, 5f)] public float DomeScale          = 1f;
    [Range(0f, 0.5f)] public float DomeScaleRandom    = 0.20f;
    /// <summary>
    /// Écrasement / allongement vertical du dôme.
    /// 0.6 = aplati (savane), 1.0 = sphère, 1.4 = allongé (palmier).
    /// </summary>
    [Range(0.4f, 2f)] public float DomeFlattenY       = 1.0f;
    public Material DomeMaterial;

    // ══════════════════════════════════════════════════════════════
    // RÈGLE DE MÉLANGE PAR LATITUDE
    // ══════════════════════════════════════════════════════════════
    [Header("Mélange Dôme ↔ Pin (latitude)")]
    /// <summary>
    /// En-dessous de cette latitude (|center.y|) → 100 % dômes.
    /// Au-dessus de PinOnlyLatitude             → 100 % pins.
    /// Entre les deux, interpolation linéaire.
    /// </summary>
    [Range(0f, 1f)] public float DomeOnlyLatitude = 0.10f;
    [Range(0f, 1f)] public float PinOnlyLatitude  = 0.60f;

    // ══════════════════════════════════════════════════════════════
    // CRISTAUX DE MONTAGNE
    // ══════════════════════════════════════════════════════════════
    [Header("Cristaux de montagne")]
    public bool SpawnMountainCrystals       = false;
    [Range(0f, 1f)]   public float CrystalDensity    = 0.3f;
    [Range(1, 8)]     public int   CrystalMaxPerTile  = 2;
    [Range(0.5f, 2f)] public float CrystalScale       = 1f;
    public Material CrystalMaterial;

    [Header("Debug")]
    public bool ShowDebugLogs = false;

    private GameObject _container;

    // ──────────────────────────────────────────────────────────────
    [ContextMenu("Spawn Objects")]
    public void SpawnAll()
    {
        Clear();
        if (Generator == null) { Debug.LogWarning("[Spawner] Generator non assigné !"); return; }

        _container = new GameObject("TileObjects");
        _container.transform.SetParent(transform, false);
        Random.InitState(Generator.Seed + 1337);

        int spawnedFir = 0, spawnedDome = 0, spawnedCrystal = 0;

        for (int i = 0; i < Generator.TileCount; i++)
        {
            int step = Generator.GetTileBiomeStep(i);

            // ── Arbres forestiers (step 3) ─────────────────────────
            if (step == 3 && (SpawnFirTrees || SpawnDomeTrees))
            {
                float lat = Mathf.Abs(Generator.GetTileCenter(i).y);

                // pinRatio : 0 = 100 % dômes, 1 = 100 % pins
                float pinRatio = Mathf.Clamp01(
                    Mathf.InverseLerp(DomeOnlyLatitude, PinOnlyLatitude, lat));

                int firCount  = SpawnFirTrees  ? Mathf.RoundToInt(FirMaxPerTile  * FirDensity  * pinRatio)        : 0;
                int domeCount = SpawnDomeTrees ? Mathf.RoundToInt(DomeMaxPerTile * DomeDensity * (1f - pinRatio)) : 0;

                // Liste de positions partagée → évite les chevauchements entre les deux types
                var sharedPlaced = new List<(Vector3 dir, float foot)>();

                if (domeCount > 0)
                {
                    var shape = new DomeTreeShape { DomeScaleY = DomeFlattenY };
                    spawnedDome += SpawnOnTile(i, shape, domeCount,
                                              DomeScale, DomeScaleRandom,
                                              DomeMaterial, sharedPlaced);
                }

                if (firCount > 0)
                {
                    spawnedFir += SpawnOnTile(i, new FirTreeShape(), firCount,
                                             FirScale, FirScaleRandom,
                                             FirMaterial, sharedPlaced);
                }
            }

            // ── Cristaux (step 4) ──────────────────────────────────
            if (SpawnMountainCrystals && step == 4)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(CrystalMaxPerTile * CrystalDensity));
                spawnedCrystal += SpawnOnTile(i, new CrystalShape(), count,
                                             CrystalScale, 0.2f,
                                             CrystalMaterial, new List<(Vector3, float)>());
            }
        }

        Debug.Log($"[Spawner] {spawnedDome} dômes  |  {spawnedFir} pins  |  {spawnedCrystal} cristaux");
    }

    [ContextMenu("Clear Objects")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            if (transform.GetChild(i).name == "TileObjects")
                DestroyImmediate(transform.GetChild(i).gameObject);
        _container = null;
    }

    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Pose <paramref name="maxCount"/> objets sur la tuile <paramref name="tileId"/>.
    /// <paramref name="sharedPlaced"/> : liste de positions déjà occupées (partagée
    /// entre plusieurs appels sur la même tuile pour éviter les chevauchements).
    /// </summary>
    int SpawnOnTile(int tileId, TileShape shape, int maxCount,
                    float baseScale, float scaleRandom, Material mat,
                    List<(Vector3 dir, float foot)> sharedPlaced)
    {
        Vector3 centerLocal = Generator.GetTileCenter(tileId);
        float   elev        = Generator.GetTileElevation(tileId);
        float   surfR       = Generator.PlanetRadius
                            + (elev == 0f ? 0.20f : elev) * Generator.ElevationScale;

        // Rayon sûr = moitié de la distance au voisin le plus proche
        float minChord = float.MaxValue;
        foreach (int nId in Generator.GetTileNeighbors(tileId))
            minChord = Mathf.Min(minChord,
                Vector3.Distance(centerLocal, Generator.GetTileCenter(nId)));

        float safeRadiusLocal = minChord * 0.5f;

        // Repère tangent local à la tuile
        Vector3 n = centerLocal;
        Vector3 t = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 b = Vector3.Cross(n, t);
        Vector3 nWorld = transform.TransformDirection(n).normalized;

        float FootLocal(float s) => GetShapeFootprint(shape) * s / surfR;

        int spawned     = 0;
        int maxAttempts = maxCount * 15;

        for (int attempt = 0; attempt < maxAttempts && spawned < maxCount; attempt++)
        {
            float s         = baseScale * (1f + Random.Range(-scaleRandom, scaleRandom));
            float footLocal = FootLocal(s) * 1.15f;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist  = Mathf.Sqrt(Random.value) * safeRadiusLocal;

            Vector3 offsetLocal = (t * Mathf.Cos(angle) + b * Mathf.Sin(angle)) * dist;
            Vector3 dirLocal    = (n + offsetLocal).normalized;

            // Vérifie l'overlap contre toutes les positions déjà placées (tous types)
            bool overlaps = false;
            foreach (var (pd, pr) in sharedPlaced)
                if (Vector3.Distance(dirLocal, pd) < footLocal + pr) { overlaps = true; break; }
            if (overlaps) continue;

            Vector3    worldPos = transform.TransformPoint(dirLocal * surfR);
            Quaternion rot      = Quaternion.FromToRotation(Vector3.up, nWorld)
                                * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up);

            var go = new GameObject($"TileObj_{tileId}_{spawned}");
            go.transform.SetParent(_container.transform, false);
            go.transform.position = worldPos;
            go.transform.rotation = rot;
            go.AddComponent<MeshFilter>().sharedMesh       = shape.Build(s);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat ?? MakeFallbackMat();

            sharedPlaced.Add((dirLocal, footLocal));
            spawned++;
        }

        if (ShowDebugLogs)
            Debug.Log($"[Spawner] Tile {tileId} → {spawned}/{maxCount} '{shape.GetType().Name}' spawned");

        return spawned;
    }

    float GetShapeFootprint(TileShape shape) => shape switch
    {
        FirTreeShape  f => f.Footprint,
        DomeTreeShape d => d.Footprint,
        PyramidShape  p => p.BaseSize * 0.6f,
        ConeShape     c => c.Radius,
        CrystalShape  x => x.BaseSize * 0.6f,
        _               => 0.08f
    };

    Material MakeFallbackMat()
    {
        var s = Shader.Find("Custom/FirTreeSnow")
             ?? Shader.Find("Custom/VertexColorURP")
             ?? Shader.Find("Standard");
        return new Material(s);
    }
}

// ──────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TileObjectSpawner))]
public class TileObjectSpawnerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        UnityEngine.GUILayout.Space(8);
        var s = (TileObjectSpawner)target;
        if (UnityEngine.GUILayout.Button("🌲  Spawn Objects", UnityEngine.GUILayout.Height(34)))
        { s.SpawnAll(); UnityEditor.EditorUtility.SetDirty(s); }
        if (UnityEngine.GUILayout.Button("🗑️  Clear Objects", UnityEngine.GUILayout.Height(28)))
        { s.Clear();    UnityEditor.EditorUtility.SetDirty(s); }
    }
}
#endif