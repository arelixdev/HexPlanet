using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexMeshBuilder : MonoBehaviour
{
    private HexGrid _grid;
    private float _planetRadius = 10f;
    private float _elevationScale = 0.8f;

    public void Build(HexGrid grid, float radius = 10f)
    {
        _grid = grid;
        _planetRadius = radius;

        var meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = BuildMesh();
    }

    Mesh BuildMesh()
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();

        foreach(var tile in _grid.Tiles)
        {
            if(tile.Corners.Count < 3) continue;
            BuildTileMesh(tile, verts, tris, uvs, colors);
        }

        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32};
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    void BuildTileMesh(HexTiles tile, List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> colors)
    {
        float elev = tile.IsWater ? 0.35f : tile.Elevation;
        float r = _planetRadius + elev * _elevationScale;
        Color c = GetBiomeColor(tile.Biome);

        int baseIdx = verts.Count;

        verts.Add(tile.Center * r);
        uvs.Add(new Vector2(0.5f, 0.5f));
        colors.Add(c);

        var sortedCorners = SortCorners(tile.Center, tile.Corners);
        foreach(var corner in sortedCorners)
        {
            float cr = _planetRadius + elev * _elevationScale * 0.95f;
            verts.Add(corner * cr);
            uvs.Add(new Vector2(corner.x * 0.5f + 0.5f, corner.z * 0.5f + 0.5f));
            colors.Add(c * 0.92f);
        }

        for(int i = 0; i < sortedCorners.Count; i++)
        {
            tris.Add(baseIdx);
            tris.Add(baseIdx + 1 + i);
            tris.Add(baseIdx + 1 + (i+1) % sortedCorners.Count);
        }

        BuildTileEdges(tile, sortedCorners, r, verts, tris, uvs, colors);
    }

    List<Vector3> SortCorners(Vector3 center, List<Vector3> corners)
    {
        var tangent = Vector3.Cross(center, Vector3.up).normalized;
        var bitangent = Vector3.Cross(center, tangent).normalized;

        return corners.OrderBy(c => Mathf.Atan2(Vector3.Dot(c - center, bitangent), Vector3.Dot(c - center, tangent))).ToList();
    }

    void BuildTileEdges(HexTiles tile, List<Vector3> sortedCorners, float r, List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> colors)
    {
        if(tile.IsWater) return; 

        float baseR = _planetRadius + 0.35f * _elevationScale;
        Color edgeColor = GetBiomeColor(tile.Biome) * 0.75f;

        for(int i = 0; i < sortedCorners.Count; i++)
        {
            Vector3 a = sortedCorners[i] * r;
            Vector3 b = sortedCorners[(i + 1) % sortedCorners.Count] * r;
            Vector3 aBase = sortedCorners[i] * baseR;
            Vector3 bBase = sortedCorners[(i + 1) % sortedCorners.Count] * baseR;

            int idx = verts.Count;
            verts.AddRange(new[]{a, b, aBase, bBase});
            uvs.AddRange(new Vector2[] {Vector2.zero, Vector2.right, Vector2.up, Vector2.one});
            colors.AddRange(new[] {edgeColor, edgeColor, edgeColor, edgeColor});

            tris.AddRange(new[] {idx, idx+1, idx+2, idx+1, idx+3, idx+2});
        }
    }

    Color GetBiomeColor(BiomeType biome) => biome switch
    {
        BiomeType.Ocean    => new Color(0.1f, 0.3f, 0.7f),
        BiomeType.Beach    => new Color(0.9f, 0.85f, 0.65f),
        BiomeType.Plains   => new Color(0.45f, 0.7f, 0.25f),
        BiomeType.Forest   => new Color(0.15f, 0.45f, 0.1f),
        BiomeType.Mountain => new Color(0.5f, 0.45f, 0.4f),
        BiomeType.Snow     => new Color(0.92f, 0.95f, 1f),
        BiomeType.Desert   => new Color(0.85f, 0.75f, 0.35f),
        BiomeType.Tundra   => new Color(0.6f, 0.65f, 0.55f),
        _                  => Color.white
    };
}
