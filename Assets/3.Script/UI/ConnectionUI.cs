using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 접속 패널(호스트/참가)과 결과 패널(재시작/종료).
// 접속 전에도 버튼이 동작해야 하므로 NetworkBehaviour가 아니라 MonoBehaviour로 둔다.
public class ConnectionUI : MonoBehaviour
{
    [Header("접속 패널")]
    [SerializeField] GameObject connectPanel;
    [SerializeField] InputField nameInput;
    [SerializeField] InputField addressInput;
    [SerializeField] Button hostButton;
    [SerializeField] Button joinButton;
    [SerializeField] Text statusText;          // "서버와의 연결이 끊어졌습니다" 등

    [Header("결과 패널")]
    [SerializeField] GameObject resultPanel;
    [SerializeField] Text resultText;          // 승리! / 패배
    [SerializeField] Text winnerText;          // 레드 팀 승리
    [SerializeField] Button restartButton;     // 호스트에게만 보임
    [SerializeField] GameObject waitHostText;  // 클라이언트에게만 보임: "호스트가 재시작합니다"
    [SerializeField] Button quitButton;

    // 씬을 다시 로드하면 이 오브젝트도 새로 생기므로, 끊김 메시지는 static으로 넘겨받는다
    public static string pendingMessage;

    // 접속 패널에서 입력한 내 이름. static이라 재시작(씬 재로드) 뒤에도 남는다
    public static string LocalName = "";
    const string NameKey = "PlayerName";

    void Awake()
    {
        nameInput.characterLimit = PlayerChat.MaxNameLength;
        hostButton.onClick.AddListener(OnClickHost);
        joinButton.onClick.AddListener(OnClickJoin);
        restartButton.onClick.AddListener(OnClickRestart);
        quitButton.onClick.AddListener(OnClickQuit);
    }

    void OnEnable() => Core.OnMatchEnded += ShowResult;
    void OnDisable() => Core.OnMatchEnded -= ShowResult;

    void Start()
    {
        // 재시작으로 씬이 다시 로드된 경우엔 이미 접속 중이므로 패널을 숨긴다
        bool connected = NetworkServer.active || NetworkClient.active;
        connectPanel.SetActive(!connected);
        resultPanel.SetActive(false);
        SetCursor(!connected);

        if (string.IsNullOrEmpty(addressInput.text))
            addressInput.text = NetworkManager.singleton.networkAddress;

        // 지난번에 쓴 이름을 미리 채워 둔다
        if (string.IsNullOrEmpty(nameInput.text))
            nameInput.text = PlayerPrefs.GetString(NameKey, "");

        statusText.text = pendingMessage ?? "";
        pendingMessage = null;
    }

    // ---------- 접속 ----------

    void SaveName()
    {
        LocalName = PlayerChat.CleanName(nameInput.text);
        PlayerPrefs.SetString(NameKey, LocalName);
    }

    void OnClickHost()
    {
        SaveName();
        NetworkManager.singleton.StartHost();
        HideConnectPanel();
    }

    void OnClickJoin()
    {
        SaveName();
        string address = addressInput.text.Trim();
        NetworkManager.singleton.networkAddress = string.IsNullOrEmpty(address) ? "localhost" : address;
        NetworkManager.singleton.StartClient();
        HideConnectPanel();
        // 접속에 실패하면 CustomNetworkManager.OnClientDisconnect가 씬을 다시 로드해 패널이 돌아온다
    }

    void HideConnectPanel()
    {
        connectPanel.SetActive(false);
        SetCursor(false);
    }

    // ---------- 결과 ----------

    void ShowResult(Team winner)
    {
        Team myTeam = Team.None;
        if (NetworkClient.localPlayer != null)
            myTeam = NetworkClient.localPlayer.GetComponent<Health>().team;

        bool iWon = myTeam == winner;
        resultText.text = myTeam == Team.None ? $"{PlayerChat.NameOf(winner)} 승리" : (iWon ? "승리!" : "패배");
        resultText.color = iWon ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.7f, 0.7f, 0.7f);
        winnerText.text = $"{PlayerChat.NameOf(winner)} 승리";
        winnerText.color = TeamColors.Of(winner);

        bool isHost = NetworkServer.active;
        restartButton.gameObject.SetActive(isHost);
        waitHostText.SetActive(!isHost);

        resultPanel.SetActive(true);
        SetCursor(true);
    }

    void OnClickRestart()
    {
        if (!NetworkServer.active) return;
        // 같은 씬을 다시 로드: 코어, 성문, 떨어진 칼이 전부 처음 상태로 돌아가고 플레이어도 다시 스폰된다
        NetworkManager.singleton.ServerChangeScene(SceneManager.GetActiveScene().name);
    }

    void OnClickQuit()
    {
        NetworkManager nm = NetworkManager.singleton;
        if (NetworkServer.active && NetworkClient.isConnected) nm.StopHost();
        else if (NetworkClient.active) nm.StopClient();
        else if (NetworkServer.active) nm.StopServer();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static void SetCursor(bool free)
    {
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }
}
