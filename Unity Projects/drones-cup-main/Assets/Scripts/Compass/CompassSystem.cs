using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CompassSystem : MonoBehaviour
{
    private RawImage compassImage;
    private RectTransform compassMarkerContainer;
    private GameObject markerPrefab;

    private List<CompassMarker> markers = new List<CompassMarker>();

    public float compassWidth = 500f; // width of the compass bar (UI)

    private void Awake()
    {
        CreateCompassUI();
    }

    private void Update()
    {
        UpdateCompass();
        UpdateMarkers();
    }

    void CreateCompassUI()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("CompassCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create Compass Background
        GameObject compassBackgroundObj = new GameObject("CompassBackground");
        compassBackgroundObj.transform.SetParent(canvasObj.transform);
        compassImage = compassBackgroundObj.AddComponent<RawImage>();
        compassImage.color = Color.grey; // Grey background if no texture yet

        RectTransform compassRect = compassBackgroundObj.GetComponent<RectTransform>();
        compassRect.anchorMin = new Vector2(0.5f, 1f);
        compassRect.anchorMax = new Vector2(0.5f, 1f);
        compassRect.pivot = new Vector2(0.5f, 1f);
        compassRect.sizeDelta = new Vector2(compassWidth, 50f);
        compassRect.anchoredPosition = new Vector2(0f, -50f);

        // Create Compass Marker Container
        GameObject markerContainerObj = new GameObject("CompassMarkerContainer");
        markerContainerObj.transform.SetParent(compassBackgroundObj.transform);
        compassMarkerContainer = markerContainerObj.AddComponent<RectTransform>();
        compassMarkerContainer.anchorMin = new Vector2(0f, 0f);
        compassMarkerContainer.anchorMax = new Vector2(1f, 1f);
        compassMarkerContainer.offsetMin = Vector2.zero;
        compassMarkerContainer.offsetMax = Vector2.zero;

        // Create Marker Prefab
        markerPrefab = new GameObject("CompassMarker");
        Image markerImage = markerPrefab.AddComponent<Image>();
        markerImage.color = Color.red;
        RectTransform markerRect = markerPrefab.GetComponent<RectTransform>();
        markerRect.sizeDelta = new Vector2(10f, 10f);

        // Hide original prefab (only clones shown)
        markerPrefab.SetActive(false);
    }

    void UpdateCompass()
    {
        compassImage.uvRect = new Rect(transform.eulerAngles.y / 360f, 0f, 1f, 1f);
    }

    void UpdateMarkers()
    {
        foreach (CompassMarker marker in markers)
        {
            if (marker == null) continue;

            Vector3 dir = marker.transform.position - transform.position;
            float angle = Vector3.SignedAngle(transform.forward, dir, Vector3.up);

            RectTransform markerRect = marker.uiMarker.GetComponent<RectTransform>();
            markerRect.anchoredPosition = new Vector2((angle / 90f) * (compassWidth / 2f), 0f);
        }
    }

    public void RegisterMarker(CompassMarker marker)
    {
        GameObject newMarker = Instantiate(markerPrefab, compassMarkerContainer);
        newMarker.SetActive(true);
        marker.uiMarker = newMarker;
        markers.Add(marker);
    }

    public void UnregisterMarker(CompassMarker marker)
    {
        if (marker.uiMarker != null)
        {
            Destroy(marker.uiMarker);
        }
        markers.Remove(marker);
    }
}
