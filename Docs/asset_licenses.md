# サードパーティ素材のライセンス調査（商用リリース向け）

調査日: 2026-09-03
対象: `Assets/Sprites/Tiles/pixelworld_complete_v.1.8/`、`Assets/Sprites/Tiles/Cute_Fantasy_Free/`
（ビジュアル監査 `Docs/visual_audit.md` 5章で指摘した第三者フリー素材2点）

この調査は各パックに同梱されている`license.txt`/`read_me.txt`の内容を実際に開いて確認したもの。
ライセンス条件そのものは配布元が定めるものであり、以下は同梱ファイルの引用と、
このプロジェクトでの使用箇所の突き合わせに限る。

---

## 1. pixelworld_complete_v.1.8（Bitglow） — 商用利用OK

`Assets/Sprites/Tiles/pixelworld_complete_v.1.8/license.txt` 全文:

```
Bitglow Asset Pack – License

Copyright © 2025 Bitglow. All rights reserved.

This asset pack is licensed under the Bitglow Asset License.

You are granted a non-exclusive, non-transferable license to use these assets
in personal and commercial projects.

You may modify the assets for use within your project.

You may NOT:
– Resell the assets (original or modified)
– Redistribute the assets as standalone files
– Include the assets in another asset pack or resource pack
– Share the files with others outside your project team

Credit to "Bitglow" is appreciated.
```

**判定: 商用プロジェクトでの使用が明示的に許可されている。** 改変も可。禁止事項は
「素材単体の再販売」「素材だけを取り出しての再配布」「別の素材集への転載」「プロジェクト
チーム外への素材ファイル単体での共有」のみで、いずれもゲーム本体への組み込みには抵触しない。
クレジット表記は必須ではないが「appreciated」（歓迎）とあるので、クレジット欄があるなら
"Bitglow"の記載を検討するとよい。

同ディレクトリには同一発行者（Bitglow）の別パック`Assets/Sprites/Tiles/pixelinterior_LRK_v1.1/`
も存在し、`license.txt`を確認したところ同一条件（商用利用可）だった。

**このプロジェクトでの使用箇所**: `MotherTarget`/`MotherActor`のキャラクターシート
（`Characters/FemaleCharacter/PNGs/F_idle_left-Sheet.png`）等、悲しみ戦・記憶回想の
一部キャラクター。→ **対応不要（差し替え不要）**。

---

## 2. Cute_Fantasy_Free — **非商用限定。商用リリースのブロッカー（2026-09-03 対応済み）**

> **対応済み**: `FriendTargetA/B/C`・`MemoryFriendA/B/C`は同一シーン内で既に使用されている
> `pixelworld_complete_v.1.8`（Bitglow、商用利用可）の`MaleCharacter`シートへ差し替え、
> `Bridge_Wood_6`は単色の手続き描画（procedural）スプライトへ差し替えた。
> `Assets/Sprites/Tiles/Cute_Fantasy_Free/`フォルダ自体もプロジェクトから完全に削除し、
> guid参照・ファイルともにプロジェクト内0件であることを確認済み。詳細は次のセクション以下、
> および今回の修正作業ログを参照。

`Assets/Sprites/Tiles/Cute_Fantasy_Free/Cute_Fantasy_Free/read_me.txt` 全文:

```
Hello! Thank you for downloading the Cute Fantasy asset pack.

This project will be getting updates over time. This version of the asset
pack is not final and there will be few more additional sprites.

License - Free Version
   - You can use these assets in non-commercial projects.
   - You can modify the assets.
   - You can not redistribute or resale, even if modified

If you like the asset pack leave a comment. It helps to support the asset
pack and get more people to see it. Thanks!
```

**判定: ファイル自体に「License - Free Version」「非商用プロジェクトでのみ使用可」と明記。
現状バンドルされているのはフリー版の規約のみで、商用可否は不明ではなく明確に「不可」。**
このパックは俗に"Cute Fantasy"（itch.ioで配布されている作品と思われる）として知られているが、
リポジトリ内には配布者名・入手元URLの記載は無い。有償版（商用ライセンス付き）を別途購入して
いる場合は条件が異なる可能性があるため、**購入記録の有無をユーザー側で確認する必要がある**
（本調査では確認不可）。購入記録が無い、または不明な場合は商用リリース前に必ず差し替えること。

**このプロジェクトでの使用箇所**（`SampleScene.unity`内のguid参照を実際に数えて確認）:

| ファイル | 使用回数 | 用途 |
|---|---|---|
| `Cute_Fantasy_Free/Player/Player.png` | 4箇所 | `FriendTargetA`/`FriendTargetC`（悲しみ戦）+ 対応する`MemoryRecallRoot`側の友達キャラクター |
| `Cute_Fantasy_Free/Player/Player_Actions.png` | 2箇所 | `FriendTargetB`（悲しみ戦）+ 対応する`MemoryRecallRoot`側の友達キャラクター |
| `Cute_Fantasy_Free/Outdoor decoration/Bridge_Wood.png` | 1箇所 | 記憶回想の屋外マップ（`SadnessMapEnvironment`）の装飾オブジェクト |

キャラクタースプライトだけでなく、屋外マップの装飾（橋）にも使われている点に注意
（監査時点では見落としがちな箇所）。

**対応結果（2026-09-03）**:
1. `FriendTargetA`/`FriendTargetC`/`MemoryFriendA`/`MemoryFriendC`（4箇所）→
   `pixelworld_complete_v.1.8/Characters/MaleCharacter/PNGs/M_idle_left-Sheet.png`（frame 0）へ
   差し替え、元の見た目のサイズに合わせて`localScale`を補正（0.625, 0.568, 1）。
2. `FriendTargetB`/`MemoryFriendB`（2箇所）→
   `pixelworld_complete_v.1.8/Characters/MaleCharacter/PNGs/M_walk_front-Sheet.png`（frame 0）へ
   差し替え、`localScale`を補正（0.708, 0.944, 1）。A/C側と別の見た目にする、という
   元のデザイン（AとCが同じ見た目、Bだけ別）はそのまま維持した。
3. `Bridge_Wood_6`（1箇所）→ 単色（wood-brown）の1x1手続き生成スプライト
   （`Assets/Sprites/ProceduralWhitePixel.png`を着色）に差し替え。橋の下の支柱のような
   小さな装飾のため、見た目への影響は軽微。
4. Play Modeで実際に表示し、目視でサイズ・見た目に破綻がないことを確認済み。
5. `Assets/Sprites/Tiles/Cute_Fantasy_Free/`フォルダを完全に削除。プロジェクト全体を
   guid検索し、残存参照0件・残存ファイル0件を確認済み。

商用ライセンス購入の有無を調べる必要はもう無い（素材自体を使わないことにしたため）。

---

## 総括

| パック | 発行元 | 商用利用 | 対応要否 |
|---|---|---|---|
| pixelworld_complete_v.1.8 | Bitglow | 可 | 不要 |
| pixelinterior_LRK_v1.1 | Bitglow | 可 | 不要 |
| Cute_Fantasy_Free | 不明（同梱ファイルに記載なし） | 不可（フリー版規約のみ確認） | **対応済み：素材ごと削除し参照0件を確認（2026-09-03）** |
