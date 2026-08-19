# 不安戦 実装一式 セットアップ手順

コードは実装済みです（`FuanBattleController.cs`、および`Assets/AngerBattle/`の`PlayerController.cs`/`DenialBullet.cs`/`BattleBGM.cs`/`FallingWord.cs`を共用）。
ここから先の**Unityエディタ上でのシーン構築**は、怒り戦（`AngerBattleRoot`）とほぼ同じ構造をコピーして作るのが一番早くて安全です。

---

## 全体方針：怒り戦のシーンをコピーする

一番簡単なのは、既存の`AngerBattleRoot`一式をHierarchy上でコピー＆リネームして作る方法です。

1. Hierarchyで`AngerBattleRoot`を選択し、`Ctrl+D`で複製
2. 複製したオブジェクトを`FuanBattleRoot`にリネーム
3. 子オブジェクトも合わせてリネーム（下記の対応表を参照）
4. 各コンポーネントの参照・数値を、下記の差分に沿って調整

### リネーム対応表

| 怒り戦（コピー元） | 不安戦（リネーム後） |
|---|---|
| `AngerBattleRoot` | `FuanBattleRoot` |
| `Player` | `Player`（そのまま） |
| `Enemy` | `Enemy`（そのまま。中身を紫色に変更） |
| `BulletSpawnPoint` | `BulletSpawnPoint`（そのまま） |
| `BattleUI`配下の`AttackLineText`/`AttackLineBackground`/`AttackLineCharacterName` | そのまま（そのまま使い回せる） |
| `BGMPlayer` | `BGMPlayer`（そのまま） |
| `AngerBattleController`（コンポーネント） | 削除し、代わりに`FuanBattleController`をアタッチ |

---

## 差分1：Enemyオブジェクト

- `EnemyAnger.cs`はそのまま使い回してOK（クラス名は`EnemyAnger`のままだが、ロジックは敵キャラ全般で共通）
- `SpriteRenderer`の**Color**を紫系の色に変更する（不安の見た目）
- 登場スライドイン（`appearFromOffsetX`/`appearDuration`）はそのままでも、必要なら調整

---

## 差分2：BGMPlayer

`BattleBGM`コンポーネントの設定値を以下に変更：

| 項目 | 値 |
|---|---|
| Clip | `Assets/Audio/ワスレナグサ.mp3` |
| Loop Start Seconds | 40.421（0:40.421） |
| Loop End Seconds | 285.473（4:45.473） |

---

## 差分3：FuanBattleController（コンポーネント）

複製した`AngerBattleRoot`直下にある`AngerBattleController`コンポーネントを削除し、代わりに`FuanBattleController`をアタッチして、以下を設定：

**参照**
- Player → `Player`
- Enemy → `Enemy`
- Bgm → `BGMPlayer`のBattleBGM
- Bullet Spawn Point → `BulletSpawnPoint`
- Denial Bullet Prefab → `Assets/AngerBattle/Prefabs/DenialBulletPrefab`（**そのまま使い回し。複製不要**）
- Falling Character Prefab → `Assets/AngerBattle/Prefabs/FallingCharacterPrefab`（**そのまま使い回し。複製不要**）
- Attack Line Text → `AttackLineText`のTMP_Text
- Character Name Text → `AttackLineCharacterName`のTMP_Text
- Line Background → `AttackLineBackground`

**台詞・数値**（デフォルト値のままでOK。コード側に以下が既に設定済み）
- Phrases：5つ（失敗したなダメだな恥ずかしいな／でもでもでもでも／どうしようぐるぐる思考が止まらない／考えない考えない考えない／すべてリセットしたい）
- Bpm：95
- Start Line：「コンタック: 心の声を震めなくちゃ。」（怒り戦と共通）
- Enemy Lines：2ブロック（「不安: わたしは不安。/自分を傷つけるもの避けたい…」「不安: 優しい世界が欲しいだけ。」）
- Attack Line：「コンタック: それは異常です。」

**蛇行移動パラメータ**（動かしながら調整。初期値）
| 項目 | 説明 | 初期値 |
|---|---|---|
| Erratic Change Interval | 方向・速度を変える間隔（秒） | 0.3 |
| Erratic Angle Spread | 方向転換の角度範囲（度） | 50 |
| Erratic Speed Range | 変化ごとの速度範囲 | 3〜11 |

---

## 差分4：MinigameLauncherへの参照追加

`MinigameLauncher`コンポーネント（Hierarchy内の`MinigameLauncher`オブジェクト）に、新しく増えた項目を設定：

- Fuan Battle Root → `FuanBattleRoot`
- Fuan Battle Controller → `FuanBattleRoot`内の`FuanBattleController`

（`Battle Root`/`Anger Battle Controller`/`Dialogue UI Root`は既存のままでOK）

---

## 差分5：Anxiety.yarnとの接続

`Anxiety.yarn`の`Anxiety_TakeMed`ノード末尾に、既に`<<start_minigame "FuanBattle">>`が記述済みです（追記不要）。

ただし、**まだ`Anger.yarn`側から`Anxiety.yarn`へ接続されていません**。怒り戦の後にこの話を続けたい場合は、`Anger.yarn`の適当な合流地点（例：`Anger_TakeMed`の`<<start_minigame "IkariBattle">>`の後、怒り戦から戻ってきた直後など）に`<<jump Anxiety>>`を追記する必要があります。これは別途相談してから対応します。

---

## 動作確認の流れ

1. シーン構築が終わったら、Play中に**F1キー**でデバッグメニューを開く
2. 「FuanBattle」ボタンで不安戦を単体起動して動作確認
3. 「Anxiety」ボタンで不安の現実パートから通しで確認
4. BGMがループ再生されるか、蛇行の弾幕が避けやすいか等、`FuanBattleController`の各数値を動かしながら調整
