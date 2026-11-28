using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusFIN.Api.Filters;
using OctopusFIN.Api.Services;

namespace OctopusFIN.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/bank")]
public class BankController(BankService svc) : ControllerBase
{
    // ===== 银行账户 =====

    [HttpGet("accounts")]
    public async Task<IActionResult> ListAccounts(
        [FromQuery] string? status, [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var (rows, total) = await svc.ListAccountsAsync(status, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    [HttpPost("accounts")]
    [OperationLog("创建银行账户")]
    public async Task<IActionResult> CreateAccount([FromBody] BankAccountInput req)
    {
        var (data, error) = await svc.CreateAccountAsync(req);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "账户已创建", data });
    }

    [HttpGet("accounts/{id:long}/balance")]
    public async Task<IActionResult> GetBalance(long id)
    {
        var data = await svc.GetBalanceAsync(id);
        return Ok(new { code = data.Success ? 200 : 400, msg = data.Message, data });
    }

    // ===== 对账单 =====

    [HttpPost("accounts/{id:long}/statements/fetch")]
    [OperationLog("拉取银行对账单")]
    public async Task<IActionResult> FetchStatements(
        long id, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var (fetched, skipped, error) = await svc.FetchStatementsAsync(id, startDate, endDate);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "对账单拉取完成", data = new { fetched, skipped } });
    }

    [HttpGet("accounts/{id:long}/statements")]
    public async Task<IActionResult> ListStatements(
        long id, [FromQuery] string? matchStatus,
        [FromQuery] int pageNum = 1, [FromQuery] int pageSize = 20)
    {
        var (rows, total) = await svc.ListStatementsAsync(id, matchStatus, pageNum, pageSize);
        return Ok(new { code = 200, msg = "ok", data = new { rows, total } });
    }

    // ===== 自动对账 =====

    [HttpPost("accounts/{id:long}/reconcile")]
    [OperationLog("银行自动对账")]
    public async Task<IActionResult> Reconcile(long id)
    {
        var (matched, error) = await svc.AutoReconcileAsync(id);
        if (error != null) return Ok(new { code = 400, msg = error, data = (object?)null });
        return Ok(new { code = 200, msg = "对账完成", data = new { matched } });
    }

    // ===== 诊断 =====

    [HttpGet("adapter")]
    public IActionResult AdapterInfo()
    {
        var (code, isMock) = svc.GetAdapterInfo();
        return Ok(new { code = 200, msg = "ok", data = new { adapterCode = code, isMock } });
    }
}
