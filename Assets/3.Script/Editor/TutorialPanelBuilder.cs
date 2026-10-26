#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 메뉴 [단칼 > 로비 튜토리얼 패널 만들기]를 누르면 열려 있는 씬에 로비 UI를 만든다.
//   LobbyCanvas
//   ├─ TutorialPanel   조작법 + 상태 문구 (레디 화면에서만 보임)
//   └─ CountdownText   3·2·1 (카운트다운 동안 화면 가운데)
// 씬에 LobbyDirector가 있으면 tutorialPanel / statusText / countdownText를 자동으로 연결한다.
// 만든 뒤에는 일반 UI처럼 인스펙터에서 자유롭게 고치면 된다.
public static class TutorialPanelBuilder
{
    const string FontPath = "Assets/9.Font/public/static/Pretendard-Medium.otf";
    const string BoldPath = "Assets/9.Font/public/static/Pretendard-SemiBold.otf";

    // 씬에 이미 쓰고 있는 색과 맞춤
    static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.62f);
    static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.15f);
    static readonly Color TitleColor = new Color(1f, 0.957f, 0.839f);     // 크림
    static readonly Color HeaderColor = new Color(1f, 0.698f, 0.22f);     // 주황
    static readonly Color KeyColor = new Color(1f, 1f, 1f, 0.14f);
    static readonly Color DescColor = new Color(1f, 1f, 1f, 0.88f);
    static readonly Color SubColor = new Color(1f, 1f, 1f, 0.55f);
    static readonly Color ReadyColor = new Color(0.45f, 0.9f, 0.5f);      // 체크 표시와 같은 녹색 계열

    static readonly string[,] MoveKeys =
    {
        { "WASD", "이동" },
        { "Space", "점프" },
        { "Shift", "누르는 동안 달리기" },
        { "마우스", "시점 회전" },
        { "E", "아이템 줍기 · 성문 버튼" },
        { "Enter", "채팅 켜기 · 끄기" },
    };

    static readonly string[,] CombatKeys =
    {
        { "좌클릭", "찌르기 (크로스라인 방향)" },
        { "우클릭", "베기" },
        { "휠 클릭", "들고 있는 무기 던지기" },
        { "Q", "막기 (방패를 들었을 때)" },
    };

    static Font font, bold;
    static Sprite rounded;

    [MenuItem("단칼/로비 튜토리얼 패널 만들기")]
    static void Build()
    {
        font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bold = AssetDatabase.LoadAssetAtPath<Font>(BoldPath);
        if (bold == null) bold = font;
        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        GameObject old = GameObject.Find("LobbyCanvas");
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("로비 UI", "LobbyCanvas가 이미 있습니다. 지우고 새로 만들까요?", "새로 만들기", "취소"))
                return;
            Undo.DestroyObjectImmediate(old);
        }

        // ---------- Canvas ----------
        GameObject canvasGo = new GameObject("LobbyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(canvasGo, "로비 튜토리얼 패널 만들기");
        canvasGo.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;   // HUD 위에 그린다

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // ---------- 패널 ----------
        GameObject panel = NewUI("TutorialPanel", canvasGo.transform);
        RectTransform prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1040, 0);
        prt.anchoredPosition = new Vector2(0, 20);

        Image bg = panel.AddComponent<Image>();
        bg.sprite = rounded;
        bg.type = Image.Type.Sliced;
        bg.color = PanelColor;
        bg.raycastTarget = false;

        VerticalLayoutGroup pv = panel.AddComponent<VerticalLayoutGroup>();
        pv.padding = new RectOffset(60, 60, 44, 40);
        pv.spacing = 22;
        pv.childAlignment = TextAnchor.UpperCenter;
        pv.childControlWidth = pv.childControlHeight = true;
        pv.childForceExpandWidth = true;
        pv.childForceExpandHeight = false;

        ContentSizeFitter fit = panel.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 제목
        NewText("Title", panel.transform, "조작법", bold, 44, TitleColor, TextAnchor.MiddleCenter);
        NewText("Goal", panel.transform, "상대 성 2층의 코어를 먼저 부수는 쪽이 승리", font, 24, SubColor, TextAnchor.MiddleCenter);
        Divider(panel.transform);

        // 두 열
        GameObject columns = NewUI("Columns", panel.transform);
        HorizontalLayoutGroup ch = columns.AddComponent<HorizontalLayoutGroup>();
        ch.spacing = 60;
        ch.childAlignment = TextAnchor.UpperCenter;
        ch.childControlWidth = ch.childControlHeight = true;
        ch.childForceExpandWidth = true;
        ch.childForceExpandHeight = false;

        Column(columns.transform, "MoveColumn", "이동 · 상호작용", MoveKeys);
        Column(columns.transform, "CombatColumn", "전투", CombatKeys);

        Divider(panel.transform);

        NewText("Rules", panel.transform,
            "칼은 한 방, 주먹과 던진 방패는 다섯 방  ·  죽어도 코어가 있으면 3초 뒤 부활",
            font, 22, SubColor, TextAnchor.MiddleCenter);

        Text status = NewText("StatusText", panel.transform, "F를 눌러 준비", bold, 32, ReadyColor, TextAnchor.MiddleCenter);
        status.gameObject.AddComponent<LayoutElement>().minHeight = 48;

        // ---------- 카운트다운 숫자 ----------
        Text countdown = NewText("CountdownText", canvasGo.transform, "", bold, 220, Color.white, TextAnchor.MiddleCenter);
        RectTransform crt = countdown.rectTransform;
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(500, 320);
        crt.anchoredPosition = new Vector2(0, 80);
        Outline outline = countdown.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(4, -4);

        // ---------- LobbyDirector 연결 ----------
        LobbyDirector director = Object.FindFirstObjectByType<LobbyDirector>();
        string wired = "씬에 LobbyDirector가 없어 연결은 건너뛰었습니다.";
        if (director != null)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("tutorialPanel").objectReferenceValue = panel;
            so.FindProperty("statusText").objectReferenceValue = status;
            SerializedProperty cd = so.FindProperty("countdownText");
            if (cd != null) cd.objectReferenceValue = countdown;
            so.ApplyModifiedProperties();
            wired = "LobbyDirector에 패널·상태 문구·카운트다운을 연결했습니다.";
        }

        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        Selection.activeGameObject = panel;
        Debug.Log($"[로비 UI] LobbyCanvas를 만들었습니다. {wired}");
    }

    // ---------- 만드는 함수들 ----------

    static void Column(Transform parent, string name, string header, string[,] keys)
    {
        GameObject col = NewUI(name, parent);
        VerticalLayoutGroup v = col.AddComponent<VerticalLayoutGroup>();
        v.spacing = 14;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        Text h = NewText("Header", col.transform, header, bold, 26, HeaderColor, TextAnchor.MiddleLeft);
        h.gameObject.AddComponent<LayoutElement>().minHeight = 40;

        for (int i = 0; i < keys.GetLength(0); i++)
            Row(col.transform, keys[i, 0], keys[i, 1]);
    }

    static void Row(Transform parent, string key, string desc)
    {
        GameObject row = NewUI("Row_" + key, parent);
        HorizontalLayoutGroup hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 20;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;

        // 키 모양 상자
        GameObject cap = NewUI("Key", row.transform);
        Image capImg = cap.AddComponent<Image>();
        capImg.sprite = rounded;
        capImg.type = Image.Type.Sliced;
        capImg.color = KeyColor;
        capImg.raycastTarget = false;
        LayoutElement capLe = cap.AddComponent<LayoutElement>();
        capLe.minWidth = capLe.preferredWidth = 128;
        capLe.minHeight = capLe.preferredHeight = 48;

        Text capText = NewText("Label", cap.transform, key, bold, 22, Color.white, TextAnchor.MiddleCenter);
        RectTransform t = capText.rectTransform;
        t.anchorMin = Vector2.zero;
        t.anchorMax = Vector2.one;
        t.offsetMin = t.offsetMax = Vector2.zero;

        // 설명
        Text d = NewText("Desc", row.transform, desc, font, 24, DescColor, TextAnchor.MiddleLeft);
        d.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }

    static void Divider(Transform parent)
    {
        GameObject line = NewUI("Divider", parent);
        Image img = line.AddComponent<Image>();
        img.color = DividerColor;
        img.raycastTarget = false;
        LayoutElement le = line.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 2;
    }

    static Text NewText(string name, Transform parent, string content, Font f, int size, Color color, TextAnchor align)
    {
        GameObject go = NewUI(name, parent);
        Text t = go.AddComponent<Text>();
        t.font = f;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.text = content;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }
}
#endif
