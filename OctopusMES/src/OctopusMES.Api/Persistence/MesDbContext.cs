using Microsoft.EntityFrameworkCore;

namespace OctopusMES.Api.Persistence;

public class MesDbContext : DbContext
{
    public MesDbContext(DbContextOptions<MesDbContext> options) : base(options) { }

    public DbSet<MesSyncUser> SyncUsers => Set<MesSyncUser>();
    public DbSet<MesSupplier> Suppliers => Set<MesSupplier>();
    public DbSet<MesPurchaseOrder> PurchaseOrders => Set<MesPurchaseOrder>();
    public DbSet<MesPurchaseItem> PurchaseItems => Set<MesPurchaseItem>();
    public DbSet<MesWorkOrder> WorkOrders => Set<MesWorkOrder>();
    public DbSet<MesWorkOrderProcess> WorkOrderProcesses => Set<MesWorkOrderProcess>();
    public DbSet<MesEquipment> Equipments => Set<MesEquipment>();
    public DbSet<MesProduct> Products => Set<MesProduct>();
    public DbSet<MesPlmProductSyncRecord> PlmProductSyncRecords => Set<MesPlmProductSyncRecord>();
    public DbSet<MesPlmSyncEvent> PlmSyncEvents => Set<MesPlmSyncEvent>();
    public DbSet<MesManufacturingSnapshot> ManufacturingSnapshots => Set<MesManufacturingSnapshot>();
    public DbSet<MesSnapshotBomItem> SnapshotBomItems => Set<MesSnapshotBomItem>();
    public DbSet<MesSnapshotOperation> SnapshotOperations => Set<MesSnapshotOperation>();
    public DbSet<MesPlmSyncAttempt> PlmSyncAttempts => Set<MesPlmSyncAttempt>();
    public DbSet<MesProductionOrderVersionAudit> ProductionOrderVersionAudits => Set<MesProductionOrderVersionAudit>();
    public DbSet<MesBom> Boms => Set<MesBom>();
    public DbSet<MesBomItem> BomItems => Set<MesBomItem>();
    public DbSet<MesRouting> Routings => Set<MesRouting>();
    public DbSet<MesRoutingOperation> RoutingOperations => Set<MesRoutingOperation>();
    public DbSet<MesWorkCenter> WorkCenters => Set<MesWorkCenter>();
    public DbSet<MesResource> Resources => Set<MesResource>();
    public DbSet<MesShift> Shifts => Set<MesShift>();
    public DbSet<MesResourceCalendar> ResourceCalendars => Set<MesResourceCalendar>();
    public DbSet<MesProductionOrder> ProductionOrders => Set<MesProductionOrder>();
    public DbSet<MesOperationTask> OperationTasks => Set<MesOperationTask>();
    public DbSet<MesSchedulePlan> SchedulePlans => Set<MesSchedulePlan>();
    public DbSet<MesScheduleSlot> ScheduleSlots => Set<MesScheduleSlot>();
    public DbSet<MesGarmentTeam> GarmentTeams => Set<MesGarmentTeam>();
    public DbSet<MesGarmentSchedule> GarmentSchedules => Set<MesGarmentSchedule>();
    public DbSet<MesGarmentOrder> GarmentOrders => Set<MesGarmentOrder>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<MesSyncUser>().ToTable("mes_sync_user");
        b.Entity<MesSupplier>().ToTable("mes_supplier");
        b.Entity<MesPurchaseOrder>().ToTable("mes_purchase_order");
        b.Entity<MesPurchaseItem>().ToTable("mes_purchase_item");
        b.Entity<MesWorkOrder>().ToTable("mes_work_order");
        b.Entity<MesWorkOrderProcess>().ToTable("mes_work_order_process");
        b.Entity<MesEquipment>().ToTable("mes_equipment");
        b.Entity<MesProduct>().ToTable("mes_product");
        b.Entity<MesPlmProductSyncRecord>().ToTable("mes_plm_product_sync_record");
        b.Entity<MesPlmSyncEvent>().ToTable("mes_plm_sync_event");
        b.Entity<MesManufacturingSnapshot>().ToTable("mes_manufacturing_snapshot");
        b.Entity<MesSnapshotBomItem>().ToTable("mes_snapshot_bom_item");
        b.Entity<MesSnapshotOperation>().ToTable("mes_snapshot_operation");
        b.Entity<MesPlmSyncAttempt>().ToTable("mes_plm_sync_attempt");
        b.Entity<MesProductionOrderVersionAudit>().ToTable("mes_production_order_version_audit");
        b.Entity<MesBom>().ToTable("mes_bom");
        b.Entity<MesBomItem>().ToTable("mes_bom_item");
        b.Entity<MesRouting>().ToTable("mes_routing");
        b.Entity<MesRoutingOperation>().ToTable("mes_routing_operation");
        b.Entity<MesWorkCenter>().ToTable("mes_work_center");
        b.Entity<MesResource>().ToTable("mes_resource");
        b.Entity<MesShift>().ToTable("mes_shift");
        b.Entity<MesResourceCalendar>().ToTable("mes_resource_calendar");
        b.Entity<MesProductionOrder>().ToTable("mes_production_order");
        b.Entity<MesOperationTask>().ToTable("mes_operation_task");
        b.Entity<MesGarmentTeam>().ToTable("mes_garment_team");
        b.Entity<MesGarmentSchedule>().ToTable("mes_garment_schedule");
        b.Entity<MesGarmentOrder>().ToTable("mes_garment_order");

        b.Entity<MesSyncUser>().HasKey(x => x.Id);
        b.Entity<MesSyncUser>().Property(x => x.Id).ValueGeneratedOnAdd();
        b.Entity<MesSyncUser>().HasIndex(x => x.UmcUserId).IsUnique();

        b.Entity<MesSupplier>().HasKey(x => x.SupplierId);
        b.Entity<MesSupplier>().Property(x => x.SupplierId).ValueGeneratedOnAdd();

        b.Entity<MesPurchaseOrder>().HasKey(x => x.PurchaseId);
        b.Entity<MesPurchaseOrder>().Property(x => x.PurchaseId).ValueGeneratedOnAdd();
        b.Entity<MesPurchaseOrder>().Property(x => x.TotalAmount).HasColumnType("decimal(18,4)");

        b.Entity<MesPurchaseItem>().HasKey(x => x.ItemId);
        b.Entity<MesPurchaseItem>().Property(x => x.ItemId).ValueGeneratedOnAdd();
        b.Entity<MesPurchaseItem>().Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Entity<MesPurchaseItem>().Property(x => x.UnitPrice).HasColumnType("decimal(18,4)");
        b.Entity<MesPurchaseItem>().Property(x => x.Amount).HasColumnType("decimal(18,4)");

        b.Entity<MesWorkOrder>().HasKey(x => x.WorkOrderId);
        b.Entity<MesWorkOrder>().Property(x => x.WorkOrderId).ValueGeneratedOnAdd();
        b.Entity<MesWorkOrder>().Property(x => x.PlannedQty).HasColumnType("decimal(18,4)");
        b.Entity<MesWorkOrder>().Property(x => x.CompletedQty).HasColumnType("decimal(18,4)");

        b.Entity<MesWorkOrderProcess>().HasKey(x => x.ProcessId);
        b.Entity<MesWorkOrderProcess>().Property(x => x.ProcessId).ValueGeneratedOnAdd();
        b.Entity<MesWorkOrderProcess>().Property(x => x.QualifiedQty).HasColumnType("decimal(18,4)");
        b.Entity<MesWorkOrderProcess>().Property(x => x.DefectQty).HasColumnType("decimal(18,4)");
        b.Entity<MesWorkOrderProcess>().Property(x => x.ReworkQty).HasColumnType("decimal(18,4)");

        b.Entity<MesProduct>().HasKey(x => x.ProductId);
        b.Entity<MesProduct>().Property(x => x.ProductId).ValueGeneratedOnAdd();
        b.Entity<MesProduct>().HasIndex(x => new { x.ProductCode, x.Version }).IsUnique();
        b.Entity<MesPlmProductSyncRecord>().HasKey(x => x.SyncRecordId);
        b.Entity<MesPlmProductSyncRecord>().Property(x => x.SyncRecordId).ValueGeneratedOnAdd();
        b.Entity<MesPlmProductSyncRecord>().HasIndex(x => new { x.PlmProductId, x.PlmVersion }).IsUnique();
        b.Entity<MesPlmProductSyncRecord>().HasIndex(x => x.Status);
        b.Entity<MesPlmSyncEvent>().HasKey(x => x.SyncEventId);
        b.Entity<MesPlmSyncEvent>().Property(x => x.SyncEventId).ValueGeneratedOnAdd();
        b.Entity<MesPlmSyncEvent>().HasIndex(x => x.EventId).IsUnique();
        b.Entity<MesManufacturingSnapshot>().HasKey(x => x.ManufacturingSnapshotId);
        b.Entity<MesManufacturingSnapshot>().Property(x => x.ManufacturingSnapshotId).ValueGeneratedOnAdd();
        b.Entity<MesManufacturingSnapshot>().HasIndex(x => new { x.PlmProductId, x.PlmManufacturingVersionId, x.PayloadHash }).IsUnique();
        b.Entity<MesManufacturingSnapshot>().HasIndex(x => new { x.ProductCode, x.Status });
        b.Entity<MesSnapshotBomItem>().HasKey(x => x.SnapshotBomItemId);
        b.Entity<MesSnapshotBomItem>().Property(x => x.SnapshotBomItemId).ValueGeneratedOnAdd();
        b.Entity<MesSnapshotBomItem>().Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Entity<MesSnapshotBomItem>().Property(x => x.LossRate).HasColumnType("decimal(9,6)");
        b.Entity<MesSnapshotBomItem>().HasIndex(x => new { x.ManufacturingSnapshotId, x.LineNo }).IsUnique();
        b.Entity<MesManufacturingSnapshot>().HasMany(x => x.BomItems).WithOne(x => x.ManufacturingSnapshot)
            .HasForeignKey(x => x.ManufacturingSnapshotId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<MesSnapshotOperation>().HasKey(x => x.SnapshotOperationId);
        b.Entity<MesSnapshotOperation>().Property(x => x.SnapshotOperationId).ValueGeneratedOnAdd();
        b.Entity<MesSnapshotOperation>().HasIndex(x => new { x.ManufacturingSnapshotId, x.Sequence }).IsUnique();
        b.Entity<MesManufacturingSnapshot>().HasMany(x => x.Operations).WithOne(x => x.ManufacturingSnapshot)
            .HasForeignKey(x => x.ManufacturingSnapshotId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<MesPlmSyncAttempt>().HasKey(x => x.SyncAttemptId);
        b.Entity<MesPlmSyncAttempt>().Property(x => x.SyncAttemptId).ValueGeneratedOnAdd();
        b.Entity<MesPlmSyncAttempt>().HasIndex(x => new { x.SyncRecordId, x.AttemptNumber }).IsUnique();
        b.Entity<MesPlmProductSyncRecord>().HasMany(x => x.Attempts).WithOne(x => x.SyncRecord)
            .HasForeignKey(x => x.SyncRecordId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<MesPlmProductSyncRecord>().HasOne(x => x.ManufacturingSnapshot).WithMany()
            .HasForeignKey(x => x.ManufacturingSnapshotId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<MesProductionOrderVersionAudit>().HasKey(x => x.VersionAuditId);
        b.Entity<MesProductionOrderVersionAudit>().Property(x => x.VersionAuditId).ValueGeneratedOnAdd();

        b.Entity<MesBom>().HasKey(x => x.BomId);
        b.Entity<MesBom>().Property(x => x.BomId).ValueGeneratedOnAdd();
        b.Entity<MesBom>().HasIndex(x => new { x.ProductId, x.Version }).IsUnique();
        b.Entity<MesBom>().HasOne(x => x.Product).WithMany(x => x.Boms)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<MesBomItem>().HasKey(x => x.BomItemId);
        b.Entity<MesBomItem>().Property(x => x.BomItemId).ValueGeneratedOnAdd();
        b.Entity<MesBomItem>().Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Entity<MesBomItem>().Property(x => x.LossRate).HasColumnType("decimal(9,6)");
        b.Entity<MesBom>().HasMany(x => x.Items).WithOne(x => x.Bom)
            .HasForeignKey(x => x.BomId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<MesRouting>().HasKey(x => x.RoutingId);
        b.Entity<MesRouting>().Property(x => x.RoutingId).ValueGeneratedOnAdd();
        b.Entity<MesRouting>().HasIndex(x => new { x.ProductId, x.Version }).IsUnique();
        b.Entity<MesRouting>().HasOne(x => x.Product).WithMany(x => x.Routings)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<MesRoutingOperation>().HasKey(x => x.RoutingOperationId);
        b.Entity<MesRoutingOperation>().Property(x => x.RoutingOperationId).ValueGeneratedOnAdd();
        b.Entity<MesRoutingOperation>().HasIndex(x => new { x.RoutingId, x.Sequence }).IsUnique();
        b.Entity<MesRouting>().HasMany(x => x.Operations).WithOne(x => x.Routing)
            .HasForeignKey(x => x.RoutingId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<MesWorkCenter>().HasKey(x => x.WorkCenterId);
        b.Entity<MesWorkCenter>().Property(x => x.WorkCenterId).ValueGeneratedOnAdd();
        b.Entity<MesWorkCenter>().HasIndex(x => x.WorkCenterCode).IsUnique();

        b.Entity<MesResource>().HasKey(x => x.ResourceId);
        b.Entity<MesResource>().Property(x => x.ResourceId).ValueGeneratedOnAdd();
        b.Entity<MesResource>().HasIndex(x => x.ResourceCode).IsUnique();
        b.Entity<MesResource>().HasOne(x => x.WorkCenter).WithMany(x => x.Resources)
            .HasForeignKey(x => x.WorkCenterId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<MesShift>().HasKey(x => x.ShiftId);
        b.Entity<MesShift>().Property(x => x.ShiftId).ValueGeneratedOnAdd();
        b.Entity<MesShift>().HasIndex(x => x.ShiftCode).IsUnique();

        b.Entity<MesResourceCalendar>().HasKey(x => x.ResourceCalendarId);
        b.Entity<MesResourceCalendar>().Property(x => x.ResourceCalendarId).ValueGeneratedOnAdd();
        b.Entity<MesResourceCalendar>().HasIndex(x => new { x.ResourceId, x.WorkDate }).IsUnique();
        b.Entity<MesResourceCalendar>().HasOne(x => x.Resource).WithMany(x => x.Calendars)
            .HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<MesResourceCalendar>().HasOne(x => x.Shift).WithMany(x => x.Calendars)
            .HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<MesProductionOrder>().HasKey(x => x.ProductionOrderId);
        b.Entity<MesProductionOrder>().Property(x => x.ProductionOrderId).ValueGeneratedOnAdd();
        b.Entity<MesProductionOrder>().Property(x => x.PlannedQty).HasColumnType("decimal(18,4)");
        b.Entity<MesProductionOrder>().Property(x => x.CompletedQty).HasColumnType("decimal(18,4)");
        b.Entity<MesProductionOrder>().HasIndex(x => x.ProductionOrderCode).IsUnique();
        b.Entity<MesProductionOrder>().HasIndex(x => x.Status);
        b.Entity<MesProductionOrder>().HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<MesProductionOrder>().HasOne(x => x.Routing).WithMany()
            .HasForeignKey(x => x.RoutingId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<MesProductionOrder>().HasOne(x => x.ManufacturingSnapshot).WithMany()
            .HasForeignKey(x => x.ManufacturingSnapshotId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MesProductionOrderVersionAudit>().HasOne<MesProductionOrder>().WithMany()
            .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<MesProductionOrderVersionAudit>().HasOne<MesManufacturingSnapshot>().WithMany()
            .HasForeignKey(x => x.OldSnapshotId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MesProductionOrderVersionAudit>().HasOne<MesManufacturingSnapshot>().WithMany()
            .HasForeignKey(x => x.NewSnapshotId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<MesOperationTask>().HasKey(x => x.OperationTaskId);
        b.Entity<MesOperationTask>().Property(x => x.OperationTaskId).ValueGeneratedOnAdd();
        b.Entity<MesOperationTask>().Property(x => x.PlannedQty).HasColumnType("decimal(18,4)");
        b.Entity<MesOperationTask>().Property(x => x.CompletedQty).HasColumnType("decimal(18,4)");
        b.Entity<MesOperationTask>().HasIndex(x => new { x.ProductionOrderId, x.Sequence }).IsUnique();
        b.Entity<MesOperationTask>().HasOne(x => x.ProductionOrder).WithMany(x => x.Tasks)
            .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<MesOperationTask>().HasOne(x => x.RoutingOperation).WithMany()
            .HasForeignKey(x => x.RoutingOperationId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<MesOperationTask>().HasOne(x => x.PredecessorTask).WithMany()
            .HasForeignKey(x => x.PredecessorTaskId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<MesSchedulePlan>().ToTable("mes_schedule_plan");
        b.Entity<MesSchedulePlan>().HasKey(x => x.SchedulePlanId);
        b.Entity<MesSchedulePlan>().Property(x => x.SchedulePlanId).ValueGeneratedOnAdd();
        b.Entity<MesSchedulePlan>().HasIndex(x => x.PlanCode).IsUnique();
        b.Entity<MesSchedulePlan>().HasIndex(x => x.Status);
        b.Entity<MesSchedulePlan>().HasMany(x => x.Slots).WithOne(x => x.Plan)
            .HasForeignKey(x => x.SchedulePlanId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<MesScheduleSlot>().ToTable("mes_schedule_slot");
        b.Entity<MesScheduleSlot>().HasKey(x => x.ScheduleSlotId);
        b.Entity<MesScheduleSlot>().Property(x => x.ScheduleSlotId).ValueGeneratedOnAdd();
        b.Entity<MesScheduleSlot>().HasIndex(x => new { x.SchedulePlanId, x.OperationTaskId }).IsUnique();

        b.Entity<MesEquipment>().HasKey(x => x.EquipmentId);
        b.Entity<MesEquipment>().Property(x => x.EquipmentId).ValueGeneratedOnAdd();
        b.Entity<MesEquipment>().HasIndex(x => x.EquipmentCode).IsUnique();

        b.Entity<MesWorkOrderProcess>()
            .HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<MesSupplier>()
            .HasMany(x => x.PurchaseOrders)
            .WithOne(x => x.Supplier)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<MesPurchaseOrder>()
            .HasMany(x => x.Items)
            .WithOne(x => x.PurchaseOrder)
            .HasForeignKey(x => x.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<MesWorkOrder>()
            .HasMany(x => x.Processes)
            .WithOne(x => x.WorkOrder)
            .HasForeignKey(x => x.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<MesGarmentTeam>().HasKey(x => x.TeamId);
        b.Entity<MesGarmentTeam>().Property(x => x.TeamId).ValueGeneratedOnAdd();
        b.Entity<MesGarmentTeam>().HasIndex(x => x.TeamCode).IsUnique();

        b.Entity<MesGarmentSchedule>().HasKey(x => x.ScheduleId);
        b.Entity<MesGarmentSchedule>().Property(x => x.ScheduleId).ValueGeneratedOnAdd();
        b.Entity<MesGarmentSchedule>().Property(x => x.PlannedQty).HasColumnType("decimal(18,4)");
        b.Entity<MesGarmentSchedule>().Property(x => x.SewingQty).HasColumnType("decimal(18,4)");
        b.Entity<MesGarmentSchedule>().Property(x => x.CuttingQty).HasColumnType("decimal(18,4)");
        b.Entity<MesGarmentSchedule>().Property(x => x.TotalHours).HasColumnType("decimal(18,2)");
        b.Entity<MesGarmentSchedule>().Property(x => x.AvgWage).HasColumnType("decimal(18,2)");
        b.Entity<MesGarmentSchedule>().HasIndex(x => new { x.TeamId, x.ScheduleDate }).IsUnique();
        b.Entity<MesGarmentSchedule>().HasIndex(x => x.Season);
        b.Entity<MesGarmentSchedule>().HasIndex(x => x.ScheduleDate);
        b.Entity<MesGarmentSchedule>().HasIndex(x => x.CustomerId);
        b.Entity<MesGarmentSchedule>().HasIndex(x => x.ContractId);
        b.Entity<MesGarmentSchedule>().HasIndex(x => x.PlmProductId);

        b.Entity<MesGarmentTeam>().HasIndex(x => x.LeaderUmcUserId);

        b.Entity<MesGarmentTeam>()
            .HasMany(x => x.Schedules)
            .WithOne(x => x.Team)
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<MesGarmentOrder>().HasKey(x => x.OrderId);
        b.Entity<MesGarmentOrder>().Property(x => x.OrderId).ValueGeneratedOnAdd();
        b.Entity<MesGarmentOrder>().Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Entity<MesGarmentOrder>().Property(x => x.ScheduledQty).HasColumnType("decimal(18,4)");
        b.Entity<MesGarmentOrder>().HasIndex(x => x.ContractId).IsUnique();
        b.Entity<MesGarmentOrder>().HasIndex(x => new { x.CustomerId, x.OrderNo });
        b.Entity<MesGarmentOrder>().HasIndex(x => x.Status);

        b.Entity<MesGarmentOrder>()
            .HasMany(x => x.Schedules)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
