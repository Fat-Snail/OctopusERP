using OctopusOA.Api.Workflow;

namespace OctopusOA.Api.Services;

/// <summary>
/// 补卡审批通过 → 自动更新考勤（事件监听器扩展点）。
/// 等价于旧 ApprovalService 中 if (TemplateCode == "attendance_fix") 的硬编码联动，
/// 但改为可插拔监听器，引擎零改动。对应设计文档 §5.1。
/// </summary>
public class AttendanceFixListener(AttendanceService attendance) : IProcessEventListener
{
    public void OnEvent(ProcessEvent evt)
    {
        if (evt.Type != ProcessEventTypes.Approved) return;
        if (!string.Equals(evt.Definition.Code, "attendance_fix", StringComparison.OrdinalIgnoreCase)) return;
        attendance.HandleApprovalFix(evt.Instance.ApplicantId, evt.Instance.FormData, evt.Instance.Id);
    }
}
