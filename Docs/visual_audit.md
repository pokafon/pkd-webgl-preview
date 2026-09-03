# PKD ビジュアル品質監査（販売版に向けて）

調査日: 2026-09-03
目的: 「画像参照が空欄かどうか」ではなく、販売版として通用するビジュアル品質かどうかを
FINAL / TEMP / PROCEDURAL / MISSING の4分類で洗い出す。

分類の定義:

- **FINAL**: 販売版としてそのまま使える専用アート
- **TEMP**: 仮画像・仮背景・仮UI（AI生成の繋ぎ、素材感の強いプレースホルダー等）で差し替え必要
- **PROCEDURAL**: コードで図形・色矩形・動的生成を行っている。ミニマル演出として妥当なものと、
  本番アート化を検討すべきものが混在するため個別に評価
- **MISSING**: 必要な画面・演出なのに専用ビジュアルが存在しない

第三者フリー素材（Cute_Fantasy_Free、pixelworld_complete_v.1.8）は「FINAL」ではなく
別区分として明記し、商用利用ライセンスの確認が必要な旨を注記する（本監査ではライセンス
内容そのものは検証していない）。

この監査はあくまで調査であり、画像の新規生成・Unityへの割り当ては行っていない。

---

## 1. プロローグ (Prologue)

`Prologue.yarn`は22行のみ。背景は`Office`のみ使用し、立ち絵`Boss`→`Girlfriend`→`None`と
切り替えて即`Anger`へjumpする。プロローグ専用の演出（時計グリッチ等）は無い。

| # | Scene/Object | 現在の画像/描画方式 | 分類 | 販売画面としての問題点 | 必要な画像仕様 | AI仮置き価値 |
|---|---|---|---|---|---|---|
| 1 | `<<background "Office">>` (Prologue) | `Assets/Sprites/OfficeBackground.jpg`。作り込まれたアニメ調オフィス夜景イラスト | FINAL | 特になし | 現状で問題なし | no |
| 2 | `<<portrait "Boss">>` (Prologue) | `Assets/Sprites/Boss.png`。線画+セルシェードのアニメ調ビジネスマン肖像、完成度高い | FINAL | 特になし | 現状で問題なし | no |
| 3 | `<<portrait "Girlfriend">>` (Prologue) | `ChatGPT Image 2026年8月22日 23_44_26.png`。AI生成のグロッシーな半立体風アニメ塗り | TEMP | AI生成であることに加え、Boss.pngの「インク線画+フラットセルシェード」と塗りの質感が明確に異なり、同一会話画面で並ぶと画風の統一感が崩れる | 1080×1920目安、Boss.pngと同じ線画+フラットセル塗りスタイルで描き起こす | yes（繋ぎとしては現状破綻していない） |
| 4 | プロローグ専用演出 | 存在しない | ー | 特になし | ー | ー |

---

## 2. 怒り編 (Anger.yarn + AngerBattleRoot)

| # | Scene/Object | 現在の画像/描画方式 | 分類 | 販売画面としての問題点 | 必要な画像仕様 | AI仮置き価値 |
|---|---|---|---|---|---|---|
| 1 | `<<background "Office"/"Train"/"Room">>` (Anger.yarn) | 既存確認済み背景 | FINAL | 特になし | 現状で問題なし | no |
| 2 | `AngerBattleRoot/Background` | `Assets/Sprites/BattleBackground.jpg`。ほぼ真っ黒に微小な星点だけの単調な夜空画像 | TEMP | 「精神世界に潜る」重要な場面転換なのに、汎用ストック風背景1枚を怒り・不安戦で使い回しており専用アートディレクションが皆無 | 1920×1080、怒りの精神世界らしい歪んだ抽象背景（渦・脈動する赤黒グラデーション等） | yes（プレースホルダー感が強く優先度高） |
| 3 | `AngerActor` | `AngerVertical.png` | FINAL | 特になし | 現状で問題なし | no |
| 4 | `ContackPlayer`（怒り戦内） | `ContackVertical.png` | FINAL | 特になし | 現状で問題なし | no |
| 5 | プレイヤー弾（`AngerBattleController.GetRuntimeBulletSprite`） | 実行時生成の白い塗りつぶし円（32×32）を着色 | PROCEDURAL | 単なる塗りつぶし円で質感が無く量産感が強い | 32〜64px、否認を感じさせる小さな弾スプライト | yes（安価で見栄え改善効果大） |
| 6 | 敵弾（`AngerBullet`） | 同上procedural円を`enemyBulletColor`で着色 | PROCEDURAL | 同上 | 同上（敵弾用バリエーション可） | yes |
| 7 | `AngerBattleHUD`（HPバー/PhasePulse/DeathFade） | procedural単色矩形（実行時生成） | PROCEDURAL | ミニマルUIとして機能するが商用戦闘UIとしては簡素 | 現状維持可。余裕があれば枠付きゲージ画像 | no |
| 8 | 「隔離する」演出（`EmotionResolutionFlow.BuildCage`） | 実行時生成の鉄格子バー6本。コードコメントに「専用アートへ差し替えるまでの仮」と明記 | PROCEDURAL | 既知の仮実装。TRUE ENDへ向かう主要演出なのに仮の棒状バーのまま | 隔離用の牢屋アート（縦長、既存立ち絵のワールドサイズに合わせた鉄格子） | yes（コード側で差し替え前提と明言済み） |
| 9 | 「消去する」選択時 | **何も再生されない**（Eliminated分岐に視覚効果が未実装。3感情共通のため不安編・悲しみ編にも同一欠落） | MISSING | 敵が瞬時に消えるだけの無演出で選択の重みが伝わらない | 消去用の短いパーティクル/フェードアウト演出、または専用スプライトアニメーション | yes（現状ゼロなので最小限の仮演出でも改善大） |
| 10 | `GoodMorningOutro`（撃破後の目覚め演出） | procedural白フェード+チャイム音 | PROCEDURAL | 意図された演出でUXとして成立している | 現状で問題なし | no |
| 11 | `ClockGlitchIntro`（精神世界突入前の時計グリッチ） | procedural（TMP_Textジッター+NoiseBlock0-9+背景アルファ） | PROCEDURAL | 演出として十分機能し狙ったグリッチ感も出ている | 現状で問題なし | no |

---

## 3. 不安編 (Anxiety.yarn + FuanBattleRoot)

| # | Scene/Object | 現在の画像/描画方式 | 分類 | 販売画面としての問題点 | 必要な画像仕様 | AI仮置き価値 |
|---|---|---|---|---|---|---|
| 1 | `<<background "TrainDay"/"Office"/"Room">>` (Anxiety.yarn) | 既存確認済み背景 | FINAL | 特になし | 現状で問題なし | no |
| 2 | `FuanBattleRoot/Background` | `BattleBackground.jpg`（怒り戦と全く同一アセット、guid一致確認済み） | TEMP | 怒り編と全く同じ星空画像の使い回しで「不安の精神世界」の個性がゼロ | 1920×1080、不安を感じさせる歪んだ抽象背景（怒り編とは異なる配色・モチーフ） | yes |
| 3 | `questionFloorSprite` = `Floor.png` | 石畳テクスチャ（陰影・ハイライト・汚れ表現あり）で質感はそこそこ良い | FINAL寄り（量産感あり） | ストック風の汎用アセットに見え、専用世界観としては個性が薄い | 現状のままでも致命的ではない | no（優先度低） |
| 4 | `yesGateSprite`/`noGateSprite` = `YesGate.png`/`NoGate.png` | 単色の楕円形シルエットのみ。門・扉・文字等のディテールなし | TEMP | 「YES/NOへ進む入口」という重要な要素なのに、ただの塗りつぶし楕円で意味が伝わりにくい | 1920×1080（透過部分のみで可）。門・裂け目・光る渦などゲートと分かるデザイン | yes |
| 5 | `questionLeftFootSprite`/`questionRightFootSprite` = `leftfoot.png`/`rightfoot.png` | 単色白ベタ塗りの足跡シルエットのみ（陰影・質感なし） | TEMP | Floor.pngと比べて明らかに手抜き感がある平坦なシルエット | 現状サイズのまま、濃淡・にじみを付けた足跡テクスチャ | yes（小さい労力で改善可） |
| 6 | `AnxietyActor` | `AnxietyVertical.png` | FINAL | 特になし | 現状で問題なし | no |
| 7 | `anxietyCharacterSprite`/`contackCharacterSprite`（締めの対峙） | `AnxietyVertical.png`/`ContackVertical.png`をそのまま流用 | FINAL | 特になし | 現状で問題なし | no |
| 8 | 雨演出（`questionRainEnabled`） | Unity標準ParticleSystem | PROCEDURAL | 演出として妥当 | 現状で問題なし | no |
| 9 | HPゲージ | `FuanBattleController`にHUD参照なし（1発で決着する設計のため非表示） | ー（仕様の可能性） | 怒り戦にはHUDがあり不安戦には無いという非対称性 | 演出上必須ではないが統一感の検討余地あり | no |
| 10 | 隔離/消去選択（`EmotionResolutionFlow`共通） | 怒り編と同一実装 | PROCEDURAL/MISSING | 怒り編#8・#9と同一の問題がここでも発生 | 怒り編と共通の解決策で対応可 | yes |

---

## 4. 逃避編 (Escape.yarn / BedFlight, 「ベッド飛行」)

| # | Scene/Object | 現在の画像/描画方式 | 分類 | 販売画面としての問題点 | 必要な画像仕様 | AI仮置き価値 |
|---|---|---|---|---|---|---|
| 1 | `<<background "Room"/"RoomDay"/"ShoppingMall">>` (Escape.yarn現実パート) | 既存確認済み背景（`RoomBackground.jpg`：作り込まれた散らかった一人暮らし部屋のアニメ調イラスト、質感高い） | FINAL | 特になし | 現状で問題なし | no |
| 2 | `CityBackground`（3層パララックス、`mati_mae.png`/`mati_ushiro.png`/`kumo.png`） | クリーンなベクター調シルエット、完成度が高い | FINAL | 街のシルエット（ベクター）・ベッド（ドット絵）・コンタック（アニメ塗り立ち絵）と3種の画風が同一画面に混在する点は気になる | 現状で問題なし | no |
| 3 | `FlyingBed`（プレイヤー） | `touhikou/flying_bed_player.png`。丁寧に描き込まれたドット絵 | FINAL | 街との画風差はあるがシルエット表現として破綻はしていない | 現状で問題なし | no |
| 4 | `Contack`（クライマックスの追跡者） | `ContackVertical.png`を流用（コード内コメントは古い「青い丸」記述のままだが実際の割り当ては最新立ち絵） | FINAL | アニメ塗り立ち絵がドット絵の空に唐突に現れる画風混在。演出意図としては成立 | 現状で問題なし（意図的流用と判断） | no |
| 5 | `sky`（`closedSkyColor`→`openSkyColor`） | 1×1白スプライトの色Lerp、完全procedural | PROCEDURAL | 「開放感」を色だけで表現するミニマル演出として妥当 | 現状で問題なし | no |
| 6 | `HouseIntro`（開始演出の家シルエット） | procedural矩形+三角形+窓 | PROCEDURAL | 街のシルエット画風と統一感があり狙い通り | 現状で問題なし | no |
| 7 | `ContacBulletTemplate`（最終弾） | `AngerBattle/Sprites/BulletSprite.png`（灰色リングのベクター円） | TEMP/PROCEDURAL | 怒り戦の弾（procedural白円）と見た目が異なる別素材で細部の統一感に欠ける | 全ミニゲーム共通の弾スプライトへ統一 | yes（優先度低〜中） |
| 8 | `endFadeGroup`（終了時暗転） | CanvasGroupのアルファフェード | PROCEDURAL | 標準的な暗転演出として問題なし | 現状で問題なし | no |

---

## 5. 悲しみ編 (Sadness.yarn / SadnessBattle + MemoryRecall, 「記憶回想」)

| # | Scene/Object | 現在の画像/描画方式 | 分類 | 販売画面としての問題点 | 必要な画像仕様 | AI仮置き価値 |
|---|---|---|---|---|---|---|
| 1 | `<<background "RoomDay">>` (Sadness.yarn現実パート) | 既存確認済み背景 | FINAL | 特になし | 現状で問題なし | no |
| 2 | `SadnessMapEnvironment`（屋外/屋内Tilemap、SadnessBattleとMemoryRecall共通） | `pixelworld_complete_v.1.8`/`Cute_Fantasy_Free`のタイルセット | 第三者フリー素材 | **要ライセンス確認**（商用再配布・改変条件を配布元規約で確認要）。汎用RPGタイルセットの流用で「幼少期の記憶」の情緒を伝える専用背景としては個性が薄い | ライセンス次第で要差し替え。余裕があれば思い出らしい専用背景（暖色・ノスタルジック） | no（ライセンス確認が最優先） |
| 3 | `MotherTarget`/`FriendTargetA/B/C` | 同上タイルセットのキャラクターシート | 第三者フリー素材 | 同上ライセンス確認が必要 | 同上 | no |
| 4 | `MotherActor`/`MemoryFriendA/B/C`（MemoryRecallRoot） | 同上タイルセット系キャラクター | 第三者フリー素材 | 同上 | 同上 | no |
| 5 | `SadnessActor`（悲しみコンタック戦の最終ターゲット） | `Assets/SadnessBattle/Sprites/Sadness.png`。丁寧に描き込まれた高品質などっと絵風の少年キャラクター | FINAL寄り | 品質は高いが`MotherTarget`等の汎用フリー素材（16px級チビキャラ）とは描き込み密度・画風が明確に異なり戦闘画面内の統一感が損なわれる | 余裕があれば周辺キャラも同系統の画風へ統一 | no |
| 6 | 記憶回想専用のCG/一枚絵 | 存在しない（探索マップと会話テキストのみ） | MISSING | 幼少期の思い出という重要な情緒パートなのに専用の一枚絵が無く演出的な山場に欠ける | 1920×1080、母・友達との思い出を象徴する一枚絵CG（任意） | yes |
| 7 | 隔離/消去選択（悲しみ） | 怒り編と共通実装 | PROCEDURAL/MISSING | 怒り編#8・#9と同じ問題（消去時は完全無演出） | 怒り編と共通の解決策 | yes |
| 8 | HPバー | `SadnessBattleController`もHUD参照なし | ー | 不安戦と同様の非対称性 | 同上 | no |

---

## 6. 通常END (Ending.yarn ノード)

| # | Scene/Object | 現在の画像/描画方式 | 分類 | 販売画面としての問題点 | 必要な画像仕様 | AI仮置き価値 |
|---|---|---|---|---|---|---|
| 1 | `title: Ending`（Escape.yarn内） | 背景コマンド無し＝直前のRoom背景を維持したまま`Boss`/`Girlfriend`/`None`立ち絵で会話継続 | FINAL（流用としては） | 特になし | 現状で問題なし | no |
| 2 | 通常END確定時の終了処理 | `<<if all_emotions_isolated()>><<jump TrueEnd_Start>><<endif>>`のみ。条件を満たさない場合ノードがそのまま終了し、明示的な終了テキスト・画面が一切無い | **MISSING（本監査で最も重大な指摘）** | 3感情のうち1つでも「消去する」を選んだ場合（＝通常ENDルート）、物語が読み終わった後に何の合図もなくダイアログが止まる。「クリアした」手応えが皆無でバグと誤認されかねない | 通常END用の終了画面（タイトルへ戻る導線を兼ねた「END」カード、1920×1080）。最低限テキストだけでも明示的な終了演出を追加すべき | yes（最優先） |

---

## 7. TRUE END (`TrueEnd_Start`〜`TrueEnd_End`, EmotionResolutionFlow.cs, PrisonConsultationVisual.cs)

| # | Scene/Object | 現在の画像/描画方式 | 分類 | 販売画面としての問題点 | 必要な画像仕様 | AI仮置き価値 |
|---|---|---|---|---|---|---|
| 1 | `<<background "Office"/"Room">>` (TrueEnd_Start) | 既存確認済み背景の流用 | FINAL | 特になし | 現状で問題なし | no |
| 2 | `<<clock_glitch_intro>>`/`<<wake_glitch_intro>>` | 怒り編・不安編と共通のprocedural演出 | PROCEDURAL | 演出使い回し自体は妥当 | 現状で問題なし | no |
| 3 | `<<show_prison "Anger/Anxiety/Sadness">>`（`PrisonConsultationVisual.ShowPrison`） | procedural：`Shade`暗幕+各感情立ち絵+`CreateCageBar`で牢格子7本をUI Image矩形生成 | PROCEDURAL | `EmotionResolutionFlow.BuildCage`（3D空間バー）と本コンポーネント（UI空間バー）は**別々に実装された微妙に異なる牢屋表現**で、同じ「隔離」モチーフに2種類の見た目が存在する二重実装 | 両実装を1つの共通アート素材（牢格子スプライト）に統一 | yes |
| 4 | 「隔離する/消去する」選択の視覚差 | 隔離＝procedural牢屋落下演出あり／消去＝無演出（前述と同一） | MISSING | TRUE ENDには全員「隔離」が必須なのでTRUE END到達者には影響しないが、「消去」を選んだ大多数のプレイヤー（＝通常END行き）が一度もまともな演出を見られない | 前述の消去演出を用意すれば同時に解決 | yes |
| 5 | `TrueEnd_End`（最終ノード、"THE END"のみ） | テキストのみ、背景コマンド無し（直前のRoom背景のまま） | **MISSING** | TRUE ENDという最大の到達点にも関わらず「仕事を選ぶ」「彼女を選ぶ」という重い二択の結果に対し両者とも全く同じ"THE END"テキストのみで、選択による絵的な差分・専用CGが一切無い | 1920×1080、選択毎に異なる終了CG（最低限、既存立ち絵＋背景の組み合わせだけでも差別化） | yes（優先度高） |
| 6 | `TrueEnd_ConsultAnger/Anxiety/Sadness`（各感情との再会セリフ） | 立ち絵は`show_prison`の牢屋演出のみ、専用の会話用ポートレート無し | ー（PROCEDURALの範囲内） | 既存の縦長バトル立ち絵を流用しており会話シーンとしてはやや不自然なアスペクト比 | 現状のままでも致命的ではない | no |

---

## 8. 各ミニゲーム共通 (共通UI・タイトル/メニュー・アイコン)

| # | Scene/Object | 現在の画像/描画方式 | 分類 | 販売画面としての問題点 | 必要な画像仕様 | AI仮置き価値 |
|---|---|---|---|---|---|---|
| 1 | `AngerBattleHUD`（HPバー、`DeathFade`破片演出） | procedural単色矩形 | PROCEDURAL | ミニマルUIとして機能するが商用HUDとしては簡素 | 現状維持可。余裕があれば枠・アイコン付きゲージ画像 | no |
| 2 | HPバーの有無の不統一 | 怒り戦のみHUDあり、他ミニゲームには無い | ー | ミニゲーム間で情報量の一貫性が無く初見プレイヤーが戸惑う可能性 | 各ミニゲームの構造に応じてHUD有無を意図的に統一する設計判断が必要 | no |
| 3 | 弾スプライトの不統一 | 怒り戦＝procedural白円、他＝`BulletSprite.png`（灰色リング）をそのまま使用 | PROCEDURAL/TEMP | 同じ「デナイアル弾」設定なのに場面によって見た目が異なる | 全ミニゲーム共通の弾スプライトに統一 | yes |
| 4 | メインメニュー/タイトル画面/設定画面 | `_Core`/`_Presentation`のいずれにもタイトルロゴ・メインメニュー・設定画面に相当するGameObjectが存在しない（`MinigameLauncher`のF1デバッグメニューはエディタ専用でビルド対象外） | **MISSING** | 商用リリースにはタイトル画面（ロゴ、スタート/コンティニュー/設定/終了ボタン等）が事実上必須。現状は起動直後に本編が始まる、または何も表示されない状態の可能性が高い | 1920×1080タイトル画面一式（ロゴ・背景・ボタンUI）、設定画面（音量調整程度でも可） | yes（最優先事項の一つ） |
| 5 | アプリアイコン | `ProjectSettings.asset`の`m_BuildTargetIcons`/`m_BuildTargetPlatformIcons`が未設定 | **MISSING** | ビルドすると既定のUnityロゴアイコンのまま配布されてしまう | 各プラットフォーム規定サイズのアプリアイコン一式 | yes |
| 6 | スプラッシュ画面 | `m_ShowUnitySplashScreen: 1`、カスタムロゴ無し、Unity既定スプラッシュのみ | TEMP（既定のまま） | 必須ではないが商用感を出すならスタジオ/タイトルロゴの表示を検討 | 任意 | no |
| 7 | プロダクト名 | `productName: PKD` | ー | 「PKD」は内部コードネームらしく正式タイトルとして公開して良いか要確認（ビジュアルではないが第一印象に直結） | 正式タイトル名の確定 | ー |
| 8 | 隔離牢屋の二重実装（再掲） | `EmotionResolutionFlow.BuildCage`と`PrisonConsultationVisual.CreateCageBar`が別々に類似格子を生成 | PROCEDURAL | 保守性・見た目統一の両面で非効率 | 牢格子アート1種を作成し両実装から参照 | yes |

---

## 総括：優先度が特に高い項目

1. **通常ENDに終了画面が存在しない**（6章） — バグに見える致命的な欠落。
2. **メインメニュー/タイトル画面・アプリアイコンが未実装**（8章） — 商用リリースの前提条件。
3. **「消去する」選択の演出が全ミニゲーム共通で皆無**（2/3/5/7章の同一問題） — 選択の手応えに直結。
4. **AngerBattle/FuanBattleの精神世界背景が同一の汎用星空ストック画像**（2/3章） — 各感情パートの個性を出す絵作りが未着手。
5. **YesGate/NoGate/足跡スプライトが単色ベタ塗りの仮素材品質**（3章）。
6. **第三者フリー素材（pixelworld_complete_v.1.8 / Cute_Fantasy_Free）のライセンス確認**（5章） — 商用可否を必ず事前確認。
7. **TRUE ENDの「仕事を選ぶ/彼女を選ぶ」に絵的な差分が無い**（7章） — 物語のクライマックスとして弱い。

---

## 付記：defeatedSprite仮画像6点の再検討（前回fill-artタスクで追加）

前回`/fill-art`で`EnemyAnger.defeatedSprite`（AngerActor/AnxietyActor/MotherTarget/
FriendTargetA/B/C、計6箇所）に、単色背景+「AI_PLACEHOLDER」ラベル+ID+種別+サイズを
描いた識別用プレースホルダー画像を割り当てた。今回改めて用途を精査した結果、**削除候補**
として報告する。

**問題点**: `defeatedSprite`は「撃破時にごく短時間だけ表示されるフラッシュ演出」用の
任意フィールドで、未設定なら見た目が変わらないだけで動作に支障は無い
（`EnemyAnger.cs`のTooltipに明記）。一方、今回割り当てたプレースホルダーは
デバッグラベル入りの目立つ色矩形であり、これが一瞬でも実際にプレイ中へ描画されると
「敵キャラの上にAI_PLACEHOLDERの文字が一瞬光る」という、未設定（変化なし）より
明確に悪い見た目になる。背景・UI等の「常時表示される仮素材」には識別用プレースホルダーが
適切だが、この用途には適さない。

**技術的背景**: `EnemyAnger.cs`のコメントによれば「素材のColorは乗算ティントのため、
色付きスプライトをColor変更だけで白くすることはできない」——つまり単純な色変更では
フラッシュを表現できないという制約から、専用の白いシルエット画像を用意する設計に
なっている。

**削除候補の内容**:
- `Assets/Scenes/SampleScene.unity`内の6箇所の`defeatedSprite`参照を`{fileID: 0}`に戻す
- `Assets/Art/AI_Placeholder/`内の対応するPNG+`.meta`ファイル6組を削除
- `Docs/art_manifest.yaml`の該当6エントリを`status: missing`に戻すか削除

**代替案（実装するかは別途判断、今回は未実施）**:
1. 本番の白シルエット画像6点を用意できるまでは、何もせず「変化なし」のフォールバックの
   ままにする（現状の設計通りの正常な状態に戻すだけ）
2. またはSprite差し替えではなく、Material/Shaderで一時的にAdditive発光させる手法に
   変更すれば、キャラクターごとの個別画像を用意せずに「白フラッシュ」を実現できる
   （HPバー等の既存procedural方針と一貫性がある）

いずれもコード変更を伴う判断のため、今回は削除するかどうかの判断材料の提示のみとし、
実際の削除・変更は行っていない。
