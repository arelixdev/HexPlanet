using UnityEngine;

/// <summary>
/// A attacher sur le meme GameObject que HexPlanetGenerator (ou TileObjectSpawner).
/// Pousse chaque frame la matrice WorldToLocal de la planete dans le material ForestMaterial,
/// afin que le shader FirTreeSnow calcule la latitude en espace LOCAL de la planete
/// (et non en espace monde), quelle que soit la rotation de la planete.
/// </summary>
[ExecuteAlways]
public class FirTreeSnowUpdater : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Le meme transform que HexPlanetGenerator / la racine de la planete.")]
    public Transform PlanetTransform;

    [Tooltip("Le material utilisant le shader Custom/FirTreeSnow.")]
    public Material  ForestMaterial;

    // IDs des proprietes shader (caches pour la perf)
    static readonly int ID_Row0 = Shader.PropertyToID("_PlanetWorldToLocalRow0");
    static readonly int ID_Row1 = Shader.PropertyToID("_PlanetWorldToLocalRow1");
    static readonly int ID_Row2 = Shader.PropertyToID("_PlanetWorldToLocalRow2");

    void Update()
    {
        PushMatrix();
    }

    // Appel manuel possible depuis l'editeur
    [ContextMenu("Push Matrix Now")]
    public void PushMatrix()
    {
        if (PlanetTransform == null || ForestMaterial == null) return;

        // worldToLocalMatrix = inverse(localToWorldMatrix)
        Matrix4x4 m = PlanetTransform.worldToLocalMatrix;

        // On envoie les 3 premieres lignes (rotation + translation)
        // Format Vector4 : (m.m_0, m.m_1, m.m_2, m.m_3) pour chaque ligne
        ForestMaterial.SetVector(ID_Row0, new Vector4(m.m00, m.m01, m.m02, m.m03));
        ForestMaterial.SetVector(ID_Row1, new Vector4(m.m10, m.m11, m.m12, m.m13));
        ForestMaterial.SetVector(ID_Row2, new Vector4(m.m20, m.m21, m.m22, m.m23));
    }

#if UNITY_EDITOR
    // Mise a jour aussi en mode edition quand la planete est tournee dans la scene
    void OnValidate() => PushMatrix();
#endif
}
