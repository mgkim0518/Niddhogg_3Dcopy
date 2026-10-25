using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class CoreAlertUI : MonoBehaviour
{
    [SerializeField] Text text; 
    [SerializeField] string message = "코어가 공격받고 있습니다!";
    [Tooltip("Core의 alertInterval(3초)보다 길어야 공격이 이어지는 동안 끊기지 않는다")]
    [SerializeField] float showTime = 3.5f;
    [SerializeField] float fadeTime = 0.4f;
    [SerializeField] float blinkSpeed = 6f;

    CanvasGroup group;
    float hideAt = -1f;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        text.text = message;
    }

    void OnEnable()
    {
        Core.OnCoreAttacked += HandleCoreAttacked;
        Core.OnMatchEnded += HandleMatchEnded;
    }

    void OnDisable()
    {
        Core.OnCoreAttacked -= HandleCoreAttacked;
        Core.OnMatchEnded -= HandleMatchEnded;
    }

    void HandleCoreAttacked(Team attacked)
    {
        if (PlayerRespawn.MatchOver) return;
        if (attacked != MyTeam()) return;   

        hideAt = Time.time + showTime;
    }

    void HandleMatchEnded(Team winner)
    {
        hideAt = -1f;
        group.alpha = 0f;
    }

    static Team MyTeam()
    {
        if (NetworkClient.localPlayer == null) return Team.None;
        Health h = NetworkClient.localPlayer.GetComponent<Health>();
        return h != null ? h.team : Team.None;
    }

    void Update()
    {
        float left = hideAt - Time.time;
        if (left <= -fadeTime)
        {
            group.alpha = 0f;
            return;
        }

        float blink = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed));
        float fade = left > 0f ? 1f : 1f + left / fadeTime;
        group.alpha = blink * fade;
    }

}
