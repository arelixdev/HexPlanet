using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HexGrid
{
    public List<HexTiles> Tiles = new();
    private float neighborThreshold = 0.42f;

    public void Build(int subdivisions)
    {
        var verts = IcoSphere.Generate(subdivisions);

        for (int i = 0; i < verts.Count; i++)
        {
            Tiles.Add(new HexTiles
            {
                Id = i,
                Center = verts[i],
                Neighbors = new List<int>(),
                Corners = new List<Vector3>()
            });
        }

        float threshold = GetNeighborThreshold(subdivisions);
        for (int i = 0; i < Tiles.Count; i++)
        {
            for (int j = i+1; j < Tiles.Count; j++)
            {
                float dist = Vector3.Distance(Tiles[i].Center, Tiles[j].Center);

                if(dist < threshold)
                {
                    Tiles[i].Neighbors.Add(j);
                    Tiles[j].Neighbors.Add(i);
                } 
            }
        }

        BuildCorners();
    }

    float GetNeighborThreshold(int sub)
    {
        return sub switch
        {
            1 => 0.72f, 2 => 0.42f, 3 => 0.22f,
            4 => 0.115f, 5 => 0.058f, _ => 0.42f
        };
    }

    void BuildCorners()
    {
        for (int i = 0; i < Tiles.Count; i++)
        {
            var tile = Tiles[i];
            var corners = new List<Vector3>();

            for (int ni = 0; ni < tile.Neighbors.Count; ni++)
            {
                int na = tile.Neighbors[ni];
                int nb = tile.Neighbors[(ni + 1) % tile.Neighbors.Count];

                if(Tiles[na].Neighbors.Contains(nb))
                {
                    Vector3 corner = (tile.Center + Tiles[na].Center + Tiles[nb].Center).normalized;
                    corners.Add(corner);
                }
            }

            tile.Corners = corners;
        }
    }

    public HexTiles GetTile(int id) => Tiles[id];
    public List<HexTiles> GetNeighbors(int id) => Tiles[id].Neighbors.Select(n => Tiles[n]).ToList();
}
