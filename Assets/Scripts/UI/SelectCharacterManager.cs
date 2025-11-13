using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SelectCharacterManager : MonoBehaviour
{
    // 使用单例模式
    public static SelectCharacterManager Instance { get; private set; }

    // --- 在 Inspector 中拖拽 ---
    [Header("UI 引用")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private List<UnityEngine.UI.Image> characterImages; // 数量固定为5
    [SerializeField] private AudioClip errorSound;
    [SerializeField] public List<GameObject> characterPrefabs;
    [SerializeField] public List<bool> characterLockStates;

    void Awake()
    {
        // -----------------------------------
        // 设置单例
        // -----------------------------------
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // （可选）如果你的UIManager需要跨场景，请取消注释下一行
        // DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        SetCharacterImages();

        curSelectedIdx = 0;
        rightNxtIdx = 2;
        leftPrevIdx = characterPrefabs.Count - 2;
    }

    private bool isPressed = false;
    private bool isLongPressed = false;
    private float longPressDuration = 1f;
    private float firstPressedTime = 0;
    void Update()
    {
        if (!rootPanel.activeSelf) return;
        
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                Debug.Log("fhhtest, leftArrowKey pressed");
                if (!isPressed)
                {
                    isPressed = true;
                    firstPressedTime = Time.time;
                }
                ToPrevCharacter();
            }
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                Debug.Log("fhhtest, rightArrowKey pressed");
                if (!isPressed)
                {
                    isPressed = true;
                    firstPressedTime = Time.time;
                }
                ToNextCharacter();
            }

            if (isPressed && Time.time - firstPressedTime > longPressDuration)
            {
                isLongPressed = true;
            }

            if (Keyboard.current.leftArrowKey.wasReleasedThisFrame)
            {
                isPressed = false;
                isLongPressed = false;
            }
            else if (Keyboard.current.rightArrowKey.wasReleasedThisFrame)
            {
                isPressed = false;
                isLongPressed = false;
            }

            if (Keyboard.current.leftArrowKey.IsPressed() && isLongPressed)
            {
                ToPrevCharacter();
            }
            else if (Keyboard.current.rightArrowKey.IsPressed() && isLongPressed)
            {
                ToNextCharacter();
            }

            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                Debug.Log("fhhtest, enterKey pressed");
                int selectedPrefabId = (leftPrevIdx + 2) % characterPrefabs.Count;
                if (characterLockStates[selectedPrefabId])
                {
                    var audioSrc = gameObject.AddComponent<AudioSource>();
                    audioSrc.PlayOneShot(errorSound);
                    Destroy(audioSrc, errorSound.length);
                    UIManager.Instance.ShowInfoPanel("This character is locked ", Color.yellow, 3f);
                }
                else
                {
                    CharacterManager.Instance.MyInfo.PrefabId = selectedPrefabId;
                    enterPressedCallback?.Invoke();
                    Hide();
                }
            }
        }
    }

    void ToNextCharacter()
    {
        AnimatioToNextCharacter();
    }

    void ToPrevCharacter()
    {
        AnimatioToPrevCharacter();
    }

    void SetCharacterImages()
    {
        for (int i = 0; i < Math.Min(characterPrefabs.Count, 3); i++)
        {
            var prefabIdx = (i) % characterPrefabs.Count;
            var figure = characterPrefabs[prefabIdx].GetComponent<CharacterStatus>().characterData.figure;
            characterImages[i].sprite = figure;
            SetLockState(i, characterLockStates[prefabIdx]);
        }

        for (int i = 1; i <= 2 && characterPrefabs.Count - i >= 3; i++)
        {
            var prefabIdx = (characterPrefabs.Count - i) % characterPrefabs.Count;
            var figure = characterPrefabs[prefabIdx].GetComponent<CharacterStatus>().characterData.figure;
            characterImages[^i].sprite = figure;
            SetLockState(characterImages.Count - i, characterLockStates[prefabIdx]);
        }
    }

    void SetLockState(int index, bool isLocked)
    {
        var lockImage = characterImages[index].rectTransform.GetChild(0);
        lockImage.gameObject.SetActive(isLocked);
    }

    private bool isPlayingAnimation = false;
    private int rightNxtIdx = 2;
    private int leftPrevIdx = 0;
    private int curSelectedIdx = 0;
    void AnimatioToNextCharacter(float duration = 0.1f)
    {
        if (isPlayingAnimation) return;
        isPlayingAnimation = true;

        rightNxtIdx = (rightNxtIdx + 1) % characterPrefabs.Count;
        leftPrevIdx = (leftPrevIdx + 1) % characterPrefabs.Count;

        int leftestIdx = (curSelectedIdx - 2 + 5) % 5;
        characterImages[leftestIdx].sprite = characterPrefabs[rightNxtIdx].GetComponent<CharacterStatus>().characterData.figure;
        SetLockState(leftestIdx, characterLockStates[rightNxtIdx]);
        curSelectedIdx = (curSelectedIdx + 1) % 5;
        characterImages[curSelectedIdx].rectTransform.SetAsLastSibling();

        var lastPosX = characterImages[^1].rectTransform.anchoredPosition.x;
        var lastPosY = characterImages[^1].rectTransform.anchoredPosition.y;
        var lastWidth = characterImages[^1].rectTransform.rect.width;
        var lastHeight = characterImages[^1].rectTransform.rect.height;
        for (int i = 4; i >= 0; i--)
        {
            float nextPosX, nextPosY, nextWidth, nextHeight;
            if (i == 0)
            {
                nextPosX = lastPosX;
                nextPosY = lastPosY;
                nextWidth = lastWidth;
                nextHeight = lastHeight;
            }
            else
            {
                nextPosX = characterImages[i - 1].rectTransform.anchoredPosition.x;
                nextPosY = characterImages[i - 1].rectTransform.anchoredPosition.y;
                nextWidth = characterImages[i - 1].rectTransform.rect.width;
                nextHeight = characterImages[i - 1].rectTransform.rect.height;
            }

            StartCoroutine(MoveToTargetRect(duration, characterImages[i], nextPosX, nextPosY, nextWidth, nextHeight, i == 0));
        }
    }

    void AnimatioToPrevCharacter(float duration = 0.1f)
    {
        if (isPlayingAnimation) return;
        isPlayingAnimation = true;

        rightNxtIdx = (rightNxtIdx - 1 + characterPrefabs.Count) % characterPrefabs.Count;
        leftPrevIdx = (leftPrevIdx - 1 + characterPrefabs.Count) % characterPrefabs.Count;

        int rightestIdx = (curSelectedIdx + 2) % 5;
        characterImages[rightestIdx].sprite = characterPrefabs[leftPrevIdx].GetComponent<CharacterStatus>().characterData.figure;
        SetLockState(rightestIdx, characterLockStates[leftPrevIdx]);
        curSelectedIdx = (curSelectedIdx - 1 + 5) % 5;
        characterImages[curSelectedIdx].rectTransform.SetAsLastSibling();

        var lastPosX = characterImages[0].rectTransform.anchoredPosition.x;
        var lastPosY = characterImages[0].rectTransform.anchoredPosition.y;
        var lastWidth = characterImages[0].rectTransform.rect.width;
        var lastHeight = characterImages[0].rectTransform.rect.height;
        for (int i = 0; i < 5; i++)
        {
            float nextPosX, nextPosY, nextWidth, nextHeight;
            if (i == 4)
            {
                nextPosX = lastPosX;
                nextPosY = lastPosY;
                nextWidth = lastWidth;
                nextHeight = lastHeight;
            }
            else
            {
                nextPosX = characterImages[i + 1].rectTransform.anchoredPosition.x;
                nextPosY = characterImages[i + 1].rectTransform.anchoredPosition.y;
                nextWidth = characterImages[i + 1].rectTransform.rect.width;
                nextHeight = characterImages[i + 1].rectTransform.rect.height;
            }

            StartCoroutine(MoveToTargetRect(duration, characterImages[i], nextPosX, nextPosY, nextWidth, nextHeight, i == 4));
        }
    }

    IEnumerator MoveToTargetRect(float duration, UnityEngine.UI.Image image, float targetPosX, float targetPosY, float targetWidth, float targetHeight, bool isLast)
    {
        float elapsedTime = 0;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            image.rectTransform.anchoredPosition = new Vector2(
                Mathf.Lerp(image.rectTransform.anchoredPosition.x, targetPosX, elapsedTime / duration),
                Mathf.Lerp(image.rectTransform.anchoredPosition.y, targetPosY, elapsedTime / duration));
            image.rectTransform.sizeDelta = new Vector2(
                Mathf.Lerp(image.rectTransform.sizeDelta.x, targetWidth, elapsedTime / duration),
                Mathf.Lerp(image.rectTransform.sizeDelta.y, targetHeight, elapsedTime / duration));
            yield return null;
        }

        image.rectTransform.anchoredPosition = new Vector2(targetPosX, targetPosY);
        image.rectTransform.sizeDelta = new Vector2(targetWidth, targetHeight);
        var lockImage = image.rectTransform.GetChild(0) as RectTransform;
        lockImage.sizeDelta = new Vector2(targetWidth / 2, targetHeight / 2);

        if (isLast) isPlayingAnimation = false;
    }

    private Action enterPressedCallback = null;
    public void RegisterEnterButtonPressed(Action callback)
    {
        enterPressedCallback = callback;
    }

    public void Show()
    {
        enabled = true;

        int leftestIdx = (curSelectedIdx - 2 + 5) % 5;
        int leftPrevPrefabIdx = leftPrevIdx;
        for (int i = 0; i < 5; ++i)
        {
            SetLockState(leftestIdx, characterLockStates[leftPrevPrefabIdx]);
            leftestIdx = (leftestIdx + 1) % 5;
            leftPrevPrefabIdx = (leftPrevPrefabIdx + 1) % characterPrefabs.Count;
        }

        rootPanel.SetActive(true);
    }
    
    public void Hide()
    {
        rootPanel.SetActive(false);
        enabled = false;
    }
}