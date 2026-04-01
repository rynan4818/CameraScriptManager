# CameraScriptManager

Beat Saber のカメラスクリプト (`SongScript.json` / CameraPlus MovementScript 形式 JSON) を整理、配布、配置するための Windows 用デスクトップツールです。

[ChroMapper-CameraMovement](https://github.com/rynan4818/ChroMapper-CameraMovement) や [ScriptMapper](https://github.com/rynan4818/ScriptMapper) で作成したスクリプトを、`MapScripts`、`SongScripts`、`Copier` の 3 つの運用フローでまとめて管理できます。

特に `SongScripts` タブは、[CameraSongScript](https://github.com/rynan4818/CameraSongScript) の `UserData/CameraSongScript/SongScripts` フォルダ向けに JSON / ZIP を一括管理する用途を想定しています。

## 特徴

* **SongScripts管理** - `SongScripts` フォルダ内の `.json` / `.zip` を一覧化し、metadata の編集保存、BeatSaver metadata 取得、プレイリスト作成、未取得譜面の BeatSaver ダウンロードを行えます
* **MapScripts管理** - `CustomLevels` / `CustomWIPLevels` 内の既存スクリプト `.json` を一覧化し、metadata 追加、フォルダ出力、ZIP 出力、元データ照合、プレイリスト作成を行えます
* **Copier機能** - `.json` やアーカイブ (`.zip` / `.7z` / `.rar` / `.tar` / `.gz`) を取り込み、BeatSaver ID(bsr) を使って対応譜面フォルダへ自動コピーできます
* **BeatSaver API / SongDetailsCache連携** - ID から曲名、作者名、BPM などを補完し、ローカルキャッシュで取得を高速化します
* **metadata編集** - `cameraScriptAuthorName`、`songName`、`levelAuthorName`、`bpm`、`duration`、`avatarHeight`、`description` などを JSON に埋め込めます
* **バックアップ対応** - `MapScripts`、`SongScripts`、`Copier` の各更新前に元データを自動バックアップできます
* **配布用パッケージ作成** - `MapScripts` から ZIP / フォルダ出力を行い、`CameraSongScript` の `SongScripts` フォルダ向け配布物を作成できます
* **更新チェック** - 起動時に 1 日 1 回以下で最新版を確認し、新しい `CameraScriptManager` があればリリースページを案内します

## 動作環境

* Windows 10 / 11
* [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) ※Releaseは.exeにランタイムを同梱

## インストール

1. [リリースページ](../../releases) から最新の `CameraScriptManager` をダウンロードします
2. ダウンロードした ZIP を任意のフォルダへ展開します
3. `CameraScriptManager.exe` を実行します

## 初回設定

<img width="971" height="368" alt="image" src="https://github.com/user-attachments/assets/45aa1fc6-9e75-4e87-9513-f57339f22a96" />

起動後は `Settings` タブで以下を設定してください。

* `CustomLevels`
* `CustomWIPLevels`
* `SongScripts`

必要に応じて、以下も設定します。

* `元データ検索(アーカイブ/JSON) 1〜3`
* `バックアップルート`

## 使い方

アプリケーションは `SongScripts` / `MapScripts` / `Copier` / `Settings` の 4 タブで構成されています。

### SongScriptsタブ

`SongScripts` フォルダ内の `.json` / `.zip` を一覧管理するタブです。`CameraSongScript` の `SongScripts` フォルダをそのまま対象にできます。

スキャン時はまず `BeatSaver ID(bsr)` ベースの一致候補を表示し、その後バックグラウンドで譜面 hash の追加照合を進めます。上部の進捗表示で `hash検索` の状態を確認できます。

<img width="1117" height="390" alt="image" src="https://github.com/user-attachments/assets/cb66211b-cd7d-4a32-b388-b8ae36b6734e" />

<img width="1246" height="576" alt="image" src="https://github.com/user-attachments/assets/c99e4db5-d5b5-439b-9563-3c3021470ba9" />

<img width="1246" height="576" alt="image" src="https://github.com/user-attachments/assets/5a86cd62-ba83-48b1-9ab5-f6e8ba7108ca" />

<img width="1233" height="386" alt="image" src="https://github.com/user-attachments/assets/debaad80-62c2-44be-8e0b-0629074122a6" />

<img width="850" height="459" alt="image" src="https://github.com/user-attachments/assets/c90b2513-597f-4bc5-83cf-e5f36be6db85" />

#### ツールバーボタン

| ボタン / 項目 | 説明 |
|---|---|
| **スキャン** | `SongScripts` フォルダをスキャンし、一覧と譜面照合結果を更新します |
| **メタ情報追加** | チェックした行の metadata を元ファイルへ保存します。`.json` だけでなく ZIP 内の対象 `.json` も更新されます |
| **プレイリストを作成** | チェックした行から Beat Saber プレイリスト (`.bplist`) を生成します |
| **譜面metadataを表示** | `songSubName` などの譜面 metadata 列の表示 / 非表示を切り替えます |

#### DataGrid列

| 列 | 説明 |
|---|---|
| **選択** | 保存、プレイリスト作成などの対象に含める行を切り替えます |
| **編集済** | 未保存の変更がある行にチェックが付きます |
| **ID** | `metadata.mapId` またはファイル / フォルダ名から推定した BeatSaver ID。編集、ロックが可能です |
| **BeatSaver** | BeatSaver ページの `Open` リンクです。未取得譜面は `[取得]` ボタンで BeatSaver から `CustomLevels` へ展開できます |
| **hash** | `metadata.hash`。編集、ロックが可能です |
| **cameraScriptAuthorName** | スクリプト作者名。EXCEL風のフィルコピー、ロックが可能です |
| **songName** | 曲名。ロックが可能です |
| **songSubName** | 曲サブ名。ロックが可能です |
| **songAuthorName** | 曲アーティスト名。ロックが可能です |
| **levelAuthorName** | 譜面作者名。ロックが可能です |
| **BPM** | BPM。ロックが可能です |
| **Duration** | 曲長 metadata。ロックが可能です |
| **AvatarHeight(cm)** | 想定アバター身長。ロックが可能です |
| **Description** | 説明文。ロックが可能です |
| **CustomLevelsフォルダ** | 一致した `CustomLevels` 側譜面フォルダ一覧です |
| **CustomWIPLevelsフォルダ** | 一致した `CustomWIPLevels` 側譜面フォルダ一覧です |
| **ソース** | `SongScripts` 配下での読み込み元パスです |
| **形式** | 元データの形式です。`JSON` または `ZIP` が表示されます |

#### 右クリックメニュー

| メニュー項目 | 説明 |
|---|---|
| **切り取り / コピー / 貼り付け** | セル値を操作します |
| **BeatSaverからmetadata取得** | 選択行の ID を使って BeatSaver metadata を反映します |
| **選択した未取得譜面を取得** | 未導入譜面だけを BeatSaver から取得して `CustomLevels` に展開します |
| **対象セルの項目をロック / ロック解除** | セルごとの編集可否を切り替えます |
| **選択 ON/OFFを切り替え** | `選択` チェックをトグル切り替えします |
| **エクスプローラーで開く** | 元ファイルの配置フォルダを開きます |

#### 保存とバックアップ

* 保存先は元ファイルそのものです
* `.json` はそのまま上書き保存されます
* `.zip` は対象 JSON のみ更新し、それ以外の ZIP 内容は維持されます
* `SongScripts保存時にバックアップを作成する` が ON の場合、更新前ファイルを既定では `UserData/backup/SongScripts` 配下へ相対パスつきで退避します

### MapScriptsタブ

`CustomLevels` / `CustomWIPLevels` フォルダ内にある既存の スクリプト`.json` を一覧管理するタブです。

<img width="1233" height="533" alt="image" src="https://github.com/user-attachments/assets/f9b0218d-a83d-4d0d-9b02-42dc9c266c7b" />

<img width="1233" height="526" alt="image" src="https://github.com/user-attachments/assets/c4bd5d62-20cb-4fd7-8bb1-0b3cd5679678" />

<img width="1071" height="288" alt="image" src="https://github.com/user-attachments/assets/602a440b-106a-445c-8e09-fefc2dd4a0bc" />

#### ツールバーボタン

| ボタン | 説明 |
|---|---|
| **スキャン** | `CustomLevels` / `CustomWIPLevels` を走査し、カメラスクリプト一覧を更新します |
| **メタ情報追加** | `選択` チェックが入った行の JSON に metadata を書き込みます |
| **フォルダ出力** | 選択した項目を `yyyyMMdd_HHmmss_OUTPUT` フォルダへ出力します |
| **ZIP出力** | 選択した項目を配布用 ZIP として出力します |
| **プレイリストを作成** | 選択した項目から Beat Saber プレイリスト (`.bplist`) を生成します |
| **元データ照合** | `Settings` で指定した元データ検索フォルダ内のアーカイブ / JSON と、譜面内スクリプトを `Movements` 件数、`Duration + Delay` 合計値、`Duration` 統計値（2番目の最大値 / 2番目の最小値 / 中央値 / 最頻値）で照合します |

#### DataGrid列

| 列 | 説明 |
|---|---|
| **選択** | 処理対象に含める行を指定します |
| **編集済** | 未保存の変更がある行にチェックが付きます |
| **ID** | BeatSaver のマップ ID。編集、ロックが可能です |
| **cameraScriptAuthorName** | スクリプト作者名。EXCEL風のフィルコピー、ロックが可能です |
| **songName** | 曲名。ロックが可能です |
| **songSubName** | 曲サブ名。ロックが可能です |
| **songAuthorName** | 曲アーティスト名。ロックが可能です |
| **levelAuthorName** | 譜面作者名。ロックが可能です |
| **BPM** | BPM。ロックが可能です |
| **AvatarHeight(cm)** | 想定アバター身長。ロックが可能です |
| **Description** | 説明文。ロックが可能です |
| **曲長(egg)** | 譜面フォルダ内 egg 音声の長さです |
| **ｽｸﾘﾌﾟﾄ長** | `Movements` 内の `Duration + Delay` 合計です |
| **差分(beat)** | 曲長とスクリプト長の差を beat 単位で表示します |
| **ファイル名** | 対象 JSON ファイル名です |
| **フォルダ** | 譜面フォルダ名です |
| **ソース** | `CustomLevels` / `CustomWIPLevels` の別です |
| **元データ(アーカイブ/JSON)** | 元データ照合で見つかった候補一覧です |
| **ハッシュ** | 譜面の hash です |

#### 右クリックメニュー

| メニュー項目 | 説明 |
|---|---|
| **切り取り / コピー / 貼り付け** | セル値を操作します |
| **BeatSaverからメタデータ取得** | 選択行の ID から BeatSaver metadata を反映します |
| **選択したIDをコピー** | 選択行の BeatSaver ID をクリップボードへコピーします |
| **対象セルの項目をロック / ロック解除** | セルごとの編集可否を切り替えます |
| **選択 ON/OFFを切り替え** | `選択` チェックをトグル切り替えします |
| **エクスプローラーで開く** > **譜面フォルダ** | 対象譜面フォルダを開きます |
| **エクスプローラーで開く** > **元データ(アーカイブ/JSON) フォルダ** | 照合した元データの保存先フォルダを開きます |

#### MapScriptsの出力形式

`フォルダ出力` と `ZIP出力` は、`Settings` の `MapScripts ZIP出力名` と `MapScripts ZIP内配置` に従います。

既定の `1. JSON名そのまま + 指定名フォルダに格納` は、`CameraSongScript` の `SongScripts` フォルダへそのまま置きやすい構成です。

### Copierタブ

カメラスクリプトファイルを譜面フォルダへコピー配置するタブです。`.json` やアーカイブをドラッグ＆ドロップまたはファイル選択で読み込み、対応譜面へ自動マッチングして配置できます。

#### ツールバー

| 項目 | 説明 |
|---|---|
| **譜面metadataを表示** | `songSubName` などの譜面 metadata 列を表示 / 非表示にします |
| **コピー時にmetadataをUI内容で再生成** | コピー時にUIに入力された metadata で JSON を更新します |
| **リネーム** | 新規取り込み時の既定リネーム方式を選択します。`無し` / `SongScript` / `IdAuthorSongName` / `カスタム` から選択できます |
| **再チェック** | `CustomLevels` / `CustomWIPLevels` を再スキャンし、照合結果を更新します |
| **クリア** | 取り込み済み一覧をクリアします |
| **コピー実行** | チェックされたコピー先へスクリプトを配置します |
| **Cache** | `SongDetailsCache` の参照状態です。緑 = ヒット、赤 = ミス、灰 = 未実行です |

#### ドロップゾーン

以下のファイルを取り込めます。

* `.json`
* `.zip`
* `.7z`
* `.rar`
* `.tar`
* `.gz`

アーカイブ内の SongScript JSON は自動検出され、ファイル名やフォルダ名から BeatSaver ID を抽出して譜面フォルダ候補を探します。

#### DataGrid列

| 列 | 説明 |
|---|---|
| **ソース** | 読み込み元ファイル / アーカイブ内パスです |
| **ID** | BeatSaver ID。自動抽出後に手動編集、ロックが可能です |
| **BeatSaver** | BeatSaver ページの `Open` リンクです。未取得譜面は `[取得]` ボタンで `CustomLevels` へ展開できます |
| **songName** | 曲名。ソース名または BeatSaver metadata から設定できます |
| **cameraScriptAuthorName** | スクリプト作者名。フィルコピー、ロックが可能です |
| **songSubName** | 曲サブ名。ロックが可能です |
| **songAuthorName** | 曲アーティスト名。ロックが可能です |
| **levelAuthorName** | 譜面作者名。ロックが可能です |
| **BPM** | BPM。ロックが可能です |
| **AvatarHeight(cm)** | 想定アバター身長。ロックが可能です |
| **Description** | 説明文。ロックが可能です |
| **CL / WIP** | `CustomLevels` / `CustomWIPLevels` へのコピー有無を切り替えます |
| **!** | 同名ファイルが既に存在する場合に上書き警告を表示します |
| **CL# / WIP#** | 一致した譜面フォルダ候補数です。複数候補は赤字で表示されます |
| **CustomLevelsフォルダ / CustomWIPLevelsフォルダ** | コピー先フォルダを選択します |
| **リネーム** | 行単位のリネーム方式です。`カスタム` 使用時は `Settings` の `Copier リネーム名` を使います |
| **ファイル名** | コピー後のファイル名です。手動編集も可能です |
| **ｽｸﾘﾌﾟﾄ長** | `Movements` 内の `Duration + Delay` 合計です |
| **曲長(egg)** | 対応譜面の OGG 音声長です |

#### 右クリックメニュー

| メニュー項目 | 説明 |
|---|---|
| **選択行を削除** | 選択行を一覧から削除します |
| **選択したIDをコピー** | 選択行の BeatSaver ID をクリップボードへコピーします |
| **BeatSaverからmetadata取得** | 選択行の BeatSaver metadata を取得します |
| **選択した未取得譜面を取得** | 未取得譜面を BeatSaver から取得して `CustomLevels` へ展開します |
| **Rename** > **無し / SongScript / ID_Author_SongName** | 選択行のリネーム方式を変更します |
| **Song Name** > **Sourceから取得 (Default)** | ファイル名や metadata を優先して曲名を設定します |
| **Song Name** > **BeatSaverから取得 (songName)** | BeatSaver の `songName` を設定します |
| **Song Name** > **BeatSaverから取得 (songName - levelAuthorName)** | BeatSaver metadata を `songName - levelAuthorName` 形式で設定します |
| **CL チェック / WIP チェック** | コピー対象を一括 ON / OFF します |
| **対象セルの項目をロック / ロック解除** | セルごとの編集可否を切り替えます |
| **エクスプローラーで開く** > **CustomLevels Folder / CustomWIPLevels Folder** | 選択中のコピー先フォルダを開きます |

### Settingsタブ

アプリ全体の設定を行うタブです。設定は自動保存されます。

#### パス設定

| 設定項目 | 説明 |
|---|---|
| **CustomLevels** | Beat Saber の `CustomLevels` フォルダを指定します |
| **CustomWIPLevels** | Beat Saber の `CustomWIPLevels` フォルダを指定します |
| **SongScripts** | `SongScripts` 管理対象フォルダを指定します。`CameraSongScript` 用なら `UserData\CameraSongScript\SongScripts` を推奨します |
| **元データ検索(アーカイブ/JSON) 1〜3** | `MapScripts` タブの `元データ照合` で検索する元データ保存先を指定します |
| **バックアップルート** | バックアップ保存先のルートフォルダを指定します。空欄の場合は `CameraScriptManager.exe` と同じ場所の `UserData\backup` を使います |

#### 命名規則設定

| 設定項目 | 説明 |
|---|---|
| **MapScripts ZIP出力名** | `MapScripts` の出力フォルダ名 / ZIP 内指定名を設定します。`デフォルト` またはカスタム書式が使えます |
| **MapScripts ZIP内配置** | `1. JSON名そのまま + 指定名フォルダに格納` / `2. JSON名を指定名に変更 + フォルダなし` / `3. SongScript.json + 指定名フォルダに格納` から選択します |
| **Copier リネーム名** | `Copier` でカスタム書式を使う場合のファイル名テンプレートです |

使用可能なタグ:

| タグ | 説明 |
|---|---|
| `{MapId}` | BeatSaver のマップ ID |
| `{SongName}` | 曲名 |
| `{SongSubName}` | 曲サブ名 |
| `{SongAuthorName}` | 曲アーティスト名 |
| `{LevelAuthorName}` | 譜面作者名 |
| `{CameraScriptAuthorName}` | カメラスクリプト作者名 |
| `{FileName}` | JSON ファイル名 |
| `{Bpm}` | BPM |

#### その他の設定

| 設定項目 | 説明 |
|---|---|
| **MapScripts更新時にバックアップを作成する** | 既定では `UserData/backup/MapScripts` へ退避します |
| **SongScripts保存時にバックアップを作成する** | 既定では `UserData/backup/SongScripts` へ退避します |
| **Copier上書き時にバックアップを作成する** | 既定では `UserData/backup/Copier` へ退避します |
| **列の幅をリセット** | `MapScripts` / `SongScripts` / `Copier` の保存済み列幅を削除し、既定値に戻します |
| **現在のバージョン / リリースURL** | `CameraScriptManager` の現在バージョンとリリースページを表示します |
| **自動アップデートチェック** | ON の場合、起動時に `https://rynan4818.github.io/release_info.json` を 1 日 1 回以下で確認します |

## CameraSongScriptとの連携

`CameraScriptManager` はスクリプトを作るだけでなく、[CameraSongScript](https://github.com/rynan4818/CameraSongScript) の `SongScripts` 運用をしやすくするための管理ツールでもあります。

`CameraSongScript` は Beat Saber 上で `SongScripts` フォルダや譜面フォルダ内のスクリプトを読み込み、`Camera2` / `CameraPlus` に曲別スクリプト機能を追加する Mod です。ゲーム内で `SongScript` 機能を使う場合は、`CameraScriptManager` とは別にこの Mod の導入が必要です。

* リポジトリ: [rynan4818/CameraSongScript](https://github.com/rynan4818/CameraSongScript)
* リリース: [CameraSongScript Releases](https://github.com/rynan4818/CameraSongScript/releases)

`MapScripts` の既定 ZIP 出力構成 `1. JSON名そのまま + 指定名フォルダに格納` は、`CameraSongScript` の `SongScripts` フォルダへそのまま置いて整理しやすい構成です。

## BeatSaver IDによるフォルダマッチング

`Copier` タブや `SongScripts` タブでは、以下の情報を使って譜面フォルダを照合します。

* `metadata.mapId`
* `metadata.hash`
* ファイル名先頭の 1〜6 桁の 16 進文字列
* フォルダ名先頭の 1〜6 桁の 16 進文字列

`SongScripts` はまず ID ベースで候補を出し、その後に譜面 hash の追加照合を行います。`Copier` は ID ベースで `CustomLevels` / `CustomWIPLevels` 候補を探し、複数候補がある場合は行ごとにコピー先を選択できます。

## プレイリスト作成

`SongScripts` タブと `MapScripts` タブでは、選択した項目から Beat Saber のプレイリストファイル (`.bplist`) を生成できます。

作成ダイアログでは以下を指定できます。

* タイトル
* 作者
* 説明
* カバー画像

## 関連プロジェクト

* [CameraSongScript](https://github.com/rynan4818/CameraSongScript) - Beat Saber で `SongScripts` フォルダや譜面内 SongScript を再生する Mod
* [ChroMapper-CameraMovement](https://github.com/rynan4818/ChroMapper-CameraMovement) - ChroMapper でのカメラスクリプト作成プラグイン
* [ScriptMapper](https://github.com/rynan4818/ScriptMapper) - カメラスクリプト生成ツール
* [BS-CameraMovement](https://github.com/rynan4818/BS-CameraMovement) - Beat Saber 用カメラスクリプト関連プラグイン
