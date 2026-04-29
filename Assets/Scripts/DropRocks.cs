using System.Collections;

using UnityEngine;

public class DropRocks : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private GroundShake groundShake;
    [SerializeField] private GameObject rocksParent; 
    private bool rocksActivated = false;
    [SerializeField] private GameObject effect;
    private bool isActive = false;
    private  ParticleSystem.ShapeModule shape;
    private float sayac = 0;
    private ParticleSystem partilceSystem;

     void Awake()
    {
        rocksParent.SetActive(false);
    }
    void Start()
    {
       
        effect.GetComponent<ParticleSystem>().Stop();

        StartCoroutine(PlayEffectWithDelay());
        partilceSystem = effect.GetComponent<ParticleSystem>();

        shape = partilceSystem.shape;
    }

    // Update is called once per frame
    void Update()
    {
        sayac += Time.deltaTime;
        
        if (isActive == true)
        {
            shape.scale = new Vector3(sayac*2, shape.scale.y, shape.scale.z);
        }

        if (sayac >= 20)
        {
            partilceSystem.Stop();
            isActive = false;
        }
        
    }

    void  FallandPlay()
    {
        if (groundShake.isShaking && !rocksActivated)
        {

            rocksParent.SetActive(true);
            rocksActivated = true;
            StartCoroutine(PlayEffectWithDelay());
            
            var particle = effect.GetComponent<ParticleSystem>();
            particle.Play();
            Debug.Log(particle.shape.scale.x);       
            isActive = true;

        }
       
    }

    IEnumerator PlayEffectWithDelay()
    {
        yield return new WaitForSeconds(2.0f);
        FallandPlay();
    }
}
