using System.Collections.Generic;
using UnityEngine;

namespace BedFlight
{
    /// <summary>
    /// 「ベッドに乗って街の上を飛ぶ」感覚を出すための背景演出。
    /// 空の色（開放感の指標）と、近景の街・遠景の街・雲の3層パララックスを管理する。
    /// 提供素材が設定されていればそれを使い、未設定なら従来の単色スプライトへフォールバックする。
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

        private float openness = 0f;

        [Header("提供された背景素材（3層パララックス）")]
        [Tooltip("街の前景。横長の1枚絵を複製して継ぎ目なく流す")]
        public Sprite foregroundCitySprite;
        [Tooltip("街の後景。切り出された建物スプライトを並べて流す")]
        public Sprite[] backgroundCitySprites;
        [Tooltip("切り出された雲スプライト。極小のノイズ片はシーン構築時に除外する")]
        public Sprite[] suppliedCloudSprites;
        public float foregroundCitySpeed = 2.5f;
        public float backgroundCitySpeed = 1.2f;
        public float foregroundCityBaseY = -4.4f;
        public float backgroundCityBaseY = -4.15f;
        public float foregroundCityScale = 1f;
        public Vector2 backgroundCityScaleRange = new Vector2(1.15f, 1.45f);
        public int backgroundCityCount = 24;
        public int foregroundSortingOrder = -3;
        public int backgroundSortingOrder = -4;
        public int suppliedCloudSortingOrder = -5;

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
        [Tooltip("画面外で先に待機させる距離。横長画面でも画面内に突然生成されないようにする")]
        public float offscreenPreloadPadding = 6f;

        [Header("除外ゾーン（この範囲にはビルを初期配置しない。任意）")]
        [Tooltip("HouseIntro（開始演出の家）とビルが重ならないようにするための設定。" +
            "家とビルは同じ速度でスクロールするため、初期配置さえ避ければ以後もずっと重ならない")]
        public bool hasBuildingExcludeZone = false;
        public float buildingExcludeZoneMinX = 0f;
        public float buildingExcludeZoneMaxX = 0f;

        private readonly List<Transform> buildings = new List<Transform>();
        private readonly List<Transform> clouds = new List<Transform>();
        private readonly List<Transform> foregroundCity = new List<Transform>();
        private readonly List<Transform> backgroundCity = new List<Transform>();
        private readonly List<Transform> suppliedClouds = new List<Transform>();
        private Sprite whiteSquare;
        private bool usingSuppliedArt;
        private float suppliedLeftBound;
        private float suppliedRightBound;

        void Awake()
        {
            whiteSquare = CreateWhiteSquareSprite();

            // skyはシーン側で見た目（大きさ・位置）だけ用意してもらい、
            // スプライト自体はここで割り当てる（空用のスプライトアセットを別途用意しなくて済むように）
            if (sky != null && sky.sprite == null)
            {
                sky.sprite = whiteSquare;
            }

            usingSuppliedArt = HasCompleteSuppliedArt();
            if (usingSuppliedArt)
            {
                CalculateSuppliedScrollBounds();
                SpawnSuppliedArt();
            }
            else
            {
                SpawnLayer(clouds, cloudCount, "Cloud", -2);
                SpawnLayer(buildings, buildingCount, "Building", -1);
            }
            ApplySkyColor();
        }

        // クライマックスでプレイヤー操作を止めるのと合わせて、背景のスクロールも止める（既定はtrue）
        private bool scrolling = true;

        void Update()
        {
            if (!scrolling) return;

            if (usingSuppliedArt)
            {
                ScrollSuppliedLayer(foregroundCity, foregroundCitySpeed, false);
                ScrollSuppliedLayer(backgroundCity, backgroundCitySpeed, false);
                ScrollSuppliedLayer(suppliedClouds, cloudSpeed, true);
            }
            else
            {
                Scroll(buildings, buildingSpeed);
                Scroll(clouds, cloudSpeed);
            }
        }

        /// <summary>ビル・雲のスクロールを止める/再開する（クライマックスでプレイヤーが止まるのに合わせる用）。</summary>
        public void SetScrolling(bool value)
        {
            scrolling = value;
        }

        /// <summary>開放フェーズの進行度（0〜1）。空が徐々に明るく開けていく。</summary>
        public void SetOpenness(float t)
        {
            openness = Mathf.Clamp01(t);
            ApplySkyColor();
        }

        private void ApplySkyColor()
        {
            if (sky == null) return;
            sky.color = Color.Lerp(closedSkyColor, openSkyColor, openness);
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
                if (name == "Building" && hasBuildingExcludeZone)
                {
                    x = AvoidExcludeZone(x, span);
                }
                PlaceRandomly(go.transform, sr, name);
                go.transform.position = new Vector3(x, go.transform.position.y, 0f);

                list.Add(go.transform);
            }
        }

        /// <summary>
        /// 除外ゾーンに、通常のビルと同じ見た目で1棟追加する。
        /// 除外ゾーンは初期配置時にビルの生成を避けるためだけのものなので、家が飛び去った後もそのまま
        /// 放置すると「家も無いのにビルだけ生えていない隙間」がずっと残ってしまう。
        /// 家が飛び立つ瞬間（HouseIntro.StartScrolling呼び出しと同時）にこれを呼び、その場にビルを
        /// 生成して隙間を埋める。以後は除外ゾーンを無効化し、通常のリサイクルに任せる。
        /// </summary>
        public void FillExcludeZone()
        {
            if (!hasBuildingExcludeZone) return;

            // 提供素材の前景は途切れない横長画像なので、家の跡を埋める追加生成は不要。
            if (usingSuppliedArt)
            {
                hasBuildingExcludeZone = false;
                return;
            }

            var go = new GameObject("Building_Fill", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = whiteSquare;
            sr.sortingOrder = -1;

            float x = Mathf.Lerp(buildingExcludeZoneMinX, buildingExcludeZoneMaxX, 0.5f);
            PlaceRandomly(go.transform, sr, "Building");
            go.transform.position = new Vector3(x, go.transform.position.y, 0f);

            buildings.Add(go.transform);
            hasBuildingExcludeZone = false;
        }

        /// <summary>xが除外ゾーンに入っている間、範囲内でランダムに引き直す（家とビルの初期配置が重ならないようにする）。</summary>
        private float AvoidExcludeZone(float x, float span)
        {
            int guard = 0;
            while (x >= buildingExcludeZoneMinX && x <= buildingExcludeZoneMaxX && guard < 20)
            {
                x = leftBound + span * Random.value;
                guard++;
            }
            return x;
        }

        private bool HasCompleteSuppliedArt()
        {
            return foregroundCitySprite != null &&
                backgroundCitySprites != null && backgroundCitySprites.Length > 0 &&
                suppliedCloudSprites != null && suppliedCloudSprites.Length > 0;
        }

        private void SpawnSuppliedArt()
        {
            SpawnForegroundCity();
            SpawnBackgroundCity();
            SpawnSuppliedClouds();
        }

        private void CalculateSuppliedScrollBounds()
        {
            suppliedLeftBound = leftBound;
            suppliedRightBound = rightBound;

            Camera targetCamera = Camera.main;
            if (targetCamera == null || !targetCamera.orthographic) return;

            float cameraHalfWidth = targetCamera.orthographicSize * targetCamera.aspect;
            float padding = Mathf.Max(0f, offscreenPreloadPadding);
            suppliedLeftBound = Mathf.Min(
                suppliedLeftBound,
                targetCamera.transform.position.x - cameraHalfWidth - padding);
            suppliedRightBound = Mathf.Max(
                suppliedRightBound,
                targetCamera.transform.position.x + cameraHalfWidth + padding);
        }

        private void SpawnForegroundCity()
        {
            float width = foregroundCitySprite.bounds.size.x * foregroundCityScale;
            if (width <= 0f) return;

            float span = suppliedRightBound - suppliedLeftBound;
            int count = Mathf.Max(2, Mathf.CeilToInt(span / width) + 2);
            for (int i = 0; i < count; i++)
            {
                var go = CreateSuppliedSpriteObject(
                    $"ForegroundCity{i}", foregroundCitySprite, foregroundSortingOrder);
                go.transform.localScale = Vector3.one * foregroundCityScale;

                float x = suppliedLeftBound + width * (i + 0.5f);
                float y = foregroundCityBaseY +
                    foregroundCitySprite.bounds.extents.y * foregroundCityScale;
                go.transform.position = new Vector3(x, y, 0f);
                foregroundCity.Add(go.transform);
            }
        }

        private void SpawnBackgroundCity()
        {
            float baseSpan = Mathf.Max(0.01f, rightBound - leftBound);
            float span = suppliedRightBound - suppliedLeftBound;
            int count = Mathf.Max(
                1,
                Mathf.CeilToInt(backgroundCityCount * span / baseSpan));
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = backgroundCitySprites[i % backgroundCitySprites.Length];
                if (sprite == null) continue;

                var go = CreateSuppliedSpriteObject(
                    $"BackgroundCity{i}", sprite, backgroundSortingOrder);
                float scale = Random.Range(backgroundCityScaleRange.x, backgroundCityScaleRange.y);
                go.transform.localScale = Vector3.one * scale;

                float x = suppliedLeftBound + span * (i + 0.5f) / count;
                float y = backgroundCityBaseY + sprite.bounds.extents.y * scale;
                go.transform.position = new Vector3(x, y, 0f);
                backgroundCity.Add(go.transform);
            }
        }

        private void SpawnSuppliedClouds()
        {
            float baseSpan = Mathf.Max(0.01f, rightBound - leftBound);
            float span = suppliedRightBound - suppliedLeftBound;
            int count = Mathf.Max(1, Mathf.CeilToInt(cloudCount * span / baseSpan));
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = suppliedCloudSprites[i % suppliedCloudSprites.Length];
                if (sprite == null) continue;

                var go = CreateSuppliedSpriteObject(
                    $"Cloud{i}", sprite, suppliedCloudSortingOrder);
                float targetWidth = Random.Range(cloudWidthRange.x, cloudWidthRange.y);
                float nativeWidth = Mathf.Max(0.01f, sprite.bounds.size.x);
                float scale = targetWidth / nativeWidth;
                go.transform.localScale = Vector3.one * scale;

                float x = suppliedLeftBound + span * (i + Random.value) / count;
                float y = Random.Range(cloudYRange.x, cloudYRange.y);
                go.transform.position = new Vector3(x, y, 0f);
                go.GetComponent<SpriteRenderer>().color = cloudColor;
                suppliedClouds.Add(go.transform);
            }
        }

        private GameObject CreateSuppliedSpriteObject(string objectName, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(objectName, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return go;
        }

        private void ScrollSuppliedLayer(List<Transform> layer, float speed, bool randomizeY)
        {
            if (layer.Count == 0) return;

            foreach (Transform item in layer)
            {
                Vector3 position = item.position;
                position.x -= speed * Time.deltaTime;

                float halfWidth = GetHalfWidth(item);
                if (position.x + halfWidth < suppliedLeftBound)
                {
                    position.x = Mathf.Max(GetRightmostEdge(layer), suppliedRightBound) + halfWidth;
                    if (randomizeY)
                    {
                        position.y = Random.Range(cloudYRange.x, cloudYRange.y);
                    }
                }

                item.position = position;
            }
        }

        private static float GetRightmostEdge(List<Transform> layer)
        {
            float rightmost = float.NegativeInfinity;
            foreach (Transform item in layer)
            {
                rightmost = Mathf.Max(rightmost, item.position.x + GetHalfWidth(item));
            }
            return rightmost;
        }

        private static float GetHalfWidth(Transform item)
        {
            var renderer = item.GetComponent<SpriteRenderer>();
            return renderer != null ? renderer.bounds.extents.x : 0f;
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
