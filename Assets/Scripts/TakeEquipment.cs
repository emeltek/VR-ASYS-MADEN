using System.Collections;
using UnityEngine;
using DG.Tweening;

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
    private bool isDoorAnimating = false;

    public string objectTag;
    private AudioSource warningAudio;

    void Start()
    {
        door = GameObject.Find("door_locker");
        door.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        warningAudio = door.GetComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Mask") && objectTag == "Mask")
        {
            isWearMask = true;
            other.gameObject.SetActive(false);
            AllEquipmentsWear();
        }
        if (other.CompareTag("LeftGlove") && objectTag == "LeftGlove")
        {
            isWearLeftGlove = true;
            other.gameObject.SetActive(false);
            AllEquipmentsWear();
        }
        if (other.CompareTag("RightGlove") && objectTag == "RightGlove")
        {
            isWearRightGlove = true;
            other.gameObject.SetActive(false);
            AllEquipmentsWear();
        }
        if (other.CompareTag("Goggles") && objectTag == "Goggles")
        {
            isWearGoggles = true;
            other.gameObject.SetActive(false);
            AllEquipmentsWear();
        }
        if (other.CompareTag("Boot") && objectTag == "Boot")
        {
            isWearBoots = true;
            other.gameObject.SetActive(false);
            AllEquipmentsWear();
        }
        if (other.CompareTag("Helmet") && objectTag == "Helmet")
        {
            isWearHelmet = true;
            other.gameObject.SetActive(false);
            AllEquipmentsWear();
        }
        if (other.CompareTag("Vest") && objectTag == "Vest")
        {
            isWearVest = true;
            other.gameObject.SetActive(false);
            AllEquipmentsWear();
        }
        if (other.CompareTag("Door") && objectTag == "Door")
        {
            if (!isDoorAnimating)
            {
                if (warningAudio != null)
                    warningAudio.Play();
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