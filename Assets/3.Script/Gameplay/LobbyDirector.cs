using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// PlayScene에 빈 오브젝트로 둔다 (NetworkIdentity 필요 없음)
// 로비 구도 → 카운트다운 동안 1인칭 위치로 이동 → cameraPivot에 붙이고 조작 켜기
public class LobbyDirector : MonoBehaviour
{
    public static LobbyDirector Instance { get; private set; }

    [Header("팀별 로비 카메라 구도 (빈 오브젝트)")]
    [SerializeField] Transform blueShot;
    [SerializeField] Transform redShot;

    [Header("UI")]
    [SerializeField] GameObject tutorialPanel;   // 가운데 조작 설명
    [SerializeField] Text statusText;            // 패널 아래 안내 문구 (F를 눌러 준비 등)
    [SerializeField] Text countdownText;         // 화면 가운데 3·2·1 (비워 두면 statusText에 표시)
    [SerializeField] GameObject hudRoot;         // 플레이 중에만 보일 HUD (CrossHair 등)

    Transform cam, pivot, shot;
    PlayerInputManager input;
    Vector3 fromPos;
    Quaternion fromRot;
    bool blending;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }
    void OnEnable() => MatchFlow.OnPhaseChanged += HandlePhase;
    void OnDisable() => MatchFlow.OnPhaseChanged -= HandlePhase;

    // PlayerController.OnStartLocalPlayer에서 호출
    public void Setup(Team team, Transform cameraPivot, PlayerInputManager playerInput)
    {
        cam = Camera.main.transform;
        pivot = cameraPivot;
        input = playerInput;
        shot = team == Team.Red ? redShot : blueShot;

        if (MatchFlow.Instance != null && MatchFlow.Instance.phase == GamePhase.Playing)
            AttachCamera();
        else
            EnterLobby();
    }

    void EnterLobby()
    {
        blending = false;
        cam.SetParent(null);
        cam.SetPositionAndRotation(shot.position, shot.rotation);
        input.SetGameplayEnabled(false);
        SetCountdown("");
        tutorialPanel.SetActive(true);
        if (hudRoot) hudRoot.SetActive(false);
    }

    void HandlePhase(GamePhase p)
    {
        if (cam == null) return;

        if (p == GamePhase.Countdown)
        {
            tutorialPanel.SetActive(false);
            statusText.text = "";
            fromPos = cam.position;
            fromRot = cam.rotation;
            blending = true;
        }
        else if (p == GamePhase.Playing) AttachCamera();
        else EnterLobby();
    }

    void AttachCamera()
    {
        blending = false;
        cam.SetParent(pivot, false);
        cam.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        tutorialPanel.SetActive(false);
        if (hudRoot) hudRoot.SetActive(true);
        statusText.text = "";
        SetCountdown("");

        if (!PlayerRespawn.MatchOver && !PlayerCombat.isInputBlocked)
            input.SetGameplayEnabled(true);
    }

    void Update()
    {
        MatchFlow flow = MatchFlow.Instance;
        if (cam == null || flow == null) return;

        if (flow.phase == GamePhase.Lobby)
        {
            UpdateLobby();
            return;
        }

        if (!blending) return;

        float remain = flow.Remaining;
        SetCountdown(remain > 0f ? Mathf.CeilToInt(remain).ToString() : "");

        // 숫자가 바뀔 때마다 크게 나타났다가 줄어드는 효과
        if (countdownText)
        {
            float frac = remain - Mathf.Floor(remain);
            countdownText.transform.localScale = Vector3.one * (1f + 0.5f * frac * frac);
        }

        float t = Mathf.SmoothStep(0f, 1f, 1f - remain / MatchFlow.CountdownSeconds);
        cam.SetPositionAndRotation(
            Vector3.Lerp(fromPos, pivot.position, t),
            Quaternion.Slerp(fromRot, pivot.rotation, t));
    }

    void SetCountdown(string text)
    {
        if (countdownText) countdownText.text = text;
        else statusText.text = text;
    }

    void UpdateLobby()
    {
        PlayerReady me = PlayerReady.Local;
        if (me == null) return;

        if (PlayerReady.All.Count < 2) statusText.text = "상대를 기다리는 중";
        else if (me.isReady) statusText.text = "상대의 준비를 기다리는 중";
        else statusText.text = "F를 눌러 준비";

        // 채팅 입력 중(F 타이핑)에는 무시
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.fKey.wasPressedThisFrame && !PlayerCombat.isInputBlocked && !me.isReady)
            me.CmdSetReady();
    }
}
