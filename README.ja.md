# Big Old Guy

[中文](README.md) · [Windows 版をダウンロード](https://github.com/Resuty/big_old_guy/releases/latest)

Unity と C# で制作した、AI と対戦する1人用カードゲームのデモです。54枚のトランプにカード効果とHPのルールを加えています。相手の手に対してパスするとダメージを受けますが、HPを使って追加のカードを引くこともできます。

Unity の学習とポートフォリオとして制作しています。なじみのあるトランプに、手札とHPをどう使うかという判断を加えてみたいと考えました。

![Windows版の対戦画面](Docs/screenshots/gameplay-zh.jpg)

![日本語の設定画面](Docs/screenshots/settings-ja.jpg)

## 遊び方

[Releases](https://github.com/Resuty/big_old_guy/releases) から `BigOldGuy-v0.1.0-Windows-x64.zip` をダウンロードし、解凍して `BigOldGuy.exe` を起動してください。同じフォルダー内の Data フォルダーと DLL も必要です。Unity のインストールは不要です。

対応プラットフォームは Windows 64 bit です。設定画面から日本語・中国語・英語を切り替えられます。BGMと効果音の音量、AIの強さと行動速度も変更できます。

| 操作 | 内容 |
| --- | --- |
| カードを左クリック | 選択・選択解除 |
| カードを長押し | 効果の説明を表示 |
| 「出す」／「パス」 | 選んだカードを出す／応答を見送る |
| 「ヒント」 | 出せる組み合わせを選択。確定はプレイヤーが行います |
| 右上の歯車／Esc | 設定を開く |
| 設定 → 対戦 → 新しい対戦 | 最初からやり直す |

## 基本ルール

- 両者ともHP 3、手札8枚で開始します。先手はじゃんけんで決まります。
- 直前の手と同じ種類で、主となる数字がより大きい組み合わせを出します。ボムとロケットには例外があります。対応する組み合わせは11種類で、連続する三枚組の「飛行機」は含みません。
- パスするとHPを失い、新しいトリックが始まります。HPが0未満になると敗北します。手札を出し切ること自体は勝利条件ではありません。
- 通常は行動開始時に1枚引きます。購入できる場面では、HPを1使ってさらに4枚引けます。先手の最初の行動ではドローと購入を行いません。
- 効果は「出す前」「出した後」「捨て札に移った後」の順に処理します。数字のコピー、追加ドロー、手札への回収、山札の並べ替えなどがあります。
- 捨て札の再利用、手札が少なくなった際の補充、ダメージ後の補償ドローがあります。

詳しいルールと効果の説明は、ゲーム内の「設定 → 対戦 → ルールブック」にまとめています。

## コードについて

ゲームのルールは通常の C#、画面は uGUI で実装しています。カードには固定のIDを付け、手札・山札・捨て札ではそのIDを管理します。カード画像は表示だけを担当するため、ルールを画面から切り離して確認できます。

| ファイル | 担当する処理 |
| --- | --- |
| [GameEngine.cs](Assets/CardDuel/Core/GameEngine.cs) | ドロー、カードの提出、ターン、効果、勝敗 |
| [Model.cs](Assets/CardDuel/Core/Model.cs) | カード、状態、操作コマンド、出した手の記録 |
| [HandEvaluator.cs](Assets/CardDuel/Core/HandEvaluator.cs) | 組み合わせの判定と比較 |
| [AiController.cs](Assets/CardDuel/Core/AiController.cs) | 観測できる情報に基づくAIの行動選択 |
| [TeachingGameScreen.cs](Assets/CardDuel/LearningView/TeachingGameScreen.cs) | 対戦画面と入力処理 |
| [TeachingSettings.cs](Assets/CardDuel/LearningView/TeachingSettings.cs) | 設定の保存、言語切り替え、BGM、効果音 |
| [RuleChecks.cs](Assets/CardDuel/Verification/RuleChecks.cs) | 42グループのルール検証 |

カードを出す前に合法性を確認し、その後で状態を更新します。数字のコピーは、その手だけに使う記録へ反映し、カード本来の数字は変更しません。AIには自分の手札と公開情報を渡し、相手の伏せた手札や山札の順序を直接参照させていません。

## 開発環境

Unity **6000.6.0f1** を使用しています。主な依存パッケージは URP 2D、uGUI、Input System です。素材は Git LFS で管理しています。

```powershell
git clone https://github.com/Resuty/big_old_guy.git
cd big_old_guy
git lfs pull
```

Unity Hub にプロジェクトを追加し、`Assets/Scenes/Game.unity` を開いて実行してください。`Card Duel → Build Windows Release` から、ルール検証と5つの固定初期状態による自動対戦を実行した後、Windows 版をビルドできます。出力先は `Builds/v0.1.0` です。[技術解説書（中国語）](Docs/CardDuel完整对局技术讲解手册.pdf) に処理の流れを記載しています。

## 制作過程と制限

ルールとゲームの方向性は私が考えました。このリポジトリには、C#を学びながらuGUIの画面を組み直した過程も残しています。`Docs` 内の旧実装は学習用の比較資料で、ゲームのコンパイル対象には含まれません。

現在はローカルの1人用デモで、オンライン対戦はありません。直前の手は画面上では先頭8枚まで表示しますが、枚数と記録はログに残ります。演出は簡素で、複数のPCやモニターでの動作確認はまだ十分ではありません。

## 素材・権利表記

カード画像、UI画像、効果音は Kenney、フォントは Noto Sans CJK を使用しています。[音声・フォントの出典](Assets/CardDuel/Licenses/ASSET-SOURCES.md) と [画像の出典](Assets/CardDuel/Licenses/PUBLIC-ART-SOURCES.json) に詳細があります。BGMはゲームとともに公開配布できることを制作者が確認しています。KenneyのCC0素材には含まれず、音源の抽出・再利用を許諾するものではありません。ソースコードは閲覧と作品紹介のために公開しており、プロジェクト独自のコードには現時点で別途オープンソースライセンスを設定していません。
