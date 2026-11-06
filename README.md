# DiskSpace

macOS 13以降のメニューバーアプリと、Windows 10/11（x64）の通知領域アプリです。内蔵・マウント中の外付けHDD/SSDの容量を表示します。

## Windows版

**Actions → Build DiskSpace for Windows → Run workflow** でビルドできます。成功した実行の **Artifacts → DiskSpace-Windows-x64** をダウンロードし、ZIPを「すべて展開」した後、**DiskSpace.exe** をダブルクリックしてください。.NETを含む自己完結型のため、利用時の開発ツールや追加ランタイムのインストールは不要です。

Windows版1.2.0は、起動するとネイビーとターコイズのフローティングパネルに残容量・空き割合のゲージを表示します。上部をドラッグして移動でき、「最前面 ON/OFF」で固定を切り替えます。右下や縁をドラッグしてサイズを変更でき、文字とゲージも拡大縮小します。下部の「不透明度」で40〜100%に調整できます（文字やボタンも含むウィンドウ全体の透過）。位置・サイズ・最前面・不透明度の設定は次回起動時に復元します。上部で対象ドライブを選び、「詳細・比較」から容量一覧を開けます。「—」で通知領域へ格納し、通知領域のアイコン（隠れている場合はタスクバーの「∧」）をクリックするとパネルを再表示します。アイコンにマウスを置くと対象ドライブと保存に使える空きを確認できます。Windowsの標準通知領域では、Macのように容量テキストを常時表示する構成にはしていません。一覧で対象切り替え・手動更新・比較情報コピー・終了ができます。×は通知領域へ格納します。

Windows版の「保存に使える空き」は `GetDiskFreeSpaceExW.lpFreeBytesAvailableToCaller`、「ディスク全体の空き」は `lpTotalNumberOfFreeBytes`、「総容量」は `lpTotalNumberOfBytes` です。利用者のクォータ等で値が異なることがあります。Macの「重要なファイル用（推定）」と同じ指標ではありません。エクスプローラーと比べるときは同じドライブのプロパティの**バイト値**で比較してください。表示単位・丸め方が異なる場合があります。

固定・リムーバブルの準備済みドライブを列挙し、30秒ごと・接続/取り外し時に更新します。選択対象を取り外すと起動ドライブへ戻ります。ドライブ文字のないフォルダマウント、ネットワーク、光学ドライブは今回の対象外です。

開発には.NET SDK 8とWindowsを使用します（Visual Studio 2022でもcsprojを開けます）。

```powershell
dotnet run --project Windows/DiskSpace.Tests -c Release
dotnet run --project Windows/DiskSpace
dotnet publish Windows/DiskSpace -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o build/windows-package
```

Actionsでは容量変換・ゲージ比率などの6件のテストに加え、配布exeで実際の起動ドライブ取得、ウィンドウ生成・表示、クリップボードの読み書きをスモークテストします。対話的な画面操作・実機の外付け接続/取り外しは別途確認が必要です。署名していないためSmartScreenで止められる場合があります。詳細は [Windowsの日本語説明](Windows/README-ja.txt) を参照してください。

Windows版1.2.0は上記の自動検証に成功しています。[検証記録](Windows/TESTING.md)で確認済み・未確認の範囲を確認できます。

## アプリを入手して使う

バージョン1.1.0から、メニューバーには選択したボリュームの「重要なファイル用（推定）」を表示します。取得失敗時は「取得できません」と表示します。一覧の「現在の空き」と比較情報は引き続き確認できます。Finderとの一致は保証しません。

最新ビルドのArtifactsにある `DiskSpace-DMG-arm64`（Apple Silicon）または `DiskSpace-DMG-x86_64`（Intel）からDMGを取得できます。外側のZIPを展開し、中のDMGを開いてアプリをApplicationsへドラッグしてください。更新時は古いDiskSpaceをメニューの「終了」で終了してからアプリを置き換えます。

ZIP内のZIPを展開できない場合は **Actions → Package DiskSpace DMG → Run workflow** で、成功したビルドの実行IDとMacのアーキテクチャを指定します。Artifactsの `DiskSpace-DMG-arm64`（Intelは `DiskSpace-DMG-x86_64`）を取得し、外側のZIPを展開します。中の **DiskSpace-macOS.dmg** を開き、**DiskSpace.app** を **Applications** へドラッグしてください。DMGはmacOSランナーで作成後に実際にマウントし、中のアプリの署名を検証しています。DMGでもAppleの公証は行っていません。

このソースをGitHubに反映後、リポジトリの **Actions → Build DiskSpace for macOS → Run workflow** を選びます（push時にも自動実行）。完了した実行の **Artifacts** から、Apple Siliconなら `DiskSpace-macOS-arm64`、Intel Macなら `DiskSpace-macOS-x86_64` をダウンロードしてください。

ダウンロードしたアーティファクトZIPを展開し、中の `DiskSpace-macOS.zip` も展開します。**DiskSpace.app** をApplicationsなどへ移し、ダブルクリックするとメニューバーに「残り（推定）… GB」が表示されます。ターミナルやStart.commandを起動する方式ではありません。クリックすると対象名、容量一覧、表示対象の切り替え、比較情報のコピー、手動更新、終了が利用できます。

ビルドはアーキテクチャごとで、Developer ID署名・Appleの公証はしていません（アドホック署名のみ）。ダウンロードしたアプリがGatekeeperに止められた場合は、出所を確認したうえでmacOSの「システム設定 → プライバシーとセキュリティ → このまま開く」を使用してください。組織のポリシーによっては起動できません。一般配布には別途Developer ID署名と公証が必要です。

## Xcodeで起動・開発

1. MacにXcode 15以降をインストールし、初回起動を完了します。
2. このリポジトリの **Package.swift** をXcodeで開きます。
3. `DiskSpace` スキームと実行先 `My Mac` を選択し、Run（⌘R）します。Dockのウィンドウではなくメニューバーを確認してください。
4. Test（⌘U）で容量変換のテストを実行します。

コマンドラインから検証・アプリ化する場合：

```sh
cd <このリポジトリ>
swift test
bash scripts/build-app.sh
```

出力は `build/DiskSpace.app` と `build/DiskSpace-macOS.zip` です。通常利用は作成済みの **DiskSpace.appをダブルクリック**してください。ビルドは実行したMacのアーキテクチャ向けです。

## どの容量を測っているか

| 表示 | 取得方法・意味 |
| --- | --- |
| 総容量 | `statfs.f_blocks × f_bsize` |
| 現在の空き | `statfs.f_bavail × f_bsize`。dfのAvailableと比較する値 |
| 重要なファイル用（推定） | Foundationの `volumeAvailableCapacityForImportantUsage`。OSによる推定値。メニューバーはこの値 |

内部はバイト単位、表示は十進（1 GB = 1,000,000,000バイト）で小数第2位です。0は `0.00 GB`、取得失敗は `取得できません` と表示します。固定値の加算やFinderに合わせる補正はしません。推定値はFinderの「空き領域」や「パージ可能」そのものではなく、その差をパージ可能容量とも断定しません。

起動ディスクは `/` の利用者向け名称で1回だけ表示し、ユーザーデータを書き込む `/System/Volumes/Data` を測定します（パスがなければ `/`）。補助マウント `/System/*`、`/private/*`、Preboot・Recovery・VM等は除きます。その他はローカルで表示可能なAPFS/HFS+/exFAT/FAT/NTFSのマウントを列挙し、同じデバイスの重複は除きます。ネットワークドライブ等は対象外です。APFSコンテナ内では容量を共有するため、各行の総容量・空きを足し合わせないでください。

30秒ごと、およびマウント・取り外し・名称変更・スリープ復帰・画面を開いた際に更新します。取得はバックグラウンドで行います。選択したドライブがなくなった場合は起動ディスクへ戻ります。

## Finderとの比較手順

1. アプリで対象ボリュームを確認し「手動更新」→「比較情報をコピー」を押し、テキストエディタへ貼り付けます。測定パス、UTCの測定時刻、各容量の未丸めバイト値を保存できます。
2. Finderの「移動 → コンピュータ」で**同じボリューム**を選び「情報を見る」（⌘I）を開きます。起動ディスクの場合、比較情報の測定パスはDataボリュームになります。
3. Finderの空き領域、括弧内のパージ可能容量、確認時刻を別途記録します。大きなファイル操作を止め、なるべく同時に比較してください。
4. 必要ならターミナルで `df -k /System/Volumes/Data`（外付けは比較情報のパスを引用符で囲む）を実行します。Availableに1,024を掛けてアプリの「現在の空き」のバイト値と比較します。測定間の書き込みで差が出る場合があります。

観測例：`16,242,320 × 1,024 = 16,632,135,680` バイト → **16.63 GB**。このときFinderのパージ可能が155.7 MBなら加算結果は約16.79 GBですが、これは一例であり、毎回成り立つ式ではありません。FinderとAPIの更新タイミング・推定・丸め等が異なるため、一致を保証しません。

## 検証状況

初期実装環境はLinuxです。GitHub ActionsのmacOSランナーで `swift test`、releaseビルド、バンドル構造、アドホック署名検証、ZIP生成を実行しています。実行結果は[ビルド記録](https://github.com/atstti/codexssd/actions/runs/42068780034)で確認できます。GUI起動・実ドライブの接続取り外し・Finder比較は**未確認**です。ActionsはGUI操作を検証しません。

テストには上記の観測値、0と取得失敗の区別、十進単位、整数オーバーフローを含めています。Macでアプリを開いた後、メニューバー表示・対象切り替え・コピー内容・30秒更新・外付け取り外し時の起動ディスク復帰を確認してください。
