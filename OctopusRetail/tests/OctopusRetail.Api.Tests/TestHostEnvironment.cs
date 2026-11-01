using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace OctopusRetail.Api.Tests;

/// <summary>单元测试里构造服务时用的最小 IHostEnvironment（默认 Test 环境）。</summary>
public sealed class TestHostEnvironment(string name = "Test") : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "OctopusRetail.Api.Tests";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
