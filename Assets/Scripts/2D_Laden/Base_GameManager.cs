using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class Base_GameManager : MonoBehaviour
{
    public GameObject gameplayHUD = null;
    public bool gameOver = false;

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.R))
        {
            Game_Over();
        }
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

    /// <summary>
    /// Finish the subscene: mark the originating SubSceneTrigger as completed,
    /// then perform the normal game-over/exit behavior.
    /// </summary>
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

    /// <summary>
    /// Searches all loaded scenes except the currently active one for a SubSceneTrigger
    /// whose subSceneName matches the given name. Returns the first match or null.
    /// </summary>
    private SubSceneTrigger FindTriggerForSubScene(string subSceneName)
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
