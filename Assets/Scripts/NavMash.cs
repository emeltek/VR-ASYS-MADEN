using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NavMash : MonoBehaviour
{
    // Start is called before the first frame update
    private NavMeshAgent navMeshAgent;
    public Transform target;
    private Animator animator;
    private Vector3 targetPosition;
    private float distance;
    public float stopDistance = 5f; // Karakterin duracağı mesafe
    public ParticleSystem particleSystem;
    private int time;
    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        distance = Vector3.Distance(transform.position, targetPosition);

    }

    void Start()
    {
        targetPosition = new Vector3(target.position.x, transform.position.y, target.position.z);
    }

    // Update is called once per frame
    void Update()
    {
        distance = Vector3.Distance(transform.position, targetPosition);

        navMeshAgent.SetDestination(target.position);
        Debug.Log(particleSystem.isPlaying);
        
        
        if (distance>stopDistance)
        {

            animator.SetBool("isRunning", true);
            animator.SetBool("isKneeling", false);

        }
        else
        {
            animator.SetBool("isRunning", false);
            animator.SetBool("isKneeling",true);
            Debug.Log("oturma animasyonu çalıştı.");
        }
    }

}
