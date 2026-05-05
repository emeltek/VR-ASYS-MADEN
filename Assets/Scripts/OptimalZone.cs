using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OptimalZone : MonoBehaviour
{
  

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
      
        Debug.Log("SafeZone'a girdi senaryo bitti puan hesaplanacak");
        if (MiningSequence.isMiningStarted)
        {
            // Maden çöküyor, erken girdi → 0 puan
            ScoreManager.Instance.ZeroOptimalScore();
            return;
        }

        if (MiningSequence.isButtonPressed == true)
        {
            ScoreManager.Instance.EnterOptimalZone();
        }
        Debug.Log("SafeZone'a girdi");

        

      
    }
}