using UnityEngine;
using ML.SDK; // MLStation / MLPlayer


public class CarControllerVR : MonoBehaviour
{
    private Rigidbody rb;

    [Header("Wheel References")]
    public WheelColliders colliders;
    public WheelMeshes wheelMeshes;

    [Header("VR Input (ML Station)")]
    public MLStation station;
    [Tooltip("Multiply raw trigger values (0..1).")]
    public float triggerScale = 1f;

    [Header("Steering (from SteeringWheelVR)")]
    public bool readSteeringFromWheel = true;
    public bool invertSteeringInput = false; // <— add this line
    public GameObject steeringWheelObj;
    public float steeringMaxAngle = 90f;
    public float maxSteerAngleDeg = 35f;
    public float steeringRate = 5f;
    public float steerDeadzoneNorm = 0.02f;
    public float steerSnapToZero = 0.002f;

    [Header("Power / Braking")]
    public float motorPower = 2500f;
    public float brakePower = 2500f;
    public float idleBrake = 0.35f;
    public float idleBrakeSpeed = 0.5f;
    public float neutralHoldBrake = 0.6f;
    public float throttleDeadzone = 0.08f;
    public float coastDrag = 0.25f;
    public float coastSpeedThreshold = 0.2f;
    public float limiterVelocitySnap = 5f;

    [Header("Gear Limiter")]
    [Range(0.5f, 1f)] public float limiterStartPct = 0.9f;
    public float limiterBrakePower = 1500f;

    [Header("Gearing (set by Gear Shifter)")]
    public int gearIndex = 0;
    public float gearMaxSpeedMS = -1f;

    [Header("Runtime (read-only)")]
    public float gasInput;
    public float brakeInput;
    public float steeringInput;
    public int gearSlotIndex = -1;

    private SteeringWheelVR steeringWheel;
    private float targetSteer;
    private PlayerManager playerManager;
    public GameObject playerManagerObj;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ResolvePlayerManager();
        if (steeringWheelObj)
            steeringWheel = (SteeringWheelVR)steeringWheelObj.GetComponent(typeof(SteeringWheelVR));

        // ✅ Player event bindings
        if (station != null)
        {
            station.OnPlayerSeated.AddListener(OnPlayerSettled);
            station.OnPlayerLeft.AddListener(OnPlayerLeft);
        }
    }

    // ✅ Player entry/exit events
    public void OnPlayerSettled()
    {
        if (station != null && station.GetPlayer() != null && station.GetPlayer().IsLocal)
        {
            ResolvePlayerManager();
            if (playerManager != null)
            {
                playerManager.inCar = true;
                playerManager.Precalibration(playerManager.playerML);
                playerManager.DisablePlayerAvatar(false);
            }
        }
    }

    public void OnPlayerLeft()
    {
        ResolvePlayerManager();
        if (playerManager != null)
        {
            playerManager.inCar = false;
            playerManager.DisablePlayerAvatar(true);
        }
    }

    void Update()
    {
        ApplyWheelPositions();
    }

    void FixedUpdate()
    {
        ReadInputsFixed();
        SmoothSteering();
        ApplyMotorAndBrakes();
        ApplySteering();
    }

    void ReadInputsFixed()
    {
        float gas = 0f, brake = 0f;

        if (station != null && station.IsOccupied)
        {
            var p = station.GetPlayer();
            if (p != null && p.UserInput != null)
            {
                gas = Mathf.Clamp01(p.UserInput.TriggerAxis2) * triggerScale;
                brake = Mathf.Clamp01(p.UserInput.TriggerAxis1) * triggerScale;
            }
        }

        gas = (Mathf.Abs(gas) < throttleDeadzone) ? 0f : gas;
        gasInput = Mathf.Clamp01(gas);
        brakeInput = Mathf.Clamp01(brake);

        float steerTgt = steeringInput;
        if (readSteeringFromWheel && steeringWheel != null)
        {
            float wheelDeg = steeringWheel.CurrentAngle;
            float norm01 = Mathf.InverseLerp(-steeringMaxAngle, steeringMaxAngle,
                Mathf.Clamp(wheelDeg, -steeringMaxAngle, steeringMaxAngle));
            steerTgt = Mathf.Clamp((norm01 - 0.5f) * 2f, -1f, 1f);
            if (invertSteeringInput)
                steerTgt = -steerTgt;
            if (Mathf.Abs(steerTgt) < steerDeadzoneNorm) steerTgt = 0f;
        }

        targetSteer = steerTgt;
    }

    void SmoothSteering()
    {
        float step = steeringRate * Time.fixedDeltaTime;
        steeringInput = Mathf.MoveTowards(steeringInput, targetSteer, step);
        if (Mathf.Abs(steeringInput) < steerSnapToZero) steeringInput = 0f;
    }

    void ApplyMotorAndBrakes()
    {
        if (colliders == null ||
            colliders.RLWheel == null || colliders.RRWheel == null ||
            colliders.FLWheel == null || colliders.FRWheel == null)
            return;

        float speed = rb ? rb.velocity.magnitude : 0f;
        float gearDir = (gearIndex < 0) ? -1f : 1f;

        float forwardAlong = 0f;
        if (rb != null)
        {
            float signedForward = Vector3.Dot(rb.velocity, transform.forward);
            forwardAlong = Mathf.Abs(signedForward) * Mathf.Sign(gearDir * Mathf.Sign(signedForward));
            forwardAlong = Mathf.Max(0f, forwardAlong);
        }

        float motorTq = 0f;

        if (gearIndex == 0)
        {
            motorTq = 0f;
        }
        else
        {
            motorTq = motorPower * gasInput * gearDir;

            if (gearMaxSpeedMS > 0f)
            {
                float startV = Mathf.Clamp01(limiterStartPct) * gearMaxSpeedMS;
                if (forwardAlong >= startV)
                {
                    float t = Mathf.InverseLerp(startV, gearMaxSpeedMS, Mathf.Min(forwardAlong, gearMaxSpeedMS));
                    float scale = 1f - Mathf.Clamp01(t);
                    motorTq *= scale;
                }
                if (forwardAlong >= gearMaxSpeedMS - 0.01f)
                    motorTq = 0f;
            }
        }

        colliders.RRWheel.motorTorque = motorTq;
        colliders.RLWheel.motorTorque = motorTq;

        float brakeTq = brakePower * brakeInput;

        if (gearIndex == 0)
            brakeTq = Mathf.Max(brakeTq, neutralHoldBrake * brakePower);

        if (gasInput <= 0.0001f && speed < idleBrakeSpeed)
            brakeTq = Mathf.Max(brakeTq, idleBrake * brakePower);

        float front = brakeTq * 0.7f;
        float rear = brakeTq * 0.3f;

        colliders.FRWheel.brakeTorque = front;
        colliders.FLWheel.brakeTorque = front;
        colliders.RRWheel.brakeTorque = rear;
        colliders.RLWheel.brakeTorque = rear;

        CapVelocityIfNeeded();
        ApplyCoastDrag();
    }

    void ApplySteering()
    {
        if (colliders == null || colliders.FLWheel == null || colliders.FRWheel == null) return;

        float steerDeg = steeringInput * maxSteerAngleDeg;
        colliders.FRWheel.steerAngle = steerDeg;
        colliders.FLWheel.steerAngle = steerDeg;
    }

    public void SetGearSlotIndex(int slotIndex)
    {
        gearSlotIndex = slotIndex;
    }

    public void SetGear(int index, float maxSpeedMS)
    {
        gearIndex = index;

        float resolvedMax = (maxSpeedMS > 0f) ? maxSpeedMS : -1f;
        float tableMax = GetGearMaxFromTable(gearSlotIndex);

        if (tableMax > 0f)
        {
            resolvedMax = (resolvedMax > 0f) ? Mathf.Min(resolvedMax, tableMax) : tableMax;
        }

        gearMaxSpeedMS = (resolvedMax > 0f) ? resolvedMax : -1f;
    }

    private float[] gearSpeedTableMS;
    public void SetGearSpeedTable(float[] gearMaxSpeedKPH)
    {
        if (gearMaxSpeedKPH == null || gearMaxSpeedKPH.Length == 0)
        {
            gearSpeedTableMS = null;
            return;
        }

        if (gearSpeedTableMS == null || gearSpeedTableMS.Length != gearMaxSpeedKPH.Length)
            gearSpeedTableMS = new float[gearMaxSpeedKPH.Length];

        for (int i = 0; i < gearMaxSpeedKPH.Length; i++)
        {
            float kph = Mathf.Abs(gearMaxSpeedKPH[i]);
            gearSpeedTableMS[i] = (kph > 0f) ? kph * 0.27777778f : -1f;
        }
    }

    private float GetGearMaxFromTable(int slotIndex)
    {
        if (gearSpeedTableMS == null) return -1f;
        if (gearSpeedTableMS.Length == 0) return -1f;
        if (slotIndex < 0) return -1f;
        if (slotIndex >= gearSpeedTableMS.Length)
            return gearSpeedTableMS[gearSpeedTableMS.Length - 1];
        return gearSpeedTableMS[slotIndex];
    }

    private void ApplyCoastDrag()
    {
        if (rb == null) return;
        if (gearIndex == 0) return;
        if (gasInput > 0.0001f) return;
        if (brakeInput > 0.0001f) return;

        Vector3 vel = rb.velocity;
        float speed = vel.magnitude;
        if (speed <= coastSpeedThreshold)
        {
            rb.velocity = Vector3.zero;
            return;
        }

        float drag = Mathf.Max(0f, coastDrag) * Time.fixedDeltaTime;
        if (drag <= 0f) return;

        float scale = Mathf.Clamp01(1f - drag);
        rb.velocity = vel * scale;
    }

    private void CapVelocityIfNeeded()
    {
        if (rb == null) return;
        if (gearMaxSpeedMS <= 0f) return;

        Vector3 vel = rb.velocity;
        if (vel.sqrMagnitude <= 0.0001f) return;

        float max = gearMaxSpeedMS;
        Vector3 forward = transform.forward;
        float forwardSpeed = Vector3.Dot(vel, forward);
        Vector3 lateral = vel - forward * forwardSpeed;

        float minForward = -max;
        float maxForward = max;

        if (gearIndex < 0)
        {
            maxForward = 0f;
        }
        else if (gearIndex > 0)
        {
            minForward = 0f;
        }

        float clampedForward = Mathf.Clamp(forwardSpeed, minForward, maxForward);
        if (!Mathf.Approximately(clampedForward, forwardSpeed))
        {
            float snap = Mathf.Max(0f, limiterVelocitySnap);
            if (snap > 0f)
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, clampedForward, snap * Time.fixedDeltaTime);
            else
                forwardSpeed = clampedForward;
        }

        Vector3 capped = (forward * forwardSpeed) + lateral;
        float cappedMag = capped.magnitude;
        if (cappedMag > max && cappedMag > 0.0001f)
        {
            float snap = Mathf.Max(0f, limiterVelocitySnap);
            float targetMag = Mathf.Min(cappedMag, max);
            if (snap > 0f)
                capped = capped.normalized * Mathf.MoveTowards(cappedMag, targetMag, snap * Time.fixedDeltaTime);
            else
                capped = capped.normalized * targetMag;
        }

        if ((capped - vel).sqrMagnitude > 0.0001f)
            rb.velocity = capped;
    }

    void ApplyWheelPositions()
    {
        if (colliders == null) return;
        UpdateWheel(colliders.FRWheel, wheelMeshes != null ? wheelMeshes.FRWheel : null);
        UpdateWheel(colliders.FLWheel, wheelMeshes != null ? wheelMeshes.FLWheel : null);
        UpdateWheel(colliders.RRWheel, wheelMeshes != null ? wheelMeshes.RRWheel : null);
        UpdateWheel(colliders.RLWheel, wheelMeshes != null ? wheelMeshes.RLWheel : null);
    }

    void UpdateWheel(WheelCollider coll, MeshRenderer wheelMesh)
    {
        if (coll == null || wheelMesh == null) return;
        coll.GetWorldPose(out var pos, out var rot);
        wheelMesh.transform.position = pos;
        wheelMesh.transform.rotation = rot;
    }

    void ResolvePlayerManager()
    {
        if (playerManager != null) return;
        playerManager = (PlayerManager)playerManagerObj.GetComponent(typeof(PlayerManager));



    }
}


[System.Serializable]
public class WheelColliders { public WheelCollider FRWheel, FLWheel, RRWheel, RLWheel; }

[System.Serializable]
public class WheelMeshes { public MeshRenderer FRWheel, FLWheel, RRWheel, RLWheel; }

    
