using ML.SDK;
using UnityEngine;

public class PlayerHeightCalibrator : MonoBehaviour
{
    private PlayerManager playerManager;
    public GameObject playerManagerObj;

    [Header("References")]
    DistanceToGround headDistanceChecker;            // your DistanceToGround on the head
    public GameObject headDistanceCheckergameObject; // assign in Inspector
    Transform playerRigRoot;                         // Player root to move
    MLPlayer locaplaya;

    // desired head height above ground (meters)
    public GameObject headtarget;        // where you want the head to be (X/Z taken from here)

    private float calibratedHeight;      // last measured head->ground distance (Y only)

    void Awake()
    {
        ResolvePlayerManager();
    }

    void ResolvePlayerManager()
    {
        if (playerManager != null) return;
        playerManager = (PlayerManager)playerManagerObj.GetComponent(typeof(PlayerManager));


    }

    public void PlayerInstantiated()
    {
        ResolvePlayerManager();
        headDistanceChecker = (DistanceToGround)headDistanceCheckergameObject.GetComponent(typeof(DistanceToGround));
        playerRigRoot = MassiveLoopRoom.GetLocalPlayer().PlayerRoot.transform;
        locaplaya = MassiveLoopRoom.GetLocalPlayer();
    }

    public void CalibrateHeight()
    {
        ResolvePlayerManager();
        if (headDistanceChecker == null || playerRigRoot == null || headDistanceCheckergameObject == null || playerManager == null || playerManager.inCar == false)
        {
            Debug.LogWarning("Missing reference(s) for calibration.");
            return;
        }

        // Measure current head->ground distance
        float headHeight = headDistanceChecker.distanceToGround;
        if (headHeight == Mathf.Infinity)
        {
            Debug.LogWarning("No ground detected below head.");
            return;
        }
        calibratedHeight = headHeight;

        // --- Compute desired HEAD world position ---
        Vector3 headWorldPos = headDistanceCheckergameObject.transform.position;

        // Ground Y directly under the current head, derived from the measured distance
        float groundY = headWorldPos.y - headHeight;

        // Desired Y is groundY + targetHeight (numeric standard height)
        float desiredHeadY = groundY + headtarget.transform.position.y;

        // Desired X/Z come from headtarget transform (keeps your intent)
        float desiredHeadX = headtarget != null ? headtarget.transform.position.x : headWorldPos.x;
        float desiredHeadZ = headtarget != null ? headtarget.transform.position.z : headWorldPos.z;

        Vector3 desiredHeadWorldPos = new Vector3(desiredHeadX, desiredHeadY, desiredHeadZ);

        // --- Delta to move the entire rig so the head lands at desired position ---
        Vector3 delta = desiredHeadWorldPos - headWorldPos;

        // Apply once, world space
        playerRigRoot.position += delta;
        if (playerManager != null)
            playerManager.driverStationSeat.rotation = playerManager.humanoidModel.rotation;

        // Optional: log for debugging
        // Debug.Log($"Calibrate: move rig by {delta} (head {headHeight:F3} -> target {targetHeight:F3})");
    }
}
