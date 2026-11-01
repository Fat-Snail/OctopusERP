using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OctopusCommon.Security;

namespace OctopusUMC.Api.Tests;

/// <summary>
/// 内部接口密钥策略。
///
/// 早期实现是 `config[key] ?? "retail-internal-secret-dev"`：
/// 配置缺失时**静默回退**到仓库里公开的开发密钥。开发环境无所谓，
/// 生产环境一旦漏配，等于用一个人人皆知的密码保护「可以直接建号」的接口。
///
/// 现行策略是 fail-closed：只有 Development / Test 才允许回退。
/// </summary>
public class InternalApiSecretTests
{
    private static IConfiguration Config(string? secret) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [InternalApiSecret.ConfigKey] = secret,
            }!)
            .Build();

    private static IHostEnvironment Env(string name) => new FakeEnv(name);

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public void Unconfigured_InDevelopmentOrTest_FallsBackToDevSecret(string envName)
    {
        InternalApiSecret.Resolve(Config(null), Env(envName))
            .Should().Be(InternalApiSecret.DevelopmentDefault);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Unconfigured_OutsideDevAndTest_ReturnsNull(string envName)
    {
        InternalApiSecret.Resolve(Config(null), Env(envName))
            .Should().BeNull("生产/预发布漏配密钥时必须拒绝，而不是回退到公开的开发密钥");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void Configured_AlwaysWins(string envName)
    {
        InternalApiSecret.Resolve(Config("s3cr3t-from-config"), Env(envName))
            .Should().Be("s3cr3t-from-config");
    }

    [Fact]
    public void BlankConfiguration_IsTreatedAsMissing()
    {
        InternalApiSecret.Resolve(Config("   "), Env("Production")).Should().BeNull();
    }

    [Fact]
    public void Matches_RejectsNullOrEmptyAndMismatch()
    {
        InternalApiSecret.Matches(null, "abc").Should().BeFalse();
        InternalApiSecret.Matches("", "abc").Should().BeFalse();
        InternalApiSecret.Matches("abc", null).Should().BeFalse();
        InternalApiSecret.Matches("abd", "abc").Should().BeFalse();
        InternalApiSecret.Matches("abc", "abc").Should().BeTrue();
    }

    private sealed class FakeEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "OctopusUMC.Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
