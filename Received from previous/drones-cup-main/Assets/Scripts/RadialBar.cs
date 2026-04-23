using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class RadialBar : MonoBehaviour
{
    public float MaxValue;

    public float currentValue;

    void Start()
    {
        
    }

    /// <summary>
    /// Changes the value of the radialbar with float amount
    /// </summary>
    void ChangeValue(float amount)
    {
        currentValue += amount;
        UpdateValue();
    }

    /// <summary>
    /// Sets the value of the radialbar to float amount
    /// </summary>
    void SetValue(float value)
    {
        currentValue = value;
        UpdateValue();
    }

    void UpdateValue()
    {
        float radialValue = currentValue / MaxValue;
        this.gameObject.GetComponent<UnityEngine.UI.Image>().fillAmount = radialValue;
    }

    void Update()
    {
    }
}
