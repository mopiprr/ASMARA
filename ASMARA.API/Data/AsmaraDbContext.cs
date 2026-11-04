using ASMARA.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Data;

public partial class AsmaraDbContext : DbContext
{
    public AsmaraDbContext(DbContextOptions<AsmaraDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<Asset> Assets { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<ItemCategory> ItemCategories { get; set; }

    public virtual DbSet<ItemInventory> ItemInventories { get; set; }

    public virtual DbSet<MaintenanceDetail> MaintenanceDetails { get; set; }

    public virtual DbSet<ProcurementDetail> ProcurementDetails { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Ticket> Tickets { get; set; }

    public virtual DbSet<TicketWorkflowLog> TicketWorkflowLogs { get; set; }

    public virtual DbSet<Workflow> Workflows { get; set; }

    public virtual DbSet<WorkflowStep> WorkflowSteps { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("Account");

            entity.HasIndex(e => e.EmployeeId, "UQ_Account_EmployeeId").IsUnique();

            entity.HasIndex(e => e.UserName, "UQ_Account_UserName").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())", "DF_Account_Id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Account_CreatedAt");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Account_IsActive");
            entity.Property(e => e.PasswordHashed).HasMaxLength(255);
            entity.Property(e => e.UserName).HasMaxLength(50);

            entity.HasOne(d => d.Employee).WithOne(p => p.Account)
                .HasForeignKey<Account>(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Account_Employee");
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("Asset");

            entity.HasIndex(e => e.EmployeeId, "IX_Asset_EmployeeId");

            entity.HasIndex(e => e.ItemId, "IX_Asset_ItemId");

            entity.HasIndex(e => e.AssetTag, "UQ_Asset_AssetTag").IsUnique();

            entity.Property(e => e.AssetTag).HasMaxLength(50);

            entity.HasOne(d => d.Employee).WithMany(p => p.Assets)
                .HasForeignKey(d => d.EmployeeId)
                .HasConstraintName("FK_Asset_Employee");

            entity.HasOne(d => d.Item).WithMany(p => p.Assets)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Asset_Item");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Department");

            entity.HasIndex(e => e.DepartmentCode, "UQ_Department_Code").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())", "DF_Department_Id");
            entity.Property(e => e.DepartmentCode).HasMaxLength(10);
            entity.Property(e => e.DepartmentName).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Department_IsActive");

            entity.HasOne(d => d.ParentDepartment).WithMany(p => p.InverseParentDepartment)
                .HasForeignKey(d => d.ParentDepartmentId)
                .HasConstraintName("FK_Department_Parent");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employee");

            entity.HasIndex(e => e.DepartmentId, "IX_Employee_DepartmentId");

            entity.HasIndex(e => e.ManagerId, "IX_Employee_ManagerId");

            entity.HasIndex(e => e.Email, "UQ_Employee_Email").IsUnique();

            entity.HasIndex(e => e.EmployeeNumber, "UQ_Employee_Number").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())", "DF_Employee_Id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Employee_CreatedAt");
            entity.Property(e => e.Email).HasMaxLength(50);
            entity.Property(e => e.EmployeeNumber).HasMaxLength(20);
            entity.Property(e => e.EmployeeStatus).HasDefaultValue((byte)1, "DF_Employee_Status");
            entity.Property(e => e.FullName).HasMaxLength(50);

            entity.HasOne(d => d.Department).WithMany(p => p.Employees)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Employee_Department");

            entity.HasOne(d => d.Manager).WithMany(p => p.InverseManager)
                .HasForeignKey(d => d.ManagerId)
                .HasConstraintName("FK_Employee_Manager");
        });

        modelBuilder.Entity<ItemCategory>(entity =>
        {
            entity.ToTable("ItemCategory");

            entity.HasIndex(e => e.CategoryCode, "UQ_ItemCategory_Code").IsUnique();

            entity.Property(e => e.CategoryCode).HasMaxLength(20);
            entity.Property(e => e.CategoryName).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_ItemCategory_IsActive");
        });

        modelBuilder.Entity<ItemInventory>(entity =>
        {
            entity.ToTable("ItemInventory");

            entity.HasIndex(e => e.ItemCategoryId, "IX_ItemInventory_ItemCategoryId");

            entity.HasIndex(e => e.ItemCode, "UQ_ItemInventory_ItemCode").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_ItemInventory_IsActive");
            entity.Property(e => e.ItemCode).HasMaxLength(20);
            entity.Property(e => e.ItemName).HasMaxLength(50);

            entity.HasOne(d => d.ItemCategory).WithMany(p => p.ItemInventories)
                .HasForeignKey(d => d.ItemCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ItemInventory_Category");
        });

        modelBuilder.Entity<MaintenanceDetail>(entity =>
        {
            entity.HasKey(e => e.TicketId);

            entity.ToTable("MaintenanceDetail");

            entity.HasIndex(e => e.AssetId, "IX_MaintenanceDetail_AssetId");

            entity.HasIndex(e => e.ReplacementAssetId, "IX_MaintenanceDetail_ReplacementAssetId");

            entity.Property(e => e.TicketId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_MaintenanceDetail_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.HandlerNote).HasMaxLength(500);
            entity.Property(e => e.PartRequirement).HasMaxLength(500);

            entity.HasOne(d => d.Asset).WithMany(p => p.MaintenanceDetailAssets)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MaintenanceDetail_Asset");

            entity.HasOne(d => d.ReplacementAsset).WithMany(p => p.MaintenanceDetailReplacementAssets)
                .HasForeignKey(d => d.ReplacementAssetId)
                .HasConstraintName("FK_MaintenanceDetail_ReplacementAsset");

            entity.HasOne(d => d.Ticket).WithOne(p => p.MaintenanceDetail)
                .HasForeignKey<MaintenanceDetail>(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MaintenanceDetail_Ticket");
        });

        modelBuilder.Entity<ProcurementDetail>(entity =>
        {
            entity.ToTable("ProcurementDetail");

            entity.HasIndex(e => e.ItemCategoryId, "IX_ProcurementDetail_ItemCategoryId");

            entity.HasIndex(e => e.ItemId, "IX_ProcurementDetail_ItemId");

            entity.HasIndex(e => e.TicketId, "IX_ProcurementDetail_TicketId");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ProcurementDetail_CreatedAt");
            entity.Property(e => e.ItemName).HasMaxLength(50);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.ItemCategory).WithMany(p => p.ProcurementDetails)
                .HasForeignKey(d => d.ItemCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProcurementDetail_Category");

            entity.HasOne(d => d.Item).WithMany(p => p.ProcurementDetails)
                .HasForeignKey(d => d.ItemId)
                .HasConstraintName("FK_ProcurementDetail_Item");

            entity.HasOne(d => d.Ticket).WithMany(p => p.ProcurementDetails)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProcurementDetail_Ticket");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");

            entity.HasIndex(e => e.RoleCode, "UQ_Role_RoleCode").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Role_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Role_IsActive");
            entity.Property(e => e.RoleCode).HasMaxLength(20);
            entity.Property(e => e.RoleName).HasMaxLength(50);

            entity.HasMany(d => d.Accounts).WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "AccountRole",
                    r => r.HasOne<Account>().WithMany()
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_AccountRole_Account"),
                    l => l.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_AccountRole_Role"),
                    j =>
                    {
                        j.HasKey("RoleId", "AccountId");
                        j.ToTable("AccountRole");
                        j.HasIndex(new[] { "AccountId" }, "IX_AccountRole_AccountId");
                    });
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Ticket");

            entity.HasIndex(e => e.AssignedToEmployeeId, "IX_Ticket_AssignedToEmployeeId");

            entity.HasIndex(e => e.CurrentTaskEmployeeId, "IX_Ticket_CurrentTaskEmployeeId");

            entity.HasIndex(e => e.EmployeeId, "IX_Ticket_EmployeeId");

            entity.HasIndex(e => e.ParentTicketId, "IX_Ticket_ParentTicketId");

            entity.HasIndex(e => e.TicketNumber, "UQ_Ticket_TicketNumber").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())", "DF_Ticket_Id");
            entity.Property(e => e.AssignedTo).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Ticket_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Subject).HasMaxLength(50);
            entity.Property(e => e.TicketNumber).HasMaxLength(20);

            entity.HasOne(d => d.Employee).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Ticket_Employee");

            entity.HasOne(d => d.ParentTicket).WithMany(p => p.InverseParentTicket)
                .HasForeignKey(d => d.ParentTicketId)
                .HasConstraintName("FK_Ticket_Parent");
        });

        modelBuilder.Entity<TicketWorkflowLog>(entity =>
        {
            entity.ToTable("TicketWorkflowLog");

            entity.HasIndex(e => e.EmployeeId, "IX_TicketWorkflowLog_EmployeeId");

            entity.HasIndex(e => new { e.TicketId, e.CreatedAt, e.Id }, "IX_TicketWorkflowLog_Ticket_Current").IsDescending(false, true, true);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_TicketWorkflowLog_CreatedAt");
            entity.Property(e => e.EmployeeName).HasMaxLength(50);
            entity.Property(e => e.FromStep).HasMaxLength(50);
            entity.Property(e => e.Note).HasMaxLength(200);
            entity.Property(e => e.ToStep).HasMaxLength(50);

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketWorkflowLogs)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TicketWorkflowLog_Ticket");
        });

        modelBuilder.Entity<Workflow>(entity =>
        {
            entity.ToTable("Workflow");

            entity.HasIndex(e => e.WorkflowCode, "UQ_Workflow_Code").IsUnique();

            entity.HasIndex(e => e.TicketType, "UX_Workflow_ActiveTicketType")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Workflow_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Workflow_IsActive");
            entity.Property(e => e.WorkflowCode).HasMaxLength(20);
            entity.Property(e => e.WorkflowName).HasMaxLength(50);
        });

        modelBuilder.Entity<WorkflowStep>(entity =>
        {
            entity.ToTable("WorkflowStep");

            entity.HasIndex(e => new { e.WorkflowId, e.StepCode }, "UQ_WorkflowStep_Workflow_StepCode").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.StepCode).HasMaxLength(50);

            entity.HasOne(d => d.Workflow).WithMany(p => p.WorkflowSteps)
                .HasForeignKey(d => d.WorkflowId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WorkflowStep_Workflow");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
