using System.Collections.Generic;
using UnityEngine;

public static class IcoSphere 
{
    public static List<Vector3> Generate(int subdivisions)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;

        var verts = new List<Vector3>
        {
          new(-1, t, 0), new(1, t, 0),
          new(-1, -t, 0), new(1, -t, 0),
          new(0, -1, t), new(0, 1, t),
          new(0, -1, -t), new(0, 1, -t),
          new(t, 0, -1), new(t, 0, 1),
          new(-t, 0, -1), new(-t, 0, 1)  
        };

        for(int i = 0; i < verts.Count; i++)
            verts[i] = verts[i].normalized;

        var faces = new List<(int, int, int)>
        {
            (0,11,5), (0,5,1), (0,1,7), (0,7,10), (0,10,11),
            (1,5,9), (5,11,4), (11,10,2), (10,7,6), (7,1,8),
            (3,9,4), (3,4,2), (3,2,6), (3,6,8), (3,8,9),
            (4,9,5), (2,4,11), (6,2,10), (8,6,7), (9,8,1)
        };

        //Subs
        var midCache = new Dictionary<long, int>();
        for (int s = 0; s < subdivisions; s++)
        {
            var newFaces = new List<(int, int, int)>();
            foreach(var (a,b,c) in faces)
            {
                int ab = GetMidPoint(a,b, verts, midCache);
                int bc = GetMidPoint(b,c, verts, midCache);
                int ca = GetMidPoint(c,a, verts, midCache);

                newFaces.Add((a,ab,ca));
                newFaces.Add((b, bc,ab));
                newFaces.Add((c, ca, bc));
                newFaces.Add((ab, bc, ca));
            }

            faces = newFaces;
            midCache.Clear();
        }

        return verts;
    }

    static int GetMidPoint(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
    {
        long key = ((long)Mathf.Min(a,b) << 32) | (uint)Mathf.Max(a,b);
        if(cache.TryGetValue(key, out int idx)) return idx;
        Vector3 mid = ((verts[a] + verts[b]) / 2).normalized;
        verts.Add(mid);
        cache[key] = verts.Count - 1;
        return verts.Count-1;
    }
}
