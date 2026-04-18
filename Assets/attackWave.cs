using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class attackWave : MonoBehaviour
{
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] float moveSpeed = 25;
    [SerializeField] float disappearSpeed = 2.5f;
    [SerializeField] Collider2D collider2;

    void Update()
    {
        spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, spriteRenderer.color.a - Time.deltaTime * disappearSpeed);
        transform.position = new Vector2(transform.position.x + Time.deltaTime * moveSpeed, transform.position.y);
        if (spriteRenderer.color.a <= 0) Destroy(gameObject);

        Collider2D[] hits = new Collider2D[8];
        int hitCount = collider2.GetContacts(hits);
        for (int i = 0; i < hitCount; i++)
        {
            var other = hits[i];
            if (other == null) continue;

            Debug.Log(other.tag);
            if (other.CompareTag("Obstacle"))
            {
                var obs = other.gameObject.GetComponent<Obstacle>();
                obs.onDestr();
                Destroy(gameObject);
            }
        }
    }
}
