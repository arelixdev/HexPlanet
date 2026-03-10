using UnityEngine;

public class PlanetController : MonoBehaviour
{
    [Header("Rotation")]
    public float RotateSpeed = 20f;
    public bool AutoRotate = true;

    [Header("Interaction")]
    public HexPlanetGenerator Generator;
    public bool ShowTileDebug = true;

    private Camera _cam;
    private Vector3 _mouseDownPos;
    private bool _clickValid;          // true si le mousedown n'a pas bougé assez pour être un drag
    private int _lastHighlightedTile = -1;

    const float DragThreshold = 5f;   // pixels

    void Start()
    {
        _cam = Camera.main;

        var meshChild = transform.Find("HexPlanetMesh");
        if (meshChild == null)
            Debug.LogWarning("[PlanetController] HexPlanetMesh introuvable — génère d'abord la planète.");
        else if (meshChild.GetComponent<MeshCollider>() == null)
            Debug.LogWarning("[PlanetController] Pas de MeshCollider sur HexPlanetMesh — régénère la planète !");
        else
            Debug.Log("[PlanetController] Prêt. MeshCollider détecté.");
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        // ── Début du clic ──────────────────────────────────────────
        if (Input.GetMouseButtonDown(0))
        {
            _mouseDownPos = Input.mousePosition;
            _clickValid   = true;
        }

        // ── Pendant le maintien ────────────────────────────────────
        if (Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - _mouseDownPos;

            if (_clickValid && delta.magnitude > DragThreshold)
                _clickValid = false;   // trop de mouvement → c'est un drag, pas un clic

            if (!_clickValid)          // on est en mode drag → on tourne la planète
            {
                Vector3 frameDelta = Input.mousePosition - _mouseDownPos;
                // utilise un lastPos dédié au drag
                if (_dragLastPos != Vector3.zero)
                {
                    Vector3 d = Input.mousePosition - _dragLastPos;
                    transform.Rotate(Vector3.up, -d.x * RotateSpeed * Time.deltaTime, Space.World);
                    transform.Rotate(_cam.transform.right, d.y * RotateSpeed * Time.deltaTime, Space.World);
                }
                _dragLastPos = Input.mousePosition;
            }
        }

        // ── Relâchement ────────────────────────────────────────────
        if (Input.GetMouseButtonUp(0))
        {
            _dragLastPos = Vector3.zero;

            if (_clickValid)           // le bouton a été relâché sans drag → c'est un vrai clic
                TrySelectTile();

            _clickValid = false;
        }

        // ── Auto-rotation quand pas de souris enfoncée ─────────────
        if (AutoRotate && !Input.GetMouseButton(0))
            transform.Rotate(Vector3.up, 2f * Time.deltaTime, Space.World);

        // ── Zoom ───────────────────────────────────────────────────
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            _cam.transform.position += _cam.transform.forward * scroll * 5f;
    }

    private Vector3 _dragLastPos = Vector3.zero;

    void TrySelectTile()
    {
        if (Generator == null)
        {
            Debug.LogWarning("[PlanetController] Generator non assigné dans l'Inspector !");
            return;
        }

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        Debug.DrawRay(ray.origin, ray.direction * 50f, Color.yellow, 2f);

        if (!Physics.Raycast(ray, out var hit, 500f))
        {
            Debug.Log("[PlanetController] Raycast : rien touché.");
            return;
        }

        // Accepte le hit sur l'enfant OU sur le parent
        bool hitPlanet = hit.transform == transform
                      || hit.transform.IsChildOf(transform);

        if (!hitPlanet)
        {
            Debug.Log($"[PlanetController] Hit sur '{hit.transform.name}' — pas la planète.");
            return;
        }

        // Convertit le point world → espace local normalisé
        Vector3 localHit = Generator.transform.InverseTransformPoint(hit.point).normalized;
        int tileId = Generator.GetClosestTileId(localHit);

        if (tileId < 0)
        {
            Debug.Log("[PlanetController] GetClosestTileId a retourné -1.");
            return;
        }

        _lastHighlightedTile = tileId;

        if (ShowTileDebug)
            Debug.Log(Generator.GetTileInfo(tileId));
    }

    void OnGUI()
    {
        if (!ShowTileDebug || _lastHighlightedTile < 0 || Generator == null) return;
        string info = Generator.GetTileInfo(_lastHighlightedTile);
        GUI.Box(new Rect(10, 10, 300, 95), "");
        GUI.Label(new Rect(18, 18, 284, 80), info);
    }
}
