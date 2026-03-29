# CameraScript Manager & Copier

Beat Saberのカメラスクリプト（SongScript.json）を一元管理・配布するためのWindows用デスクトップツールです。

[ChroMapper-CameraMovement](https://github.com/rynan4818/ChroMapper-CameraMovement) や [ScriptMapper](https://github.com/rynan4818/ScriptMapper) で作成したカメラスクリプトファイルのメタデータ管理、アーカイブ作成、譜面フォルダへのコピー配置を効率的に行えます。

## 特徴

* **MapScripts機能** — CustomLevels / CustomWIPLevelsフォルダ内の既存カメラスクリプトを一覧表示し、メタデータの編集・追加、ZIP配布用アーカイブの作成、BeatSaberプレイリスト(.bplist)の生成が可能
* **Copier機能** — JSONファイルやアーカイブ（ZIP/7z/RAR/tar/gz）をドラッグ＆ドロップで読み込み、BeatSaver IDによる自動フォルダマッチングで譜面フォルダへSongScript.jsonをコピー配置
* **BeatSaver API連携** — IDから曲名・作者名・BPM等のメタデータを自動取得。[SongDetailsCache](https://github.com/kinsi55/BeatSaber_SongDetails)によるローカルキャッシュで高速化
* **メタデータ編集** — カメラスクリプト作者名、曲名、BPM、アバター身長、説明文などのメタデータをJSON内に埋め込み。セル単位のロック機能で誤編集を防止
* **リネーム機能** — コピー時にファイル名を自動リネーム（SongScript / ID_Author_SongName / カスタム書式に対応）
* **上書きバックアップ** — `CameraScriptManager.exe` 配下の `backup` フォルダに、`MapScripts` / `SongScripts` / `Copier` ごとの形式で自動退避
* **元データ照合** — アーカイブやJSONファイルとCustomLevels/WIPのスクリプトをハッシュ照合し、元データの対応関係を特定
* **曲長・スクリプト長の表示** — OGG音声ファイルの長さとカメラスクリプトのDuration合計を表示し、差分を確認可能

## 動作環境

* Windows 10 / 11
* [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) 以降

## インストール

1. [リリースページ](../../releases)から最新の`CameraScriptManager`をダウンロードします
2. ダウンロードしたZIPファイルを任意のフォルダに解凍します
3. `CameraScriptManager.exe`を実行します

### 初回設定

起動後、まず**Settingsタブ**でBeat Saberの譜面フォルダパスを設定してください。

## 使い方

アプリケーションは4つのタブで構成されています。

### MapScriptsタブ

CustomLevels / CustomWIPLevels フォルダ内にある既存のカメラスクリプト（SongScript.json）を一覧管理するタブです。

#### ツールバーボタン

| ボタン | 説明 |
|---|---|
| **再チェック** | CustomLevels / CustomWIPLevelsフォルダを再スキャンし、カメラスクリプトの一覧を更新します |
| **メタ情報追加** | 「選択」チェックが入った項目のSongScript.jsonに、編集したメタデータを書き込みます。変更前のファイルは `backup/MapScripts` にZIPでバックアップされます |
| **フォルダ出力** | 選択した項目をフォルダとしてエクスポートします。保存先に選んだフォルダの下へ `yyyyMMdd_HHmmss_OUTPUT` フォルダを作成し、Settings の ZIP出力名・ZIP内配置設定と同じ構造で出力します |
| **ZIP出力** | 選択した項目をZIPアーカイブとしてエクスポートします。Settings の ZIP出力名・ZIP内配置設定に従って配布用パッケージを作成します |
| **プレイリストを作成** | 選択した項目からBeatSaberプレイリスト(.bplist)を生成します |
| **元データ照合** | Settingsで設定した元データ検索フォルダ内のアーカイブ/JSONと、一覧のスクリプトをハッシュ値で照合します |

#### DataGrid列

| 列 | 説明 |
|---|---|
| **選択** | 処理対象とする項目をチェックボックスで指定します |
| **編集済** | メタデータが変更されている場合にチェックが付きます。変更行は黄色で強調表示されます |
| **ID** | BeatSaverのマップID（16進数）。編集可能、ロック可能 |
| **cameraScriptAuthorName** | カメラスクリプト作者名。ドラッグハンドル（右下の■）で下方向にフィルコピーが可能。ロック可能 |
| **songName** | 曲名。ロック可能 |
| **songSubName** | 曲サブ名。ロック可能 |
| **songAuthorName** | 曲アーティスト名。ロック可能 |
| **levelAuthorName** | 譜面作者名。ロック可能 |
| **BPM** | テンポ。ロック可能 |
| **AvatarHeight(cm)** | アバター身長（cm）。ロック可能 |
| **Description** | 説明文。ロック可能 |
| **曲長(egg)** | 譜面フォルダ内のOGG音声ファイルの長さ（読み取り専用） |
| **ｽｸﾘﾌﾟﾄ長** | カメラスクリプトのMovementsのDuration+Delay合計（読み取り専用） |
| **差分(beat)** | 曲長とスクリプト長の差分をbeat単位で表示（読み取り専用） |
| **ファイル名** | SongScript.jsonのファイル名（読み取り専用） |
| **フォルダ** | 曲フォルダのパス（読み取り専用） |
| **ソース** | CustomLevelsまたはCustomWIPLevelsのどちらのフォルダか（読み取り専用） |
| **元データ(アーカイブ/JSON)** | 元データ照合の結果、マッチした元ファイルのパス（読み取り専用） |
| **ハッシュ** | スクリプトファイルのハッシュ値（読み取り専用） |

#### 右クリックメニュー（コンテキストメニュー）

| メニュー項目 | 説明 |
|---|---|
| **切り取り / コピー / 貼り付け** | セルの値を操作します |
| **BeatSaverからメタデータ取得** | 選択行のIDを使ってBeatSaver APIからsongName、BPM等のメタデータを取得し、フィールドに反映します |
| **選択したIDをコピー** | 選択行のBeatSaver IDをクリップボードにコピーします |
| **対象セルの項目をロック / ロック解除** | 選択したセルの列をロック（読み取り専用）または解除します。ロックされたセルはグレー背景で表示されます |
| **選択 ON/OFFを切り替え** | 選択チェックボックスの状態をトグル切り替えします |
| **エクスプローラーで開く** > **譜面フォルダ** | 対象の譜面フォルダをエクスプローラーで開きます |
| **エクスプローラーで開く** > **元データ(アーカイブ/JSON) フォルダ** | 元データ照合で見つかった元ファイルのフォルダをエクスプローラーで開きます |

---

### Copierタブ

カメラスクリプトファイルを譜面フォルダへコピー配置するタブです。JSONファイルやアーカイブをドラッグ＆ドロップまたはファイル選択で読み込み、対応する譜面フォルダに自動マッチングしてコピーします。

#### ツールバー

| 項目 | 説明 |
|---|---|
| **譜面metadataを表示** | DataGridにメタデータ列（songSubName, songAuthorName, levelAuthorName, BPM, AvatarHeight, Description）の表示/非表示を切り替えます |
| **metadataをSongScriptに追加** | コピー実行時に、入力されたメタデータをSongScript.json内に埋め込みます |
| **リネーム** | コピー時のファイルリネーム方式をドロップダウンで選択します。`無し`（元ファイル名のまま）/ `SongScript`（SongScript.jsonにリネーム）/ `IdAuthorSongName`（ID_Author_SongName_SongScript.json形式）/ `カスタム`（Settings で設定した書式）から選択可能 |
| **再チェック** | CustomLevels / CustomWIPLevelsフォルダを再スキャンし、フォルダマッチングを更新します |
| **クリア** | 読み込んだエントリをすべてクリアします |
| **コピー実行** | チェックが入ったエントリを対象の譜面フォルダにコピーします |
| **Cache** | SongDetailsCacheの状態インジケータ。緑=キャッシュヒット、赤=ミス、灰=未実行。ヒット数/ミス数も表示 |

#### ドロップゾーン

`.json`ファイルまたはアーカイブファイル（`.zip` / `.7z` / `.rar` / `.tar` / `.gz`）をドラッグ＆ドロップして読み込みます。「**ファイルを選択...**」ボタンからファイルダイアログで選択することも可能です。

アーカイブ内のSongScript JSONファイルは自動的に検出・展開されます。ファイル名やフォルダ名から BeatSaver ID（16進数）を自動抽出します。

#### DataGrid列

| 列 | 説明 |
|---|---|
| **ソース** | 読み込み元のファイルパス（読み取り専用） |
| **ID** | BeatSaverのマップID（16進数）。自動抽出されますが、手動で編集も可能。ロック可能 |
| **BeatSaver** | BeatSaverのマップページへの「Open」リンク |
| **songName** | 曲名。BeatSaver APIまたはSongDetailsCacheから自動取得。ロック可能 |
| **cameraScriptAuthorName** | カメラスクリプト作者名。ドラッグハンドルでフィルコピー可能。ロック可能 |
| **songSubName** | 曲サブ名。ロック可能（metadataを表示ON時に表示） |
| **songAuthorName** | 曲アーティスト名。ロック可能（metadataを表示ON時に表示） |
| **levelAuthorName** | 譜面作者名。ロック可能（metadataを表示ON時に表示） |
| **BPM** | テンポ。ロック可能（metadataを表示ON時に表示） |
| **AvatarHeight(cm)** | アバター身長（cm）。ロック可能（metadataを表示ON時に表示） |
| **Description** | 説明文。ロック可能（metadataを表示ON時に表示） |
| **CL** | CustomLevelsフォルダへのコピーを有効にするチェックボックス。対応フォルダが見つかった場合に自動でONになります |
| **!** | CustomLevelsフォルダに同名ファイルが既に存在する場合に赤い「!」アイコンで警告を表示します |
| **CL#** | 同一IDに対応するCustomLevelsフォルダの数。複数ある場合は赤字で表示されます |
| **CustomLevelsフォルダ** | コピー先のCustomLevelsフォルダをドロップダウンで選択します。複数候補がある場合に選択可能です |
| **WIP** | CustomWIPLevelsフォルダへのコピーを有効にするチェックボックス |
| **!** | CustomWIPLevelsフォルダに同名ファイルが既に存在する場合に警告を表示します |
| **WIP#** | 同一IDに対応するCustomWIPLevelsフォルダの数 |
| **CustomWIPLevelsフォルダ** | コピー先のCustomWIPLevelsフォルダをドロップダウンで選択します |
| **リネーム** | 行ごとにリネーム方式を個別変更できます |
| **ファイル名** | コピー後のファイル名（リネーム後の名前）。手動で編集も可能 |
| **ｽｸﾘﾌﾟﾄ長** | カメラスクリプトの長さ（読み取り専用） |
| **曲長(egg)** | 対応するOGG音声ファイルの長さ（読み取り専用） |

#### 右クリックメニュー（コンテキストメニュー）

| メニュー項目 | 説明 |
|---|---|
| **選択行を削除** | 選択した行を一覧から削除します |
| **選択したIDをコピー** | 選択行のBeatSaver IDをクリップボードにコピーします |
| **Rename** > **無し / SongScript / ID_Author_SongName** | 選択行のリネーム方式を変更します |
| **Song Name** > **Sourceから取得 (Default)** | 曲名をソース（ファイル名/metadata）から取得します |
| **Song Name** > **BeatSaverから取得 (songName)** | 曲名をBeatSaver APIから取得して設定します |
| **Song Name** > **BeatSaverから取得 (songName - levelAuthorName)** | 曲名を「songName - levelAuthorName」の形式でBeatSaverから取得して設定します |
| **CL チェック** > **ON / OFF** | 選択行のCustomLevelsコピーチェックを一括ON/OFFします |
| **WIP チェック** > **ON / OFF** | 選択行のCustomWIPLevelsコピーチェックを一括ON/OFFします |
| **対象セルの項目をロック / ロック解除** | 選択したセルの列をロック/解除します |
| **エクスプローラーで開く** > **CustomLevels Folder** | 対応するCustomLevelsフォルダをエクスプローラーで開きます |
| **エクスプローラーで開く** > **CustomWIPLevels Folder** | 対応するCustomWIPLevelsフォルダをエクスプローラーで開きます |

---

### Settingsタブ

アプリケーション全体の設定を行うタブです。設定は自動保存されます。

#### パス設定

| 設定項目 | 説明 |
|---|---|
| **CustomLevels** | Beat Saberの`CustomLevels`フォルダのパスを指定します。「参照...」ボタンでフォルダを選択可能です |
| **CustomWIPLevels** | Beat Saberの`CustomWIPLevels`フォルダのパスを指定します |
| **元データ検索(アーカイブ/JSON) 1〜3** | MapScriptsタブの「元データ照合」で使用する検索対象フォルダを最大3つ指定できます。カメラスクリプトの元アーカイブやJSONファイルが保存されているフォルダを設定します |
| **バックアップルート** | バックアップ保存先のルートフォルダを指定します。空欄の場合は `CameraScriptManager.exe` 配下の `backup` フォルダを使用します |

#### 命名規則設定

| 設定項目 | 説明 |
|---|---|
| **MapScripts ZIP出力名** | MapScriptsタブのZIPエクスポート時に使う「指定名」の書式を指定します。`デフォルト`（{MapId}\_{SongName}\_{LevelAuthorName}）またはカスタム書式から選択します |
| **MapScripts ZIP内配置** | MapScriptsタブのZIPエクスポート時のJSON配置方法を指定します。`1. JSON名そのまま + 指定名フォルダに格納`（既定） / `2. JSON名を指定名に変更 + フォルダなし` / `3. SongScript.json + 指定名フォルダに格納` から選択します |
| **Copier リネーム名** | Copierタブでリネーム方式「カスタム」を選択した場合のファイル名の書式を指定します。デフォルトは`{MapId}_{CameraScriptAuthorName}_{SongName}_SongScript`です |

既定の `MapScripts ZIP内配置` は `1. JSON名そのまま + 指定名フォルダに格納` です。この構造で出力した ZIP は、`CameraSongScript` の `SongScripts` フォルダにそのまま配置して読み込める前提で保持しています。

**使用可能なタグ**（クリックでクリップボードにコピーされます）:

| タグ | 説明 |
|---|---|
| `{MapId}` | BeatSaverのマップID |
| `{SongName}` | 曲名 |
| `{SongSubName}` | 曲サブ名 |
| `{SongAuthorName}` | 曲アーティスト名 |
| `{LevelAuthorName}` | 譜面作者名 |
| `{CameraScriptAuthorName}` | カメラスクリプト作者名（入力値） |
| `{FileName}` | JSONファイル名 |
| `{Bpm}` | BPM |

#### その他の設定

| 設定項目 | 説明 |
|---|---|
| **MapScripts更新時にバックアップを作成する** | ONの場合、変更前のスクリプトを `backup/MapScripts/backup_yyyyMMdd_HHmmss.zip` にまとめて保存します |
| **SongScripts保存時にバックアップを作成する** | ONの場合、変更前のSongScriptsを `backup/SongScripts` 配下に元のフォルダ構成を再現して、日時付きの元拡張子ファイルで保存します |
| **Copier上書き時にバックアップを作成する** | ONの場合、上書き対象を `backup/Copier/CustomLevels` または `backup/Copier/CustomWIPLevels` 配下に譜面フォルダ構成つきで、日時付きの元拡張子ファイルで保存します |

## BeatSaver IDによるフォルダマッチングの仕組み

Copierタブでカメラスクリプトファイルを読み込むと、ファイル名やアーカイブ内のフォルダ名からBeatSaverのマップID（16進数）を自動抽出します。このIDを使って、CustomLevels / CustomWIPLevelsフォルダ内の譜面フォルダを照合し、コピー先フォルダの候補を自動的に特定します。

譜面フォルダ名は通常 `{MapId} ({SongName} - {LevelAuthorName})` の形式になっており、先頭の16進数部分でマッチングを行います。

## プレイリスト作成

MapScriptsタブで「選択」チェックが入った項目から、BeatSaberのプレイリストファイル（.bplist）を生成できます。作成ダイアログでは以下の情報を入力します：

* **タイトル** — プレイリスト名
* **作者** — プレイリスト作者名
* **説明** — プレイリストの説明文
* **カバー画像** — プレイリストのカバー画像（オプション）

## 関連プロジェクト

* [ChroMapper-CameraMovement](https://github.com/rynan4818/ChroMapper-CameraMovement) — ChroMapperでのカメラスクリプト作成プラグイン
* [ScriptMapper](https://github.com/rynan4818/ScriptMapper) — カメラスクリプト生成ツール
* [BS-CameraMovement](https://github.com/rynan4818/BS-CameraMovement) — Beat Saberでのカメラスクリプト再生プラグイン
