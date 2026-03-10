using System;
using System.Collections.Generic;
using UnityEngine;

public enum BiomeType {Ocean, Beach, Plains, Forest, Mountain, Snow, Desert, Tundra}
public enum TileType {Flat, Slope, Peak, Coast, River}

[Serializable]
public class HexTiles
{
    public int Id;
    public Vector3 Center;
    public List<int> Neighbors;
    public List<Vector3> Corners;

    public float Elevation;
    public float Moisture;
    public float Temperature;

    public BiomeType Biome;
    public TileType Type;
    public bool IsWater => Elevation < 0.35;
    public bool IsLand => !IsWater;

    //WFC
    public List<int> PossibleStates;
    public int CollapsedState = -1;
    public bool IsCollapsed => CollapsedState >= 0;
    public float Entropy => PossibleStates?.Count ?? 0;
}
