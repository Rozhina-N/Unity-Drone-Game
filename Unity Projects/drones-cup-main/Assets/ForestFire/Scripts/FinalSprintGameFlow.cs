using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FinalSprintGameFlow : MonoBehaviour
{
    private enum GameFlowState
    {
        WaitingToStart,
        Running,
        Ended
    }

    [Header("Game Rules")]
    [SerializeField] [Min(1f)] private float gameDurationSeconds = 120f;
    [SerializeField] [Min(0f)] private float completionCheckDelay = 0.25f;
    [SerializeField] private bool pauseTimeBeforeStart = true;
    [SerializeField] private bool pauseTimeOnEnd = true;

    [Header("Text")]
    [SerializeField] private string startTitleText = "Final Sprint";
    [SerializeField] private string startSubtitleText = "Ready?";
    [SerializeField] private string startButtonText = "Start";
    [SerializeField] private string endTitleText = "Game Over";
    [SerializeField] private string restartButtonText = "Restart";
    [SerializeField] private string endButtonText = "End";
    [SerializeField] private string timeUpEndReasonText = "Time is up";
    [SerializeField] private string completedEndReasonText = "All fires are out and all available people are dropped off";
    [SerializeField] private string earlyEndReasonText = "Game ended early";
    [SerializeField] private string finalScoreFormat = "Final Score: {0} / {1}";
    [SerializeField] private string finalScoreUnavailableText = "Final Score: N/A";
    [SerializeField] private string timerFormat = "{0:00}:{1:00}";

    [Header("Optional References")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private Canvas gameFlowCanvas;
    [SerializeField] private GameObject startScreenRoot;
    [SerializeField] private GameObject endScreenRoot;
    [SerializeField] private GameObject runningHudRoot;
    [SerializeField] private Button startButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button endButton;
    [SerializeField] private Text timerText;
    [SerializeField] private Text endReasonText;
    [SerializeField] private Text finalScoreText;

    [Header("Generated UI")]
    [SerializeField] private bool autoCreateUiIfMissing = true;
    [SerializeField] private Color screenOverlayColor = new Color(0f, 0f, 0f, 0.78f);
    [SerializeField] private Color panelTextColor = Color.white;
    [SerializeField] private Color buttonColor = new Color(0.18f, 0.58f, 0.33f, 1f);
    [SerializeField] private Color endButtonColor = new Color(0.55f, 0.12f, 0.1f, 1f);
    [SerializeField] private Color buttonTextColor = Color.white;

    private GameFlowState state;
    private float elapsedSeconds;
    private bool isQuitting;

    private void Awake()
    {
        ResolveReferences();
        state = GameFlowState.WaitingToStart;
        elapsedSeconds = 0f;

        if (pauseTimeBeforeStart)
        {
            Time.timeScale = 0f;
        }
    }

    private void Start()
    {
        EnsureUi();
        ApplyConfiguredText();
        WireButtons();
        ShowStartScreen();
    }

    private void Update()
    {
        if (state != GameFlowState.Running)
        {
            return;
        }

        elapsedSeconds += Time.deltaTime;
        UpdateTimerText();

        if (elapsedSeconds >= gameDurationSeconds)
        {
            EndGame(timeUpEndReasonText);
            return;
        }

        if (elapsedSeconds >= completionCheckDelay && AreAllFiresOut() && AreAllAvailablePeopleDroppedOff())
        {
            EndGame(completedEndReasonText);
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDisable()
    {
        if (!isQuitting)
        {
            Time.timeScale = 1f;
        }
    }

    public void StartGame()
    {
        if (state == GameFlowState.Running)
        {
            return;
        }

        state = GameFlowState.Running;
        elapsedSeconds = 0f;
        Time.timeScale = 1f;

        SetActive(startScreenRoot, false);
        SetActive(endScreenRoot, false);
        SetActive(runningHudRoot, true);
        SetEndButtonInteractable(true);
        UpdateTimerText();
    }

    public void EndGameEarly()
    {
        if (state == GameFlowState.Ended)
        {
            return;
        }

        EndGame(earlyEndReasonText);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
            return;
        }

        SceneManager.LoadScene(activeScene.name);
    }

    private void EndGame(string reason)
    {
        if (state == GameFlowState.Ended)
        {
            return;
        }

        state = GameFlowState.Ended;

        if (pauseTimeOnEnd)
        {
            Time.timeScale = 0f;
        }

        SetActive(startScreenRoot, false);
        SetActive(runningHudRoot, false);
        SetActive(endScreenRoot, true);
        SetEndButtonInteractable(false);
        UpdateEndScreen(reason);
    }

    private void ShowStartScreen()
    {
        state = GameFlowState.WaitingToStart;
        elapsedSeconds = 0f;

        if (pauseTimeBeforeStart)
        {
            Time.timeScale = 0f;
        }

        SetActive(startScreenRoot, true);
        SetActive(endScreenRoot, false);
        SetActive(runningHudRoot, false);
        SetEndButtonInteractable(true);
        UpdateTimerText();
    }

    private bool AreAllFiresOut()
    {
        var allTrees = FlammableTree.RegisteredTrees;
        for (int i = 0; i < allTrees.Count; i++)
        {
            FlammableTree tree = allTrees[i];
            if (tree != null && tree.isActiveAndEnabled && (tree.IsBurning || tree.IsExtinguishing))
            {
                return false;
            }
        }

        return true;
    }

    private bool AreAllAvailablePeopleDroppedOff()
    {
        if (HasPeopleCarriedByDrones())
        {
            return false;
        }

        RescueTarget[] rescueTargets = FindObjectsByType<RescueTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rescueTargets.Length; i++)
        {
            RescueTarget target = rescueTargets[i];
            if (target != null && target.CanBeRescued)
            {
                return false;
            }
        }

        return true;
    }

    private bool HasPeopleCarriedByDrones()
    {
        FirefighterInteraction[] firefighters = FindObjectsByType<FirefighterInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < firefighters.Length; i++)
        {
            FirefighterInteraction firefighter = firefighters[i];
            if (firefighter != null && firefighter.CarriedRescueCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (scoreManager == null)
        {
            scoreManager = FindFirstObjectByType<ScoreManager>();
        }
    }

    private void EnsureUi()
    {
        if (!autoCreateUiIfMissing)
        {
            return;
        }

        if (gameFlowCanvas == null)
        {
            gameFlowCanvas = CreateCanvas();
        }

        if (startScreenRoot == null)
        {
            startScreenRoot = CreateStartScreen(gameFlowCanvas.transform);
        }

        if (endScreenRoot == null)
        {
            endScreenRoot = CreateEndScreen(gameFlowCanvas.transform);
        }

        if (runningHudRoot == null)
        {
            runningHudRoot = CreateRunningHud(gameFlowCanvas.transform);
        }

        if (endButton == null)
        {
            endButton = CreateEndButton(gameFlowCanvas.transform);
        }
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Final Sprint Game Flow Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private GameObject CreateStartScreen(Transform parent)
    {
        GameObject screen = CreateFullScreenRoot("Start Screen", parent, true);
        CreateText("Title", screen.transform, startTitleText, 64, FontStyle.Bold, new Vector2(0f, 105f), new Vector2(720f, 90f));
        CreateText("Subtitle", screen.transform, startSubtitleText, 34, FontStyle.Normal, new Vector2(0f, 25f), new Vector2(360f, 55f));
        startButton = CreateButton("Start Button", screen.transform, startButtonText, buttonColor, new Vector2(0f, -75f), new Vector2(230f, 64f));
        return screen;
    }

    private GameObject CreateEndScreen(Transform parent)
    {
        GameObject screen = CreateFullScreenRoot("End Screen", parent, true);
        CreateText("Title", screen.transform, endTitleText, 64, FontStyle.Bold, new Vector2(0f, 130f), new Vector2(720f, 90f));
        endReasonText = CreateText("End Reason", screen.transform, string.Empty, 28, FontStyle.Normal, new Vector2(0f, 55f), new Vector2(900f, 50f));
        finalScoreText = CreateText("Final Score", screen.transform, string.Empty, 38, FontStyle.Bold, new Vector2(0f, -5f), new Vector2(520f, 62f));
        restartButton = CreateButton("Restart Button", screen.transform, restartButtonText, buttonColor, new Vector2(0f, -105f), new Vector2(250f, 64f));
        return screen;
    }

    private GameObject CreateRunningHud(Transform parent)
    {
        GameObject root = new GameObject("Running HUD", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject timerObject = new GameObject("Timer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        timerObject.transform.SetParent(root.transform, false);

        RectTransform timerRect = timerObject.GetComponent<RectTransform>();
        timerRect.anchorMin = new Vector2(0.5f, 1f);
        timerRect.anchorMax = new Vector2(0.5f, 1f);
        timerRect.pivot = new Vector2(0.5f, 1f);
        timerRect.anchoredPosition = new Vector2(0f, -20f);
        timerRect.sizeDelta = new Vector2(260f, 52f);

        timerText = timerObject.GetComponent<Text>();
        timerText.alignment = TextAnchor.MiddleCenter;
        timerText.color = panelTextColor;
        timerText.font = GetDefaultFont();
        timerText.fontStyle = FontStyle.Bold;
        timerText.fontSize = 34;
        timerText.raycastTarget = false;

        return root;
    }

    private Button CreateEndButton(Transform parent)
    {
        Button button = CreateButton("End Button", parent, endButtonText, endButtonColor, new Vector2(20f, 20f), new Vector2(130f, 48f));
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(20f, 20f);
        button.transform.SetAsLastSibling();
        return button;
    }

    private void ApplyConfiguredText()
    {
        ApplyNamedChildText(startScreenRoot, "Title", startTitleText);
        ApplyNamedChildText(startScreenRoot, "Subtitle", startSubtitleText);
        ApplyNamedChildText(endScreenRoot, "Title", endTitleText);
        ApplyButtonText(startButton, startButtonText);
        ApplyButtonText(restartButton, restartButtonText);
        ApplyButtonText(endButton, endButtonText);
    }

    private void ApplyNamedChildText(GameObject root, string childName, string text)
    {
        if (root == null)
        {
            return;
        }

        Transform child = root.transform.Find(childName);
        if (child != null && child.TryGetComponent(out Text label))
        {
            label.text = text;
        }
    }

    private void ApplyButtonText(Button button, string text)
    {
        if (button == null)
        {
            return;
        }

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = text;
        }
    }

    private GameObject CreateFullScreenRoot(string objectName, Transform parent, bool includeOverlay)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform));
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (includeOverlay)
        {
            Image image = root.AddComponent<Image>();
            image.color = screenOverlayColor;
        }

        return root;
    }

    private Text CreateText(string objectName, Transform parent, string text, int fontSize, FontStyle fontStyle, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = panelTextColor;
        label.font = GetDefaultFont();
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 12;
        label.resizeTextMaxSize = fontSize;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateButton(string objectName, Transform parent, string buttonLabel, Color backgroundColor, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        Text label = CreateText("Text", buttonObject.transform, buttonLabel, 28, FontStyle.Bold, Vector2.zero, size);
        label.color = buttonTextColor;
        label.raycastTarget = false;

        return button;
    }

    private void WireButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
            startButton.onClick.AddListener(StartGame);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartGame);
            restartButton.onClick.AddListener(RestartGame);
        }

        if (endButton != null)
        {
            endButton.onClick.RemoveListener(EndGameEarly);
            endButton.onClick.AddListener(EndGameEarly);
        }
    }

    private void UpdateTimerText()
    {
        if (timerText == null)
        {
            return;
        }

        float remainingSeconds = Mathf.Max(0f, gameDurationSeconds - elapsedSeconds);
        int wholeSeconds = Mathf.CeilToInt(remainingSeconds);
        int minutes = wholeSeconds / 60;
        int seconds = wholeSeconds % 60;
        timerText.text = FormatText(timerFormat, "{0:00}:{1:00}", minutes, seconds);
    }

    private void UpdateEndScreen(string reason)
    {
        ResolveReferences();

        if (endReasonText != null)
        {
            endReasonText.text = reason;
        }

        if (finalScoreText == null)
        {
            return;
        }

        if (scoreManager == null)
        {
            finalScoreText.text = finalScoreUnavailableText;
            return;
        }

        finalScoreText.text = FormatText(finalScoreFormat, "Final Score: {0} / {1}", scoreManager.CurrentScore, scoreManager.MaxScore);
    }

    private string FormatText(string format, string fallbackFormat, params object[] args)
    {
        string safeFormat = string.IsNullOrWhiteSpace(format) ? fallbackFormat : format;

        try
        {
            return string.Format(safeFormat, args);
        }
        catch (System.FormatException)
        {
            return string.Format(fallbackFormat, args);
        }
    }

    private void SetEndButtonInteractable(bool interactable)
    {
        if (endButton != null)
        {
            endButton.gameObject.SetActive(true);
            endButton.interactable = interactable;
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
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
        gameDurationSeconds = Mathf.Max(1f, gameDurationSeconds);
        completionCheckDelay = Mathf.Max(0f, completionCheckDelay);
    }
}
