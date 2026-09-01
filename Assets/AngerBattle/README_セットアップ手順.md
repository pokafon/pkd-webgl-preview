# 怒り戦 実装一式 セットアップ手順（旧版・現状と不一致）

> **注（2026-08-31）**：このREADMEは横スクロール弾幕回避版（旧仕様）の手順です。現在の`AngerBattleController.cs`は縦型HP制シューティングへ再設計されており、`FallingWord.cs`は削除済みです。最新の状況は[怒り戦_打ち合わせメモ.md](../../怒り戦_打ち合わせメモ.md)の「【重要】ゲーム性が別物へ再設計されている」を参照してください。
> 現在の実装仕様は[現行仕様.md](../../現行仕様.md)を正とし、このREADMEの手順は使用しないでください。

## ファイル一覧
- `PlayerController.cs` — コンタックの移動
- `FallingWord.cs` — 流れてくる1文字分の弾
- `EnemyAnger.cs` — 怒り本体
- `DenialBullet.cs` — 攻撃弾（見た目はシンプルな弾。セリフとは別物）
- `BattleBGM.cs` — BGM（Trick_style）の再生・区間ループ・停止
- `AngerBattleController.cs` — 怒り戦全体の進行管理
- `MinigameLauncher.cs` — Yarnの`<<start_minigame>>`との接続役

すべて`AngerBattle`という名前空間（namespace）に入っています。

---

## 全体の流れ（おさらい）

1. BGM（Trick_style）を再生しながら、3つの台詞を順番に1文字ずつ、
   バラバラの高さ・タイミングで右から左へ流す
   - 「人から奪うだけのくせに…」→「消えてしまえばいいのに」→「全部気に入らない」
   - 被弾してもペナルティ・ゲームオーバーはない
2. 3つとも避け終えたら、**最後に1回だけ**「怒り」本体が登場
   - 登場と同時にBGMを停止
   - 即セリフは出さず、1拍分だけ間を置く
3. 一拍後、自動で「それは異常です」というセリフを表示（プレイヤー操作なし）
4. プレイヤーがEnterキーを押すと、シンプルな弾を発射
5. 一発ヒットで怒りを撃破し、怒り戦終了

---

## Unity側で作る階層構成（例）

```
AngerBattleRoot（普段は非アクティブ）
├── Player（PlayerController.cs をアタッチ）
├── Enemy（EnemyAnger.cs をアタッチ、Collider2D＝Is Trigger 必須）
├── BulletSpawnPoint（空オブジェクトでOK、攻撃弾の発射位置）
├── AttackLineText（TMP_Text。Yarnの通常セリフ表示と似た見た目のもの）
├── BGMPlayer（AudioSource + BattleBGM.cs をアタッチ）
└── AngerBattleController（AngerBattleController.cs をアタッチ）
```

`Dialogue System`と同じシーン内に、この`AngerBattleRoot`を作ってください。

---

## BGMファイルの配置

`Trick_style.mp3`を、Unityプロジェクトの `Assets/Audio/` に配置してください
（`Audio`フォルダがなければ新規作成）。

配置後、Unity上でAudioClipとして認識されるので、`BattleBGM`の`Clip`欄にドラッグします。

**BattleBGMの設定値**
| 項目 | 値 |
|---|---|
| Clip | Trick_style.mp3 |
| Loop Start Seconds | 12（0:12.000） |
| Loop End Seconds | 223.862（3:43.862） |

---

## プレハブとして別途作るもの

### 1. FallingCharacterPrefab（流れる文字用）
- TextMeshPro（TMP_Text）を持つオブジェクト
- `FallingWord.cs`は自動でアタッチされる仕組みになっているので、事前に付けなくてもOK
- `AngerBattleController`の`Falling Character Prefab`欄にドラッグ

### 2. DenialBulletPrefab（攻撃弾用）
- `DenialBullet.cs`をアタッチ
- Collider2D（Is Trigger）を追加
- 見た目は仮の四角や丸でOK
- `AngerBattleController`の`Denial Bullet Prefab`欄にドラッグ

### 3. AttackLineText（攻撃セリフ表示用）
- 怒りの登場から一拍後に、自動で「それは異常です」が表示される演出用
- Yarnの通常セリフ表示（Line Presenter）と似た見た目のTMP_Textを用意する
- `AngerBattleController`の`Attack Line Text`欄にドラッグ
- 表示するセリフ自体は`Attack Line`欄で変更可能（初期値は「それは異常です」）

---

## MinigameLauncherの配置

`Dialogue System`と同じ階層（または近く）に空オブジェクトを作り、`MinigameLauncher.cs`をアタッチしてください。

インスペクターで以下を設定：
- `Battle Root` → 上で作った`AngerBattleRoot`
- `Anger Battle Controller` → `AngerBattleRoot`内の`AngerBattleController`
- `Dialogue UI Root` → 戦闘中に隠したいダイアログUI（`Canvas`など、任意）

---

## Yarnスクリプト側の追記

`Anger.yarn`の`Anger_TakeMed`ノードの最後に、以下の1行を追加してください。

```yarn
title: Anger_TakeMed
---

クラリオンは一錠押し出し、水と一緒に飲み込む。
画面が白く焼ける。

？？？: 全部気に入らない。

<<start_minigame "IkariBattle">>
===
```

これで、このノードに到達すると自動的に怒り戦が始まり、怒りを撃破すると自動的にYarnの続き（もしあれば次のノード）に進みます。

---

## 調整ポイント（動かしながら微調整する部分）

`AngerBattleController`のインスペクターで、以下の数値は実際に動かしながら調整してください。

| 項目 | 説明 | 初期値 |
|---|---|---|
| Bpm | BGMのテンポ（Trick_style = 145） | 145 |
| Beats Per Character | 何拍ごとに1文字出すか（小さいほど忙しい） | 1 |
| Phrase Gap Beats | 台詞と台詞の間に空ける拍数 | 2 |
| Spawn Jitter | 出現タイミングのランダムなずれ幅（秒） | 0.08 |
| Word Speed | 文字が流れる速さ | 6 |
| Spawn Y Range | 文字が出現する高さの範囲 | -3.5〜3.5 |
| Beats Before Attack Line | 怒り登場からセリフ表示までの拍数 | 1 |

---

## 保留中の演出（今は未実装）

- 敵登場後、即「それは異常です」を出すのではなく、敵が一言喋ってから表示する演出
  - 実装する場合は、`AngerBattleController.RunBattleSequence()`内、
    `ShowAttackLine()`を呼ぶ直前に敵のセリフ表示処理を挟む想定

---

## 動作確認の流れ

1. `AngerBattleRoot`を一旦アクティブにして、単体で動くか確認する
2. `MinigameLauncher`経由で`<<start_minigame "IkariBattle">>`を呼んで自動起動するか確認する
3. BGMが避けフェーズで鳴り、怒り登場と同時に止まるか確認する
4. BPMやSpeedの数値を動かしながら「避けやすさ」「気持ちよさ」を調整する
