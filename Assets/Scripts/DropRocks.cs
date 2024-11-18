using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DropRocks : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private GroundShake groundShake;
    [SerializeField] private GameObject rocksParent; 
    private bool rocksActivated = false;
    [SerializeField] private GameObject effect;

     void Awake()
    {
        rocksParent.SetActive(false);
    }
    void Start()
    {
       
        effect.GetComponent<ParticleSystem>().Stop();

        StartCoroutine(PlayEffectWithDelay());
    }

    // Update is called once per frame
    void Update()
    {
       
    }

    void FallandPlay()
    {
        if (groundShake.isShaking && !rocksActivated)
        {

            Debug.Log("method çalıştı.");
            rocksParent.SetActive(true);
            rocksActivated = true;
            StartCoroutine(PlayEffectWithDelay());
            effect.GetComponent<ParticleSystem>().Play();
        }
    }

    IEnumerator PlayEffectWithDelay()
    {
        yield return new WaitForSeconds(2.0f);
        FallandPlay();
    }
}
