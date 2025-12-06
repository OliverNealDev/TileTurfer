using System.Collections;
using UnityEngine;

public class bulletController : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private bool isPlayerBullet;
    [SerializeField] private float speed = 5f;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float size = 1f;
    [SerializeField] private float stunDuration = 0f;
    
    void Start()
    {
        StartCoroutine(DespawnSequence());
    }

    public void Initialise(
        bool SetIsPlayerBullet, 
        float SetSpeed, 
        float SetKnockbackForce, 
        float SetSize, 
        float SetStunDuration)
    {
        isPlayerBullet = SetIsPlayerBullet;
        speed = SetSpeed;
        knockbackForce = SetKnockbackForce;
        size = SetSize;
        stunDuration = SetStunDuration;
        
        if (TryGetComponent<Rigidbody2D>(out Rigidbody2D projRb))
        {
            projRb.linearVelocity = transform.right * speed;
        }
        
        if (isPlayerBullet)
        {
            gameObject.tag = "PlayerBullet";
            transform.GetChild(0).GetComponent<SpriteRenderer>().color = new Color32(64, 186, 255, 255);
        }
        else
        {
            gameObject.tag = "EnemyBullet";
            transform.GetChild(0).GetComponent<SpriteRenderer>().color = new Color32(255, 66, 66, 255);
        }
    }

    IEnumerator DespawnSequence()
    {
        yield return new WaitForSeconds(1.5f);

        float duration = 0.25f;
        float elapsed = 0f;
        
        Vector3 startScale = transform.localScale;
        SpriteRenderer sr = transform.GetChild(0).GetComponent<SpriteRenderer>();
        Color startColor = sr.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            
            sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));

            yield return null;
        }

        Destroy(gameObject);
    }
}