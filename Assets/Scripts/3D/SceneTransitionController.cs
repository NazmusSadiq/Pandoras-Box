using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionController : MonoBehaviour
{
    public static SceneTransitionController Instance { get; private set; }

    [Tooltip("Assign main HUD from the main scene (optional).")]
    public GameObject mainGameplayHUD;

    [Tooltip("Root objects in the main scene you want to keep active (eg managers).")]
    public GameObject[] keepActiveInMainScene;

    public string keepTag = "Persistent";

    private string mainSceneName;
    private string loadedSubSceneName = null;

    // NEW: transitioning + cooldown
    private bool isTransitioning = false;
    private float nextAllowedEntryTime = 0f;
    [Tooltip("Seconds to ignore re-entry right after returning to the main scene.")]
    public float reEntryCooldown = 0.5f;

    private List<GameObject> disabledMainRoots = new List<GameObject>();
    private List<Camera> disabledMainCameras = new List<Camera>();
    public ThreeD_Character player = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        mainSceneName = SceneManager.GetActiveScene().name;
    }

    public bool IsSubSceneLoaded => !string.IsNullOrEmpty(loadedSubSceneName);
    public bool IsTransitioning => isTransitioning;

    public bool CanAcceptEntry()
    {
        return !isTransitioning && Time.time >= nextAllowedEntryTime && !IsSubSceneLoaded;
    }

    public void EnterSubScene(string subSceneName)
    {
        if (!CanAcceptEntry())
        {
            Debug.Log("[SceneTransitionController] EnterSubScene blocked (transitioning or cooldown).");
            return;
        }

        if (IsSubSceneLoaded)
        {
            Debug.LogWarning("[SceneTransitionController] Subscene already loaded: " + loadedSubSceneName);
            return;
        }

        StartCoroutine(EnterSubSceneCoroutine(subSceneName));
        if(player)  player.dead = true;
    }

    private IEnumerator EnterSubSceneCoroutine(string subSceneName)
    {
        isTransitioning = true;
        Debug.Log("[SceneTransitionController] EnterSubScene: preparing to load " + subSceneName);

        // hide main HUD
        if (mainGameplayHUD != null) mainGameplayHUD.SetActive(false);

        // disable main root objects (except keepers)
        Scene mainScene = SceneManager.GetSceneByName(mainSceneName);
        if (mainScene.IsValid())
        {
            var roots = mainScene.GetRootGameObjects();
            foreach (var go in roots)
            {
                if (ShouldKeepActive(go)) continue;

                // disable cameras under this root
                var cams = go.GetComponentsInChildren<Camera>(true);
                foreach (var c in cams)
                {
                    if (c.enabled) { c.enabled = false; disabledMainCameras.Add(c); }
                }

                if (go.activeSelf) { go.SetActive(false); disabledMainRoots.Add(go); }
            }
        }

        var ao = SceneManager.LoadSceneAsync(subSceneName, LoadSceneMode.Additive);
        if (ao == null) { Debug.LogError("[SceneTransitionController] Failed to load subscene: " + subSceneName); isTransitioning = false; yield break; }
        while (!ao.isDone) yield return null;

        loadedSubSceneName = subSceneName;
        Scene loaded = SceneManager.GetSceneByName(subSceneName);
        if (loaded.IsValid())
        {
            SceneManager.SetActiveScene(loaded);
            Debug.Log("[SceneTransitionController] Subscene loaded and set active: " + subSceneName);
        }
        else
        {
            Debug.LogWarning("[SceneTransitionController] Subscene loaded but not found by name: " + subSceneName);
        }

        Time.timeScale = 1f;
        isTransitioning = false;
    }

    // NEW: safer public API used by your GameManager
    public void ExitCurrentSubScene()
    {
        if (isTransitioning)
        {
            Debug.Log("[SceneTransitionController] Exit requested but already transitioning.");
            return;
        }
        StartCoroutine(ExitCurrentSubSceneCoroutine());
    }

    private IEnumerator ExitCurrentSubSceneCoroutine()
    {
        isTransitioning = true;
        if(player)  player.dead = false;

        Scene active = SceneManager.GetActiveScene();
        Debug.Log("[SceneTransitionController] ExitCurrentSubScene called. ActiveScene = " + active.name + " (mainSceneName=" + mainSceneName + ", loadedSubSceneName=" + loadedSubSceneName + ")");

        Scene toUnload = default;
        bool haveToUnload = false;

        if (active.name != mainSceneName)
        {
            toUnload = active;
            haveToUnload = true;
        }
        else if (!string.IsNullOrEmpty(loadedSubSceneName))
        {
            toUnload = SceneManager.GetSceneByName(loadedSubSceneName);
            if (toUnload.IsValid()) haveToUnload = true;
        }

        if (!haveToUnload)
        {
            Debug.LogWarning("[SceneTransitionController] No subscene found to unload. Ensuring main HUD restored.");
            RestoreMainSceneState();
            isTransitioning = false;
            yield break;
        }

        Debug.Log("[SceneTransitionController] Unloading subscene: " + toUnload.name);
        var ao = SceneManager.UnloadSceneAsync(toUnload);
        if (ao == null) { Debug.LogError("[SceneTransitionController] UnloadSceneAsync returned null for " + toUnload.name); isTransitioning = false; yield break; }
        while (!ao.isDone) yield return null;

        // Make main scene active again
        Scene main = SceneManager.GetSceneByName(mainSceneName);
        if (main.IsValid()) SceneManager.SetActiveScene(main);
        else if (SceneManager.sceneCount > 0) SceneManager.SetActiveScene(SceneManager.GetSceneAt(0));

        // restore disabled roots and cameras
        RestoreMainSceneState();

        // clear record
        Debug.Log("[SceneTransitionController] Unloaded subscene and restored main scene.");
        loadedSubSceneName = null;

        nextAllowedEntryTime = Time.time + reEntryCooldown;
        isTransitioning = false;
    }

    private void RestoreMainSceneState()
    {
        foreach (var go in disabledMainRoots)
            if (go != null) go.SetActive(true);
        disabledMainRoots.Clear();

        foreach (var cam in disabledMainCameras)
            if (cam != null) cam.enabled = true;
        disabledMainCameras.Clear();

        if (mainGameplayHUD != null) mainGameplayHUD.SetActive(true);

    }

    private bool ShouldKeepActive(GameObject go)
    {
        if (go == this.gameObject) return true; 
        foreach (var k in keepActiveInMainScene) if (k == go) return true;
        if (!string.IsNullOrEmpty(keepTag) && go.CompareTag(keepTag)) return true;
        return false;
    }
}
