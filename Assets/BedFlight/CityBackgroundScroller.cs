using System.Collections.Generic;
using UnityEngine;

namespace BedFlight
{
    /// <summary>
    /// 「ベッドに乗って街の上を飛ぶ」感覚を出すための背景演出。
    /// 空の色（開放感の指標）と、近景のビル・遠景の雲の2層パララックスを管理する。
    /// 既製アセットは使わず、全て実行時にコードで生成した単色スプライトを使う。
    ///
    /// プレイヤー自身は画面内で上下左右に動くだけで前には進まないため（PlayerControllerと同じ方式）、
    /// 「飛んでいる」実感はこちらの背景スクロールだけで作る。
    /// </summary>
    public class CityBackgroundScroller : MonoBehaviour
    {
        [Header("空（開放感でLerpする背景色）")]
        [Tooltip("背景全体を覆う空のSpriteRenderer")]
        public SpriteRenderer sky;
        [Tooltip("開放フェーズ開始時（まだ疲れた雰囲気）の空の色")]
        public Color closedSkyColor = new Color(0.24f, 0.27f, 0.34f);
        [Tooltip("開放フェーズが進みきった時（気持ちよく飛べている）の空の色")]
        public Color openSkyColor = new Color(0.55f, 0.82f, 1f);
        [Tooltip("チラ見せフェーズが進みきった時に少し混ざる、不穏な色")]
        public Color tenseSkyColor = new Color(0.35f, 0.2f, 0.32f);

        private float openness = 0f;
        private float tension = 0f;

        [Header("ビル（近景）")]
        public int buildingCount = 14;
        public float buildingSpeed = 2.5f;
        public Vector2 buildingWidthRange = new Vector2(0.8f, 1.6f);
        public Vector2 buildingHeightRange = new Vector2(1.5f, 4.5f);
        [Tooltip("ビルの下端をそろえるY座標")]
        public float buildingBaseY = -4.4f;
        public Color buildingColorA = new Color(0.14f, 0.14f, 0.2f);
        public Color buildingColorB = new Color(0.2f, 0.19f, 0.28f);

        [Header("雲（遠景）")]
        public int cloudCount = 6;
        public float cloudSpeed = 0.7f;
        public Vector2 cloudYRange = new Vector2(1f, 3.6f);
        public Vector2 cloudWidthRange = new Vector2(1.4f, 2.6f);
        public Color cloudColor = new Color(1f, 1f, 1f, 0.8f);

        [Header("スクロール範囲（ワールド座標）")]
        public float leftBound = -12f;
        public float rightBound = 12f;

        private readonly List<Transform> buildings = new List<Transform>();
        private readonly List<Transform> clouds = new List<Transform>();
        private Sprite whiteSquare;

        void Awake()
        {
            whiteSquare = CreateWhiteSquareSprite();

            // skyはシーン側で見た目（大きさ・位置）だけ用意してもらい、
            // スプライト自体はここで割り当てる（空用のスプライトアセットを別途用意しなくて済むように）
            if (sky != null && sky.sprite == null)
            {
                sky.sprite = whiteSquare;
            }

            SpawnLayer(clouds, cloudCount, "Cloud", -2);
            SpawnLayer(buildings, buildingCount, "Building", -1);
            ApplySkyColor();
        }

        void Update()
        {
            Scroll(buildings, buildingSpeed);
            Scroll(clouds, cloudSpeed);
        }

        /// <summary>開放フェーズの進行度（0〜1）。空が徐々に明るく開けていく。</summary>
        public void SetOpenness(float t)
        {
            openness = Mathf.Clamp01(t);
            ApplySkyColor();
        }

        /// <summary>チラ見せフェーズの進行度（0〜1）。開放感を大きく壊さない程度に不穏な色を混ぜる。</summary>
        public void SetTension(float t)
        {
            tension = Mathf.Clamp01(t);
            ApplySkyColor();
        }

        private void ApplySkyColor()
        {
            if (sky == null) return;
            Color baseColor = Color.Lerp(closedSkyColor, openSkyColor, openness);
            sky.color = Color.Lerp(baseColor, tenseSkyColor, tension * 0.5f);
        }

        private void SpawnLayer(List<Transform> list, int count, string name, int sortingOrder)
        {
            float span = rightBound - leftBound;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"{name}{i}", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = whiteSquare;
                sr.sortingOrder = sortingOrder;

                float x = leftBound + span * (i + Random.value) / count;
                PlaceRandomly(go.transform, sr, name);
                go.transform.position = new Vector3(x, go.transform.position.y, 0f);

                list.Add(go.transform);
            }
        }

        private void PlaceRandomly(Transform t, SpriteRenderer sr, string name)
        {
            if (name == "Building")
            {
                float w = Random.Range(buildingWidthRange.x, buildingWidthRange.y);
                float h = Random.Range(buildingHeightRange.x, buildingHeightRange.y);
                t.localScale = new Vector3(w, h, 1f);
                t.position = new Vector3(t.position.x, buildingBaseY + h / 2f, 0f);
                sr.color = Random.value < 0.5f ? buildingColorA : buildingColorB;
            }
            else
            {
                float w = Random.Range(cloudWidthRange.x, cloudWidthRange.y);
                t.localScale = new Vector3(w, w * 0.4f, 1f);
                t.position = new Vector3(t.position.x, Random.Range(cloudYRange.x, cloudYRange.y), 0f);
                sr.color = cloudColor;
            }
        }

        private void Scroll(List<Transform> list, float speed)
        {
            foreach (var t in list)
            {
                Vector3 pos = t.position;
                pos.x -= speed * Time.deltaTime;

                float halfWidth = t.localScale.x / 2f;
                if (pos.x + halfWidth < leftBound)
                {
                    pos.x = rightBound + halfWidth;

                    // ループのたびに高さ・色をランダムに変え直し、単調な繰り返しに見えにくくする
                    bool isBuilding = t.name.StartsWith("Building");
                    var sr = t.GetComponent<SpriteRenderer>();
                    if (isBuilding)
                    {
                        float w = Random.Range(buildingWidthRange.x, buildingWidthRange.y);
                        float h = Random.Range(buildingHeightRange.x, buildingHeightRange.y);
                        t.localScale = new Vector3(w, h, 1f);
                        pos.y = buildingBaseY + h / 2f;
                        sr.color = Random.value < 0.5f ? buildingColorA : buildingColorB;
                    }
                    else
                    {
                        pos.y = Random.Range(cloudYRange.x, cloudYRange.y);
                    }
                }

                t.position = pos;
            }
        }

        private static Sprite CreateWhiteSquareSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }
    }
}
