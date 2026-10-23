using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InteractMessageUI : MonoBehaviour
{
    [SerializeField] Text text;
    [SerializeField] float showTime = 1.5f;
    [SerializeField] float fadeTime = 0.3f;

    CanvasGroup group;
    float hideAt;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    void OnEnable() => PlayerInteract.OnInteractMessage += Show;
    void OnDisable() => PlayerInteract.OnInteractMessage -= Show;

    void Show(string message)
    {
        text.text = message;   
        hideAt = Time.time + showTime;
        group.alpha = 1f;
    }

    void Update()
    {
        if (group.alpha <= 0f) return;

        float left = hideAt - Time.time;
        if (left > 0f) return;

        group.alpha = Mathf.Clamp01(1f + left / fadeTime);
    }
}
