// RotateOverTime.cs
using UnityEngine;

public class RotateOverTime : MonoBehaviour
{
    [Tooltip("每秒旋转的角度")]
    public float rotationSpeed = 20f;

    void Update()
    {
        // 围绕Z轴旋转 (对于2D视角)
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }
}
