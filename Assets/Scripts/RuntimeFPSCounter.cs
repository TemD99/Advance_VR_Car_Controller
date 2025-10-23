using UnityEngine;
using TMPro;

public class VRFpsTMP : MonoBehaviour
{
    [Header("TextMeshPro Targets (separate labels)")]
    public TMP_Text fpsLabel;              // e.g. "72 FPS (13.9 ms)"
    public TMP_Text speedLabel;            // e.g. "Speed: 54 km/h"
    public TMP_Text gearLabel;             // e.g. "Gear: 3" or "Gear: N"

    [Header("Vehicle Sources")]
     CarControllerVR car;
    public GameObject carObj; // preferred: read gearIndex from here
    public Rigidbody carBody;              // for speed; if null we'll try to find one

    [Header("Sampling")]
    [Tooltip("How often labels refresh.")]
    [Range(0.05f, 3f)] public float sampleDuration = 0.25f;
    public bool showFrameTimeMs = true;

    public enum SpeedUnits { KPH, MPH, MPS }
    [Header("Speed Display")]
    public SpeedUnits speedUnits = SpeedUnits.KPH;
    [Tooltip("Round the shown speed to this many decimals.")]
    [Range(0, 2)] public int speedDecimals = 0;

    // --- internals ---
    float _timer;
    int _frames;
    float _fps;
    float _ms;

    void Start()
    {
        // If user only assigned the car, try to grab its rigidbody
        if (!carBody && car) carBody = car.GetComponent<Rigidbody>();
        if (!carBody) carBody = GetComponentInParent<Rigidbody>(); // last resort
        car = (CarControllerVR)carObj.GetComponent(typeof(CarControllerVR));
    }

    void Update()
    {
        // FPS sampling
        _timer += Time.unscaledDeltaTime;
        _frames++;

        if (_timer >= sampleDuration)
        {
            _fps = _frames / Mathf.Max(1e-6f, _timer);
            _ms = 1000f / Mathf.Max(1e-6f, _fps);
            _frames = 0;
            _timer = 0f;

            // --- FPS label ---
            if (fpsLabel)
            {
                // color from red→yellow→green roughly between 30 and 120 fps
                float t = Mathf.InverseLerp(30f, 120f, _fps);
                fpsLabel.color = Color.Lerp(Color.red, Color.green, t);

                if (showFrameTimeMs)
                    fpsLabel.text = $"{_fps:0.#} FPS  ({_ms:0.#} ms)";
                else
                    fpsLabel.text = $"{_fps:0.#} FPS";
            }

            // --- Speed label ---
            if (speedLabel)
            {
                float v = (carBody ? carBody.velocity.magnitude : 0f); // m/s
                float shown = speedUnits switch
                {
                    SpeedUnits.KPH => v * 3.6f,
                    SpeedUnits.MPH => v * 2.23693629f,
                    _ => v
                };
                string unit = speedUnits == SpeedUnits.KPH ? "km/h" :
                              speedUnits == SpeedUnits.MPH ? "mph" : "m/s";
                string fmt = speedDecimals == 0 ? "0" : (speedDecimals == 1 ? "0.0" : "0.00");
                speedLabel.text = $"Speed: {shown.ToString(fmt)} {unit}";
            }

            // --- Gear label ---
            if (gearLabel)
            {
                // Prefer CarControllerVR.gearIndex; fallback to "?" if not available
                string gearStr = "?";
                if (car)
                {
                    if (car.gearIndex == 0) gearStr = "N";
                    else if (car.gearIndex < 0) gearStr = "R";
                    else gearStr = car.gearIndex.ToString();
                }
                gearLabel.text = $"Gear: {gearStr}";
            }
        }
    }
}
