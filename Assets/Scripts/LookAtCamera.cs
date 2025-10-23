using System;
using UnityEngine;
using TMPro;     // TextMeshPro
using ML.SDK;   // (not required here, but kept if you need it elsewhere)

public class LookAtCamera : MonoBehaviour
{
  

    void Update ()
    {
       

        // --- Billboard (face camera) ---
        if (Camera.main != null )
        {
            
                Vector3 lookTarget = new Vector3(Camera.main.transform.position.x, transform.position.y, Camera.main.transform.position.z);
                transform.LookAt(lookTarget);
           
        }
    }

    
}
