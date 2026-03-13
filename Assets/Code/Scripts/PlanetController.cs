using UnityEngine;

/// <summary>
/// Orbit camera around the planet (planet stays still).
/// • Drag  → orbit (azimuth + elevation)
/// • Scroll → zoom
/// • Click  → tile selection (unchanged)
/// • Auto-orbit when idle
/// </summary>
public class PlanetController : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────
    [Header("Orbit")]
    [Tooltip("Degrees per pixel dragged.")]
    public float OrbitSensitivity = 0.35f;
    [Tooltip("Slow auto-orbit speed (deg/sec) when not dragging.")]
    public float AutoOrbitSpeed   = 6f;
    public bool  AutoRotate       = true;

    [Header("Zoom")]
    public float ZoomSpeed  = 5f;
    public float MinDistance = 8f;
    public float MaxDistance = 40f;

    [Header("Interaction")]
    public HexPlanetGenerator Generator;
    public bool ShowTileDebug = true;

    // ── Private state ──────────────────────────────────────────────
    private Camera _cam;

    // Spherical coords (degrees)
    private float _azimuth   =  30f;   // horizontal angle around Y
    private float _elevation =  25f;   // vertical angle from equator
    private float _distance  =  22f;   // camera distance from planet centre

    // Drag bookkeeping
    private Vector3 _mouseDownPos;
    private bool    _clickValid;
    private Vector3 _dragLastPos = Vector3.zero;

    // UI
    private int _lastHighlightedTile = -1;

    const float DragThreshold = 5f;   // pixels before drag is recognised

    // ──────────────────────────────────────────────────────────────
    void Start()
    {
        _cam = Camera.main;

        // Initialise distance from current camera position so the scene
        // view position is respected on first play.
        if (_cam != null)
            _distance = Mathf.Clamp(
                Vector3.Distance(_cam.transform.position, transform.position),
                MinDistance, MaxDistance);

        ApplyCameraTransform();

        // Safety checks
        var meshChild = transform.Find("HexPlanetMesh");
        if (meshChild == null)
            Debug.LogWarning("[PlanetController] HexPlanetMesh not found — generate the planet first.");
        else if (meshChild.GetComponent<MeshCollider>() == null)
            Debug.LogWarning("[PlanetController] No MeshCollider on HexPlanetMesh — regenerate the planet!");
        else
            Debug.Log("[PlanetController] Ready. MeshCollider detected.");
    }

    // ──────────────────────────────────────────────────────────────
    void Update()
    {
        HandleInput();
    }

    // ──────────────────────────────────────────────────────────────
    void HandleInput()
    {
        // ── Mouse button down ──────────────────────────────────────
        if (Input.GetMouseButtonDown(0))
        {
            _mouseDownPos = Input.mousePosition;
            _clickValid   = true;
            _dragLastPos  = Input.mousePosition;
        }

        // ── While held ────────────────────────────────────────────
        if (Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - _mouseDownPos;

            if (_clickValid && delta.magnitude > DragThreshold)
                _clickValid = false;    // too much movement → it's a drag

            if (!_clickValid)
            {
                Vector3 d = Input.mousePosition - _dragLastPos;

                // Horizontal drag → azimuth (left/right orbit)
                _azimuth += d.x * OrbitSensitivity;

                // Vertical drag → elevation (up/down orbit)
                _elevation += d.y * OrbitSensitivity;
                _elevation  = Mathf.Clamp(_elevation, -89f, 89f);

                _dragLastPos = Input.mousePosition;
                ApplyCameraTransform();
            }
        }

        // ── Release ───────────────────────────────────────────────
        if (Input.GetMouseButtonUp(0))
        {
            if (_clickValid)
                TrySelectTile();

            _clickValid  = false;
            _dragLastPos = Vector3.zero;
        }

        // ── Auto-orbit when no mouse button pressed ───────────────
        if (AutoRotate && !Input.GetMouseButton(0))
        {
            _azimuth += AutoOrbitSpeed * Time.deltaTime;
            ApplyCameraTransform();
        }

        // ── Zoom ──────────────────────────────────────────────────
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            _distance -= scroll * ZoomSpeed;
            _distance  = Mathf.Clamp(_distance, MinDistance, MaxDistance);
            ApplyCameraTransform();
        }
    }

    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Reposition the camera on its spherical orbit around the planet.
    /// The planet transform is NOT touched.
    /// </summary>
    void ApplyCameraTransform()
    {
        if (_cam == null) return;

        // Convert spherical → Cartesian (Y-up)
        float azRad  = _azimuth   * Mathf.Deg2Rad;
        float elRad  = _elevation * Mathf.Deg2Rad;

        Vector3 dir = new Vector3(
            Mathf.Cos(elRad) * Mathf.Sin(azRad),
            Mathf.Sin(elRad),
            Mathf.Cos(elRad) * Mathf.Cos(azRad)
        );

        Vector3 planetPos = transform.position;
        _cam.transform.position = planetPos + dir * _distance;
        _cam.transform.LookAt(planetPos, Vector3.up);
    }

    // ──────────────────────────────────────────────────────────────
    void TrySelectTile()
    {
        if (Generator == null)
        {
            Debug.LogWarning("[PlanetController] Generator not assigned in Inspector!");
            return;
        }

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        Debug.DrawRay(ray.origin, ray.direction * 50f, Color.yellow, 2f);

        if (!Physics.Raycast(ray, out var hit, 500f))
        {
            Debug.Log("[PlanetController] Raycast: nothing hit.");
            return;
        }

        bool hitPlanet = hit.transform == transform
                      || hit.transform.IsChildOf(transform);

        if (!hitPlanet)
        {
            Debug.Log($"[PlanetController] Hit '{hit.transform.name}' — not the planet.");
            return;
        }

        // World → local normalised direction
        Vector3 localHit = Generator.transform.InverseTransformPoint(hit.point).normalized;
        int tileId = Generator.GetClosestTileId(localHit);

        if (tileId < 0)
        {
            Debug.Log("[PlanetController] GetClosestTileId returned -1.");
            return;
        }

        _lastHighlightedTile = tileId;

        if (ShowTileDebug)
            Debug.Log(Generator.GetTileInfo(tileId));
    }

    // ──────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (!ShowTileDebug || _lastHighlightedTile < 0 || Generator == null) return;
        string info = Generator.GetTileInfo(_lastHighlightedTile);
        GUI.Box(new Rect(10, 10, 300, 95), "");
        GUI.Label(new Rect(18, 18, 284, 80), info);
    }
}