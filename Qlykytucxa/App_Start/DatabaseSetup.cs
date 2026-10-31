using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Qlykytucxa.Models;

namespace Qlykytucxa
{
    // Tao cac bang mo rong (neu chua co) va chuyen du lieu JSON cu trong App_Data vao SQL.
    // Chay mot lan o request dau tien; neu loi (VD: SQL chua bat) se thu lai o request sau.
    public static class DatabaseSetup
    {
        private static readonly object _lock = new object();
        private static bool _initialized;

        public const string SchemaSql = @"
IF OBJECT_ID(N'dbo.Announcements', N'U') IS NULL
CREATE TABLE dbo.Announcements (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Announcements PRIMARY KEY,
    Title       NVARCHAR(300)  NOT NULL,
    Category    NVARCHAR(100)  NOT NULL,
    Content     NVARCHAR(MAX)  NOT NULL,
    Priority    NVARCHAR(50)   NULL,
    CreatedAt   DATETIME2      NOT NULL,
    AuthorName  NVARCHAR(200)  NULL,
    IsActive    BIT            NOT NULL
);

IF OBJECT_ID(N'dbo.IncidentRequests', N'U') IS NULL
CREATE TABLE dbo.IncidentRequests (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IncidentRequests PRIMARY KEY,
    RoomId       INT            NOT NULL CONSTRAINT FK_IncidentRequests_Rooms REFERENCES dbo.Rooms(RoomId),
    RoomNumber   NVARCHAR(20)   NULL,
    Title        NVARCHAR(300)  NOT NULL,
    Category     NVARCHAR(50)   NOT NULL,
    Description  NVARCHAR(MAX)  NOT NULL,
    ReporterName NVARCHAR(100)  NOT NULL,
    CreatedAt    DATETIME2      NOT NULL,
    AssignedTo   NVARCHAR(100)  NULL,
    Status       NVARCHAR(30)   NULL,
    Cost         DECIMAL(18,0)  NOT NULL CONSTRAINT CK_IncidentRequests_Cost CHECK (Cost >= 0),
    ResolvedAt   DATETIME2      NULL
);

IF OBJECT_ID(N'dbo.CashFlowTransactions', N'U') IS NULL
CREATE TABLE dbo.CashFlowTransactions (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CashFlowTransactions PRIMARY KEY,
    TransactionDate DATETIME2      NOT NULL,
    Type            NVARCHAR(10)   NOT NULL CONSTRAINT CK_CashFlowTransactions_Type CHECK (Type IN (N'Thu', N'Chi')),
    Category        NVARCHAR(100)  NOT NULL,
    Amount          DECIMAL(18,0)  NOT NULL CONSTRAINT CK_CashFlowTransactions_Amount CHECK (Amount > 0),
    RelatedPerson   NVARCHAR(200)  NULL,
    RoomNumber      NVARCHAR(50)   NULL,
    Description     NVARCHAR(MAX)  NULL,
    PaymentMethod   NVARCHAR(50)   NULL
);

IF OBJECT_ID(N'dbo.RoomFeePayments', N'U') IS NULL
CREATE TABLE dbo.RoomFeePayments (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RoomFeePayments PRIMARY KEY,
    RegistrationId  INT            NOT NULL CONSTRAINT FK_RoomFeePayments_RoomRegistrations REFERENCES dbo.RoomRegistrations(RegistrationId),
    StudentId       INT            NOT NULL CONSTRAINT FK_RoomFeePayments_Students REFERENCES dbo.Students(StudentId),
    StudentCode     NVARCHAR(20)   NULL,
    StudentName     NVARCHAR(100)  NULL,
    RoomId          INT            NOT NULL CONSTRAINT FK_RoomFeePayments_Rooms REFERENCES dbo.Rooms(RoomId),
    RoomNumber      NVARCHAR(20)   NULL,
    FeeMonth        INT            NOT NULL CONSTRAINT CK_RoomFeePayments_FeeMonth CHECK (FeeMonth BETWEEN 1 AND 12),
    FeeYear         INT            NOT NULL,
    Amount          DECIMAL(18,0)  NOT NULL,
    SemesterPeriod  NVARCHAR(50)   NULL,
    DueDate         DATETIME2      NOT NULL,
    PaidDate        DATETIME2      NULL,
    PaymentMethod   NVARCHAR(50)   NULL,
    IsPaid          BIT            NOT NULL,
    Note            NVARCHAR(500)  NULL,
    -- Moi sinh vien chi bi tinh tien phong 1 lan / thang (ke ca thang chuyen phong)
    CONSTRAINT UQ_RoomFeePayments_Student_Month UNIQUE (StudentId, FeeYear, FeeMonth)
);";

        // Nang cap cho chuc nang tai chinh (dat coc, bang gia, huy phieu, nguoi thu).
        // Moi lenh chay rieng (cot moi them o lenh truoc moi dung duoc o lenh sau) va deu chay lai duoc nhieu lan.
        public static readonly string[] FinanceUpgradeSql =
        {
@"IF OBJECT_ID(N'dbo.Deposits', N'U') IS NULL
CREATE TABLE dbo.Deposits (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Deposits PRIMARY KEY,
    RegistrationId  INT            NOT NULL CONSTRAINT FK_Deposits_RoomRegistrations REFERENCES dbo.RoomRegistrations(RegistrationId),
    StudentId       INT            NOT NULL CONSTRAINT FK_Deposits_Students REFERENCES dbo.Students(StudentId),
    StudentCode     NVARCHAR(20)   NULL,
    StudentName     NVARCHAR(100)  NULL,
    RoomNumber      NVARCHAR(20)   NULL,
    Amount          DECIMAL(18,0)  NOT NULL CONSTRAINT CK_Deposits_Amount CHECK (Amount > 0),
    CollectedDate   DATETIME2      NOT NULL,
    PaymentMethod   NVARCHAR(50)   NULL,
    CollectedBy     NVARCHAR(100)  NULL,
    Status          NVARCHAR(30)   NOT NULL,
    RefundedDate    DATETIME2      NULL,
    RefundAmount    DECIMAL(18,0)  NULL,
    DeductionAmount DECIMAL(18,0)  NULL,
    DeductionReason NVARCHAR(500)  NULL,
    RefundedBy      NVARCHAR(100)  NULL,
    Note            NVARCHAR(500)  NULL,
    CONSTRAINT CK_Deposits_Refund CHECK (ISNULL(RefundAmount, 0) >= 0 AND ISNULL(DeductionAmount, 0) >= 0
                                         AND ISNULL(RefundAmount, 0) + ISNULL(DeductionAmount, 0) <= Amount)
);",
// Moi hop dong chi co 1 khoan coc con hieu luc
@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Deposits_Registration_Active')
CREATE UNIQUE INDEX UX_Deposits_Registration_Active ON dbo.Deposits(RegistrationId) WHERE Status <> N'Đã hủy';",
@"IF OBJECT_ID(N'dbo.UtilityPrices', N'U') IS NULL
CREATE TABLE dbo.UtilityPrices (
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UtilityPrices PRIMARY KEY,
    EffectiveFrom  DATE           NOT NULL CONSTRAINT UQ_UtilityPrices_EffectiveFrom UNIQUE
                                  CONSTRAINT CK_UtilityPrices_FirstDay CHECK (DAY(EffectiveFrom) = 1),
    ElectricPrice  DECIMAL(18,0)  NOT NULL CONSTRAINT CK_UtilityPrices_Electric CHECK (ElectricPrice > 0),
    WaterPrice     DECIMAL(18,0)  NOT NULL CONSTRAINT CK_UtilityPrices_Water CHECK (WaterPrice > 0),
    Note           NVARCHAR(200)  NULL,
    CreatedBy      NVARCHAR(100)  NULL,
    CreatedAt      DATETIME2      NOT NULL
);",
@"IF NOT EXISTS (SELECT 1 FROM dbo.UtilityPrices)
INSERT INTO dbo.UtilityPrices (EffectiveFrom, ElectricPrice, WaterPrice, Note, CreatedBy, CreatedAt)
VALUES ('2000-01-01', 3500, 15000, N'Đơn giá ban đầu', N'Hệ thống', SYSDATETIME());",
@"IF COL_LENGTH('dbo.RoomFeePayments', 'CollectedBy') IS NULL
ALTER TABLE dbo.RoomFeePayments ADD CollectedBy NVARCHAR(100) NULL;",
@"IF COL_LENGTH('dbo.CashFlowTransactions', 'CreatedBy') IS NULL
ALTER TABLE dbo.CashFlowTransactions ADD
    CreatedBy        NVARCHAR(100) NULL,
    RoomFeePaymentId INT NULL CONSTRAINT FK_CashFlowTransactions_RoomFeePayments REFERENCES dbo.RoomFeePayments(Id),
    UtilityBillId    INT NULL CONSTRAINT FK_CashFlowTransactions_UtilityBills REFERENCES dbo.UtilityBills(BillId),
    DepositId        INT NULL CONSTRAINT FK_CashFlowTransactions_Deposits REFERENCES dbo.Deposits(Id),
    IsVoided         BIT NOT NULL CONSTRAINT DF_CashFlowTransactions_IsVoided DEFAULT 0,
    VoidedAt         DATETIME2 NULL,
    VoidedBy         NVARCHAR(100) NULL,
    VoidReason       NVARCHAR(500) NULL;",
@"IF COL_LENGTH('dbo.UtilityBills', 'ElectricUnitPrice') IS NULL
ALTER TABLE dbo.UtilityBills ADD
    ElectricUnitPrice DECIMAL(18,0) NULL,
    WaterUnitPrice    DECIMAL(18,0) NULL,
    PaidDate          DATETIME2 NULL;",
// Hoa don cu: luu lai don gia da ap dung (gia mac dinh truoc day)
@"UPDATE dbo.UtilityBills SET ElectricUnitPrice = 3500, WaterUnitPrice = 15000
WHERE ElectricUnitPrice IS NULL OR WaterUnitPrice IS NULL;",
// Hoa don da thu truoc khi co so quy -> bo sung but toan, ngay thu uoc tinh = cuoi thang cua ky hoa don
@"INSERT INTO dbo.CashFlowTransactions (TransactionDate, Type, Category, Amount, RelatedPerson, RoomNumber, Description, PaymentMethod, CreatedBy, UtilityBillId, IsVoided)
SELECT ISNULL(b.PaidDate, EOMONTH(DATEFROMPARTS(b.BillingYear, b.BillingMonth, 1))), N'Thu', N'Tiền điện nước', b.TotalAmount,
       N'Phòng ' + r.RoomNumber, r.RoomNumber,
       N'Thu tiền điện nước tháng ' + CAST(b.BillingMonth AS NVARCHAR(2)) + N'/' + CAST(b.BillingYear AS NVARCHAR(4))
         + N' phòng ' + r.RoomNumber + N' (bổ sung tự động, ngày thu ước tính)',
       NULL, N'Hệ thống', b.BillId, 0
FROM dbo.UtilityBills b JOIN dbo.Rooms r ON r.RoomId = b.RoomId
WHERE b.IsPaid = 1 AND b.TotalAmount > 0
  AND NOT EXISTS (SELECT 1 FROM dbo.CashFlowTransactions t WHERE t.UtilityBillId = b.BillId);"
        };

        public static void EnsureInitialized(string appDataPath)
        {
            if (_initialized) return;
            lock (_lock)
            {
                if (_initialized) return;

                using (var ext = new DormExtContext())
                {
                    ext.Database.ExecuteSqlCommand(SchemaSql);
                    foreach (string sql in FinanceUpgradeSql)
                    {
                        ext.Database.ExecuteSqlCommand(sql);
                    }
                }
                ImportJsonData(appDataPath);

                using (var db = new MiniDormDBEntities())
                {
                    // Form sua hop dong cu luu trang thai khong dau -> chuan hoa ve hang so co dau
                    db.Database.ExecuteSqlCommand("UPDATE dbo.RoomRegistrations SET Status = {0} WHERE Status = N'Dang o'", DormConstants.StatusDangO);
                    db.Database.ExecuteSqlCommand("UPDATE dbo.RoomRegistrations SET Status = {0} WHERE Status = N'Da tra phong'", DormConstants.StatusDaTraPhong);
                    Services.OccupancyService.Recalculate(db);
                }

                _initialized = true;
            }
        }

        #region Import du lieu JSON cu
        private static void ImportJsonData(string appDataPath)
        {
            using (var db = new MiniDormDBEntities())
            using (var ext = new DormExtContext())
            {
                ImportFile<Announcement>(appDataPath, "announcements.json", list =>
                {
                    if (ext.Announcements.Any()) return;
                    foreach (var a in list) ext.Announcements.Add(a);
                    ext.SaveChanges();
                });

                ImportFile<IncidentRequest>(appDataPath, "incidents.json", list =>
                {
                    if (ext.IncidentRequests.Any()) return;
                    var roomIds = new HashSet<int>(db.Rooms.Select(r => r.RoomId));
                    foreach (var i in list.Where(i => roomIds.Contains(i.RoomId)))
                    {
                        if (string.IsNullOrEmpty(i.Status)) i.Status = DormConstants.IncidentMoiTiepNhan;
                        ext.IncidentRequests.Add(i);
                    }
                    ext.SaveChanges();
                });

                ImportFile<CashFlowTransaction>(appDataPath, "cashflow.json", list =>
                {
                    if (ext.CashFlowTransactions.Any()) return;
                    foreach (var t in list.Where(t => t.Amount > 0).OrderBy(t => t.TransactionDate))
                    {
                        ext.CashFlowTransactions.Add(t);
                    }
                    ext.SaveChanges();
                });

                ImportFile<RoomFeePayment>(appDataPath, "room_fees.json", list =>
                {
                    if (ext.RoomFeePayments.Any()) return;
                    var regIds = new HashSet<int>(db.RoomRegistrations.Select(r => r.RegistrationId));
                    var seen = new HashSet<string>();
                    // Ban ghi cu khong co thang: suy ra tu han nop (= ngay vao o + 15 ngay)
                    foreach (var f in list.Where(f => regIds.Contains(f.RegistrationId)).OrderByDescending(f => f.IsPaid))
                    {
                        var month = f.DueDate.AddDays(-15);
                        f.FeeMonth = month.Month;
                        f.FeeYear = month.Year;
                        if (!seen.Add(f.StudentId + "-" + f.FeeYear + "-" + f.FeeMonth)) continue;
                        f.SemesterPeriod = $"Tháng {f.FeeMonth:D2}/{f.FeeYear}";
                        ext.RoomFeePayments.Add(f);
                    }
                    ext.SaveChanges();
                });
            }
        }

        // Doc file JSON, import, roi doi ten file thanh *.json.imported de khong import lai (khong xoa du lieu goc)
        private static void ImportFile<T>(string appDataPath, string fileName, Action<List<T>> import)
        {
            string path = Path.Combine(appDataPath, fileName);
            if (!File.Exists(path)) return;

            var list = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path)) ?? new List<T>();
            import(list);

            string target = path + ".imported";
            if (File.Exists(target)) target = path + "." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".imported";
            File.Move(path, target);
        }
        #endregion
    }
}
