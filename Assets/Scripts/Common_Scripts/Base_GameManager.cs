using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class Base_GameManager : MonoBehaviour
{
    public GameObject gameplayHUD = null;
    public bool gameOver = false;
    public int lives = 3;

    public int enemy_Num = 5;
    protected TextMeshProUGUI lifeCount = null;
    protected TextMeshProUGUI enemyCount = null;
    protected TextMeshProUGUI instructionText = null;
    protected virtual void Start()
    {
        if (gameplayHUD != null)
        {
            Transform lifeCountObj = gameplayHUD.transform.Find("Life/LifeCount");
            Transform towerCountObj = gameplayHUD.transform.Find("Enemy/EnemyCount");
            Transform instructionTextObj = gameplayHUD.transform.Find("InstructionText");

            if (lifeCountObj != null)
                lifeCount = lifeCountObj.GetComponent<TextMeshProUGUI>();

            if (towerCountObj != null)
                enemyCount = towerCountObj.GetComponent<TextMeshProUGUI>();
            if (instructionTextObj != null)
                instructionText = instructionTextObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            Debug.LogWarning("gameplayHUD not assigned!");
        }

        UpdateLifeDisplay();
        UpdateEnemyDisplay();
        StartCoroutine(DisableInstructionTextAfterDelay(3f));
    }

    protected System.Collections.IEnumerator DisableInstructionTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        Debug.Log("instruction gone start");
        if (instructionText != null)
        {
            instructionText.SetText("");
        }
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

    protected void UpdateLifeDisplay()
    {
        if (lifeCount != null)
            lifeCount.SetText(lives.ToString());
    }


    public void Destroy_Enemy()
    {
        enemy_Num--;
        UpdateEnemyDisplay();
    }

    protected void UpdateEnemyDisplay()
    {
        if (enemyCount != null)
            enemyCount.SetText(enemy_Num.ToString());
        else
            Debug.LogWarning("towerCount is null when trying to update UI!");
    }
    public void Game_Over()
    {
        if (gameplayHUD != null)
        {
            gameplayHUD.SetActive(false);
            Debug.Log("[Subscene] Subscene HUD removed");
        }

        if (SceneTransitionController.Instance != null)
        {
            Debug.Log("[Subscene] Calling SceneTransitionController.ExitCurrentSubScene()");
            Time.timeScale = 1f;
            SceneTransitionController.Instance.ExitCurrentSubScene();
            return;
        }
        gameOver = true;
    }
    public void Finish_Game()
    {
        // 1) Find the SubSceneTrigger in other loaded scenes that corresponds to this subscene
        string currentSubScene = SceneManager.GetActiveScene().name;
        SubSceneTrigger originTrigger = FindTriggerForSubScene(currentSubScene);

        if (originTrigger != null)
        {
            originTrigger.hasCompleted = true;
            Debug.Log($"[Subscene] Marked SubSceneTrigger '{originTrigger.name}' hasCompleted = true for subscene '{currentSubScene}'.");
        }
        else
        {
            Debug.LogWarning($"[Subscene] Could not find SubSceneTrigger for subscene '{currentSubScene}'. Make sure the cylinder/trigger is in a loaded scene and has subSceneName = '{currentSubScene}', and is in keepActiveInMainScene list.");
        }

        // 2) Proceed with Game_Over which will ask the controller to unload the subscene
        Game_Over();
    }

    protected SubSceneTrigger FindTriggerForSubScene(string subSceneName)
    {
        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);

            // Skip the current (sub)scene
            if (!s.IsValid() || s.name == SceneManager.GetActiveScene().name) continue;

            // Look through root objects in that scene
            GameObject[] roots = s.GetRootGameObjects();
            foreach (var root in roots)
            {
                // Find triggers in children (including inactive ones)
                var triggers = root.GetComponentsInChildren<SubSceneTrigger>(true);
                foreach (var t in triggers)
                {
                    if (t == null) continue;
                    if (string.Equals(t.subSceneName, subSceneName, System.StringComparison.OrdinalIgnoreCase))
                        return t;
                }
            }
        }

        return null;
    }

    public abstract bool CanProceed();
}
