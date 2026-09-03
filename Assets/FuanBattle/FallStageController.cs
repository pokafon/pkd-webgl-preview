using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AngerBattle
{
    /// <summary>
    /// 不安戦の穴に落ちた後の短い縦落下ミニゲーム（試作）。
    /// コンタックは自動で下方向へ落下し、プレイヤーは左右移動のみ行う。
    /// 最下部でYES／NOへ分岐し、プレイヤーの最終X位置を回答として返す。
    /// 仮素材（単色矩形）・簡易障害物のみ。ヒント収集・波動エフェクト・複雑な障害物は今回入れない。
    /// </summary>
    public sealed class FallStageController : MonoBehaviour
    {
        private const int ObstacleCount = 3;
        private static readonly Color BackgroundColor = new Color(0.05f, 0.05f, 0.09f, 1f);
        private static readonly Color ObstacleColor = new Color(0.55f, 0.20f, 0.24f, 1f);
        private static readonly Color YesZoneColor = new Color(0.16f, 0.30f, 0.46f, 1f);
        private static readonly Color NoZoneColor = new Color(0.34f, 0.20f, 0.42f, 1f);
        private static readonly Color PlayerFlashColor = new Color(1f, 0.85f, 0.85f, 1f);

        private static Sprite solidSprite;

        private Camera fallCamera;
        private PlayerController player;
        private Transform playerTransform;
        private SpriteRenderer playerRenderer;
        private Color playerOriginalColor = Color.white;

        private float autoDescendSpeed = 3.2f;
        private float playerMoveSpeed = 6f;
        private float slowDuration = 1.5f;
        private float slowCooldown = 3f;
        private float slowFactor = 0.35f;
        private float fallDistance = 11f;
        private float corridorHalfWidth = 2.2f;
        private TMP_FontAsset font;

        private GameObject root;
        private SpriteRenderer backgroundRenderer;
        private SpriteRenderer yesZoneRenderer;
        private SpriteRenderer noZoneRenderer;
        private TextMeshPro yesLabel;
        private TextMeshPro noLabel;
        private readonly List<SpriteRenderer> obstacleRenderers = new List<SpriteRenderer>();
        private readonly List<float> obstacleSpawnDistance = new List<float>();
        private readonly List<float> obstacleLocalX = new List<float>();
        private readonly List<float> obstacleHalfWidth = new List<float>();

        private Vector3 anchor;
        private Vector3 capturedCameraPosition;
        private float capturedCameraSize;
        private Quaternion capturedCameraRotation;

        public bool IsReady => fallCamera != null && playerTransform != null;

        public void Configure(
            Camera requestedCamera,
            PlayerController requestedPlayer,
            TMP_FontAsset requestedFont,
            float requestedAutoDescendSpeed,
            float requestedPlayerMoveSpeed,
            float requestedSlowDuration,
            float requestedSlowCooldown,
            float requestedSlowFactor,
            float requestedFallDistance,
            float requestedCorridorHalfWidth)
        {
            fallCamera = requestedCamera;
            player = requestedPlayer;
            playerTransform = player != null ? player.transform : null;
            playerRenderer = playerTransform != null ? playerTransform.GetComponent<SpriteRenderer>() : null;
            font = requestedFont;
            autoDescendSpeed = Mathf.Max(0.5f, requestedAutoDescendSpeed);
            playerMoveSpeed = Mathf.Max(0.5f, requestedPlayerMoveSpeed);
            slowDuration = Mathf.Max(0.1f, requestedSlowDuration);
            slowCooldown = Mathf.Max(0f, requestedSlowCooldown);
            slowFactor = Mathf.Clamp(requestedSlowFactor, 0.05f, 1f);
            fallDistance = Mathf.Max(2f, requestedFallDistance);
            corridorHalfWidth = Mathf.Max(0.5f, requestedCorridorHalfWidth);

            EnsureVisuals();
        }

        /// <summary>穴へ落ちてから、YES／NOのどちらへ入ったかを返すまでの落下シーケンスを再生する。</summary>
        public IEnumerator PlayFall(Action<string> onAnswer)
        {
            if (!IsReady)
            {
                onAnswer?.Invoke("YES");
                yield break;
            }

            CaptureCameraState();
            anchor = capturedCameraPosition + new Vector3(0f, -60f, 0f);
            LayoutCamera();
            SpawnObstacles();
            root.SetActive(true);

            if (player != null)
            {
                player.enabled = false;
            }
            if (playerRenderer != null)
            {
                playerOriginalColor = playerRenderer.color;
            }

            float playerWorldY = anchor.y + fallDistance * 0.5f;
            float traveled = 0f;
            float playerX = 0f;
            float slowTimer = 0f;
            float cooldownTimer = 0f;
            float flashTimer = 0f;

            playerTransform.position = new Vector3(anchor.x, playerWorldY, playerTransform.position.z);

            while (traveled < fallDistance)
            {
                float dt = Time.unscaledDeltaTime;

                if (cooldownTimer > 0f)
                {
                    cooldownTimer -= dt;
                }
                if (slowTimer > 0f)
                {
                    slowTimer -= dt;
                }
                else if (Input.GetKeyDown(KeyCode.Space) && cooldownTimer <= 0f)
                {
                    slowTimer = slowDuration;
                    cooldownTimer = slowCooldown + slowDuration;
                }

                float descendMultiplier = slowTimer > 0f ? slowFactor : 1f;
                traveled = Mathf.Min(fallDistance, traveled + autoDescendSpeed * descendMultiplier * dt);

                float h = Input.GetAxisRaw("Horizontal");
                float maxX = corridorHalfWidth - 0.3f;
                playerX = Mathf.Clamp(playerX + h * playerMoveSpeed * dt, -maxX, maxX);
                playerTransform.position = new Vector3(anchor.x + playerX, playerWorldY, playerTransform.position.z);

                if (flashTimer > 0f)
                {
                    flashTimer -= dt;
                    if (playerRenderer != null)
                    {
                        playerRenderer.color = flashTimer > 0f ? PlayerFlashColor : playerOriginalColor;
                    }
                }
                else if (IsTouchingObstacle(playerX, traveled))
                {
                    flashTimer = 0.18f;
                }

                UpdateObstacles(traveled, playerWorldY);
                UpdateZoneVisibility(traveled, playerWorldY);

                yield return null;
            }

            if (playerRenderer != null)
            {
                playerRenderer.color = playerOriginalColor;
            }

            string answer = playerX < 0f ? "YES" : "NO";
            root.SetActive(false);
            RestoreCamera();
            onAnswer?.Invoke(answer);
        }

        private bool IsTouchingObstacle(float playerX, float traveled)
        {
            const float proximity = 0.35f;
            for (int i = 0; i < obstacleSpawnDistance.Count; i++)
            {
                if (Mathf.Abs(obstacleSpawnDistance[i] - traveled) > proximity)
                {
                    continue;
                }
                if (Mathf.Abs(playerX - obstacleLocalX[i]) <= obstacleHalfWidth[i] + 0.28f)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateObstacles(float traveled, float playerWorldY)
        {
            for (int i = 0; i < obstacleRenderers.Count; i++)
            {
                float worldY = playerWorldY - (obstacleSpawnDistance[i] - traveled);
                obstacleRenderers[i].transform.position = new Vector3(anchor.x + obstacleLocalX[i], worldY, 0f);
            }
        }

        private void UpdateZoneVisibility(float traveled, float playerWorldY)
        {
            const float zoneApproach = 1.6f;
            bool zonesVisible = traveled >= fallDistance - zoneApproach;
            if (yesZoneRenderer != null) yesZoneRenderer.gameObject.SetActive(zonesVisible);
            if (noZoneRenderer != null) noZoneRenderer.gameObject.SetActive(zonesVisible);
            if (yesLabel != null) yesLabel.gameObject.SetActive(zonesVisible);
            if (noLabel != null) noLabel.gameObject.SetActive(zonesVisible);
            if (!zonesVisible)
            {
                return;
            }

            float zoneWorldY = playerWorldY - (fallDistance - traveled);
            float zoneWidth = corridorHalfWidth;
            if (yesZoneRenderer != null)
            {
                yesZoneRenderer.transform.position = new Vector3(anchor.x - zoneWidth * 0.5f, zoneWorldY, 0f);
            }
            if (noZoneRenderer != null)
            {
                noZoneRenderer.transform.position = new Vector3(anchor.x + zoneWidth * 0.5f, zoneWorldY, 0f);
            }
            if (yesLabel != null)
            {
                yesLabel.transform.position = new Vector3(anchor.x - zoneWidth * 0.5f, zoneWorldY, -0.1f);
            }
            if (noLabel != null)
            {
                noLabel.transform.position = new Vector3(anchor.x + zoneWidth * 0.5f, zoneWorldY, -0.1f);
            }
        }

        private void SpawnObstacles()
        {
            obstacleSpawnDistance.Clear();
            obstacleLocalX.Clear();
            obstacleHalfWidth.Clear();

            float usableDistance = Mathf.Max(1f, fallDistance - 3f);
            for (int i = 0; i < obstacleRenderers.Count; i++)
            {
                float spawnDistance = usableDistance * (i + 1) / (ObstacleCount + 1) + 1f;
                float halfWidth = 0.55f;
                float maxOffset = Mathf.Max(0.2f, corridorHalfWidth - halfWidth - 0.3f);
                float side = i % 2 == 0 ? 1f : -1f;
                float localX = side * maxOffset * (0.5f + 0.5f * ((i * 37) % 5) / 5f);

                obstacleSpawnDistance.Add(spawnDistance);
                obstacleLocalX.Add(localX);
                obstacleHalfWidth.Add(halfWidth);

                obstacleRenderers[i].transform.localScale = new Vector3(halfWidth * 2f, 0.4f, 1f);
                obstacleRenderers[i].gameObject.SetActive(true);
            }
        }

        private void LayoutCamera()
        {
            fallCamera.transform.position = new Vector3(anchor.x, anchor.y, capturedCameraPosition.z);
            fallCamera.transform.rotation = Quaternion.identity;
            float requiredHalfWidth = corridorHalfWidth + 0.6f;
            fallCamera.orthographicSize = Mathf.Max(3.6f, requiredHalfWidth / Mathf.Max(0.4f, fallCamera.aspect));

            if (backgroundRenderer != null)
            {
                backgroundRenderer.transform.position = new Vector3(anchor.x, anchor.y, 0.2f);
                backgroundRenderer.transform.localScale = new Vector3(corridorHalfWidth * 2f, fallDistance + 4f, 1f);
            }
        }

        private void CaptureCameraState()
        {
            capturedCameraPosition = fallCamera.transform.position;
            capturedCameraSize = fallCamera.orthographicSize;
            capturedCameraRotation = fallCamera.transform.rotation;
        }

        private void RestoreCamera()
        {
            fallCamera.transform.position = capturedCameraPosition;
            fallCamera.orthographicSize = capturedCameraSize;
            fallCamera.transform.rotation = capturedCameraRotation;
        }

        private void EnsureVisuals()
        {
            if (root != null)
            {
                return;
            }

            root = new GameObject("FallStageVisuals");
            root.transform.SetParent(transform, false);

            backgroundRenderer = CreateSolidRenderer("FallBackground", BackgroundColor, 30);
            yesZoneRenderer = CreateSolidRenderer("FallYesZone", YesZoneColor, 32);
            noZoneRenderer = CreateSolidRenderer("FallNoZone", NoZoneColor, 32);
            yesZoneRenderer.transform.localScale = new Vector3(corridorHalfWidth, 1.4f, 1f);
            noZoneRenderer.transform.localScale = new Vector3(corridorHalfWidth, 1.4f, 1f);

            if (font != null)
            {
                yesLabel = CreateLabel("YES", new Color(0.72f, 0.86f, 1f, 1f));
                noLabel = CreateLabel("NO", new Color(0.90f, 0.76f, 1f, 1f));
            }

            for (int i = 0; i < ObstacleCount; i++)
            {
                SpriteRenderer obstacle = CreateSolidRenderer($"FallObstacle{i:00}", ObstacleColor, 33);
                obstacle.gameObject.SetActive(false);
                obstacleRenderers.Add(obstacle);
            }

            root.SetActive(false);
        }

        private SpriteRenderer CreateSolidRenderer(string objectName, Color color, int sortingOrder)
        {
            GameObject obj = new GameObject(objectName, typeof(SpriteRenderer));
            obj.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
            renderer.sprite = GetSolidSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private TextMeshPro CreateLabel(string text, Color color)
        {
            GameObject obj = new GameObject(text + "FallLabel", typeof(TextMeshPro));
            obj.transform.SetParent(root.transform, false);
            TextMeshPro label = obj.GetComponent<TextMeshPro>();
            label.text = text;
            label.font = font;
            label.fontSize = 4.4f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = color;
            label.characterSpacing = 8f;
            label.rectTransform.sizeDelta = new Vector2(4.5f, 1.6f);
            MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = 34;
            label.gameObject.SetActive(false);
            return label;
        }

        private static Sprite GetSolidSprite()
        {
            if (solidSprite != null)
            {
                return solidSprite;
            }

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                name = "FallStageSolidTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            solidSprite.name = "FallStageSolidSprite";
            return solidSprite;
        }
    }
}
