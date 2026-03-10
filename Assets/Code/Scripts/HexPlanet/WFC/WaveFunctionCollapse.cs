using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class TileRule
{
    public int StateId;
    public BiomeType RequiredBiome;
    public List<int> AllowedNeighborStates;
    public float Weight;
}

public class WaveFunctionCollapse
{
    private List<TileRule> _rules;
    private HexGrid _grid;

    public WaveFunctionCollapse(HexGrid grid, List<TileRule> rules)
    {
        _grid = grid;
        _rules = rules;
        Initialize();
    }

    void Initialize()
    {
        foreach(var tile in _grid.Tiles)
        {
            tile.PossibleStates = _rules
            .Where(r => r.RequiredBiome == tile.Biome)
            .Select(r => r.StateId)
            .ToList();

            if(tile.PossibleStates.Count == 0)
                tile.PossibleStates = new List<int> {0};
        }
    }

    public void Collapse()
    {
        var uncollapsed = new List<HexTiles>(_grid.Tiles.Where(t => !t.IsCollapsed));

        while(uncollapsed.Count > 0)
        {
            var tile = uncollapsed
                .OrderBy(t => t.Entropy + UnityEngine.Random.Range(0f, 0.1f))
                .First();

            CollapseOne(tile);
            uncollapsed.Remove(tile);

            Propagate(tile);
        }
    }

    void CollapseOne(HexTiles tile)
    {
        float totalWeight = tile.PossibleStates
            .Sum(s => _rules.FirstOrDefault(r => r.StateId == s)?.Weight ?? 1);
        float rand = UnityEngine.Random.Range(0f, totalWeight);
        float cumul = 0f;

        foreach(int state in tile.PossibleStates)
        {
            cumul += _rules.FirstOrDefault(r => r.StateId == state)?.Weight ?? 1f;
            if(rand <= cumul) { tile.CollapsedState = state; return;}
        }
        tile.CollapsedState = tile.PossibleStates[0];
    }

    void Propagate(HexTiles source)
    {
        var queue = new Queue<HexTiles>();
        queue.Enqueue(source);

        while(queue.Count > 0)
        {
            var current = queue.Dequeue();
            var allowed = GetAllowedNeighborState(current.CollapsedState);

            foreach(int nId in current.Neighbors)
            {
                var neighbor = _grid.GetTile(nId);
                if(neighbor.IsCollapsed) continue;

                int before = neighbor.PossibleStates.Count;
                neighbor.PossibleStates = neighbor.PossibleStates.Intersect(allowed).ToList();

                if(neighbor.PossibleStates.Count == 0)
                    neighbor.PossibleStates = allowed.ToList();

                if(neighbor.PossibleStates.Count != before)
                    queue.Enqueue(neighbor);
            }
        }

        
    }

    List<int> GetAllowedNeighborState(int stateId)
    {
        return _rules
            .FirstOrDefault(r => r.StateId == stateId)
            ?.AllowedNeighborStates ?? _rules.Select(r => r.StateId).ToList();
    }
}
