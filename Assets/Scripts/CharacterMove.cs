using DG.Tweening;
using UnityEngine;

public class CharacterMove : MonoBehaviour
{
    public Transform target; // Hedef noktası
    public float stopDistance = 5f; // Karakterin duracağı mesafe
    public float moveSpeed = 2f; // Hareket hızı

    private Animator animator;
    private Vector3 targetPosition;
    private float distance;

    void Start()
    {
        // Animator bileşenini al
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Animator component not found!");
        }

        // Hedef pozisyonu belirle (Y eksenini sabit tutarak)
        targetPosition = new Vector3(target.position.x, transform.position.y, target.position.z);
    }

    void Update()
    {
        // Hedefe olan mesafeyi hesapla
        distance = Vector3.Distance(transform.position, targetPosition);

        if (distance > stopDistance)
        {
            // Hedefe doğru dön
            Vector3 direction = (targetPosition - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);

            // Animasyonu çalıştır ve hareket et
            animator.SetBool("isRunning", true);
            
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }
        else
        {
            // Animasyonu durdur
            animator.SetBool("isRunning", false);
            
        }
    }

    // Hedef pozisyonu güncelleme metodu
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetPosition = new Vector3(target.position.x, transform.position.y, target.position.z);
    }
}