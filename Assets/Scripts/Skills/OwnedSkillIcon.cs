using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OwnedSkillIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SkillData SkillData { get;  private set; }
    private Image srcImage;

    void Awake()
    {
        srcImage = GetComponent<Image>();
    }

    void Start()
    {
        if (SkillData != null)
        {
            srcImage.sprite = SkillData.icon;
            srcImage.color = new Color(1f, 1f, 1f, 1f);
        }
    }

    public void SetSkillData(SkillData skillData, float alpha = 1f)
    {
        SkillData = skillData;
        if (skillData == null)
        {
            srcImage.sprite = null;
            srcImage.color = new Color(1f, 1f, 1f, 0f);
        }
        else
        {
            srcImage.sprite = skillData.icon;
            srcImage.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (SkillData == null) return;
        // 调用管理器的静态方法来显示 Tooltip
        SkillTooltip.ShowTooltip_Static(
            SkillData.skillName +
            "\n\n" +
            SkillData.description +
            "\n\n" +
            SkillData.backgroundStory,

            SkillData.tipColor
        );
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 调用管理器的静态方法来隐藏 Tooltip
        SkillTooltip.HideTooltip_Static();
    }
}