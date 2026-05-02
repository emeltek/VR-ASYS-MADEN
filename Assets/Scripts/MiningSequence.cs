using System.Collections;
using UnityEngine;

public class MiningSequence : MonoBehaviour
{
    public static bool isMiningStarted = false;
    public static bool isEducationSceneFinish;

    public GameObject rockObject;
    public AudioSource shakeStartAudio;
    public AudioSource shakeEndAudio;
    public AudioSource dropRockAudio;
    public SmokeSpawner smokeSpawner;

    public void StartMining()
    {
        StartCoroutine(MiningRoutine());
    }
    void Awake()
    {
        smokeSpawner.onAllSmokesSpawned += OnSmokesFinished;
    }

    private void OnSmokesFinished()
    {
        MiningSequence.isMiningStarted = false;
        if (shakeEndAudio != null)
        {
            shakeEndAudio.Play();
            isEducationSceneFinish = true;
        }
        
    }

    private IEnumerator MiningRoutine()
    {
        isMiningStarted = true;

   
        // Sarsıntı başlangıç sesi
        if (shakeStartAudio != null)
            shakeStartAudio.Play();
        
        if (dropRockAudio != null)
            dropRockAudio.Play();

        yield return new WaitForSeconds(2.5f);

        // Smoke particle başla
        if (smokeSpawner != null)
            smokeSpawner.SpawnSmoke();

        yield return new WaitForSeconds(3f);

        // Taş aktif et
        if (rockObject != null)
            rockObject.SetActive(true);
        
    }
}