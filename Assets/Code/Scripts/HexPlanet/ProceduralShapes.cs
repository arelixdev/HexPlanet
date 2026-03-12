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

// ── Arbre dôme (tropical / équatorial) ───────────────────────────
// Tronc cylindrique + canopée en demi-sphère aplatie ou étirée.
// DomeScaleY < 1  →  dôme écrasé (savane / chêne)
// DomeScaleY > 1  →  dôme étiré  (palmier / eucalyptus)
// La règle de mélange Dôme↔Pin est entièrement gérée par TileObjectSpawner.
public class DomeTreeShape : TileShape
{
    public int   Segments     = 8;
    public int   Rings        = 5;

    // Tronc
    public float TrunkRadius  = 0.018f;
    public float TrunkHeight  = 0.14f;
    public Color TrunkColor   = new Color(0.32f, 0.20f, 0.09f);

    // Canopée
    public float DomeRadius   = 0.13f;
    /// <summary>Échelle verticale du dôme : 0.6 = aplati (savane), 1.4 = allongé (palmier).</summary>
    public float DomeScaleY   = 1.0f;
    public Color DomeColorBot = new Color(0.18f, 0.58f, 0.12f);
    public Color DomeColorTop = new Color(0.35f, 0.75f, 0.18f);

    /// <summary>Empreinte au sol utilisée par TileObjectSpawner pour éviter les overlaps.</summary>
    public float Footprint => DomeRadius * 0.95f;

    public override Mesh Build(float scale)
    {
        var verts  = new List<Vector3>();
        var colors = new List<Color>();
        var tris   = new List<int>();

        float tr  = TrunkRadius * scale;
        float th  = TrunkHeight * scale;
        float dr  = DomeRadius  * scale;
        // Centre du dôme : légèrement au-dessus du haut du tronc
        float dcy = th + dr * DomeScaleY * 0.20f;

        // ── Tronc ──────────────────────────────────────────────────
        DomeCylinder(verts, colors, tris,
                     baseY: 0f, topY: th,
                     baseR: tr, topR: tr * 0.65f,
                     col: TrunkColor, seg: 6);

        // ── Dôme (demi-sphère UV, de phi≈−PI/5 à phi=PI/2) ────────
        // phiBot légèrement négatif → la canopée enveloppe bien le haut du tronc
        const float phiBot = -0.628f;   // ≈ −PI/5
        const float phiTop =  1.571f;   // ≈   PI/2

        int seg      = Segments;
        int domeBase = verts.Count;

        // Sommet
        verts.Add(new Vector3(0f, dcy + dr * DomeScaleY, 0f));
        colors.Add(DomeColorTop);

        // Anneaux de phiTop → phiBot
        for (int r = 0; r <= Rings; r++)
        {
            float phi   = Mathf.Lerp(phiTop, phiBot, (float)r / Rings);
            float ringY = dcy + Mathf.Sin(phi) * dr * DomeScaleY;
            float ringR = Mathf.Cos(phi) * dr;
            float t     = 1f - (float)r / Rings;
            Color c     = Color.Lerp(DomeColorBot, DomeColorTop, t * t);

            for (int s = 0; s < seg; s++)
            {
                float a = s * Mathf.PI * 2f / seg;
                verts.Add(new Vector3(Mathf.Cos(a) * ringR, ringY, Mathf.Sin(a) * ringR));
                colors.Add(c);
            }
        }

        // Centre bas (ferme le bas de la canopée)
        int   botCenter = verts.Count;
        float botY      = dcy + Mathf.Sin(phiBot) * dr * DomeScaleY;
        verts.Add(new Vector3(0f, botY, 0f));
        colors.Add(DomeColorBot * 0.80f);

        // ── Triangulation ──────────────────────────────────────────
        int topIdx = domeBase;

        // Calotte supérieure
        for (int s = 0; s < seg; s++)
        {
            tris.Add(topIdx);
            tris.Add(domeBase + 1 + (s + 1) % seg);
            tris.Add(domeBase + 1 + s);
        }

        // Bandes latérales
        for (int r = 0; r < Rings; r++)
        {
            int row0 = domeBase + 1 + r * seg;
            int row1 = row0 + seg;
            for (int s = 0; s < seg; s++)
            {
                int a = row0 + s,             b = row0 + (s + 1) % seg;
                int c = row1 + s,             d = row1 + (s + 1) % seg;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        }

        // Calotte inférieure
        int lastRow = domeBase + 1 + Rings * seg;
        for (int s = 0; s < seg; s++)
        {
            tris.Add(botCenter);
            tris.Add(lastRow + s);
            tris.Add(lastRow + (s + 1) % seg);
        }

        var mesh = new Mesh { name = "DomeTree" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        return mesh;
    }

    // Cylindre autonome (pas de dépendance à FirTreeShape)
    void DomeCylinder(List<Vector3> verts, List<Color> colors, List<int> tris,
                      float baseY, float topY, float baseR, float topR,
                      Color col, int seg)
    {
        int start = verts.Count;
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
            verts.Add(new Vector3(cx * baseR, baseY, cz * baseR)); colors.Add(col);
            verts.Add(new Vector3(cx * topR,  topY,  cz * topR));  colors.Add(col * 1.1f);
        }
        for (int i = 0; i < seg; i++)
        {
            int b0 = start + i * 2;
            int b1 = start + ((i + 1) % seg) * 2;
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