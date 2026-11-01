namespace OctopusMES.Api.Persistence;

public class MesSyncUser
{
    public long Id { get; set; }
    public long UmcUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Avatar { get; set; }
    public int Status { get; set; } = 1;
    public DateTime LastSyncAt { get; set; }
}

public class MesSupplier
{
    public long SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? BankAccount { get; set; }
    public string? TaxNumber { get; set; }
    public string Status { get; set; } = "active";
    public int Level { get; set; } = 3;
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<MesPurchaseOrder> PurchaseOrders { get; set; } = new();
}

public class MesPurchaseOrder
{
    public long PurchaseId { get; set; }
    public string PurchaseCode { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public string Status { get; set; } = "draft";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "CNY";
    public DateTime? ExpectedDelivery { get; set; }
    public long? OaApprovalId { get; set; }
    public long? WmsInboundOrderId { get; set; }
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public MesSupplier? Supplier { get; set; }
    public List<MesPurchaseItem> Items { get; set; } = new();
}

public class MesPurchaseItem
{
    public long ItemId { get; set; }
    public long PurchaseId { get; set; }
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string? Remark { get; set; }

    public MesPurchaseOrder? PurchaseOrder { get; set; }
}

public class MesWorkOrder
{
    public long WorkOrderId { get; set; }
    public string WorkOrderCode { get; set; } = string.Empty;
    public long? PlmProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal CompletedQty { get; set; }
    public string? Unit { get; set; }
    public string Status { get; set; } = "draft";
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public string? WorkshopName { get; set; }
    public string? Remark { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>P12：实际生产周期（小时），完工时计算</summary>
    public double? LeadTimeHr { get; set; }
    /// <summary>P12：排程偏差（小时）= ActualEnd - PlannedEnd（正=逾期）</summary>
    public double? ScheduleVarianceHr { get; set; }

    public List<MesWorkOrderProcess> Processes { get; set; } = new();
}

public class MesWorkOrderProcess
{
    public long ProcessId { get; set; }
    public long WorkOrderId { get; set; }
    public int StepOrder { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? WorkCenter { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? Remark { get; set; }

    // P12：报工字段
    public int? StandardCycleSec { get; set; }
    public int? ActualCycleSec { get; set; }
    public decimal QualifiedQty { get; set; }
    public decimal DefectQty { get; set; }
    public decimal ReworkQty { get; set; }
    public long? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public long? EquipmentId { get; set; }
    public int DowntimeMinutes { get; set; }
    public string? DowntimeReason { get; set; }

    public MesWorkOrder? WorkOrder { get; set; }
    public MesEquipment? Equipment { get; set; }
}

// 通用离散制造主数据：产品、BOM、工艺路线、工作中心、资源与日历。
public class MesProduct
{
    public long ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }

    public List<MesBom> Boms { get; set; } = new();
    public List<MesRouting> Routings { get; set; } = new();
}

public class MesPlmProductSyncRecord
{
    public long SyncRecordId { get; set; }
    public long PlmProductId { get; set; }
    public string PlmVersion { get; set; } = string.Empty;
    public long? PlmManufacturingVersionId { get; set; }
    public long? MesProductId { get; set; }
    public long? MesBomId { get; set; }
    public long? MesRoutingId { get; set; }
    public string SourceSystem { get; set; } = "PLM";
    public string Status { get; set; } = "pending";
    public string? PayloadHash { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? SyncedAt { get; set; }
    public long? ManufacturingSnapshotId { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public bool RequiresManualAction { get; set; }

    public MesManufacturingSnapshot? ManufacturingSnapshot { get; set; }
    public List<MesPlmSyncAttempt> Attempts { get; set; } = new();
}

public class MesPlmSyncEvent
{
    public long SyncEventId { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public long PlmProductId { get; set; }
    public long PlmManufacturingVersionId { get; set; }
    public string Status { get; set; } = "received";
    public string? ErrorMessage { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public class MesManufacturingSnapshot
{
    public long ManufacturingSnapshotId { get; set; }
    public long PlmProductId { get; set; }
    public long PlmManufacturingVersionId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductVersion { get; set; } = string.Empty;
    public string BomVersion { get; set; } = string.Empty;
    public string RoutingVersion { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTime SyncedAt { get; set; }

    public List<MesSnapshotBomItem> BomItems { get; set; } = new();
    public List<MesSnapshotOperation> Operations { get; set; } = new();
}

public class MesSnapshotBomItem
{
    public long SnapshotBomItemId { get; set; }
    public long ManufacturingSnapshotId { get; set; }
    public int LineNo { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal LossRate { get; set; }
    public MesManufacturingSnapshot? ManufacturingSnapshot { get; set; }
}

public class MesSnapshotOperation
{
    public long SnapshotOperationId { get; set; }
    public long ManufacturingSnapshotId { get; set; }
    public int Sequence { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string RequiredCapability { get; set; } = string.Empty;
    public int StandardCycleSec { get; set; }
    public int SetupMinutes { get; set; }
    public bool IsSplittable { get; set; }
    public MesManufacturingSnapshot? ManufacturingSnapshot { get; set; }
}

public class MesPlmSyncAttempt
{
    public long SyncAttemptId { get; set; }
    public long SyncRecordId { get; set; }
    public int AttemptNumber { get; set; }
    public string Status { get; set; } = "failed";
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ValidationIssuesJson { get; set; }
    public bool Retryable { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public MesPlmProductSyncRecord? SyncRecord { get; set; }
}

public class MesProductionOrderVersionAudit
{
    public long VersionAuditId { get; set; }
    public long ProductionOrderId { get; set; }
    public long OldSnapshotId { get; set; }
    public long NewSnapshotId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class MesBom
{
    public long BomId { get; set; }
    public long ProductId { get; set; }
    public string BomCode { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }

    public MesProduct? Product { get; set; }
    public List<MesBomItem> Items { get; set; } = new();
}

public class MesBomItem
{
    public long BomItemId { get; set; }
    public long BomId { get; set; }
    public int LineNo { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal LossRate { get; set; }

    public MesBom? Bom { get; set; }
}

public class MesRouting
{
    public long RoutingId { get; set; }
    public long ProductId { get; set; }
    public string RoutingCode { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }

    public MesProduct? Product { get; set; }
    public List<MesRoutingOperation> Operations { get; set; } = new();
}

public class MesRoutingOperation
{
    public long RoutingOperationId { get; set; }
    public long RoutingId { get; set; }
    public int Sequence { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string? RequiredCapability { get; set; }
    public int StandardCycleSec { get; set; }
    public int SetupMinutes { get; set; }
    public bool IsSplittable { get; set; } = true;
    public bool IsOutsource { get; set; }
    public int? PredecessorSequence { get; set; }

    public MesRouting? Routing { get; set; }
}

public class MesWorkCenter
{
    public long WorkCenterId { get; set; }
    public string WorkCenterCode { get; set; } = string.Empty;
    public string WorkCenterName { get; set; } = string.Empty;
    public string Status { get; set; } = "active";

    public List<MesResource> Resources { get; set; } = new();
}

public class MesResource
{
    public long ResourceId { get; set; }
    public string ResourceCode { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    /// <summary>equipment / team / subcontractor</summary>
    public string ResourceKind { get; set; } = "equipment";
    public long? WorkCenterId { get; set; }
    /// <summary>逗号分隔的能力编码，避免第一阶段引入额外能力表。</summary>
    public string? CapabilityCodes { get; set; }
    public string Status { get; set; } = "available";
    public string? OutsourcePartnerName { get; set; }

    public MesWorkCenter? WorkCenter { get; set; }
    public List<MesResourceCalendar> Calendars { get; set; } = new();
}

public class MesShift
{
    public long ShiftId { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int BreakMinutes { get; set; }
    public bool IsOvernight { get; set; }
    public string Status { get; set; } = "active";

    public List<MesResourceCalendar> Calendars { get; set; } = new();
}

public class MesResourceCalendar
{
    public long ResourceCalendarId { get; set; }
    public long ResourceId { get; set; }
    public DateTime WorkDate { get; set; }
    public long? ShiftId { get; set; }
    public bool IsAvailable { get; set; } = true;
    public TimeSpan? AvailableFrom { get; set; }
    public TimeSpan? AvailableTo { get; set; }
    public bool IsMaintenance { get; set; }
    public string? Reason { get; set; }

    public MesResource? Resource { get; set; }
    public MesShift? Shift { get; set; }
}

public class MesProductionOrder
{
    public long ProductionOrderId { get; set; }
    public string ProductionOrderCode { get; set; } = string.Empty;
    public long? ProductId { get; set; }
    public long? RoutingId { get; set; }
    public long? PlmSyncRecordId { get; set; }
    public long? ManufacturingSnapshotId { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal CompletedQty { get; set; }
    public DateTime? DueDate { get; set; }
    public int Priority { get; set; }
    public string Status { get; set; } = "draft";
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public MesProduct? Product { get; set; }
    public MesRouting? Routing { get; set; }
    public MesManufacturingSnapshot? ManufacturingSnapshot { get; set; }
    public List<MesOperationTask> Tasks { get; set; } = new();
}

public class MesOperationTask
{
    public long OperationTaskId { get; set; }
    public long ProductionOrderId { get; set; }
    public long? RoutingOperationId { get; set; }
    public long? PredecessorTaskId { get; set; }
    public int Sequence { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string? RequiredCapability { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal CompletedQty { get; set; }
    public int StandardCycleSec { get; set; }
    public string Status { get; set; } = "pending";
    public bool IsSplittable { get; set; } = true;
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }

    public MesProductionOrder? ProductionOrder { get; set; }
    public MesRoutingOperation? RoutingOperation { get; set; }
    public MesOperationTask? PredecessorTask { get; set; }
}

public class MesSchedulePlan
{
    public long SchedulePlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public long ProductionOrderId { get; set; }
    public string Status { get; set; } = "simulation";
    public string Strategy { get; set; } = "due_date_first";
    public DateTime? FrozenUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<MesScheduleSlot> Slots { get; set; } = new();
}

public class MesScheduleSlot
{
    public long ScheduleSlotId { get; set; }
    public long SchedulePlanId { get; set; }
    public long OperationTaskId { get; set; }
    public long ResourceId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = "planned";
    public MesSchedulePlan? Plan { get; set; }
}

// P12：设备档案（OEE 计算基础）
public class MesEquipment
{
    public long EquipmentId { get; set; }
    public string EquipmentCode { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? WorkCenter { get; set; }
    /// <summary>idle / running / maintenance / fault / offline</summary>
    public string Status { get; set; } = "idle";
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
}

// 服装车间组（A组/B组/C组/D组/L组下多个组长子单元）
public class MesGarmentTeam
{
    public long TeamId { get; set; }
    /// <summary>组编码：A / B / C / D / L-104 / L-105 / L-定制 / L-瑞棉 / L-宝铭</summary>
    public string TeamCode { get; set; } = string.Empty;
    /// <summary>显示名：A组-员工100</summary>
    public string TeamName { get; set; } = string.Empty;
    /// <summary>原始组别(L组下多组长共享 GroupLabel = "L组")</summary>
    public string GroupLabel { get; set; } = string.Empty;
    /// <summary>组长姓名快照(冗余字段，便于显示与 UMC 不可达时降级)</summary>
    public string Leader { get; set; } = string.Empty;
    /// <summary>组长 UMC UserId(自有员工)。外协组为 null</summary>
    public long? LeaderUmcUserId { get; set; }
    /// <summary>对应 UMC 部门(A组/B组/...)。外协组可为 null</summary>
    public long? UmcDeptId { get; set; }
    /// <summary>是否外协组(定制/瑞棉/宝铭)</summary>
    public bool IsOutsource { get; set; }
    /// <summary>外协合作方名称(IsOutsource=true 时填)</summary>
    public string? OutsourcePartnerName { get; set; }
    public int? Headcount { get; set; }
    public string Status { get; set; } = "active";
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<MesGarmentSchedule> Schedules { get; set; } = new();
}

// 服装订单池（CRM 合同激活后通过 Webhook 同步到 MES，等待生产经理拆分到组×日）
public class MesGarmentOrder
{
    public long OrderId { get; set; }
    /// <summary>关联 CRM 合同 ID</summary>
    public long ContractId { get; set; }
    /// <summary>关联 CRM 客户 ID</summary>
    public long CustomerId { get; set; }
    /// <summary>客户名称(快照)</summary>
    public string CustomerName { get; set; } = string.Empty;
    /// <summary>客户款号: 98071 / CC1557</summary>
    public string OrderNo { get; set; } = string.Empty;
    /// <summary>关联 PLM 商品 ID(可选,可由款式编码反查)</summary>
    public long? PlmProductId { get; set; }
    /// <summary>款式编码快照</summary>
    public string? StyleCode { get; set; }
    /// <summary>订单数量</summary>
    public decimal Quantity { get; set; }
    /// <summary>已排数量</summary>
    public decimal ScheduledQty { get; set; }
    /// <summary>季节</summary>
    public string? Season { get; set; }
    /// <summary>货期</summary>
    public DateTime? DueDate { get; set; }
    /// <summary>是否加单(返单)</summary>
    public bool IsAdditional { get; set; }
    /// <summary>原始合同/订单 ID</summary>
    public long? OriginalContractId { get; set; }
    /// <summary>状态: pending_schedule / partially_scheduled / fully_scheduled / cancelled</summary>
    public string Status { get; set; } = "pending_schedule";
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<MesGarmentSchedule> Schedules { get; set; } = new();
}

// 服装日排程（一组×一天 = 一条记录, JIT 排单）
public class MesGarmentSchedule
{
    public long ScheduleId { get; set; }
    /// <summary>所属订单池 ID(可空,人工排程时可不挂)</summary>
    public long? OrderId { get; set; }
    public long TeamId { get; set; }
    /// <summary>排程日期</summary>
    public DateTime ScheduleDate { get; set; }
    /// <summary>赛季: 22春夏 / 22秋冬 ...</summary>
    public string Season { get; set; } = string.Empty;
    /// <summary>客户名快照: 客户001 / 客户002 (冗余字段，CrmCustomer 不可达时降级显示)</summary>
    public string? Customer { get; set; }
    /// <summary>关联 CRM 客户 ID</summary>
    public long? CustomerId { get; set; }
    /// <summary>客户款号: 98071 / CC1557 / 96225加单4</summary>
    public string? OrderNo { get; set; }
    /// <summary>关联 CRM 合同 ID</summary>
    public long? ContractId { get; set; }
    /// <summary>款式编码快照: LB5210100龟背绣花</summary>
    public string? StyleCode { get; set; }
    /// <summary>关联 PLM 商品 ID</summary>
    public long? PlmProductId { get; set; }
    /// <summary>关联 OA 投产审批单 ID(发布后填)</summary>
    public long? OaApprovalId { get; set; }
    /// <summary>关联 MES 工单 ID(审批通过后自动建)</summary>
    public long? WorkOrderId { get; set; }
    /// <summary>排程状态: draft / published / approved / in_production / completed / cancelled</summary>
    public string ScheduleStatus { get; set; } = "draft";
    /// <summary>货期</summary>
    public DateTime? DueDate { get; set; }
    /// <summary>预算产能(件)</summary>
    public decimal? PlannedQty { get; set; }
    /// <summary>实际车缝产出(件)</summary>
    public decimal? SewingQty { get; set; }
    /// <summary>裁床产出(件)</summary>
    public decimal? CuttingQty { get; set; }
    /// <summary>当日工时(小时)</summary>
    public decimal? TotalHours { get; set; }
    /// <summary>当日实到人数</summary>
    public int? Headcount { get; set; }
    /// <summary>当日均薪</summary>
    public decimal? AvgWage { get; set; }
    /// <summary>回布状态: "9-16/ok" / "存仓/ok" / "未到"</summary>
    public string? FabricStatus { get; set; }
    public string? Remark { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public MesGarmentTeam? Team { get; set; }
    public MesGarmentOrder? Order { get; set; }
}
