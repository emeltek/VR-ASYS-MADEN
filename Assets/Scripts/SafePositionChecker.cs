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
        if (!MiningSequence.isMiningStarted) return;

        if (other.CompareTag("LeftHand"))
        {
            isLeftHandInside = true;
            CheckBothHands();
        }

        if (other.CompareTag("RightHand"))
        {
            isRightHandInside = true;
            CheckBothHands();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("LeftHand"))
            isLeftHandInside = false;

        if (other.CompareTag("RightHand"))
            isRightHandInside = false;
    }

    private void CheckBothHands()
    {
        if (!isLeftHandInside || !isRightHandInside) return;
        if (!ScoreManager.Instance.IsInSafeZone) return;

        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == "VR-ASYS-MADEN-EGITIM")
        {
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