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
    [SerializeField] InputField portInput;     // 비워 두면 KcpTransport에 설정된 포트(7777)
    [SerializeField] Button hostButton;
    [SerializeField] Button joinButton;
    [SerializeField] Button serverButton;      // 선택: 에디터에서 데디케이티드 서버 테스트용 (없어도 됨)
    [SerializeField] Text statusText;          // "서버와의 연결이 끊어졌습니다" 등

    [Header("결과 패널")]
    [SerializeField] GameObject resultPanel;
    [SerializeField] Text resultText;          // 승리! / 패배
    [SerializeField] Text winnerText;          // 레드 팀 승리
    [SerializeField] Button restartButton;     // 호스트, 또는 데디케이티드 서버에 접속한 클라이언트에게 보임
    [SerializeField] GameObject waitHostText;  // 호스트 모드의 클라이언트에게만 보임: "호스트가 재시작합니다"
    [SerializeField] Button quitButton;

    // 씬을 다시 로드하면 이 오브젝트도 새로 생기므로, 끊김 메시지는 static으로 넘겨받는다
    public static string pendingMessage;

    // 접속 패널에서 입력한 내 이름. static이라 재시작(씬 재로드) 뒤에도 남는다
    public static string LocalName = "";
    const string NameKey = "PlayerName";
    const string PortKey = "Port";

    void Awake()
    {
        nameInput.characterLimit = PlayerChat.MaxNameLength;
        portInput.contentType = InputField.ContentType.IntegerNumber;
        portInput.characterLimit = 5;
        hostButton.onClick.AddListener(OnClickHost);
        joinButton.onClick.AddListener(OnClickJoin);
        if (serverButton != null) serverButton.onClick.AddListener(OnClickServer);
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

        // 지난번에 쓴 포트, 없으면 트랜스포트의 기본 포트
        if (string.IsNullOrEmpty(portInput.text))
            portInput.text = PlayerPrefs.GetString(PortKey, CurrentPort());

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

    static string CurrentPort() =>
        Transport.active is PortTransport pt ? pt.Port.ToString() : "7777";

    // 입력한 포트를 트랜스포트에 적용. 호스트는 이 포트로 열고, 클라이언트는 이 포트로 접속한다
    bool ApplyPort()
    {
        string text = portInput.text.Trim();
        if (text.Length == 0) return true;   // 비어 있으면 기존 포트 그대로

        if (!ushort.TryParse(text, out ushort port) || port == 0)
        {
            statusText.text = "포트는 1~65535 사이의 숫자로 입력하세요";
            return false;
        }

        if (Transport.active is PortTransport pt) pt.Port = port;
        PlayerPrefs.SetString(PortKey, text);
        return true;
    }

    void OnClickHost()
    {
        if (!ApplyPort()) return;
        SaveName();
        NetworkManager.singleton.StartHost();
        HideConnectPanel();
    }

    void OnClickJoin()
    {
        if (!ApplyPort()) return;
        SaveName();
        string address = addressInput.text.Trim();
        NetworkManager.singleton.networkAddress = string.IsNullOrEmpty(address) ? "localhost" : address;
        NetworkManager.singleton.StartClient();
        HideConnectPanel();
        // 접속에 실패하면 CustomNetworkManager.OnClientDisconnect가 씬을 다시 로드해 패널이 돌아온다
    }

    void OnClickServer()
    {
        if (!ApplyPort()) return;
        NetworkManager.singleton.StartServer();
        connectPanel.SetActive(false);   // 서버 화면은 관전용. 커서는 풀어 둔다
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

        // 상대가 호스트면 재시작은 호스트가 결정. 그 외(내가 호스트 / 데디케이티드 서버)는 버튼을 보여준다
        bool waitForHost = !NetworkServer.active && PlayerChat.OpponentIsHost;
        restartButton.gameObject.SetActive(!waitForHost);
        restartButton.interactable = true;
        waitHostText.SetActive(waitForHost);

        resultPanel.SetActive(true);
        SetCursor(true);
    }

    void OnClickRestart()
    {
        // 서버에 요청만 보낸다. 호스트면 바로, 데디케이티드 서버면 전원이 눌렀을 때 씬을 다시 로드한다
        if (!NetworkClient.isConnected) return;
        NetworkClient.Send(new RestartRequestMessage());
        restartButton.interactable = false;   // 두 번 누르지 않게
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
