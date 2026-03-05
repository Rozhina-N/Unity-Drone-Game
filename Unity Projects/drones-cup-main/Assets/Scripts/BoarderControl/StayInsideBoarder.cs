using UnityEngine;

public class StayInsideBoarder : MonoBehaviour
{
    private BoarderControler boarderControler;

    void Start()
    {
        boarderControler = Object.FindFirstObjectByType<BoarderControler>();
        if (boarderControler == null)
        {
            Debug.LogError("No BoarderControler found in the scene.");
        }
    }

    void Update()
    {
        if (boarderControler == null) return;

        if (!boarderControler.IsInsideBounds(transform.position))
        {
            transform.position = boarderControler.ClampPositionInsideBounds(transform.position);
        }
    }
}

