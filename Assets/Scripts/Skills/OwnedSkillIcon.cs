using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OwnedSkillIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SkillData skillData;
    private Image srcImage;

    void Awake()
    {
        srcImage = GetComponent<Image>();
    }

    void Start()
    {
        srcImage.sprite = skillData.icon;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 调用管理器的静态方法来显示 Tooltip
        SkillTooltip.ShowTooltip_Static(skillData.description);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 调用管理器的静态方法来隐藏 Tooltip
        SkillTooltip.HideTooltip_Static();
    }
}