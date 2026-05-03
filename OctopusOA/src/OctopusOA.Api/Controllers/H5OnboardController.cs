using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.DTOs;
using OctopusOA.Api.Persistence;
using OctopusOA.Api.Services;

namespace OctopusOA.Api.Controllers;

/// <summary>H5 入职填写接口（无需登录，通过 token 验证）</summary>
[ApiController]
[Route("api/h5/onboard")]
[AllowAnonymous]
public class H5OnboardController : ControllerBase
{
    private readonly OaDbContext _db;
    private readonly EmployeeService _service;

    public H5OnboardController(OaDbContext db, EmployeeService service)
    {
        _db = db;
        _service = service;
    }

    /// <summary>获取 H5 表单数据（含 HR 预填信息）</summary>
    [HttpGet("{token}")]
    public IActionResult Get(string token)
    {
        var emp = _db.Employees.FirstOrDefault(e => e.H5Token == token);
        if (emp == null) return Ok(new { code = 404, msg = "无效的入职链接" });

        return Ok(new
        {
            code = 200,
            msg = "ok",
            data = new H5OnboardResponse
            {
                EmployeeId = emp.EmployeeId,
                Name = emp.Name,
                Phone = emp.Phone,
                Gender = emp.Gender,
                Email = emp.Email,
                BirthDate = emp.BirthDate,
                Ethnicity = emp.Ethnicity,
                ApplyPosition = emp.ApplyPosition,
                ApplyDeptName = emp.ApplyDeptName,
                Status = emp.Status,
                AlreadyFilled = emp.H5FilledAt.HasValue,
            }
        });
    }

    /// <summary>提交 H5 入职信息</summary>
    [HttpPost("{token}")]
    public IActionResult Submit(string token, [FromBody] H5SubmitRequest req)
    {
        var err = _service.H5Submit(token, req);
        if (err != null) return Ok(new { code = 500, msg = err });
        return Ok(new { code = 200, msg = "提交成功，请等待 HR 确认" });
    }
}
