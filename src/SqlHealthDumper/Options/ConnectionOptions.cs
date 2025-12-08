namespace SqlHealthDumper.Options;

/// <summary>
/// SQL Server への接続に使用する認証方式。
/// </summary>
public enum AuthenticationMode
{
    /// <summary>
    /// Windows 統合認証。
    /// </summary>
    Windows,

    /// <summary>
    /// SQL 認証 (ユーザー/パスワード)。
    /// </summary>
    Sql
}

/// <summary>
/// 接続先と認証に関する設定群。
/// </summary>
public sealed class ConnectionOptions
{
    /// <summary>
    /// サーバー/インスタンス名。
    /// </summary>
    public string? Server { get; set; }

    /// <summary>
    /// 完全な接続文字列。Server 指定より優先。
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// 認証方式。
    /// </summary>
    public AuthenticationMode Authentication { get; set; } = AuthenticationMode.Windows;

    /// <summary>
    /// SQL 認証で利用するユーザー名。
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// SQL 認証パスワード。
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 暗号化接続を要求するか。
    /// </summary>
    public bool Encrypt { get; set; } = true;

    /// <summary>
    /// サーバー証明書を検証せず信頼するか。
    /// </summary>
    public bool TrustServerCertificate { get; set; }

    /// <summary>
    /// 信頼させたい証明書ファイルへのパス。
    /// </summary>
    public string? CertificatePath { get; set; }
}
