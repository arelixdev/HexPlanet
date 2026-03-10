using UnityEngine;

public class BiomeGenerator
{
    private int _seed;
    private float _planetRadius;

    public BiomeGenerator(int seed, float radius = 10f)
    {
        _seed = seed;
        _planetRadius = radius;
        Random.InitState(seed);
    }

    public void GenerateElevation(HexGrid grid, int octaves = 5)
    {
        Vector3 offset = new(
            Random.Range(-1000,1000),
            Random.Range(-1000,1000),
            Random.Range(-1000,1000)
        );

        foreach(var tile in grid.Tiles)
        {
            float elev = 0f;
            float amplitude = 1f, frequency = 1.8f, maxVal = 0f;

            for (int o = 0; o < octaves; o++)
            {
                Vector3 p = (tile.Center + offset) * frequency;
                elev += Mathf.PerlinNoise(p.x, p.y) * amplitude;

                elev += Mathf.PerlinNoise(p.y, p.z) * amplitude * 0.5f;
                maxVal += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            tile.Elevation = elev / maxVal;
            tile.Elevation = Mathf.Pow(tile.Elevation, 1.3f);
        }

        NormalizeElevation(grid);
    }

    void NormalizeElevation(HexGrid grid)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach(var t in grid.Tiles)
        {
            min = Mathf.Min(min, t.Elevation);
            max = Mathf.Max(max, t.Elevation);
        }
        foreach(var t in grid.Tiles)
        {
            t.Elevation = Mathf.InverseLerp(min, max, t.Elevation);
        }
    }

    public void GenerateMoisture(HexGrid grid)
    {
        Vector3 offset = new(Random.Range(-500f,500f), Random.Range(-500f,500f), 0);

        foreach(var tile in grid.Tiles)
        {
            Vector3 p = (tile.Center + offset) * 2.5f;
            tile.Moisture = Mathf.PerlinNoise(p.x, p.z);
        }
    }

    public void GenerateTemperature(HexGrid grid)
    {
        foreach(var tile in grid.Tiles)
        {
            float latitude = Mathf.Abs(tile.Center.y);
            tile.Temperature = 1f - latitude - tile.Elevation * 0.4f;
            tile.Temperature = Mathf.Clamp01(tile.Temperature);
        }
    }

    public void AssignBiomes(HexGrid grid)
    {
        foreach (var tile in grid.Tiles)
        {
            tile.Biome = GetBiome(tile.Elevation, tile.Moisture, tile.Temperature);
        }
    }

    BiomeType GetBiome(float elev, float moisture, float temp)
    {
        if (elev < 0.35f) return BiomeType.Ocean;
        if (elev < 0.38f) return BiomeType.Beach;
        if (temp < 0.15f) return moisture > 0.5f ? BiomeType.Tundra : BiomeType.Snow;
        if (elev > 0.8f)  return temp < 0.3f ? BiomeType.Snow : BiomeType.Mountain;
        if (temp > 0.7f)  return moisture < 0.3f ? BiomeType.Desert : BiomeType.Plains;
        if (moisture > 0.6f) return BiomeType.Forest;
        return BiomeType.Plains;
    }
}
