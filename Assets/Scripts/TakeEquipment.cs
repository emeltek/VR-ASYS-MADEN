using System.Collections;
using UnityEngine;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class TakeEquipment : MonoBehaviour
{
 
    private static bool isWearMask = false;
    private static bool isWearLeftGlove = false;
    private static bool isWearRightGlove = false;
    private static bool isWearGoggles = false;
    private static bool isWearBoots = false;
    private static bool isWearHelmet = false;
    private static bool isWearVest = false;

    private GameObject door;
    static private bool isDoorAnimating = false;

    private GameObject menuUILast;
    public string objectTag;
    private AudioSource warningAudio;
    private AudioSource nextSceneAudio;
    private AudioSource wearEquipmentAudio;
    private string sceneName;

    void Start()
    {   
        sceneName = SceneManager.GetActiveScene().name;
        door = GameObject.Find("door_locker");
        if (sceneName == "VR-ASYS-MADEN-EGITIM")
        {
           
            door.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }
        else
        {
            door.transform.DORotate(new Vector3(-90f, 0f, -90f), 1f)
                .SetEase(Ease.InOutQuad);
        }
    
        AudioSource[] sources = door.GetComponents<AudioSource>();
        warningAudio = sources[0];
        nextSceneAudio = sources[1];
        wearEquipmentAudio = sources[2];
      
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Mask") && objectTag == "Mask")
        {
            isWearMask = true;
            other.gameObject.SetActive(false);
            wearEquipmentAudio.Play();
            ScoreManager.Instance.AddEquipmentScore("Mask");
            AllEquipmentsWear();
        }
        if (other.CompareTag("LeftGlove") && objectTag == "LeftGlove")
        {
            isWearLeftGlove = true;
            other.gameObject.SetActive(false);
            wearEquipmentAudio.Play();

            ScoreManager.Instance.AddEquipmentScore("LeftGlove");

            AllEquipmentsWear();
        }
        if (other.CompareTag("RightGlove") && objectTag == "RightGlove")
        {
            isWearRightGlove = true;
            other.gameObject.SetActive(false);
            wearEquipmentAudio.Play();
            ScoreManager.Instance.AddEquipmentScore("RightGlove");

            AllEquipmentsWear();
        }
        if (other.CompareTag("Goggles") && objectTag == "Goggles")
        {
            isWearGoggles = true;
            other.gameObject.SetActive(false);
            wearEquipmentAudio.Play();
            ScoreManager.Instance.AddEquipmentScore("Goggles");

            AllEquipmentsWear();
        }
        if (other.CompareTag("Boot") && objectTag == "Boot")
        {
            isWearBoots = true;
            other.gameObject.SetActive(false);
            wearEquipmentAudio.Play();
            ScoreManager.Instance.AddEquipmentScore("Boot");

            AllEquipmentsWear();
        }
        if (other.CompareTag("Helmet") && objectTag == "Helmet")
        {
            isWearHelmet = true;
            other.gameObject.SetActive(false);
            wearEquipmentAudio.Play();
            ScoreManager.Instance.AddEquipmentScore("Helmet");

            AllEquipmentsWear();
        }
        if (other.CompareTag("Vest") && objectTag == "Vest")
        {
            isWearVest = true;
            other.gameObject.SetActive(false);
            wearEquipmentAudio.Play();
            ScoreManager.Instance.AddEquipmentScore("Vest");

            AllEquipmentsWear();
        }
        if (other.CompareTag("Door") && objectTag == "Door")
        {
            if (!isDoorAnimating && sceneName == "VR-ASYS-MADEN-EGITIM")
            {
                if (warningAudio != null)
                    warningAudio.Play();
            }

            if (isDoorAnimating == true && MiningSequence.isEducationSceneFinish == true && sceneName =="VR-ASYS-MADEN-EGITIM")
            {
                nextSceneAudio.Play();
            }
        }
    }

    private void AllEquipmentsWear()
    {
        if (isWearMask && isWearLeftGlove && isWearRightGlove && isWearGoggles && isWearBoots && isWearHelmet && isWearVest)
        {
            isDoorAnimating = true;
            door.transform.DORotate(new Vector3(-90f, 0f, -90f), 1f)
                .SetEase(Ease.InOutQuad);
        }
    }
}