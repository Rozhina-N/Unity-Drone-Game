using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RescueCapacityBar : MonoBehaviour
{
    private const string DefaultCanvasName = "RescueCapacityCanvas";
    private const string BackgroundName = "Background";
    private const string FillName = "Fill";
    private const string FullTextName = "FullText";

    [Header("References")]
    [SerializeField] private FirefighterInteraction firefighterInteraction;
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private Text fullText;

    [Header("Layout")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 4f, 0f);
    [SerializeField] private Vector2 barSize = new Vector2(160f, 20f);
    [SerializeField] [Min(0.0001f)] private float worldScale = 0.01f;

    [Header("Colors")]
    [SerializeField] private Color normalFillColor = new Color(0.25f, 0.75f, 1f, 1f);
    [SerializeField] private Color fullFillColor = new Color(1f, 0.85f, 0.15f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color fullTextColor = Color.white;

    [Header("Display")]
    [SerializeField] private bool hideWhenEmpty;
    [SerializeField] private bool showFullText = true;
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

        if (showFullText && fullText == null)
        {
            Transform textParent = backgroundImage != null ? backgroundImage.transform : worldCanvas.transform;
            fullText = FindOrCreateFullText(textParent);
        }

        ConfigureColors(false);
    }

    private Canvas CreateWorldCanvas()
    {
        GameObject canvasObject = new GameObject(DefaultCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.layer = gameObject.layer;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 20;

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

    private Text FindOrCreateFullText(Transform parent)
    {
        Transform existing = parent.Find(FullTextName);
        if (existing != null && existing.TryGetComponent(out Text existingText))
        {
            existingText.raycastTarget = false;
            return existingText;
        }

        GameObject textObject = new GameObject(FullTextName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        textObject.layer = parent.gameObject.layer;

        Text text = textObject.GetComponent<Text>();
        text.text = "FULL";
        text.alignment = TextAnchor.MiddleCenter;
        text.font = GetDefaultFont();
        text.fontStyle = FontStyle.Bold;
        text.fontSize = 14;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 8;
        text.resizeTextMaxSize = 16;
        text.raycastTarget = false;
        text.color = fullTextColor;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

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
        int carriedCount = firefighterInteraction != null ? firefighterInteraction.CarriedRescueCount : 0;
        int carryLimit = firefighterInteraction != null ? firefighterInteraction.RescueCarryLimit : 0;
        float fillAmount = carryLimit > 0 ? Mathf.Clamp01((float)carriedCount / carryLimit) : 0f;
        bool isFull = carryLimit > 0 && carriedCount >= carryLimit;
        bool shouldShow = firefighterInteraction != null && (!hideWhenEmpty || carriedCount > 0);

        if (worldCanvas != null)
        {
            worldCanvas.enabled = shouldShow;
        }

        ConfigureFillRect(fillAmount);
        ConfigureColors(isFull);

        if (fullText != null)
        {
            fullText.text = "FULL";
            fullText.color = fullTextColor;
            fullText.gameObject.SetActive(shouldShow && showFullText && isFull);
        }
    }

    private void ConfigureColors(bool isFull)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }

        if (fillImage != null)
        {
            fillImage.color = isFull ? fullFillColor : normalFillColor;
        }
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

        ConfigureCanvasRect();
        ConfigureBackgroundRect();
        UpdateBar();
    }
}
