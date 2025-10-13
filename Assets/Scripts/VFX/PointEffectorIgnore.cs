using UnityEngine;

public class PointEffectorIgnore : MonoBehaviour
{
    public Collider2D pointEffectorCollider;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var my = CharacterManager.Instance.GetMyselfGameObject();
        var myColliders = my.GetComponentsInChildren<Collider2D>();
        foreach (var c in myColliders)
        {
            Physics2D.IgnoreCollision(c, pointEffectorCollider);
        }
    }
}
