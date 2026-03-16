using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un seul outline qui suit la tuile survolée.
/// • Par défaut : blanc pulsant.
/// • Après 'A'  : vert (toggle).
/// • 'A' à nouveau : repasse blanc.
/// Le mesh est reconstruit uniquement quand la tuile change.
/// La couleur est swappée instantanément via l'instance de material.
/// </summary>
public class TileHoverOutline : MonoBehaviour
{
    [Header("References")]
    public HexPlanetGenerator Generator;

    [Header("Materials")]
    public Material WhiteOutlineMaterial;
    public Material GreenOutlineMaterial;

    [Header("Style")]
    [Range(0.005f, 0.08f)] public float HeightOffset  = 0.022f;
    [Range(0.03f,  0.30f)] public float LineWidthFrac  = 0.14f;

    // ── État interne ───────────────────────────────────────────────
    private GameObject   _go;
    private MeshFilter   _mf;
    private MeshRenderer _mr;

    private int  _currentTile  = -1;
    private bool _isGreen      = false;   // false = blanc, true = vert

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        _go = new GameObject("_HoverOutlineGO");
        _go.transform.SetParent(transform, false);

        _mf = _go.AddComponent<MeshFilter>();
        _mr = _go.AddComponent<MeshRenderer>();
        _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _mr.receiveShadows    = false;
        _mr.enabled           = false;
    }

    // ── Appelé chaque frame par PlanetController ──────────────────
    public void ShowTile(int tileId)
    {
        // Tuile changée → rebuild le mesh
        if (tileId != _currentTile)
        {
            _currentTile = tileId;

            if (tileId < 0) { _mr.enabled = false; return; }

            Mesh mesh = BuildMesh(tileId);
            if (mesh == null) { _mr.enabled = false; return; }

            _mf.mesh    = mesh;
            _mr.enabled = true;
            ApplyMaterial();
        }
    }

    public void HideHover()
    {
        _currentTile  = -1;
        _mr.enabled   = false;
    }

    // ── Appelé par PlanetController quand 'A' est pressé ─────────
    public void ToggleColor()
    {
        _isGreen = !_isGreen;
        ApplyMaterial();
        Debug.Log($"[HoverOutline] Toggle → {(_isGreen ? "VERT" : "BLANC")}");
    }

    // ──────────────────────────────────────────────────────────────
    void ApplyMaterial()
    {
        if (!_mr.enabled) return;
        Material src = _isGreen ? GreenOutlineMaterial : WhiteOutlineMaterial;
        if (src == null) { }
        _mr.material = src;   // instancie automatiquement une copie locale
    }

    // ──────────────────────────────────────────────────────────────
    Mesh BuildMesh(int tileId)
    {
        var corners = Generator.GetTileRawCorners(tileId);
        if (corners == null || corners.Count < 3)
        {
            Debug.LogWarning($"[HoverOutline] Tuile {tileId} : {corners?.Count ?? -1} coins — régénère la planète !");
            return null;
        }

        Vector3 center = Generator.GetTileCenter(tileId);
        float   elev   = Generator.GetTileElevation(tileId);
        float   r      = Generator.PlanetRadius
                       + (elev == 0f ? 0.20f : elev) * Generator.ElevationScale
                       + HeightOffset;
        float outerT   = Generator.TileInset;
        float innerT   = Mathf.Max(0f, outerT - LineWidthFrac);

        int n     = corners.Count;
        var verts = new List<Vector3>(n * 2);
        var tris  = new List<int>(n * 6);

        for (int i = 0; i < n; i++)
        {
            verts.Add(Vector3.Lerp(center, corners[i], outerT).normalized * r);
            verts.Add(Vector3.Lerp(center, corners[i], innerT).normalized * r);
        }

        for (int i = 0; i < n; i++)
        {
            int o0 = i * 2,           i0 = o0 + 1;
            int o1 = (i + 1) % n * 2, i1 = o1 + 1;
            tris.Add(o0); tris.Add(o1); tris.Add(i0);
            tris.Add(o1); tris.Add(i1); tris.Add(i0);
        }

        var mesh = new Mesh { name = $"Outline_{tileId}" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        return mesh;
    }
}