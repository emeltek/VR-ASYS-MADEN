using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MiningSequence : MonoBehaviour
{
    public static bool isMiningStarted = false;
    public static bool isEducationSceneFinish;
    private string sceneName;
    
    public GameObject startButton;
    
    public GameObject scoreText;
    public GameObject rockObject;
    public AudioSource shakeStartAudio;
    public AudioSource shakeEndAudio;
    public AudioSource dropRockAudio;
    public SmokeSpawner smokeSpawner;
    public GameObject lastMenuUI;
    public GameObject infoText;
    public static bool isButtonPressed;

    

    public void StartMining()
    {
        startButton.SetActive(false);
        isButtonPressed = true;
        StartCoroutine(MiningRoutine());
        
    }
    void Awake()
    {
        smokeSpawner.onAllSmokesSpawned += OnSmokesFinished;
    }

    private void Start()
    {
        sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != "VR-ASYS-MADEN-EGITIM")
        {
          
            scoreText.SetActive(false);
        }
        lastMenuUI.SetActive(false);
    }

    private void OnSmokesFinished()
    {
        MiningSequence.isMiningStarted = false;
        ScoreManager.Instance.FinalizeSafeZoneScore();
        ScoreManager.Instance.StartOptimalTimer(); // smoke bitince timer başlar
        
        if (shakeEndAudio != null)
        {
            if (sceneName == "VR-ASYS-MADEN-EGITIM")
            {
                shakeEndAudio.Play();
            }
            
            isEducationSceneFinish = true;
            lastMenuUI.SetActive(true);
            scoreText.SetActive(true);
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