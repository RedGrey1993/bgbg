using UnityEngine;

public class SimpleLaser : MonoBehaviour
{
    public LineRenderer lineRenderer1;
    public LineRenderer lineRenderer2;
    public Transform StartPoint { get; set;}
    public float MaxDistance { get; set;} = 10;
    public Vector3 dir { get; set; }

    void FixedUpdate()
    {
        // 1. 设置起点
        lineRenderer1.SetPosition(0, StartPoint.position);
        lineRenderer2.SetPosition(0, StartPoint.position);

        // 2. 设定终点 (直接向前延伸固定长度，不需要碰撞)
        // 如果你需要它打到墙上停下，才需要用 Physics.Raycast
        Vector3 endPosition = StartPoint.position + dir * MaxDistance;
        
        lineRenderer1.SetPosition(1, endPosition);
        lineRenderer2.SetPosition(1, endPosition);
    }
}