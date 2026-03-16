using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class HexPlanetGenerator : MonoBehaviour
{
    [Header("Planet Settings")]
    [Range(1, 5)] public int Subdivisions = 3;
    public float PlanetRadius = 5f;
    public int Seed = 42;

    [Header("Terrain")]
    [Range(1, 8)] public int NoiseOctaves = 4;
    [Range(0.1f, 2f)] public float ElevationScale = 0.5f;
    [Range(0f, 1f)] public float SeaLevel = 0.38f;

    [Header("Mesh Style")]
    [Range(0.5f, 1f)]  public float TileInset        = 0.80f;
    [Range(0f, 0.4f)]  public float CliffThreshold   = 0.10f;
    [Range(0f, 0.18f)] public float CornerJitter     = 0.06f;

    [Header("Visual")]
    public Material PlanetMaterial;

    // ── ATMOSPHERE ─────────────────────────────────────────────────
    [Header("Atmosphere")]
    public bool ShowAtmosphere = true;
    /// <summary>Material using the Custom/Atmosphere shader.</summary>
    public Material AtmosphereMaterial;
    [Range(1.01f, 1.35f)] public float AtmosphereScale     = 1.10f;
    [ColorUsage(true, true)] public Color AtmosphereColor = new Color(0.30f, 0.55f, 1.00f, 1f);
    [ColorUsage(true, true)] public Color AtmosphereRimColor = new Color(0.15f, 0.35f, 0.90f, 1f);
    [Range(0.5f, 8f)] public float AtmospherePower     = 3.2f;
    [Range(0f, 3f)]   public float AtmosphereIntensity = 1.4f;
    [Range(0f, 1f)]   public float AtmosphereInnerOpacity = 0.04f;
    [Range(0f, 1f)]   public float AtmosphereRimWidth = 0.72f;

    // ── Serialised data ────────────────────────────────────────────
    [SerializeField] private List<Vector3> tileCenters    = new List<Vector3>();
    [SerializeField] private List<float>   tileElevations = new List<float>();
    [SerializeField] private List<Color>   tileColors     = new List<Color>();
    [SerializeField] private int           tileCount;
    [SerializeField] private List<int> tileBiomeSteps = new List<int>();

    [SerializeField] private List<int> neighborFlat    = new List<int>();
    [SerializeField] private List<int> neighborOffsets = new List<int>();

    [SerializeField] private List<int> facesFlat = new List<int>();

    // ── Per-tile sorted raw corners (face centroids) ───────────────
    [SerializeField] private List<Vector3> cornerFlat    = new List<Vector3>();
    [SerializeField] private List<int>     cornerOffsets = new List<int>();

    // Non-serialised
    private List<List<int>> vertFaces     = new List<List<int>>();
    private List<Vector3>   faceCentroids = new List<Vector3>();

    // ── Neighbour access ───────────────────────────────────────────
    public List<int> GetTileNeighbors(int id)
    {
        if (neighborOffsets == null || id >= neighborOffsets.Count) return new List<int>();
        int start = neighborOffsets[id];
        int end   = (id + 1 < neighborOffsets.Count) ? neighborOffsets[id + 1] : neighborFlat.Count;
        return neighborFlat.GetRange(start, end - start);
    }

    /// <summary>Returns the sorted raw face-centroid corners of tile <paramref name="id"/>.</summary>
    public List<Vector3> GetTileRawCorners(int id)
    {
        if (cornerOffsets == null || id < 0 || id >= cornerOffsets.Count)
            return new List<Vector3>();
        int start = cornerOffsets[id];
        int end   = (id + 1 < cornerOffsets.Count) ? cornerOffsets[id + 1] : cornerFlat.Count;
        if (end <= start) return new List<Vector3>();
        return cornerFlat.GetRange(start, end - start);
    }

    public int     TileCount                => tileCount;
    public Vector3 GetTileCenter(int id)    => tileCenters[id];
    public float   GetTileElevation(int id) => tileElevations[id];
    public Color   GetTileColor(int id)     => tileColors[id];
    public int GetTileBiomeStep(int id) => (tileBiomeSteps.Count > id) ? tileBiomeSteps[id] : 0;

    // ──────────────────────────────────────────────────────────────
    void Start()
    {
        if (tileCount == 0)
        {
            Debug.Log("[HexPlanet] No data at Start — auto-generating.");
            Generate();
        }
        else
        {
            Debug.Log($"[HexPlanet] Loaded: {tileCount} tiles.");
        }
    }

    [ContextMenu("Generate Planet")]
    public void Generate()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        tileCenters.Clear(); tileElevations.Clear(); tileColors.Clear();
        neighborFlat.Clear(); neighborOffsets.Clear();
        facesFlat.Clear();
        cornerFlat.Clear(); cornerOffsets.Clear();
        vertFaces.Clear(); faceCentroids.Clear();
        tileCount = 0;

        Random.InitState(Seed);

        StepBuildIcoSphere();
        StepGenerateTerrain();
        StepBuildMesh();
        StepBuildAtmosphere();

        Debug.Log($"[HexPlanet] {tileCount} tiles generated (subdivisions={Subdivisions})");

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    // ══════════════════════════════════════════════════════════════
    // STEP 1 – Icosphere + dual graph + corner jitter
    // ══════════════════════════════════════════════════════════════
    void StepBuildIcoSphere()
    {
        float phi = (1f + Mathf.Sqrt(5f)) / 2f;

        var verts = new List<Vector3>
        {
            Norm(-1,phi,0), Norm(1,phi,0),  Norm(-1,-phi,0), Norm(1,-phi,0),
            Norm(0,-1,phi), Norm(0,1,phi),  Norm(0,-1,-phi), Norm(0,1,-phi),
            Norm(phi,0,-1), Norm(phi,0,1),  Norm(-phi,0,-1), Norm(-phi,0,1)
        };

        var faces = new List<int[]>
        {
            new[]{0,11,5}, new[]{0,5,1},   new[]{0,1,7},   new[]{0,7,10},  new[]{0,10,11},
            new[]{1,5,9},  new[]{5,11,4},  new[]{11,10,2}, new[]{10,7,6},  new[]{7,1,8},
            new[]{3,9,4},  new[]{3,4,2},   new[]{3,2,6},   new[]{3,6,8},   new[]{3,8,9},
            new[]{4,9,5},  new[]{2,4,11},  new[]{6,2,10},  new[]{8,6,7},   new[]{9,8,1}
        };

        var midCache = new Dictionary<long, int>();
        for (int s = 0; s < Subdivisions; s++)
        {
            var next = new List<int[]>();
            foreach (var f in faces)
            {
                int a=f[0], b=f[1], c=f[2];
                int ab=Mid(a,b,verts,midCache), bc=Mid(b,c,verts,midCache), ca=Mid(c,a,verts,midCache);
                next.Add(new[]{a,ab,ca}); next.Add(new[]{b,bc,ab});
                next.Add(new[]{c,ca,bc}); next.Add(new[]{ab,bc,ca});
            }
            faces = next;
            midCache.Clear();
        }

        tileCenters = new List<Vector3>(verts);
        tileCount   = tileCenters.Count;

        var nbLists = Enumerable.Range(0, tileCount).Select(_ => new List<int>()).ToList();
        var seen    = new HashSet<long>();
        foreach (var f in faces)
            for (int e = 0; e < 3; e++)
            {
                int va=f[e], vb=f[(e+1)%3];
                if (seen.Add(EKey(va,vb)))
                {
                    if (!nbLists[va].Contains(vb)) nbLists[va].Add(vb);
                    if (!nbLists[vb].Contains(va)) nbLists[vb].Add(va);
                }
            }

        neighborFlat.Clear(); neighborOffsets.Clear();
        for (int i = 0; i < tileCount; i++)
        { neighborOffsets.Add(neighborFlat.Count); neighborFlat.AddRange(nbLists[i]); }

        vertFaces = Enumerable.Range(0, tileCount).Select(_ => new List<int>()).ToList();
        faceCentroids.Clear();
        facesFlat.Clear();

        for (int fi = 0; fi < faces.Count; fi++)
        {
            var f = faces[fi];
            faceCentroids.Add(((verts[f[0]]+verts[f[1]]+verts[f[2]])/3f).normalized);
            facesFlat.Add(f[0]); facesFlat.Add(f[1]); facesFlat.Add(f[2]);
            foreach (int vi in f) vertFaces[vi].Add(fi);
        }

        if (CornerJitter > 0.001f)
        {
            for (int fi = 0; fi < faceCentroids.Count; fi++)
            {
                Vector3 n = faceCentroids[fi];
                Vector3 t = Vector3.Cross(n,
                    Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 b = Vector3.Cross(n, t);
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist  = Random.Range(0f, CornerJitter);
                faceCentroids[fi] = (n + (t * Mathf.Cos(angle) + b * Mathf.Sin(angle)) * dist).normalized;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // STEP 2 – Elevation + biome colours
    // ══════════════════════════════════════════════════════════════
    void StepGenerateTerrain()
    {
        tileBiomeSteps.Clear();
        Vector3 off = new Vector3(
            Random.Range(-999f,999f),
            Random.Range(-999f,999f),
            Random.Range(-999f,999f));

        for (int i = 0; i < tileCount; i++)
        {
            Vector3 p = tileCenters[i] * 1.8f + off;
            float e=0,amp=1,freq=1,maxAmp=0;
            for (int o = 0; o < NoiseOctaves; o++)
            {
                float nx=Mathf.PerlinNoise(p.x*freq, p.y*freq);
                float ny=Mathf.PerlinNoise(p.y*freq, p.z*freq);
                float nz=Mathf.PerlinNoise(p.z*freq, p.x*freq);
                e += ((nx+ny+nz)/3f)*amp; maxAmp+=amp;
                amp*=0.55f; freq*=2.1f;
            }
            tileElevations.Add(e/maxAmp);
        }

        float eMin=tileElevations.Min(), eMax=tileElevations.Max();
        for (int i=0;i<tileCount;i++)
            tileElevations[i]=Mathf.InverseLerp(eMin,eMax,tileElevations[i]);

        for (int i = 0; i < tileCount; i++)
        {
            if (tileElevations[i] < SeaLevel)
            {
                tileElevations[i] = 0f;
                tileBiomeSteps.Add(0);
            }
            else
            {
                float t = Mathf.InverseLerp(SeaLevel, 1f, tileElevations[i]);
                int step;
                if      (t < 0.12f) step = 1;
                else if (t < 0.35f) step = 2;
                else if (t < 0.50f) step = 3;
                else if (t < 0.88f) step = 4;
                else                step = 5;

                tileBiomeSteps.Add(step);
                int elevStep = (step == 3) ? 2 : step;
                tileElevations[i] = elevStep * 0.20f;
            }
        }

        const int SmoothPasses = 2;
        var smoothedSteps = new int[tileCount];

        for (int pass = 0; pass < SmoothPasses; pass++)
        {
            for (int i = 0; i < tileCount; i++)
            {
                if (tileBiomeSteps[i] == 0) { smoothedSteps[i] = 0; continue; }
                var nbs = GetTileNeighbors(i);
                if (nbs.Count == 0) { smoothedSteps[i] = tileBiomeSteps[i]; continue; }
                var counts = new Dictionary<int, int>();
                int majority = tileBiomeSteps[i], bestCount = 0;
                foreach (int ni in nbs)
                {
                    int ns = tileBiomeSteps[ni];
                    if (ns == 0) continue;
                    counts.TryGetValue(ns, out int cnt);
                    counts[ns] = cnt + 1;
                    if (counts[ns] > bestCount) { bestCount = counts[ns]; majority = ns; }
                }
                smoothedSteps[i] = (Random.value < 0.70f) ? majority : tileBiomeSteps[i];
            }
            for (int i = 0; i < tileCount; i++)
                if (tileBiomeSteps[i] != 0) tileBiomeSteps[i] = smoothedSteps[i];
        }

        for (int i = 0; i < tileCount; i++)
        {
            int step = tileBiomeSteps[i];
            if (step == 0) { tileElevations[i] = 0f; continue; }
            int elevStep = (step == 3) ? 2 : step;
            tileElevations[i] = elevStep * 0.20f;
        }

        for (int i = 0; i < tileCount; i++)
            tileColors.Add(BiomeColor(i));
    }

    Color BiomeColor(int i)
    {
        float e    = tileElevations[i];
        float lat  = Mathf.Abs(tileCenters[i].y);
        int   step = (tileBiomeSteps.Count > i) ? tileBiomeSteps[i] : 0;

        if (e == 0f)    return new Color(0.06f, 0.20f, 0.60f);
        if (step == 1)  return new Color(0.88f, 0.82f, 0.58f);
        if (lat > 0.82f) return new Color(0.92f, 0.96f, 1.00f);
        if (step == 2)  return new Color(0.40f, 0.70f, 0.20f);
        if (step == 3)  return new Color(0.15f, 0.48f, 0.10f);
        if (step == 4)  return lat > 0.60f
                            ? new Color(0.52f, 0.60f, 0.44f)
                            : new Color(0.52f, 0.48f, 0.40f);
        return lat > 0.50f
            ? new Color(0.92f, 0.96f, 1.00f)
            : new Color(0.48f, 0.44f, 0.40f);
    }

    // ══════════════════════════════════════════════════════════════
    // STEP 3 – Organic mesh
    // ══════════════════════════════════════════════════════════════
    void StepBuildMesh()
    {
        var mV = new List<Vector3>();
        var mT = new List<int>();
        var mC = new List<Color>();

        float[] R = new float[tileCount];
        for (int i = 0; i < tileCount; i++)
        {
            float e = tileElevations[i];
            R[i] = PlanetRadius + (e == 0f ? 0.20f : e) * ElevationScale;
        }

        Vector3 IC(int ti, Vector3 raw) =>
            Vector3.Lerp(tileCenters[ti], raw, TileInset).normalized;

        void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
                     Color cTop, Color cBot)
        {
            Vector3 n   = Vector3.Cross(p1-p0, p3-p0);
            Vector3 mid = (p0+p1+p2+p3)*0.25f;

            int b = mV.Count;
            mV.Add(p0); mV.Add(p1); mV.Add(p2); mV.Add(p3);
            mC.Add(cTop); mC.Add(cTop); mC.Add(cBot); mC.Add(cBot);

            if (Vector3.Dot(n, mid) >= 0)
            { mT.Add(b); mT.Add(b+1); mT.Add(b+3); mT.Add(b); mT.Add(b+3); mT.Add(b+2); }
            else
            { mT.Add(b); mT.Add(b+3); mT.Add(b+1); mT.Add(b); mT.Add(b+2); mT.Add(b+3); }
        }

        // PASS 1 : INSET TOP FACES
        // Also records each tile's sorted raw corners for hover outline use.
        cornerFlat.Clear(); cornerOffsets.Clear();

        for (int vi = 0; vi < tileCount; vi++)
        {
            var fids = vertFaces[vi];

            // Always record offset even for degenerate tiles
            cornerOffsets.Add(cornerFlat.Count);

            if (fids.Count < 3) continue;

            float   r      = R[vi];
            Color   col    = tileColors[vi];
            Vector3 centre = tileCenters[vi];

            var rawC = SortAround(centre, fids.Select(fi => faceCentroids[fi]).ToList());

            // ── Store corners so TileHoverOutline can query them ──
            cornerFlat.AddRange(rawC);

            var inC  = rawC.Select(c => IC(vi, c)).ToList();

            int b = mV.Count;
            mV.Add(centre * r); mC.Add(col);
            foreach (var c in inC) { mV.Add(c * r); mC.Add(col); }

            for (int ci = 0; ci < inC.Count; ci++)
                { mT.Add(b); mT.Add(b+1+ci); mT.Add(b+1+(ci+1)%inC.Count); }
        }

        // PASS 2 : EDGE STRIPS AND CLIFFS
        var processed = new HashSet<long>();

        for (int vi = 0; vi < tileCount; vi++)
        {
            foreach (int ni in GetTileNeighbors(vi))
            {
                if (!processed.Add(EKey(vi, ni))) continue;

                var sf = vertFaces[vi].Intersect(vertFaces[ni]).ToList();
                if (sf.Count < 2) continue;

                Vector3 raw0 = faceCentroids[sf[0]];
                Vector3 raw1 = faceCentroids[sf[1]];

                float   rA = R[vi], rB = R[ni];
                Vector3 a0 = IC(vi, raw0)*rA,  a1 = IC(vi, raw1)*rA;
                Vector3 b0 = IC(ni, raw0)*rB,  b1 = IC(ni, raw1)*rB;

                float diff    = rA - rB;
                float absDiff = Mathf.Abs(diff);

                if (absDiff <= CliffThreshold)
                {
                    bool sameBiome = ColorsSimilar(tileColors[vi], tileColors[ni], 0.08f);
                    bool plainsEdge = Mathf.Abs(tileElevations[vi] - 0.40f) < 0.05f
                                || Mathf.Abs(tileElevations[ni] - 0.40f) < 0.05f;

                    Color fc;
                    if (plainsEdge && !sameBiome)
                        fc = new Color(0.50f, 0.50f, 0.50f);
                    else
                        fc = Color.Lerp(tileColors[vi], tileColors[ni], 0.5f);

                    AddQuad(a0, a1, b0, b1, fc, fc);
                }
                else
                {
                    bool aHigh = diff > 0;
                    int  hiTile = aHigh ? vi : ni;

                    Vector3 h0 = aHigh ? a0 : b0,  h1 = aHigh ? a1 : b1;
                    Vector3 l0 = aHigh ? b0 : a0,  l1 = aHigh ? b1 : a1;

                    Color hiC  = tileColors[hiTile];
                    float hiElev = tileElevations[hiTile];
                    Color cliff;
                    if (Mathf.Abs(hiElev - 0.40f) < 0.1f)
                        cliff = new Color(0.50f, 0.48f, 0.44f);
                    else
                        cliff = new Color(
                            hiC.r * 0.50f + 0.08f,
                            hiC.g * 0.42f + 0.06f,
                            hiC.b * 0.30f + 0.05f);

                    AddQuad(h0, h1, l0, l1, cliff, cliff * 0.65f);
                }
            }
        }

        // PASS 3 : CORNER TRIANGLE FILLS
        int faceCount = faceCentroids.Count;
        for (int fi = 0; fi < faceCount; fi++)
        {
            int t0 = facesFlat[fi*3];
            int t1 = facesFlat[fi*3+1];
            int t2 = facesFlat[fi*3+2];

            Vector3 raw = faceCentroids[fi];
            Vector3 p0  = IC(t0, raw) * R[t0];
            Vector3 p1  = IC(t1, raw) * R[t1];
            Vector3 p2  = IC(t2, raw) * R[t2];

            bool sameCorner = ColorsSimilar(tileColors[t0], tileColors[t1], 0.08f)
                           && ColorsSimilar(tileColors[t1], tileColors[t2], 0.08f);
            Color avg = (tileColors[t0] + tileColors[t1] + tileColors[t2]) / 3f;
            Color c   = sameCorner ? avg : avg;

            Vector3 n   = Vector3.Cross(p1-p0, p2-p0);
            Vector3 mid = (p0+p1+p2)/3f;

            int b = mV.Count;
            mV.Add(p0); mV.Add(p1); mV.Add(p2);
            mC.Add(c);  mC.Add(c);  mC.Add(c);

            if (Vector3.Dot(n, mid) >= 0)
                { mT.Add(b); mT.Add(b+1); mT.Add(b+2); }
            else
                { mT.Add(b); mT.Add(b+2); mT.Add(b+1); }
        }

        var mesh = new Mesh { name = "HexPlanet" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(mV);
        mesh.SetTriangles(mT, 0);
        mesh.SetColors(mC);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("HexPlanetMesh");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh    = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = PlanetMaterial ?? MakeMat();
        go.AddComponent<MeshCollider>().sharedMesh  = mesh;

        var sc = gameObject.GetComponent<SphereCollider>()
              ?? gameObject.AddComponent<SphereCollider>();
        sc.radius = PlanetRadius;
    }

    // ══════════════════════════════════════════════════════════════
    // STEP 4 – Atmosphere sphere
    // ══════════════════════════════════════════════════════════════
    void StepBuildAtmosphere()
    {
        if (!ShowAtmosphere) return;

        float atmoRadius = PlanetRadius * AtmosphereScale;
        Mesh atmoMesh = BuildSphereMesh(atmoRadius, 32, 24);

        Material mat = AtmosphereMaterial;

        if (mat == null)
        {
            var shader = Shader.Find("Custom/Atmosphere");
            if (shader == null)
            {
                Debug.LogWarning("[HexPlanet] Atmosphere shader 'Custom/Atmosphere' not found.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            mat = new Material(shader) { name = "AtmosphereMat_Generated" };
        }

        if (mat.HasProperty("_AtmoColor"))     mat.SetColor("_AtmoColor",    AtmosphereColor);
        if (mat.HasProperty("_RimColor"))      mat.SetColor("_RimColor",     AtmosphereRimColor);
        if (mat.HasProperty("_FresnelPower"))  mat.SetFloat("_FresnelPower", AtmospherePower);
        if (mat.HasProperty("_Intensity"))     mat.SetFloat("_Intensity",    AtmosphereIntensity);
        if (mat.HasProperty("_InnerOpacity"))  mat.SetFloat("_InnerOpacity", AtmosphereInnerOpacity);
        if (mat.HasProperty("_RimWidth"))      mat.SetFloat("_RimWidth",     AtmosphereRimWidth);

        var go = new GameObject("Atmosphere");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh        = atmoMesh;
        go.AddComponent<MeshRenderer>().sharedMaterial  = mat;

        Debug.Log($"[HexPlanet] Atmosphere built (radius={atmoRadius:F2}).");
    }

    static Mesh BuildSphereMesh(float radius, int slices, int stacks)
    {
        var verts  = new List<Vector3>();
        var norms  = new List<Vector3>();
        var tris   = new List<int>();

        for (int st = 0; st <= stacks; st++)
        {
            float phi    = Mathf.PI * st / stacks;
            float sinPhi = Mathf.Sin(phi);
            float cosPhi = Mathf.Cos(phi);

            for (int sl = 0; sl <= slices; sl++)
            {
                float theta    = 2f * Mathf.PI * sl / slices;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                var n = new Vector3(sinPhi * cosTheta, cosPhi, sinPhi * sinTheta);
                verts.Add(n * radius);
                norms.Add(-n);
            }
        }

        for (int st = 0; st < stacks; st++)
        for (int sl = 0; sl < slices; sl++)
        {
            int a = st * (slices + 1) + sl;
            int b = a  + slices + 1;

            tris.Add(a);   tris.Add(a+1); tris.Add(b);
            tris.Add(b);   tris.Add(a+1); tris.Add(b+1);
        }

        var mesh = new Mesh { name = "AtmosphereSphere" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ══════════════════════════════════════════════════════════════
    // INTERACTION
    // ══════════════════════════════════════════════════════════════
    public int GetClosestTileId(Vector3 localDir)
    {
        if (tileCount == 0) { Debug.LogWarning("[HexPlanet] tileCount=0."); return -1; }
        int best=-1; float bestDot=-2f;
        for (int i=0;i<tileCount;i++)
        {
            float d=Vector3.Dot(tileCenters[i],localDir);
            if (d>bestDot){bestDot=d;best=i;}
        }
        return best;
    }

    public string GetTileInfo(int id)
    {
        if (id<0||id>=tileCount) return "Invalid tile";
        float e=tileElevations[id];
        float lat=Mathf.Abs(tileCenters[id].y);
        var nb=GetTileNeighbors(id);
        return $"Tile #{id}\n" +
               $"Biome    : {GetBiomeName(id)} ({(e==0f?"Water":"Land")})\n" +
               $"Elevation: {e:F2}  |  Latitude: {lat:F2}\n" +
               $"Neighbours: {nb.Count}";
    }

    public string GetBiomeName(int id)
    {
        if (id < 0 || id >= tileCount) return "Invalid";
        float lat  = Mathf.Abs(tileCenters[id].y);
        int   step = (tileBiomeSteps.Count > id) ? tileBiomeSteps[id] : 0;
        if (step == 0)  return "Ocean";
        if (step == 1)  return "Beach";
        if (lat > 0.82f) return "Polar";
        if (step == 2)  return "Plains";
        if (step == 3)  return "Forest";
        if (step == 4)  return lat > 0.60f ? "Tundra" : "Mountain";
        return lat > 0.50f ? "Snow" : "Peak";
    }

    // ══════════════════════════════════════════════════════════════
    // UTILITIES
    // ══════════════════════════════════════════════════════════════
    static bool ColorsSimilar(Color a, Color b, float threshold) =>
        Mathf.Abs(a.r - b.r) < threshold &&
        Mathf.Abs(a.g - b.g) < threshold &&
        Mathf.Abs(a.b - b.b) < threshold;

    List<Vector3> SortAround(Vector3 axis, List<Vector3> pts)
    {
        Vector3 up  = Mathf.Abs(Vector3.Dot(axis,Vector3.up))<0.99f?Vector3.up:Vector3.right;
        Vector3 tan = Vector3.Cross(axis,up).normalized;
        Vector3 bit = Vector3.Cross(axis,tan).normalized;
        return pts.OrderBy(p=>{
            Vector3 d=p-axis*Vector3.Dot(p,axis);
            return Mathf.Atan2(Vector3.Dot(d,bit),Vector3.Dot(d,tan));
        }).ToList();
    }

    Material MakeMat()
    {
        var s = Shader.Find("Custom/VertexColorURP")
             ?? Shader.Find("Custom/VertexColor")
             ?? Shader.Find("Standard");
        return new Material(s);
    }

    static Vector3 Norm(float x,float y,float z)=>new Vector3(x,y,z).normalized;

    static int Mid(int a,int b,List<Vector3> v,Dictionary<long,int> c)
    {
        long key=((long)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
        if (c.TryGetValue(key,out int r)) return r;
        v.Add(((v[a]+v[b])*0.5f).normalized);
        c[key]=v.Count-1; return v.Count-1;
    }

    static long EKey(int a,int b)=>((long)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
}

// ──────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
[CustomEditor(typeof(HexPlanetGenerator))]
public class HexPlanetGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(8);
        if (GUILayout.Button("🌍  Generate Planet", GUILayout.Height(36)))
        {
            var gen = (HexPlanetGenerator)target;
            gen.Generate();
            EditorUtility.SetDirty(gen);
        }
    }
}
#endif