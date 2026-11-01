using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusHRM.Api.Tests;

/// <summary>P14：HRM 集成测试</summary>
public class HrmTests : IClassFixture<HrmTestFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public HrmTests(HrmTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task GetEmployees_ReturnsSeed()
    {
        var resp = await _client.GetAsync("/api/employee");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task CreateEmployee_DuplicateNo_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/employee", new
        {
            employeeNo = "EMP-001", fullName = "重复号", baseSalary = 5000m,
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task CreateEmployee_AutoCode_Succeeds()
    {
        var resp = await _client.PostAsJsonAsync("/api/employee", new
        {
            fullName = "新员工", baseSalary = 8000m, department = "测试部",
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("employeeNo").GetString()!.StartsWith("EMP-").Should().BeTrue();
    }

    [Fact]
    public async Task ResignEmployee_ChangesStatus()
    {
        // 先建一个临时员工用于离职
        var createResp = await _client.PostAsJsonAsync("/api/employee", new { fullName = "待离职", baseSalary = 3000m });
        var id = (await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("employeeId").GetInt64();

        var resp = await _client.PutAsJsonAsync($"/api/employee/{id}/resign", new { resignedAt = DateTime.UtcNow });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("resigned");
    }

    [Fact]
    public async Task PayrollFlow_StartCalculateApprovePay()
    {
        // start
        var startResp = await _client.PostAsJsonAsync("/api/payroll/start", new { year = 2026, month = 11 });
        var startDoc = await startResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        startDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var runId = startDoc.RootElement.GetProperty("data").GetProperty("payrollRunId").GetInt64();

        // calculate
        var calcResp = await _client.PutAsync($"/api/payroll/{runId}/calculate", null);
        var calcDoc = await calcResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        calcDoc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        var calc = calcDoc.RootElement.GetProperty("data");
        calc.GetProperty("status").GetString().Should().Be("calculated");
        calc.GetProperty("employeeCount").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        calc.GetProperty("totalGrossPay").GetDecimal().Should().BeGreaterThan(0);

        // approve & pay
        var payResp = await _client.PutAsync($"/api/payroll/{runId}/approve-pay", null);
        var payDoc = await payResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        payDoc!.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("paid");
        payDoc.RootElement.GetProperty("data").GetProperty("paidAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task PayrollStart_DuplicatePeriod_ReturnsExisting()
    {
        var first = await _client.PostAsJsonAsync("/api/payroll/start", new { year = 2025, month = 12 });
        var firstId = (await first.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("payrollRunId").GetInt64();

        var second = await _client.PostAsJsonAsync("/api/payroll/start", new { year = 2025, month = 12 });
        var secondId = (await second.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("payrollRunId").GetInt64();

        secondId.Should().Be(firstId);
    }

    [Fact]
    public async Task PayrollCalculate_TwiceFails()
    {
        var s = await _client.PostAsJsonAsync("/api/payroll/start", new { year = 2027, month = 1 });
        var id = (await s.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("payrollRunId").GetInt64();
        await _client.PutAsync($"/api/payroll/{id}/calculate", null);
        var resp2 = await _client.PutAsync($"/api/payroll/{id}/calculate", null);
        var doc2 = await resp2.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc2!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task LeaveBalance_ConsumeReducesRemaining()
    {
        // 种子：employeeId=2 annual = 8 天剩余
        var resp = await _client.PostAsJsonAsync("/api/leave/consume", new
        {
            employeeId = 2L, leaveType = "annual", year = DateTime.UtcNow.Year, days = 1m,
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("remainingDays").GetDecimal().Should().Be(7m);

        // 重复扣减
        var resp2 = await _client.PostAsJsonAsync("/api/leave/consume", new
        {
            employeeId = 2L, leaveType = "annual", year = DateTime.UtcNow.Year, days = 100m,
        });
        var doc2 = await resp2.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc2!.RootElement.GetProperty("code").GetInt32().Should().Be(400);
        doc2.RootElement.GetProperty("msg").GetString().Should().Contain("不足");
    }

    [Fact]
    public async Task GetLeaveBalance_ByEmployee()
    {
        var resp = await _client.GetAsync("/api/leave/balance/2");
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Kpi_CreateAndSubmit()
    {
        var createResp = await _client.PostAsJsonAsync("/api/kpi", new
        {
            employeeId = 2L, period = "2026-Q4", title = "Q4 季度评估",
        });
        var id = (await createResp.Content.ReadFromJsonAsync<JsonDocument>(_json))!
            .RootElement.GetProperty("data").GetProperty("kpiId").GetInt64();

        var submitResp = await _client.PutAsJsonAsync($"/api/kpi/{id}/submit", new { score = 88m, comment = "表现优秀" });
        var doc = await submitResp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("grade").GetString().Should().Be("A"); // 88 → A
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("submitted");
    }

    [Fact]
    public async Task OaWebhook_HiredEmployee_CreatesHrmRecord()
    {
        var resp = await _client.PostAsJsonAsync("/api/webhook/oa/employee-hired", new
        {
            oaEmployeeId = 99L,
            umcUserId = 200L,
            fullName = "Webhook 入职员工",
            department = "运营部", position = "实习生",
            phone = "13900000000", email = "x@y.com",
        });
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("probation");

        // 幂等：同 oaEmployeeId 再调一次应返回原记录
        var resp2 = await _client.PostAsJsonAsync("/api/webhook/oa/employee-hired", new
        {
            oaEmployeeId = 99L, umcUserId = 200L, fullName = "Webhook 入职员工",
            department = "运营部", position = "实习生", phone = "x", email = "x@y.com",
        });
        var doc2 = await resp2.Content.ReadFromJsonAsync<JsonDocument>(_json);
        doc2!.RootElement.GetProperty("code").GetInt32().Should().Be(200);
    }
}
