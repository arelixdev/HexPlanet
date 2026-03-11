using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class TileObjectSpawner : MonoBehaviour
{
    [Header("Référence")]
    public HexPlanetGenerator Generator;

    [Header("Forêt")]
    public bool  SpawnForest               = true;
    [Range(0f,1f)]   public float ForestDensity    = 0.6f;
    [Range(1,20)]    public int   ForestMaxPerTile  = 4;
    [Range(0.5f,5f)] public float ForestScale       = 1f;
    [Range(0f,0.5f)] public float ForestScaleRandom = 0.25f;
    // Assigner le material avec le shader Custom/FirTreeSnow
    public Material ForestMaterial;

    [Header("Cristaux de montagne")]
    public bool SpawnMountainCrystals       = false;
    [Range(0f,1f)]   public float CrystalDensity    = 0.3f;
    [Range(1,8)]     public int   CrystalMaxPerTile  = 2;
    [Range(0.5f,2f)] public float CrystalScale       = 1f;
    public Material CrystalMaterial;

    [Header("Debug")]
    public bool ShowDebugLogs = false;

    private GameObject _container;

    [ContextMenu("Spawn Objects")]
    public void SpawnAll()
    {
        Clear();
        if (Generator == null) { Debug.LogWarning("[Spawner] Generator non assigné !"); return; }

        _container = new GameObject("TileObjects");
        _container.transform.SetParent(transform, false);
        Random.InitState(Generator.Seed + 1337);

        int spawnedForest = 0, spawnedCrystal = 0;

        for (int i = 0; i < Generator.TileCount; i++)
        {
            int step = Generator.GetTileBiomeStep(i);

            if (SpawnForest && step == 3)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(ForestMaxPerTile * ForestDensity));
                // La neige est entièrement gérée par FirTreeSnow.shader via la latitude monde.
                // Le C# n'a plus rien à calculer ici.
                spawnedForest += SpawnOnTile(i, new FirTreeShape(), count,
                                            ForestScale, ForestScaleRandom, ForestMaterial);
            }

            if (SpawnMountainCrystals && step == 4)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(CrystalMaxPerTile * CrystalDensity));
                spawnedCrystal += SpawnOnTile(i, new CrystalShape(), count,
                                            CrystalScale, 0.2f, CrystalMaterial);
            }
        }

        Debug.Log($"[Spawner] {spawnedForest} sapins  |  {spawnedCrystal} cristaux");
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
                    float baseScale, float scaleRandom, Material mat)
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

        Vector3 n = centerLocal;
        Vector3 t = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 b = Vector3.Cross(n, t);
        Vector3 nWorld = transform.TransformDirection(n).normalized;

        float baseFootprintLocal(float s) => GetShapeFootprint(shape) * s / surfR;

        var localPlaced = new List<(Vector3 dirLocal, float footLocal)>();
        int spawned     = 0;
        int maxAttempts = maxCount * 15;

        for (int attempt = 0; attempt < maxAttempts && spawned < maxCount; attempt++)
        {
            float s         = baseScale * (1f + Random.Range(-scaleRandom, scaleRandom));
            float footLocal = baseFootprintLocal(s) * 1.15f;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist  = Mathf.Sqrt(Random.value) * safeRadiusLocal;

            Vector3 offsetLocal = (t * Mathf.Cos(angle) + b * Mathf.Sin(angle)) * dist;
            Vector3 dirLocal    = (n + offsetLocal).normalized;

            bool overlaps = false;
            foreach (var (pd, pr) in localPlaced)
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

            localPlaced.Add((dirLocal, footLocal));
            spawned++;
        }

        if (ShowDebugLogs)
            Debug.Log($"[Spawner] Tile {tileId} → {spawned}/{maxCount} spawned");

        return spawned;
    }

    float GetShapeFootprint(TileShape shape) => shape switch
    {
        FirTreeShape f => f.Footprint,
        PyramidShape p => p.BaseSize * 0.6f,
        ConeShape    c => c.Radius,
        CrystalShape x => x.BaseSize * 0.6f,
        _              => 0.08f
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