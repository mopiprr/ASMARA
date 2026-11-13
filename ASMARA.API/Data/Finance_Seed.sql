-- =====================================================================
-- ASMARA Finance Module Initial Data Seeder (DepartmentBudget & BudgetLog)
-- =====================================================================
USE AsmaraDB;
GO

SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    DECLARE @FiscalYear INT = YEAR(GETUTCDATE());
    DECLARE @AdminEmployeeId UNIQUEIDENTIFIER;

    -- 1. Ambil salah satu karyawan (Finance/Admin/pertama) sebagai pencatat log awal
    SELECT TOP 1 @AdminEmployeeId = Id 
    FROM dbo.Employee 
    WHERE IsActive = 1
    ORDER BY EmployeeNumber ASC;

    -- Fallback jika belum ada employee
    IF @AdminEmployeeId IS NULL
    BEGIN
        SELECT TOP 1 @AdminEmployeeId = Id FROM dbo.Employee;
    END

    IF @AdminEmployeeId IS NULL
    BEGIN
        SET @AdminEmployeeId = '00000000-0000-0000-0000-000000000000';
    END

    PRINT 'Memulai seeding DepartmentBudget untuk tahun fiskal: ' + CAST(@FiscalYear AS NVARCHAR(10));

    -- 2. Insert DepartmentBudget untuk setiap Department aktif yang belum memiliki budget di tahun berjalan
    --    Alokasi default: Rp 500.000.000 (Lima Ratus Juta Rupiah)
    INSERT INTO dbo.DepartmentBudget (
        Id,
        DepartmentId,
        FiscalYear,
        AllocatedBudget,
        SpentBudget,
        CommittedBudget,
        CreatedAt,
        UpdatedAt
    )
    SELECT 
        NEWID(),
        d.Id,
        @FiscalYear,
        500000000.00, -- AllocatedBudget
        0.00,         -- SpentBudget
        0.00,         -- CommittedBudget
        SYSUTCDATETIME(),
        NULL
    FROM dbo.Department d
    WHERE d.IsActive = 1
      AND NOT EXISTS (
          SELECT 1 
          FROM dbo.DepartmentBudget db 
          WHERE db.DepartmentId = d.Id AND db.FiscalYear = @FiscalYear
      );

    PRINT 'DepartmentBudget berhasil disinkronkan.';

    -- 3. Insert entri BudgetLog awal (Initial Allocation / AllocationIncrease = 4)
    --    Untuk setiap DepartmentBudget di tahun berjalan yang belum memiliki log
    ;WITH NewBudgets AS (
        SELECT 
            db.Id AS DepartmentBudgetId,
            db.AllocatedBudget,
            d.DepartmentName,
            ROW_NUMBER() OVER (ORDER BY d.DepartmentName ASC) AS RowNum
        FROM dbo.DepartmentBudget db
        INNER JOIN dbo.Department d ON db.DepartmentId = d.Id
        WHERE db.FiscalYear = @FiscalYear
          AND NOT EXISTS (
              SELECT 1 
              FROM dbo.BudgetLog bl 
              WHERE bl.DepartmentBudgetId = db.Id
          )
    )
    INSERT INTO dbo.BudgetLog (
        Id,
        DepartmentBudgetId,
        TicketId,
        RelatedDepartmentId,
        LogNumber,
        ChangeType,
        Amount,
        PreviousAllocated,
        NewAllocated,
        PreviousRemaining,
        NewRemaining,
        Description,
        CreatedByEmployeeId,
        CreatedAt
    )
    SELECT 
        NEWID(),
        nb.DepartmentBudgetId,
        NULL,                                                                                          -- Tanpa tiket (alokasi awal sistem)
        NULL,                                                                                          -- Bukan transfer antar departemen
        CONCAT('LOG-', FORMAT(GETUTCDATE(), 'yyyyMM'), '-', RIGHT('0000' + CAST(nb.RowNum AS VARCHAR(4)), 4)),
        4,                                                                                             -- 4 = BudgetChangeTypeEnum.AllocationIncrease
        nb.AllocatedBudget,                                                                            -- Amount
        0.00,                                                                                          -- PreviousAllocated
        nb.AllocatedBudget,                                                                            -- NewAllocated
        0.00,                                                                                          -- PreviousRemaining
        nb.AllocatedBudget,                                                                            -- NewRemaining
        CONCAT('Initial budget allocation for ', nb.DepartmentName, ' (Fiscal Year ', @FiscalYear, ')'),
        @AdminEmployeeId,
        SYSUTCDATETIME()
    FROM NewBudgets nb;

    COMMIT TRANSACTION;
    PRINT 'SUCCESS: Data awal DepartmentBudget dan BudgetLog berhasil dibuat!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'ERROR: Terjadi kesalahan saat melakukan seeding!';
    THROW;
END CATCH;
GO
