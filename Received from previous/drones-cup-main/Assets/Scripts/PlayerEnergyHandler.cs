using UnityEngine;
using UnityEngine.UI;

public class PlayerEnergyHandler : MonoBehaviour
{
    public float maxEnergy = 100f;
    public float currentEnergy;
    public Image energyBarFill;
    void Start()
    {
        UpdateEnergyBar(); 
    }
    
	public void AddEnergy(int amount)
	{
		currentEnergy = Mathf.Min(currentEnergy + amount, maxEnergy);
		UpdateEnergyBar();
	}    

    public void TakeDamage(float amount)
    {
        currentEnergy -= amount;

        // Makes sure player health cant go below 0 or above max health
        currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);
        
        UpdateEnergyBar();
        if (currentEnergy <= 0)
        {
            Die(); 
        }
    }
    
    public bool TryShootEnergy(float cost)
    {
        if (currentEnergy >= cost)
        {
            Debug.Log(gameObject.name);
            currentEnergy -= cost;
            UpdateEnergyBar();
            return true; 
        }
        else
        {
            return false; 
        }
    }
    
    private void UpdateEnergyBar()
    {
        energyBarFill.fillAmount = currentEnergy / maxEnergy;
    }
    
    private void Die()
    {
        Debug.Log(gameObject.name + " is defeated.");
    }
}
