using UnityEngine;

namespace BedFlight
{
    /// <summary>
    /// 画面上部に吊られたベビーメリーを、カメラに追従させながらごく小さく揺らす。
    /// UIではなくワールド上のSpriteRendererとして扱い、背景の一部らしさを保つ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BabyMobileAmbient : MonoBehaviour
    {
        [Header("カメラ追従")]
        [Tooltip("未設定の場合はMain Cameraを使用する")]
        public Camera targetCamera;
        [Tooltip("画面内の基準位置。Yを1付近にすると上端から吊られて見える")]
        public Vector2 viewportAnchor = new Vector2(0.68f, 1.01f);
        [Tooltip("メリーを置くワールド座標のZ")]
        public float worldPlaneZ = 0f;

        [Header("見た目（Inspectorから調整）")]
        [Tooltip("メリー全体の大きさ")]
        [Min(0.01f)] public float visualScale = 0.65f;
        [Tooltip("雲より小さい値にすると雲の後ろへ回る。現在の雲は-5")]
        public int sortingOrder = -6;
        [Tooltip("吊り紐とメリー本体へ共通で掛ける色")]
        public Color visualTint = new Color32(38, 50, 74, 220);
        [Tooltip("吊り紐のSpriteRenderer")]
        public SpriteRenderer cordRenderer;
        [Tooltip("メリー本体のSpriteRenderer")]
        public SpriteRenderer mobileRenderer;

        [Header("揺れの対象")]
        [Tooltip("天井側を支点に揺れる吊り紐のTransform")]
        public Transform cordPivot;
        [Tooltip("紐の下端を支点に、少し遅れて揺れるメリー本体のTransform")]
        public Transform mobilePivot;

        [Header("揺れ幅")]
        [Min(0f)] public float cordAngle = 0.35f;
        [Min(0f)] public float mobileAngle = 0.8f;
        [Min(0f)] public float swayFrequency = 0.18f;
        [Min(0f)] public float bobAmplitude = 0.05f;
        [Min(0f)] public float bobFrequency = 0.14f;
        [Tooltip("紐に対して本体の揺れが少し遅れる量（ラジアン）")]
        public float mobilePhaseLag = 0.55f;

        private Quaternion cordBaseRotation;
        private Quaternion mobileBaseRotation;
        private Vector3 mobileBasePosition;
        private float elapsed;
        private bool basePoseCaptured;

        private void Awake()
        {
            ApplyVisualSettings();
            CaptureBasePose();
        }

        private void OnValidate()
        {
            ApplyVisualSettings();
        }

        private void OnEnable()
        {
            elapsed = 0f;
            if (!basePoseCaptured)
            {
                CaptureBasePose();
            }
            else
            {
                ResetToBasePose();
            }
            FollowCamera();
        }

        private void OnDisable()
        {
            ResetToBasePose();
        }

        private void LateUpdate()
        {
            FollowCamera();
            elapsed += Time.deltaTime;

            float swayPhase = elapsed * swayFrequency * Mathf.PI * 2f;
            if (cordPivot != null)
            {
                float angle = Mathf.Sin(swayPhase) * cordAngle;
                cordPivot.localRotation = cordBaseRotation * Quaternion.Euler(0f, 0f, angle);
            }

            if (mobilePivot != null)
            {
                float angle = Mathf.Sin(swayPhase - mobilePhaseLag) * mobileAngle;
                mobilePivot.localRotation = mobileBaseRotation * Quaternion.Euler(0f, 0f, angle);

                float bobPhase = elapsed * bobFrequency * Mathf.PI * 2f;
                Vector3 position = mobileBasePosition;
                position.y += Mathf.Sin(bobPhase) * bobAmplitude;
                mobilePivot.localPosition = position;
            }
        }

        private void CaptureBasePose()
        {
            if (cordPivot != null) cordBaseRotation = cordPivot.localRotation;
            if (mobilePivot != null)
            {
                mobileBaseRotation = mobilePivot.localRotation;
                mobileBasePosition = mobilePivot.localPosition;
            }
            basePoseCaptured = true;
        }

        private void ResetToBasePose()
        {
            if (!basePoseCaptured) return;
            if (cordPivot != null) cordPivot.localRotation = cordBaseRotation;
            if (mobilePivot != null)
            {
                mobilePivot.localRotation = mobileBaseRotation;
                mobilePivot.localPosition = mobileBasePosition;
            }
        }

        private void FollowCamera()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            float depth = worldPlaneZ - targetCamera.transform.position.z;
            Vector3 world = targetCamera.ViewportToWorldPoint(
                new Vector3(viewportAnchor.x, viewportAnchor.y, depth));
            world.z = worldPlaneZ;
            transform.position = world;
        }

        [ContextMenu("見た目設定を反映")]
        public void ApplyVisualSettings()
        {
            if (cordPivot != null)
            {
                cordPivot.localScale = Vector3.one * Mathf.Max(0.01f, visualScale);
            }

            if (cordRenderer != null)
            {
                cordRenderer.sortingOrder = sortingOrder;
                cordRenderer.color = visualTint;
            }

            if (mobileRenderer != null)
            {
                mobileRenderer.sortingOrder = sortingOrder;
                mobileRenderer.color = visualTint;
            }
        }
    }
}
