using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PickupItem : MonoBehaviour
{
    public SkillData skillData;
    private SpriteRenderer sprite;
    public int Id { get; set; }
    private Collider2D col2D;
    private float bornTime;
    private bool isPicked = false;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        col2D = GetComponent<Collider2D>();
        col2D.enabled = false;
        bornTime = Time.time;
    }

    void Start()
    {
        if (skillData != null && sprite != null)
        {
            sprite.sprite = skillData.icon;
            sprite.color = new Color(1f, 1f, 1f, 1f);
        }
    }

    void FixedUpdate()
    {
        if (Time.time - bornTime > 3f && !isPicked) // 超过拾取的动画时间
        {
            col2D.enabled = true;
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

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(Constants.TagPlayerFeet)) // bullet的tag也可能是Player，layer是bullets
        {
            var status = other.GetCharacterStatus();
            if (status != null)
            {
                isPicked = true;
                col2D.enabled = false;
                status.StartCoroutine(PickupItemAnim(status));
            }
        }
    }

    private IEnumerator PickupItemAnim(CharacterStatus status)
    {
        Vector3 initialPos = gameObject.transform.position;
        if (status.characterData.Is3DModel())
        {
            status.CharacterAI.PlayAnimationAllLayers("Pick up item");
            float squatDownTime = 0.59f; // 下蹲时间
            yield return new WaitForSeconds(squatDownTime);

            var rightHandTransform = status.CharacterAI.animator.GetBoneTransform(HumanBodyBones.LeftHand);
            gameObject.transform.SetParent(rightHandTransform);
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localScale *= status.transform.transform.lossyScale.x / gameObject.transform.lossyScale.x;
        }

        if (status.State.ActiveSkillId > 0)
        {
            var skillData = SkillDatabase.Instance.GetActiveSkill(status.State.ActiveSkillId);
            LevelManager.Instance.ShowPickUpItem(initialPos, skillData, status.State.ActiveSkillCurCd);
        }

        status.State.ActiveSkillId = skillData.id;
        status.State.ActiveSkillCurCd = LevelManager.Instance.PickupItems[Id].Item1.CurrentCooldown;
        if (status.State.PlayerId == CharacterManager.Instance.MyInfo.Id)
        {
            UIManager.Instance.UpdateMyStatusUI(status);
            UIManager.Instance.ShowInfoPanel($"You got an active item: {skillData.skillName}, press space to use.", Color.white, 3);
        }
        LevelManager.Instance.PickupItems.Remove(Id);

        if (status.characterData.Is3DModel())
        {
            float standUpTime = 2.25f;
            yield return new WaitForSeconds(standUpTime);
        }
        Destroy(gameObject);
    }
}
