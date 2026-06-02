using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum ScoreDisplayMode
{
    RawScore,
    Percentage
}

[DisallowMultipleComponent]
public class ScoreManager : MonoBehaviour
{
    [Header("Score Settings")]
    [SerializeField] [Min(0)] [InspectorName("Starting Score / Max Score")] private int maxScore = 1000;
    [SerializeField] [Min(0)] private int burntTreePenalty = 10;
    [SerializeField] [Min(0)] private int burntPersonPenalty = 100;
    [SerializeField] private ScoreDisplayMode displayMode = ScoreDisplayMode.RawScore;
    [SerializeField] private bool clampScoreToZero = true;

    [Header("UI")]
    [SerializeField] private Text scoreText;

    [Header("Forest Saved Progress Bar")]
    [SerializeField] private bool showForestSavedProgressBar;
    [SerializeField] private bool autoCreateProgressBarIfMissing = true;
    [SerializeField] private bool showProgressBarLabel = true;
    [SerializeField] private GameObject progressBarRoot;
    [SerializeField] private Slider forestSavedSlider;
    [SerializeField] private Image forestSavedFillImage;
    [SerializeField] private Text forestSavedProgressLabel;
    [SerializeField] private Vector2 progressBarSize = new Vector2(260f, 22f);
    [SerializeField] private Vector2 progressBarScreenOffset = new Vector2(20f, -58f);
    [SerializeField] private Vector2 progressBarOffsetFromScoreText = new Vector2(0f, -30f);
    [SerializeField] private Color progressBarBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color progressBarFillColor = new Color(0.2f, 0.75f, 0.35f, 1f);
    [SerializeField] private Color progressBarTextColor = Color.white;

    [Header("Debug")]
    [SerializeField] private bool logScoreChanges;

    private readonly HashSet<int> countedBurntTreeIds = new HashSet<int>();
    private readonly HashSet<int> countedLostPersonIds = new HashSet<int>();

    private int currentScore;
    private int burntTreesCount;
    private int lostPeopleCount;

    public int CurrentScore => currentScore;
    public int MaxScore => maxScore;
    public int BurntTreesCount => burntTreesCount;
    public int LostPeopleCount => lostPeopleCount;
    public float ForestSaved01 => GetForestSaved01();
    public int ForestSavedPercent => Mathf.RoundToInt(GetForestSaved01() * 100f);

    private void Awake()
    {
        ResetScore();
    }

    private void OnEnable()
    {
        FlammableTree.TreeBurnt += HandleTreeBurnt;
        RescueTarget.TargetLostToFire += HandleTargetLostToFire;
        UpdateUI();
    }

    private void OnDisable()
    {
        FlammableTree.TreeBurnt -= HandleTreeBurnt;
        RescueTarget.TargetLostToFire -= HandleTargetLostToFire;
    }

    [ContextMenu("Reset Score")]
    public void ResetScore()
    {
        currentScore = maxScore;
        burntTreesCount = 0;
        lostPeopleCount = 0;
        countedBurntTreeIds.Clear();
        countedLostPersonIds.Clear();
        UpdateUI();
    }

    [ContextMenu("Create Forest Saved Progress Bar UI")]
    public void CreateForestSavedProgressBarUI()
    {
        CreateProgressBarUI();
        UpdateUI();
    }

    private void HandleTreeBurnt(FlammableTree tree)
    {
        if (tree == null || !countedBurntTreeIds.Add(tree.GetInstanceID()))
        {
            return;
        }

        burntTreesCount++;
        ApplyPenalty(burntTreePenalty, "burnt tree");
    }

    private void HandleTargetLostToFire(RescueTarget target)
    {
        if (target == null || !countedLostPersonIds.Add(target.GetInstanceID()))
        {
            return;
        }

        lostPeopleCount++;
        ApplyPenalty(burntPersonPenalty, "lost person");
    }

    private void ApplyPenalty(int penalty, string reason)
    {
        int safePenalty = Mathf.Max(0, penalty);
        currentScore -= safePenalty;

        if (clampScoreToZero)
        {
            currentScore = Mathf.Max(0, currentScore);
        }

        UpdateUI();

        if (logScoreChanges)
        {
            Debug.Log($"Score penalty: {reason} (-{safePenalty}). Current score: {currentScore}", this);
        }
    }

    private void UpdateUI()
    {
        UpdateScoreText();
        UpdateProgressBar(Application.isPlaying);
    }

    private void UpdateScoreText()
    {
        if (scoreText == null)
        {
            return;
        }

        scoreText.text = GetScoreDisplayText();
    }

    private void UpdateProgressBar(bool allowCreate)
    {
        if (showForestSavedProgressBar && autoCreateProgressBarIfMissing && GetProgressBarRoot() == null && allowCreate)
        {
            CreateProgressBarUI();
        }

        GameObject resolvedProgressBarRoot = GetProgressBarRoot();
        if (resolvedProgressBarRoot != null)
        {
            resolvedProgressBarRoot.SetActive(showForestSavedProgressBar);
        }

        if (!showForestSavedProgressBar)
        {
            return;
        }

        float forestSaved = GetForestSaved01();
        int forestSavedPercent = ForestSavedPercent;

        if (forestSavedSlider != null)
        {
            forestSavedSlider.minValue = 0f;
            forestSavedSlider.maxValue = 1f;
            forestSavedSlider.interactable = false;
            forestSavedSlider.value = forestSaved;
        }

        if (forestSavedFillImage != null)
        {
            forestSavedFillImage.type = Image.Type.Filled;
            forestSavedFillImage.fillMethod = Image.FillMethod.Horizontal;
            forestSavedFillImage.fillOrigin = 0;
            forestSavedFillImage.color = progressBarFillColor;
            forestSavedFillImage.fillAmount = forestSaved;
        }

        if (forestSavedProgressLabel != null)
        {
            forestSavedProgressLabel.gameObject.SetActive(showProgressBarLabel);
            forestSavedProgressLabel.color = progressBarTextColor;
            forestSavedProgressLabel.text = $"Forest Saved: {forestSavedPercent}%";
        }

        if (progressBarRoot != null && progressBarRoot.TryGetComponent(out Image backgroundImage))
        {
            backgroundImage.color = progressBarBackgroundColor;
        }
    }

    private GameObject GetProgressBarRoot()
    {
        if (progressBarRoot != null)
        {
            return progressBarRoot;
        }

        if (forestSavedSlider != null)
        {
            return forestSavedSlider.gameObject;
        }

        if (forestSavedFillImage != null)
        {
            return forestSavedFillImage.gameObject;
        }

        return null;
    }

    private string GetScoreDisplayText()
    {
        if (displayMode == ScoreDisplayMode.Percentage)
        {
            return $"Forest Saved: {ForestSavedPercent}%";
        }

        return $"Score: {currentScore}";
    }

    private float GetForestSaved01()
    {
        if (maxScore <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)currentScore / maxScore);
    }

    private void CreateProgressBarUI()
    {
        if (GetProgressBarRoot() != null)
        {
            return;
        }

        Canvas canvas = ResolveCanvas();
        GameObject barRoot = new GameObject("Forest Saved Progress Bar");
        RectTransform rootRect = barRoot.AddComponent<RectTransform>();
        barRoot.transform.SetParent(canvas.transform, false);
        ConfigureProgressBarRect(rootRect);

        Image backgroundImage = barRoot.AddComponent<Image>();
        backgroundImage.color = progressBarBackgroundColor;

        GameObject fillObject = new GameObject("Fill");
        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillObject.transform.SetParent(barRoot.transform, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        forestSavedFillImage = fillObject.AddComponent<Image>();
        forestSavedFillImage.color = progressBarFillColor;
        forestSavedFillImage.type = Image.Type.Filled;
        forestSavedFillImage.fillMethod = Image.FillMethod.Horizontal;
        forestSavedFillImage.fillOrigin = 0;

        GameObject labelObject = new GameObject("Label");
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelObject.transform.SetParent(barRoot.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        forestSavedProgressLabel = labelObject.AddComponent<Text>();
        forestSavedProgressLabel.alignment = TextAnchor.MiddleCenter;
        forestSavedProgressLabel.color = progressBarTextColor;
        forestSavedProgressLabel.raycastTarget = false;
        forestSavedProgressLabel.resizeTextForBestFit = true;
        forestSavedProgressLabel.resizeTextMinSize = 9;
        forestSavedProgressLabel.resizeTextMaxSize = 16;
        forestSavedProgressLabel.font = GetDefaultFont();

        progressBarRoot = barRoot;
        progressBarRoot.SetActive(showForestSavedProgressBar);
    }

    private Canvas ResolveCanvas()
    {
        if (scoreText != null)
        {
            Canvas scoreCanvas = scoreText.GetComponentInParent<Canvas>();
            if (scoreCanvas != null)
            {
                return scoreCanvas;
            }
        }

        Canvas existingCanvas = FindFirstObjectByType<Canvas>();
        if (existingCanvas != null)
        {
            return existingCanvas;
        }

        GameObject canvasObject = new GameObject("Score UI Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private void ConfigureProgressBarRect(RectTransform rootRect)
    {
        rootRect.sizeDelta = progressBarSize;

        if (scoreText != null && scoreText.TryGetComponent(out RectTransform scoreTextRect))
        {
            rootRect.anchorMin = scoreTextRect.anchorMin;
            rootRect.anchorMax = scoreTextRect.anchorMax;
            rootRect.pivot = scoreTextRect.pivot;
            rootRect.anchoredPosition = scoreTextRect.anchoredPosition + progressBarOffsetFromScoreText;
            return;
        }

        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = progressBarScreenOffset;
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
        maxScore = Mathf.Max(0, maxScore);
        burntTreePenalty = Mathf.Max(0, burntTreePenalty);
        burntPersonPenalty = Mathf.Max(0, burntPersonPenalty);
        progressBarSize.x = Mathf.Max(1f, progressBarSize.x);
        progressBarSize.y = Mathf.Max(1f, progressBarSize.y);

        if (!Application.isPlaying)
        {
            currentScore = maxScore;
            burntTreesCount = 0;
            lostPeopleCount = 0;
        }

        UpdateScoreText();
        UpdateProgressBar(false);
    }
}
