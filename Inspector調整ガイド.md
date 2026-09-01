# Unity Inspector調整ガイド

最終更新: 2026-09-02

コードを開かず、UnityのHierarchyとInspectorだけで見た目・速度・音量などを調整するための案内です。
対象シーンは`Assets/Scenes/SampleScene.unity`です。

## 最初に見る場所

1. Unityで`SampleScene`を開く。
2. Play中なら停止する。
3. Hierarchy左上の検索欄へ、下表の「選ぶオブジェクト名」を入力する。
4. 検索結果を選び、Inspectorに表示される同名のコンポーネントを開く。
5. 値を変更してPlayで確認し、問題なければシーンを保存する。

| 調整対象 | Hierarchyの場所 | 選ぶオブジェクト名 | Inspectorで見るコンポーネント |
|---|---|---|---|
| 怒り戦全体 | `_Minigames/AngerBattleRoot/AngerBattleController` | `AngerBattleController` | `Anger Battle Controller` |
| 怒り戦HP表示 | `_Minigames/AngerBattleRoot/AngerBattleHUD` | `AngerBattleHUD` | `Anger Battle HUD` |
| 不安戦 | `_Minigames/FuanBattleRoot/FuanBattleController` | `FuanBattleController` | `Fuan Battle Controller` |
| ベッド飛行全体 | `_Minigames/BedFlightRoot/BedFlightController` | `BedFlightController` | `Bed Flight Controller` |
| 街・雲の背景 | `_Minigames/BedFlightRoot/CityBackground` | `CityBackground` | `City Background Scroller` |
| ベビーメリー | `_Minigames/BedFlightRoot/BabyMobileAnchor` | `BabyMobileAnchor` | `Baby Mobile Ambient` |
| 記憶回想 | `_Minigames/MemoryRecallRoot/MemoryRecallController` | `MemoryRecallController` | `Memory Recall Controller` |
| 悲しみ戦 | `_Minigames/SadnessBattleRoot/SadnessBattleController` | `SadnessBattleController` | `Sadness Battle Controller` |
| 共有マップのカメラ | `_World/SadnessMapEnvironment` | `SadnessMapEnvironment` | `Sadness Map Environment` |
| ゲーム全体音量 | `_Core/MinigameLauncher` | `MinigameLauncher` | `Minigame Launcher` |

## 数値の基本

- `Position` / `Viewport`: 位置。Xは右へ行くほど増え、Yは上へ行くほど増える。
- `Scale` / `Size`: 大きさ。値を増やすと大きくなる。
- `Speed`: 速度。値を増やすと速くなる。
- `Duration` / `Seconds`: 秒数。値を増やすと演出が長くなる。
- `Sorting Order`: 描画順。同じSorting Layer内では、値が大きいほど手前に出る。
- `Volume`: 音量。`0`が無音、`1`が元音量。
- `Color`の`A`: 透明度。`0`が透明、`1`が不透明。

## 怒り戦

### キャラクター・弾・命中演出

Hierarchyで`AngerBattleController`を選び、`Anger Battle Controller`を見る。

| Inspectorの欄 | 現在値 | 何が変わるか |
|---|---:|---|
| `Player Battle Position` | `(0, -3.15, 0)` | コンタックの開始位置 |
| `Enemy Battle Position` | `(0, 3.05, 0)` | 怒り本体の開始位置 |
| `Player Battle Scale` | `(0.36, 0.36, 1)` | コンタックの大きさ |
| `Enemy Battle Scale` | `(0.5, 0.5, 1)` | 怒り本体の大きさ |
| `Player Battle Speed` | `6.7` | コンタックの移動速度 |
| `Screen Edge Margin` | `0.12` | キャラクターと画面端の余白 |
| `Combatant Sorting Order` | `10` | コンタックと怒り本体の描画順 |
| `Enemy Bullet Sorting Order` | `20` | 赤弾の描画順 |
| `Player Bullet Sorting Order` | `21` | 青弾の描画順 |
| `Player Bullet Spawn Offset` | `(0, 0.7, 0)` | コンタックの中心から青弾が出る位置 |
| `Enemy Bullet Origin Height` | `0.25` | 怒り本体から赤弾が出る高さ。増やすと上へ移動 |
| `Standard Enemy Bullet Speed` | `5.0` | 通常の赤弾速度 |
| `Curtain Bullet Speed` | `4.2` | カーテン弾の速度 |
| `Enemy Bullet Scale` | `0.28` | 赤弾の大きさ |
| `Player Shot Cooldown` | `0.16` | 青弾の連射間隔。小さくすると連射が速い |
| `Enemy Hit Effect Duration` | `0.14` | 怒りに青弾が当たった時の演出時間 |
| `Enemy Hit Shake Strength` | `0.07` | 怒り本体の揺れ幅 |
| `Enemy Hit Punch Amount` | `0.055` | 怒り本体の一瞬の拡大量 |
| `Enemy Hit Flash Brightness` | `0.8` | 命中時に白く光る強さ |

`Fallback Player Min Bounds`と`Fallback Player Max Bounds`は、カメラから移動範囲を計算できなかった時だけ使う保険です。通常調整は不要です。

### HPゲージとリトライ表示

Hierarchyで`AngerBattleHUD`を選び、`Anger Battle HUD`を見る。

| Inspector内の見出し | 主な欄 | 何が変わるか |
|---|---|---|
| `Canvas・解像度` | `Canvas Sorting Order` | HUD全体の描画順 |
| `Canvas・解像度` | `Reference Resolution` | UI配置の基準解像度。現在は`1920 × 1080` |
| `HPゲージ・ラベル配置` | `Enemy Bar Position` | 怒りのHPゲージ位置 |
| 同上 | `Enemy Label Position` | 「怒り」の文字位置 |
| 同上 | `Player Bar Position` | コンタックのHPゲージ位置 |
| 同上 | `Player Label Position` | 「コンタック」の文字位置 |
| 同上 | `Health Bar Size` | 両方のHPゲージサイズ |
| 同上 | `Label Size` / `Label Font Size` | 名前表示の領域と文字サイズ |
| 同上 | `Retry Text Position` / `Retry Font Size` | リトライ表示の位置と文字サイズ |
| `HUD演出` | `Death Fragment Count` | 敗北時に飛び散る破片数 |
| 同上 | `Death Effect Duration` | 敗北演出の長さ |
| 同上 | `Death Fade Alpha` | 敗北時の暗転濃度 |
| 同上 | `Bar Flash Duration` | HPゲージが白く光る時間 |
| 同上 | `Phase Pulse Duration` | フェーズ移行時の赤い脈動時間 |

Play停止中は値を変えると既存HUDへ反映されます。反映されない場合は、コンポーネント右上のメニューから`Inspector設定をHUDへ反映`を選びます。

## 不安戦

Hierarchyで`FuanBattleController`を選び、`Fuan Battle Controller`を見る。

| Inspectorの欄 | 何が変わるか |
|---|---|
| `Opening Layout` → `Anxiety Start Viewport` | 冒頭の不安の位置 |
| `Opening Layout` → `Anxiety End Viewport` | 不安が逃げ終わる位置 |
| `Opening Layout` → `Contack Start Viewport` | 冒頭のコンタック位置 |
| `Opening Layout` → `Anxiety Screen Height` | 冒頭の不安の大きさ |
| `Opening Layout` → `Contack Screen Height` | 冒頭のコンタックの大きさ |
| `Opening Chase Duration` | 不安が上へ逃げる時間 |
| `Opening After Exit Hold Seconds` | 不安が消えてから床が動き始めるまでの間 |
| `Opening Scroll Duration` | 質問画面まで床が動く時間 |
| `Face Off Layout` | 終盤で向き合う二人の位置と大きさ |
| `Question Dive Duration` | YES／NO入口へカメラが寄る時間 |
| `Question Dive Orthographic Size` | 入口へ寄った時のカメラサイズ |
| `Question Rain Rate` | 質問中の雨量 |
| `Question Running Wet Road Volume` | 走る足音の音量 |

Viewport位置は、X=`0`が左端、X=`1`が右端、Y=`0`が下端、Y=`1`が上端です。Yが`1`より大きいと画面外上側になります。

## ベッド飛行

### ベッドと進行時間

Hierarchyで`BedFlightController`を選び、`Bed Flight Controller`を見る。

| Inspectorの欄 | 現在値 | 何が変わるか |
|---|---:|---|
| `Screen Edge Margin` | `0.15` | ベッドと画面端の余白 |
| `Freedom Duration Seconds` | `25` | 自由に飛べる時間 |
| `Contac Resting Position` | `(-4, 0, 0)` | 終盤に現れるコンタックの定位置 |
| `Move To Resting Duration` | `0.3` | 終盤にベッドを中央へ動かす時間 |
| `Appear From Offset X` | `6` | コンタックが画面外から移動する距離 |
| `Appear Duration` | `0.8` | コンタックの登場時間 |
| `Beat Before Line Seconds` | `0.6` | 登場後、台詞を出すまでの間 |
| `Beat Before Fire Seconds` | `0.6` | 台詞後、弾を撃つまでの間 |
| `Post Hit Pause Seconds` | `0.6` | 命中後、暗転までの間 |
| `Fade Duration` | `1.2` | 終了暗転の時間 |

`House Intro`は廃止済みのため、空欄のままにします。

### 街並み・雲

Hierarchyで`CityBackground`を選び、`City Background Scroller`を見る。

| Inspectorの欄 | 何が変わるか |
|---|---|
| `Foreground City Speed` | 手前の街の速度 |
| `Background City Speed` | 奥の街の速度 |
| `Cloud Speed` | 雲の速度 |
| `Foreground City Base Y` | 手前の街の上下位置 |
| `Background City Base Y` | 奥の街の上下位置 |
| `Foreground City Scale` | 手前の街の大きさ |
| `Background City Scale Range` | 奥の街の大きさのランダム範囲 |
| `Foreground Sorting Order` | 手前の街の描画順 |
| `Background Sorting Order` | 奥の街の描画順 |
| `Supplied Cloud Sorting Order` | 雲の描画順 |
| `Offscreen Preload Padding` | 画面外で先に読み込んで待機させる距離。増やすと突然表示されにくい |

画像配列と個数を変更すると並び方そのものが変わるため、速度・位置の微調整だけなら触らない方が安全です。

### ベビーメリー

Hierarchyで`BabyMobileAnchor`を選び、`Baby Mobile Ambient`を見る。

| Inspectorの欄 | 現在値 | 何が変わるか |
|---|---:|---|
| `Viewport Anchor` | `(0.68, 1.01)` | 画面上の位置。Xを増やすと右へ移動 |
| `Visual Scale` | `0.65` | メリー全体の大きさ |
| `Sorting Order` | `-6` | 描画順。現在は雲とプレイヤーより後ろ |
| `Visual Tint` | 青灰色・少し透明 | 色と透明度 |
| `Vertical Amplitude` | 小さい値 | 上下に浮く幅 |
| `Vertical Frequency` | 小さい値 | 上下動の速さ |
| `Cord Sway Degrees` | 小さい値 | 吊り紐の揺れ角度 |
| `Mobile Sway Degrees` | 小さい値 | メリー本体の揺れ角度 |
| `Mobile Follow` | 小さい値 | 吊り紐に本体が追いつく速さ |

位置と大きさだけ調整する場合は、まず`Viewport Anchor`と`Visual Scale`だけを変更します。

## 記憶回想・悲しみ戦

### 記憶回想

Hierarchyで`MemoryRecallController`を選び、`Memory Recall Controller`を見る。

- `Interact Range`: 友達と会話できる距離。
- `Mother Interact Range`: 母と会話できる距離。
- `Evening Chime Source` / `Evening Chime Clip`: 17時チャイムの再生元と音源。

### 悲しみ戦

Hierarchyで`SadnessBattleController`を選び、`Sadness Battle Controller`を見る。

- `Interact Range`: 攻撃対象へ反応できる距離。
- `Mother Line Hold Seconds`: 母の台詞を保持する時間。
- `Evening Chime Source` / `Evening Chime Clip`: 17時チャイムの再生元と音源。

### 共有マップのカメラと出口

Hierarchyで`SadnessMapEnvironment`を選び、`Sadness Map Environment`を見る。

- `Map Orthographic Size`: 探索中のカメラサイズ。増やすと広い範囲が映る。
- `Follow Player`: プレイヤーへカメラを追従させるか。
- `Fit Whole Home In View`: 室内全体を画面へ収めるか。
- `Home Camera Padding`: 室内全景の外側に残す余白。
- `Home Exit Half Width` / `Home Exit Depth`: 室内出口として反応する範囲。

## ゲーム全体の音量

Hierarchyで`MinigameLauncher`を選び、`Minigame Launcher`を見る。

- `Global Audio Volume`: 全BGM・SE・チャイムへ最後に掛ける音量。
- 現在値は`0.7`で、元の音量から約3割小さくしている。
- さらに少し下げるなら`0.6`、元の音量へ戻すなら`1.0`。

## 触らない項目

次はユーザーがSceneビューで調整した内容を正とするため、このガイドの調整対象外です。

- Tilemap Layer
- Sorting Layer
- Order in Layer
- TilemapとGridのTransform
- 家・道路・床・壁・キャラクターマーカーの手動配置
- `Outdoor Min Bounds` / `Outdoor Max Bounds` / `Home Min Bounds` / `Home Max Bounds`
- `MemoryRecallSceneBuilder`、`SadnessMapEditorUtility`などの再構築メニュー

## 変更を元へ戻す方法

- 変更直後なら`Ctrl + Z`。
- Play中に試した値は、Playを停止すると通常は元へ戻る。
- 保存済みの値を戻す場合は、このガイドの現在値を入れ直す。
- コンポーネント右上の`Reset`は参照まで消える場合があるため、安易に使わない。
