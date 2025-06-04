using UnityEngine;
using UnityEngine.AI;

public class Wander : MonoBehaviour
{
    public float wanderRadius = 10f;
    public float wanderTimer = 5f;
    Animator animator;
    private NavMeshAgent agent;
    private float timer;

    float currentTargetOffset;

    float offsetLerpSpeed;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        timer = wanderTimer;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>(); // Make sure Animator is on the same GameObject
        

        
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= wanderTimer)
        {
            Vector3 newPos = RandomNavSphere(transform.position, wanderRadius, -1);
            agent.SetDestination(newPos);
            timer = 0;

            wanderRadius = Random.Range(5f, 20f);
            wanderTimer = Random.Range(2f, 7f);
           currentTargetOffset = Random.Range(1f, 7f);
            offsetLerpSpeed = Random.Range(1f, 4f);




        }
        agent.baseOffset = Mathf.Lerp(agent.baseOffset, currentTargetOffset, Time.deltaTime * offsetLerpSpeed);
        // Check movement and set animation
        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool("isMoving", isMoving);
    }

    public static Vector3 RandomNavSphere(Vector3 origin, float dist, int layermask)
    {
        Vector3 randomDirection = Random.insideUnitSphere * dist;
        randomDirection += origin;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(randomDirection, out navHit, dist, layermask))
        {
            return navHit.position;
        }

        return origin; // fallback if a valid position isn't found
    }
}
