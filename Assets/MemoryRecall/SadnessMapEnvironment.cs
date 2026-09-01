using UnityEngine;
using UnityEngine.Tilemaps;

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

        [Header("屋外の玄関")]
        [Tooltip("家の玄関へ入る領域。OutdoorDoorのBoxCollider2DをSceneビューで編集できる")]
        public BoxCollider2D outdoorHomeTrigger;
        [HideInInspector]
        public bool outdoorHomeMarkersAligned;

        [Header("移動可能範囲")]
        public Vector2 outdoorMinBounds;
        public Vector2 outdoorMaxBounds;
        public Vector2 homeMinBounds;
        public Vector2 homeMaxBounds;

        [Tooltip("室内の壁タイル。設定すると、室内マップ探索中はこの壁を通り抜けられなくなる")]
        public Tilemap homeWallTilemap;

        [Header("室内出口")]
        [Min(0.5f)]
        [Tooltip("下中央の出口として扱う横幅の半分")]
        public float homeExitHalfWidth = 2f;
        [Min(0.5f)]
        [Tooltip("室内マップ下端から、この距離以内へ入ったら出口として扱う")]
        public float homeExitDepth = 3f;
        [Tooltip("Sceneビューで編集できる、室内下中央の出口領域")]
        public BoxCollider2D homeExitTrigger;

        [Header("カメラ")]
        public Camera gameplayCamera;
        public bool followPlayer = true;
        [Tooltip("マップ探索中だけ適用するorthographic size（他の場面のカメラサイズには影響しない）")]
        public float mapOrthographicSize = 8f;
        [Tooltip("室内ではマップ全体が必ず画面内に収まるよう、自動的にカメラを引く")]
        public bool fitWholeHomeInView = true;
        [Min(0f)]
        [Tooltip("室内全景の上下左右に確保する余白（ワールド座標）")]
        public float homeCameraPadding = 1f;

        private AngerBattle.PlayerController followedPlayer;
        private Vector2 activeMinBounds;
        private Vector2 activeMaxBounds;
        private Vector3 originalCameraPosition;
        private float originalOrthographicSize;
        private bool cameraStateCaptured;

        private void Awake()
        {
            CaptureCameraState();
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
            CaptureCameraState();
            ApplyMapCameraSize(false);
            if (homeGrid != null) homeGrid.SetActive(false);
            if (outdoorGrid != null) outdoorGrid.SetActive(true);
            if (player != null) player.wallTilemap = null;
            ApplyArea(player, outdoorMinBounds, outdoorMaxBounds, moveToStart ? outdoorStart : null);
        }

        public void ShowHome(AngerBattle.PlayerController player, bool moveToStart)
        {
            CaptureCameraState();
            ApplyMapCameraSize(fitWholeHomeInView);
            if (outdoorGrid != null) outdoorGrid.SetActive(false);
            if (homeGrid != null) homeGrid.SetActive(true);
            if (player != null) player.wallTilemap = homeWallTilemap;
            ApplyArea(player, homeMinBounds, homeMaxBounds, moveToStart ? homeStart : null);
        }

        public void HideMaps(bool restoreCamera = true)
        {
            followedPlayer = null;
            if (outdoorGrid != null) outdoorGrid.SetActive(false);
            if (homeGrid != null) homeGrid.SetActive(false);

            if (restoreCamera && cameraStateCaptured && gameplayCamera != null)
            {
                gameplayCamera.transform.position = originalCameraPosition;
                gameplayCamera.orthographicSize = originalOrthographicSize;
            }
        }

        /// <summary>
        /// 室内下中央の張り出した通路に入ったかを判定する。
        /// 点マーカーとの距離だけにすると、Tilemapの見た目と計算上の下端が少し違うだけで
        /// 出られなくなるため、出口は横幅と奥行きを持つ領域として扱う。
        /// </summary>
        public bool IsAtHomeExit(Transform actor)
        {
            if (actor == null)
            {
                return false;
            }

            if (homeExitTrigger != null)
            {
                return homeExitTrigger.OverlapPoint(actor.position);
            }

            float centerX = homeDoor != null
                ? homeDoor.position.x
                : (homeMinBounds.x + homeMaxBounds.x) * 0.5f;
            float exitThresholdY = homeMinBounds.y + Mathf.Max(0.5f, homeExitDepth);

            return actor.position.y <= exitThresholdY &&
                   Mathf.Abs(actor.position.x - centerX) <= Mathf.Max(0.5f, homeExitHalfWidth);
        }

        /// <summary>帰宅可能中に、屋外の家の玄関領域へ入ったかを判定する。</summary>
        public bool IsAtOutdoorHomeEntrance(Transform actor)
        {
            if (actor == null)
            {
                return false;
            }

            if (outdoorHomeTrigger != null)
            {
                return outdoorHomeTrigger.OverlapPoint(actor.position);
            }

            return outdoorDoor != null &&
                   Vector2.Distance(actor.position, outdoorDoor.position) <= 1.5f;
        }

        private void OnDrawGizmos()
        {
            if (homeExitTrigger == null)
            {
                return;
            }

            Bounds bounds = homeExitTrigger.bounds;
            Gizmos.color = new Color(0.1f, 1f, 0.85f, 0.95f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Gizmos.DrawSphere(homeExitTrigger.transform.position, 0.14f);

            if (outdoorHomeTrigger != null)
            {
                Bounds outdoorBounds = outdoorHomeTrigger.bounds;
                Gizmos.color = new Color(1f, 0.72f, 0.1f, 0.95f);
                Gizmos.DrawWireCube(outdoorBounds.center, outdoorBounds.size);
                Gizmos.DrawSphere(outdoorHomeTrigger.transform.position, 0.14f);

                if (outdoorStart != null)
                {
                    Gizmos.color = new Color(0.25f, 1f, 0.25f, 0.95f);
                    Gizmos.DrawSphere(outdoorStart.position, 0.22f);
                    Gizmos.DrawLine(outdoorStart.position, outdoorHomeTrigger.transform.position);
                }
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

        private void CaptureCameraState()
        {
            if (cameraStateCaptured || gameplayCamera == null)
            {
                return;
            }

            originalCameraPosition = gameplayCamera.transform.position;
            originalOrthographicSize = gameplayCamera.orthographicSize;
            cameraStateCaptured = true;
        }

        private void ApplyMapCameraSize(bool fitWholeArea)
        {
            if (gameplayCamera == null)
            {
                return;
            }

            float requestedSize = mapOrthographicSize;
            if (fitWholeArea && gameplayCamera.aspect > 0f)
            {
                float width = Mathf.Max(0f, homeMaxBounds.x - homeMinBounds.x) + homeCameraPadding * 2f;
                float height = Mathf.Max(0f, homeMaxBounds.y - homeMinBounds.y) + homeCameraPadding * 2f;
                float sizeForWidth = width / (2f * gameplayCamera.aspect);
                float sizeForHeight = height * 0.5f;
                requestedSize = Mathf.Max(requestedSize, sizeForWidth, sizeForHeight);
            }

            gameplayCamera.orthographicSize = requestedSize;
        }
    }
}
