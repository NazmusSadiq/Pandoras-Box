using TMPro;
using UnityEngine;

public class Hollow_Knight_GameManager : Base_GameManager
{
    public int lives = 3;

    public int enemy_Num = 3;        
    TextMeshProUGUI lifeCount = null;
    TextMeshProUGUI enemyCount = null;

    private void Start()
    {
        if (gameplayHUD != null)
        {
            Transform lifeCountObj = gameplayHUD.transform.Find("Life/LifeCount");
            Transform towerCountObj = gameplayHUD.transform.Find("Tower/TowerCount");

            if (lifeCountObj != null)
                lifeCount = lifeCountObj.GetComponent<TextMeshProUGUI>();

            if (towerCountObj != null)
                enemyCount = towerCountObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            Debug.LogWarning("gameplayHUD not assigned!");
        }

        UpdateLifeDisplay();
        UpdateEnemyDisplay();
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


    public void Destroy_Enemy()
    {
        enemy_Num--;
        UpdateEnemyDisplay();
    }

    private void UpdateEnemyDisplay()
    {
        if (enemyCount != null)
            enemyCount.SetText(enemy_Num.ToString());
        else
            Debug.LogWarning("towerCount is null when trying to update UI!");
    }

    public override bool CanProceed()
    {
        return enemy_Num <= 0;
    }
}
