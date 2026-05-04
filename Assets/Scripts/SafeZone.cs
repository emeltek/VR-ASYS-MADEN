using System;
using UnityEngine;

public class SafeZone : MonoBehaviour
{
    private static bool playerInside = false;
    private float penaltyTimer = 0f;
    private const float penaltyInterval = 3f;
    private AudioSource audioSource;
    public static bool isPlayerInSafeZone = false;
    private AudioSource correctSound;


    

    void Update()
    {
        if (!MiningSequence.isMiningStarted) return;

        if (playerInside)
        {
            // içeride ceza yok
        }
        else
        {
            penaltyTimer += Time.deltaTime;
            if (penaltyTimer >= penaltyInterval)
            {
                penaltyTimer = 0f;
                //ScoreManager.Instance.PenalizeSafeZone();
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        
       if (!other.CompareTag("Player")) return;
       //if (!MiningSequence.isMiningStarted) return;
        playerInside = true;
        isPlayerInSafeZone = true;
        penaltyTimer = 0f;
       // ScoreManager.Instance.EnterSafeZone();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
       // if (!MiningSequence.isMiningStarted) return;

        
        isPlayerInSafeZone = false;
        penaltyTimer = 0f;
       // ScoreManager.Instance.ExitSafeZone();
    }
}