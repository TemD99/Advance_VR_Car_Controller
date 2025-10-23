using ML.SDK;
using System;
using System.Collections;
using TMPro;
using UnityEngine;


[DefaultExecutionOrder(-10000)]
public class PlayerManager : MonoBehaviour
{


    public Transform cam { get; private set; }

    [HideInInspector] public Animator anim;
    [HideInInspector] public Transform playerHead;
    public Transform playerHeadObject;
    [HideInInspector] public Transform leftHandAvatar;
    [HideInInspector] public Transform rightHandAvatar;
    [HideInInspector] public Transform leftHandIK;
    [HideInInspector] public Transform rightHandIK;
    [HideInInspector] public Transform hip;
    [HideInInspector] public Transform leftFoot;
    [HideInInspector] public Transform rightFoot;
    public Transform playerRootObject;
    public Transform humanoidModel;
    public Transform humanoidModelNeck;
    public Transform driverStationSeat;
    public Transform customNamePlate;
    public bool playerInstantiated = false;
    bool primary = false;

    public MLPlayer playerML;
    public GameObject left_target;   // left target
    public GameObject right_target;  // right target
     Vector3 left_targetOriginPos;   // left target
    Vector3 right_targetOriginPos;  // right target
    Quaternion left_targetOriginRot;   // left target
    Quaternion right_targetOriginRot;  // right target
    // ADDED: single-press gate
    private bool _lastPrimary = false;

    // Offsets (tune these in degrees to match your model's forward/up)
    [SerializeField] public Vector3 leftHandOffsetEuler = new Vector3(0f, 90f, 90f);
    [SerializeField] public Vector3 rightHandOffsetEuler = new Vector3(0f, -90f, -90f);

    // ===== ADDED: per-hand tracking lock counters (>=1 => PlayerManager stops updating that hand's target) =====
    public int leftHandHoldLocks = 0;
    public int rightHandHoldLocks = 0;
    public MLStation station;
    [HideInInspector]public bool inCar = false;
    // ===== ADDED: simple API for others (e.g., SteeringWheelVR) to pause/resume PlayerManager hand tracking =====
    const string EVENT_ID = "DisableRemotePlayerAvatar";
    EventToken token;

    private void DisableRemotePlayerAvatar(object[] args)
    {
        if (args.Length > 0)
        {
            int actorId = (int)args[0];
            int state = (int)args[1];
            MLPlayer[] mLPlayers = MassiveLoopRoom.GetAllPlayers();
            foreach (MLPlayer player in mLPlayers) { 
            
            if(player.ActorId == actorId)
                {
                    Animator[] anims = player.PlayerRoot.GetComponentsInChildren<Animator>(true);
                    for (int i = 0; i < anims.Length; i++)
                    {
                        if (anims[i].avatar != null)
                        {
                            if (state == 1)
                            {
                             
                                
                                anims[i].gameObject.SetActive(true);
                                MLUtility.FindInChildrenRecursive(player.PlayerRoot.transform, "Pref_ PlayerOverheadUI").gameObject.SetActive(true);
                                customNamePlate.gameObject.SetActive(false);
                            }
                            else
                            {
                                anims[i].gameObject.SetActive(false);
                                MLUtility.FindInChildrenRecursive(player.PlayerRoot.transform, "Pref_ PlayerOverheadUI").gameObject.SetActive(false);
                                customNamePlate.gameObject.SetActive(true);
                                player.LoadPlayerThumbnail(thumbnail =>
                                {
                                    // This block acts as the callback and has access to customText and myGameObject

                                    OnThumbnailLoaded(thumbnail, player, customNamePlate.gameObject);
                                });

                              
                               
                            }

                        }

                    }
                    break;
                }
            }
        }
    }
    private void OnThumbnailLoaded(Texture2D thumbnail, MLPlayer mlPlayer, GameObject myGameObject)
    {
        if (thumbnail == null)
        {
            Debug.LogError("Failed to load player thumbnail.");
            return;
        }

        // Get UI components
        UnityEngine.UI.RawImage namePlateImage = myGameObject.GetComponentInChildren<UnityEngine.UI.RawImage>();
        TextMeshProUGUI text = myGameObject.GetComponentInChildren<TextMeshProUGUI>();

        if (namePlateImage != null)
        {
            // Assign the Texture2D directly to the RawImage
            namePlateImage.texture = thumbnail;

            // Optionally adjust UV rect to fit full texture
            namePlateImage.uvRect = new Rect(0, 0, 1, 1);

            // Update player name
            if (text != null)
            {
                text.text = mlPlayer.NickName;
            }
            else
            {
                Debug.LogWarning("No TextMeshProUGUI component found in children of the provided GameObject.");
            }
        }
        else
        {
            Debug.LogError("No RawImage component found in children of the provided GameObject.");
        }
    }

    public void BeginHandOverride(bool isLeft)
    {
        if (isLeft) leftHandHoldLocks = Mathf.Max(0, leftHandHoldLocks + 1);
        else rightHandHoldLocks = Mathf.Max(0, rightHandHoldLocks + 1);
    }
    public void EndHandOverride(bool isLeft)
    {
        if (isLeft) leftHandHoldLocks = Mathf.Max(0, leftHandHoldLocks - 1);
        else rightHandHoldLocks = Mathf.Max(0, rightHandHoldLocks - 1);
    }

    

    void Start()
    {
        MassiveLoopRoom.OnPlayerInstantiated += PlayerJoined;
        token = this.AddEventHandler(EVENT_ID, DisableRemotePlayerAvatar);
        left_targetOriginPos = left_target.transform.localPosition;
        right_targetOriginPos = right_target.transform.localPosition;
        left_targetOriginRot = left_target.transform.localRotation;
        right_targetOriginRot = right_target.transform.localRotation;
    }

    private void Update()
    {
        if (playerInstantiated == false) return;

        primary = playerML.UserInput.Primary2;

        // fire once per press (rising edge), ignores hold/repeat
        if (primary && !_lastPrimary)
        {
            Precalibration(playerML);
        }
        _lastPrimary = primary;

        if (leftHandIK != null && rightHandIK != null)
        {
            // LEFT — skip updating if locked by SteeringWheelVR
            if (leftHandHoldLocks == 0 && left_target != null && inCar)
            {
                left_target.transform.position = leftHandIK.position;
                Quaternion leftRot = leftHandIK.rotation * Quaternion.Euler(leftHandOffsetEuler);
                left_target.transform.rotation = leftRot;
            }

            // RIGHT — skip updating if locked by SteeringWheelVR
            if (rightHandHoldLocks == 0 && right_target != null && inCar)
            {
                right_target.transform.position = rightHandIK.position;
                Quaternion rightRot = rightHandIK.rotation * Quaternion.Euler(rightHandOffsetEuler);
                right_target.transform.rotation = rightRot;
            }
        }
    }

    private void PlayerJoined(MLPlayer player)
    {
        if (player == null) return;

        if (player.IsLocal)
        {
            playerML = player;
            playerInstantiated = true;
    

            StartCoroutine(InitializeSyn(player));
        }
       
    }

    IEnumerator InitializeSyn(MLPlayer player)
    {
        yield return new WaitForSeconds(3);


        Animator[] anims = player.PlayerRoot.GetComponentsInChildren<Animator>();
        for (int i = 0; i < anims.Length; i++)
        {
            if (i == 1)
            {
                anim = anims[i];
            }
           
        }

        playerHead = Camera.main.transform;
        if (playerHeadObject != null && playerHead != null && playerRootObject != null)
        {
            playerHeadObject.transform.parent = playerHead.transform;
            playerHeadObject.transform.position = playerHead.transform.position;
            playerHeadObject.transform.rotation = playerHead.transform.rotation;
            playerRootObject.transform.parent = player.PlayerRoot.transform;
            playerRootObject.transform.position = player.PlayerRoot.transform.position;
            playerRootObject.transform.rotation = player.PlayerRoot.transform.rotation;
        }

        rightHandAvatar = anim.GetBoneTransform(HumanBodyBones.RightHand);
        leftHandAvatar = anim.GetBoneTransform(HumanBodyBones.LeftHand);

        rightHandIK = MLUtility.FindInChildrenRecursive(player.PlayerRoot.transform, "RightHand_IK");
        leftHandIK = MLUtility.FindInChildrenRecursive(player.PlayerRoot.transform, "LeftHand_IK");
       

        DistanceToGround temp = (DistanceToGround)playerHeadObject.GetComponent(typeof(DistanceToGround));
        if (temp != null) temp.PlayerInstantiated();
       
    
    }

 public void DisablePlayerAvatar(bool value)
    {
        anim.gameObject.SetActive(value);
        // Assuming you already have a reference to the top-level "RightHand Controller"
        GameObject.Find("RightHand Controller").transform.GetChild(0).GetChild(1).gameObject.SetActive(value);
        GameObject.Find("LeftHand Controller").transform.GetChild(0).GetChild(1).gameObject.SetActive(value);
        this.InvokeNetwork(EVENT_ID, EventTarget.Others, null, playerML.ActorId, value);
        if (value == false)
        {
            humanoidModelNeck.localScale = new Vector3(0, 0, 0);
        }
        else
        {
            humanoidModelNeck.localScale = new Vector3(1, 1, 1);
            left_target.transform.localPosition = left_targetOriginPos;
            right_target.transform.localPosition = right_targetOriginPos;
            left_target.transform.localRotation = left_targetOriginRot;
            right_target.transform.localRotation = right_targetOriginRot;
        }

    }



 
    public void Precalibration(MLPlayer player)
    {
        CalibratePlayerHeight(player);
    }

    void CalibratePlayerHeight(MLPlayer player)
    {
        if (!playerHeadObject) return;

        PlayerHeightCalibrator temp1 = (PlayerHeightCalibrator)playerRootObject.GetComponent(typeof(PlayerHeightCalibrator));
        if (temp1 != null)
        {
            temp1.PlayerInstantiated();
            temp1.CalibrateHeight();
        }
    }

    
   
}
