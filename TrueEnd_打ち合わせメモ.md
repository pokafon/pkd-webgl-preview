# TRUE END（相談パート）打ち合わせメモ

## バグ報告（2026-09-02）

**症状**：TRUE END内、コンタックが怒り・不安・悲しみに順番に相談するパート（[Escape.yarn](Assets/Yarn/Escape.yarn) `title: TrueEnd_Consult`）で、
- 怒りと相談した後、不安・悲しみの選択肢が出てこない（ループする＝怒りの選択肢だけが繰り返し出続ける）。
- 本来は3つ（怒り／不安／悲しみ）の選択肢が出るはずが、実際には1つしか出ていない。

**再現箇所**：`TrueEnd_Consult`ノード（[Escape.yarn:170](Assets/Yarn/Escape.yarn:170)〜246）。3つの`->`選択肢はそれぞれ`<<if $consulted... == false>>`条件付きで、選んだ後`<<set $consulted... = true>>`→`<<jump TrueEnd_Consult>>`で同じノードに戻ってくる作り。

**現状の調査でわかったこと**：
- このTRUE ENDパート一式（`TrueEnd_Start`〜`TrueEnd_End`）は**未コミットの新規追加**（`git diff HEAD`で全体が差分として出る＝まだ一度も実機で通しプレイされていない可能性が高い）。
- Yarnスクリプト自体の構文（インデント、条件式、変数宣言）は静的に見た限り問題なし。重複タイトルや変数の二重宣言もなし。
- シーン側`DialogueRunner`の`showUnavailableOptions`は`true`（[SampleScene.unity:27018](Assets/Scenes/SampleScene.unity:27018)）。これはEscape.yarn冒頭のコメント「各選択肢の一方は`<<if false>>`で非活性にする」という設計前提と一致しており、本来は条件を満たさない選択肢も**グレーアウト表示**されるはず（消えるのではなく）。
- 選択肢UI（OptionsListView）はYarn Spinnerパッケージ標準のプレハブを使用しており、プロジェクト側の独自実装ではない。
- → 静的には原因を特定しきれず、実際にPlay modeで再現させないと切り分けが難しい（Unityバッチモードはコンパイル／シーンYAMLのエラーしか検出できず、この手のランタイム挙動は検出できない）。

## 対応（2026-09-02・実装済み）

**結論**：ゲーム本来のロジックはバグっていなかった。原因はデバッグメニュー（F1）側。
- `consultedAnger`等のリセットは`TrueEnd_Start`ノード内の`<<set...=false>>`でしか行われない。
- デバッグメニューの「TrueEnd_Consult」ボタンは`TrueEnd_Start`を経由せず`DialogueRunner.StartDialogue("TrueEnd_Consult")`で直接ジャンプする実装だったため、前回プレイ分の値（誰か相談済み扱い）を引き継いでしまい、選択肢が最初から欠けて見えた。
- 通常ルート（エンディングから自然に進む／「TrueEnd_Start」ボタンから入る）では再現しない。

**修正内容**：[MinigameLauncher.cs](Assets/AngerBattle/MinigameLauncher.cs)の`JumpToStoryNode`に、ジャンプ先が`TrueEnd_Start`以外の`TrueEnd_*`ノードの場合は`$consultedAnger`/`$consultedAnxiety`/`$consultedSadness`を`false`にリセットしてからジャンプする処理を追加。デバッグメニューからどのTrueEndノードに飛んでも、フラグが前回プレイの値を引きずらなくなった。

Unityバッチモードでコンパイル確認済み（エラーなし）。

**実機確認（2026-09-02・Unity Pipeline経由）**：Editorを開いてPlay mode中に、リフレクションで`JumpToStoryNode`を直接呼び、事前に`$consultedAnxiety`/`$consultedSadness`をtrueにした状態から実行 → 3フラグとも正しく`false`にリセットされることを確認。コンソールにエラー・警告なし。修正は実機で機能している。

## シナリオ文書との照合・反映（2026-09-02）

`シナリオ全文_読みやすい版.md`とYarn各ファイルを突き合わせ、ユーザー判断のもと以下を反映済み：

- **①エンディング本編の彼女の台詞**：Yarn側（[Escape.yarn:110](Assets/Yarn/Escape.yarn:110)「将来のこともそろそろ決めたいね。」）が正、MD側（L939）をそれに合わせて修正。
- **②「部屋には、僕が選んだものが一つもない。」**：MD側の確定事項が正。Yarnの`Ending`ノード（[Escape.yarn:115](Assets/Yarn/Escape.yarn:115)付近）に欠落していた一文を追加。
- **③怒り編シーン1の地の文**：MD側（「あと少しで仕事が終わる。帰ったら、PCで昨日の作業の続きをやろう。」）が正。[Anger.yarn:6-8](Assets/Yarn/Anger.yarn:6)のYarn独自追加描写（「近代化の波〜」）を削除しMDの文言に差し替え。
- **④TRUE END全体**：Yarn実装が正。MD側に存在しなかったため、「エンディング（新規追加）」章の直後に「# TRUE END（新規追加）」章を新規追加（相談パートの台詞・確定事項を記載）。あわせて「## メモ：現行シナリオ設計・確定事項」内の「エンディング：確定事項」に、隔離3つ揃うとTRUE ENDへ分岐する旨の注記を追加（単一エンディング前提だった記述を更新。指輪の言及も①に合わせて削除）。「メモ」章自体の他の記述（章構成・作品の核・人物設計・優先順位ルール）は現行のまま維持（ユーザーとの相談の結果、丸ごと削除はせず該当箇所のみ更新する方針）。

Assets/Refresh後、Unity Pipeline経由でコンソール確認：エラー・警告なし。

## 【真の原因判明・修正済み】選択肢が1つしか出ない本当の理由（2026-09-02）

上記「デバッグメニュー側が原因」という結論は誤りだった（副次的な実在のバグではあったので修正はそのまま活かす）。Unity Pipeline経由でEditorのPlay modeに実際に入り、`LineAdvancer.RequestNextLine()`/`OptionItem.InvokeOptionSelected()`を外部からリフレクションで叩いて通しプレイを自動化したところ、**正規ルート（Ending→TrueEnd_Start→TrueEnd_Consult）でも怒りの選択肢だけが無限に繰り返される**ことを確認。

**根本原因**：`TrueEnd_Consult`ノードの3つの`->`選択肢は、それぞれの本文（`<<show_prison>>`+セリフ8行+`<<hide_prison>>`+`<<jump>>`、10文超）が長すぎて、**Yarn Spinnerコンパイラが3つを1つの選択肢グループとして扱わず、1つずつ別々の`showOptions`呼び出しに分解してコンパイルしていた**。コンパイル済みバイトコード（`YarnProject.Program.Nodes["TrueEnd_Consult"].Instructions`をリフレクションでダンプして確認）で、`addOption`が3回あるのに`showOptions`も3回（1回ずつ）呼ばれていることを直接確認して特定した。本文を数行に削った状態で試すと`addOption`3回+`showOptions`1回に変わることも確認済み（本文の分量がグルーピングを壊す境界であることを実験で特定）。

同じファイル内の他の正常な選択肢（`Escape`ノードの「たまった家事をやる／やらない」等）は本文が1〜2行と短く、この問題を踏んでいなかった。

**修正内容**：`TrueEnd_Consult`の3つの選択肢の本文を「フラグをtrueにする＋専用ノードへジャンプ」の2行だけに縮め、実際のセリフ（`show_prison`〜`hide_prison`）は`TrueEnd_ConsultAnger`/`TrueEnd_ConsultAnxiety`/`TrueEnd_ConsultSadness`という3つの新規ノードに分離した（プレイヤーに見える内容・演出は一切変更なし）。各ノードの最後は`TrueEnd_ConsultLoop`（`<<jump TrueEnd_Consult>>`だけの中継ノード）経由で`TrueEnd_Consult`に戻る。

**実機確認**：Unity Pipelineで通しプレイを自動実行し、
- 1回目：3つとも選択肢に表示される（`n=3`）
- 相談後：相談済みの選択肢だけグレーアウト（`[--]`）され、残り2つは選べる
- 3人全員話し終えると自動で`TrueEnd_AfterConsult`→「仕事を選ぶ／彼女を選ぶ」→`THE END`まで正常に完走
することを確認済み。コンソールにエラー・警告なし。

## 副次対応：Escape.yarn:96の警告修正（2026-09-02）

`\<b\>ここじゃないどこかに行きたい\</b\>`がYarnコンパイラで「single '<' and '>' — コマンドの書き間違いでは？」という警告を出していた件。MDの`**太字**`は文書側の強調記法であり（話者名にも同じ記法が使われているだけで実際のセリフには反映されていない）、プロジェクト内の他のYarnファイルにも`<b>`タグの使用例が一切なかったため、タグとエスケープを削除しプレーンテキスト化。Unity Pipeline経由でconsoleをクリア→Assets/Refreshで再確認し、警告が消えたことを確認済み。
