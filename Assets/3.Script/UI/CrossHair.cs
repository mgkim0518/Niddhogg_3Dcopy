using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class CrossHair : MonoBehaviour
{
    [SerializeField] Image[] parts;                       
    [SerializeField] Color normalColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] Color targetColor = new Color(1f, 0.82f, 0.25f, 1f);
    [Tooltip("Player + Wall (벽 너머의 적은 색이 안 바뀌게)")]
    [SerializeField] LayerMask checkMask;
    [Tooltip("PlayerCombat의 thrustRadius와 같게")]
    [SerializeField] float checkRadius = 0.15f;
    [SerializeField] RectTransform[] lines;   // 위, 아래, 왼쪽, 오른쪽 순서
    [SerializeField] float normalGap = 11f;
    [SerializeField] float targetGap = 7f;

    static readonly Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    CanvasGroup group;
    Health myHealth;
    PlayerCombat myCombat;
    Transform cam;
    bool shownTarget;
    readonly RaycastHit[] hits = new RaycastHit[8];

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        SetColor(normalColor);
    }

    void Update()
    {
        if (myHealth == null && !FindLocalPlayer())
        {
            group.alpha = 0f;   // 접속 전, 로비
            return;
        }

        bool visible = !myHealth.isDead;
        group.alpha = visible ? 1f : 0f;
        if (!visible) return;

        bool target = EnemyInRange();
        if (target != shownTarget)
        {
            shownTarget = target;
            SetTarget(target);
        }
    }

    bool FindLocalPlayer()
    {
        if (NetworkClient.localPlayer == null || Camera.main == null) return false;
        myHealth = NetworkClient.localPlayer.GetComponent<Health>();
        myCombat = NetworkClient.localPlayer.GetComponent<PlayerCombat>();
        cam = Camera.main.transform;
        return myHealth != null && myCombat != null;
    }

    // 가장 가까이 맞은 것이 살아 있는 적이면 true
    bool EnemyInRange()
    {
        int count = Physics.SphereCastNonAlloc(cam.position, checkRadius, cam.forward, hits,
            myCombat.CurrentRange, checkMask, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        Health nearestHealth = null;
        bool nearestIsWall = false;

        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.transform.IsChildOf(myHealth.transform)) continue;   // 내 몸
            if (hits[i].distance >= nearest) continue;

            nearest = hits[i].distance;
            nearestHealth = hits[i].collider.GetComponentInParent<Health>();
            nearestIsWall = nearestHealth == null;
        }

        if (nearestIsWall || nearestHealth == null) return false;
        return nearestHealth.team != myHealth.team && !nearestHealth.isDead && !nearestHealth.invincible;
    }
    void SetTarget(bool target)
    {
        SetColor(target ? targetColor : normalColor);
        float gap = target ? targetGap : normalGap;
        for (int i = 0; i < lines.Length; i++)
            lines[i].anchoredPosition = dirs[i] * gap;
    }

    void SetColor(Color c)
    {
        foreach (Image img in parts) if (img) img.color = c;
    }
}
