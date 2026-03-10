using System.Collections.Generic;
using UnityEngine;

// ══════════════════════════════════════════════════════════════════
// Catalogue de formes procédurales pour les objets de tuile
// Pour ajouter une forme : créer une nouvelle classe héritant de TileShape
// ══════════════════════════════════════════════════════════════════

public abstract class TileShape
{
    // Génère un Mesh orienté "vers le haut" (axe Y local = normal de la planète)
    // scale = taille relative (1 = taille par défaut)
    public abstract Mesh Build(float scale);
}

// ── Pyramide carrée (arbres stylisés) ─────────────────────────────
public class PyramidShape : TileShape
{
    public float BaseSize   = 0.12f;
    public float Height     = 0.30f;
    public Color BaseColor  = new Color(0.10f, 0.35f, 0.08f);
    public Color ApexColor  = new Color(0.20f, 0.55f, 0.12f);

    public override Mesh Build(float scale)
    {
        float b = BaseSize * scale * 0.5f;
        float h = Height   * scale;

        // 4 sommets de base + 1 apex
        var v = new Vector3[]
        {
            new(-b, 0,  b),  // 0
            new( b, 0,  b),  // 1
            new( b, 0, -b),  // 2
            new(-b, 0, -b),  // 3
            new( 0, h,  0),  // 4 apex
        };

        // Couleurs par vertex
        var c = new Color[] { BaseColor, BaseColor, BaseColor, BaseColor, ApexColor };

        // Triangles (4 faces latérales + 1 fond)
        var t = new int[]
        {
            0,1,4,  1,2,4,  2,3,4,  3,0,4,   // faces
            0,3,2,  0,2,1                      // base
        };

        var m = new Mesh { name = "Pyramid" };
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.SetColors(c);
        m.RecalculateNormals();
        return m;
    }
}

// ── Cône (arbres ronds) ───────────────────────────────────────────
public class ConeShape : TileShape
{
    public int   Segments  = 6;
    public float Radius    = 0.08f;
    public float Height    = 0.28f;
    public Color BaseColor = new Color(0.10f, 0.35f, 0.08f);
    public Color ApexColor = new Color(0.22f, 0.58f, 0.14f);

    public override Mesh Build(float scale)
    {
        float r = Radius * scale;
        float h = Height * scale;

        var verts  = new List<Vector3>();
        var colors = new List<Color>();
        var tris   = new List<int>();

        // Apex
        verts.Add(new Vector3(0, h, 0)); colors.Add(ApexColor);

        // Cercle de base
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            verts.Add(new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            colors.Add(BaseColor);
        }

        // Centre base
        int centerIdx = verts.Count;
        verts.Add(Vector3.zero); colors.Add(BaseColor);

        // Faces latérales
        for (int i = 0; i < Segments; i++)
        {
            tris.Add(0);
            tris.Add(1 + i);
            tris.Add(1 + (i + 1) % Segments);
        }

        // Fond
        for (int i = 0; i < Segments; i++)
        {
            tris.Add(centerIdx);
            tris.Add(1 + (i + 1) % Segments);
            tris.Add(1 + i);
        }

        var m = new Mesh { name = "Cone" };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.SetColors(colors);
        m.RecalculateNormals();
        return m;
    }
}

// ── Cristal (montagne / biome arctique) ───────────────────────────
public class CrystalShape : TileShape
{
    public float BaseSize = 0.08f;
    public float Height   = 0.35f;
    public Color Color    = new Color(0.70f, 0.88f, 1.00f);

    public override Mesh Build(float scale)
    {
        float b = BaseSize * scale * 0.5f;
        float h = Height   * scale;
        float m = h * 0.15f; // point inférieur légèrement sous la base

        var v = new Vector3[]
        {
            new(-b, 0,  b),  // 0
            new( b, 0,  b),  // 1
            new( b, 0, -b),  // 2
            new(-b, 0, -b),  // 3
            new( 0, h,  0),  // 4 apex haut
            new( 0,-m,  0),  // 5 apex bas
        };

        var c = new Color[]
        {
            Color * 0.7f, Color * 0.7f, Color * 0.7f, Color * 0.7f,
            Color, Color * 0.5f
        };

        var t = new int[]
        {
            0,1,4,  1,2,4,  2,3,4,  3,0,4,   // faces hautes
            0,5,1,  1,5,2,  2,5,3,  3,5,0    // faces basses
        };

        var mesh = new Mesh { name = "Crystal" };
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.SetColors(c);
        mesh.RecalculateNormals();
        return mesh;
    }
}
