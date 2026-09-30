using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]

public class RandomSoundDelay : MonoBehaviour
{
    [SerializeField] private float minDelay = 3;
    [SerializeField] private float maxDelay = 100;
    [SerializeField] private AudioClip[] audioClips;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        // (r5 / random-sound) Bail out cleanly if no clips are wired
        // up. Random.Range(0, 0) used to throw IndexOutOfRangeException
        // the first time ScreamRoutine ran on a cow with an empty
        // audioClips array, the exception killed the coroutine, and
        // the user never heard any sound. Loud warning so the
        // Inspector misconfiguration is obvious in the console.
        if (audioClips == null || audioClips.Length == 0)
        {
            Debug.LogWarning($"[RandomSoundDelay] {gameObject.name}: no audioClips wired in Inspector, routine will not run.");
            return;
        }
        StartCoroutine(ScreamRoutine());
    }

    IEnumerator ScreamRoutine()
    {
        // (r5 / random-sound) Defensive re-check inside the loop too.
        // If audioClips was non-empty at Start but later emptied by
        // some external code, the next iteration would NRE.
        while (audioClips != null && audioClips.Length > 0)
        {
            float waitTime = Random.Range(minDelay, maxDelay);

            yield return new WaitForSeconds(waitTime);

            var clip = RandomSound();
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }

    private AudioClip RandomSound()
    {
        // (r5 / random-sound) Length is re-checked at call site so a
        // mid-flight race can't NRE even if external code mutates
        // the array. Random.Range(0, length) is exclusive on the
        // upper bound so we never get a clip at index == length.
        if (audioClips == null || audioClips.Length == 0) return null;
        int i = Random.Range(0, audioClips.Length);
        return audioClips[i];
    }
}