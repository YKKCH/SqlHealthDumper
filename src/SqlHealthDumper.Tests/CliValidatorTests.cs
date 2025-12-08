using SqlHealthDumper.Options;
using Xunit;

namespace SqlHealthDumper.Tests;

/// <summary>
/// <see cref="CliValidator"/> の入力検証ロジックを確認するテスト群。
/// </summary>
public sealed class CliValidatorTests
{
    [Fact]
    /// <summary>
    /// サーバーと接続文字列の両方が欠けている場合にエラーが返ることを検証。
    /// </summary>
    public void Validate_RequiresServerOrConnectionString()
    {
        var cli = new CliOptions();
        var errors = CliValidator.Validate(cli);
        Assert.Contains(errors, e => e.Contains("server") && e.Contains("connection-string"));
    }

    [Fact]
    /// <summary>
    /// SQL 認証時に資格情報が必須であることを検証。
    /// </summary>
    public void Validate_RequiresSqlCredentialsWhenSqlAuth()
    {
        var cli = new CliOptions { Auth = "sql" };
        var errors = CliValidator.Validate(cli);
        Assert.Contains(errors, e => e.Contains("--user"));
        Assert.Contains(errors, e => e.Contains("--password"));
    }

    [Fact]
    /// <summary>
    /// Windows 認証の最小構成がエラーにならないことを確認。
    /// </summary>
    public void Validate_NoErrorsForMinimalWindowsAuth()
    {
        var cli = new CliOptions { Server = "localhost", Auth = "windows" };
        var errors = CliValidator.Validate(cli);
        Assert.Empty(errors);
    }
}
