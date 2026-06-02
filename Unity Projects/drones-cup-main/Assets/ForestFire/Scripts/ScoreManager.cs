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

    private void Awake()
    {
        ResetScore();
    }

    private void OnEnable()
    {
        FlammableTree.TreeBurnt += HandleTreeBurnt;
        RescueTarget.TargetLostToFire += HandleTargetLostToFire;
        UpdateScoreText();
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
        UpdateScoreText();
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

        UpdateScoreText();

        if (logScoreChanges)
        {
            Debug.Log($"Score penalty: {reason} (-{safePenalty}). Current score: {currentScore}", this);
        }
    }

    private void UpdateScoreText()
    {
        if (scoreText == null)
        {
            return;
        }

        scoreText.text = GetScoreDisplayText();
    }

    private string GetScoreDisplayText()
    {
        if (displayMode == ScoreDisplayMode.Percentage)
        {
            int savedPercent = maxScore <= 0 ? 0 : Mathf.RoundToInt((float)currentScore / maxScore * 100f);
            return $"Forest Saved: {savedPercent}%";
        }

        return $"Score: {currentScore}";
    }

    private void OnValidate()
    {
        maxScore = Mathf.Max(0, maxScore);
        burntTreePenalty = Mathf.Max(0, burntTreePenalty);
        burntPersonPenalty = Mathf.Max(0, burntPersonPenalty);

        if (!Application.isPlaying)
        {
            currentScore = maxScore;
            burntTreesCount = 0;
            lostPeopleCount = 0;
        }

        UpdateScoreText();
    }
}
