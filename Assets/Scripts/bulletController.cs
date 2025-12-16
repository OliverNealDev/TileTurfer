using System.Collections;
using UnityEngine;

public class bulletController : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private bool isPlayerBullet;
    public bool IsPlayerBullet => isPlayerBullet; 

    [SerializeField] private float speed = 5f;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float size = 1f;
    [SerializeField] private float stunDuration = 0f;
    [SerializeField] private Collider2D parentCollider; 

    private Collider2D bulletCollider;
    private TurfManager turfManager;
    
    void Awake()
    {
        bulletCollider = GetComponent<Collider2D>();
        turfManager = FindFirstObjectByType<TurfManager>();
    }

    void Start()
    {
        StartCoroutine(DespawnSequence());
    }

    public void Initialise(
        bool SetIsPlayerBullet, 
        float SetSpeed, 
        float SetKnockbackForce, 
        float SetSize, 
        float SetStunDuration,
        Collider2D SetParentCollider)
    {
        isPlayerBullet = SetIsPlayerBullet;
        speed = SetSpeed;
        knockbackForce = SetKnockbackForce;
        size = SetSize;
        stunDuration = SetStunDuration;
        parentCollider = SetParentCollider;
        
        if (parentCollider != null && bulletCollider != null)
        {
            Physics2D.IgnoreCollision(bulletCollider, parentCollider);
        }

        if (TryGetComponent<Rigidbody2D>(out Rigidbody2D projRb))
        {
            projRb.linearVelocity = transform.right * speed;
        }
        
        if (transform.childCount > 0)
        {
            SpriteRenderer sr = transform.GetChild(0).GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (isPlayerBullet)
                {
                    gameObject.tag = "PlayerBullet";
                    
                    if (turfManager != null)
                    {
                        sr.color = turfManager.playerColor; 
                    }
                    else
                    {
                        sr.color = new Color32(44, 131, 181, 255); 
                    }
                }
                else
                {
                    gameObject.tag = "EnemyBullet";
                    if (turfManager != null)
                    {
                         sr.color = turfManager.enemyColor;
                    }
                    else
                    {
                         sr.color = new Color32(209, 52, 52, 255);
                    }
                }
            }
        }
    }

    IEnumerator DespawnSequence()
    {
        yield return new WaitForSeconds(1.5f);
        StartCoroutine(ShrinkAndDestroy());
    }
    
    IEnumerator InstantDespawnSequence()
    {
        yield return StartCoroutine(ShrinkAndDestroy());
    }

    IEnumerator ShrinkAndDestroy()
    {
        float duration = 0.25f;
        float elapsed = 0f;
        
        Vector3 startScale = transform.localScale;
        SpriteRenderer sr = (transform.childCount > 0) ? transform.GetChild(0).GetComponent<SpriteRenderer>() : null;
        
        Color startColor = (sr != null) ? sr.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            
            if (sr != null) 
            {
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.collider == parentCollider) return;

        bulletController otherBullet = other.gameObject.GetComponent<bulletController>();
        if (otherBullet != null)
        {
            if (otherBullet.IsPlayerBullet == this.isPlayerBullet) return;
        }

        if (bulletCollider != null && bulletCollider.enabled == false) return;

        StopCoroutine(DespawnSequence());

        if (bulletCollider != null) bulletCollider.enabled = false; 
        
        if (TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.linearVelocity = Vector2.zero; 
            rb.bodyType = RigidbodyType2D.Kinematic; 
        }

        StartCoroutine(InstantDespawnSequence());
    }
}