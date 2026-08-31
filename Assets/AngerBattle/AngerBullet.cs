using System;
using System.Collections;
using UnityEngine;

namespace AngerBattle
{
    /// <summary>怒りが放つ赤い弾。コンタック中心の小さな円形ヒットボックスだけを判定する。</summary>
    [RequireComponent(typeof(Collider2D))]
    public class AngerBullet : MonoBehaviour
    {
        private Vector2 direction = Vector2.down;
        private float speed = 5.5f;
        private float maxTravelDistance = 24f;
        private Vector3 startPosition;
        private PlayerController target;
        private Action onTargetHit;
        private float targetHitRadius = 0.28f;
        private float bulletHitRadius = 0.11f;
        private bool hitReported;
        private bool shattering;

        public void Configure(Vector2 travelDirection, float travelSpeed, PlayerController player, float hitRadius, Action hitCallback)
        {
            direction = travelDirection.sqrMagnitude > 0.0001f ? travelDirection.normalized : Vector2.down;
            speed = Mathf.Max(0.1f, travelSpeed);
            target = player;
            targetHitRadius = Mathf.Max(0.05f, hitRadius);
            onTargetHit = hitCallback;
        }

        private void Start()
        {
            startPosition = transform.position;
        }

        private void Update()
        {
            if (shattering) return;

            transform.position += (Vector3)direction * speed * Time.deltaTime;
            CheckTargetOverlap();
            if (Vector3.Distance(startPosition, transform.position) > maxTravelDistance)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hitReported || target == null || (other.gameObject != target.gameObject && other.GetComponentInParent<PlayerController>() != target))
            {
                return;
            }

            ReportHit();
        }

        private void CheckTargetOverlap()
        {
            if (hitReported || target == null || !target.gameObject.activeInHierarchy)
            {
                return;
            }

            float allowedDistance = targetHitRadius + bulletHitRadius;
            if (((Vector2)(transform.position - target.transform.position)).sqrMagnitude <= allowedDistance * allowedDistance)
            {
                ReportHit();
            }
        }

        public void Shatter(Color color)
        {
            if (shattering) return;
            shattering = true;
            hitReported = true;
            Collider2D collider = GetComponent<Collider2D>();
            if (collider != null) collider.enabled = false;
            StartCoroutine(PlayShatter(color));
        }

        private IEnumerator PlayShatter(Color color)
        {
            SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
            Vector2 velocity = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1.5f, 4f);
            float spin = UnityEngine.Random.Range(-480f, 480f);
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            const float duration = 0.5f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position += (Vector3)velocity * Time.unscaledDeltaTime;
                transform.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
                transform.localScale = startScale * Mathf.Lerp(1.7f, 0.1f, t);
                if (renderer != null)
                {
                    Color faded = Color.Lerp(Color.white, color, t);
                    faded.a = 1f - t;
                    renderer.color = faded;
                }
                yield return null;
            }
            Destroy(gameObject);
        }

        private void ReportHit()
        {
            hitReported = true;
            onTargetHit?.Invoke();
            Destroy(gameObject);
        }
    }
}
