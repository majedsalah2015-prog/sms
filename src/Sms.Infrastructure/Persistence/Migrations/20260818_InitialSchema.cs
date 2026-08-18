using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class InitialSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.EnsureSchema(
                name: "ppl");

            migrationBuilder.EnsureSchema(
                name: "svc");

            migrationBuilder.EnsureSchema(
                name: "rpt");

            migrationBuilder.EnsureSchema(
                name: "msg");

            migrationBuilder.EnsureSchema(
                name: "aud");

            migrationBuilder.EnsureSchema(
                name: "doc");

            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.EnsureSchema(
                name: "fin");

            migrationBuilder.EnsureSchema(
                name: "sec");

            migrationBuilder.EnsureSchema(
                name: "wf");

            migrationBuilder.CreateTable(
                name: "AcademicYear",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LabelEn = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LabelAr = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    HijriLabel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ClosingEndsOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYear", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActivityType",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionCampaign",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    GradeYearProfileId = table.Column<int>(type: "int", nullable: false),
                    OpenDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CloseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiresAssessment = table.Column<bool>(type: "bit", nullable: false),
                    ApplicationFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionCampaign", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgedReceivablesSnapshot",
                schema: "rpt",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayerId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    GradeYearProfileId = table.Column<int>(type: "int", nullable: true),
                    Current = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Days1To30 = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Days31To60 = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Days61To90 = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Over90 = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AsOfUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgedReceivablesSnapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Announcement",
                schema: "msg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BodyAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AudienceScope = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReachCount = table.Column<int>(type: "int", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcement", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnomalyRule",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Severity = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnomalyRule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Application",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    ApplicationNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirstNameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FatherNameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    GrandfatherNameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FamilyNameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FirstNameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FatherNameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    GrandfatherNameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FamilyNameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Gender = table.Column<short>(type: "smallint", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NationalityLookupId = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    RegistrationDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegisteredStudentId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Application", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceDay",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CapturedByUserId = table.Column<int>(type: "int", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDay", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntry",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: true),
                    AcademicYearId = table.Column<int>(type: "int", nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    FieldName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    ActingRoleId = table.Column<int>(type: "int", nullable: true),
                    IsDelegated = table.Column<bool>(type: "bit", nullable: false),
                    Action = table.Column<short>(type: "smallint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceScreen = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupPolicy",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentClass = table.Column<short>(type: "smallint", nullable: false),
                    RetentionDailyCount = table.Column<int>(type: "int", nullable: false),
                    RetentionMonthlyCount = table.Column<int>(type: "int", nullable: false),
                    RetentionYearlyCount = table.Column<int>(type: "int", nullable: false),
                    OnPremResponsibilityAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupPolicy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BehaviorCode",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    MaxSuspensionDays = table.Column<int>(type: "int", nullable: true),
                    AppealWindowDays = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehaviorCode", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BehaviorContract",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    DisciplineCaseId = table.Column<int>(type: "int", nullable: true),
                    Terms = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ParentSignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StudentAcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PledgeAttachmentId = table.Column<int>(type: "int", nullable: true),
                    EndsOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehaviorContract", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetCounter",
                schema: "msg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<short>(type: "smallint", nullable: false),
                    PeriodKey = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    MessageCount = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetCounter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Building",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Building", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bus",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PlateNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    RequiredLicenseClass = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CafeteriaItem",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    AllergenTags = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NutritionClass = table.Column<short>(type: "smallint", nullable: false),
                    IsStaffOnly = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CafeteriaItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CertificateType",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiresPublishedResults = table.Column<bool>(type: "bit", nullable: false),
                    FeeClearanceRule = table.Column<short>(type: "smallint", nullable: false),
                    IsPortalRequestable = table.Column<bool>(type: "bit", nullable: false),
                    ValidityDays = table.Column<int>(type: "int", nullable: true),
                    NumberingSeriesCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectionCalendarSnapshot",
                schema: "rpt",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    InstallmentCount = table.Column<int>(type: "int", nullable: false),
                    ScheduledAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OverdueCount = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AsOfUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionCalendarSnapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationMatrix",
                schema: "msg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TopicCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RoutedToRoleId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationMatrix", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionEvent",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExternalBodyRef = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyAttendanceSummarySnapshot",
                schema: "rpt",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    GradeYearProfileId = table.Column<int>(type: "int", nullable: false),
                    StageId = table.Column<int>(type: "int", nullable: false),
                    ScheduledCount = table.Column<int>(type: "int", nullable: false),
                    AbsentCount = table.Column<int>(type: "int", nullable: false),
                    ExemptedCount = table.Column<int>(type: "int", nullable: false),
                    LateCount = table.Column<int>(type: "int", nullable: false),
                    PresentPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AsOfUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyAttendanceSummarySnapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Department",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HeadTeacherUserId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Department", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticsBundle",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticsBundle", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentType",
                schema: "doc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ModuleCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AllowedFormats = table.Column<short>(type: "smallint", nullable: false),
                    MaxSizeBytes = table.Column<int>(type: "int", nullable: true),
                    IsMandatoryByDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsExpiryTracked = table.Column<bool>(type: "bit", nullable: false),
                    IsRestricted = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employee",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EmployeeNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserAccountId = table.Column<int>(type: "int", nullable: true),
                    FirstNameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FatherNameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    GrandfatherNameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FamilyNameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FirstNameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FatherNameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    GrandfatherNameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FamilyNameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Gender = table.Column<short>(type: "smallint", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NationalityLookupId = table.Column<int>(type: "int", nullable: false),
                    PrimaryIdTypeLookupId = table.Column<int>(type: "int", nullable: true),
                    PrimaryIdNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PrimaryIdExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employee", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExamRound",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamRound", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExamType",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsScheduled = table.Column<bool>(type: "bit", nullable: false),
                    IsMakeupEligible = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExposureNotice",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    DiseaseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExposureFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExposureTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExposureNotice", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeeCategory",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(6,4)", nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsRefundable = table.Column<bool>(type: "bit", nullable: false),
                    IsServiceLinked = table.Column<bool>(type: "bit", nullable: false),
                    GlExportCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GateEvent",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<short>(type: "smallint", nullable: false),
                    EventTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PickupPersonName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsAuthorizedPickupOverride = table.Column<bool>(type: "bit", nullable: false),
                    ReleasedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlAccountMapping",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AccountNameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountNameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlAccountMapping", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlExportBatch",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PeriodFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedByUserId = table.Column<int>(type: "int", nullable: false),
                    TotalDebit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCredit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SourceDocumentCount = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlExportBatch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GradingScale",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    StageId = table.Column<int>(type: "int", nullable: false),
                    CurriculumLookupValueId = table.Column<int>(type: "int", nullable: true),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingScale", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatch",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CommittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RolledBackAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrityCheckpoint",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FirstEntryId = table.Column<long>(type: "bigint", nullable: true),
                    LastEntryId = table.Column<long>(type: "bigint", nullable: true),
                    EntryCount = table.Column<int>(type: "int", nullable: false),
                    EntriesHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PreviousChainHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ChainHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrityCheckpoint", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrityVerificationRun",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IntegrityCheckpointId = table.Column<long>(type: "bigint", nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: false),
                    RanAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrityVerificationRun", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobDefinition",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KeepApartPair",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentAId = table.Column<int>(type: "int", nullable: false),
                    StudentBId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeepApartPair", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LayoutTemplate",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutTemplate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeavePass",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReturnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeavePass", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegalHold",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataClass = table.Column<short>(type: "smallint", nullable: false),
                    SubjectReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PlacedByUserId = table.Column<int>(type: "int", nullable: false),
                    PlacedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalHold", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LicenseState",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Tier = table.Column<short>(type: "smallint", nullable: false),
                    StudentCountCap = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GraceDays = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LookupCategory",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tier = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LookupCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceWindow",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MessageAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MessageEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsEmergency = table.Column<bool>(type: "bit", nullable: false),
                    IsReadOnlyMode = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceWindow", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalFile",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    BloodType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    EmergencyBannerAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EmergencyBannerEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IntakeVerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReconfirmedAcademicYearId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalFile", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberPolicy",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MemberKind = table.Column<short>(type: "smallint", nullable: false),
                    StageId = table.Column<int>(type: "int", nullable: true),
                    MaxConcurrentLoans = table.Column<int>(type: "int", nullable: false),
                    LoanDays = table.Column<int>(type: "int", nullable: false),
                    MaxRenewals = table.Column<int>(type: "int", nullable: false),
                    MaxReservations = table.Column<int>(type: "int", nullable: false),
                    FinesEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FinePerDay = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    FineCap = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    LostAfterOverdueDays = table.Column<int>(type: "int", nullable: true),
                    HoldWindowDays = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPolicy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Menu",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberingSeries",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FormatTemplate = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResetPolicy = table.Column<short>(type: "smallint", nullable: false),
                    GapPolicy = table.Column<short>(type: "smallint", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberingSeries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfficialLetter",
                schema: "msg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    LetterNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RecipientUserId = table.Column<int>(type: "int", nullable: false),
                    BodySnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiresAcknowledgment = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialLetter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrgUnit",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ParentOrgUnitId = table.Column<int>(type: "int", nullable: true),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgUnit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parent",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ParentFileNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserAccountId = table.Column<int>(type: "int", nullable: true),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrimaryMobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccupationEmployer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParentMeeting",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    DisciplineCaseId = table.Column<int>(type: "int", nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HeldAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentMeeting", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payer",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permission",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ScreenCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Action = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PointLedger",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<short>(type: "smallint", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointLedger", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceList",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceList", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromotionCriteria",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    GradeYearProfileId = table.Column<int>(type: "int", nullable: false),
                    OverallPassMark = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MaxFailedSubjectsForPromotion = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionCriteria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Provider",
                schema: "msg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<short>(type: "smallint", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provider", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurgeExecution",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataClass = table.Column<short>(type: "smallint", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: true),
                    HorizonUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    SecondApproverUserId = table.Column<int>(type: "int", nullable: true),
                    CertificateNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurgeExecution", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportDefinition",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OwningModuleCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SupportedFormats = table.Column<short>(type: "smallint", nullable: false),
                    Sensitivity = table.Column<short>(type: "smallint", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    RequiredParameterKeysCsv = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RestoreCase",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<short>(type: "smallint", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: true),
                    PointInTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    GapAnalysisNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CertificateNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestoreCase", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RequireTwoFactor = table.Column<bool>(type: "bit", nullable: false),
                    EnforceSingleSession = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolGroup",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScreeningCampaign",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    GradeYearProfileId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreeningCampaign", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Signatory",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DocumentClassCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signatory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SnapshotEvent",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TriggerOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    TakenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnapshotEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpendControl",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    DailyLimit = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    BlockedCategories = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AllergyHardBlock = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpendControl", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stage",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SequenceOrder = table.Column<int>(type: "int", nullable: false),
                    DefaultGenderPolicy = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StocktakeSession",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StocktakeSession", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreAccountChargePolicy",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<short>(type: "smallint", nullable: false),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false),
                    CapPerSale = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreAccountChargePolicy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreReturnPolicy",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<short>(type: "smallint", nullable: false),
                    WindowDays = table.Column<int>(type: "int", nullable: false),
                    SealedOnly = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreReturnPolicy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Student",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserAccountId = table.Column<int>(type: "int", nullable: true),
                    FirstNameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FatherNameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    GrandfatherNameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FamilyNameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FirstNameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FatherNameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    GrandfatherNameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FamilyNameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Gender = table.Column<short>(type: "smallint", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NationalityLookupId = table.Column<int>(type: "int", nullable: false),
                    PrimaryIdTypeLookupId = table.Column<int>(type: "int", nullable: true),
                    PrimaryIdNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PrimaryIdExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PhotoAttachmentId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Student", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionRule",
                schema: "msg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EventCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Channel = table.Column<short>(type: "smallint", nullable: false),
                    Timing = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsStatutory = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionRule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Template",
                schema: "msg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EventCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Channel = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Template", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TermResult",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    CurriculumOfferingId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    ScorePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ScaleBandId = table.Column<int>(type: "int", nullable: true),
                    CalculationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermResult", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Thread",
                schema: "msg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TopicCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InitiatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RoutedToRoleId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Thread", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TillSession",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    CashierUserId = table.Column<int>(type: "int", nullable: false),
                    TillCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FloatAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SystemTotal = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CountedTotal = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    VarianceReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TillSession", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimetableShape",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    StageId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableShape", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimetableVersion",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableVersion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Title",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TitleEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Transliteration = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Author = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Isbn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DeweyClass = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SubjectTags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MinStageSequence = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Title", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransportStaff",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    ContractorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LicenseNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    LicenseClass = table.Column<short>(type: "smallint", nullable: true),
                    LicenseExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportStaff", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAccount",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountType = table.Column<short>(type: "smallint", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PasswordChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false),
                    LockedOutUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccount", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationCampaign",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VaccineCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DoseNumber = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationCampaign", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationScheduleEntry",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    VaccineCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DoseNumber = table.Column<int>(type: "int", nullable: false),
                    DueAgeMonths = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationScheduleEntry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Wallet",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    HolderKind = table.Column<short>(type: "smallint", nullable: false),
                    HolderId = table.Column<int>(type: "int", nullable: false),
                    OverdraftAllowance = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WidgetDefinition",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OwningModuleCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiredPermissionId = table.Column<int>(type: "int", nullable: false),
                    RefreshClass = table.Column<short>(type: "smallint", nullable: false),
                    DrillTargetCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsPortalEligible = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WidgetDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinition",
                schema: "wf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EntityTypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearResult",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    Gpa = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    FailedSubjectCount = table.Column<int>(type: "int", nullable: false),
                    PromotionOutcome = table.Column<short>(type: "smallint", nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearResult", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarDay",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DayType = table.Column<short>(type: "smallint", nullable: false),
                    Audience = table.Column<short>(type: "smallint", nullable: false),
                    Source = table.Column<short>(type: "smallint", nullable: false),
                    IsProvisional = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarDay", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarDay_AcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "core",
                        principalTable: "AcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEvent",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<short>(type: "smallint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Audience = table.Column<short>(type: "smallint", nullable: false),
                    IsPortalVisible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEvent_AcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "core",
                        principalTable: "AcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalendarVersion",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarVersion_AcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "core",
                        principalTable: "AcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolloverBatch",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    SourceAcademicYearId = table.Column<int>(type: "int", nullable: false),
                    TargetAcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    PromotionsApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PromotionsApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    TimetableDeferredReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CarryForwardPostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CarryForwardTotal = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolloverBatch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolloverBatch_AcademicYear_SourceAcademicYearId",
                        column: x => x.SourceAcademicYearId,
                        principalSchema: "core",
                        principalTable: "AcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolloverBatch_AcademicYear_TargetAcademicYearId",
                        column: x => x.TargetAcademicYearId,
                        principalSchema: "core",
                        principalTable: "AcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Semester",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Semester", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Semester_AcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "core",
                        principalTable: "AcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Program",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    ActivityTypeId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SupervisorEmployeeId = table.Column<int>(type: "int", nullable: false),
                    VenueRoomId = table.Column<int>(type: "int", nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    EligibilityGenderPolicy = table.Column<short>(type: "smallint", nullable: true),
                    EligibilityStageId = table.Column<int>(type: "int", nullable: true),
                    CostAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    FeeCategoryId = table.Column<int>(type: "int", nullable: true),
                    RequiresConsent = table.Column<bool>(type: "bit", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Program", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Program_ActivityType_ActivityTypeId",
                        column: x => x.ActivityTypeId,
                        principalSchema: "ppl",
                        principalTable: "ActivityType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnomalyHit",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnomalyRuleId = table.Column<int>(type: "int", nullable: false),
                    AuditEntryId = table.Column<long>(type: "bigint", nullable: false),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    DispositionNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DispositionedByUserId = table.Column<int>(type: "int", nullable: true),
                    DispositionedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnomalyHit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnomalyHit_AnomalyRule_AnomalyRuleId",
                        column: x => x.AnomalyRuleId,
                        principalSchema: "aud",
                        principalTable: "AnomalyRule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationAssessment",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AssessedByUserId = table.Column<int>(type: "int", nullable: false),
                    AssessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationAssessment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationAssessment_Application_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "ppl",
                        principalTable: "Application",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WaitingListEntry",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    GradeYearProfileId = table.Column<int>(type: "int", nullable: false),
                    OrderRank = table.Column<int>(type: "int", nullable: false),
                    OfferedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OfferExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsOfferAccepted = table.Column<bool>(type: "bit", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitingListEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaitingListEntry_Application_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "ppl",
                        principalTable: "Application",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Justification",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AttendanceDayId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewState = table.Column<short>(type: "smallint", nullable: false),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DocumentAttachmentId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Justification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Justification_AttendanceDay_AttendanceDayId",
                        column: x => x.AttendanceDayId,
                        principalSchema: "core",
                        principalTable: "AttendanceDay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BackupRun",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BackupPolicyId = table.Column<int>(type: "int", nullable: false),
                    DatabaseIncluded = table.Column<bool>(type: "bit", nullable: false),
                    AttachmentStoreIncluded = table.Column<bool>(type: "bit", nullable: false),
                    ConfigurationIncluded = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    RanAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRun", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupRun_BackupPolicy_BackupPolicyId",
                        column: x => x.BackupPolicyId,
                        principalSchema: "ops",
                        principalTable: "BackupPolicy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsequenceType",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BehaviorCodeId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SeverityRank = table.Column<int>(type: "int", nullable: false),
                    IsSuspensionClass = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsequenceType", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsequenceType_BehaviorCode_BehaviorCodeId",
                        column: x => x.BehaviorCodeId,
                        principalSchema: "svc",
                        principalTable: "BehaviorCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeritType",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BehaviorCodeId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    MaxPointsPerAward = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeritType", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeritType_BehaviorCode_BehaviorCodeId",
                        column: x => x.BehaviorCodeId,
                        principalSchema: "svc",
                        principalTable: "BehaviorCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ViolationType",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BehaviorCodeId = table.Column<int>(type: "int", nullable: false),
                    ArticleRef = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViolationType", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ViolationType_BehaviorCode_BehaviorCodeId",
                        column: x => x.BehaviorCodeId,
                        principalSchema: "svc",
                        principalTable: "BehaviorCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Floor",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BuildingId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SequenceOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Floor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Floor_Building_BuildingId",
                        column: x => x.BuildingId,
                        principalSchema: "core",
                        principalTable: "Building",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusDocument",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BusId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttachmentId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusDocument", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusDocument_Bus_BusId",
                        column: x => x.BusId,
                        principalSchema: "svc",
                        principalTable: "Bus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovement",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    CafeteriaItemId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovement_CafeteriaItem_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalSchema: "svc",
                        principalTable: "CafeteriaItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CertificateRequest",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    CertificateTypeId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClearanceOverridden = table.Column<bool>(type: "bit", nullable: false),
                    ClearanceOverrideReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateRequest_CertificateType_CertificateTypeId",
                        column: x => x.CertificateTypeId,
                        principalSchema: "ppl",
                        principalTable: "CertificateType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subject",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subject", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subject_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "core",
                        principalTable: "Department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attachment",
                schema: "doc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    OwningEntityType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    OwningEntityId = table.Column<long>(type: "bigint", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NotesAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NotesEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    ExpiryDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachment_DocumentType_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "doc",
                        principalTable: "DocumentType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contract",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SalaryBasic = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    SalaryAllowances = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contract", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contract_Employee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "ppl",
                        principalTable: "Employee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Qualification",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    InstitutionName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DateAwarded = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsTeachingRelevant = table.Column<bool>(type: "bit", nullable: false),
                    DocumentAttachmentId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Qualification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Qualification_Employee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "ppl",
                        principalTable: "Employee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherProfile",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    MaxWeeklyPeriods = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherProfile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherProfile_Employee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "ppl",
                        principalTable: "Employee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bundle",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    GradeYearProfileId = table.Column<int>(type: "int", nullable: false),
                    FeeCategoryId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChargeMode = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bundle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bundle_FeeCategory_FeeCategoryId",
                        column: x => x.FeeCategoryId,
                        principalSchema: "ppl",
                        principalTable: "FeeCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscountType",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Basis = table.Column<short>(type: "smallint", nullable: false),
                    ComputationStage = table.Column<short>(type: "smallint", nullable: false),
                    FeeCategoryId = table.Column<int>(type: "int", nullable: true),
                    CapAmountPerStudent = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsStackable = table.Column<bool>(type: "bit", nullable: false),
                    MaxCombinedPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EligibilityMode = table.Column<short>(type: "smallint", nullable: false),
                    RenewalMode = table.Column<short>(type: "smallint", nullable: false),
                    RequiresHardshipDocumentation = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountType", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscountType_FeeCategory_FeeCategoryId",
                        column: x => x.FeeCategoryId,
                        principalSchema: "ppl",
                        principalTable: "FeeCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeeStructureLine",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    GradeYearProfileId = table.Column<int>(type: "int", nullable: false),
                    FeeCategoryId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeStructureLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeStructureLine_FeeCategory_FeeCategoryId",
                        column: x => x.FeeCategoryId,
                        principalSchema: "ppl",
                        principalTable: "FeeCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MealPlan",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FeeCategoryId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DailyValueCap = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    UnredeemedDayPolicy = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlan_FeeCategory_FeeCategoryId",
                        column: x => x.FeeCategoryId,
                        principalSchema: "ppl",
                        principalTable: "FeeCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanTemplate",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FeeCategoryId = table.Column<int>(type: "int", nullable: true),
                    DownPaymentPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    GraceDays = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanTemplate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanTemplate_FeeCategory_FeeCategoryId",
                        column: x => x.FeeCategoryId,
                        principalSchema: "ppl",
                        principalTable: "FeeCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoreItem",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<short>(type: "smallint", nullable: false),
                    FeeCategoryId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreItem_FeeCategory_FeeCategoryId",
                        column: x => x.FeeCategoryId,
                        principalSchema: "ppl",
                        principalTable: "FeeCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GlJournalLine",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    GlExportBatchId = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    AccountKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SourceDocumentCount = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlJournalLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlJournalLine_GlExportBatch_GlExportBatchId",
                        column: x => x.GlExportBatchId,
                        principalSchema: "fin",
                        principalTable: "GlExportBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Blueprint",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    CurriculumOfferingId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    GradingScaleId = table.Column<int>(type: "int", nullable: false),
                    RedistributeWeightOnExemption = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blueprint", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Blueprint_GradingScale_GradingScaleId",
                        column: x => x.GradingScaleId,
                        principalSchema: "core",
                        principalTable: "GradingScale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScaleBand",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    GradingScaleId = table.Column<int>(type: "int", nullable: false),
                    MinPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MaxPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    BandCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LabelAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LabelEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    GpaPoints = table.Column<decimal>(type: "decimal(4,2)", nullable: true),
                    IsPassing = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScaleBand", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScaleBand_GradingScale_GradingScaleId",
                        column: x => x.GradingScaleId,
                        principalSchema: "core",
                        principalTable: "GradingScale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobRun",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    TriggerType = table.Column<short>(type: "smallint", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRun", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobRun_JobDefinition_JobDefinitionId",
                        column: x => x.JobDefinitionId,
                        principalSchema: "ops",
                        principalTable: "JobDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LookupValue",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    LookupCategoryId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LookupValue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LookupValue_LookupCategory_LookupCategoryId",
                        column: x => x.LookupCategoryId,
                        principalSchema: "core",
                        principalTable: "LookupCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Allergy",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MedicalFileId = table.Column<int>(type: "int", nullable: false),
                    Substance = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Severity = table.Column<short>(type: "smallint", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allergy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Allergy_MedicalFile_MedicalFileId",
                        column: x => x.MedicalFileId,
                        principalSchema: "svc",
                        principalTable: "MedicalFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CarePlan",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MedicalFileId = table.Column<int>(type: "int", nullable: false),
                    ConditionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Triggers = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ResponseSteps = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    EmergencyContactsNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsLinkedToBanner = table.Column<bool>(type: "bit", nullable: false),
                    ReviewDueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarePlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarePlan_MedicalFile_MedicalFileId",
                        column: x => x.MedicalFileId,
                        principalSchema: "svc",
                        principalTable: "MedicalFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicVisit",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MedicalFileId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    VisitNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NurseUserId = table.Column<int>(type: "int", nullable: false),
                    ArrivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TriageNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TemperatureC = table.Column<decimal>(type: "decimal(4,1)", nullable: true),
                    PulseBpm = table.Column<int>(type: "int", nullable: true),
                    BloodPressure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Outcome = table.Column<short>(type: "smallint", nullable: false),
                    PickupVerifiedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PickupExceptionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicVisit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicVisit_MedicalFile_MedicalFileId",
                        column: x => x.MedicalFileId,
                        principalSchema: "svc",
                        principalTable: "MedicalFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InfectiousCase",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MedicalFileId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    DiseaseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AbsenceFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AbsenceTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfectiousCase", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InfectiousCase_MedicalFile_MedicalFileId",
                        column: x => x.MedicalFileId,
                        principalSchema: "svc",
                        principalTable: "MedicalFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicalCondition",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MedicalFileId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalCondition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalCondition_MedicalFile_MedicalFileId",
                        column: x => x.MedicalFileId,
                        principalSchema: "svc",
                        principalTable: "MedicalFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicationAuthorization",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MedicalFileId = table.Column<int>(type: "int", nullable: false),
                    MedicationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DosePerAdministration = table.Column<decimal>(type: "decimal(9,3)", nullable: false),
                    DoseUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScheduleTimes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuthorizedByParentId = table.Column<int>(type: "int", nullable: false),
                    PhysicianNoteAttachmentId = table.Column<int>(type: "int", nullable: true),
                    IsControlled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationAuthorization", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicationAuthorization_MedicalFile_MedicalFileId",
                        column: x => x.MedicalFileId,
                        principalSchema: "svc",
                        principalTable: "MedicalFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuLine",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MenuId = table.Column<int>(type: "int", nullable: false),
                    CafeteriaItemId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuLine_CafeteriaItem_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalSchema: "svc",
                        principalTable: "CafeteriaItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuLine_Menu_MenuId",
                        column: x => x.MenuId,
                        principalSchema: "svc",
                        principalTable: "Menu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SeriesState",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    NumberingSeriesId = table.Column<int>(type: "int", nullable: false),
                    ResetKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastIssuedSequence = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesState", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesState_NumberingSeries_NumberingSeriesId",
                        column: x => x.NumberingSeriesId,
                        principalSchema: "core",
                        principalTable: "NumberingSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeAssignment",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    OrgUnitId = table.Column<int>(type: "int", nullable: false),
                    PositionLookupId = table.Column<int>(type: "int", nullable: false),
                    ManagerEmployeeId = table.Column<int>(type: "int", nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeAssignment_Employee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "ppl",
                        principalTable: "Employee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeAssignment_OrgUnit_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalSchema: "ppl",
                        principalTable: "OrgUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Charge",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    PayerId = table.Column<int>(type: "int", nullable: false),
                    FeeCategoryId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<short>(type: "smallint", nullable: false),
                    ChargeNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    VatRateSnapshot = table.Column<decimal>(type: "decimal(6,4)", nullable: true),
                    VatAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InvoiceUuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PreviousInvoiceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceAcademicYearId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Charge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Charge_FeeCategory_FeeCategoryId",
                        column: x => x.FeeCategoryId,
                        principalSchema: "ppl",
                        principalTable: "FeeCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charge_Payer_PayerId",
                        column: x => x.PayerId,
                        principalSchema: "ppl",
                        principalTable: "Payer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefundVoucher",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PayerId = table.Column<int>(type: "int", nullable: false),
                    VoucherNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Method = table.Column<short>(type: "smallint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundVoucher", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefundVoucher_Payer_PayerId",
                        column: x => x.PayerId,
                        principalSchema: "ppl",
                        principalTable: "Payer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatementIssue",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PayerId = table.Column<int>(type: "int", nullable: false),
                    StatementNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AsOfUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementIssue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatementIssue_Payer_PayerId",
                        column: x => x.PayerId,
                        principalSchema: "ppl",
                        principalTable: "Payer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportExecution",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ReportDefinitionId = table.Column<int>(type: "int", nullable: false),
                    ExecutedByUserId = table.Column<int>(type: "int", nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Format = table.Column<short>(type: "smallint", nullable: false),
                    WasExport = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportExecution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportExecution_ReportDefinition_ReportDefinitionId",
                        column: x => x.ReportDefinitionId,
                        principalSchema: "core",
                        principalTable: "ReportDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportSubscription",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ReportDefinitionId = table.Column<int>(type: "int", nullable: false),
                    SubscriberUserId = table.Column<int>(type: "int", nullable: false),
                    Frequency = table.Column<short>(type: "smallint", nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Format = table.Column<short>(type: "smallint", nullable: false),
                    DeliveryChannel = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSubscription", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportSubscription_ReportDefinition_ReportDefinitionId",
                        column: x => x.ReportDefinitionId,
                        principalSchema: "core",
                        principalTable: "ReportDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermission",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermission_Permission_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "sec",
                        principalTable: "Permission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermission_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "sec",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "School",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolGroupId = table.Column<int>(type: "int", nullable: true),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LicenseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MinistryCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LicenseExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AddressLine = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_School", x => x.Id);
                    table.ForeignKey(
                        name: "FK_School_SchoolGroup_SchoolGroupId",
                        column: x => x.SchoolGroupId,
                        principalSchema: "core",
                        principalTable: "SchoolGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScreeningResult",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ScreeningCampaignId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Value1 = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    Value2 = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsAbnormal = table.Column<bool>(type: "bit", nullable: false),
                    ReferralIssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FollowUpCompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreeningResult", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreeningResult_ScreeningCampaign_ScreeningCampaignId",
                        column: x => x.ScreeningCampaignId,
                        principalSchema: "svc",
                        principalTable: "ScreeningCampaign",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradeLevel",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StageId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SequenceOrder = table.Column<int>(type: "int", nullable: false),
                    PromotionTargetGradeLevelId = table.Column<int>(type: "int", nullable: true),
                    IsGraduating = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeLevel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeLevel_GradeLevel_PromotionTargetGradeLevelId",
                        column: x => x.PromotionTargetGradeLevelId,
                        principalSchema: "core",
                        principalTable: "GradeLevel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradeLevel_Stage_StageId",
                        column: x => x.StageId,
                        principalSchema: "core",
                        principalTable: "Stage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyContact",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RelationshipLookupId = table.Column<int>(type: "int", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsPickupAuthorized = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyContact", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyContact_Student_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "ppl",
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentGuardianLink",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    RelationshipLookupId = table.Column<int>(type: "int", nullable: false),
                    IsPrimaryContact = table.Column<bool>(type: "bit", nullable: false),
                    IsFinanciallyResponsible = table.Column<bool>(type: "bit", nullable: false),
                    IsPickupAuthorized = table.Column<bool>(type: "bit", nullable: false),
                    IsPortalVisible = table.Column<bool>(type: "bit", nullable: false),
                    GuardianshipDocAttachmentId = table.Column<int>(type: "int", nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentGuardianLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentGuardianLink_Student_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "ppl",
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplateVersion",
                schema: "msg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    SubjectAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubjectEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BodyAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishStatus = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateVersion_Template_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "msg",
                        principalTable: "Template",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ThreadMessage",
                schema: "msg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ThreadId = table.Column<int>(type: "int", nullable: false),
                    SenderUserId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreadMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThreadMessage_Thread_ThreadId",
                        column: x => x.ThreadId,
                        principalSchema: "msg",
                        principalTable: "Thread",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Receipt",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PayerId = table.Column<int>(type: "int", nullable: false),
                    TillSessionId = table.Column<int>(type: "int", nullable: true),
                    ReceiptNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Method = table.Column<short>(type: "smallint", nullable: false),
                    MethodRefNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Purpose = table.Column<short>(type: "smallint", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receipt_Payer_PayerId",
                        column: x => x.PayerId,
                        principalSchema: "ppl",
                        principalTable: "Payer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Receipt_TillSession_TillSessionId",
                        column: x => x.TillSessionId,
                        principalSchema: "ppl",
                        principalTable: "TillSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sale",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    HolderKind = table.Column<short>(type: "smallint", nullable: false),
                    HolderId = table.Column<int>(type: "int", nullable: false),
                    TillSessionId = table.Column<int>(type: "int", nullable: true),
                    OperatorUserId = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tender = table.Column<short>(type: "smallint", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CapturedOfflineAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sale", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sale_TillSession_TillSessionId",
                        column: x => x.TillSessionId,
                        principalSchema: "ppl",
                        principalTable: "TillSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodSlot",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TimetableShapeId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsBreak = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodSlot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodSlot_TimetableShape_TimetableShapeId",
                        column: x => x.TimetableShapeId,
                        principalSchema: "core",
                        principalTable: "TimetableShape",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Copy",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TitleId = table.Column<int>(type: "int", nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AcquiredOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShelfLocation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Copy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Copy_Title_TitleId",
                        column: x => x.TitleId,
                        principalSchema: "svc",
                        principalTable: "Title",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReadingLog",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    TitleId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingLog_Title_TitleId",
                        column: x => x.TitleId,
                        principalSchema: "svc",
                        principalTable: "Title",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Route",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    RouteNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Direction = table.Column<short>(type: "smallint", nullable: false),
                    BusId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    AttendantId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Route", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Route_Bus_BusId",
                        column: x => x.BusId,
                        principalSchema: "svc",
                        principalTable: "Bus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Route_TransportStaff_AttendantId",
                        column: x => x.AttendantId,
                        principalSchema: "svc",
                        principalTable: "TransportStaff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Route_TransportStaff_DriverId",
                        column: x => x.DriverId,
                        principalSchema: "svc",
                        principalTable: "TransportStaff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoginAttempt",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserAccountId = table.Column<int>(type: "int", nullable: true),
                    UserNameAttempted = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAttempt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoginAttempt_UserAccount_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "sec",
                        principalTable: "UserAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PasswordHistory",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserAccountId = table.Column<int>(type: "int", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordHistory_UserAccount_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "sec",
                        principalTable: "UserAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleAssignment",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserAccountId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleAssignment_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "sec",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleAssignment_UserAccount_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "sec",
                        principalTable: "UserAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TwoFactorEnrollment",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserAccountId = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<short>(type: "smallint", nullable: false),
                    SecretKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoFactorEnrollment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TwoFactorEnrollment_UserAccount_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "sec",
                        principalTable: "UserAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserSession",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserAccountId = table.Column<int>(type: "int", nullable: false),
                    SessionToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LastActivityAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSession_UserAccount_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "sec",
                        principalTable: "UserAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsentRecord",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    VaccinationCampaignId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ConsentedByParentId = table.Column<int>(type: "int", nullable: false),
                    IsGranted = table.Column<bool>(type: "bit", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttachmentId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentRecord_VaccinationCampaign_VaccinationCampaignId",
                        column: x => x.VaccinationCampaignId,
                        principalSchema: "svc",
                        principalTable: "VaccinationCampaign",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationRecord",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MedicalFileId = table.Column<int>(type: "int", nullable: false),
                    VaccineCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DoseNumber = table.Column<int>(type: "int", nullable: false),
                    GivenOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<short>(type: "smallint", nullable: false),
                    VaccinationCampaignId = table.Column<int>(type: "int", nullable: true),
                    ExternalCardAttachmentId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaccinationRecord_MedicalFile_MedicalFileId",
                        column: x => x.MedicalFileId,
                        principalSchema: "svc",
                        principalTable: "MedicalFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VaccinationRecord_VaccinationCampaign_VaccinationCampaignId",
                        column: x => x.VaccinationCampaignId,
                        principalSchema: "svc",
                        principalTable: "VaccinationCampaign",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LayoutTemplateWidget",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    LayoutTemplateId = table.Column<int>(type: "int", nullable: false),
                    WidgetDefinitionId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutTemplateWidget", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LayoutTemplateWidget_LayoutTemplate_LayoutTemplateId",
                        column: x => x.LayoutTemplateId,
                        principalSchema: "core",
                        principalTable: "LayoutTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LayoutTemplateWidget_WidgetDefinition_WidgetDefinitionId",
                        column: x => x.WidgetDefinitionId,
                        principalSchema: "core",
                        principalTable: "WidgetDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserLayout",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserAccountId = table.Column<int>(type: "int", nullable: false),
                    WidgetDefinitionId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLayout", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLayout_WidgetDefinition_WidgetDefinitionId",
                        column: x => x.WidgetDefinitionId,
                        principalSchema: "core",
                        principalTable: "WidgetDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowState",
                schema: "wf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsInitial = table.Column<bool>(type: "bit", nullable: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    IsEditableInState = table.Column<bool>(type: "bit", nullable: false),
                    IsPortalVisible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowState", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowState_WorkflowDefinition_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalSchema: "wf",
                        principalTable: "WorkflowDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Term",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SemesterId = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Term", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Term_Semester_SemesterId",
                        column: x => x.SemesterId,
                        principalSchema: "core",
                        principalTable: "Semester",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Achievement",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ProgramId = table.Column<int>(type: "int", nullable: true),
                    CompetitionEventId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AwardedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CertificateIssueId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achievement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Achievement_CompetitionEvent_CompetitionEventId",
                        column: x => x.CompetitionEventId,
                        principalSchema: "ppl",
                        principalTable: "CompetitionEvent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Achievement_Program_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "ppl",
                        principalTable: "Program",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivitySession",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ProgramId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivitySession_Program_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "ppl",
                        principalTable: "Program",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityTrip",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ProgramId = table.Column<int>(type: "int", nullable: false),
                    ItineraryText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StaffRatioRequired = table.Column<int>(type: "int", nullable: false),
                    AssignedStaffCount = table.Column<int>(type: "int", nullable: false),
                    TransportRouteId = table.Column<int>(type: "int", nullable: true),
                    TransportConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    DepartureChecklistComplete = table.Column<bool>(type: "bit", nullable: false),
                    ReturnHeadcountConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityTrip", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityTrip_Program_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "ppl",
                        principalTable: "Program",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramEnrollment",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ProgramId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ChargeId = table.Column<int>(type: "int", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WithdrawalReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramEnrollment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramEnrollment_Program_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "ppl",
                        principalTable: "Program",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BackupVerificationRun",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BackupRunId = table.Column<int>(type: "int", nullable: false),
                    DatabaseRestoreOk = table.Column<bool>(type: "bit", nullable: false),
                    RowCountSanityOk = table.Column<bool>(type: "bit", nullable: false),
                    AttachmentHashSampleOk = table.Column<bool>(type: "bit", nullable: false),
                    IntegrityCheckpointOk = table.Column<bool>(type: "bit", nullable: false),
                    CheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupVerificationRun", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupVerificationRun_BackupRun_BackupRunId",
                        column: x => x.BackupRunId,
                        principalSchema: "ops",
                        principalTable: "BackupRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LadderStep",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BehaviorCodeId = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    RepetitionCount = table.Column<int>(type: "int", nullable: false),
                    ConsequenceTypeId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LadderStep", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LadderStep_BehaviorCode_BehaviorCodeId",
                        column: x => x.BehaviorCodeId,
                        principalSchema: "svc",
                        principalTable: "BehaviorCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LadderStep_ConsequenceType_ConsequenceTypeId",
                        column: x => x.ConsequenceTypeId,
                        principalSchema: "svc",
                        principalTable: "ConsequenceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Merit",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: true),
                    MeritTypeId = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Merit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Merit_MeritType_MeritTypeId",
                        column: x => x.MeritTypeId,
                        principalSchema: "svc",
                        principalTable: "MeritType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Incident",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    IncidentNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: true),
                    ReporterUserId = table.Column<int>(type: "int", nullable: false),
                    ViolationTypeId = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    EvidenceAttachmentId = table.Column<int>(type: "int", nullable: true),
                    IsTeacherResolved = table.Column<bool>(type: "bit", nullable: false),
                    CaseId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incident", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incident_ViolationType_ViolationTypeId",
                        column: x => x.ViolationTypeId,
                        principalSchema: "svc",
                        principalTable: "ViolationType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Room",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    FloorId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoomTypeLookupId = table.Column<int>(type: "int", nullable: false),
                    StandardCapacity = table.Column<int>(type: "int", nullable: false),
                    ExamCapacity = table.Column<int>(type: "int", nullable: false),
                    WingTag = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Room", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Room_Floor_FloorId",
                        column: x => x.FloorId,
                        principalSchema: "core",
                        principalTable: "Floor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CertificateIssue",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    CertificateRequestId = table.Column<int>(type: "int", nullable: false),
                    CertificateTypeId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CertificateNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VerificationCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReprintCount = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReissuedFromCertificateIssueId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateIssue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateIssue_CertificateIssue_ReissuedFromCertificateIssueId",
                        column: x => x.ReissuedFromCertificateIssueId,
                        principalSchema: "ppl",
                        principalTable: "CertificateIssue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificateIssue_CertificateRequest_CertificateRequestId",
                        column: x => x.CertificateRequestId,
                        principalSchema: "ppl",
                        principalTable: "CertificateRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherSubjectQualification",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TeacherUserId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    StageId = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherSubjectQualification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherSubjectQualification_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "core",
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttachmentVersion",
                schema: "doc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AttachmentId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Format = table.Column<short>(type: "smallint", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StorageReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ScanStatus = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttachmentVersion_Attachment_AttachmentId",
                        column: x => x.AttachmentId,
                        principalSchema: "doc",
                        principalTable: "Attachment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherAssignment",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    TeacherProfileId = table.Column<int>(type: "int", nullable: false),
                    CurriculumOfferingId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<short>(type: "smallint", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherAssignment_TeacherProfile_TeacherProfileId",
                        column: x => x.TeacherProfileId,
                        principalSchema: "core",
                        principalTable: "TeacherProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DistributionSession",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BundleId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionSession_Bundle_BundleId",
                        column: x => x.BundleId,
                        principalSchema: "svc",
                        principalTable: "Bundle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EligibilityRule",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DiscountTypeId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    ChildOrdinal = table.Column<int>(type: "int", nullable: true),
                    Percent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EligibilityRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EligibilityRule_DiscountType_DiscountTypeId",
                        column: x => x.DiscountTypeId,
                        principalSchema: "ppl",
                        principalTable: "DiscountType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScholarshipProgram",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DiscountTypeId = table.Column<int>(type: "int", nullable: false),
                    MaxAwards = table.Column<int>(type: "int", nullable: true),
                    MaxTotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScholarshipProgram", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScholarshipProgram_DiscountType_DiscountTypeId",
                        column: x => x.DiscountTypeId,
                        principalSchema: "ppl",
                        principalTable: "DiscountType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanAssignment",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    PayerId = table.Column<int>(type: "int", nullable: false),
                    PlanTemplateId = table.Column<int>(type: "int", nullable: false),
                    FeeCategoryId = table.Column<int>(type: "int", nullable: true),
                    IsException = table.Column<bool>(type: "bit", nullable: false),
                    ExceptionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RescheduleCount = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanAssignment_Payer_PayerId",
                        column: x => x.PayerId,
                        principalSchema: "ppl",
                        principalTable: "Payer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanAssignment_PlanTemplate_PlanTemplateId",
                        column: x => x.PlanTemplateId,
                        principalSchema: "ppl",
                        principalTable: "PlanTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplateInstallment",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PlanTemplateId = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    SplitPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OffsetDaysFromYearStart = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateInstallment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateInstallment_PlanTemplate_PlanTemplateId",
                        column: x => x.PlanTemplateId,
                        principalSchema: "ppl",
                        principalTable: "PlanTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BundleLine",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BundleId = table.Column<int>(type: "int", nullable: false),
                    StoreItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BundleLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BundleLine_Bundle_BundleId",
                        column: x => x.BundleId,
                        principalSchema: "svc",
                        principalTable: "Bundle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BundleLine_StoreItem_StoreItemId",
                        column: x => x.StoreItemId,
                        principalSchema: "svc",
                        principalTable: "StoreItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceListLine",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PriceListId = table.Column<int>(type: "int", nullable: false),
                    StoreItemId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceListLine_PriceList_PriceListId",
                        column: x => x.PriceListId,
                        principalSchema: "svc",
                        principalTable: "PriceList",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceListLine_StoreItem_StoreItemId",
                        column: x => x.StoreItemId,
                        principalSchema: "svc",
                        principalTable: "StoreItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Variant",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StoreItemId = table.Column<int>(type: "int", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Size = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LowStockThreshold = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Variant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Variant_StoreItem_StoreItemId",
                        column: x => x.StoreItemId,
                        principalSchema: "svc",
                        principalTable: "StoreItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BlueprintComponent",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BlueprintId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(7,2)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueprintComponent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlueprintComponent_Blueprint_BlueprintId",
                        column: x => x.BlueprintId,
                        principalSchema: "core",
                        principalTable: "Blueprint",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Marksheet",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    BlueprintId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marksheet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Marksheet_Blueprint_BlueprintId",
                        column: x => x.BlueprintId,
                        principalSchema: "core",
                        principalTable: "Blueprint",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdministrationLog",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MedicationAuthorizationId = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NurseUserId = table.Column<int>(type: "int", nullable: false),
                    DoseGiven = table.Column<decimal>(type: "decimal(9,3)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    IsDeviation = table.Column<bool>(type: "bit", nullable: false),
                    DeviationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministrationLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdministrationLog_MedicationAuthorization_MedicationAuthorizationId",
                        column: x => x.MedicationAuthorizationId,
                        principalSchema: "svc",
                        principalTable: "MedicationAuthorization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditNote",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ChargeId = table.Column<int>(type: "int", nullable: false),
                    CreditNoteNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCarryForward = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditNote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditNote_Charge_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "ppl",
                        principalTable: "Charge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MealPlanSubscription",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MealPlanId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChargeId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanSubscription", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlanSubscription_Charge_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "ppl",
                        principalTable: "Charge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MealPlanSubscription_MealPlan_MealPlanId",
                        column: x => x.MealPlanId,
                        principalSchema: "svc",
                        principalTable: "MealPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradeYearProfile",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    GradeLevelId = table.Column<int>(type: "int", nullable: false),
                    CurriculumLookupValueId = table.Column<int>(type: "int", nullable: true),
                    GenderPolicy = table.Column<short>(type: "smallint", nullable: false),
                    MinAgeAtCutoff = table.Column<decimal>(type: "decimal(4,2)", nullable: true),
                    MaxAgeAtCutoff = table.Column<decimal>(type: "decimal(4,2)", nullable: true),
                    AgeCutoffDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetSections = table.Column<int>(type: "int", nullable: false),
                    TargetSectionSize = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeYearProfile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeYearProfile_AcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "core",
                        principalTable: "AcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradeYearProfile_GradeLevel_GradeLevelId",
                        column: x => x.GradeLevelId,
                        principalSchema: "core",
                        principalTable: "GradeLevel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Delivery",
                schema: "msg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    EventCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Channel = table.Column<short>(type: "smallint", nullable: false),
                    RecipientUserId = table.Column<int>(type: "int", nullable: false),
                    TemplateVersionId = table.Column<int>(type: "int", nullable: false),
                    RenderedSubject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RenderedBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Delivery", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Delivery_TemplateVersion_TemplateVersionId",
                        column: x => x.TemplateVersionId,
                        principalSchema: "msg",
                        principalTable: "TemplateVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Delivery_UserAccount_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalSchema: "sec",
                        principalTable: "UserAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocation",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ReceiptId = table.Column<int>(type: "int", nullable: false),
                    ChargeId = table.Column<int>(type: "int", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocation_Charge_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "ppl",
                        principalTable: "Charge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocation_Receipt_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "ppl",
                        principalTable: "Receipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pdc",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PayerId = table.Column<int>(type: "int", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChequeNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ChequeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ClearedReceiptId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pdc", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pdc_Payer_PayerId",
                        column: x => x.PayerId,
                        principalSchema: "ppl",
                        principalTable: "Payer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pdc_Receipt_ClearedReceiptId",
                        column: x => x.ClearedReceiptId,
                        principalSchema: "ppl",
                        principalTable: "Receipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoreSale",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: true),
                    PayerId = table.Column<int>(type: "int", nullable: false),
                    Tender = table.Column<short>(type: "smallint", nullable: false),
                    TillSessionId = table.Column<int>(type: "int", nullable: true),
                    OperatorUserId = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChargeId = table.Column<int>(type: "int", nullable: true),
                    ReceiptId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FinanceOverrideReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreSale", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreSale_Charge_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "ppl",
                        principalTable: "Charge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreSale_Payer_PayerId",
                        column: x => x.PayerId,
                        principalSchema: "ppl",
                        principalTable: "Payer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreSale_Receipt_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "ppl",
                        principalTable: "Receipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreSale_TillSession_TillSessionId",
                        column: x => x.TillSessionId,
                        principalSchema: "ppl",
                        principalTable: "TillSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WalletLedger",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReceiptId = table.Column<int>(type: "int", nullable: true),
                    SaleId = table.Column<int>(type: "int", nullable: true),
                    RefundVoucherId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletLedger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletLedger_Receipt_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "ppl",
                        principalTable: "Receipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletLedger_RefundVoucher_RefundVoucherId",
                        column: x => x.RefundVoucherId,
                        principalSchema: "ppl",
                        principalTable: "RefundVoucher",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletLedger_Wallet_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "svc",
                        principalTable: "Wallet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleLine",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    SaleId = table.Column<int>(type: "int", nullable: false),
                    CafeteriaItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AllergyWarned = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleLine_CafeteriaItem_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalSchema: "svc",
                        principalTable: "CafeteriaItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleLine_Sale_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "svc",
                        principalTable: "Sale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Loan",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    CopyId = table.Column<int>(type: "int", nullable: false),
                    MemberKind = table.Column<short>(type: "smallint", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RenewalCount = table.Column<int>(type: "int", nullable: false),
                    IsClassVisit = table.Column<bool>(type: "bit", nullable: false),
                    WasOverrideCheckout = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Loan_Copy_CopyId",
                        column: x => x.CopyId,
                        principalSchema: "svc",
                        principalTable: "Copy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservation",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TitleId = table.Column<int>(type: "int", nullable: false),
                    MemberKind = table.Column<short>(type: "smallint", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    QueuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    HeldCopyId = table.Column<int>(type: "int", nullable: true),
                    HoldExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservation_Copy_HeldCopyId",
                        column: x => x.HeldCopyId,
                        principalSchema: "svc",
                        principalTable: "Copy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservation_Title_TitleId",
                        column: x => x.TitleId,
                        principalSchema: "svc",
                        principalTable: "Title",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StocktakeLine",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StocktakeSessionId = table.Column<int>(type: "int", nullable: false),
                    CopyId = table.Column<int>(type: "int", nullable: false),
                    ExpectedStatus = table.Column<short>(type: "smallint", nullable: false),
                    WasScanned = table.Column<bool>(type: "bit", nullable: false),
                    Finding = table.Column<short>(type: "smallint", nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StocktakeLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StocktakeLine_Copy_CopyId",
                        column: x => x.CopyId,
                        principalSchema: "svc",
                        principalTable: "Copy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StocktakeLine_StocktakeSession_StocktakeSessionId",
                        column: x => x.StocktakeSessionId,
                        principalSchema: "svc",
                        principalTable: "StocktakeSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RouteStop",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    RouteId = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    ScheduledTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    ZoneFeeCategoryId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteStop", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteStop_FeeCategory_ZoneFeeCategoryId",
                        column: x => x.ZoneFeeCategoryId,
                        principalSchema: "ppl",
                        principalTable: "FeeCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RouteStop_Route_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "svc",
                        principalTable: "Route",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Trip",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    RouteId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Direction = table.Column<short>(type: "smallint", nullable: false),
                    BusId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    AttendantId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SweepConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    RosterCount = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trip", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trip_Route_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "svc",
                        principalTable: "Route",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScopeGrant",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    RoleAssignmentId = table.Column<int>(type: "int", nullable: false),
                    Dimension = table.Column<short>(type: "smallint", nullable: false),
                    ScopeValueId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScopeGrant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScopeGrant_RoleAssignment_RoleAssignmentId",
                        column: x => x.RoleAssignmentId,
                        principalSchema: "sec",
                        principalTable: "RoleAssignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstance",
                schema: "wf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    WorkflowDefinitionId = table.Column<int>(type: "int", nullable: false),
                    EntityTypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CurrentStateId = table.Column<int>(type: "int", nullable: false),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    RoutingValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ReturnCount = table.Column<int>(type: "int", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowInstance_WorkflowDefinition_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalSchema: "wf",
                        principalTable: "WorkflowDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowInstance_WorkflowState_CurrentStateId",
                        column: x => x.CurrentStateId,
                        principalSchema: "wf",
                        principalTable: "WorkflowState",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransition",
                schema: "wf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionId = table.Column<int>(type: "int", nullable: false),
                    FromStateId = table.Column<int>(type: "int", nullable: false),
                    ToStateId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<short>(type: "smallint", nullable: false),
                    RequiredRoleId = table.Column<int>(type: "int", nullable: true),
                    ReasonPolicy = table.Column<short>(type: "smallint", nullable: false),
                    MinRoutingValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MaxRoutingValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TriggersFinalEffect = table.Column<bool>(type: "bit", nullable: false),
                    PermissionModuleCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PermissionScreenCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PermissionAction = table.Column<short>(type: "smallint", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransition_WorkflowDefinition_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalSchema: "wf",
                        principalTable: "WorkflowDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransition_WorkflowState_FromStateId",
                        column: x => x.FromStateId,
                        principalSchema: "wf",
                        principalTable: "WorkflowState",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransition_WorkflowState_ToStateId",
                        column: x => x.ToStateId,
                        principalSchema: "wf",
                        principalTable: "WorkflowState",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityAttendance",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ActivitySessionId = table.Column<int>(type: "int", nullable: false),
                    ProgramEnrollmentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityAttendance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityAttendance_ActivitySession_ActivitySessionId",
                        column: x => x.ActivitySessionId,
                        principalSchema: "ppl",
                        principalTable: "ActivitySession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityAttendance_ProgramEnrollment_ProgramEnrollmentId",
                        column: x => x.ProgramEnrollmentId,
                        principalSchema: "ppl",
                        principalTable: "ProgramEnrollment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityConsentRecord",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ProgramEnrollmentId = table.Column<int>(type: "int", nullable: false),
                    ConsentTextSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GrantedByUserId = table.Column<int>(type: "int", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityConsentRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityConsentRecord_ProgramEnrollment_ProgramEnrollmentId",
                        column: x => x.ProgramEnrollmentId,
                        principalSchema: "ppl",
                        principalTable: "ProgramEnrollment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Case",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    OfficerUserId = table.Column<int>(type: "int", nullable: true),
                    RequiresPrincipal = table.Column<bool>(type: "bit", nullable: false),
                    ProposedConsequenceTypeId = table.Column<int>(type: "int", nullable: true),
                    DecidedConsequenceTypeId = table.Column<int>(type: "int", nullable: true),
                    DecisionArticleRef = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    PrincipalUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeviationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Case", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Case_Incident_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "svc",
                        principalTable: "Incident",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Placement",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TimetableVersionId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    PeriodSlotId = table.Column<int>(type: "int", nullable: false),
                    CurriculumOfferingId = table.Column<int>(type: "int", nullable: false),
                    TeacherProfileId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Placement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Placement_Room_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "core",
                        principalTable: "Room",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Placement_TimetableVersion_TimetableVersionId",
                        column: x => x.TimetableVersionId,
                        principalSchema: "core",
                        principalTable: "TimetableVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomAvailabilityException",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<short>(type: "smallint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomAvailabilityException", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomAvailabilityException_Room_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "core",
                        principalTable: "Room",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomBooking",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomBooking", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomBooking_Room_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "core",
                        principalTable: "Room",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomFeature",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    FeatureLookupId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomFeature", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomFeature_Room_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "core",
                        principalTable: "Room",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VerificationLog",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    CertificateIssueId = table.Column<int>(type: "int", nullable: true),
                    SubmittedCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    WasFound = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationLog_CertificateIssue_CertificateIssueId",
                        column: x => x.CertificateIssueId,
                        principalSchema: "ppl",
                        principalTable: "CertificateIssue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscountGrant",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    DiscountTypeId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<short>(type: "smallint", nullable: false),
                    BasisValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    RequiredTier = table.Column<short>(type: "smallint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProposedByUserId = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScholarshipProgramId = table.Column<int>(type: "int", nullable: true),
                    SponsorNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EnvelopeOverrideReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RevokedEffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RenewedFromGrantId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountGrant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscountGrant_DiscountGrant_RenewedFromGrantId",
                        column: x => x.RenewedFromGrantId,
                        principalSchema: "ppl",
                        principalTable: "DiscountGrant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiscountGrant_DiscountType_DiscountTypeId",
                        column: x => x.DiscountTypeId,
                        principalSchema: "ppl",
                        principalTable: "DiscountType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiscountGrant_ScholarshipProgram_ScholarshipProgramId",
                        column: x => x.ScholarshipProgramId,
                        principalSchema: "ppl",
                        principalTable: "ScholarshipProgram",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RescheduleCase",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PlanAssignmentId = table.Column<int>(type: "int", nullable: false),
                    ProposedByUserId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProposedScheduleJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RemainderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequiresPrincipal = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ProposedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RescheduleCase", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RescheduleCase_PlanAssignment_PlanAssignmentId",
                        column: x => x.PlanAssignmentId,
                        principalSchema: "ppl",
                        principalTable: "PlanAssignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleRevision",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PlanAssignmentId = table.Column<int>(type: "int", nullable: false),
                    Cause = table.Column<short>(type: "smallint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRevision", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleRevision_PlanAssignment_PlanAssignmentId",
                        column: x => x.PlanAssignmentId,
                        principalSchema: "ppl",
                        principalTable: "PlanAssignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoreStockMovement",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StoreVariantId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreStockMovement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreStockMovement_Variant_StoreVariantId",
                        column: x => x.StoreVariantId,
                        principalSchema: "svc",
                        principalTable: "Variant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Exam",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ExamRoundId = table.Column<int>(type: "int", nullable: false),
                    ExamTypeId = table.Column<int>(type: "int", nullable: false),
                    CurriculumOfferingId = table.Column<int>(type: "int", nullable: false),
                    GradeYearProfileId = table.Column<int>(type: "int", nullable: false),
                    BlueprintComponentId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exam", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Exam_BlueprintComponent_BlueprintComponentId",
                        column: x => x.BlueprintComponentId,
                        principalSchema: "core",
                        principalTable: "BlueprintComponent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Exam_ExamRound_ExamRoundId",
                        column: x => x.ExamRoundId,
                        principalSchema: "core",
                        principalTable: "ExamRound",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Exam_ExamType_ExamTypeId",
                        column: x => x.ExamTypeId,
                        principalSchema: "core",
                        principalTable: "ExamType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarkEntry",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MarksheetId = table.Column<int>(type: "int", nullable: false),
                    BlueprintComponentId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(7,2)", nullable: true),
                    IsAbsent = table.Column<bool>(type: "bit", nullable: false),
                    IsExempt = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarkEntry_Marksheet_MarksheetId",
                        column: x => x.MarksheetId,
                        principalSchema: "core",
                        principalTable: "Marksheet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BundleAssignment",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BundleId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    PayerId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ChargeId = table.Column<int>(type: "int", nullable: true),
                    CreditNoteId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BundleAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BundleAssignment_Bundle_BundleId",
                        column: x => x.BundleId,
                        principalSchema: "svc",
                        principalTable: "Bundle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BundleAssignment_Charge_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "ppl",
                        principalTable: "Charge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BundleAssignment_CreditNote_CreditNoteId",
                        column: x => x.CreditNoteId,
                        principalSchema: "ppl",
                        principalTable: "CreditNote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Waiver",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ChargeId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequiredTier = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ProposedByUserId = table.Column<int>(type: "int", nullable: false),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreditNoteId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Waiver", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Waiver_Charge_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "ppl",
                        principalTable: "Charge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Waiver_CreditNote_CreditNoteId",
                        column: x => x.CreditNoteId,
                        principalSchema: "ppl",
                        principalTable: "CreditNote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Redemption",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    MealPlanSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SaleId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Redemption", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Redemption_MealPlanSubscription_MealPlanSubscriptionId",
                        column: x => x.MealPlanSubscriptionId,
                        principalSchema: "svc",
                        principalTable: "MealPlanSubscription",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Redemption_Sale_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "svc",
                        principalTable: "Sale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumOffering",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    GradeYearProfileId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    WeeklyPeriods = table.Column<int>(type: "int", nullable: false),
                    IsAssessable = table.Column<bool>(type: "bit", nullable: false),
                    GpaWeight = table.Column<decimal>(type: "decimal(6,3)", nullable: false),
                    IsElective = table.Column<bool>(type: "bit", nullable: false),
                    ElectiveGroupTag = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumOffering", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumOffering_GradeYearProfile_GradeYearProfileId",
                        column: x => x.GradeYearProfileId,
                        principalSchema: "core",
                        principalTable: "GradeYearProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurriculumOffering_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "core",
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enrollment",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    GradeYearProfileId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExitDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    SourceType = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enrollment_AcademicYear_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalSchema: "core",
                        principalTable: "AcademicYear",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollment_GradeYearProfile_GradeYearProfileId",
                        column: x => x.GradeYearProfileId,
                        principalSchema: "core",
                        principalTable: "GradeYearProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollment_Student_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "ppl",
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Section",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    GradeYearProfileId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    GenderPolicy = table.Column<short>(type: "smallint", nullable: false),
                    DefaultClassroomId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Section", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Section_GradeYearProfile_GradeYearProfileId",
                        column: x => x.GradeYearProfileId,
                        principalSchema: "core",
                        principalTable: "GradeYearProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Section_Room_DefaultClassroomId",
                        column: x => x.DefaultClassroomId,
                        principalSchema: "core",
                        principalTable: "Room",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Installment",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PlanAssignmentId = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsSuperseded = table.Column<bool>(type: "bit", nullable: false),
                    IsWrittenOff = table.Column<bool>(type: "bit", nullable: false),
                    WriteOffReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CoveringPdcId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Installment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Installment_Pdc_CoveringPdcId",
                        column: x => x.CoveringPdcId,
                        principalSchema: "ppl",
                        principalTable: "Pdc",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Installment_PlanAssignment_PlanAssignmentId",
                        column: x => x.PlanAssignmentId,
                        principalSchema: "ppl",
                        principalTable: "PlanAssignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoreSaleLine",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StoreSaleId = table.Column<int>(type: "int", nullable: false),
                    StoreVariantId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreSaleLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreSaleLine_StoreSale_StoreSaleId",
                        column: x => x.StoreSaleId,
                        principalSchema: "svc",
                        principalTable: "StoreSale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreSaleLine_Variant_StoreVariantId",
                        column: x => x.StoreVariantId,
                        principalSchema: "svc",
                        principalTable: "Variant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CirculationEvent",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    LoanId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CirculationEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CirculationEvent_Loan_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "svc",
                        principalTable: "Loan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FineProposal",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    LoanId = table.Column<int>(type: "int", nullable: false),
                    MemberKind = table.Column<short>(type: "smallint", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ChargeId = table.Column<int>(type: "int", nullable: true),
                    CreditNoteId = table.Column<int>(type: "int", nullable: true),
                    ProposedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FineProposal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FineProposal_Charge_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "ppl",
                        principalTable: "Charge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FineProposal_CreditNote_CreditNoteId",
                        column: x => x.CreditNoteId,
                        principalSchema: "ppl",
                        principalTable: "CreditNote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FineProposal_Loan_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "svc",
                        principalTable: "Loan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEvent",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TripId = table.Column<int>(type: "int", nullable: true),
                    StudentId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    State = table.Column<short>(type: "smallint", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SafetyEvent_Trip_TripId",
                        column: x => x.TripId,
                        principalSchema: "svc",
                        principalTable: "Trip",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TripLog",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    TripId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Event = table.Column<short>(type: "smallint", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    ReceivedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    HandoverConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripLog_Trip_TripId",
                        column: x => x.TripId,
                        principalSchema: "svc",
                        principalTable: "Trip",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStep",
                schema: "wf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<int>(type: "int", nullable: false),
                    FromStateId = table.Column<int>(type: "int", nullable: false),
                    ToStateId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<short>(type: "smallint", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    IsDelegated = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStep", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStep_WorkflowInstance_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "wf",
                        principalTable: "WorkflowInstance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowStep_WorkflowState_FromStateId",
                        column: x => x.FromStateId,
                        principalSchema: "wf",
                        principalTable: "WorkflowState",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowStep_WorkflowState_ToStateId",
                        column: x => x.ToStateId,
                        principalSchema: "wf",
                        principalTable: "WorkflowState",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActionApplied",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DisciplineCaseId = table.Column<int>(type: "int", nullable: false),
                    ConsequenceTypeId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Days = table.Column<int>(type: "int", nullable: true),
                    ApprovedByPrincipalUserId = table.Column<int>(type: "int", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionApplied", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionApplied_Case_DisciplineCaseId",
                        column: x => x.DisciplineCaseId,
                        principalSchema: "svc",
                        principalTable: "Case",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActionApplied_ConsequenceType_ConsequenceTypeId",
                        column: x => x.ConsequenceTypeId,
                        principalSchema: "svc",
                        principalTable: "ConsequenceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Appeal",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DisciplineCaseId = table.Column<int>(type: "int", nullable: false),
                    FiledByParentId = table.Column<int>(type: "int", nullable: false),
                    FiledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Grounds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ReviewerUserId = table.Column<int>(type: "int", nullable: true),
                    Outcome = table.Column<short>(type: "smallint", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appeal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appeal_Case_DisciplineCaseId",
                        column: x => x.DisciplineCaseId,
                        principalSchema: "svc",
                        principalTable: "Case",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseStatement",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DisciplineCaseId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttachmentId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseStatement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseStatement_Case_DisciplineCaseId",
                        column: x => x.DisciplineCaseId,
                        principalSchema: "svc",
                        principalTable: "Case",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Session",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    PlacementId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    OverrideRoomId = table.Column<int>(type: "int", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Session_Placement_PlacementId",
                        column: x => x.PlacementId,
                        principalSchema: "core",
                        principalTable: "Placement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Session_Room_OverrideRoomId",
                        column: x => x.OverrideRoomId,
                        principalSchema: "core",
                        principalTable: "Room",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscountDocument",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DiscountGrantId = table.Column<int>(type: "int", nullable: false),
                    ChargeId = table.Column<int>(type: "int", nullable: false),
                    DocumentNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountDocument", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscountDocument_Charge_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "ppl",
                        principalTable: "Charge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiscountDocument_DiscountGrant_DiscountGrantId",
                        column: x => x.DiscountGrantId,
                        principalSchema: "ppl",
                        principalTable: "DiscountGrant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RenewalQueueItem",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PriorGrantId = table.Column<int>(type: "int", nullable: false),
                    NewAcademicYearId = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<short>(type: "smallint", nullable: false),
                    AdjustedBasisValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewGrantId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenewalQueueItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RenewalQueueItem_DiscountGrant_PriorGrantId",
                        column: x => x.PriorGrantId,
                        principalSchema: "ppl",
                        principalTable: "DiscountGrant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamSitting",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ExamId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamSitting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamSitting_Exam_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "core",
                        principalTable: "Exam",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamSitting_Room_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "core",
                        principalTable: "Room",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MakeupEligibility",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ExamId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    IsSystemDerived = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MakeupEligibility", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MakeupEligibility_Exam_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "core",
                        principalTable: "Exam",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HandoutRecord",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    DistributionSessionId = table.Column<int>(type: "int", nullable: false),
                    BundleAssignmentId = table.Column<int>(type: "int", nullable: false),
                    BundleLineId = table.Column<int>(type: "int", nullable: false),
                    StoreVariantId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Acknowledged = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoutRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandoutRecord_BundleAssignment_BundleAssignmentId",
                        column: x => x.BundleAssignmentId,
                        principalSchema: "svc",
                        principalTable: "BundleAssignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoutRecord_DistributionSession_DistributionSessionId",
                        column: x => x.DistributionSessionId,
                        principalSchema: "svc",
                        principalTable: "DistributionSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoutRecord_Variant_StoreVariantId",
                        column: x => x.StoreVariantId,
                        principalSchema: "svc",
                        principalTable: "Variant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolloverStudentState",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    RolloverBatchId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    SourceEnrollmentId = table.Column<int>(type: "int", nullable: false),
                    SourceGradeYearProfileId = table.Column<int>(type: "int", nullable: false),
                    ProposedDecision = table.Column<short>(type: "smallint", nullable: false),
                    Decision = table.Column<short>(type: "smallint", nullable: false),
                    DecisionSource = table.Column<short>(type: "smallint", nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TargetGradeYearProfileId = table.Column<int>(type: "int", nullable: true),
                    ReRegistration = table.Column<short>(type: "smallint", nullable: false),
                    ReRegistrationDecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReRegistrationChargeId = table.Column<int>(type: "int", nullable: true),
                    AssignedSectionId = table.Column<int>(type: "int", nullable: true),
                    TargetEnrollmentId = table.Column<int>(type: "int", nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CarryForwardAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolloverStudentState", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolloverStudentState_Enrollment_SourceEnrollmentId",
                        column: x => x.SourceEnrollmentId,
                        principalSchema: "ppl",
                        principalTable: "Enrollment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolloverStudentState_RolloverBatch_RolloverBatchId",
                        column: x => x.RolloverBatchId,
                        principalSchema: "core",
                        principalTable: "RolloverBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolloverStudentState_Student_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "ppl",
                        principalTable: "Student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransportSubscription",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    PayerId = table.Column<int>(type: "int", nullable: false),
                    AmRouteStopId = table.Column<int>(type: "int", nullable: true),
                    PmRouteStopId = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ChargeId = table.Column<int>(type: "int", nullable: true),
                    IsSelfReleaseAllowed = table.Column<bool>(type: "bit", nullable: false),
                    SuspendedEffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspensionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SuspensionApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportSubscription", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransportSubscription_Charge_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "ppl",
                        principalTable: "Charge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransportSubscription_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "ppl",
                        principalTable: "Enrollment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransportSubscription_RouteStop_AmRouteStopId",
                        column: x => x.AmRouteStopId,
                        principalSchema: "svc",
                        principalTable: "RouteStop",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransportSubscription_RouteStop_PmRouteStopId",
                        column: x => x.PmRouteStopId,
                        principalSchema: "svc",
                        principalTable: "RouteStop",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HomeroomAssignment",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    TeacherUserId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeroomAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeroomAssignment_Section_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "core",
                        principalTable: "Section",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SectionMembership",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransferReasonCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionMembership", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectionMembership_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "ppl",
                        principalTable: "Enrollment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SectionMembership_Section_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "core",
                        principalTable: "Section",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DunningEvent",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    InstallmentId = table.Column<int>(type: "int", nullable: false),
                    Step = table.Column<short>(type: "smallint", nullable: false),
                    FiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TriggeredByBrokenPromise = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DunningEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DunningEvent_Installment_InstallmentId",
                        column: x => x.InstallmentId,
                        principalSchema: "ppl",
                        principalTable: "Installment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstallmentChargeLine",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    InstallmentId = table.Column<int>(type: "int", nullable: false),
                    ChargeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallmentChargeLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstallmentChargeLine_Charge_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "ppl",
                        principalTable: "Charge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstallmentChargeLine_Installment_InstallmentId",
                        column: x => x.InstallmentId,
                        principalSchema: "ppl",
                        principalTable: "Installment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromiseToPay",
                schema: "ppl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    InstallmentId = table.Column<int>(type: "int", nullable: false),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    PromisedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromiseToPay", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromiseToPay_Installment_InstallmentId",
                        column: x => x.InstallmentId,
                        principalSchema: "ppl",
                        principalTable: "Installment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnExchange",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StoreSaleLineId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    NewStoreVariantId = table.Column<int>(type: "int", nullable: true),
                    IsSealed = table.Column<bool>(type: "bit", nullable: false),
                    CreditNoteId = table.Column<int>(type: "int", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnExchange", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnExchange_CreditNote_CreditNoteId",
                        column: x => x.CreditNoteId,
                        principalSchema: "ppl",
                        principalTable: "CreditNote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnExchange_StoreSaleLine_StoreSaleLineId",
                        column: x => x.StoreSaleLineId,
                        principalSchema: "svc",
                        principalTable: "StoreSaleLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnExchange_Variant_NewStoreVariantId",
                        column: x => x.NewStoreVariantId,
                        principalSchema: "svc",
                        principalTable: "Variant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Substitution",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    SubstituteTeacherProfileId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsCountedForPayroll = table.Column<bool>(type: "bit", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Substitution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Substitution_Session_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "core",
                        principalTable: "Session",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamAttendance",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ExamSittingId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamAttendance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamAttendance_ExamSitting_ExamSittingId",
                        column: x => x.ExamSittingId,
                        principalSchema: "core",
                        principalTable: "ExamSitting",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamIncident",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    ExamSittingId = table.Column<int>(type: "int", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamIncident", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamIncident_ExamSitting_ExamSittingId",
                        column: x => x.ExamSittingId,
                        principalSchema: "core",
                        principalTable: "ExamSitting",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RouteWaitlist",
                schema: "svc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    RouteId = table.Column<int>(type: "int", nullable: false),
                    TransportSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    QueuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteWaitlist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteWaitlist_Route_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "svc",
                        principalTable: "Route",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RouteWaitlist_TransportSubscription_TransportSubscriptionId",
                        column: x => x.TransportSubscriptionId,
                        principalSchema: "svc",
                        principalTable: "TransportSubscription",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYear_SchoolId_Active",
                schema: "core",
                table: "AcademicYear",
                column: "SchoolId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYear_SchoolId_Preparation",
                schema: "core",
                table: "AcademicYear",
                column: "SchoolId",
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Achievement_CompetitionEventId",
                schema: "ppl",
                table: "Achievement",
                column: "CompetitionEventId");

            migrationBuilder.CreateIndex(
                name: "IX_Achievement_ProgramId",
                schema: "ppl",
                table: "Achievement",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionApplied_ConsequenceTypeId",
                schema: "svc",
                table: "ActionApplied",
                column: "ConsequenceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionApplied_DisciplineCaseId",
                schema: "svc",
                table: "ActionApplied",
                column: "DisciplineCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityAttendance_ActivitySessionId_ProgramEnrollmentId",
                schema: "ppl",
                table: "ActivityAttendance",
                columns: new[] { "ActivitySessionId", "ProgramEnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityAttendance_ProgramEnrollmentId",
                schema: "ppl",
                table: "ActivityAttendance",
                column: "ProgramEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityConsentRecord_ProgramEnrollmentId",
                schema: "ppl",
                table: "ActivityConsentRecord",
                column: "ProgramEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySession_ProgramId",
                schema: "ppl",
                table: "ActivitySession",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTrip_ProgramId",
                schema: "ppl",
                table: "ActivityTrip",
                column: "ProgramId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdministrationLog_MedicationAuthorizationId",
                schema: "svc",
                table: "AdministrationLog",
                column: "MedicationAuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionCampaign_SchoolId_AcademicYearId_GradeYearProfileId",
                schema: "ppl",
                table: "AdmissionCampaign",
                columns: new[] { "SchoolId", "AcademicYearId", "GradeYearProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgedReceivablesSnapshot_School_Payer",
                schema: "rpt",
                table: "AgedReceivablesSnapshot",
                columns: new[] { "SchoolId", "PayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgedReceivablesSnapshot_School_Profile",
                schema: "rpt",
                table: "AgedReceivablesSnapshot",
                columns: new[] { "SchoolId", "GradeYearProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_Allergy_MedicalFileId",
                schema: "svc",
                table: "Allergy",
                column: "MedicalFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyHit_AnomalyRuleId",
                schema: "aud",
                table: "AnomalyHit",
                column: "AnomalyRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyRule_Code",
                schema: "aud",
                table: "AnomalyRule",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appeal_DisciplineCaseId",
                schema: "svc",
                table: "Appeal",
                column: "DisciplineCaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Application_CampaignId",
                schema: "ppl",
                table: "Application",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Application_ParentId",
                schema: "ppl",
                table: "Application",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Application_SchoolId_ApplicationNo",
                schema: "ppl",
                table: "Application",
                columns: new[] { "SchoolId", "ApplicationNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAssessment_ApplicationId",
                schema: "ppl",
                table: "ApplicationAssessment",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachment_DocumentType_Expiry",
                schema: "doc",
                table: "Attachment",
                columns: new[] { "DocumentTypeId", "ExpiryDateUtc" },
                filter: "[ExpiryDateUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Attachment_OwningEntityType_OwningEntityId_DocumentTypeId",
                schema: "doc",
                table: "Attachment",
                columns: new[] { "OwningEntityType", "OwningEntityId", "DocumentTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentVersion_AttachmentId_VersionNumber",
                schema: "doc",
                table: "AttachmentVersion",
                columns: new[] { "AttachmentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDay_Enrollment_Date",
                schema: "core",
                table: "AttendanceDay",
                columns: new[] { "EnrollmentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDay_School_Date_Status",
                schema: "core",
                table: "AttendanceDay",
                columns: new[] { "SchoolId", "Date", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDay_SectionId",
                schema: "core",
                table: "AttendanceDay",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntry_ActorUserId_OccurredAtUtc",
                schema: "aud",
                table: "AuditEntry",
                columns: new[] { "ActorUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntry_EntityType_EntityId_OccurredAtUtc",
                schema: "aud",
                table: "AuditEntry",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntry_SchoolId_OccurredAtUtc",
                schema: "aud",
                table: "AuditEntry",
                columns: new[] { "SchoolId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BackupRun_BackupPolicyId",
                schema: "ops",
                table: "BackupRun",
                column: "BackupPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupVerificationRun_BackupRunId",
                schema: "ops",
                table: "BackupVerificationRun",
                column: "BackupRunId");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorCode_SchoolId_AcademicYearId_Version",
                schema: "svc",
                table: "BehaviorCode",
                columns: new[] { "SchoolId", "AcademicYearId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Blueprint_CurriculumOfferingId_TermId",
                schema: "core",
                table: "Blueprint",
                columns: new[] { "CurriculumOfferingId", "TermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Blueprint_GradingScaleId",
                schema: "core",
                table: "Blueprint",
                column: "GradingScaleId");

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintComponent_BlueprintId",
                schema: "core",
                table: "BlueprintComponent",
                column: "BlueprintId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCounter_SchoolId_Channel_PeriodKey",
                schema: "msg",
                table: "BudgetCounter",
                columns: new[] { "SchoolId", "Channel", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bundle_FeeCategoryId",
                schema: "svc",
                table: "Bundle",
                column: "FeeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BundleAssignment_BundleId_StudentId",
                schema: "svc",
                table: "BundleAssignment",
                columns: new[] { "BundleId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BundleAssignment_ChargeId",
                schema: "svc",
                table: "BundleAssignment",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_BundleAssignment_CreditNoteId",
                schema: "svc",
                table: "BundleAssignment",
                column: "CreditNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_BundleLine_BundleId",
                schema: "svc",
                table: "BundleLine",
                column: "BundleId");

            migrationBuilder.CreateIndex(
                name: "IX_BundleLine_StoreItemId",
                schema: "svc",
                table: "BundleLine",
                column: "StoreItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Bus_SchoolId_PlateNo",
                schema: "svc",
                table: "Bus",
                columns: new[] { "SchoolId", "PlateNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusDocument_BusId_Kind",
                schema: "svc",
                table: "BusDocument",
                columns: new[] { "BusId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarDay_AcademicYearId_Date",
                schema: "core",
                table: "CalendarDay",
                columns: new[] { "AcademicYearId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvent_AcademicYearId_StartDate",
                schema: "core",
                table: "CalendarEvent",
                columns: new[] { "AcademicYearId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarVersion_AcademicYearId_VersionNumber",
                schema: "core",
                table: "CalendarVersion",
                columns: new[] { "AcademicYearId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarePlan_MedicalFileId",
                schema: "svc",
                table: "CarePlan",
                column: "MedicalFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Case_IncidentId",
                schema: "svc",
                table: "Case",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseStatement_DisciplineCaseId",
                schema: "svc",
                table: "CaseStatement",
                column: "DisciplineCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateIssue_CertificateRequestId",
                schema: "ppl",
                table: "CertificateIssue",
                column: "CertificateRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateIssue_ReissuedFromCertificateIssueId",
                schema: "ppl",
                table: "CertificateIssue",
                column: "ReissuedFromCertificateIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateIssue_SchoolId_CertificateNo",
                schema: "ppl",
                table: "CertificateIssue",
                columns: new[] { "SchoolId", "CertificateNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateIssue_VerificationCode",
                schema: "ppl",
                table: "CertificateIssue",
                column: "VerificationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRequest_CertificateTypeId",
                schema: "ppl",
                table: "CertificateRequest",
                column: "CertificateTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRequest_StudentId",
                schema: "ppl",
                table: "CertificateRequest",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Charge_FeeCategoryId",
                schema: "ppl",
                table: "Charge",
                column: "FeeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Charge_OpeningBalance_Unique",
                schema: "ppl",
                table: "Charge",
                columns: new[] { "StudentId", "PayerId", "SourceAcademicYearId" },
                unique: true,
                filter: "[SourceAcademicYearId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Charge_Payer_Status",
                schema: "ppl",
                table: "Charge",
                columns: new[] { "PayerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Charge_SchoolId_ChargeNo",
                schema: "ppl",
                table: "Charge",
                columns: new[] { "SchoolId", "ChargeNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Charge_StudentId",
                schema: "ppl",
                table: "Charge",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_CirculationEvent_LoanId",
                schema: "svc",
                table: "CirculationEvent",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisit_MedicalFileId",
                schema: "svc",
                table: "ClinicVisit",
                column: "MedicalFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisit_SchoolId_VisitNo",
                schema: "svc",
                table: "ClinicVisit",
                columns: new[] { "SchoolId", "VisitNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionCalendarSnapshot_School_DueDate",
                schema: "rpt",
                table: "CollectionCalendarSnapshot",
                columns: new[] { "SchoolId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMatrix_SchoolId_TopicCode",
                schema: "msg",
                table: "CommunicationMatrix",
                columns: new[] { "SchoolId", "TopicCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecord_VaccinationCampaignId_StudentId",
                schema: "svc",
                table: "ConsentRecord",
                columns: new[] { "VaccinationCampaignId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsequenceType_BehaviorCodeId",
                schema: "svc",
                table: "ConsequenceType",
                column: "BehaviorCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_EmployeeId",
                schema: "ppl",
                table: "Contract",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Copy_SchoolId_Barcode",
                schema: "svc",
                table: "Copy",
                columns: new[] { "SchoolId", "Barcode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Copy_TitleId",
                schema: "svc",
                table: "Copy",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNote_ChargeId",
                schema: "ppl",
                table: "CreditNote",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNote_SchoolId_CreditNoteNo",
                schema: "ppl",
                table: "CreditNote",
                columns: new[] { "SchoolId", "CreditNoteNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumOffering_GradeYearSubject_Current",
                schema: "core",
                table: "CurriculumOffering",
                columns: new[] { "GradeYearProfileId", "SubjectId" },
                unique: true,
                filter: "[EffectiveToUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumOffering_SubjectId",
                schema: "core",
                table: "CurriculumOffering",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyAttendanceSummarySnapshot_School_Date_Section",
                schema: "rpt",
                table: "DailyAttendanceSummarySnapshot",
                columns: new[] { "SchoolId", "Date", "SectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyAttendanceSummarySnapshot_School_Date_Stage",
                schema: "rpt",
                table: "DailyAttendanceSummarySnapshot",
                columns: new[] { "SchoolId", "Date", "StageId" });

            migrationBuilder.CreateIndex(
                name: "IX_Delivery_RecipientUserId_Status",
                schema: "msg",
                table: "Delivery",
                columns: new[] { "RecipientUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Delivery_School_Status_Queued",
                schema: "msg",
                table: "Delivery",
                columns: new[] { "SchoolId", "Status", "CreatedAtUtc" },
                filter: "[Status] IN (1, 4)");

            migrationBuilder.CreateIndex(
                name: "IX_Delivery_Status",
                schema: "msg",
                table: "Delivery",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Delivery_TemplateVersionId",
                schema: "msg",
                table: "Delivery",
                column: "TemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountDocument_ChargeId",
                schema: "ppl",
                table: "DiscountDocument",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountDocument_DiscountGrantId",
                schema: "ppl",
                table: "DiscountDocument",
                column: "DiscountGrantId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountDocument_SchoolId_DocumentNo",
                schema: "ppl",
                table: "DiscountDocument",
                columns: new[] { "SchoolId", "DocumentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscountGrant_DiscountTypeId",
                schema: "ppl",
                table: "DiscountGrant",
                column: "DiscountTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountGrant_RenewedFromGrantId",
                schema: "ppl",
                table: "DiscountGrant",
                column: "RenewedFromGrantId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountGrant_ScholarshipProgramId",
                schema: "ppl",
                table: "DiscountGrant",
                column: "ScholarshipProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountGrant_StudentId_AcademicYearId",
                schema: "ppl",
                table: "DiscountGrant",
                columns: new[] { "StudentId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscountType_FeeCategoryId",
                schema: "ppl",
                table: "DiscountType",
                column: "FeeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionSession_BundleId",
                schema: "svc",
                table: "DistributionSession",
                column: "BundleId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentType_SchoolId_Code",
                schema: "doc",
                table: "DocumentType",
                columns: new[] { "SchoolId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DunningEvent_InstallmentId_Step",
                schema: "ppl",
                table: "DunningEvent",
                columns: new[] { "InstallmentId", "Step" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EligibilityRule_DiscountTypeId",
                schema: "ppl",
                table: "EligibilityRule",
                column: "DiscountTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyContact_StudentId",
                schema: "ppl",
                table: "EmergencyContact",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employee_SchoolId_EmployeeNo",
                schema: "ppl",
                table: "Employee",
                columns: new[] { "SchoolId", "EmployeeNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAssignment_Employee_Current",
                schema: "ppl",
                table: "EmployeeAssignment",
                column: "EmployeeId",
                unique: true,
                filter: "[EffectiveToUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAssignment_OrgUnitId",
                schema: "ppl",
                table: "EmployeeAssignment",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_AcademicYearId",
                schema: "ppl",
                table: "Enrollment",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_GradeYearProfileId",
                schema: "ppl",
                table: "Enrollment",
                column: "GradeYearProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_School_Year_Profile",
                schema: "ppl",
                table: "Enrollment",
                columns: new[] { "SchoolId", "AcademicYearId", "GradeYearProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_Student_Year_Active",
                schema: "ppl",
                table: "Enrollment",
                columns: new[] { "StudentId", "AcademicYearId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Exam_BlueprintComponentId",
                schema: "core",
                table: "Exam",
                column: "BlueprintComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_Exam_ExamRoundId",
                schema: "core",
                table: "Exam",
                column: "ExamRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_Exam_ExamTypeId",
                schema: "core",
                table: "Exam",
                column: "ExamTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Exam_GradeYearProfileId_Date",
                schema: "core",
                table: "Exam",
                columns: new[] { "GradeYearProfileId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttendance_ExamSittingId_EnrollmentId",
                schema: "core",
                table: "ExamAttendance",
                columns: new[] { "ExamSittingId", "EnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamIncident_ExamSittingId",
                schema: "core",
                table: "ExamIncident",
                column: "ExamSittingId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSitting_ExamId",
                schema: "core",
                table: "ExamSitting",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSitting_RoomId",
                schema: "core",
                table: "ExamSitting",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureLine_FeeCategoryId",
                schema: "ppl",
                table: "FeeStructureLine",
                column: "FeeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructureLine_Profile_Category",
                schema: "ppl",
                table: "FeeStructureLine",
                columns: new[] { "GradeYearProfileId", "FeeCategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FineProposal_ChargeId",
                schema: "svc",
                table: "FineProposal",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_FineProposal_CreditNoteId",
                schema: "svc",
                table: "FineProposal",
                column: "CreditNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_FineProposal_LoanId",
                schema: "svc",
                table: "FineProposal",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_Floor_BuildingId",
                schema: "core",
                table: "Floor",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_GateEvent_EnrollmentId",
                schema: "core",
                table: "GateEvent",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_GlAccountMapping_SchoolId_Key",
                schema: "fin",
                table: "GlAccountMapping",
                columns: new[] { "SchoolId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlExportBatch_SchoolId_BatchNo",
                schema: "fin",
                table: "GlExportBatch",
                columns: new[] { "SchoolId", "BatchNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlJournalLine_GlExportBatchId",
                schema: "fin",
                table: "GlJournalLine",
                column: "GlExportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeLevel_PromotionTargetGradeLevelId",
                schema: "core",
                table: "GradeLevel",
                column: "PromotionTargetGradeLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeLevel_SchoolId_Code",
                schema: "core",
                table: "GradeLevel",
                columns: new[] { "SchoolId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeLevel_StageId",
                schema: "core",
                table: "GradeLevel",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeYearProfile_AcademicYearId",
                schema: "core",
                table: "GradeYearProfile",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeYearProfile_GradeLevelId_AcademicYearId",
                schema: "core",
                table: "GradeYearProfile",
                columns: new[] { "GradeLevelId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandoutRecord_BundleAssignmentId",
                schema: "svc",
                table: "HandoutRecord",
                column: "BundleAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoutRecord_DistributionSessionId",
                schema: "svc",
                table: "HandoutRecord",
                column: "DistributionSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoutRecord_StoreVariantId",
                schema: "svc",
                table: "HandoutRecord",
                column: "StoreVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeroomAssignment_SectionId_Current",
                schema: "core",
                table: "HomeroomAssignment",
                column: "SectionId",
                unique: true,
                filter: "[EffectiveToUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Incident_SchoolId_IncidentNo",
                schema: "svc",
                table: "Incident",
                columns: new[] { "SchoolId", "IncidentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incident_StudentId_AcademicYearId",
                schema: "svc",
                table: "Incident",
                columns: new[] { "StudentId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Incident_ViolationTypeId",
                schema: "svc",
                table: "Incident",
                column: "ViolationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_InfectiousCase_MedicalFileId",
                schema: "svc",
                table: "InfectiousCase",
                column: "MedicalFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Installment_CoveringPdcId",
                schema: "ppl",
                table: "Installment",
                column: "CoveringPdcId");

            migrationBuilder.CreateIndex(
                name: "IX_Installment_PlanAssignmentId_DueDate",
                schema: "ppl",
                table: "Installment",
                columns: new[] { "PlanAssignmentId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Installment_School_DueDate_Open",
                schema: "ppl",
                table: "Installment",
                columns: new[] { "SchoolId", "DueDate" },
                filter: "[IsSuperseded] = 0 AND [IsWrittenOff] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentChargeLine_ChargeId",
                schema: "ppl",
                table: "InstallmentChargeLine",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallmentChargeLine_InstallmentId",
                schema: "ppl",
                table: "InstallmentChargeLine",
                column: "InstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrityCheckpoint_PeriodStartUtc",
                schema: "aud",
                table: "IntegrityCheckpoint",
                column: "PeriodStartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_JobDefinition_Code",
                schema: "ops",
                table: "JobDefinition",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobRun_JobDefinitionId_StartedAtUtc",
                schema: "ops",
                table: "JobRun",
                columns: new[] { "JobDefinitionId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Justification_AttendanceDayId",
                schema: "core",
                table: "Justification",
                column: "AttendanceDayId");

            migrationBuilder.CreateIndex(
                name: "IX_LadderStep_BehaviorCodeId_Severity_RepetitionCount",
                schema: "svc",
                table: "LadderStep",
                columns: new[] { "BehaviorCodeId", "Severity", "RepetitionCount" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LadderStep_ConsequenceTypeId",
                schema: "svc",
                table: "LadderStep",
                column: "ConsequenceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutTemplate_SchoolId_RoleId",
                schema: "core",
                table: "LayoutTemplate",
                columns: new[] { "SchoolId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LayoutTemplateWidget_LayoutTemplateId_WidgetDefinitionId",
                schema: "core",
                table: "LayoutTemplateWidget",
                columns: new[] { "LayoutTemplateId", "WidgetDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LayoutTemplateWidget_WidgetDefinitionId",
                schema: "core",
                table: "LayoutTemplateWidget",
                column: "WidgetDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_LeavePass_EnrollmentId",
                schema: "core",
                table: "LeavePass",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseState_SchoolId",
                schema: "core",
                table: "LicenseState",
                column: "SchoolId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loan_CopyId_ReturnedAtUtc",
                schema: "svc",
                table: "Loan",
                columns: new[] { "CopyId", "ReturnedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Loan_MemberKind_MemberId",
                schema: "svc",
                table: "Loan",
                columns: new[] { "MemberKind", "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempt_SchoolId_UserNameAttempted_CreatedAtUtc",
                schema: "sec",
                table: "LoginAttempt",
                columns: new[] { "SchoolId", "UserNameAttempted", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempt_UserAccountId",
                schema: "sec",
                table: "LoginAttempt",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LookupCategory_SchoolId_Code",
                schema: "core",
                table: "LookupCategory",
                columns: new[] { "SchoolId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LookupValue_LookupCategoryId_Code",
                schema: "core",
                table: "LookupValue",
                columns: new[] { "LookupCategoryId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MakeupEligibility_ExamId_EnrollmentId",
                schema: "core",
                table: "MakeupEligibility",
                columns: new[] { "ExamId", "EnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarkEntry_Enrollment",
                schema: "core",
                table: "MarkEntry",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkEntry_Marksheet_Component_Enrollment",
                schema: "core",
                table: "MarkEntry",
                columns: new[] { "MarksheetId", "BlueprintComponentId", "EnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Marksheet_BlueprintId_SectionId",
                schema: "core",
                table: "Marksheet",
                columns: new[] { "BlueprintId", "SectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlan_FeeCategoryId",
                schema: "svc",
                table: "MealPlan",
                column: "FeeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanSubscription_ChargeId",
                schema: "svc",
                table: "MealPlanSubscription",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanSubscription_MealPlanId",
                schema: "svc",
                table: "MealPlanSubscription",
                column: "MealPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalCondition_MedicalFileId",
                schema: "svc",
                table: "MedicalCondition",
                column: "MedicalFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalFile_SchoolId_StudentId",
                schema: "svc",
                table: "MedicalFile",
                columns: new[] { "SchoolId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicationAuthorization_MedicalFileId",
                schema: "svc",
                table: "MedicationAuthorization",
                column: "MedicalFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberPolicy_SchoolId_MemberKind_StageId",
                schema: "svc",
                table: "MemberPolicy",
                columns: new[] { "SchoolId", "MemberKind", "StageId" },
                unique: true,
                filter: "[StageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Menu_SchoolId_Date",
                schema: "svc",
                table: "Menu",
                columns: new[] { "SchoolId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuLine_CafeteriaItemId",
                schema: "svc",
                table: "MenuLine",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuLine_MenuId",
                schema: "svc",
                table: "MenuLine",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_Merit_MeritTypeId",
                schema: "svc",
                table: "Merit",
                column: "MeritTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MeritType_BehaviorCodeId",
                schema: "svc",
                table: "MeritType",
                column: "BehaviorCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_SchoolId_Code_Version",
                schema: "core",
                table: "NumberingSeries",
                columns: new[] { "SchoolId", "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficialLetter_SchoolId_LetterNo",
                schema: "msg",
                table: "OfficialLetter",
                columns: new[] { "SchoolId", "LetterNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parent_SchoolId_ParentFileNo",
                schema: "ppl",
                table: "Parent",
                columns: new[] { "SchoolId", "ParentFileNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordHistory_UserAccountId_CreatedAtUtc",
                schema: "sec",
                table: "PasswordHistory",
                columns: new[] { "UserAccountId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocation_ChargeId",
                schema: "ppl",
                table: "PaymentAllocation",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocation_ReceiptId",
                schema: "ppl",
                table: "PaymentAllocation",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Pdc_ClearedReceiptId",
                schema: "ppl",
                table: "Pdc",
                column: "ClearedReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Pdc_PayerId",
                schema: "ppl",
                table: "Pdc",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodSlot_TimetableShapeId_DayOfWeek_SequenceNumber",
                schema: "core",
                table: "PeriodSlot",
                columns: new[] { "TimetableShapeId", "DayOfWeek", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permission_ModuleCode_ScreenCode_Action",
                schema: "sec",
                table: "Permission",
                columns: new[] { "ModuleCode", "ScreenCode", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Placement_RoomId",
                schema: "core",
                table: "Placement",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Placement_TimetableVersionId_PeriodSlotId",
                schema: "core",
                table: "Placement",
                columns: new[] { "TimetableVersionId", "PeriodSlotId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignment_PayerId",
                schema: "ppl",
                table: "PlanAssignment",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignment_PlanTemplateId",
                schema: "ppl",
                table: "PlanAssignment",
                column: "PlanTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAssignment_StudentId_AcademicYearId_FeeCategoryId",
                schema: "ppl",
                table: "PlanAssignment",
                columns: new[] { "StudentId", "AcademicYearId", "FeeCategoryId" },
                unique: true,
                filter: "[FeeCategoryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlanTemplate_FeeCategoryId",
                schema: "ppl",
                table: "PlanTemplate",
                column: "FeeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanTemplate_SchoolId_AcademicYearId",
                schema: "ppl",
                table: "PlanTemplate",
                columns: new[] { "SchoolId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_PointLedger_StudentId_AcademicYearId_TermId",
                schema: "svc",
                table: "PointLedger",
                columns: new[] { "StudentId", "AcademicYearId", "TermId" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceList_SchoolId_Version",
                schema: "svc",
                table: "PriceList",
                columns: new[] { "SchoolId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceListLine_PriceListId",
                schema: "svc",
                table: "PriceListLine",
                column: "PriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceListLine_StoreItemId",
                schema: "svc",
                table: "PriceListLine",
                column: "StoreItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Program_ActivityTypeId",
                schema: "ppl",
                table: "Program",
                column: "ActivityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollment_ProgramId_StudentId",
                schema: "ppl",
                table: "ProgramEnrollment",
                columns: new[] { "ProgramId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_PromiseToPay_InstallmentId",
                schema: "ppl",
                table: "PromiseToPay",
                column: "InstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionCriteria_GradeYearProfileId",
                schema: "core",
                table: "PromotionCriteria",
                column: "GradeYearProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Provider_SchoolId_Channel",
                schema: "msg",
                table: "Provider",
                columns: new[] { "SchoolId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_Qualification_EmployeeId",
                schema: "ppl",
                table: "Qualification",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingLog_TitleId",
                schema: "svc",
                table: "ReadingLog",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipt_Payer_IssuedAt",
                schema: "ppl",
                table: "Receipt",
                columns: new[] { "PayerId", "IssuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Receipt_SchoolId_ReceiptNo",
                schema: "ppl",
                table: "Receipt",
                columns: new[] { "SchoolId", "ReceiptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipt_TillSessionId",
                schema: "ppl",
                table: "Receipt",
                column: "TillSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Redemption_MealPlanSubscriptionId_Date",
                schema: "svc",
                table: "Redemption",
                columns: new[] { "MealPlanSubscriptionId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Redemption_SaleId",
                schema: "svc",
                table: "Redemption",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundVoucher_PayerId",
                schema: "ppl",
                table: "RefundVoucher",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundVoucher_SchoolId_VoucherNo",
                schema: "ppl",
                table: "RefundVoucher",
                columns: new[] { "SchoolId", "VoucherNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RenewalQueueItem_PriorGrantId_NewAcademicYearId",
                schema: "ppl",
                table: "RenewalQueueItem",
                columns: new[] { "PriorGrantId", "NewAcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinition_SchoolId_Code",
                schema: "core",
                table: "ReportDefinition",
                columns: new[] { "SchoolId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportExecution_ReportDefinitionId",
                schema: "ppl",
                table: "ReportExecution",
                column: "ReportDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSubscription_ReportDefinitionId_SubscriberUserId",
                schema: "ppl",
                table: "ReportSubscription",
                columns: new[] { "ReportDefinitionId", "SubscriberUserId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RescheduleCase_PlanAssignmentId",
                schema: "ppl",
                table: "RescheduleCase",
                column: "PlanAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_HeldCopyId",
                schema: "svc",
                table: "Reservation",
                column: "HeldCopyId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_TitleId_Status",
                schema: "svc",
                table: "Reservation",
                columns: new[] { "TitleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnExchange_CreditNoteId",
                schema: "svc",
                table: "ReturnExchange",
                column: "CreditNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnExchange_NewStoreVariantId",
                schema: "svc",
                table: "ReturnExchange",
                column: "NewStoreVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnExchange_StoreSaleLineId",
                schema: "svc",
                table: "ReturnExchange",
                column: "StoreSaleLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Role_SchoolId_Code",
                schema: "sec",
                table: "Role",
                columns: new[] { "SchoolId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignment_RoleId",
                schema: "sec",
                table: "RoleAssignment",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignment_UserAccountId_RoleId",
                schema: "sec",
                table: "RoleAssignment",
                columns: new[] { "UserAccountId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_PermissionId",
                schema: "sec",
                table: "RolePermission",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_RoleId_PermissionId",
                schema: "sec",
                table: "RolePermission",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolloverBatch_SchoolId_SourceAcademicYearId_TargetAcademicYearId",
                schema: "core",
                table: "RolloverBatch",
                columns: new[] { "SchoolId", "SourceAcademicYearId", "TargetAcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolloverBatch_SourceAcademicYearId",
                schema: "core",
                table: "RolloverBatch",
                column: "SourceAcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_RolloverBatch_TargetAcademicYearId",
                schema: "core",
                table: "RolloverBatch",
                column: "TargetAcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_RolloverStudentState_RolloverBatchId_StudentId",
                schema: "core",
                table: "RolloverStudentState",
                columns: new[] { "RolloverBatchId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolloverStudentState_SourceEnrollmentId",
                schema: "core",
                table: "RolloverStudentState",
                column: "SourceEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RolloverStudentState_StudentId",
                schema: "core",
                table: "RolloverStudentState",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_RolloverStudentState_TargetEnrollmentId",
                schema: "core",
                table: "RolloverStudentState",
                column: "TargetEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Room_FloorId",
                schema: "core",
                table: "Room",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "IX_Room_SchoolId_Code",
                schema: "core",
                table: "Room",
                columns: new[] { "SchoolId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomAvailabilityException_RoomId_StartDate",
                schema: "core",
                table: "RoomAvailabilityException",
                columns: new[] { "RoomId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomBooking_RoomId_StartUtc",
                schema: "core",
                table: "RoomBooking",
                columns: new[] { "RoomId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomFeature_RoomId_FeatureLookupId",
                schema: "core",
                table: "RoomFeature",
                columns: new[] { "RoomId", "FeatureLookupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Route_AttendantId",
                schema: "svc",
                table: "Route",
                column: "AttendantId");

            migrationBuilder.CreateIndex(
                name: "IX_Route_BusId",
                schema: "svc",
                table: "Route",
                column: "BusId");

            migrationBuilder.CreateIndex(
                name: "IX_Route_DriverId",
                schema: "svc",
                table: "Route",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Route_SchoolId_RouteNo",
                schema: "svc",
                table: "Route",
                columns: new[] { "SchoolId", "RouteNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteStop_RouteId_SequenceNumber",
                schema: "svc",
                table: "RouteStop",
                columns: new[] { "RouteId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteStop_ZoneFeeCategoryId",
                schema: "svc",
                table: "RouteStop",
                column: "ZoneFeeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteWaitlist_RouteId",
                schema: "svc",
                table: "RouteWaitlist",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteWaitlist_TransportSubscriptionId",
                schema: "svc",
                table: "RouteWaitlist",
                column: "TransportSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvent_TripId",
                schema: "svc",
                table: "SafetyEvent",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Sale_HolderKind_HolderId_AtUtc",
                schema: "svc",
                table: "Sale",
                columns: new[] { "HolderKind", "HolderId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Sale_TillSessionId",
                schema: "svc",
                table: "Sale",
                column: "TillSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLine_CafeteriaItemId",
                schema: "svc",
                table: "SaleLine",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLine_SaleId",
                schema: "svc",
                table: "SaleLine",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScaleBand_GradingScaleId",
                schema: "core",
                table: "ScaleBand",
                column: "GradingScaleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRevision_PlanAssignmentId",
                schema: "ppl",
                table: "ScheduleRevision",
                column: "PlanAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScholarshipProgram_DiscountTypeId",
                schema: "ppl",
                table: "ScholarshipProgram",
                column: "DiscountTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_School_SchoolGroupId",
                schema: "core",
                table: "School",
                column: "SchoolGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ScopeGrant_RoleAssignmentId",
                schema: "sec",
                table: "ScopeGrant",
                column: "RoleAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningResult_ScreeningCampaignId_StudentId",
                schema: "svc",
                table: "ScreeningResult",
                columns: new[] { "ScreeningCampaignId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Section_DefaultClassroomId",
                schema: "core",
                table: "Section",
                column: "DefaultClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_Section_GradeYearProfileId_NameEn",
                schema: "core",
                table: "Section",
                columns: new[] { "GradeYearProfileId", "NameEn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectionMembership_Enrollment_EffectiveFrom",
                schema: "core",
                table: "SectionMembership",
                columns: new[] { "EnrollmentId", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SectionMembership_EnrollmentId_Current",
                schema: "core",
                table: "SectionMembership",
                column: "EnrollmentId",
                unique: true,
                filter: "[EffectiveToUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SectionMembership_Section_EffectiveTo",
                schema: "core",
                table: "SectionMembership",
                columns: new[] { "SectionId", "EffectiveToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SectionMembership_SectionId",
                schema: "core",
                table: "SectionMembership",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Semester_AcademicYearId_SequenceNumber",
                schema: "core",
                table: "Semester",
                columns: new[] { "AcademicYearId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeriesState_NumberingSeriesId_ResetKey",
                schema: "core",
                table: "SeriesState",
                columns: new[] { "NumberingSeriesId", "ResetKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Session_OverrideRoomId",
                schema: "core",
                table: "Session",
                column: "OverrideRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Session_PlacementId_Date",
                schema: "core",
                table: "Session",
                columns: new[] { "PlacementId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Session_School_Date",
                schema: "core",
                table: "Session",
                columns: new[] { "SchoolId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Signatory_SchoolId_DocumentClassCode_EffectiveToUtc",
                schema: "core",
                table: "Signatory",
                columns: new[] { "SchoolId", "DocumentClassCode", "EffectiveToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendControl_SchoolId_StudentId",
                schema: "svc",
                table: "SpendControl",
                columns: new[] { "SchoolId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatementIssue_PayerId",
                schema: "ppl",
                table: "StatementIssue",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementIssue_SchoolId_StatementNo",
                schema: "ppl",
                table: "StatementIssue",
                columns: new[] { "SchoolId", "StatementNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovement_CafeteriaItemId",
                schema: "svc",
                table: "StockMovement",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeLine_CopyId",
                schema: "svc",
                table: "StocktakeLine",
                column: "CopyId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeLine_StocktakeSessionId_CopyId",
                schema: "svc",
                table: "StocktakeLine",
                columns: new[] { "StocktakeSessionId", "CopyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreAccountChargePolicy_SchoolId_Category",
                schema: "svc",
                table: "StoreAccountChargePolicy",
                columns: new[] { "SchoolId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreItem_FeeCategoryId",
                schema: "svc",
                table: "StoreItem",
                column: "FeeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreReturnPolicy_SchoolId_Category",
                schema: "svc",
                table: "StoreReturnPolicy",
                columns: new[] { "SchoolId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreSale_ChargeId",
                schema: "svc",
                table: "StoreSale",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreSale_PayerId",
                schema: "svc",
                table: "StoreSale",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreSale_ReceiptId",
                schema: "svc",
                table: "StoreSale",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreSale_TillSessionId",
                schema: "svc",
                table: "StoreSale",
                column: "TillSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreSaleLine_StoreSaleId",
                schema: "svc",
                table: "StoreSaleLine",
                column: "StoreSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreSaleLine_StoreVariantId",
                schema: "svc",
                table: "StoreSaleLine",
                column: "StoreVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreStockMovement_StoreVariantId",
                schema: "svc",
                table: "StoreStockMovement",
                column: "StoreVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Student_PrimaryId",
                schema: "ppl",
                table: "Student",
                columns: new[] { "SchoolId", "PrimaryIdTypeLookupId", "PrimaryIdNo" },
                unique: true,
                filter: "[PrimaryIdNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Student_SchoolId_StudentNo",
                schema: "ppl",
                table: "Student",
                columns: new[] { "SchoolId", "StudentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardianLink_ParentId",
                schema: "ppl",
                table: "StudentGuardianLink",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentGuardianLink_StudentId",
                schema: "ppl",
                table: "StudentGuardianLink",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Subject_DepartmentId",
                schema: "core",
                table: "Subject",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Subject_SchoolId_Code",
                schema: "core",
                table: "Subject",
                columns: new[] { "SchoolId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRule_SchoolId_EventCode_Channel",
                schema: "msg",
                table: "SubscriptionRule",
                columns: new[] { "SchoolId", "EventCode", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Substitution_SessionId",
                schema: "core",
                table: "Substitution",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignment_CurriculumOfferingId_SectionId",
                schema: "core",
                table: "TeacherAssignment",
                columns: new[] { "CurriculumOfferingId", "SectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignment_Offering_Section_Primary",
                schema: "core",
                table: "TeacherAssignment",
                columns: new[] { "CurriculumOfferingId", "SectionId", "Role" },
                unique: true,
                filter: "[EffectiveToUtc] IS NULL AND [Role] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignment_TeacherProfileId",
                schema: "core",
                table: "TeacherAssignment",
                column: "TeacherProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherProfile_EmployeeId",
                schema: "core",
                table: "TeacherProfile",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjectQualification_SubjectId",
                schema: "core",
                table: "TeacherSubjectQualification",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSubjectQualification_TeacherUserId_SubjectId_StageId",
                schema: "core",
                table: "TeacherSubjectQualification",
                columns: new[] { "TeacherUserId", "SubjectId", "StageId" },
                unique: true,
                filter: "[StageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Template_SchoolId_EventCode_Channel",
                schema: "msg",
                table: "Template",
                columns: new[] { "SchoolId", "EventCode", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateInstallment_PlanTemplateId_SequenceNumber",
                schema: "ppl",
                table: "TemplateInstallment",
                columns: new[] { "PlanTemplateId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateVersion_TemplateId_VersionNumber",
                schema: "msg",
                table: "TemplateVersion",
                columns: new[] { "TemplateId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Term_SemesterId_SequenceNumber",
                schema: "core",
                table: "Term",
                columns: new[] { "SemesterId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TermResult_Enrollment_Offering_Term",
                schema: "core",
                table: "TermResult",
                columns: new[] { "EnrollmentId", "CurriculumOfferingId", "TermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThreadMessage_ThreadId",
                schema: "msg",
                table: "ThreadMessage",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportSubscription_AmRouteStopId",
                schema: "svc",
                table: "TransportSubscription",
                column: "AmRouteStopId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportSubscription_ChargeId",
                schema: "svc",
                table: "TransportSubscription",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportSubscription_EnrollmentId",
                schema: "svc",
                table: "TransportSubscription",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportSubscription_PmRouteStopId",
                schema: "svc",
                table: "TransportSubscription",
                column: "PmRouteStopId");

            migrationBuilder.CreateIndex(
                name: "IX_Trip_RouteId_Date",
                schema: "svc",
                table: "Trip",
                columns: new[] { "RouteId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripLog_TripId_StudentId",
                schema: "svc",
                table: "TripLog",
                columns: new[] { "TripId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorEnrollment_UserAccountId_Method",
                schema: "sec",
                table: "TwoFactorEnrollment",
                columns: new[] { "UserAccountId", "Method" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccount_SchoolId_UserName",
                schema: "sec",
                table: "UserAccount",
                columns: new[] { "SchoolId", "UserName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLayout_UserAccountId_WidgetDefinitionId",
                schema: "core",
                table: "UserLayout",
                columns: new[] { "UserAccountId", "WidgetDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLayout_WidgetDefinitionId",
                schema: "core",
                table: "UserLayout",
                column: "WidgetDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSession_SessionToken",
                schema: "sec",
                table: "UserSession",
                column: "SessionToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSession_UserAccountId_RevokedAtUtc",
                schema: "sec",
                table: "UserSession",
                columns: new[] { "UserAccountId", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecord_MedicalFileId",
                schema: "svc",
                table: "VaccinationRecord",
                column: "MedicalFileId");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecord_VaccinationCampaignId",
                schema: "svc",
                table: "VaccinationRecord",
                column: "VaccinationCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationScheduleEntry_SchoolId_VaccineCode_DoseNumber",
                schema: "svc",
                table: "VaccinationScheduleEntry",
                columns: new[] { "SchoolId", "VaccineCode", "DoseNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Variant_SchoolId_Sku",
                schema: "svc",
                table: "Variant",
                columns: new[] { "SchoolId", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Variant_StoreItemId",
                schema: "svc",
                table: "Variant",
                column: "StoreItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationLog_CertificateIssueId",
                schema: "ppl",
                table: "VerificationLog",
                column: "CertificateIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_ViolationType_BehaviorCodeId",
                schema: "svc",
                table: "ViolationType",
                column: "BehaviorCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitingListEntry_ApplicationId",
                schema: "ppl",
                table: "WaitingListEntry",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitingListEntry_Profile_Rank",
                schema: "ppl",
                table: "WaitingListEntry",
                columns: new[] { "GradeYearProfileId", "OrderRank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Waiver_ChargeId",
                schema: "ppl",
                table: "Waiver",
                column: "ChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_Waiver_CreditNoteId",
                schema: "ppl",
                table: "Waiver",
                column: "CreditNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Wallet_SchoolId_HolderKind_HolderId",
                schema: "svc",
                table: "Wallet",
                columns: new[] { "SchoolId", "HolderKind", "HolderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedger_ReceiptId",
                schema: "svc",
                table: "WalletLedger",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedger_RefundVoucherId",
                schema: "svc",
                table: "WalletLedger",
                column: "RefundVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedger_Wallet_Id",
                schema: "svc",
                table: "WalletLedger",
                columns: new[] { "WalletId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WidgetDefinition_SchoolId_Code",
                schema: "core",
                table: "WidgetDefinition",
                columns: new[] { "SchoolId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinition_SchoolId_Code_Version",
                schema: "wf",
                table: "WorkflowDefinition",
                columns: new[] { "SchoolId", "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_CurrentStateId",
                schema: "wf",
                table: "WorkflowInstance",
                column: "CurrentStateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_EntityTypeName_EntityId",
                schema: "wf",
                table: "WorkflowInstance",
                columns: new[] { "EntityTypeName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_WorkflowDefinitionId",
                schema: "wf",
                table: "WorkflowInstance",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowState_WorkflowDefinitionId",
                schema: "wf",
                table: "WorkflowState",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStep_FromStateId",
                schema: "wf",
                table: "WorkflowStep",
                column: "FromStateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStep_ToStateId",
                schema: "wf",
                table: "WorkflowStep",
                column: "ToStateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStep_WorkflowInstanceId_OccurredAtUtc",
                schema: "wf",
                table: "WorkflowStep",
                columns: new[] { "WorkflowInstanceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransition_FromStateId",
                schema: "wf",
                table: "WorkflowTransition",
                column: "FromStateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransition_ToStateId",
                schema: "wf",
                table: "WorkflowTransition",
                column: "ToStateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransition_WorkflowDefinitionId",
                schema: "wf",
                table: "WorkflowTransition",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_YearResult_Enrollment_Year",
                schema: "core",
                table: "YearResult",
                columns: new[] { "EnrollmentId", "AcademicYearId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Achievement",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ActionApplied",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "ActivityAttendance",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ActivityConsentRecord",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ActivityTrip",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "AdministrationLog",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "AdmissionCampaign",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "AgedReceivablesSnapshot",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "Allergy",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Announcement",
                schema: "msg");

            migrationBuilder.DropTable(
                name: "AnomalyHit",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "Appeal",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "ApplicationAssessment",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "AttachmentVersion",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "AuditEntry",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "BackupVerificationRun",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "BehaviorContract",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "BudgetCounter",
                schema: "msg");

            migrationBuilder.DropTable(
                name: "BundleLine",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "BusDocument",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "CalendarDay",
                schema: "core");

            migrationBuilder.DropTable(
                name: "CalendarEvent",
                schema: "core");

            migrationBuilder.DropTable(
                name: "CalendarVersion",
                schema: "core");

            migrationBuilder.DropTable(
                name: "CarePlan",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "CaseStatement",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "CirculationEvent",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "ClinicVisit",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "CollectionCalendarSnapshot",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "CommunicationMatrix",
                schema: "msg");

            migrationBuilder.DropTable(
                name: "ConsentRecord",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Contract",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "CurriculumOffering",
                schema: "core");

            migrationBuilder.DropTable(
                name: "DailyAttendanceSummarySnapshot",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "Delivery",
                schema: "msg");

            migrationBuilder.DropTable(
                name: "DiagnosticsBundle",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "DiscountDocument",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "DunningEvent",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "EligibilityRule",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "EmergencyContact",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "EmployeeAssignment",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ExamAttendance",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ExamIncident",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ExposureNotice",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "FeeStructureLine",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "FineProposal",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "GateEvent",
                schema: "core");

            migrationBuilder.DropTable(
                name: "GlAccountMapping",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "GlJournalLine",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "HandoutRecord",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "HomeroomAssignment",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ImportBatch",
                schema: "core");

            migrationBuilder.DropTable(
                name: "InfectiousCase",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "InstallmentChargeLine",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "IntegrityCheckpoint",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "IntegrityVerificationRun",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "JobRun",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Justification",
                schema: "core");

            migrationBuilder.DropTable(
                name: "KeepApartPair",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "LadderStep",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "LayoutTemplateWidget",
                schema: "core");

            migrationBuilder.DropTable(
                name: "LeavePass",
                schema: "core");

            migrationBuilder.DropTable(
                name: "LegalHold",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "LicenseState",
                schema: "core");

            migrationBuilder.DropTable(
                name: "LoginAttempt",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "LookupValue",
                schema: "core");

            migrationBuilder.DropTable(
                name: "MaintenanceWindow",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "MakeupEligibility",
                schema: "core");

            migrationBuilder.DropTable(
                name: "MarkEntry",
                schema: "core");

            migrationBuilder.DropTable(
                name: "MedicalCondition",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "MemberPolicy",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "MenuLine",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Merit",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "OfficialLetter",
                schema: "msg");

            migrationBuilder.DropTable(
                name: "Parent",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ParentMeeting",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "PasswordHistory",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "PaymentAllocation",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "PeriodSlot",
                schema: "core");

            migrationBuilder.DropTable(
                name: "PointLedger",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "PriceListLine",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "PromiseToPay",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "PromotionCriteria",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Provider",
                schema: "msg");

            migrationBuilder.DropTable(
                name: "PurgeExecution",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Qualification",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ReadingLog",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Redemption",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "RenewalQueueItem",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ReportExecution",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ReportSubscription",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "RescheduleCase",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "Reservation",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "RestoreCase",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "ReturnExchange",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "RolePermission",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "RolloverStudentState",
                schema: "core");

            migrationBuilder.DropTable(
                name: "RoomAvailabilityException",
                schema: "core");

            migrationBuilder.DropTable(
                name: "RoomBooking",
                schema: "core");

            migrationBuilder.DropTable(
                name: "RoomFeature",
                schema: "core");

            migrationBuilder.DropTable(
                name: "RouteWaitlist",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "SafetyEvent",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "SaleLine",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "ScaleBand",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ScheduleRevision",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "School",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ScopeGrant",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "ScreeningResult",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "SectionMembership",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SeriesState",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Signatory",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SnapshotEvent",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "SpendControl",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "StatementIssue",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "StockMovement",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "StocktakeLine",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "StoreAccountChargePolicy",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "StoreReturnPolicy",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "StoreStockMovement",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "StudentGuardianLink",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "SubscriptionRule",
                schema: "msg");

            migrationBuilder.DropTable(
                name: "Substitution",
                schema: "core");

            migrationBuilder.DropTable(
                name: "TeacherAssignment",
                schema: "core");

            migrationBuilder.DropTable(
                name: "TeacherSubjectQualification",
                schema: "core");

            migrationBuilder.DropTable(
                name: "TemplateInstallment",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "Term",
                schema: "core");

            migrationBuilder.DropTable(
                name: "TermResult",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ThreadMessage",
                schema: "msg");

            migrationBuilder.DropTable(
                name: "TripLog",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "TwoFactorEnrollment",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "UserLayout",
                schema: "core");

            migrationBuilder.DropTable(
                name: "UserSession",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "VaccinationRecord",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "VaccinationScheduleEntry",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "VerificationLog",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "WaitingListEntry",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "Waiver",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "WalletLedger",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "WorkflowStep",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "WorkflowTransition",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "YearResult",
                schema: "core");

            migrationBuilder.DropTable(
                name: "CompetitionEvent",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ActivitySession",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ProgramEnrollment",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "MedicationAuthorization",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "AnomalyRule",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "Attachment",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "BackupRun",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Case",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "TemplateVersion",
                schema: "msg");

            migrationBuilder.DropTable(
                name: "OrgUnit",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ExamSitting",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Loan",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "GlExportBatch",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "BundleAssignment",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "DistributionSession",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "JobDefinition",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "AttendanceDay",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ConsequenceType",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "LayoutTemplate",
                schema: "core");

            migrationBuilder.DropTable(
                name: "LookupCategory",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Marksheet",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Menu",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "MeritType",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "TimetableShape",
                schema: "core");

            migrationBuilder.DropTable(
                name: "PriceList",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Installment",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "MealPlanSubscription",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "DiscountGrant",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ReportDefinition",
                schema: "core");

            migrationBuilder.DropTable(
                name: "StoreSaleLine",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Permission",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "RolloverBatch",
                schema: "core");

            migrationBuilder.DropTable(
                name: "TransportSubscription",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Sale",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "SchoolGroup",
                schema: "core");

            migrationBuilder.DropTable(
                name: "RoleAssignment",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "ScreeningCampaign",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Section",
                schema: "core");

            migrationBuilder.DropTable(
                name: "NumberingSeries",
                schema: "core");

            migrationBuilder.DropTable(
                name: "CafeteriaItem",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "StocktakeSession",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Session",
                schema: "core");

            migrationBuilder.DropTable(
                name: "TeacherProfile",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Subject",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Semester",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Thread",
                schema: "msg");

            migrationBuilder.DropTable(
                name: "Trip",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "WidgetDefinition",
                schema: "core");

            migrationBuilder.DropTable(
                name: "VaccinationCampaign",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "CertificateIssue",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "Application",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "RefundVoucher",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "Wallet",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "WorkflowInstance",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "Program",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "MedicalFile",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "DocumentType",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "BackupPolicy",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "Incident",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Template",
                schema: "msg");

            migrationBuilder.DropTable(
                name: "Exam",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Copy",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "CreditNote",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "Bundle",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Pdc",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "PlanAssignment",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "MealPlan",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "ScholarshipProgram",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "StoreSale",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Variant",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Enrollment",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "RouteStop",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Role",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "UserAccount",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "Placement",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Employee",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "Department",
                schema: "core");

            migrationBuilder.DropTable(
                name: "CertificateRequest",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "WorkflowState",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "ActivityType",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "ViolationType",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "BlueprintComponent",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ExamRound",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ExamType",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Title",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "PlanTemplate",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "DiscountType",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "Charge",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "Receipt",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "StoreItem",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "GradeYearProfile",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Student",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "Route",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Room",
                schema: "core");

            migrationBuilder.DropTable(
                name: "TimetableVersion",
                schema: "core");

            migrationBuilder.DropTable(
                name: "CertificateType",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "WorkflowDefinition",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "BehaviorCode",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Blueprint",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Payer",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "TillSession",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "FeeCategory",
                schema: "ppl");

            migrationBuilder.DropTable(
                name: "AcademicYear",
                schema: "core");

            migrationBuilder.DropTable(
                name: "GradeLevel",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Bus",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "TransportStaff",
                schema: "svc");

            migrationBuilder.DropTable(
                name: "Floor",
                schema: "core");

            migrationBuilder.DropTable(
                name: "GradingScale",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Stage",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Building",
                schema: "core");
        }
    }
}
