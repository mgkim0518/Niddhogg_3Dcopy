using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ChatUI : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] Image background;       
    [SerializeField] InputField inputField;   
    [SerializeField] Text[] lineSlots;       
    [SerializeField] Color systemColor = new Color(0.95f, 0.8f, 0.3f);

    [Header("알파값")]
    [SerializeField] float bgClosedAlpha = 0f;
    [SerializeField] float bgOpenAlpha = 0.7f;
    [SerializeField] float inputClosedAlpha = 0.3f;   
    [SerializeField] float inputOpenAlpha = 1f;

    [Header("시간")]
    [SerializeField] float showTime = 5f;     
    [SerializeField] float fadeTime = 1f;     
    [SerializeField] float switchTime = 0.15f; 
    [SerializeField] float inputFadeOutTime = 0.4f; 

    struct Entry
    {
        public string text;
        public float time;   // 도착 시각
    }

    readonly List<Entry> lines = new List<Entry>();
    CanvasGroup inputGroup;
    CanvasGroup[] slotGroups;
    PlayerInputManager playerInput;
    bool isOpen;

    const int SubmitDelayFrames = 2; 
    int submitFrame = -1;
    bool isRefocusing;

    void Awake()
    {
        inputField.characterLimit = PlayerChat.MaxLength;
        inputField.lineType = InputField.LineType.MultiLineNewline;
        inputField.onValidateInput = (text, index, c) => (c == '\n' || c == '\r') ? '\0' : c;

        inputGroup = GetOrAdd(inputField.gameObject);
        inputGroup.alpha = inputClosedAlpha;
        SetInputInteractable(false);

        slotGroups = new CanvasGroup[lineSlots.Length];
        for (int i = 0; i < lineSlots.Length; i++)
        {
            lineSlots[i].supportRichText = true;
            lineSlots[i].text = "";
            lineSlots[i].gameObject.SetActive(false);
            slotGroups[i] = GetOrAdd(lineSlots[i].gameObject);
            slotGroups[i].alpha = 0f;
            slotGroups[i].blocksRaycasts = false;
        }

        SetBgAlpha(bgClosedAlpha);
    }

    void OnEnable()
    {
        PlayerChat.OnChatMessage += AddChatLine;
        PlayerChat.OnSystemMessage += AddSystemLine;
    }

    void OnDisable()
    {
        PlayerChat.OnChatMessage -= AddChatLine;
        PlayerChat.OnSystemMessage -= AddSystemLine;
    }

    void Update()
    {
        HandleKeys();
        UpdateAlpha();
    }

    void KeepFocus()
    {
        if (!isOpen || submitFrame >= 0) return;

        if (!inputField.isFocused)
        {
            if (!isRefocusing)
            {
                inputField.ActivateInputField();
                isRefocusing = true;
            }
        }
        else if (isRefocusing)
        {
            inputField.MoveTextEnd(false);
            isRefocusing = false;
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && isOpen) isRefocusing = false;
    }

    void HandleKeys()
    {
        Keyboard kb = Keyboard.current; 
        if (kb == null) return;

        if (submitFrame >= 0)
        {
            if (Time.frameCount >= submitFrame) Submit();
            return;
        }

        bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;

        if (!isOpen)
        {
            if (enter && PlayerChat.Local != null) Open();
        }
        else if (enter)
        {
            submitFrame = Time.frameCount + SubmitDelayFrames;
        }
        else if (kb.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    void UpdateAlpha()
    {
        float step = Time.unscaledDeltaTime / switchTime;

        SetBgAlpha(Mathf.MoveTowards(background.color.a, isOpen ? bgOpenAlpha : bgClosedAlpha, step));
        float inputStep = Time.unscaledDeltaTime / (isOpen ? switchTime : inputFadeOutTime);
        inputGroup.alpha = Mathf.MoveTowards(inputGroup.alpha, isOpen ? inputOpenAlpha : inputClosedAlpha, inputStep);

        int offset = lineSlots.Length - lines.Count;
        float now = Time.unscaledTime;
        for (int i = 0; i < lineSlots.Length; i++)
        {
            int idx = i - offset;
            if (idx < 0) continue;  

            float target;
            if (isOpen)
            {
                target = 1f;
            }
            else
            {
                float age = now - lines[idx].time;
                target = 1f - Mathf.Clamp01((age - showTime) / fadeTime);
            }

            slotGroups[i].alpha = Mathf.MoveTowards(slotGroups[i].alpha, target, step);
        }
    }

    void SetBgAlpha(float a)
    {
        Color c = background.color;
        c.a = a;
        background.color = c;
    }

    void Open()
    {
        isOpen = true;
        SetInputInteractable(true);
        inputField.text = "";
        inputField.ActivateInputField();
        SetBlocked(true);
    }

    void Submit()
    {
        string message = inputField.text;
        Close();
        if (PlayerChat.Local != null) PlayerChat.Local.Send(message);
    }

    void Close()
    {
        isOpen = false;
        submitFrame = -1;
        inputField.text = "";
        inputField.DeactivateInputField();
        SetInputInteractable(false);
        SetBlocked(false);
    }

    void SetInputInteractable(bool on)
    {
        inputGroup.interactable = on;
        inputGroup.blocksRaycasts = on;
    }

    void SetBlocked(bool blocked)
    {
        PlayerCombat.isInputBlocked = blocked;

        if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInputManager>();
        if (playerInput != null && !PlayerRespawn.MatchOver)
            playerInput.SetGameplayEnabled(!blocked);
    }

    void AddChatLine(Team team, string message)
    {
        string hex = ColorUtility.ToHtmlStringRGB(TeamColors.Of(team));
        AddLine($"<color=#{hex}>{PlayerChat.NameOf(team)}</color>: {message}");
    }

    void AddSystemLine(string message)
    {
        string hex = ColorUtility.ToHtmlStringRGB(systemColor);
        AddLine($"<color=#{hex}>{message}</color>");
    }

    void AddLine(string line)
    {
        lines.Add(new Entry { text = line, time = Time.unscaledTime });
        if (lines.Count > lineSlots.Length) lines.RemoveAt(0);   

        int offset = lineSlots.Length - lines.Count;
        for (int i = 0; i < lineSlots.Length; i++)
        {
            int idx = i - offset;
            bool has = idx >= 0;

            lineSlots[i].gameObject.SetActive(has);
            lineSlots[i].text = has ? lines[idx].text : "";

            if (!has) slotGroups[i].alpha = 0f;
            else if (i < lineSlots.Length - 1) slotGroups[i].alpha = slotGroups[i + 1].alpha;
            else slotGroups[i].alpha = 1f;  
        }
    }

    static CanvasGroup GetOrAdd(GameObject go)
    {
        CanvasGroup g = go.GetComponent<CanvasGroup>();
        return g != null ? g : go.AddComponent<CanvasGroup>();
    }
}
