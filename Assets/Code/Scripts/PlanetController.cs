using UnityEngine;

/// <summary>
/// Orbit camera + hover outline.
/// • Hover  → outline blanc pulsant sur la tuile sous le curseur.
/// • A      → toggle blanc / vert sur l'outline (suit toujours la tuile survolée).
/// </summary>
public class PlanetController : MonoBehaviour
{
    [Header("Orbit")]
    public float OrbitSensitivity = 0.35f;
    public float AutoOrbitSpeed   = 6f;
    public bool  AutoRotate       = true;

    [Header("Zoom")]
    public float ZoomSpeed   = 5f;
    public float MinDistance = 8f;
    public float MaxDistance = 40f;

    [Header("Interaction")]
    public HexPlanetGenerator Generator;
    public bool ShowTileDebug = true;

    [Header("Hover Outline")]
    public TileHoverOutline HoverOutline;

    // ── Private ────────────────────────────────────────────────────
    private Camera _cam;
    private float  _azimuth   = 30f;
    private float  _elevation = 25f;
    private float  _distance  = 22f;

    private Vector3 _mouseDownPos;
    private bool    _clickValid;
    private Vector3 _dragLastPos;

    private int _lastHighlightedTile = -1;
    private int _hoveredTile         = -1;

    const float DragThreshold = 5f;

    // ──────────────────────────────────────────────────────────────
    void Start()
    {
        _cam = Camera.main;
        if (_cam != null)
            _distance = Mathf.Clamp(
                Vector3.Distance(_cam.transform.position, transform.position),
                MinDistance, MaxDistance);
        ApplyCameraTransform();
    }

    void Update()
    {
        HandleMouse();
        HandleHover();
        HandleKeyboard();
    }

    // ──────────────────────────────────────────────────────────────
    void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _mouseDownPos = Input.mousePosition;
            _clickValid   = true;
            _dragLastPos  = Input.mousePosition;
        }

        if (Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - _mouseDownPos;
            if (_clickValid && delta.magnitude > DragThreshold)
                _clickValid = false;

            if (!_clickValid)
            {
                Vector3 d  = Input.mousePosition - _dragLastPos;
                _azimuth   += d.x * OrbitSensitivity;
                _elevation += d.y * OrbitSensitivity;
                _elevation  = Mathf.Clamp(_elevation, -89f, 89f);
                _dragLastPos = Input.mousePosition;
                ApplyCameraTransform();
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (_clickValid) TrySelectTile();
            _clickValid  = false;
            _dragLastPos = Vector3.zero;
        }

        if (AutoRotate && !Input.GetMouseButton(0))
        {
            _azimuth += AutoOrbitSpeed * Time.deltaTime;
            ApplyCameraTransform();
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            _distance -= scroll * ZoomSpeed;
            _distance  = Mathf.Clamp(_distance, MinDistance, MaxDistance);
            ApplyCameraTransform();
        }
    }

    // ──────────────────────────────────────────────────────────────
    void HandleHover()
    {
        if (HoverOutline == null || Generator == null || _cam == null) return;

        if (Input.GetMouseButton(0))
        {
            HoverOutline.HideHover();
            _hoveredTile = -1;
            return;
        }

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 500f))
        {
            HoverOutline.HideHover();
            _hoveredTile = -1;
            return;
        }

        bool hitPlanet = hit.transform == transform
                      || hit.transform.IsChildOf(transform);
        if (!hitPlanet)
        {
            HoverOutline.HideHover();
            _hoveredTile = -1;
            return;
        }

        Vector3 localHit = Generator.transform.InverseTransformPoint(hit.point).normalized;
        int tileId = Generator.GetClosestTileId(localHit);
        if (tileId < 0)
        {
            HoverOutline.HideHover();
            _hoveredTile = -1;
            return;
        }

        _hoveredTile = tileId;
        HoverOutline.ShowTile(tileId);
    }

    // ──────────────────────────────────────────────────────────────
    void HandleKeyboard()
    {
        if (!Input.GetKeyDown(KeyCode.A)) return;

        if (HoverOutline == null)
        {
            Debug.LogWarning("[PlanetController] HoverOutline non assigné !");
            return;
        }

        // Toggle la couleur de l'outline (blanc ↔ vert), peu importe la tuile
        HoverOutline.ToggleColor();
    }

    // ──────────────────────────────────────────────────────────────
    void ApplyCameraTransform()
    {
        if (_cam == null) return;
        float azRad = _azimuth   * Mathf.Deg2Rad;
        float elRad = _elevation * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(
            Mathf.Cos(elRad) * Mathf.Sin(azRad),
            Mathf.Sin(elRad),
            Mathf.Cos(elRad) * Mathf.Cos(azRad));
        _cam.transform.position = transform.position + dir * _distance;
        _cam.transform.LookAt(transform.position, Vector3.up);
    }

    void TrySelectTile()
    {
        if (Generator == null) return;
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 500f)) return;
        bool hitPlanet = hit.transform == transform || hit.transform.IsChildOf(transform);
        if (!hitPlanet) return;
        Vector3 localHit = Generator.transform.InverseTransformPoint(hit.point).normalized;
        int tileId = Generator.GetClosestTileId(localHit);
        if (tileId < 0) return;
        _lastHighlightedTile = tileId;
        if (ShowTileDebug) Debug.Log(Generator.GetTileInfo(tileId));
    }

    void OnGUI()
    {
        if (!ShowTileDebug || _lastHighlightedTile < 0 || Generator == null) return;
        string info = Generator.GetTileInfo(_lastHighlightedTile);
        GUI.Box(new Rect(10, 10, 300, 95), "");
        GUI.Label(new Rect(18, 18, 284, 80), info);
    }
}