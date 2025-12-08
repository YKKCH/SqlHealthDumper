using System.Reflection;
using SqlHealthDumper.Options;

namespace SqlHealthDumper.Infrastructure;

/// <summary>
/// 論理キーを元に SQL テキストを取得するための契約。
/// </summary>
public interface ISqlLoader
{
    /// <summary>
    /// キーに対応する SQL を読み込み、署名を保証したテキストとして返す。
    /// </summary>
    string GetSql(string key);
}

/// <summary>
/// 埋め込みリソースの SQL を既定とし、外部ファイルがあればそちらを優先するローダー。
/// </summary>
public sealed class SqlLoader : ISqlLoader
{
    private readonly QuerySourceOptions _options;
    private readonly Assembly _assembly;

    /// <summary>
    /// クエリ取得に必要な名前空間や override パスを受け取る。
    /// </summary>
    public SqlLoader(QuerySourceOptions options)
    {
        _options = options;
        _assembly = Assembly.GetExecutingAssembly();
    }

    /// <inheritdoc />
    public string GetSql(string key)
    {
        // 外部パスが指定されている場合はカスタム SQL を優先する
        if (!string.IsNullOrWhiteSpace(_options.QueriesPathOverride))
        {
            var overridePath = BuildPath(_options.QueriesPathOverride!, key);
            if (File.Exists(overridePath))
            {
                var overrideText = File.ReadAllText(overridePath);
                return SqlSignature.EnsureSignature(overrideText);
            }
        }

        var resourceName = $"{_options.EmbeddedNamespace}.{key}.sql";
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded SQL not found: {resourceName}");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        return SqlSignature.EnsureSignature(text);
    }

    private static string BuildPath(string root, string key)
    {
        // key 形式: Scope.name -> Queries/{Scope}/{name}.sql
        var parts = key.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return Path.Combine(root, $"{key}.sql");
        }

        var scope = parts[0];
        var name = parts[1];
        return Path.Combine(root, scope, $"{name}.sql");
    }
}
