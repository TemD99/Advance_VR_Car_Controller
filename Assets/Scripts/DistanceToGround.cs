using ML.SDK;
using UnityEngine;
using TMPro;
using System.Text;

public class DistanceToGround : MonoBehaviour
{
    public string groundLayerName = "Water";
    public float maxDistance = 100f;
    [Tooltip("Seconds between debug UI updates. Set lower for more responsive readout.")]
    [Range(0.05f, 1f)] public float uiUpdateInterval = 0.25f;
    public bool showDebugText = true;

    // Ground ray results
    public float distanceToGround;     // vertical hit distance
    public float distance3D;           // 3D to hit point (same ray)

    // Offsets from PARENT ORIGIN (0,0,0) in local (parent) space
    public float localXFromParentZero;         // signed X
    public float localZFromParentZero;         // signed Z
    public float absLocalXFromParentZero;      // |X|
    public float absLocalZFromParentZero;      // |Z|
    public float planarFromParentZeroXZ;       // sqrt(x^2 + z^2)

    bool playerInstantiated = false;
    public TextMeshProUGUI textMeshProUGUI;

    int _groundLayerMask = ~0;
    float _uiTimer;
    static readonly StringBuilder _sb = new StringBuilder(256);

    public void PlayerInstantiated() { playerInstantiated = true; }

    void Awake()
    {
        int groundLayer = LayerMask.NameToLayer(groundLayerName);
        _groundLayerMask = (groundLayer >= 0) ? (1 << groundLayer) : ~0;
    }

    void Update()
    {
        if (!playerInstantiated) return;

        Vector3 local = (transform.parent != null)
            ? transform.parent.InverseTransformPoint(transform.position)
            : transform.position;

        localXFromParentZero = local.x;
        localZFromParentZero = local.z;
        absLocalXFromParentZero = Mathf.Abs(localXFromParentZero);
        absLocalZFromParentZero = Mathf.Abs(localZFromParentZero);
        planarFromParentZeroXZ = Mathf.Sqrt(localXFromParentZero * localXFromParentZero +
                                            localZFromParentZero * localZFromParentZero);

        bool hitGround = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, maxDistance, _groundLayerMask);

        if (hitGround)
        {
            distanceToGround = hit.distance;
            distance3D = Vector3.Distance(transform.position, hit.point);
        }
        else
        {
            distanceToGround = Mathf.Infinity;
            distance3D = Mathf.Infinity;
        }

        if (!showDebugText || textMeshProUGUI == null)
            return;

        _uiTimer += Time.deltaTime;
        if (_uiTimer < Mathf.Max(0.01f, uiUpdateInterval))
            return;

        _uiTimer = 0f;
        _sb.Length = 0;

        if (hitGround)
        {
            _sb.Append("Head→Ground dY: ").Append(distanceToGround.ToString("F3")).Append(" m (3D: ")
               .Append(distance3D.ToString("F3")).Append(")\nFrom Parent(0): X=")
               .Append(localXFromParentZero.ToString("F3")).Append(" (")
               .Append(absLocalXFromParentZero.ToString("F3")).Append(")  Z=")
               .Append(localZFromParentZero.ToString("F3")).Append(" (")
               .Append(absLocalZFromParentZero.ToString("F3")).Append(")\nPlanar XZ from Parent(0): ")
               .Append(planarFromParentZeroXZ.ToString("F3")).Append(" m");
        }
        else
        {
            _sb.Append("No ground.\nFrom Parent(0): X=").Append(localXFromParentZero.ToString("F3"))
               .Append("  Z=").Append(localZFromParentZero.ToString("F3")).Append("\nPlanar XZ: ")
               .Append(planarFromParentZeroXZ.ToString("F3"));
        }

        textMeshProUGUI.text = _sb.ToString();
    }
}
