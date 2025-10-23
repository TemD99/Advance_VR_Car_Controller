using UnityEngine;

using ML.SDK;

using TMPro;

public class VRGearShifter_Simple : MonoBehaviour

{

    private PlayerManager playerManager;

    public GameObject playerManagerObj;

    [Header("References")]

    public Transform lever;

    public Transform railStart;

    public Transform railEnd;

    [Header("Grab Settings")]

    public float grabRadius = 0.25f;

    public float releaseRadius = 0.40f;

    [Header("Snap Targets (Gears)")]

    public Transform[] snapTargets;

    public float snapDuration = 0.1f;

    [Header("Snap Space")]

    [Tooltip("ON = preserve local X, snap local Y/Z (safer when parent isn’t world-aligned). OFF = preserve world X.")]

    public bool snapInLocalSpace = true;

    [Header("Vehicle Link")]

    public GameObject carObj;

    [Tooltip("Per-slot max speeds (kph). Index matches snapTargets.")]

    public float[] gearMaxSpeedKPK = new float[6] { 0f, 20f, 40f, 60f, 80f, 100f };

    public int neutralGearSlotIndex = 0;

    public int reverseGearSlotIndex = -1;

    [Header("TMP Gear Indicators")]

    public TMP_Text[] gearLabelTexts;

    public Color inactiveColor = new Color(0.75f, 0.75f, 0.75f, 1f);

    public Color activeColor = new Color(0.20f, 0.85f, 0.30f, 1f);

    public bool highlightNearestWhileHolding = true;

    public bool highlightNearestWhileSnapping = true;

    [Header("Input")]

    public MLStation station;

    // === Continuous Target Driving (replaces parenting) ===

    [Header("Target Driving (while holding)")]

    [Tooltip("If true, while the lever is held the active PlayerManager hand target is moved to the lever each frame.")]

    public bool driveTargetWhileHolding = true;

    [Tooltip("Also rotate the target to match the lever while holding.")]

    public bool driveTargetRotation = true;

    [Tooltip("Optional world-space offset applied to the target relative to the lever while holding.")]

    public Vector3 targetWorldPosOffset = Vector3.zero;

    [Tooltip("Optional rotation offset (Euler) applied on top of lever rotation while holding.")]

    public Vector3 targetEulerOffset = Vector3.zero;

    [Tooltip("If > 0, smooth target following with MoveTowards/RotateTowards per second. 0 = instant.")]

    public float targetFollowSpeed = 0f; // m/s and deg/s (approx)

    [Header("Target Driving Source")]

    public Transform targetFollowSource;

    private Transform _leverT;

    private CarControllerVR _car;

    private bool isHolding;

    private bool prevLeftGrabPressed;

    private bool prevRightGrabPressed;

    private bool _activeHandIsLeft;

    private Transform _activeHand;

    private bool _handOverrideActive;

    private bool _isSnapping;

    private float _snapClock;

    // World-space snap state

    private Vector3 _snapFromW, _snapToW;

    // Local-space snap state

    private Vector3 _snapFromL, _snapToL;

    private int _pendingGear = -1;

    private int _currentGearIndex = 0;

    private int _highlightedIndex = -1;

    // PlayerManager targets (no reparenting)

    private Transform _leftTarget, _rightTarget;

    private Transform FollowSource => targetFollowSource != null ? targetFollowSource : _leverT;

    void ResolvePlayerManager()
    {
        if (playerManager != null) return;
        playerManager = (PlayerManager)playerManagerObj.GetComponent(typeof(PlayerManager));

    }

    void OnValidate()

    {

        EnsureSpeedArrayLength();

    }

    void Start()

    {

        ResolvePlayerManager();

        _leverT = lever ? lever : transform;

        EnsureSpeedArrayLength();

        _car = (CarControllerVR)carObj.GetComponent(typeof(CarControllerVR));

        if (_car != null)

            _car.SetGearSpeedTable(gearMaxSpeedKPK);

        EnsureTargets();

        ResolveSpecialSlots(out var reverseIdx, out var neutralIdx);

        int slotCount = (snapTargets != null) ? snapTargets.Length : 0;

        if (slotCount > 0)

        {

            if (neutralIdx < 0) neutralIdx = Mathf.Clamp(neutralGearSlotIndex, 0, slotCount - 1);

            _currentGearIndex = Mathf.Clamp(neutralIdx >= 0 ? neutralIdx : 0, 0, slotCount - 1);

        }

        else

        {

            _currentGearIndex = 0;

        }

        ForceLeverToSlotImmediate(_currentGearIndex);

        PushGearToCar(_currentGearIndex);

        UpdateHighlightPinned();

    }

    void Update()

    {

        bool leftPressed = false;

        bool rightPressed = false;

        if (station != null && station.IsOccupied)

        {

            var p = station.GetPlayer();

            if (p != null && p.UserInput != null)

            {

                leftPressed = p.UserInput.Grip1 > 0.5f;

                rightPressed = p.UserInput.Grip2 > 0.5f;

            }

        }

        bool leftDown = leftPressed && !prevLeftGrabPressed;

        bool rightDown = rightPressed && !prevRightGrabPressed;

        if (!isHolding)

        {

            if (leftDown) TryBeginGrab(true);

            if (!isHolding && rightDown) TryBeginGrab(false);

        }

        else

        {

            bool activePressed = _activeHandIsLeft ? leftPressed : rightPressed;

            bool activeReleased = !activePressed && (_activeHandIsLeft ? prevLeftGrabPressed : prevRightGrabPressed);

            Transform refT = ResolveActiveHand();

            if (!refT)

            {

                activeReleased = true;

            }

            else if (releaseRadius > 0f && Vector3.Distance(refT.position, _leverT.position) > releaseRadius)

            {

                activeReleased = true;

            }

            if (activeReleased)

            {

                EndGrab();

            }

            else

            {

                // Move lever along rail according to active hand position

                float t = Project01(refT.position);

                _leverT.position = RailPos(t);

                // While holding, also drive the appropriate PlayerManager target to the lever

                if (driveTargetWhileHolding)

                    DriveActivePMTargetToSource();

                if (highlightNearestWhileHolding) UpdateHighlightNearest();

                else UpdateHighlightPinned();

            }

        }

        if (!isHolding)

        {

            if (_isSnapping)

                UpdateSnapMotion();

            else

                UpdateHighlightPinned();

        }

        prevLeftGrabPressed = leftPressed;

        prevRightGrabPressed = rightPressed;

    }

    private void EnsureTargets()

    {

        ResolvePlayerManager();

        if (playerManager == null) return;

        if (_leftTarget == null && playerManager.left_target != null)

            _leftTarget = playerManager.left_target.transform;

        if (_rightTarget == null && playerManager.right_target != null)

            _rightTarget = playerManager.right_target.transform;

    }

    private Transform ResolveActiveHand()

    {

        if (_activeHand == null)

            _activeHand = ResolveHandTransform(_activeHandIsLeft);

        return _activeHand;

    }

    private void DriveActivePMTargetToSource()

    {

        EnsureTargets();

        Transform tgt = _activeHandIsLeft ? _leftTarget : _rightTarget;

        if (tgt == null) return;

        Transform src = FollowSource; // ← lever or your custom object

        // Desired world transform for the target

        Vector3 desiredPos = src.position + targetWorldPosOffset;

        Quaternion desiredRot = src.rotation * Quaternion.Euler(targetEulerOffset);

        if (targetFollowSpeed > 0f)

        {

            float step = targetFollowSpeed * Time.deltaTime;

            tgt.position = Vector3.MoveTowards(tgt.position, desiredPos, step);

            if (driveTargetRotation)

            {

                float angStep = targetFollowSpeed * Time.deltaTime * 60f;

                tgt.rotation = Quaternion.RotateTowards(tgt.rotation, desiredRot, angStep);

            }

        }

        else

        {

            tgt.position = desiredPos;

            if (driveTargetRotation)

                tgt.rotation = desiredRot;

        }

    }

    private void UpdateSnapMotion()

    {

        _snapClock += Time.deltaTime;

        float u = Mathf.Clamp01(_snapClock / Mathf.Max(0.0001f, snapDuration));

        u = u * u * (3f - 2f * u); // smoothstep

        if (snapInLocalSpace)

        {

            // Lerp ONLY local Y/Z; local X preserved by construction of _snapFromL/_snapToL

            Vector3 lp = _leverT.localPosition;

            lp.y = Mathf.Lerp(_snapFromL.y, _snapToL.y, u);

            lp.z = Mathf.Lerp(_snapFromL.z, _snapToL.z, u);

            _leverT.localPosition = lp;

        }

        else

        {

            // Lerp ONLY world Y/Z; world X preserved by construction of _snapFromW/_snapToW

            Vector3 p = _leverT.position;

            p.y = Mathf.Lerp(_snapFromW.y, _snapToW.y, u);

            p.z = Mathf.Lerp(_snapFromW.z, _snapToW.z, u);

            _leverT.position = p;

        }

        if (highlightNearestWhileSnapping) UpdateHighlightNearest();

        else UpdateHighlightToIndex(_pendingGear >= 0 ? _pendingGear : _currentGearIndex);

        if (u >= 1f)

        {

            _isSnapping = false;

            int landed = (_pendingGear >= 0) ? _pendingGear : _currentGearIndex;

            SetSelectedGear(landed);

            _pendingGear = -1;

            UpdateHighlightPinned();

        }

    }

    private void TryBeginGrab(bool useLeftHand)

    {

        if (isHolding) return;

        Transform hand = ResolveHandTransform(useLeftHand);

        if (!hand) return;

        if (Vector3.Distance(hand.position, _leverT.position) > grabRadius)

            return;

        _activeHandIsLeft = useLeftHand;

        _activeHand = hand;

        isHolding = true;

        _isSnapping = false;

        _pendingGear = -1;

        // Pause PlayerManager updates for this hand's target (so it won't overwrite our writes)

        ResolvePlayerManager();

        if (playerManager != null && HandSupportsOverride(hand, useLeftHand))

        {

            playerManager.BeginHandOverride(useLeftHand);

            _handOverrideActive = true;

        }

        else

        {

            _handOverrideActive = false;

        }

    }

    private Transform ResolveHandTransform(bool isLeftHand)

    {

        ResolvePlayerManager();

        if (playerManager != null)

        {

            Transform ik = isLeftHand ? playerManager.leftHandIK : playerManager.rightHandIK;

            if (ik != null) return ik;

            return playerManager.playerHead;

        }

        return null;

    }

    private bool HandSupportsOverride(Transform candidate, bool isLeftHand)

    {

        ResolvePlayerManager();

        if (playerManager == null || candidate == null) return false;

        return candidate == (isLeftHand ? playerManager.leftHandIK : playerManager.rightHandIK);

    }

    private void EndGrab()

    {

        // Stop driving target; simply unlock so PlayerManager resumes normal IK-follow

        ResolvePlayerManager();

        if (_handOverrideActive && playerManager != null)

            playerManager.EndHandOverride(_activeHandIsLeft);

        _handOverrideActive = false;

        _activeHand = null;

        isHolding = false;

        BeginSnapToNearest();

    }

    // === SNAP LOGIC ===

    private void BeginSnapToNearest()

    {

        if (snapTargets == null || snapTargets.Length == 0) return;

        int nearest = NearestSnapIndex();

        if (nearest < 0) return;

        _pendingGear = nearest;

        _isSnapping = true;

        _snapClock = 0f;

        if (snapInLocalSpace)

        {

            // Preserve LOCAL X, snap LOCAL Y/Z to target

            _snapFromL = _leverT.localPosition;

            Vector3 targetLocal = _snapFromL;

            if (_leverT.parent)

                targetLocal = _leverT.parent.InverseTransformPoint(snapTargets[nearest].position);

            else

                targetLocal = snapTargets[nearest].position; // no parent => local == world

            _snapToL = new Vector3(_snapFromL.x, targetLocal.y, targetLocal.z);

            if (snapDuration <= 0f)

            {

                _leverT.localPosition = _snapToL;

                _isSnapping = false;

                SetSelectedGear(_pendingGear);

                _pendingGear = -1;

            }

        }

        else

        {

            // Preserve WORLD X, snap WORLD Y/Z to target

            _snapFromW = _leverT.position;

            Vector3 target = snapTargets[nearest].position;

            _snapToW = new Vector3(_snapFromW.x, target.y, target.z);

            if (snapDuration <= 0f)

            {

                _leverT.position = _snapToW;

                _isSnapping = false;

                SetSelectedGear(_pendingGear);

                _pendingGear = -1;

            }

        }

    }

    private void SetSelectedGear(int idx)

    {

        _currentGearIndex = idx;

        PushGearToCar(idx);

    }

    private void PushGearToCar(int idx)

    {

        if (_car == null) return;

        _car.SetGearSpeedTable(gearMaxSpeedKPK);

        int clampedSlot = ClampSlotIndex(idx);

        _car.SetGearSlotIndex(clampedSlot);

        int carGear = SlotToCarGear(clampedSlot);

        float kph = ResolveSlotKph(clampedSlot);

        float ms = (kph > 0f) ? kph * 0.27777778f : 0f;

        _car.SetGear(carGear, ms);

    }

    private int ClampSlotIndex(int idx)

    {

        if (idx < 0) return -1;

        int max = (snapTargets != null) ? snapTargets.Length : 0;

        if (max <= 0) return idx;

        return Mathf.Clamp(idx, 0, max - 1);

    }

    private int SlotToCarGear(int slotIndex)

    {

        ResolveSpecialSlots(out var reverseIdx, out var neutralIdx);

        if (slotIndex == reverseIdx) return -1;

        if (slotIndex == neutralIdx) return 0;

        int gearOrdinal = 0;

        int total = (snapTargets != null) ? snapTargets.Length : 0;

        for (int i = 0; i < total; i++)

        {

            if (i == reverseIdx || i == neutralIdx) continue;

            gearOrdinal++;

            if (i == slotIndex) return gearOrdinal;

        }

        return gearOrdinal > 0 ? gearOrdinal : 0;

    }

    private float ResolveSlotKph(int slotIndex)

    {

        if (slotIndex < 0) return 0f;

        ResolveSpecialSlots(out _, out var neutralIdx);

        if (slotIndex == neutralIdx) return 0f;

        if (gearMaxSpeedKPK == null || gearMaxSpeedKPK.Length == 0) return 0f;

        int clampedSlot = Mathf.Clamp(slotIndex, 0, gearMaxSpeedKPK.Length - 1);

        float value = gearMaxSpeedKPK[clampedSlot];

        return Mathf.Max(0f, Mathf.Abs(value));

    }

    private void EnsureSpeedArrayLength()

    {

        int desired = (snapTargets != null) ? snapTargets.Length : 0;

        if (desired <= 0) return;

        if (gearMaxSpeedKPK == null || gearMaxSpeedKPK.Length != desired)

        {

            float fallback = 0f;

            if (gearMaxSpeedKPK != null && gearMaxSpeedKPK.Length > 0)

                fallback = gearMaxSpeedKPK[gearMaxSpeedKPK.Length - 1];

            float[] resized = new float[desired];

            for (int i = 0; i < desired; i++)

            {

                if (gearMaxSpeedKPK != null && i < gearMaxSpeedKPK.Length)

                    resized[i] = gearMaxSpeedKPK[i];

                else

                    resized[i] = fallback;

            }

            gearMaxSpeedKPK = resized;

        }

    }

    private void ResolveSpecialSlots(out int reverseIdx, out int neutralIdx)

    {

        int count = (snapTargets != null) ? snapTargets.Length : 0;

        reverseIdx = -1;

        neutralIdx = -1;

        if (count <= 0) return;

        if (reverseGearSlotIndex >= 0)

            reverseIdx = Mathf.Clamp(reverseGearSlotIndex, 0, count - 1);

        else

            reverseIdx = 0;

        if (neutralGearSlotIndex >= 0)

            neutralIdx = Mathf.Clamp(neutralGearSlotIndex, 0, count - 1);

        else

            neutralIdx = (reverseIdx == 0 && count > 1) ? 1 : 0;

        if (neutralIdx == reverseIdx)

        {

            if (neutralGearSlotIndex >= 0)

            {

                neutralIdx = (neutralIdx == 0 && count > 1) ? 1 : Mathf.Max(0, neutralIdx - 1);

            }

            else

            {

                neutralIdx = (reverseIdx == 0 && count > 1) ? 1 : 0;

            }

        }

    }

    private int NearestSnapIndex()

    {

        if (snapTargets == null || snapTargets.Length == 0) return -1;

        Vector3 cur = _leverT.position;

        int best = -1; float bestD2 = float.PositiveInfinity;

        for (int i = 0; i < snapTargets.Length; i++)

        {

            var t = snapTargets[i];

            if (!t) continue;

            float d2 = (t.position - cur).sqrMagnitude;

            if (d2 < bestD2) { bestD2 = d2; best = i; }

        }

        return best;

    }

    private void ForceLeverToSlotImmediate(int idx)

    {

        if (snapTargets == null || idx < 0 || idx >= snapTargets.Length) return;

        var t = snapTargets[idx];

        if (!t) return;

        if (snapInLocalSpace)

        {

            Vector3 curL = _leverT.localPosition;

            Vector3 targetL = curL;

            if (_leverT.parent)

                targetL = _leverT.parent.InverseTransformPoint(t.position);

            else

                targetL = t.position;

            _leverT.localPosition = new Vector3(curL.x, targetL.y, targetL.z);

        }

        else

        {

            Vector3 curW = _leverT.position;

            Vector3 targetW = t.position;

            _leverT.position = new Vector3(curW.x, targetW.y, targetW.z);

        }

    }

    // === RAIL LOGIC ===

    private bool RailValid => railStart && railEnd;

    private float Project01(Vector3 p)

    {

        if (!RailValid) return 0f;

        Vector3 a = railStart.position;

        Vector3 ab = railEnd.position - a;

        float t = Vector3.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude);

        return Mathf.Clamp01(t);

    }

    private Vector3 RailPos(float t01)

    {

        if (!RailValid) return _leverT.position;

        return Vector3.Lerp(railStart.position, railEnd.position, Mathf.Clamp01(t01));

    }

#if UNITY_EDITOR

    void OnDrawGizmosSelected()

    {

        if (RailValid)

        {

            Gizmos.color = Color.cyan;

            Gizmos.DrawLine(railStart.position, railEnd.position);

            Gizmos.DrawWireSphere(railStart.position, 0.01f);

            Gizmos.DrawWireSphere(railEnd.position, 0.01f);

        }

    }

#endif

    private void UpdateHighlightNearest()

    {

        int idx = NearestSnapIndex();

        if (idx < 0) idx = _currentGearIndex;

        UpdateHighlightToIndex(idx);

    }

    private void UpdateHighlightPinned()

    {

        UpdateHighlightToIndex(_currentGearIndex);

    }

    private void UpdateHighlightToIndex(int idx)

    {

        if (idx == _highlightedIndex) return;

        if (_highlightedIndex >= 0)

            SetTMPColorForIndex(_highlightedIndex, inactiveColor);

        SetTMPColorForIndex(idx, activeColor);

        _highlightedIndex = idx;

    }

    private void SetTMPColorForIndex(int i, Color c)

    {

        TMP_Text tmp = GetTMPForIndex(i);

        if (!tmp) return;

        tmp.color = c;

    }

    private TMP_Text GetTMPForIndex(int i)

    {

        if (gearLabelTexts != null && i >= 0 && i < gearLabelTexts.Length && gearLabelTexts[i])

            return gearLabelTexts[i];

        if (snapTargets != null && i >= 0 && i < snapTargets.Length && snapTargets[i])

            return snapTargets[i].GetComponentInChildren<TMP_Text>(true);

        return null;

    }


}
