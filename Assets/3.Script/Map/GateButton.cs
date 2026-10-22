using UnityEngine;

// Button_Outside, Button_Inside에 붙인다. NetworkIdentity는 붙이지 않는다.
// 콜라이더는 Interactable 레이어. 어느 문의 몇 번 버튼인지는 CastleGate가 Awake에서 알려준다.
public class GateButton : MonoBehaviour
{
    [SerializeField] Renderer buttonRenderer;
    [SerializeField] Color litColor = new Color(1f, 0.75f, 0.2f);
    [SerializeField] Color dimColor = new Color(0.3f, 0.3f, 0.3f);

    public CastleGate Gate { get; private set; }
    public int Index { get; private set; }

    MaterialPropertyBlock block;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");   // URP Lit
    static readonly int ColorId = Shader.PropertyToID("_Color");           // Built-in Standard

    public void Init(CastleGate gate, int index)
    {
        Gate = gate;
        Index = index;
    }

    // 눌러서 열 수 있는 상태면 밝게, 문이 열려 있는 동안은 어둡게
    public void SetLit(bool lit)
    {
        if (buttonRenderer == null) return;
        block ??= new MaterialPropertyBlock();
        Color c = lit ? litColor : dimColor;
        buttonRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        block.SetColor(ColorId, c);
        buttonRenderer.SetPropertyBlock(block);
    }
}
