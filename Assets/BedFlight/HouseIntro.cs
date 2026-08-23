using UnityEngine;

namespace BedFlight
{
    /// <summary>
    /// 開始演出：ベッドが家から飛び出してくる導入アニメーション用の家のビジュアル。
    /// 三角屋根＋四角い胴体＋窓を実行時に単色シルエットとして生成する（既製アセット不使用の方針を踏襲）。
    /// ベッドが飛び出した後は<see cref="StartScrolling"/>を呼ぶことで、背景のビルと同じ速度で
    /// 左へスクロールしていく（ループ・回収はしない。使い切りの演出用オブジェクトのため、
    /// 画面外に出たら非表示にするだけ）。
    /// </summary>
    public class HouseIntro : MonoBehaviour
    {
        [Header("見た目")]
        public Color houseColor = new Color(0.16f, 0.14f, 0.18f);
        public Color windowColor = new Color(0.55f, 0.82f, 1f);
        public float bodyWidth = 2.2f;
        public float bodyHeight = 2f;
        public float roofHeight = 1.4f;

        [Tooltip("家の胴体・屋根のSorting Order。ベッドが家の中に隠れている間は、これより奥（小さい値）にベッド側を沈めて隠す")]
        public int silhouetteSortingOrder = -1;

        [Header("スクロール（CityBackgroundScrollerのビルと合わせる）")]
        public float scrollSpeed = 2.5f;
        public float despawnX = -14f;

        private bool scrolling = false;

        void Awake()
        {
            BuildVisual();
        }

        void Update()
        {
            if (!scrolling) return;

            Vector3 pos = transform.position;
            pos.x -= scrollSpeed * Time.deltaTime;
            transform.position = pos;

            if (pos.x < despawnX)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>ベッドが飛び出した後に呼ぶ。以後、背景のビルと同じ速度で左へスクロールしていく。</summary>
        public void StartScrolling()
        {
            scrolling = true;
        }

        /// <summary>
        /// ベッド（プレイヤー）が飛び出す起点位置（家の胴体の中、やや低め）を返す。
        /// 屋根寄りではなく胴体の中に沈めることで、「家の中から出てくる」印象を狙う
        /// （実際に姿を隠すのはBedFlightController側でSorting Orderを家より奥に沈めて行う）。
        /// </summary>
        public Vector3 GetLaunchStartPosition()
        {
            return transform.position + new Vector3(0f, bodyHeight * 0.3f, 0f);
        }

        private void BuildVisual()
        {
            var bodyGO = new GameObject("HouseBody", typeof(SpriteRenderer));
            bodyGO.transform.SetParent(transform, false);
            var bodyRenderer = bodyGO.GetComponent<SpriteRenderer>();
            bodyRenderer.sprite = CreateSolidSquareSprite();
            bodyRenderer.color = houseColor;
            bodyRenderer.sortingOrder = silhouetteSortingOrder;
            bodyGO.transform.localPosition = new Vector3(0f, bodyHeight / 2f, 0f);
            bodyGO.transform.localScale = new Vector3(bodyWidth, bodyHeight, 1f);

            var roofGO = new GameObject("HouseRoof", typeof(SpriteRenderer));
            roofGO.transform.SetParent(transform, false);
            var roofRenderer = roofGO.GetComponent<SpriteRenderer>();
            roofRenderer.sprite = CreateTriangleSprite();
            roofRenderer.color = houseColor;
            roofRenderer.sortingOrder = silhouetteSortingOrder;
            roofGO.transform.localPosition = new Vector3(0f, bodyHeight, 0f);
            roofGO.transform.localScale = new Vector3(bodyWidth * 1.15f, roofHeight, 1f);

            BuildWindow();
        }

        /// <summary>胴体の中央あたりに、格子（十字の桟）付きの窓を追加する。</summary>
        private void BuildWindow()
        {
            float windowSize = bodyWidth * 0.32f;
            Vector3 windowLocalPos = new Vector3(0f, bodyHeight * 0.55f, 0f);

            var paneGO = new GameObject("HouseWindowPane", typeof(SpriteRenderer));
            paneGO.transform.SetParent(transform, false);
            var paneRenderer = paneGO.GetComponent<SpriteRenderer>();
            paneRenderer.sprite = CreateSolidSquareSprite();
            paneRenderer.color = windowColor;
            paneRenderer.sortingOrder = silhouetteSortingOrder + 1;
            paneGO.transform.localPosition = windowLocalPos;
            paneGO.transform.localScale = new Vector3(windowSize, windowSize, 1f);

            float mullionThickness = windowSize * 0.14f;

            var vBarGO = new GameObject("HouseWindowMullionV", typeof(SpriteRenderer));
            vBarGO.transform.SetParent(transform, false);
            var vBarRenderer = vBarGO.GetComponent<SpriteRenderer>();
            vBarRenderer.sprite = CreateSolidSquareSprite();
            vBarRenderer.color = houseColor;
            vBarRenderer.sortingOrder = silhouetteSortingOrder + 2;
            vBarGO.transform.localPosition = windowLocalPos;
            vBarGO.transform.localScale = new Vector3(mullionThickness, windowSize, 1f);

            var hBarGO = new GameObject("HouseWindowMullionH", typeof(SpriteRenderer));
            hBarGO.transform.SetParent(transform, false);
            var hBarRenderer = hBarGO.GetComponent<SpriteRenderer>();
            hBarRenderer.sprite = CreateSolidSquareSprite();
            hBarRenderer.color = houseColor;
            hBarRenderer.sortingOrder = silhouetteSortingOrder + 2;
            hBarGO.transform.localPosition = windowLocalPos;
            hBarGO.transform.localScale = new Vector3(windowSize, mullionThickness, 1f);
        }

        /// <summary>CityBackgroundScrollerの空・雲・ビルと同じ方式で、単色の正方形スプライトを実行時に生成する。</summary>
        private static Sprite CreateSolidSquareSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }

        /// <summary>底辺が下・頂点が上の二等辺三角形シルエットを、ピクセル単位で塗って生成する。</summary>
        private static Sprite CreateTriangleSprite()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                // yが大きいほど頂点に近づくため、三角形の横幅は上に向かって狭くなる。
                float halfWidthAtY = (size / 2f) * (1f - (float)y / (size - 1));
                for (int x = 0; x < size; x++)
                {
                    float distFromCenter = Mathf.Abs(x - size / 2f);
                    pixels[y * size + x] = distFromCenter <= halfWidthAtY ? Color.white : new Color(0f, 0f, 0f, 0f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f), size);
        }
    }
}
