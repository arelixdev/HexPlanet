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

    public Color DarkGreen  = new Color(0.08f, 0.28f, 0.06f);
    public Color MidGreen   = new Color(0.14f, 0.42f, 0.10f);
    public Color LightGreen = new Color(0.22f, 0.55f, 0.14f);
    public Color TrunkColor = new Color(0.30f, 0.18f, 0.08f);
    public float TrunkRadius = 0.018f;
    public float TrunkHeight = 0.055f;

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
        float totalH = y2 + tH;

        float tr = TrunkRadius * scale;
        BuildCylinder(verts, colors, tris, 0f, th, totalH, tr, tr * 0.7f, TrunkColor, 5);
        BuildCone(verts, colors, tris, y0, y0 + bH, totalH, BottomRadius * scale, DarkGreen, MidGreen);
        BuildCone(verts, colors, tris, y1, y1 + mH, totalH, MidRadius * scale, MidGreen, LightGreen);
        BuildCone(verts, colors, tris, y2, y2 + tH, totalH, TopRadius * scale, MidGreen, LightGreen);

        var mesh = new Mesh { name = "FirTree" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        return mesh;
    }

    static Color WithAlpha(Color c, float localY, float totalH)
        => new Color(c.r, c.g, c.b, Mathf.Clamp01(localY / totalH));

    void BuildCone(List<Vector3> verts, List<Color> colors, List<int> tris,
                   float baseY, float apexY, float totalH, float radius,
                   Color baseCol, Color apexCol)
    {
        int baseStart = verts.Count;
        verts.Add(new Vector3(0, apexY, 0));
        colors.Add(WithAlpha(apexCol, apexY, totalH));
        int apexIdx = baseStart;

        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            verts.Add(new Vector3(Mathf.Cos(a) * radius, baseY, Mathf.Sin(a) * radius));
            colors.Add(WithAlpha(baseCol, baseY, totalH));
        }
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
public class DomeTreeShape : TileShape
{
    public int   Segments     = 8;
    public int   Rings        = 5;
    public float TrunkRadius  = 0.018f;
    public float TrunkHeight  = 0.14f;
    public Color TrunkColor   = new Color(0.32f, 0.20f, 0.09f);
    public float DomeRadius   = 0.13f;
    public float DomeScaleY   = 1.0f;
    public Color DomeColorBot = new Color(0.18f, 0.58f, 0.12f);
    public Color DomeColorTop = new Color(0.35f, 0.75f, 0.18f);

    public float Footprint => DomeRadius * 0.95f;

    public override Mesh Build(float scale)
    {
        var verts  = new List<Vector3>();
        var colors = new List<Color>();
        var tris   = new List<int>();

        float tr  = TrunkRadius * scale;
        float th  = TrunkHeight * scale;
        float dr  = DomeRadius  * scale;
        float dcy = th + dr * DomeScaleY * 0.20f;

        DomeCylinder(verts, colors, tris, 0f, th, tr, tr * 0.65f, TrunkColor, 6);

        const float phiBot = -0.628f;
        const float phiTop =  1.571f;
        int seg      = Segments;
        int domeBase = verts.Count;

        verts.Add(new Vector3(0f, dcy + dr * DomeScaleY, 0f));
        colors.Add(DomeColorTop);

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

        int   botCenter = verts.Count;
        float botY      = dcy + Mathf.Sin(phiBot) * dr * DomeScaleY;
        verts.Add(new Vector3(0f, botY, 0f));
        colors.Add(DomeColorBot * 0.80f);

        int topIdx = domeBase;
        for (int s = 0; s < seg; s++)
        {
            tris.Add(topIdx);
            tris.Add(domeBase + 1 + (s + 1) % seg);
            tris.Add(domeBase + 1 + s);
        }
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

// ── Rocher arrondi organique ──────────────────────────────────────
// Icosphère subdivisée (42 vertex, 80 faces) déformée par vertex.
// Chaque Seed produit une silhouette unique.
//
// Valeurs ajustées pour des rochers plus hauts et moins plats :
//  • sy          : 0.90 – 1.80  (était 0.40–1.10) → plus de volume vertical
//  • squashBase  : 0.55 – 0.80  (était 0.20–0.45) → base moins écrasée
public class RockShape : TileShape
{
    public int   Seed     = 0;
    public float BaseSize = 0.07f;

    public float Footprint => BaseSize * 1.1f;

    static readonly Color s_Belly = new Color(0.28f, 0.27f, 0.25f);
    static readonly Color s_Base  = new Color(0.38f, 0.36f, 0.34f);
    static readonly Color s_Mid   = new Color(0.52f, 0.50f, 0.47f);
    static readonly Color s_Top   = new Color(0.66f, 0.63f, 0.59f);

    public override Mesh Build(float scale)
    {
        var rng = new System.Random(Seed);
        float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);

        float b = BaseSize * scale;

        // ── Profil global ──────────────────────────────────────────
        float sx         = R(0.55f, 1.45f);  // largeur X
        float sy         = R(0.90f, 1.80f);  // hauteur  (augmentée : était 0.40–1.10)
        float sz         = R(0.55f, 1.45f);  // largeur Z
        float bumpStr    = R(0.08f, 0.22f);  // bosses organiques
        float squashBase = R(0.55f, 0.80f);  // base moins aplatie (était 0.20–0.45)
        float heightBias = R(-0.05f, 0.10f); // léger décalage vertical du centre

        // ── Icosphère de base (1 subdivision → 42 vertex, 80 faces) ─
        float phi = (1f + Mathf.Sqrt(5f)) / 2f;

        var srcVerts = new List<Vector3>
        {
            Nrm(-1,phi,0), Nrm(1,phi,0),  Nrm(-1,-phi,0), Nrm(1,-phi,0),
            Nrm(0,-1,phi), Nrm(0,1,phi),  Nrm(0,-1,-phi), Nrm(0,1,-phi),
            Nrm(phi,0,-1), Nrm(phi,0,1),  Nrm(-phi,0,-1), Nrm(-phi,0,1)
        };

        var srcFaces = new List<(int,int,int)>
        {
            (0,11,5),(0,5,1),(0,1,7),(0,7,10),(0,10,11),
            (1,5,9),(5,11,4),(11,10,2),(10,7,6),(7,1,8),
            (3,9,4),(3,4,2),(3,2,6),(3,6,8),(3,8,9),
            (4,9,5),(2,4,11),(6,2,10),(8,6,7),(9,8,1)
        };

        var midCache = new Dictionary<long, int>();
        var newFaces = new List<(int,int,int)>();
        foreach (var (a, bc, c) in srcFaces)
        {
            int ab  = Mid(a, bc, srcVerts, midCache);
            int bcc = Mid(bc, c, srcVerts, midCache);
            int ca  = Mid(c, a, srcVerts, midCache);
            newFaces.Add((a,ab,ca)); newFaces.Add((bc,bcc,ab));
            newFaces.Add((c,ca,bcc)); newFaces.Add((ab,bcc,ca));
        }
        srcFaces = newFaces;

        int vCount   = srcVerts.Count;
        var deformed = new Vector3[vCount];

        // ── Déformation organique ──────────────────────────────────
        for (int i = 0; i < vCount; i++)
        {
            Vector3 n = srcVerts[i];

            // 1. Étirement anisotrope
            Vector3 v = new Vector3(n.x * sx, n.y * sy, n.z * sz);

            // 2. Bosse aléatoire par vertex
            float bump = R(1f - bumpStr, 1f + bumpStr);
            v *= bump;

            // 3. Aplatissement de la base (moins prononcé qu'avant)
            if (v.y < 0f) v.y *= squashBase;

            // 4. Décalage du centre de masse
            v.y += heightBias * b * 0.5f;

            // 5. Mise à l'échelle finale
            v *= b;
            deformed[i] = v;
        }

        // ── Pose sur le sol ────────────────────────────────────────
        float minY = float.MaxValue;
        foreach (var v in deformed) if (v.y < minY) minY = v.y;
        for (int i = 0; i < vCount; i++) deformed[i].y -= minY;

        // ── Gradient de couleur bas → haut ─────────────────────────
        float maxY = 0f;
        foreach (var v in deformed) if (v.y > maxY) maxY = v.y;

        var colors = new Color[vCount];
        for (int i = 0; i < vCount; i++)
        {
            float t     = maxY > 0f ? Mathf.Clamp01(deformed[i].y / maxY) : 0f;
            float noise = R(-0.04f, 0.04f);
            Color c;
            if      (t < 0.15f) c = Color.Lerp(s_Belly, s_Base, t / 0.15f);
            else if (t < 0.55f) c = Color.Lerp(s_Base,  s_Mid,  (t - 0.15f) / 0.40f);
            else                c = Color.Lerp(s_Mid,   s_Top,  (t - 0.55f) / 0.45f);
            colors[i] = new Color(
                Mathf.Clamp01(c.r + noise),
                Mathf.Clamp01(c.g + noise),
                Mathf.Clamp01(c.b + noise));
        }

        // ── Assemblage flat-shading ────────────────────────────────
        var mVerts  = new List<Vector3>();
        var mColors = new List<Color>();
        var mTris   = new List<int>();

        foreach (var (a, bc, c) in srcFaces)
        {
            int idx = mVerts.Count;
            mVerts.Add(deformed[a]);  mColors.Add(colors[a]);
            mVerts.Add(deformed[bc]); mColors.Add(colors[bc]);
            mVerts.Add(deformed[c]);  mColors.Add(colors[c]);

            Vector3 triCenter = (deformed[a] + deformed[bc] + deformed[c]) / 3f;
            Vector3 normal    = Vector3.Cross(deformed[bc] - deformed[a], deformed[c] - deformed[a]);
            if (Vector3.Dot(normal, triCenter) < 0f)
                { mTris.Add(idx); mTris.Add(idx+2); mTris.Add(idx+1); }
            else
                { mTris.Add(idx); mTris.Add(idx+1); mTris.Add(idx+2); }
        }

        var mesh = new Mesh { name = $"Rock_{Seed}" };
        mesh.SetVertices(mVerts);
        mesh.SetTriangles(mTris, 0);
        mesh.SetColors(mColors);
        mesh.RecalculateNormals();
        return mesh;
    }

    static Vector3 Nrm(float x, float y, float z) => new Vector3(x, y, z).normalized;

    static int Mid(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
    {
        long key = ((long)Mathf.Min(a,b) << 32) | (uint)Mathf.Max(a,b);
        if (cache.TryGetValue(key, out int idx)) return idx;
        verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
        cache[key] = verts.Count - 1;
        return verts.Count - 1;
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