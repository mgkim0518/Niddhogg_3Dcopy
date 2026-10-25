using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 씬의 Canvas 왼쪽 아래 ChatPanel에 붙인다.
// Enter: 열기 / 보내고 닫기, Esc: 보내지 않고 닫기.
// 닫힘: 입력창만 흐리게 보이고, 새 메시지는 잠깐 떴다가 페이드아웃.
// 열림: 배경·입력창이 진해지고, 남아 있는 채팅이 모두 보인다.
// 경기가 끝난 뒤에도 그대로 쓸 수 있고, 연결이 끊겨도 로그는 남는다.
public class ChatUI : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] Image background;        // 패널의 배경 Image (돌 텍스처)
    [SerializeField] InputField inputField;   // CanvasGroup이 없으면 자동으로 붙인다
    [SerializeField] Text[] lineSlots;        // 위 → 아래 순서 (맨 아래가 가장 최근)
    [SerializeField] Color systemColor = new Color(0.95f, 0.8f, 0.3f);

    [Header("알파값")]
    [SerializeField] float bgClosedAlpha = 0f;
    [SerializeField] float bgOpenAlpha = 0.7f;
    [SerializeField] float inputClosedAlpha = 0f;   // 입력 중이 아닐 때는 완전히 숨김
    [SerializeField] float inputOpenAlpha = 1f;

    [Header("시간")]
    [SerializeField] float showTime = 5f;     // 닫혀 있을 때 새 메시지가 보이는 시간
    [SerializeField] float fadeTime = 1f;     // 그 뒤 사라지는 데 걸리는 시간
    [SerializeField] float switchTime = 0.15f; // 열 때 전환 속도 (빠르게 나타남)
    [SerializeField] float inputFadeOutTime = 0.4f; // 닫을 때 입력창이 사라지는 시간

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

    void Awake()
    {
        inputField.characterLimit = PlayerChat.MaxLength;
        inputField.lineType = InputField.LineType.MultiLineSubmit;   // 자동 줄바꿈, Enter는 전송

        inputGroup = GetOrAdd(inputField.gameObject);
        inputGroup.alpha = inputClosedAlpha;
        SetInputInteractable(false);

        // 리치 텍스트 색(<color>)은 Text.color 알파를 무시하므로, 줄마다 CanvasGroup으로 흐리게 한다
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

    void HandleKeys()
    {
        Keyboard kb = Keyboard.current;   // 프로젝트가 New Input System 전용이라 Input.GetKey 대신 사용
        if (kb == null) return;

        bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;

        if (!isOpen)
        {
            // 연결이 끊겨 내 캐릭터가 없으면 보낼 곳이 없으므로 열지 않는다
            if (enter && PlayerChat.Local != null) Open();
        }
        else if (enter)
        {
            Submit();
        }
        else if (kb.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    // ---------- 알파 ----------

    void UpdateAlpha()
    {
        float step = Time.unscaledDeltaTime / switchTime;

        SetBgAlpha(Mathf.MoveTowards(background.color.a, isOpen ? bgOpenAlpha : bgClosedAlpha, step));
        // 입력창: 메시지와 상관없이 "입력 중인가"로만 결정. 나타날 땐 빠르게, 사라질 땐 천천히
        float inputStep = Time.unscaledDeltaTime / (isOpen ? switchTime : inputFadeOutTime);
        inputGroup.alpha = Mathf.MoveTowards(inputGroup.alpha, isOpen ? inputOpenAlpha : inputClosedAlpha, inputStep);

        // 슬롯 i에 들어 있는 줄의 목표 알파
        int offset = lineSlots.Length - lines.Count;
        float now = Time.unscaledTime;
        for (int i = 0; i < lineSlots.Length; i++)
        {
            int idx = i - offset;
            if (idx < 0) continue;   // 빈 슬롯

            float target;
            if (isOpen)
            {
                target = 1f;
            }
            else
            {
                float age = now - lines[idx].time;
                target = 1f - Mathf.Clamp01((age - showTime) / fadeTime);   // showTime 동안 1, 이후 fadeTime 동안 0으로
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

    // ---------- 열기 / 닫기 ----------

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
        inputField.text = "";
        inputField.DeactivateInputField();
        SetInputInteractable(false);
        SetBlocked(false);
    }

    // 닫혀 있을 때는 흐리게 보이기만 하고 클릭·포커스는 받지 않는다
    void SetInputInteractable(bool on)
    {
        inputGroup.interactable = on;
        inputGroup.blocksRaycasts = on;
    }

    // 입력 중에는 공격·줍기(isInputBlocked)와 이동·시점·점프(액션 맵)를 모두 막는다
    void SetBlocked(bool blocked)
    {
        PlayerCombat.isInputBlocked = blocked;

        if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInputManager>();
        // 경기가 끝나 Core가 조작을 꺼둔 상태면 다시 켜지 않는다 (채팅만 계속 가능)
        if (playerInput != null && !PlayerRespawn.MatchOver)
            playerInput.SetGameplayEnabled(!blocked);
    }

    // ---------- 메시지 ----------

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
        if (lines.Count > lineSlots.Length) lines.RemoveAt(0);   // 가장 오래된 줄 버림

        // 가장 최근 줄을 맨 아래 칸에 두고, 나머지를 위로 채운다.
        // 줄이 한 칸씩 밀리므로 알파도 같이 밀어 줘야 깜빡이지 않는다.
        int offset = lineSlots.Length - lines.Count;
        for (int i = 0; i < lineSlots.Length; i++)
        {
            int idx = i - offset;
            bool has = idx >= 0;

            lineSlots[i].gameObject.SetActive(has);
            lineSlots[i].text = has ? lines[idx].text : "";

            if (!has) slotGroups[i].alpha = 0f;
            else if (i < lineSlots.Length - 1) slotGroups[i].alpha = slotGroups[i + 1].alpha;
            else slotGroups[i].alpha = 1f;   // 새 줄은 바로 보이게
        }
    }

    static CanvasGroup GetOrAdd(GameObject go)
    {
        CanvasGroup g = go.GetComponent<CanvasGroup>();
        return g != null ? g : go.AddComponent<CanvasGroup>();
    }
}
