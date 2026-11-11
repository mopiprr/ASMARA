-- ASMARA Finance Module DDL
-- Tables: DepartmentBudget & BudgetLog

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DepartmentBudget')
BEGIN
    CREATE TABLE dbo.DepartmentBudget (
        Id UNIQUEIDENTIFIER CONSTRAINT DF_DepartmentBudget_Id DEFAULT (newsequentialid()) NOT NULL,
        DepartmentId UNIQUEIDENTIFIER NOT NULL,
        FiscalYear INT NOT NULL,
        AllocatedBudget DECIMAL(18,2) CONSTRAINT DF_DepartmentBudget_AllocatedBudget DEFAULT (0) NOT NULL,
        SpentBudget DECIMAL(18,2) CONSTRAINT DF_DepartmentBudget_SpentBudget DEFAULT (0) NOT NULL,
        CommittedBudget DECIMAL(18,2) CONSTRAINT DF_DepartmentBudget_CommittedBudget DEFAULT (0) NOT NULL,
        CreatedAt DATETIME2(7) CONSTRAINT DF_DepartmentBudget_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        UpdatedAt DATETIME2(7) NULL,
        CONSTRAINT PK_DepartmentBudget PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_DepartmentBudget_Department FOREIGN KEY (DepartmentId) REFERENCES dbo.Department(Id),
        CONSTRAINT UQ_DepartmentBudget_Dept_Year UNIQUE (DepartmentId, FiscalYear)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BudgetLog')
BEGIN
    CREATE TABLE dbo.BudgetLog (
        Id UNIQUEIDENTIFIER CONSTRAINT DF_BudgetLog_Id DEFAULT (newsequentialid()) NOT NULL,
        DepartmentBudgetId UNIQUEIDENTIFIER NOT NULL,
        TicketId UNIQUEIDENTIFIER NULL,
        RelatedDepartmentId UNIQUEIDENTIFIER NULL,
        LogNumber NVARCHAR(30) NOT NULL,
        ChangeType TINYINT NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        PreviousAllocated DECIMAL(18,2) NOT NULL,
        NewAllocated DECIMAL(18,2) NOT NULL,
        PreviousRemaining DECIMAL(18,2) NOT NULL,
        NewRemaining DECIMAL(18,2) NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        CreatedByEmployeeId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2(7) CONSTRAINT DF_BudgetLog_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CONSTRAINT PK_BudgetLog PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_BudgetLog_DepartmentBudget FOREIGN KEY (DepartmentBudgetId) REFERENCES dbo.DepartmentBudget(Id),
        CONSTRAINT FK_BudgetLog_Ticket FOREIGN KEY (TicketId) REFERENCES dbo.Ticket(Id),
        CONSTRAINT FK_BudgetLog_RelatedDepartment FOREIGN KEY (RelatedDepartmentId) REFERENCES dbo.Department(Id),
        CONSTRAINT FK_BudgetLog_Employee FOREIGN KEY (CreatedByEmployeeId) REFERENCES dbo.Employee(Id),
        CONSTRAINT UQ_BudgetLog_LogNumber UNIQUE (LogNumber)
    );
END
