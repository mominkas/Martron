using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CinematicCamera : MonoBehaviour
{

 public Camera Camera;
 public Vector2 motion;

    // Start is called before the first frame update
    void Start()
    {
        Camera.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        
    }

    // Update is called once per frame
    void Update()
    {
        motion = new Vector2(1,0);
        Camera.transform.Translate(motion * 100000);
        
    }
}
