using UnityEngine;
using Mirror;

// Player 프리팹에 붙인다. Health.team에 따라 캐릭터 색을 바꾸고,
// 1인칭에서는 내 몸이 안 보이므로 화면 왼쪽 위에 내 팀을 표시한다.
[RequireComponent(typeof(Health))]
public class PlayerTeamColor : NetworkBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");   // URP Lit
    static readonly int ColorId = Shader.PropertyToID("_Color");           // Built-in Standard

    private Renderer[] targets;
    private Health health;
    private MaterialPropertyBlock block;

    void Awake()
    {
        health = GetComponent<Health>();
        targets = GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }

    public override void OnStartClient()
    {
        Apply(health.team);
    }

    void Apply(Team team)
    {
        if (team == Team.None) return;
        block ??= new MaterialPropertyBlock();
        Color c = TeamColors.Of(team);
        foreach (Renderer r in targets)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            r.SetPropertyBlock(block);
        }
    }

    // 금요일 UI가 생기기 전까지 쓰는 임시 표시
    void OnGUI()
    {
        if (!isLocalPlayer) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 24 };
        style.normal.textColor = TeamColors.Of(health.team);
        GUI.Label(new Rect(20, 20, 300, 40), $"{health.team} 팀", style);
    }
}
