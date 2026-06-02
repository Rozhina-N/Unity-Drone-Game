using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WaterCapacityBar : MonoBehaviour
{
    private const string DefaultCanvasName = "WaterCapacityCanvas";
    private const string BackgroundName = "Background";
    private const string FillName = "Fill";
    private const string EmptyTextName = "EmptyText";

    [Header("References")]
    [SerializeField] private FirefighterInteraction firefighterInteraction;
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private Text emptyText;

    [Header("Layout")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 4.35f, 0f);
    [SerializeField] private Vector2 barSize = new Vector2(160f, 20f);
    [SerializeField] [Min(0.0001f)] private float worldScale = 0.01f;

    [Header("Colors")]
    [SerializeField] private Color normalFillColor = new Color(0.1f, 0.55f, 1f, 1f);
    [SerializeField] private Color lowFillColor = new Color(0.95f, 0.75f, 0.15f, 1f);
    [SerializeField] private Color emptyFillColor = new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color emptyTextColor = Color.white;

    [Header("Display")]
    [SerializeField] private bool hideWhenFull;
    [SerializeField] private bool showEmptyText = true;
    [SerializeField] [Range(0f, 1f)] private float lowWaterThreshold = 0.25f;
    [SerializeField] [Min(1)] private int emptyTextFontSize = 28;
    [SerializeField] private bool autoCreateUiIfMissing = true;

    private Camera targetCamera;
    private RectTransform canvasRectTransform;
    private RectTransform backgroundRectTransform;
    private RectTransform fillRectTransform;

    private void Awake()
    {
        ResolveReferences();
        EnsureUi();
        UpdateBar();
    }

    private void Reset()
    {
        firefighterInteraction = GetComponent<FirefighterInteraction>();
    }

    private void Update()
    {
        if (firefighterInteraction == null)
        {
            firefighterInteraction = GetComponent<FirefighterInteraction>();
        }

        UpdateBar();
    }

    private void LateUpdate()
    {
        if (worldCanvas == null)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        Transform canvasTransform = worldCanvas.transform;
        canvasTransform.position = transform.position + positionOffset;
        canvasTransform.localScale = Vector3.one * worldScale;

        if (targetCamera != null)
        {
            canvasTransform.rotation = targetCamera.transform.rotation;
            worldCanvas.worldCamera = targetCamera;
        }
    }

    private void ResolveReferences()
    {
        if (firefighterInteraction == null)
        {
            firefighterInteraction = GetComponent<FirefighterInteraction>();
        }

        if (worldCanvas == null)
        {
            Transform existingCanvas = transform.Find(DefaultCanvasName);
            if (existingCanvas != null)
            {
                worldCanvas = existingCanvas.GetComponent<Canvas>();
            }
        }

        if (worldCanvas != null)
        {
            canvasRectTransform = worldCanvas.GetComponent<RectTransform>();
        }

        targetCamera = Camera.main;
    }

    private void EnsureUi()
    {
        if (!autoCreateUiIfMissing && worldCanvas == null)
        {
            return;
        }

        if (worldCanvas == null)
        {
            worldCanvas = CreateWorldCanvas();
        }

        if (worldCanvas == null)
        {
            return;
        }

        canvasRectTransform = worldCanvas.GetComponent<RectTransform>();
        ConfigureCanvasRect();

        if (backgroundImage == null)
        {
            backgroundImage = FindOrCreateImage(BackgroundName, worldCanvas.transform);
        }

        if (backgroundImage != null)
        {
            backgroundRectTransform = backgroundImage.rectTransform;
            ConfigureBackgroundRect();
        }

        if (fillImage == null)
        {
            Transform fillParent = backgroundImage != null ? backgroundImage.transform : worldCanvas.transform;
            fillImage = FindOrCreateImage(FillName, fillParent);
        }

        if (fillImage != null)
        {
            fillRectTransform = fillImage.rectTransform;
            ConfigureFillRect(0f);
        }

        if (showEmptyText && emptyText == null)
        {
            Transform textParent = backgroundImage != null ? backgroundImage.transform : worldCanvas.transform;
            emptyText = FindOrCreateEmptyText(textParent);
        }

        ConfigureEmptyText();
        ConfigureColors(0f);
    }

    private Canvas CreateWorldCanvas()
    {
        GameObject canvasObject = new GameObject(DefaultCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.layer = gameObject.layer;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 21;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 10f;

        return canvas;
    }

    private Image FindOrCreateImage(string objectName, Transform parent)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null && existing.TryGetComponent(out Image existingImage))
        {
            existingImage.raycastTarget = false;
            return existingImage;
        }

        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        imageObject.layer = parent.gameObject.layer;

        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private Text FindOrCreateEmptyText(Transform parent)
    {
        Transform existing = parent.Find(EmptyTextName);
        if (existing != null && existing.TryGetComponent(out Text existingText))
        {
            existingText.raycastTarget = false;
            return existingText;
        }

        GameObject textObject = new GameObject(EmptyTextName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        textObject.layer = parent.gameObject.layer;

        Text text = textObject.GetComponent<Text>();
        text.text = "EMPTY";
        return text;
    }

    private void ConfigureCanvasRect()
    {
        if (canvasRectTransform == null)
        {
            return;
        }

        canvasRectTransform.sizeDelta = barSize;
        canvasRectTransform.pivot = new Vector2(0.5f, 0.5f);
        canvasRectTransform.localScale = Vector3.one * worldScale;
    }

    private void ConfigureBackgroundRect()
    {
        if (backgroundRectTransform == null)
        {
            return;
        }

        backgroundRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRectTransform.pivot = new Vector2(0.5f, 0.5f);
        backgroundRectTransform.anchoredPosition = Vector2.zero;
        backgroundRectTransform.sizeDelta = barSize;
    }

    private void ConfigureFillRect(float fillAmount)
    {
        if (fillRectTransform == null)
        {
            return;
        }

        fillRectTransform.anchorMin = Vector2.zero;
        fillRectTransform.anchorMax = new Vector2(Mathf.Clamp01(fillAmount), 1f);
        fillRectTransform.pivot = new Vector2(0f, 0.5f);
        fillRectTransform.offsetMin = Vector2.zero;
        fillRectTransform.offsetMax = Vector2.zero;
    }

    private void UpdateBar()
    {
        bool hasWaterSource = firefighterInteraction != null && firefighterInteraction.UsesWaterCapacity;
        float fillAmount = hasWaterSource ? firefighterInteraction.WaterFillAmount : 0f;
        bool isEmpty = hasWaterSource && firefighterInteraction.IsWaterEmpty;
        bool shouldShow = hasWaterSource && (!hideWhenFull || fillAmount < 1f);

        if (worldCanvas != null)
        {
            worldCanvas.enabled = shouldShow;
        }

        ConfigureFillRect(fillAmount);
        ConfigureColors(fillAmount);

        if (emptyText != null)
        {
            emptyText.text = "EMPTY";
            ConfigureEmptyText();
            emptyText.gameObject.SetActive(shouldShow && showEmptyText && isEmpty);
        }
    }

    private void ConfigureColors(float fillAmount)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }

        if (fillImage == null)
        {
            return;
        }

        if (fillAmount <= 0f)
        {
            fillImage.color = emptyFillColor;
            return;
        }

        fillImage.color = fillAmount <= lowWaterThreshold ? lowFillColor : normalFillColor;
    }

    private void ConfigureEmptyText()
    {
        if (emptyText == null)
        {
            return;
        }

        emptyText.text = "EMPTY";
        emptyText.alignment = TextAnchor.MiddleCenter;
        emptyText.font = emptyText.font != null ? emptyText.font : GetDefaultFont();
        emptyText.fontStyle = FontStyle.Bold;
        emptyText.fontSize = emptyTextFontSize;
        emptyText.resizeTextForBestFit = true;
        emptyText.resizeTextMinSize = Mathf.Min(8, emptyTextFontSize);
        emptyText.resizeTextMaxSize = emptyTextFontSize;
        emptyText.raycastTarget = false;
        emptyText.color = emptyTextColor;

        RectTransform textRect = emptyText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void OnValidate()
    {
        barSize.x = Mathf.Max(1f, barSize.x);
        barSize.y = Mathf.Max(1f, barSize.y);
        worldScale = Mathf.Max(0.0001f, worldScale);
        emptyTextFontSize = Mathf.Max(1, emptyTextFontSize);
    }
}
