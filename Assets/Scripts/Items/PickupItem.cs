using UnityEngine;
using UnityEngine.UI;

public class PickupItem : MonoBehaviour
{
    public SkillData skillData;
    private SpriteRenderer sprite;
    public int Id { get; set; }

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        if (skillData != null && sprite != null)
        {
            sprite.sprite = skillData.icon;
            sprite.color = new Color(1f, 1f, 1f, 1f);
        }
    }

    public void SetSkillData(SkillData skillData)
    {
        this.skillData = skillData;
        if (skillData != null && sprite != null)
        {
            sprite.sprite = skillData.icon;
            sprite.color = new Color(1f, 1f, 1f, 1f);
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(Constants.TagPlayer) || collision.transform.parent.CompareTag(Constants.TagPlayer)) // bullet的tag也可能是Player，layer是bullets
        {
            GameObject player = collision.gameObject;
            var status = player.GetComponentInParent<CharacterStatus>();
            if (status == null)
            {
                status = player.GetComponentInChildren<CharacterStatus>();
            }
            if (status != null)
            {
                status.State.ActiveSkillId = skillData.id;
                if (status.State.PlayerId == CharacterManager.Instance.MyInfo.Id)
                {
                    var spc = UIManager.Instance.GetComponent<StatusPanelController>();
                    spc.UpdateMyStatusUI(status.State);
                    UIManager.Instance.ShowInfoPanel($"You got an active item: {skillData.skillName}, press space to use.", Color.white, 3);
                }
                LevelManager.Instance.PickupItems.Remove(Id);
                Destroy(gameObject);
            }
        }
    }
}
