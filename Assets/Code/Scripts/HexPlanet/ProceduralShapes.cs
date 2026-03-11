using System.Collections.Generic;
using UnityEngine;

// ══════════════════════════════════════════════════════════════════
// Catalogue de formes procédurales pour les objets de tuile
// ══════════════════════════════════════════════════════════════════

public abstract class TileShape
{
    public abstract Mesh Build(float scale);
}

// ── Pyramide carrée (legacy) ───────────────────────────────────────
public class PyramidShape : TileShape
{
    public float BaseSize  = 0.12f;
    public float Height    = 0.30f;
    public Color BaseColor = new Color(0.10f, 0.35f, 0.08f);
    public Color ApexColor = new Color(0.20f, 0.55f, 0.12f);

    public override Mesh Build(float scale)
    {
        float b = BaseSize * scale * 0.5f;
        float h = Height   * scale;
        var v = new Vector3[] {
            new(-b,0,b), new(b,0,b), new(b,0,-b), new(-b,0,-b), new(0,h,0),
        };
        var c = new Color[] { BaseColor, BaseColor, BaseColor, BaseColor, ApexColor };
        var t = new int[]   { 0,1,4, 1,2,4, 2,3,4, 3,0,4, 0,3,2, 0,2,1 };
        var m = new Mesh { name = "Pyramid" };
        m.SetVertices(v); m.SetTriangles(t, 0); m.SetColors(c);
        m.RecalculateNormals();
        return m;
    }
}

// ── Sapin – 3 cônes empilés ────────────────────────────────────────
// La hauteur normalisée du vertex (0 = sol, 1 = pointe) est encodée
// dans le canal ALPHA de la vertex color.
// Le shader FirTreeSnow lit cet alpha pour calculer la neige
// depuis la pointe vers le bas, piloté par la latitude mondiale.
// RGB = couleur verte de base (multipliée par _TreeColor dans le shader).
public class FirTreeShape : TileShape
{
    public int   Segments      = 7;
    public float BottomRadius  = 0.120f;
    public float BottomHeight  = 0.160f;
    public float MidRadius     = 0.085f;
    public float MidHeight     = 0.140f;
    public float TopRadius     = 0.050f;
    public float TopHeight     = 0.120f;
    public float OverlapFactor = 0.30f;

    // Couleurs RGB baked dans les vertex (indépendantes de la neige)
    public Color DarkGreen  = new Color(0.08f, 0.28f, 0.06f);
    public Color MidGreen   = new Color(0.14f, 0.42f, 0.10f);
    public Color LightGreen = new Color(0.22f, 0.55f, 0.14f);
    public Color TrunkColor = new Color(0.30f, 0.18f, 0.08f);
    public float TrunkRadius = 0.018f;
    public float TrunkHeight = 0.055f;

    // Footprint pour le placement sans overlap dans TileObjectSpawner
    public float Footprint => BottomRadius * 0.9f;

    public override Mesh Build(float scale)
    {
        var verts  = new List<Vector3>();
        var colors = new List<Color>();
        var tris   = new List<int>();

        float th = TrunkHeight * scale;
        float bH = BottomHeight * scale;
        float mH = MidHeight    * scale;
        float tH = TopHeight    * scale;
        float ov = OverlapFactor;

        float y0 = th;
        float y1 = y0 + bH * (1f - ov);
        float y2 = y1 + mH * (1f - ov);
        float totalH = y2 + tH;   // apex du cône supérieur = hauteur totale

        // ── Tronc ──────────────────────────────────────────────────
        float tr = TrunkRadius * scale;
        BuildCylinder(verts, colors, tris,
                      baseY: 0f, topY: th, totalH: totalH,
                      baseRadius: tr, topRadius: tr * 0.7f,
                      col: TrunkColor, segments: 5);

        // ── 3 cônes ────────────────────────────────────────────────
        BuildCone(verts, colors, tris,
                  baseY: y0, apexY: y0 + bH, totalH: totalH,
                  radius: BottomRadius * scale,
                  baseCol: DarkGreen, apexCol: MidGreen);

        BuildCone(verts, colors, tris,
                  baseY: y1, apexY: y1 + mH, totalH: totalH,
                  radius: MidRadius * scale,
                  baseCol: MidGreen, apexCol: LightGreen);

        BuildCone(verts, colors, tris,
                  baseY: y2, apexY: y2 + tH, totalH: totalH,
                  radius: TopRadius * scale,
                  baseCol: MidGreen, apexCol: LightGreen);

        var mesh = new Mesh { name = "FirTree" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        return mesh;
    }

    // Encode la hauteur normalisée dans l'alpha du vertex
    static Color WithAlpha(Color c, float localY, float totalH)
        => new Color(c.r, c.g, c.b, Mathf.Clamp01(localY / totalH));

    void BuildCone(List<Vector3> verts, List<Color> colors, List<int> tris,
                   float baseY, float apexY, float totalH, float radius,
                   Color baseCol, Color apexCol)
    {
        int baseStart = verts.Count;

        // Apex
        verts.Add(new Vector3(0, apexY, 0));
        colors.Add(WithAlpha(apexCol, apexY, totalH));
        int apexIdx = baseStart;

        // Cercle de base
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            verts.Add(new Vector3(Mathf.Cos(a) * radius, baseY, Mathf.Sin(a) * radius));
            colors.Add(WithAlpha(baseCol, baseY, totalH));
        }

        // Centre de la base
        int centerIdx = verts.Count;
        verts.Add(new Vector3(0, baseY, 0));
        colors.Add(WithAlpha(baseCol * 0.85f, baseY, totalH));

        for (int i = 0; i < Segments; i++)
        {
            tris.Add(apexIdx);
            tris.Add(baseStart + 1 + (i + 1) % Segments);
            tris.Add(baseStart + 1 + i);
        }
        for (int i = 0; i < Segments; i++)
        {
            tris.Add(centerIdx);
            tris.Add(baseStart + 1 + i);
            tris.Add(baseStart + 1 + (i + 1) % Segments);
        }
    }

    void BuildCylinder(List<Vector3> verts, List<Color> colors, List<int> tris,
                       float baseY, float topY, float totalH,
                       float baseRadius, float topRadius, Color col, int segments)
    {
        int startIdx = verts.Count;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
            verts.Add(new Vector3(cx * baseRadius, baseY, cz * baseRadius));
            verts.Add(new Vector3(cx * topRadius,  topY,  cz * topRadius));
            colors.Add(WithAlpha(col,        baseY, totalH));
            colors.Add(WithAlpha(col * 1.1f, topY,  totalH));
        }
        for (int i = 0; i < segments; i++)
        {
            int b0 = startIdx + i * 2;
            int b1 = startIdx + ((i + 1) % segments) * 2;
            tris.Add(b0); tris.Add(b1);     tris.Add(b0 + 1);
            tris.Add(b1); tris.Add(b1 + 1); tris.Add(b0 + 1);
        }
    }
}

// ── Cône simple (legacy) ──────────────────────────────────────────
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

        verts.Add(new Vector3(0, h, 0)); colors.Add(ApexColor);
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            verts.Add(new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            colors.Add(BaseColor);
        }
        int ci = verts.Count;
        verts.Add(Vector3.zero); colors.Add(BaseColor);
        for (int i = 0; i < Segments; i++)
        { tris.Add(0); tris.Add(1 + i); tris.Add(1 + (i + 1) % Segments); }
        for (int i = 0; i < Segments; i++)
        { tris.Add(ci); tris.Add(1 + (i+1) % Segments); tris.Add(1 + i); }

        var m = new Mesh { name = "Cone" };
        m.SetVertices(verts); m.SetTriangles(tris, 0); m.SetColors(colors);
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
        float m = h * 0.15f;
        var v = new Vector3[] {
            new(-b,0,b), new(b,0,b), new(b,0,-b), new(-b,0,-b),
            new(0,h,0),  new(0,-m,0),
        };
        var c = new Color[] {
            Color*0.7f, Color*0.7f, Color*0.7f, Color*0.7f,
            Color, Color*0.5f
        };
        var t = new int[] {
            0,1,4, 1,2,4, 2,3,4, 3,0,4,
            0,5,1, 1,5,2, 2,5,3, 3,5,0
        };
        var mesh = new Mesh { name = "Crystal" };
        mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.SetColors(c);
        mesh.RecalculateNormals();
        return mesh;
    }
}