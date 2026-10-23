using UnityEngine;
using UnityEngine.UI;

public class CoreHealthBar : MonoBehaviour
{
    [SerializeField] Slider slider;
    [SerializeField] Image fill;
    [Range(0f, 1f)] [SerializeField] float lowHpRatio = 0.25f;

    Core core;
    Camera cam;

    void Awake()
    {
        core = GetComponentInParent<Core>();
    }
    
    void OnEnable()
    {
        Core.OnCoreHpChanged += HandleHpChanged;
        if (core != null) SetHp(core.HP);
    }

    void OnDisable()
    {
        Core.OnCoreHpChanged -= HandleHpChanged;
    }

    void Start()
    {
        slider.maxValue = DamageTable.CoreMaxHp;
        if (core != null && fill != null) fill.color = TeamColors.Of(core.CoreTeam);
        if (core != null) SetHp(core.HP);
    }

    void HandleHpChanged(Team team, int hp)
    {
        if (core == null || team != core.CoreTeam) return; 
        SetHp(hp);
    }

    void SetHp(int hp)
    {
        if (slider == null) return;
        slider.SetValueWithoutNotify(hp);
        if (fill != null) fill.enabled = hp > 0;
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

        if (slider != null && fill != null)
        {
            float ratio = slider.normalizedValue;
            Color c = fill.color;
            c.a = (ratio > 0f && ratio <= lowHpRatio)
                ? Mathf.Lerp(0.4f, 1f, Mathf.PingPong(Time.time * 3f, 1f))
                : 1f;
            fill.color = c;
        }
    }
}
