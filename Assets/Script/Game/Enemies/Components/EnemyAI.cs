using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public float moveSpeed = 2f;
    public int hp = 2;
    [Tooltip("主角的Tag，默认用 Player")]
    public string playerTag = "Player";

    private Transform player;
    private Rigidbody2D rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        // Make sure enemies are driven by physics so they can collide/push each other.
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        FindPlayer();
    }

    private void FixedUpdate()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        Vector2 current = rb.position;
        Vector2 target = player.position;
        Vector2 next = Vector2.MoveTowards(current, target, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(next);
    }

    private void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        player = p != null ? p.transform : null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag(playerTag))
        {
            Debug.Log("受伤");
        }
    }
}