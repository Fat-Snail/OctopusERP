using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctopusOA.Api.DTOs;
using OctopusOA.Api.Persistence;
using OctopusOA.Api.Services;
using System.Security.Claims;

namespace OctopusOA.Api.Controllers;

/// <summary>职员档案管理（HR 端）</summary>
[ApiController]
[Route("api/employee")]
[Authorize]
public class EmployeeController : ControllerBase
{
    private readonly OaDbContext _db;
    private readonly EmployeeService _service;

    public EmployeeController(OaDbContext db, EmployeeService service)
    {
        _db = db;
        _service = service;
    }

    private long GetCurrentUserId() =>
        long.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    /// <summary>创建临时档案</summary>
    [HttpPost]
    public IActionResult Create([FromBody] CreateEmployeeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return Ok(new { code = 400, msg = "姓名不能为空" });
        if (string.IsNullOrWhiteSpace(req.Phone)) return Ok(new { code = 400, msg = "手机号不能为空" });

        var emp = _service.Create(req, GetCurrentUserId());
        var resp = _service.MapToResponse(emp);
        resp.H5Url = $"{Request.Scheme}://{Request.Host}/h5/onboard/{emp.H5Token}";
        return Ok(new { code = 200, msg = "创建成功", data = resp });
    }

    /// <summary>档案列表</summary>
    [HttpGet("list")]
    public IActionResult GetList([FromQuery] string? status, [FromQuery] string? name)
    {
        var query = _db.Employees.AsEnumerable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(e => e.Status == status);
        if (!string.IsNullOrEmpty(name)) query = query.Where(e => e.Name.Contains(name));

        var list = query.OrderByDescending(e => e.CreateTime).Select(e =>
        {
            var resp = _service.MapToResponse(e);
            resp.H5Url = $"{Request.Scheme}://{Request.Host}/h5/onboard/{e.H5Token}";
            return resp;
        }).ToList();
        return Ok(new { code = 200, msg = "ok", data = new { rows = list, total = list.Count } });
    }

    /// <summary>档案详情</summary>
    [HttpGet("{id:long}")]
    public IActionResult GetById(long id)
    {
        var emp = _db.Employees.FirstOrDefault(e => e.EmployeeId == id);
        if (emp == null) return Ok(new { code = 404, msg = "档案不存在" });

        var resp = _service.MapToResponse(emp);
        resp.H5Url = $"{Request.Scheme}://{Request.Host}/h5/onboard/{emp.H5Token}";
        return Ok(new { code = 200, msg = "ok", data = resp });
    }

    /// <summary>修改 HR 填写部分</summary>
    [HttpPut("{id:long}")]
    public IActionResult Update(long id, [FromBody] UpdateEmployeeRequest req)
    {
        var emp = _db.Employees.FirstOrDefault(e => e.EmployeeId == id);
        if (emp == null) return Ok(new { code = 404, msg = "档案不存在" });

        emp.Name = req.Name; emp.Gender = req.Gender; emp.Phone = req.Phone;
        emp.BirthDate = req.BirthDate; emp.Email = req.Email; emp.Ethnicity = req.Ethnicity;
        emp.ApplyPosition = req.ApplyPosition; emp.ApplyDeptId = req.ApplyDeptId; emp.ApplyDeptName = req.ApplyDeptName;
        emp.Education = req.Education; emp.GraduateSchool = req.GraduateSchool; emp.Major = req.Major;
        emp.ExpectedSalary = req.ExpectedSalary; emp.WorkYears = req.WorkYears;
        emp.ResumeUrl = req.ResumeUrl; emp.HrRemark = req.HrRemark;
        emp.UpdateTime = DateTime.UtcNow;
        _db.SaveChanges();
        return Ok(new { code = 200, msg = "修改成功" });
    }

    /// <summary>确认入职</summary>
    [HttpPut("{id:long}/confirm")]
    public IActionResult Confirm(long id)
    {
        var err = _service.Confirm(id);
        return err != null ? Ok(new { code = 500, msg = err }) : Ok(new { code = 200, msg = "已确认入职" });
    }

    /// <summary>拒绝入职</summary>
    [HttpPut("{id:long}/reject")]
    public IActionResult Reject(long id)
    {
        var err = _service.Reject(id);
        return err != null ? Ok(new { code = 500, msg = err }) : Ok(new { code = 200, msg = "已拒绝" });
    }

    /// <summary>办理离职</summary>
    [HttpPut("{id:long}/resign")]
    public IActionResult Resign(long id)
    {
        var err = _service.Resign(id);
        return err != null ? Ok(new { code = 500, msg = err }) : Ok(new { code = 200, msg = "已办理离职" });
    }

    /// <summary>删除档案（仅 temp/rejected）</summary>
    [HttpDelete("{id:long}")]
    public IActionResult Delete(long id)
    {
        var err = _service.Delete(id);
        return err != null ? Ok(new { code = 500, msg = err }) : Ok(new { code = 200, msg = "已删除" });
    }
}
