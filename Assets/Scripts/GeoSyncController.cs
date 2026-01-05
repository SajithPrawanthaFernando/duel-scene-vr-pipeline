using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GeoSyncController : MonoBehaviour
{
    [Header("Enable/Disable")]
    [Tooltip("Master switch. If false, GeoAnchor stays at identity (no GPS/compass alignment).")]
    public bool geoSyncEnabled = false;

    [Header("Target Location (Temple of Tooth reference)")]
    public double targetLatitude = 7.293052;
    public double targetLongitude = 80.640896;

    [Header("Fence Check (Inside/Outside)")]
    [Tooltip("If true, GeoSync only applies when the user is inside the polygon fence OR demoMode is enabled.")]
    public bool requireInsideFence = true;

    [Tooltip("Polygon points in (lat, lon).")]
    public List<Vector2> fenceLatLon = new List<Vector2>()
    {
        new Vector2(7.293030f, 80.641597f),
        new Vector2(7.294403f, 80.641435f),
        new Vector2(7.294393f, 80.639677f),
        new Vector2(7.293240f, 80.639792f),
    };

    [Header("UI Panels")]
    public GameObject awayPanel;    
    public GameObject insidePanel;   

    [Header("Scale")]
    [Tooltip("Unity units per real-world meter. 1 unit = 1 meter.")]
    public float unityUnitsPerMeter = 1f;

    [Header("Yaw / Compass")]
    [Tooltip("Manual yaw offset (degrees) to fine-tune alignment.")]
    public float yawOffsetDegrees = 0f;

    [Header("Smoothing")]
    public float positionLerp = 5f;
    public float rotationLerp = 5f;

    [Header("Demo Mode")]
    [Tooltip("If true, bypass GPS + fence and use simulated offsets + heading.")]
    public bool demoMode = true;

    [Tooltip("Fake offset in meters (x=east, y=north) from targetLat/targetLon.")]
    public Vector2 demoLatLonOffsetMeters = Vector2.zero;

    [Tooltip("Fake heading in degrees (0 = facing north).")]
    public float demoHeadingDegrees = 0f;

    [Header("GPS Settings (Mobile/Headset builds)")]
    public float desiredAccuracyMeters = 5f;
    public float updateDistanceMeters = 1f;
    public float gpsInitTimeoutSeconds = 10f;

    // Runtime status
    public bool IsRunning { get; private set; }
    public bool IsInsideFence { get; private set; }
    public bool IsGeoApplying { get; private set; } 

    public double CurrentLatitude { get; private set; }
    public double CurrentLongitude { get; private set; }
    public float CurrentHeading { get; private set; }

    Coroutine _serviceRoutine;
    Vector3 _targetPos;
    Quaternion _targetRot;

    void Start()
    {
        // Start in a known state
        ApplyEnabledState(geoSyncEnabled, instant: true);
    }

    // Public toggles

    public void ToggleGeoSync()
    {
        ApplyEnabledState(!geoSyncEnabled, instant: false);
    }

    public void ToggleDemoMode()
    {
        SetDemoMode(!demoMode);
    }

    public void SetDemoMode(bool enabled)
    {
        demoMode = enabled;

        // If GeoSync is enabled, switch services accordingly
        if (geoSyncEnabled)
        {
            if (demoMode)
                StopGeoServices();
            else
                StartGeoServices();
        }

        // Refresh UI immediately
        RefreshUI();
    }

    public void ApplyEnabledState(bool enabled, bool instant)
    {
        geoSyncEnabled = enabled;

        if (!geoSyncEnabled)
        {
            StopGeoServices();
            IsGeoApplying = false;

            // When disabled, hide both panels 
            SetUIPanels(inside: false, away: false);

            if (instant)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            return;
        }

        // Enabled
        if (demoMode)
            StopGeoServices();  // demo doesn't need GPS
        else
            StartGeoServices();

        if (instant)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        RefreshUI();
    }

    // Main loop 

    void Update()
    {
        if (!geoSyncEnabled)
            return;

        // Acquire lat/lon + heading
        if (!TryGetLatLonHeading(out double lat, out double lon, out float heading))
        {
            // GPS not ready => show neutral, don't move GeoAnchor
            IsGeoApplying = false;

            // Show neutral: hide away while GPS initializes
            SetUIPanels(inside: false, away: false);
            return;
        }

        CurrentLatitude = lat;
        CurrentLongitude = lon;
        CurrentHeading = heading;

        // Fence check (only if required and not demo)
        IsInsideFence = true;
        if (!demoMode && requireInsideFence && fenceLatLon != null && fenceLatLon.Count >= 3)
        {
            IsInsideFence = PointInPolygonLatLon(new Vector2((float)lat, (float)lon), fenceLatLon);
        }

        // Decide UI + whether to apply geosync
        bool canApply = demoMode || !requireInsideFence || IsInsideFence;

        if (!canApply)
        {
            // Outside => show away message, don't move GeoAnchor
            IsGeoApplying = false;
            SetUIPanels(inside: false, away: true);
            return;
        }

        // Inside OR demo => apply geosync
        IsGeoApplying = true;
        SetUIPanels(inside: true, away: false);

        // Compute offset in meters from target -> current
        Vector2 offsetMeters = LatLonToMetersOffset(lat, lon, targetLatitude, targetLongitude);

        // Move GeoAnchor opposite the user offset
        _targetPos = new Vector3(-offsetMeters.x, 0f, -offsetMeters.y) * unityUnitsPerMeter;

        // Rotate GeoAnchor so north aligns to world forward
        float yaw = -heading + yawOffsetDegrees;
        _targetRot = Quaternion.Euler(0f, yaw, 0f);

        // Smooth apply
        transform.localPosition = Vector3.Lerp(transform.localPosition, _targetPos, Time.deltaTime * positionLerp);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, _targetRot, Time.deltaTime * rotationLerp);
    }

    // Helpers

    bool TryGetLatLonHeading(out double lat, out double lon, out float heading)
    {
        lat = targetLatitude;
        lon = targetLongitude;
        heading = 0f;

        if (demoMode)
        {
            // Fake GPS around the target using meter offsets
            double metersNorth = demoLatLonOffsetMeters.y;
            double metersEast = demoLatLonOffsetMeters.x;

            double dLat = metersNorth / 111320.0;
            double dLon = metersEast / (111320.0 * Mathf.Cos((float)(targetLatitude * Mathf.Deg2Rad)));

            lat = targetLatitude + dLat;
            lon = targetLongitude + dLon;
            heading = demoHeadingDegrees;
            return true;
        }

        // Real GPS
        if (!IsRunning || Input.location.status != LocationServiceStatus.Running)
            return false;

        var loc = Input.location.lastData;
        lat = loc.latitude;
        lon = loc.longitude;

        float th = Input.compass.trueHeading;
        heading = (th > 0.01f) ? th : Input.compass.magneticHeading;

        return true;
    }

    void RefreshUI()
    {
        if (!geoSyncEnabled)
        {
            SetUIPanels(false, false);
            return;
        }

        // Demo mode always shows "inside" state
        if (demoMode)
        {
            SetUIPanels(true, false);
            return;
        }

        // If GPS not running yet, show neutral
        if (!IsRunning)
        {
            SetUIPanels(false, false);
            return;
        }
      
    }

    void SetUIPanels(bool inside, bool away)
    {
        if (insidePanel != null) insidePanel.SetActive(inside);
        if (awayPanel != null) awayPanel.SetActive(away);
    }

    void StartGeoServices()
    {
        if (_serviceRoutine != null)
            StopCoroutine(_serviceRoutine);

        _serviceRoutine = StartCoroutine(StartServicesRoutine());
    }

    void StopGeoServices()
    {
        if (_serviceRoutine != null)
        {
            StopCoroutine(_serviceRoutine);
            _serviceRoutine = null;
        }

        if (Input.compass.enabled)
            Input.compass.enabled = false;

        if (Input.location.status == LocationServiceStatus.Running ||
            Input.location.status == LocationServiceStatus.Initializing)
        {
            if (Input.location.isEnabledByUser)
                Input.location.Stop();
        }

        IsRunning = false;
    }

    IEnumerator StartServicesRoutine()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("GeoSync: Location services not enabled by user.");
            IsRunning = false;
            yield break;
        }

        Input.compass.enabled = true;
        Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);

        float timeout = gpsInitTimeoutSeconds;
        while (Input.location.status == LocationServiceStatus.Initializing && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            Debug.LogWarning("GeoSync: Location failed to start: " + Input.location.status);
            IsRunning = false;
            yield break;
        }

        IsRunning = true;
    }

    // Returns (x=east meters, y=north meters) from target -> current
    static Vector2 LatLonToMetersOffset(double curLat, double curLon, double tgtLat, double tgtLon)
    {
        double latRad = tgtLat * Mathf.Deg2Rad;
        double metersPerDegLat = 111320.0;
        double metersPerDegLon = 111320.0 * Mathf.Cos((float)latRad);

        double dLat = curLat - tgtLat;
        double dLon = curLon - tgtLon;

        double northMeters = dLat * metersPerDegLat;
        double eastMeters = dLon * metersPerDegLon;

        return new Vector2((float)eastMeters, (float)northMeters);
    }

    // input points are (lat,lon) but geometry uses x=lon, y=lat.
    static bool PointInPolygonLatLon(Vector2 pointLatLon, List<Vector2> polyLatLon)
    {
        float x = pointLatLon.y; // lon
        float y = pointLatLon.x; // lat
        bool inside = false;

        for (int i = 0, j = polyLatLon.Count - 1; i < polyLatLon.Count; j = i++)
        {
            float xi = polyLatLon[i].y; float yi = polyLatLon[i].x;
            float xj = polyLatLon[j].y; float yj = polyLatLon[j].x;

            bool intersect =
                ((yi > y) != (yj > y)) &&
                (x < (xj - xi) * (y - yi) / (yj - yi + 1e-7f) + xi);

            if (intersect) inside = !inside;
        }

        return inside;
    }

    public void SetFencePolygon(List<Vector2> newPolyLatLon)
    {
        fenceLatLon = newPolyLatLon ?? new List<Vector2>();
    }
}
