using UnityEngine;
using UnityEngine.UI;
using System;

public class LearnableSkillButton : MonoBehaviour
{
    public SkillData skillData { get; private set; }
    private Action<SkillData> onSkillSelectedCallback;

    private Button button;
    private Image iconImage; // 按钮上的图标

    private void Awake()
    {
        button = GetComponent<Button>();
        iconImage = GetComponent<Image>();
        button.onClick.AddListener(OnButtonClicked);
    }

    public void Setup(SkillData data, Action<SkillData> callback)
    {
        skillData = data;
        iconImage.sprite = data.icon;
        onSkillSelectedCallback = callback;
    }

    private void OnButtonClicked()
    {
        onSkillSelectedCallback?.Invoke(skillData);
    }
}
