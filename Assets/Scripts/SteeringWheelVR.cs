using ML.SDK;
using UnityEngine;


public class SteeringWheelVR : MonoBehaviour
{
    private PlayerManager playerManager;
    public GameObject playerManagerObj;
    public enum RotationAxis { X, Y, Z }

    [Header("Hands")]
    public Transform leftHand;
    public Transform rightHand;

    [Header("Grab Points")]
    public Transform[] grabPoints;

    [Header("Proximity")]
    public float grabRadius = 0.25f;
    public float releaseRadius = 0.4f;

    [Header("Wheel Rotation")]
    public RotationAxis rotationAxis = RotationAxis.Z;
    public float minAngle = -90f;
    public float maxAngle = 90f;
    public float startAngle = 0f;
    public bool invertDirection = false;
    [Range(0f, 50f)] public float wheelFollowSpeed = 20f;

    [Header("Spring Return Settings")]
    public float springToCenter = 20f;  // pull strength
    public float springDamping = 4f;    // prevents overshoot

    [Header("Reparenting (optional)")]
    public Transform reparentRootLeft;
    public Transform reparentRootRight;
    public bool reparentOnGrab = true;
    public bool restoreWorldPose = true;

    [Header("Hand Pose Offsets")]
    public Vector3 leftHandEulerOffset = Vector3.zero;
    public Vector3 rightHandEulerOffset = Vector3.zero;
    public Vector3 grabPointHandPoseLocalOffset = Vector3.zero;

    [HideInInspector] public bool leftGrabPressed = false;
    [HideInInspector] public bool rightGrabPressed = false;
    public MLStation station;
    [Header("Input")]
    [Range(0.25f, 4f)] public float inputSensitivity = 1f; // 1 = default, >1 = more turn per hand movement

    private class HandState
    {
        public bool holding;
        public bool prevPressed;
        public Transform hand;
        public Transform input;
        public Transform reparentTarget;
        public Transform originalParent;
        public int pointIndex = -1;
        public float lastRaw;
        public float continuous;
        public bool hasContinuous;
        public float grabOffset;
    }

    private readonly HandState L = new HandState();
    private readonly HandState R = new HandState();
    private int[] pointOwner;
    private float wheelAngle;
    private float targetAngle;
    private float wheelVel;
    private Quaternion baseLocalRotation;

    void Awake()
    {
        ResolvePlayerManager();
        baseLocalRotation = transform.localRotation;
        wheelAngle = Mathf.Clamp(startAngle, minAngle, maxAngle);
        targetAngle = wheelAngle;

        L.hand = leftHand;
        R.hand = rightHand;

        InitPointOwner();
        ApplyAngleImmediate();
    }

    void OnValidate() { InitPointOwner(); }

    private void InitPointOwner()
    {
        int n = (grabPoints != null) ? grabPoints.Length : 0;
        pointOwner = new int[n];
        for (int i = 0; i < n; i++) pointOwner[i] = -1;
    }

    void ResolvePlayerManager()
    {
        if (playerManager != null) return;
        playerManager = (PlayerManager)playerManagerObj.GetComponent(typeof(PlayerManager));

    }

    void Update()
    {
        if (station != null && station.IsOccupied)
        {
            var player = station.GetPlayer();
            if (player != null && player.UserInput != null)
            {
                leftGrabPressed = player.UserInput.Grip1 > 0.5f;
                rightGrabPressed = player.UserInput.Grip2 > 0.5f;
            }
        }

        L.input = ResolveInputSource(true);
        R.input = ResolveInputSource(false);

        bool leftDown = leftGrabPressed && !L.prevPressed;
        bool rightDown = rightGrabPressed && !R.prevPressed;

        if (leftDown) TryBeginGrab(L, true, reparentRootLeft);
        if (rightDown) TryBeginGrab(R, false, reparentRootRight);

        if (L.holding && (!leftGrabPressed || TooFarFromPoint(L))) EndGrab(L, true);
        if (R.holding && (!rightGrabPressed || TooFarFromPoint(R))) EndGrab(R, false);

        int count = 0;
        float sum = 0f;

        if (L.holding && L.input != null) { sum += HandTargetByInput(L); count++; }
        if (R.holding && R.input != null) { sum += HandTargetByInput(R); count++; }

        if (count > 0)
        {
            // Player controlling wheel
            targetAngle = Mathf.Clamp(sum / count, minAngle, maxAngle);
            wheelAngle = Mathf.LerpAngle(wheelAngle, targetAngle, Time.deltaTime * wheelFollowSpeed);
        }
        else
        {
            // Spring return when released
            float accel = (-wheelAngle * springToCenter) - wheelVel * springDamping;
            wheelVel += accel * Time.deltaTime;
            wheelAngle += wheelVel * Time.deltaTime;
            wheelAngle = Mathf.Clamp(wheelAngle, minAngle, maxAngle);

            // Reset velocity near zero
            if (Mathf.Abs(wheelAngle) < 0.1f && Mathf.Abs(wheelVel) < 0.01f)
            {
                wheelAngle = 0f;
                wheelVel = 0f;
            }
        }

        ApplyAngleImmediate();

        PoseHand(L, true);
        PoseHand(R, false);

        L.prevPressed = leftGrabPressed;
        R.prevPressed = rightGrabPressed;
    }

    private Transform ResolveInputSource(bool isLeft)
    {
        Transform t;

        ResolvePlayerManager();
        if (playerManager != null)
        {
            t = isLeft ? playerManager.leftHandIK : playerManager.rightHandIK;
            if (t != null) return t;
        }

        return isLeft ? leftHand : rightHand;
    }

    private void TryBeginGrab(HandState H, bool isLeft, Transform reparentRoot)
    {
        if (H.hand == null) return;
        H.input = ResolveInputSource(isLeft);
        if (H.input == null) return;

        int idx = NearestFreeGrabPoint(H.input.position);
        if (idx < 0) return;

        ResolvePlayerManager();
        if (playerManager != null)
            playerManager.BeginHandOverride(isLeft);

        pointOwner[idx] = isLeft ? 0 : 1;
        H.pointIndex = idx;

        H.reparentTarget = (reparentRoot != null) ? reparentRoot : H.hand;
        if (reparentOnGrab && H.reparentTarget != null)
        {
            H.originalParent = H.reparentTarget.parent;
            H.reparentTarget.SetParent(transform, true);
        }

        float raw = HandRawAngle(H.input.position);
        H.lastRaw = raw;
        H.continuous = raw;
        H.hasContinuous = true;
        H.grabOffset = wheelAngle - H.continuous;
        H.holding = true;
        wheelVel = 0f; // stop motion
    }

    private void EndGrab(HandState H, bool isLeft)
    {
        ResolvePlayerManager();
        if (playerManager != null)
            playerManager.EndHandOverride(isLeft);

        if (H.holding && reparentOnGrab && H.reparentTarget != null)
            H.reparentTarget.SetParent(H.originalParent, restoreWorldPose);

        if (grabPoints != null && H.pointIndex >= 0 && H.pointIndex < pointOwner.Length)
            pointOwner[H.pointIndex] = -1;

        H.pointIndex = -1;
        H.holding = false;
        H.hasContinuous = false;
    }

    private bool TooFarFromPoint(HandState H)
    {
        if (H.input == null || grabPoints == null || H.pointIndex < 0 || H.pointIndex >= grabPoints.Length) return true;
        var p = grabPoints[H.pointIndex];
        if (!p) return true;
        return (p.position - H.input.position).sqrMagnitude > (releaseRadius * releaseRadius);
    }

    private float HandTargetByInput(HandState H)
    {
        float raw = HandRawAngle(H.input.position);
        if (!H.hasContinuous)
        {
            H.lastRaw = raw;
            H.continuous = raw;
            H.hasContinuous = true;
        }
        else
        {
            float step = Mathf.DeltaAngle(H.lastRaw, raw);
            H.lastRaw = raw;
            // apply sensitivity to *delta*, preserves continuity & grabOffset
            H.continuous += step * inputSensitivity;
        }
        return H.continuous + H.grabOffset;
    }

    private void PoseHand(HandState H, bool isLeft)
    {
        if (!H.holding || H.hand == null || grabPoints == null || H.pointIndex < 0) return;

        Vector3 worldPos; Quaternion worldRot;
        ComputeRimPoseFromPoint(grabPoints[H.pointIndex].position, out worldPos, out worldRot);

        Vector3 eulerOffset = isLeft ? leftHandEulerOffset : rightHandEulerOffset;
        worldRot *= Quaternion.Euler(eulerOffset);

        if (grabPointHandPoseLocalOffset != Vector3.zero)
            worldPos += worldRot * grabPointHandPoseLocalOffset;

        H.hand.position = worldPos;
        H.hand.rotation = worldRot;
    }

    private void ComputeRimPoseFromPoint(Vector3 pointWorld, out Vector3 worldPoint, out Quaternion worldRot)
    {
        Vector3 axisWorld = transform.TransformDirection(AxisVector()).normalized;
        Vector3 radial = pointWorld - transform.position;
        radial -= Vector3.Dot(radial, axisWorld) * axisWorld;
        if (radial.sqrMagnitude < 1e-8f) radial = transform.right;
        radial.Normalize();
        Vector3 tangent = Vector3.Cross(axisWorld, radial).normalized;
        worldPoint = pointWorld;
        worldRot = Quaternion.LookRotation(tangent, axisWorld);
    }

    private float HandRawAngle(Vector3 handWorldPos)
    {
        Vector3 Lp = transform.InverseTransformPoint(handWorldPos);
        float ang = rotationAxis switch
        {
            RotationAxis.X => Mathf.Atan2(Lp.y, Lp.z) * Mathf.Rad2Deg,
            RotationAxis.Y => Mathf.Atan2(Lp.z, Lp.x) * Mathf.Rad2Deg,
            _ => Mathf.Atan2(Lp.y, Lp.x) * Mathf.Rad2Deg
        };
        ang = Mathf.DeltaAngle(0f, ang);
        if (invertDirection) ang = -ang;
        return ang;
    }

    private void ApplyAngleImmediate()
    {
        transform.localRotation = baseLocalRotation * Quaternion.AngleAxis(wheelAngle, AxisVector());
    }

    private Vector3 AxisVector()
    {
        return rotationAxis switch
        {
            RotationAxis.X => Vector3.right,
            RotationAxis.Y => Vector3.up,
            _ => Vector3.forward
        };
    }

    private int NearestFreeGrabPoint(Vector3 fromWorld)
    {
        if (grabPoints == null || grabPoints.Length == 0) return -1;

        float best = float.PositiveInfinity;
        int bestIdx = -1;

        for (int i = 0; i < grabPoints.Length; i++)
        {
            var gp = grabPoints[i];
            if (!gp) continue;
            if (pointOwner[i] != -1) continue;

            float d2 = (gp.position - fromWorld).sqrMagnitude;
            if (d2 < best) { best = d2; bestIdx = i; }
        }

        if (bestIdx < 0) return -1;
        if (best > grabRadius * grabRadius) return -1;
        return bestIdx;
    }
    public float CurrentAngle => wheelAngle;
}
