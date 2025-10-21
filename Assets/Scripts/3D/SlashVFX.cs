using System.Collections.Generic;
using UnityEngine;

public class SlashAttachSwordJoint : StateMachineBehaviour
{
    [Header("Prefab (Project asset)")]
    public GameObject slashPrefab;

    [Header("Audio")]
    public AudioClip slashSound;
    [Range(0f, 1f)] public float slashVolume = 1f;
    public float soundMinInterval = 0.05f;
    public float soundDestroyTime = 1f;

    [Header("Timing")]
    public float activateDelay = 0.12f;
    public float activeDuration = 0.6f;
    public bool disableAfter = true;

    const string SwordJointName = "mixamorig:Sword_joint";
    static readonly Vector3 ForcedLocalPosition = Vector3.zero;
    static readonly Vector3 ForcedLocalEuler = Vector3.zero;
    static readonly Vector3 ForcedLocalScale = new Vector3(0.6f, 0.6f, 0.6f);

    float timer;
    bool activated;
    float activeTimer;

    GameObject runtimeSlash;
    bool runtimeInstantiated;

    // prevent overlapping sounds
    static Dictionary<int, float> lastPlayTime = new Dictionary<int, float>();
    bool soundPlayed; // new: track if slash sound has already played

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        timer = 0f;
        activated = false;
        activeTimer = 0f;
        runtimeSlash = null;
        runtimeInstantiated = false;
        soundPlayed = false; // reset sound

        if (animator == null) return;

        Transform sword = FindDeepChild(animator.transform, SwordJointName);
        Transform attachTarget = sword != null ? sword : animator.transform;

        if (slashPrefab != null)
        {
            runtimeSlash = Object.Instantiate(slashPrefab, attachTarget);
            runtimeInstantiated = true;

            runtimeSlash.transform.localPosition = ForcedLocalPosition;
            runtimeSlash.transform.localEulerAngles = ForcedLocalEuler;
            runtimeSlash.transform.localScale = ForcedLocalScale;
            runtimeSlash.SetActive(false);
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        timer += Time.deltaTime;

        if (!activated && timer >= activateDelay)
        {
            if (runtimeSlash != null)
                ActivateSlash(runtimeSlash);

            if (!soundPlayed)
            {
                PlaySlashSound(animator);
                soundPlayed = true; // mark as played
            }

            activated = true;
            activeTimer = 0f;
        }

        if (activated && activeDuration > 0f && disableAfter)
        {
            activeTimer += Time.deltaTime;
            if (activeTimer >= activeDuration)
            {
                if (runtimeInstantiated)
                    DestroyRuntime();
                else if (runtimeSlash != null)
                    DeactivateSlash(runtimeSlash);

                activated = false;
            }
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (runtimeSlash != null)
        {
            if (runtimeInstantiated)
                DestroyRuntime();
            else if (disableAfter)
                DeactivateSlash(runtimeSlash);
        }

        runtimeSlash = null;
        runtimeInstantiated = false;
        activated = false;
        soundPlayed = false; // reset
        timer = 0f;
        activeTimer = 0f;
    }

    void ActivateSlash(GameObject go)
    {
        if (go == null) return;
        go.SetActive(true);

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Play(true);
        }

        var childAnim = go.GetComponentInChildren<Animator>(true);
        if (childAnim != null)
        {
            childAnim.Rebind();
            childAnim.Update(0f);
        }
    }

    void DeactivateSlash(GameObject go)
    {
        if (go == null) return;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (disableAfter) go.SetActive(false);
    }

    void DestroyRuntime()
    {
        if (runtimeSlash == null) return;
        Object.Destroy(runtimeSlash);
        runtimeSlash = null;
    }

    void PlaySlashSound(Animator animator)
    {
        if (slashSound == null || animator == null) return;

        int id = animator.GetInstanceID();
        float now = Time.time;

        if (lastPlayTime.TryGetValue(id, out float last) && now - last < soundMinInterval)
            return;

        lastPlayTime[id] = now;

        // Find sword joint or fallback
        Transform sword = FindDeepChild(animator.transform, SwordJointName);
        Vector3 position = sword != null ? sword.position : animator.transform.position;

        // Create a temporary GameObject to play the sound
        GameObject soundGO = new GameObject("TempSlashSound");
        soundGO.transform.position = position;
        AudioSource audioSource = soundGO.AddComponent<AudioSource>();
        audioSource.clip = slashSound;
        audioSource.volume = slashVolume;
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.Play();

        // Destroy after specified time
        Object.Destroy(soundGO, soundDestroyTime);
    }

    Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        foreach (Transform t in parent)
        {
            Transform r = FindDeepChild(t, name);
            if (r != null) return r;
        }
        return null;
    }
}
