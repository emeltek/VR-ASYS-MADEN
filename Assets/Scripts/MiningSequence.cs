using System;
using System.Collections;
using Oculus.Interaction.Locomotion.Editor.Generated;
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
    public GameObject lastMenuUI;
    public GameObject infoText;

    public void StartMining()
    {
        StartCoroutine(MiningRoutine());
        
    }
    void Awake()
    {
        smokeSpawner.onAllSmokesSpawned += OnSmokesFinished;
    }

    private void Start()
    {
        lastMenuUI.SetActive(false);
    }

    private void OnSmokesFinished()
    {
        MiningSequence.isMiningStarted = false;
        //ScoreManager.Instance.FinalizeSafeZoneScore();
        
        if (shakeEndAudio != null)
        {
            shakeEndAudio.Play();
            isEducationSceneFinish = true;
            lastMenuUI.SetActive(true);
        }
    }

    private IEnumerator MiningRoutine()
    {
        isMiningStarted = true;
        infoText.SetActive(false);
   
        // Sarsıntı başlangıç sesi
        if (shakeStartAudio != null)
            shakeStartAudio.Play();
        yield return new WaitForSeconds(4f);

        // Smoke particle başla
        if (smokeSpawner != null)
            smokeSpawner.SpawnSmoke();

        
        if (dropRockAudio != null)
            dropRockAudio.Play();
        
        yield return new WaitForSeconds(3f);

        // Taş aktif et
        if (rockObject != null)
            rockObject.SetActive(true);
        
    }
}