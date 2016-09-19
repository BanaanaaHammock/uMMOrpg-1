// This component copies a Transform's position to automatically follow it,
// which is useful for the camera.
using UnityEngine;

public class CopyPosition : MonoBehaviour {
    public bool x = false;
    public bool y = false;
    public bool z = false;

    public Transform target = null;

    // cache
    Transform tr = null;

    void Awake () {
        tr = transform;
    }

    void Update () {
        if (target) {
            var p = tr.position;
            var t = target.position;
            tr.position = new Vector3((x ? t.x : p.x),
                                      (y ? t.y : p.y),
                                      (z ? t.z : p.z));
       }
    }
}
