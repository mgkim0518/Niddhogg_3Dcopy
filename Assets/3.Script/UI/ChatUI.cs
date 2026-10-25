using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 씬의 Canvas 왼쪽 아래 ChatPanel에 붙인다.
// Enter: 열기 / 보내고 닫기, Esc: 보내지 않고 닫기.
public class ChatUI : MonoBehaviour
{
    [SerializeField] InputField inputField;
    [SerializeField] Text logText;
    [SerializeField] int maxLines = 8;

    readonly Queue<string> lines = new Queue<string>();
    PlayerInputManager playerInput;
    bool isOpen;

    void Awake()
    {
        inputField.characterLimit = PlayerChat.MaxLength;
        inputField.lineType = InputField.LineType.SingleLine;
        inputField.gameObject.SetActive(false);

        logText.supportRichText = true;
        logText.text = "";
    }

    void OnEnable() => PlayerChat.OnChatMessage += AddLine;
    void OnDisable() => PlayerChat.OnChatMessage -= AddLine;

    void Update()
    {
        Keyboard kb = Keyboard.current;   // 프로젝트가 New Input System 전용이라 Input.GetKey 대신 사용
        if (kb == null) return;

        bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;

        if (!isOpen)
        {
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

    void Open()
    {
        isOpen = true;
        inputField.gameObject.SetActive(true);
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
        inputField.DeactivateInputField();
        inputField.gameObject.SetActive(false);
        SetBlocked(false);
    }

    // 입력 중에는 공격·줍기(isInputBlocked)와 이동·시점·점프(액션 맵)를 모두 막는다
    void SetBlocked(bool blocked)
    {
        PlayerCombat.isInputBlocked = blocked;

        if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInputManager>();
        // 경기가 끝나 Core가 조작을 꺼둔 상태면 다시 켜지 않는다
        if (playerInput != null && !PlayerRespawn.MatchOver)
            playerInput.SetGameplayEnabled(!blocked);
    }

    void AddLine(Team team, string message)
    {
        string name = team == Team.Red ? "레드" : "블루";
        string hex = ColorUtility.ToHtmlStringRGB(TeamColors.Of(team));

        lines.Enqueue($"<color=#{hex}>{name}</color>: {message}");
        while (lines.Count > maxLines) lines.Dequeue();

        logText.text = string.Join("\n", lines);
    }
}
