using UnityEngine;
using UnityEngine.SceneManagement;

public class SafePositionChecker : MonoBehaviour
{
    private bool isLeftHandInside = false;
    private bool isRightHandInside = false;
    private bool scoreGiven = false;

    public AudioSource safePositionAudio;

    private void OnTriggerEnter(Collider other)
    {
       // if (!MiningSequence.isMiningStarted) return;

        if (other.CompareTag("LeftGlove"))
        {
            Debug.Log("sol el girdi.");

            isLeftHandInside = true;
            CheckBothHands();
        }

        if (other.CompareTag("RightGlove"))
        {
            Debug.Log("Sağ el girdi.");
            isRightHandInside = true;
            CheckBothHands();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        //if (!MiningSequence.isMiningStarted) return;
    
        if (other.CompareTag("LeftGlove"))
            isLeftHandInside = false;

        if (other.CompareTag("RightGlove"))
            isRightHandInside = false;
    }
    private void CheckBothHands()
    {
        if (!isLeftHandInside || !isRightHandInside) return;
        //if (!ScoreManager.Instance.IsInSafeZone) return;
        if (!SafeZone.isPlayerInSafeZone) return;

        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == "VR-ASYS-MADEN-EGITIM")
        {
            Debug.Log("iki  el girdi.");
            if (safePositionAudio != null)
                safePositionAudio.Play();
        }
        else
        {
            if (scoreGiven) return;
            scoreGiven = true;
            ScoreManager.Instance.AddSafePositionScore();
        }
    }
}