# SqlHealthDumper

## はじめに
SqlHealthDumper は SQL Server の健全性を一括取得し、LLM（ChatGPT / Claude / Gemini など）がそのまま読める構造化データとして出力する .NET 8 製ツールです。目的は単なるレポート化ではなく、LLM がインスタンス状態を正確に理解し、具体的な改善策を提示できるだけのデータ基盤を整えることにあります。DMV・Query Store・インデックス・I/O・待機統計など膨大な断片情報を正規化し、ファイル名や表構造を固定化することで、毎回同じプロンプトで解析できるように設計されています。

### 開発背景
- 実運用ではヘルスチェックが属人化し、担当者が手作業で DMV を収集していました。
- オンプレミスと Azure SQL Managed Instance（およびコンテナ環境）が混在しており、両者を同じ形式で蓄積したいという要望がありました。
- LLM によるトラブルシューティングを成立させるには、DB 状態を一貫した形式で収集し続ける仕組みが不可欠でした。

### 開発ステータスと注意事項
- 本プロジェクトは **Early Access / Preview** 段階であり、すべての SQL Server バージョン・構成・Azure MI での検証が完了しているわけではありません。
- SQL Server 2016–2019 では DMV の列差異により出力レイアウトが変わる可能性があります。
- Azure SQL Managed Instance の読み取り専用レプリカでは一部 DMV にアクセスできず、挙動が異なる場合があります。
- Query Store が無効の場合は DMV ベースのトップクエリ収集に自動フォールバックしますが、得られる情報が減少します。
- OSS として環境差異に関する Issue / PR を歓迎しています。特定バージョン向け SQL の改良や追加テストケースの共有も大歓迎です。

## コアコンセプト
1. **LLM が解析しやすい構造化データ**  
   ファイル番号（`00_instance_overview.md` など）や列順序を固定し、ヒストグラムやクエリテキストは JSON として併設します。長い SQL は自動的に分離し、Markdown からリンクさせることでプロンプトを汚しません。
2. **クエリ差し替え可能な拡張性**  
   `src/SqlHealthDumper/Queries` の構成に合わせて `sql/<Scope>/<name>.sql` を置けば、再ビルドなしで SQL を上書きできます（`--queries-path` 指定）。
3. **運用現場で役立つ観点を網羅**  
   インスタンス資源、DB サイズ/ファイル、インデックス利用状況、欠落インデックス、トップクエリ、統計、バックアップ/DBCC、テーブル詳細まで収集。Query Store が有効なら自動的に優先します。

## 収集・出力内容
- **00_instance_overview.md**  
  エディション、バージョン、環境種別（オンプレ/Azure MI/コンテナ）、CPU/メモリサマリ、待機統計、ホスト情報、Query Store サポート状況など。
- **10_db_overview.md**  
  互換性レベル、リカバリモデル、ログ再利用待ち、暗号化、HADR、有無、データ/ログサイズ、ファイルごとの増分設定やパス、注意点。
- **12_index_usage_and_missing.md**  
  Seeks/Scans/Lookups/Updates、Missing Index 推定。既定では sys.* オブジェクトを除外（`IncludeSystemObjects` トグルはエンジンに実装済み、CLI 対応はロードマップ）。
- **13_top_queries.md**  
  Query Store（対応環境）または DMV から CPU/Duration/Reads/Writes/実行回数/最終実行時刻/SQL テキストを取得。長い SQL は `Queries/xxxx.sql` に自動抽出してリンク。
- **14_stats_and_params.md**  
  `dm_db_stats_properties` ベースの統計情報、密度ベクタ、ヒストグラム要約、取得できない場合のノート（Rows 推定/ヒスト未取得フラグ付き）。
- **20_backup_and_maintenance.md**  
  バックアップ履歴、DBCC CHECKDB ジョブ推定、Agent Job 状況。Azure SQL Database など非対応環境ではスキップし、理由を Markdown とノートへ記載。
- **Tables/<Schema.Table>.md / .hist.json**  
  列定義、インデックス、制約、トリガ、テーブル統計、ヒストグラム JSON。LLM に渡す前に任意テーブルだけ抜粋する用途にも適しています。

生成物例:
```
result/
  localhost,46551_20251208_1329/
    00_instance_overview.md
    snapshot_state.json
    failures.json
    log.txt
    HealthDemo/
      10_db_overview.md
      12_index_usage_and_missing.md
      13_top_queries.md
      14_stats_and_params.md
      20_backup_and_maintenance.md
      Tables/
        sales.Orders.md
        sales.Orders.hist.json
      Queries/
        8F1C8D87A93402B2.sql
```

## セットアップ
### 前提
- 収集対象 SQL Server で `VIEW SERVER STATE`, `VIEW ANY DEFINITION`, `VIEW DATABASE STATE` などの権限（バックアップ履歴取得時は `msdb` 参照権限）。

### 1. バイナリから使う（おすすめ）

1. GitHub Releases から最新のバイナリをダウンロード
   - Windows: `SqlHealthDumper-win-x64.zip`

2. 任意のフォルダに解凍

3. コマンドプロンプト or PowerShell（Windows）またはターミナル（Linux/macOS）で実行:

   **Windows 認証の例:**
   ```powershell
   # Windows
   .\SqlHealthDumper.exe run --server localhost --output .\result
   ```

   **SQL 認証の例:**
   ```powershell
   # Windows
   .\SqlHealthDumper.exe run --server prodsql --auth sql --user sa --password "***" --output C:\snapshots
   ```

### 2. ソースからビルドする

**前提:** .NET 8 SDK が必要です。

```bash
git clone https://github.com/YKKCH/SqlHealthDumper.git
cd SqlHealthDumper
dotnet publish src/SqlHealthDumper -c Release

# Windows 認証の例
dotnet run --project src/SqlHealthDumper -- run --server localhost --output .\result

# SQL 認証の例
dotnet run --project src/SqlHealthDumper -- run --server prodsql --auth sql --user sa --password "***" --output C:\snapshots
```

エラー時には CLI がバリデーションの詳細とサンプルコマンドを表示します。`--output` 配下にはタイムスタンプ付きのサブフォルダが作られ、その直下に Markdown / JSON / log が配置されます。

## コマンドラインオプション
| オプション | 説明 | 既定値 |
| --- | --- | --- |
| `--server` | 接続先インスタンス名（`--connection-string` と排他）。 | *(必須)* |
| `--connection-string` | 完全な接続文字列。 | *(なし)* |
| `--auth` | `windows` または `sql`。 | `windows` |
| `--user`, `--password` | SQL 認証利用時に必須。 | *(なし)* |
| `--output` | 出力ルートディレクトリ。 | `<アプリ>/output` |
| `--include-system-dbs` | master / msdb / tempdb を含める。 | `false` |
| `--db` / `--exclude-db` | 収集対象 / 除外 DB（カンマ区切り）。 | *(全ユーザーDB)* |
| `--query-timeout` | 各 SQL のタイムアウト秒。 | モード依存 (90/60/45) |
| `--max-parallelism` | DB 単位の最大並列数。 | モード依存 (1/3/6) |
| `--table-parallelism` | テーブル収集の並列数。 | モード依存 (1/2/4) |
| `--queries-path` | 外部 SQL ディレクトリ（`Scope/Name.sql` 構成）。 | *(埋込リソース)* |
| `--log-level` | `info` / `debug` / `trace`。 | `info` |
| `--log-file` | ログファイルパス。 | `<output>/log.txt` |
| `--lock-timeout` | `SET LOCK_TIMEOUT` ミリ秒。 | ドライバー既定 |
| `--no-top-queries` | トップクエリ収集を無効化。 | `false` |
| `--no-missing-index` | インデックス利用/欠落収集を無効化。 | `false` |
| `--no-table-md` | テーブル Markdown / ヒストを出力しない。 | `false` |
| `--no-backup-info` | バックアップ/メンテナンス収集を無効化。 | `false` |
| `--no-stats` | 統計収集を無効化。 | `false` |
| `--mode` | 実行プロファイル（負荷レベル）を指定するモード `low` / `balanced` / `fast`。 | `low` |

> 補足: `IncludeSystemObjects` と `--no-resume` は内部の設定項目として存在しますが、現在 CLI オプションには未露出です（ロードマップ参照）。

### Web ダッシュボード (`serve` コマンド)

収集済みスナップショットをブラウザで閲覧したい場合は `serve` サブコマンドを使用します。指定ディレクトリ配下のスナップショットを自動検出し、ファイル一覧と Markdown/JSON のプレビューを提供します。

```bash
# 例: result 配下を 5080 番ポートで公開しブラウザを自動起動
SqlHealthDumper serve --path ./result --port 5080 --open-browser
```

| オプション | 説明 | 既定値 |
| --- | --- | --- |
| `--path` | スナップショットを格納したディレクトリ、もしくは個別スナップショットフォルダ。 | `./result` |
| `--port` | 待ち受けポート番号。 | `5080` |
| `--host` | バインドするホスト。`0.0.0.0` を指定すると LAN からもアクセスできます（自己責任）。 | `localhost` |
| `--open-browser` | 起動後に既定ブラウザを開く。 | `false` |

> serve コマンドはローカル環境向けに設計されています。プレビュー対象はテキストファイルのみで、2 MB を超える場合は冒頭部分だけを表示します。

## 運用メモ
- **ログ & リジューム**  
  コンソールと `<output>/log.txt` に同時出力。実行中は `snapshot_state.json` と `failures.json` を逐次更新し、再実行時は完了済みセクションを自動スキップします。
- **Azure / 機能差分**  
  Azure SQL Database で未対応の DMV（Missing Index など）は失敗させずスキップ扱いにして理由をノートへ出力します。Managed Instance ではフル機能を想定しています。
- **長時間処理の回避**  
  コレクタ SQL には `SET DEADLOCK_PRIORITY LOW` を挿入済み。`--lock-timeout` や `--query-timeout` を調整することで本番負荷への影響を低減できます。
- **モード切り替え**  
  `low` は単一並列で安全重視、`balanced` は 3 並列、`fast` は 6 並列 + 短いタイムアウト。個別設定を上書きすれば任意のバランスを組めます。

## 拡張とカスタマイズ
1. **SQL の差し替え**  
   `sql/Instance/instance_waits.sql` のようにフォルダを用意し、`--queries-path sql` を付ければ埋め込み SQL を置き換えられます。
2. **Collector の追加**  
   `Collectors/` を参考に新しい Collector を実装し、`SqlLoader` で読み込む SQL を追加するだけでログ出力やリトライを共通利用できます。
3. **出力セクションの制御**  
   `--no-*` オプションで不要なセクションを抑止し、LLM 用の軽量スナップショットや監視用の最小構成を作れます。
4. **JSON の二次利用**  
   テーブルヒストグラムや `snapshot_state.json` を Elastic / Grafana / BI ツールへ流し込み、LLM 解析前に可視化することも想定しています。

## 典型ユースケース
- スナップショット一式を LLM に投入し、待機統計やインデックス利用、統計情報を読み解いて改善プランを提示させる。
- インデックス劣化や Wait の偏りを長期トレンドで追跡し、パフォーマンス劣化を事前に検知。
- バッチ処理や API 負荷急増前後でスナップショットを比較し、性能低下の要因を切り分ける。
- Azure MI の読み取りレプリカで負荷分析し、自動チューニングや顧客向けレポートの補助に利用する。

## ロードマップ
- 継続監視・スケジュール実行機能
- HTML レポート / Web ダッシュボード拡張（serve UI の比較・共有機能）
- Grafana・Elasticsearch・OpenTelemetry 連携
- クエリプラグイン化（パック配布）
- マルチインスタンス同時監視
- CLI からの `--include-system-objects` / `--no-resume` サポートとレジューム制御強化
- 対応バージョン拡大（旧バージョン SQL Server / Azure MI 固有差異の吸収）
- 追加テストシナリオ（互換レベル・可用性構成・Query Store 状態を跨いだ自動テスト）

## 既知の制限
- Azure SQL Database では Missing Index / Backup 履歴の一部が取得できずスキップ扱いになります。
- LLM 向けの Markdown / JSON 出力は提供済みですが、静的 HTML 生成は未実装であり serve コマンドはローカルプレビュー向けの最小構成です。
- `IncludeSystemObjects` や `NoResume` は AppConfig に存在するものの、現状 CLI で切り替えできません。
- serve モードのプレビューはテキストのみ対象で 2 MB を超えるファイルは冒頭部分のみ表示されます。

## ライセンスと利用上のお願い
- 本ツールは MIT License の下で公開されており、ソフトウェアは無保証（AS IS）で提供されます。
- プロダクション環境に適用する際は、対象バージョンやワークロードで十分に検証したうえで導入してください。
