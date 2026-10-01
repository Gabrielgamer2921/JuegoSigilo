using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HudUI : MonoBehaviour
{
    private PlayerHiding hiding;
    private PlayerWhistle whistle;

    private Font font;

    private Text whistleText;
    private Image whistleFill;
    private Text promptText;
    private GameObject resultPanel;
    private Text resultTitle;
    private GameObject introPanel;
    private bool introActive;

    private void Start()
    {
        hiding = FindFirstObjectByType<PlayerHiding>();
        whistle = FindFirstObjectByType<PlayerWhistle>();

        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i].GetComponent<EnemyIndicator>() == null)
                enemies[i].gameObject.AddComponent<EnemyIndicator>();
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildCanvas();

        introActive = true;
        Time.timeScale = 0f;
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("HUD");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();

        RectTransform bottomRight = CreatePanel("PanelSilbido", root, new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(300f, 80f));
        whistleText = CreateText("Silbido", bottomRight, 28, TextAnchor.UpperRight, Color.white);
        SetRect(whistleText.rectTransform, new Vector2(0f, 0.45f), new Vector2(0.96f, 1f));
        Image whistleBack = CreateImage("SilbidoFondo", bottomRight, new Color(0f, 0f, 0f, 0.6f));
        SetRect(whistleBack.rectTransform, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.34f));
        whistleFill = CreateImage("SilbidoRelleno", whistleBack.rectTransform, new Color(1f, 0.95f, 0.5f));
        SetFill(whistleFill, 1f);

        promptText = CreateText("Aviso", root, 40, TextAnchor.MiddleCenter, Color.white);
        promptText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        promptText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        promptText.rectTransform.pivot = new Vector2(0.5f, 0f);
        promptText.rectTransform.anchoredPosition = new Vector2(0f, 190f);
        promptText.rectTransform.sizeDelta = new Vector2(900f, 70f);
        AddOutline(promptText);

        resultPanel = new GameObject("Resultado");
        resultPanel.transform.SetParent(root, false);
        Image resultBack = resultPanel.AddComponent<Image>();
        resultBack.color = new Color(0f, 0f, 0f, 0.7f);
        Stretch(resultBack.rectTransform);

        RectTransform resultRect = resultPanel.GetComponent<RectTransform>();
        resultTitle = CreateText("Titulo", resultRect, 110, TextAnchor.MiddleCenter, Color.white);
        SetRect(resultTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.75f));
        Text resultSub = CreateText("Subtitulo", resultRect, 44, TextAnchor.MiddleCenter, Color.white);
        SetRect(resultSub.rectTransform, new Vector2(0f, 0.35f), new Vector2(1f, 0.5f));
        resultSub.text = "Presioná R para reiniciar";
        resultPanel.SetActive(false);

        introPanel = new GameObject("Controles");
        introPanel.transform.SetParent(root, false);
        Image introBack = introPanel.AddComponent<Image>();
        introBack.color = new Color(0f, 0f, 0f, 0.8f);
        Stretch(introBack.rectTransform);

        RectTransform introRect = introPanel.GetComponent<RectTransform>();
        Text introTitle = CreateText("Titulo", introRect, 80, TextAnchor.MiddleCenter, Color.white);
        SetRect(introTitle.rectTransform, new Vector2(0f, 0.72f), new Vector2(1f, 0.88f));
        introTitle.text = "CONTROLES";

        Text keys = CreateText("Teclas", introRect, 46, TextAnchor.MiddleRight, new Color(1f, 0.95f, 0.5f));
        SetRect(keys.rectTransform, new Vector2(0f, 0.3f), new Vector2(0.46f, 0.7f));
        keys.text = "WASD\nShift\nC\nQ\nE\nMouse";
        keys.lineSpacing = 1.3f;

        Text actions = CreateText("Acciones", introRect, 46, TextAnchor.MiddleLeft, Color.white);
        SetRect(actions.rectTransform, new Vector2(0.54f, 0.3f), new Vector2(1f, 0.7f));
        actions.text = "Moverse\nCorrer (hace ruido)\nAgacharse (sigilo)\nSilbar para distraer\nEsconderse / salir\nMover la cámara";
        actions.lineSpacing = 1.3f;

        Text goal = CreateText("Objetivo", introRect, 36, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f));
        SetRect(goal.rectTransform, new Vector2(0f, 0.18f), new Vector2(1f, 0.28f));
        goal.text = "Llegá a la meta sin que te atrapen";

        Text start = CreateText("Empezar", introRect, 44, TextAnchor.MiddleCenter, new Color(0.4f, 1f, 0.5f));
        SetRect(start.rectTransform, new Vector2(0f, 0.06f), new Vector2(1f, 0.16f));
        start.text = "Presioná cualquier tecla para empezar";
    }

    private void Update()
    {
        if (whistleText == null) return;

        if (introActive)
        {
            bool pressed = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (pressed)
            {
                introActive = false;
                introPanel.SetActive(false);
                Time.timeScale = 1f;
            }
            return;
        }

        UpdateWhistle();
        UpdatePrompt();
        UpdateResult();
    }

    private void UpdateWhistle()
    {
        if (whistle == null) return;

        float remaining = whistle.CooldownRemaining;
        if (remaining > 0f)
        {
            whistleText.text = "SILBIDO  " + remaining.ToString("0.0") + "s";
            whistleText.color = new Color(1f, 1f, 1f, 0.6f);
            SetFill(whistleFill, 1f - remaining / Mathf.Max(0.01f, whistle.Cooldown));
        }
        else
        {
            whistleText.text = "[Q] SILBAR";
            whistleText.color = Color.white;
            SetFill(whistleFill, 1f);
        }
    }

    private void UpdatePrompt()
    {
        promptText.text = hiding != null ? hiding.PromptText : "";
    }

    private void UpdateResult()
    {
        GameManager gm = GameManager.Instance;
        bool ended = gm != null && gm.State != GameManager.GameState.Playing;

        if (resultPanel.activeSelf != ended) resultPanel.SetActive(ended);
        if (!ended) return;

        resultTitle.text = gm.ResultMessage;
        resultTitle.color = gm.State == GameManager.GameState.Won ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.3f, 0.3f);
    }

    private RectTransform CreatePanel(string name, RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Image image = CreateImage(name, parent, new Color(0f, 0f, 0f, 0.35f));
        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text CreateText(string name, RectTransform parent, int size, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private void AddOutline(Text text)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void SetFill(Image image, float amount)
    {
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
