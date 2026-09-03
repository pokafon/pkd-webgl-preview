---
name: fill-art
description: PKDプロジェクトの画像未設定箇所を調査し、Docs/art_manifest.yamlを更新して仮画像を生成、Unityへ自動割り当てするまでの一連の作業を実行する。「仮画像で埋めて」「/fill-art」等で起動。
---

# /fill-art

PKD (Unity 2D) の画像未実装箇所を仮画像で埋め、通し確認できる状態を維持するためのSkill。

## 絶対条件（毎回必ず守る）

1. 既存の完成画像・確定済み画像・手作業で設定済みの画像は絶対に上書きしない。
2. 仮画像は必ず `Assets/Art/AI_Placeholder/` 以下に保存する。
3. 仮画像は完成品として扱わない（`Docs/art_manifest.yaml` の `notes` に仮素材である旨を残す）。
4. 差し替えやすいよう、`Docs/art_manifest.yaml` の命名規則・一覧管理を必ず維持する。
5. 作業後は必ず「何を生成し、どこに割り当てたか」を報告する。
6. シーン(.unity)・プレハブ(.prefab)を編集する前に、Unity Editorが起動していないか
   `tasklist //FI "IMAGENAME eq Unity.exe"` で確認する（CLAUDE.md ルール4）。起動していたら
   閉じてもらうよう伝えてから作業する。

## 手順

1. **調査**: `Assets/Scenes/*.unity`、`Assets/**/*.prefab`、および `Assets/Scripts` 内の
   Sprite/Texture2D フィールドをgrepし、`SpriteRenderer.m_Sprite`/`Image.sprite` が
   `{fileID: 0}` になっている箇所、および明らかに未接続なSprite系フィールドを洗い出す。
   - `現行仕様.md` と `シナリオ全文_読みやすい版.md` を読み、各画像が何を表すべきかの
     文脈（背景の種類、キャラクターの感情、シーン名）を確認する。
   - 直近のgit commit log（完成済み立ち絵・BGM登録など）を確認し、既に確定済みの画像を
     誤って仮画像対象にしない。
   - コード側のみで手続き的に描画されている演出（procedural cage等）で、まだ専用アートに
     差し替わっていないものも「status: missing」候補として拾うが、Unityへの自動割り当てが
     できない（スクリプト変更が必要な）場合は `status: blocked` として区別する。

2. **manifest更新**: `Docs/art_manifest.yaml` に不足画像を追記する。既存エントリの
   `status: filled` や `done` は上書きしない。各エントリに最低限
   `id, status, type, scene, object, target_path, width, height, prompt, notes` を入れる。
   推定に自信がない場合は `prompt`/`notes` に「要確認」と明記し、断定しない。

3. **仮画像生成**: `python Tools/generate_placeholder_image.py` を実行する。
   `status: missing` の項目のみ処理され、`Assets/Art/AI_Placeholder/` 以下にPNGが出力される。
   特定IDのみ再生成したい場合は `--id <id>` を複数指定できる。

4. **Unityへの割り当て**（シーン/プレハブYAMLを直接編集する場合は必ず手順6のEditor確認を先に行う）:
   - 新規PNGごとに `.meta` ファイルを作成し、`TextureImporter` を
     `textureType: 8` (Sprite (2D and UI))、`spriteImportMode: 1` (Single) に設定する。
     UI用途なら `wrapMode`/`filterMode` を用途に応じて調整する。
   - 対応する `.unity`/`.prefab` 内の該当フィールド（`m_Sprite: {fileID: 0}` など）を、
     新規GUIDを使った `{fileID: 21300000, guid: <新規guid>, type: 3}` に書き換える。
   - **既存画像が入っているフィールドは絶対に触らない。空欄のみ埋める。**
   - `status: blocked`（procedural生成でシーンに対応GameObjectが無いもの）は、
     このSkillの範囲では割り当てを行わず、スクリプト変更が必要な旨をレポートに残す
     （CLAUDE.mdルール1により、スクリプトのロジック変更は別途ユーザーの実装許可を得てから行う）。

5. **検証**: 変更をまとめた後、最後に1回だけ
   `Unity.exe -batchmode -nographics -quit -projectPath "G:\Unity_Games\PKD" -logFile <log>`
   でシーンYAMLの構造的な妥当性とコンパイルを確認する（CLAUDE.mdルール2）。
   Editor起動中でロック競合になった場合は、ユーザーに閉じるよう伝える。

6. **manifest更新（結果反映）**: 実際にUnityへ割り当てたエントリの `status` を `filled` に、
   コード変更待ちのものは `blocked` のまま残す。

7. **報告**: 生成した仮画像一覧（ID/保存先/種別/割り当て先）、自動割り当てした箇所
   （Scene名/Object名/コンポーネント名/割り当て画像）、未処理項目とその理由、
   次に人間がやるべきことを優先順位付きで報告する。
