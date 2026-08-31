using UnityEngine;

namespace MemoryRecall
{
    /// <summary>
    /// 悲しみ編で共有する屋外／屋内Tilemapとカメラを管理する。
    /// 記憶回想（こども）と悲しみバトル（コンタック）の双方が同じマップを使う。
    /// </summary>
    public class SadnessMapEnvironment : MonoBehaviour
    {
        [Header("マップ")]
        public GameObject outdoorGrid;
        public GameObject homeGrid;

        [Header("移動マーカー")]
        public Transform outdoorStart;
        public Transform outdoorDoor;
        public Transform homeStart;
        public Transform homeDoor;
        public Transform[] outdoorFriendSpots;
        public Transform homeMotherSpot;

        [Header("移動可能範囲")]
        public Vector2 outdoorMinBounds;
        public Vector2 outdoorMaxBounds;
        public Vector2 homeMinBounds;
        public Vector2 homeMaxBounds;

        [Header("カメラ")]
        public Camera gameplayCamera;
        public bool followPlayer = true;

        private AngerBattle.PlayerController followedPlayer;
        private Vector2 activeMinBounds;
        private Vector2 activeMaxBounds;
        private Vector3 originalCameraPosition;
        private bool cameraPositionCaptured;

        private void Awake()
        {
            CaptureCameraPosition();
            HideMaps(false);
        }

        private void LateUpdate()
        {
            if (!followPlayer || followedPlayer == null || gameplayCamera == null)
            {
                return;
            }

            float halfHeight = gameplayCamera.orthographicSize;
            float halfWidth = halfHeight * gameplayCamera.aspect;
            float minX = activeMinBounds.x + halfWidth;
            float maxX = activeMaxBounds.x - halfWidth;
            float minY = activeMinBounds.y + halfHeight;
            float maxY = activeMaxBounds.y - halfHeight;

            float x = minX <= maxX
                ? Mathf.Clamp(followedPlayer.transform.position.x, minX, maxX)
                : (activeMinBounds.x + activeMaxBounds.x) * 0.5f;
            float y = minY <= maxY
                ? Mathf.Clamp(followedPlayer.transform.position.y, minY, maxY)
                : (activeMinBounds.y + activeMaxBounds.y) * 0.5f;

            Vector3 current = gameplayCamera.transform.position;
            gameplayCamera.transform.position = new Vector3(x, y, current.z);
        }

        public void ShowOutdoor(AngerBattle.PlayerController player, bool moveToStart)
        {
            CaptureCameraPosition();
            if (homeGrid != null) homeGrid.SetActive(false);
            if (outdoorGrid != null) outdoorGrid.SetActive(true);
            ApplyArea(player, outdoorMinBounds, outdoorMaxBounds, moveToStart ? outdoorStart : null);
        }

        public void ShowHome(AngerBattle.PlayerController player, bool moveToStart)
        {
            CaptureCameraPosition();
            if (outdoorGrid != null) outdoorGrid.SetActive(false);
            if (homeGrid != null) homeGrid.SetActive(true);
            ApplyArea(player, homeMinBounds, homeMaxBounds, moveToStart ? homeStart : null);
        }

        public void HideMaps(bool restoreCamera = true)
        {
            followedPlayer = null;
            if (outdoorGrid != null) outdoorGrid.SetActive(false);
            if (homeGrid != null) homeGrid.SetActive(false);

            if (restoreCamera && cameraPositionCaptured && gameplayCamera != null)
            {
                gameplayCamera.transform.position = originalCameraPosition;
            }
        }

        private void ApplyArea(
            AngerBattle.PlayerController player,
            Vector2 minBounds,
            Vector2 maxBounds,
            Transform startPoint)
        {
            followedPlayer = player;
            activeMinBounds = minBounds;
            activeMaxBounds = maxBounds;

            if (player != null)
            {
                player.minBounds = minBounds;
                player.maxBounds = maxBounds;
                if (startPoint != null)
                {
                    player.transform.position = startPoint.position;
                }
            }

            LateUpdate();
        }

        private void CaptureCameraPosition()
        {
            if (cameraPositionCaptured || gameplayCamera == null)
            {
                return;
            }

            originalCameraPosition = gameplayCamera.transform.position;
            cameraPositionCaptured = true;
        }
    }
}
