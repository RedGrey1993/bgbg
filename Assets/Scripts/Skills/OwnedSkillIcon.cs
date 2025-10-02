using UnityEngine;
using UnityEngine.EventSystems;

public class OwnedSkillIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SkillData skillData;

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