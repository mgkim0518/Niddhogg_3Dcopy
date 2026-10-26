using UnityEngine;

// 머리 위 체크 표시가 항상 카메라를 보게 한다
public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam) transform.rotation = cam.transform.rotation;
    }
}
