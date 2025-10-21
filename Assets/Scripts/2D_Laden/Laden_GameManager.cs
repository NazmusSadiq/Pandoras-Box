using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class Laden_GameManager : Base_GameManager
{
    public int lives = 3;

    public int tower_Num = 3;        
    TextMeshProUGUI lifeCount = null;
    TextMeshProUGUI towerCount = null;

    private void Start()
    {
        if (gameplayHUD != null)
        {
            Transform lifeCountObj = gameplayHUD.transform.Find("Life/LifeCount");
            Transform towerCountObj = gameplayHUD.transform.Find("Tower/TowerCount");

            if (lifeCountObj != null)
                lifeCount = lifeCountObj.GetComponent<TextMeshProUGUI>();

            if (towerCountObj != null)
                towerCount = towerCountObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            Debug.LogWarning("gameplayHUD not assigned!");
        }

        UpdateLifeDisplay();
        UpdateTowerDisplay();
    }


    public void Reduce_Lives()
    {
        lives--;
        UpdateLifeDisplay();

        if (lives <= 0)
        {
            Game_Over();
        }
    }

    private void UpdateLifeDisplay()
    {
        if (lifeCount != null)
            lifeCount.SetText(lives.ToString());
    }


    public void Destroy_Tower()
    {
        tower_Num--;
        UpdateTowerDisplay();
    }

    private void UpdateTowerDisplay()
    {
        if (towerCount != null)
            towerCount.SetText(tower_Num.ToString());
        else
            Debug.LogWarning("towerCount is null when trying to update UI!");
    }

    public override bool CanProceed()
    {
        return tower_Num <= 0;
    }
}
