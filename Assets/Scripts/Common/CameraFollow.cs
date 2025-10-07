using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    // 你想要跟随的目标物体
    public Transform target;

    // 相机相对于目标物体的偏移量
    public Vector3 offset = new Vector3(0, 0, -10);

    void Start()
    {

    }

    // 使用 LateUpdate 是一个好习惯，可以确保相机在目标物体完成所有移动和旋转更新后再更新位置
    void LateUpdate()
    {
        // 如果目标物体不存在，则不执行任何操作
        if (target == null)
        {
            // Debug.LogWarning("相机没有设置跟随目标！");
            return;
        }

        // 更新相机的位置
        // 将目标物体的位置加上偏移量，赋值给相机的位置
        transform.position = target.position + offset;

        // （可选）让相机始终朝向目标
        // 如果你希望相机一直看着物体，可以取消下面这行代码的注释
        // transform.LookAt(target);

        // 如果你只想要一个固定的旋转角度，可以在这里设置，或者在 Inspector 中设置好后不再修改
        // transform.rotation = Quaternion.Euler(20, 0, 0); // 例如，始终保持向下20度的视角
    }
}
