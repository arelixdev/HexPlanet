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
    [Range(0.4f, 2f)] public float DomeFlattenY       = 1.0f;
    public Material DomeMaterial;

    // ══════════════════════════════════════════════════════════════
    // RÈGLE DE MÉLANGE PAR LATITUDE
    // ══════════════════════════════════════════════════════════════
    [Header("Mélange Dôme ↔ Pin (latitude)")]
    [Range(0f, 1f)] public float DomeOnlyLatitude = 0.10f;
    [Range(0f, 1f)] public float PinOnlyLatitude  = 0.60f;

    // ══════════════════════════════════════════════════════════════
    // ROCHERS (plaines – step 2)
    // ══════════════════════════════════════════════════════════════
    [Header("Rochers (plaines)")]
    public bool SpawnRocks = true;

    [Tooltip("Probabilité qu'une tile de plaine reçoive des rochers (0=aucune, 1=toutes).")]
    [Range(0f, 1f)] public float RockTileChance   = 0.35f;

    [Tooltip("Nombre minimum de rochers par tile choisie.")]
    [Range(1, 3)]   public int   RockMinPerTile   = 2;

    [Tooltip("Nombre maximum de rochers par tile choisie.")]
    [Range(1, 3)]   public int   RockMaxPerTile   = 3;

    [Range(0.3f, 4f)] public float RockScale       = 1f;
    [Range(0f, 0.6f)] public float RockScaleRandom = 0.40f;

    [Tooltip("Taille de base du rocher.")]
    [Range(0.03f, 0.20f)] public float RockBaseSize = 0.07f;

    [Tooltip("Fraction du rayon de la tile utilisée pour le placement (0=centre, 1=bord).")]
    [Range(0.05f, 0.60f)] public float RockSpreadRadius = 0.30f;

    [Tooltip("Enfoncement dans le sol (valeur positive = descend vers le centre de la planète).")]
    [Range(0f, 0.5f)] public float RockSinkOffset = 0.20f;

    [Tooltip("Material VertexColorURP recommandé. Laissez vide pour le fallback auto.")]
    public Material RockMaterial;

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
    private int _rockSeedCounter;

    // ──────────────────────────────────────────────────────────────
    [ContextMenu("Spawn Objects")]
    public void SpawnAll()
    {
        Clear();
        if (Generator == null) { Debug.LogWarning("[Spawner] Generator non assigné !"); return; }

        _container = new GameObject("TileObjects");
        _container.transform.SetParent(transform, false);
        Random.InitState(Generator.Seed + 1337);
        _rockSeedCounter = Generator.Seed * 9973;

        int spawnedFir = 0, spawnedDome = 0, spawnedCrystal = 0, spawnedRock = 0;
        int rockTilesChosen = 0;

        for (int i = 0; i < Generator.TileCount; i++)
        {
            int step = Generator.GetTileBiomeStep(i);

            // ── Arbres forestiers (step 3) ─────────────────────────
            if (step == 3 && (SpawnFirTrees || SpawnDomeTrees))
            {
                float lat = Mathf.Abs(Generator.GetTileCenter(i).y);
                float pinRatio = Mathf.Clamp01(
                    Mathf.InverseLerp(DomeOnlyLatitude, PinOnlyLatitude, lat));

                int firCount  = SpawnFirTrees  ? Mathf.RoundToInt(FirMaxPerTile  * FirDensity  * pinRatio)        : 0;
                int domeCount = SpawnDomeTrees ? Mathf.RoundToInt(DomeMaxPerTile * DomeDensity * (1f - pinRatio)) : 0;

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

            // ── Rochers (step 2 = plaines) ─────────────────────────
            if (SpawnRocks && step == 2)
            {
                if (Random.value < RockTileChance)
                {
                    rockTilesChosen++;
                    int count = Random.Range(RockMinPerTile, RockMaxPerTile + 1);
                    spawnedRock += SpawnRocksOnTile(i, count);
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

        Debug.Log($"[Spawner] {spawnedDome} dômes  |  {spawnedFir} pins  |  " +
                  $"{spawnedRock} rochers sur {rockTilesChosen} tiles plaine  |  {spawnedCrystal} cristaux");
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
    int SpawnOnTile(int tileId, TileShape shape, int maxCount,
                    float baseScale, float scaleRandom, Material mat,
                    List<(Vector3 dir, float foot)> sharedPlaced)
    {
        Vector3 centerLocal = Generator.GetTileCenter(tileId);
        float   elev        = Generator.GetTileElevation(tileId);
        float   surfR       = Generator.PlanetRadius
                            + (elev == 0f ? 0.20f : elev) * Generator.ElevationScale;

        float minChord = float.MaxValue;
        foreach (int nId in Generator.GetTileNeighbors(tileId))
            minChord = Mathf.Min(minChord,
                Vector3.Distance(centerLocal, Generator.GetTileCenter(nId)));

        float safeRadiusLocal = minChord * 0.5f;

        Vector3 n      = centerLocal;
        Vector3 t      = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 b      = Vector3.Cross(n, t);
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
            Debug.Log($"[Spawner] Tile {tileId} → {spawned}/{maxCount} '{shape.GetType().Name}'");

        return spawned;
    }

    // ──────────────────────────────────────────────────────────────
    int SpawnRocksOnTile(int tileId, int count)
    {
        Vector3 centerLocal = Generator.GetTileCenter(tileId);
        float   elev        = Generator.GetTileElevation(tileId);
        float   surfR       = Generator.PlanetRadius
                            + (elev == 0f ? 0.20f : elev) * Generator.ElevationScale;

        // RockSinkOffset enfonce le rocher vers le centre de la planète
        float spawnR = surfR - RockSinkOffset;

        float minChord = float.MaxValue;
        foreach (int nId in Generator.GetTileNeighbors(tileId))
            minChord = Mathf.Min(minChord,
                Vector3.Distance(centerLocal, Generator.GetTileCenter(nId)));

        float spawnZone = minChord * 0.5f * RockSpreadRadius;

        Vector3 n      = centerLocal;
        Vector3 tan    = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 bitan  = Vector3.Cross(n, tan);
        Vector3 nWorld = transform.TransformDirection(n).normalized;

        var placed      = new List<(Vector3 dir, float foot)>();
        int spawned     = 0;
        int maxAttempts = count * 30;

        for (int attempt = 0; attempt < maxAttempts && spawned < count; attempt++)
        {
            int rockSeed = _rockSeedCounter++;

            var shape = new RockShape
            {
                Seed     = rockSeed,
                BaseSize = RockBaseSize
            };

            float s         = RockScale * (1f + Random.Range(-RockScaleRandom, RockScaleRandom));
            float footLocal = (shape.Footprint * s) / surfR * 1.50f;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist  = Mathf.Sqrt(Random.value) * spawnZone;

            Vector3 offsetLocal = (tan * Mathf.Cos(angle) + bitan * Mathf.Sin(angle)) * dist;
            Vector3 dirLocal    = (n + offsetLocal).normalized;

            bool overlaps = false;
            foreach (var (pd, pr) in placed)
                if (Vector3.Distance(dirLocal, pd) < footLocal + pr) { overlaps = true; break; }
            if (overlaps) continue;

            // spawnR < surfR → le rocher est positionné légèrement sous la surface
            Vector3    worldPos = transform.TransformPoint(dirLocal * spawnR);
            Quaternion rot      = Quaternion.FromToRotation(Vector3.up, nWorld)
                                * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up);

            var go = new GameObject($"Rock_{tileId}_{spawned}_s{rockSeed}");
            go.transform.SetParent(_container.transform, false);
            go.transform.position = worldPos;
            go.transform.rotation = rot;
            go.AddComponent<MeshFilter>().sharedMesh       = shape.Build(s);
            go.AddComponent<MeshRenderer>().sharedMaterial = RockMaterial ?? MakeFallbackMat();

            placed.Add((dirLocal, footLocal));
            spawned++;
        }

        if (ShowDebugLogs)
            Debug.Log($"[Spawner] Tile {tileId} → {spawned}/{count} rochers (sink={RockSinkOffset})");

        return spawned;
    }

    // ──────────────────────────────────────────────────────────────
    float GetShapeFootprint(TileShape shape) => shape switch
    {
        FirTreeShape  f => f.Footprint,
        DomeTreeShape d => d.Footprint,
        RockShape     r => r.Footprint,
        PyramidShape  p => p.BaseSize * 0.6f,
        ConeShape     c => c.Radius,
        CrystalShape  x => x.BaseSize * 0.6f,
        _               => 0.08f
    };

    Material MakeFallbackMat()
    {
        var s = Shader.Find("Custom/VertexColorURP")
             ?? Shader.Find("Custom/FirTreeSnow")
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