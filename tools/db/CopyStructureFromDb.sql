/*
================================================================================
  SMS — نسخ الهيكل المكاني والأكاديمي بين قاعدتين
        Copy the facility and academic structure between two databases
================================================================================

  ما يفعله هذا السكريبت / What this script does
  ---------------------------------------------
  يمسح من القاعدة الهدف المباني والطوابق والقاعات والمراحل والصفوف والشعب،
  ثم ينسخها كما هي من القاعدة المصدر مع الإبقاء على نفس المعرِّفات (Id).

  Deletes the buildings, floors, rooms, stages, grade levels and sections from
  the TARGET database, then copies them from the SOURCE database, preserving
  the original Id of every row.

  الجداول المنسوخة / TABLES COPIED   (بالترتيب / in this order)
      core.Building              المباني
      core.Floor                 الطوابق
      core.Room                  القاعات الدراسية
      core.RoomFeature           خصائص القاعة        (صفة للقاعة، تسقط بسقوطها)
      core.Stage                 المراحل            (لازمة لأن الصف يشير إليها)
      core.GradeLevel            الصفوف
      core.GradeYearProfile      ملف الصف للسنة      (لازم لأن الشعبة تشير إليه)
      core.CurriculumOffering    خطة المنهج للصف     (تسقط بسقوط ملف الصف)
      core.Section               الشعب

  Stage and GradeYearProfile are not optional extras: GradeLevel.StageId and
  Section.GradeYearProfileId are NOT NULL foreign keys, so the two carrier
  tables have to travel with the five the request names.

  RoomFeature and CurriculumOffering are here for a different reason: deleting
  a Room or a GradeYearProfile forces them to go with it, so copying the parent
  without them would silently lose rows the caller never asked to lose. They
  are copied so a re-run leaves the target equal to the source, not poorer.

  كيف تُعالَج المراجع الخارجية / How outside references are resolved
  ------------------------------------------------------------------
  المعرِّفات داخل هذه الجداول السبعة تُنسخ كما هي (IDENTITY_INSERT)، فتبقى
  الروابط بينها سليمة دون إعادة ترقيم. أما ما يشير خارجها فيُعاد ربطه بالمعنى
  لا بالرقم:

  Ids INSIDE these nine tables are copied verbatim (IDENTITY_INSERT), so every
  link between them survives with no re-numbering. References pointing OUTSIDE
  them are re-resolved by meaning, never by number:

      SchoolId                  -> @TargetSchoolId              (رقم المدرسة الهدف)
      AcademicYearId            -> matched on AcademicYear.LabelEn
      Room.RoomTypeLookupId     -> matched on (LookupCategory.Code, LookupValue.Code)
      RoomFeature.FeatureLookupId-> matched on (LookupCategory.Code, LookupValue.Code)
      GradeYearProfile
        .CurriculumLookupValueId-> matched on (LookupCategory.Code, LookupValue.Code)
      CurriculumOffering
        .SubjectId              -> matched on Subject.Code

  المواد نفسها لا تُنسخ — تبقى كما هي في القاعدة الهدف. إن أشارت خطة منهج في
  المصدر إلى مادة برمز غير موجود في الهدف يتوقف السكريبت ويعرض الرموز الناقصة.
  Subjects themselves are NOT copied — the target keeps its own catalogue. If a
  source offering names a subject Code the target does not have, the script
  aborts and lists the missing codes rather than dropping the offering.

  ملاحظات مهمة / Important notes
  ------------------------------
  ١) السكريبت يعمل بوضع المعاينة افتراضيًا (@WhatIf = 1): يعرض ما سيُحذف وما
     سيُنسخ ولا يغيّر شيئًا. اجعله 0 للتنفيذ الفعلي.
     Runs in preview mode by default. Set @WhatIf = 0 to actually write.

  ٢) التنفيذ كله داخل معاملة واحدة مع XACT_ABORT، فأي خطأ يتراجع بالكامل.
     Everything runs in one transaction with XACT_ABORT; any error rolls the
     whole copy back and the target keeps its original rows.

  ٣) تسجيلات الطلبة (ppl.Enrollment) تشير إلى core.GradeYearProfile. إن وُجد أي
     تسجيل في القاعدة الهدف يتوقف السكريبت ولا يحذف شيئًا — بيانات الطلبة لا
     تُمسح ضمنًا. أفرغ التسجيلات بنفسك أولًا إن كان ذلك مقصودًا.
     Student enrollments reference core.GradeYearProfile. If the target holds
     ANY enrollment the script aborts and deletes nothing — student data is
     never dropped as a side effect. Clear enrollments deliberately first.

  ٤) الجداول التابعة التي لا يَنسخها السكريبت (الحجوزات، جلسات الامتحان،
     الحصص، عضويات الشعب، استثناءات إتاحة القاعة) تمنع الحذف افتراضيًا، لأن
     حذفها خسارة صافية. اجعل @PurgeDependents = 1 لحذفها معها.
     Dependent tables the script does NOT copy (bookings, exam sittings,
     sessions, section memberships, room availability exceptions) block the
     delete by default, because removing them would be a net loss. Set
     @PurgeDependents = 1 to remove them too.
     خصائص القاعة وخطة المنهج لا تمنع، لأنهما تُنسخان فتُستبدلان لا تُفقدان.
     RoomFeature and CurriculumOffering do NOT block: they are deleted and then
     re-copied from the source, so they are replaced rather than lost.

  ٥) انحراف مقصود عن BR-GLB-007 / A deliberate deviation from BR-GLB-007:
     هذه الجداول موسومة [Audited]، والتدقيق يُكتب داخل SmsDbContext.SaveChanges
     لا في قاعدة البيانات. النسخ بـ SQL خام يتجاوز AuditCaptor، فلن تُكتب أي
     صفوف في aud.AuditEntry لهذه العملية. أعمدة CreatedBy/CreatedAt تُنقل كما
     هي من المصدر. هذه أداة ترحيل بيانات، لا مسار عمل داخل المنتج.
     These tables are [Audited], but audit rows are written by AuditCaptor
     inside SmsDbContext.SaveChanges, not by the database. A raw SQL copy
     bypasses it, so NO aud.AuditEntry rows are written for this operation.
     CreatedBy/CreatedAt are carried over from the source as-is. This is a
     data-migration tool, not a path through the product.

  ٦) هذا السكريبت لا يُنشئ نسخة احتياطية. خُذ نسخة قبل التنفيذ:
     This script takes no backup. Take one first:
         BACKUP DATABASE [Sms] TO DISK = N'C:\Temp\Sms_before_copy.bak' WITH INIT;

  الاستخدام / Usage
  -----------------
      -- معاينة أولًا / preview first (@WhatIf = 1, the default)
      sqlcmd -S .\SQLEXPRESS -E -d Sms -f 65001 -i tools\db\CopyStructureFromDb.sql

      -- ثم التنفيذ بعد ضبط @WhatIf = 0 في قسم الإعدادات أدناه
      -- then run for real after setting @WhatIf = 0 in the Settings block below

  يعمل أيضًا من SSMS مباشرة (لا يستعمل متغيّرات sqlcmd).
  Runs from SSMS as-is — it uses no sqlcmd variables; the source database name
  is a plain DECLARE and reaches the source through one staging block.
================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* المخطط يستعمل فهارس مُصفّاة (HasFilter)، وأي DELETE عليها يفشل بالخطأ 1934
   ما لم يكن QUOTED_IDENTIFIER مفعّلًا. SSMS يفعّله افتراضيًا، أما sqlcmd فلا.
   The model uses filtered indexes; DELETE against them fails with error 1934
   unless QUOTED_IDENTIFIER is ON. SSMS defaults it ON, sqlcmd defaults it OFF. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

/*------------------------------------------------------------------ الإعدادات
  Settings — راجعها قبل كل تشغيل / review before every run.
--------------------------------------------------------------------------- */
DECLARE @SourceDb             sysname = N'sms1';  -- القاعدة المصدر / copy FROM
DECLARE @ConfirmDatabase      sysname = N'Sms';   -- القاعدة الهدف / copy INTO
DECLARE @WhatIf               bit     = 1;        -- 1 = معاينة فقط، 0 = تنفيذ فعلي
DECLARE @SourceSchoolId       int     = 0;        -- 0 = اكتشاف تلقائي / auto-detect
DECLARE @TargetSchoolId       int     = 0;        -- 0 = اكتشاف تلقائي / auto-detect
DECLARE @CreateMissingLookups bit     = 1;        -- 1 = أنشئ القوائم المرجعية الناقصة
DECLARE @PurgeDependents      bit     = 0;        -- 1 = احذف الجداول التابعة أيضًا
DECLARE @ResetIdentity        bit     = 1;        -- 1 = أعِد ضبط عدّادات IDENTITY

/*--------------------------------------------------------------- حارس القاعدة
  Guards: refuse to run against the wrong database, or with no real source.
--------------------------------------------------------------------------- */
IF DB_NAME() <> @ConfirmDatabase
BEGIN
    RAISERROR(N'ABORT: connected to [%s] but @ConfirmDatabase is [%s]. Nothing was changed.',
              16, 1, @@SERVERNAME, @ConfirmDatabase);
    RETURN;
END;

IF DB_ID(@SourceDb) IS NULL
BEGIN
    RAISERROR(N'ABORT: source database [%s] does not exist on this server. Nothing was changed.',
              16, 1, @SourceDb);
    RETURN;
END;

IF @SourceDb = DB_NAME()
BEGIN
    RAISERROR(N'ABORT: @SourceDb and @ConfirmDatabase are both [%s]. Nothing was changed.',
              16, 1, @SourceDb);
    RETURN;
END;

DECLARE @src nvarchar(300) = QUOTENAME(@SourceDb);
DECLARE @missingTable sysname = NULL;

SELECT TOP (1) @missingTable = t.n
FROM (VALUES (N'School'), (N'AcademicYear'), (N'LookupCategory'), (N'LookupValue'),
             (N'Subject'),
             (N'Building'), (N'Floor'), (N'Room'), (N'RoomFeature'),
             (N'Stage'), (N'GradeLevel'), (N'GradeYearProfile'),
             (N'CurriculumOffering'), (N'Section')) AS t(n)
WHERE OBJECT_ID(@src + N'.core.' + QUOTENAME(t.n)) IS NULL;

IF @missingTable IS NOT NULL
BEGIN
    RAISERROR(N'ABORT: source database [%s] has no core.%s — it is not an SMS database. Nothing was changed.',
              16, 1, @SourceDb, @missingTable);
    RETURN;
END;

/*------------------------------------------------------- تجهيز جداول المصدر
  Stage the source rows into #temp tables through one dynamic block, so every
  statement after this point is plain static SQL against local tables. Local
  #temp tables created here are visible inside sp_executesql (same session).
--------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#SrcSchool')   IS NOT NULL DROP TABLE #SrcSchool;
IF OBJECT_ID('tempdb..#SrcYear')     IS NOT NULL DROP TABLE #SrcYear;
IF OBJECT_ID('tempdb..#SrcLookup')   IS NOT NULL DROP TABLE #SrcLookup;
IF OBJECT_ID('tempdb..#SrcBuilding') IS NOT NULL DROP TABLE #SrcBuilding;
IF OBJECT_ID('tempdb..#SrcFloor')    IS NOT NULL DROP TABLE #SrcFloor;
IF OBJECT_ID('tempdb..#SrcRoom')     IS NOT NULL DROP TABLE #SrcRoom;
IF OBJECT_ID('tempdb..#SrcFeature')  IS NOT NULL DROP TABLE #SrcFeature;
IF OBJECT_ID('tempdb..#SrcStage')    IS NOT NULL DROP TABLE #SrcStage;
IF OBJECT_ID('tempdb..#SrcGrade')    IS NOT NULL DROP TABLE #SrcGrade;
IF OBJECT_ID('tempdb..#SrcProfile')  IS NOT NULL DROP TABLE #SrcProfile;
IF OBJECT_ID('tempdb..#SrcOffering') IS NOT NULL DROP TABLE #SrcOffering;
IF OBJECT_ID('tempdb..#SrcSection')  IS NOT NULL DROP TABLE #SrcSection;
IF OBJECT_ID('tempdb..#SrcSubject')  IS NOT NULL DROP TABLE #SrcSubject;
IF OBJECT_ID('tempdb..#YearMap')     IS NOT NULL DROP TABLE #YearMap;
IF OBJECT_ID('tempdb..#LookupMap')   IS NOT NULL DROP TABLE #LookupMap;
IF OBJECT_ID('tempdb..#SubjectMap')  IS NOT NULL DROP TABLE #SubjectMap;

CREATE TABLE #SrcSchool  (Id int PRIMARY KEY);

CREATE TABLE #SrcYear    (Id int PRIMARY KEY, SchoolId int NOT NULL,
                          LabelEn nvarchar(100) NOT NULL, LabelAr nvarchar(100) NOT NULL);

CREATE TABLE #SrcLookup  (Id int PRIMARY KEY, SchoolId int NOT NULL,
                          CategoryCode nvarchar(50) NOT NULL, CatNameAr nvarchar(200) NOT NULL,
                          CatNameEn nvarchar(200) NOT NULL, CatTier smallint NOT NULL,
                          ValueCode nvarchar(50) NOT NULL, ValNameAr nvarchar(200) NOT NULL,
                          ValNameEn nvarchar(200) NOT NULL, SortOrder int NOT NULL,
                          IsActive bit NOT NULL);

CREATE TABLE #SrcBuilding(Id int PRIMARY KEY, SchoolId int NOT NULL,
                          NameAr nvarchar(100) NOT NULL, NameEn nvarchar(100) NOT NULL,
                          IsActive bit NOT NULL, CreatedByUserId int NOT NULL,
                          CreatedAtUtc datetime2 NOT NULL, ModifiedByUserId int NULL,
                          ModifiedAtUtc datetime2 NULL);

CREATE TABLE #SrcFloor   (Id int PRIMARY KEY, SchoolId int NOT NULL, BuildingId int NOT NULL,
                          NameAr nvarchar(100) NOT NULL, NameEn nvarchar(100) NOT NULL,
                          SequenceOrder int NOT NULL, IsActive bit NOT NULL,
                          CreatedByUserId int NOT NULL, CreatedAtUtc datetime2 NOT NULL,
                          ModifiedByUserId int NULL, ModifiedAtUtc datetime2 NULL);

CREATE TABLE #SrcRoom    (Id int PRIMARY KEY, SchoolId int NOT NULL, FloorId int NOT NULL,
                          Code nvarchar(20) NOT NULL, NameAr nvarchar(100) NOT NULL,
                          NameEn nvarchar(100) NOT NULL, RoomTypeLookupId int NOT NULL,
                          StandardCapacity int NOT NULL, ExamCapacity int NOT NULL,
                          WingTag smallint NOT NULL, IsActive bit NOT NULL,
                          CreatedByUserId int NOT NULL, CreatedAtUtc datetime2 NOT NULL,
                          ModifiedByUserId int NULL, ModifiedAtUtc datetime2 NULL);

CREATE TABLE #SrcFeature (Id int PRIMARY KEY, SchoolId int NOT NULL, RoomId int NOT NULL,
                          FeatureLookupId int NOT NULL,
                          CreatedByUserId int NOT NULL, CreatedAtUtc datetime2 NOT NULL,
                          ModifiedByUserId int NULL, ModifiedAtUtc datetime2 NULL);

CREATE TABLE #SrcSubject (Id int PRIMARY KEY, SchoolId int NOT NULL, Code nvarchar(20) NOT NULL);

CREATE TABLE #SrcStage   (Id int PRIMARY KEY, SchoolId int NOT NULL,
                          NameAr nvarchar(100) NOT NULL, NameEn nvarchar(100) NOT NULL,
                          SequenceOrder int NOT NULL, DefaultGenderPolicy smallint NOT NULL,
                          IsActive bit NOT NULL, CreatedByUserId int NOT NULL,
                          CreatedAtUtc datetime2 NOT NULL, ModifiedByUserId int NULL,
                          ModifiedAtUtc datetime2 NULL);

CREATE TABLE #SrcGrade   (Id int PRIMARY KEY, SchoolId int NOT NULL, StageId int NOT NULL,
                          Code nvarchar(20) NOT NULL, NameAr nvarchar(100) NOT NULL,
                          NameEn nvarchar(100) NOT NULL, SequenceOrder int NOT NULL,
                          PromotionTargetGradeLevelId int NULL, IsGraduating bit NOT NULL,
                          IsActive bit NOT NULL, CreatedByUserId int NOT NULL,
                          CreatedAtUtc datetime2 NOT NULL, ModifiedByUserId int NULL,
                          ModifiedAtUtc datetime2 NULL);

CREATE TABLE #SrcProfile (Id int PRIMARY KEY, SchoolId int NOT NULL, AcademicYearId int NOT NULL,
                          GradeLevelId int NOT NULL, CurriculumLookupValueId int NULL,
                          GenderPolicy smallint NOT NULL, MinAgeAtCutoff decimal(4,2) NULL,
                          MaxAgeAtCutoff decimal(4,2) NULL, AgeCutoffDate datetime2 NULL,
                          TargetSections int NOT NULL, TargetSectionSize int NOT NULL,
                          IsActive bit NOT NULL, CreatedByUserId int NOT NULL,
                          CreatedAtUtc datetime2 NOT NULL, ModifiedByUserId int NULL,
                          ModifiedAtUtc datetime2 NULL);

CREATE TABLE #SrcOffering(Id int PRIMARY KEY, SchoolId int NOT NULL, AcademicYearId int NOT NULL,
                          GradeYearProfileId int NOT NULL, SubjectId int NOT NULL,
                          WeeklyPeriods int NOT NULL, IsAssessable bit NOT NULL,
                          GpaWeight decimal(6,3) NOT NULL, IsElective bit NOT NULL,
                          ElectiveGroupTag nvarchar(30) NULL,
                          EffectiveFromUtc datetime2 NOT NULL, EffectiveToUtc datetime2 NULL,
                          CreatedByUserId int NOT NULL, CreatedAtUtc datetime2 NOT NULL,
                          ModifiedByUserId int NULL, ModifiedAtUtc datetime2 NULL);

CREATE TABLE #SrcSection (Id int PRIMARY KEY, SchoolId int NOT NULL, AcademicYearId int NOT NULL,
                          GradeYearProfileId int NOT NULL, NameAr nvarchar(60) NOT NULL,
                          NameEn nvarchar(60) NOT NULL, Capacity int NOT NULL,
                          GenderPolicy smallint NOT NULL, DefaultClassroomId int NULL,
                          Status smallint NOT NULL, CreatedByUserId int NOT NULL,
                          CreatedAtUtc datetime2 NOT NULL, ModifiedByUserId int NULL,
                          ModifiedAtUtc datetime2 NULL);

DECLARE @stage nvarchar(max) = N'
INSERT INTO #SrcSchool (Id) SELECT Id FROM ' + @src + N'.core.School;

INSERT INTO #SrcYear (Id, SchoolId, LabelEn, LabelAr)
    SELECT Id, SchoolId, LabelEn, LabelAr FROM ' + @src + N'.core.AcademicYear;

INSERT INTO #SrcLookup (Id, SchoolId, CategoryCode, CatNameAr, CatNameEn, CatTier,
                        ValueCode, ValNameAr, ValNameEn, SortOrder, IsActive)
    SELECT lv.Id, lv.SchoolId, lc.Code, lc.NameAr, lc.NameEn, lc.Tier,
           lv.Code, lv.NameAr, lv.NameEn, lv.SortOrder, lv.IsActive
    FROM ' + @src + N'.core.LookupValue lv
    JOIN ' + @src + N'.core.LookupCategory lc ON lc.Id = lv.LookupCategoryId;

INSERT INTO #SrcBuilding SELECT Id, SchoolId, NameAr, NameEn, IsActive,
       CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM ' + @src + N'.core.Building;

INSERT INTO #SrcFloor SELECT Id, SchoolId, BuildingId, NameAr, NameEn, SequenceOrder, IsActive,
       CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM ' + @src + N'.core.Floor;

INSERT INTO #SrcRoom SELECT Id, SchoolId, FloorId, Code, NameAr, NameEn, RoomTypeLookupId,
       StandardCapacity, ExamCapacity, WingTag, IsActive,
       CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM ' + @src + N'.core.Room;

INSERT INTO #SrcFeature SELECT Id, SchoolId, RoomId, FeatureLookupId,
       CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM ' + @src + N'.core.RoomFeature;

INSERT INTO #SrcSubject (Id, SchoolId, Code) SELECT Id, SchoolId, Code FROM ' + @src + N'.core.Subject;

INSERT INTO #SrcStage SELECT Id, SchoolId, NameAr, NameEn, SequenceOrder, DefaultGenderPolicy,
       IsActive, CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM ' + @src + N'.core.Stage;

INSERT INTO #SrcGrade SELECT Id, SchoolId, StageId, Code, NameAr, NameEn, SequenceOrder,
       PromotionTargetGradeLevelId, IsGraduating, IsActive,
       CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM ' + @src + N'.core.GradeLevel;

INSERT INTO #SrcProfile SELECT Id, SchoolId, AcademicYearId, GradeLevelId, CurriculumLookupValueId,
       GenderPolicy, MinAgeAtCutoff, MaxAgeAtCutoff, AgeCutoffDate, TargetSections,
       TargetSectionSize, IsActive, CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM ' + @src + N'.core.GradeYearProfile;

INSERT INTO #SrcOffering SELECT Id, SchoolId, AcademicYearId, GradeYearProfileId, SubjectId,
       WeeklyPeriods, IsAssessable, GpaWeight, IsElective, ElectiveGroupTag,
       EffectiveFromUtc, EffectiveToUtc,
       CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM ' + @src + N'.core.CurriculumOffering;

INSERT INTO #SrcSection SELECT Id, SchoolId, AcademicYearId, GradeYearProfileId, NameAr, NameEn,
       Capacity, GenderPolicy, DefaultClassroomId, Status,
       CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM ' + @src + N'.core.Section;
';

EXEC sp_executesql @stage;

/*--------------------------------------------------------- تحديد رقم المدرسة
  Resolve the school on each side.
--------------------------------------------------------------------------- */
/* RAISERROR لا يقبل استعلامًا فرعيًا كوسيط / RAISERROR takes no subquery argument */
DECLARE @schoolCount int;

IF @SourceSchoolId = 0
BEGIN
    SET @schoolCount = (SELECT COUNT(*) FROM #SrcSchool);
    IF @schoolCount <> 1
    BEGIN
        RAISERROR(N'ABORT: source [%s] holds %d schools. Set @SourceSchoolId explicitly. Nothing was changed.',
                  16, 1, @SourceDb, @schoolCount);
        RETURN;
    END;
    SELECT @SourceSchoolId = Id FROM #SrcSchool;
END;
ELSE IF NOT EXISTS (SELECT 1 FROM #SrcSchool WHERE Id = @SourceSchoolId)
BEGIN
    RAISERROR(N'ABORT: source [%s] has no school with Id %d. Nothing was changed.',
              16, 1, @SourceDb, @SourceSchoolId);
    RETURN;
END;

IF @TargetSchoolId = 0
BEGIN
    SET @schoolCount = (SELECT COUNT(*) FROM core.School);
    IF @schoolCount <> 1
    BEGIN
        RAISERROR(N'ABORT: target holds %d schools. Set @TargetSchoolId explicitly. Nothing was changed.',
                  16, 1, @schoolCount);
        RETURN;
    END;
    SELECT @TargetSchoolId = Id FROM core.School;
END;
ELSE IF NOT EXISTS (SELECT 1 FROM core.School WHERE Id = @TargetSchoolId)
BEGIN
    RAISERROR(N'ABORT: target has no school with Id %d. Nothing was changed.', 16, 1, @TargetSchoolId);
    RETURN;
END;

/* من هنا فصاعدًا لا شيء خارج المدرسة المصدر / narrow every staged table to the source school */
DELETE FROM #SrcBuilding WHERE SchoolId <> @SourceSchoolId;
DELETE FROM #SrcFloor    WHERE SchoolId <> @SourceSchoolId;
DELETE FROM #SrcRoom     WHERE SchoolId <> @SourceSchoolId;
DELETE FROM #SrcFeature  WHERE SchoolId <> @SourceSchoolId;
DELETE FROM #SrcStage    WHERE SchoolId <> @SourceSchoolId;
DELETE FROM #SrcGrade    WHERE SchoolId <> @SourceSchoolId;
DELETE FROM #SrcProfile  WHERE SchoolId <> @SourceSchoolId;
DELETE FROM #SrcOffering WHERE SchoolId <> @SourceSchoolId;
DELETE FROM #SrcSection  WHERE SchoolId <> @SourceSchoolId;

/* صفوف يتيمة بعد التضييق: أب خارج المدرسة المصدر / orphans after narrowing */
DELETE FROM #SrcFloor    WHERE BuildingId         NOT IN (SELECT Id FROM #SrcBuilding);
DELETE FROM #SrcRoom     WHERE FloorId            NOT IN (SELECT Id FROM #SrcFloor);
DELETE FROM #SrcFeature  WHERE RoomId             NOT IN (SELECT Id FROM #SrcRoom);
DELETE FROM #SrcGrade    WHERE StageId            NOT IN (SELECT Id FROM #SrcStage);
DELETE FROM #SrcProfile  WHERE GradeLevelId       NOT IN (SELECT Id FROM #SrcGrade);
DELETE FROM #SrcOffering WHERE GradeYearProfileId NOT IN (SELECT Id FROM #SrcProfile);
DELETE FROM #SrcSection  WHERE GradeYearProfileId NOT IN (SELECT Id FROM #SrcProfile);

UPDATE #SrcGrade   SET PromotionTargetGradeLevelId = NULL
    WHERE PromotionTargetGradeLevelId IS NOT NULL
      AND PromotionTargetGradeLevelId NOT IN (SELECT Id FROM #SrcGrade);
UPDATE #SrcSection SET DefaultClassroomId = NULL
    WHERE DefaultClassroomId IS NOT NULL
      AND DefaultClassroomId NOT IN (SELECT Id FROM #SrcRoom);

/*---------------------------------------------------------- ربط السنة الدراسية
  Map academic years by LabelEn — the Id means nothing across databases.
--------------------------------------------------------------------------- */
CREATE TABLE #YearMap (SrcId int PRIMARY KEY, TgtId int NOT NULL);

INSERT INTO #YearMap (SrcId, TgtId)
SELECT sy.Id, ty.Id
FROM #SrcYear sy
JOIN core.AcademicYear ty ON ty.LabelEn = sy.LabelEn AND ty.SchoolId = @TargetSchoolId
WHERE sy.SchoolId = @SourceSchoolId;

IF EXISTS (SELECT 1 FROM #SrcProfile  p WHERE p.AcademicYearId NOT IN (SELECT SrcId FROM #YearMap))
    OR EXISTS (SELECT 1 FROM #SrcSection  s WHERE s.AcademicYearId NOT IN (SELECT SrcId FROM #YearMap))
    OR EXISTS (SELECT 1 FROM #SrcOffering o WHERE o.AcademicYearId NOT IN (SELECT SrcId FROM #YearMap))
BEGIN
    PRINT N'--- سنوات دراسية غير موجودة في القاعدة الهدف / academic years missing in the target ---';
    SELECT DISTINCT N'MISSING AcademicYear.LabelEn' AS Problem, sy.LabelEn, sy.LabelAr
    FROM #SrcYear sy
    WHERE sy.Id IN (SELECT AcademicYearId FROM #SrcProfile
                    UNION SELECT AcademicYearId FROM #SrcSection
                    UNION SELECT AcademicYearId FROM #SrcOffering)
      AND sy.Id NOT IN (SELECT SrcId FROM #YearMap);

    RAISERROR(N'ABORT: the source references academic years that do not exist in the target (matched on LabelEn). Create them first. Nothing was changed.',
              16, 1);
    RETURN;
END;

/*------------------------------------------------------- ربط القوائم المرجعية
  Map lookup values by (category code, value code).
--------------------------------------------------------------------------- */
CREATE TABLE #LookupMap (SrcId int PRIMARY KEY, TgtId int NOT NULL);

IF OBJECT_ID('tempdb..#NeededLookup') IS NOT NULL DROP TABLE #NeededLookup;
CREATE TABLE #NeededLookup (SrcId int PRIMARY KEY);

INSERT INTO #NeededLookup (SrcId)
SELECT DISTINCT RoomTypeLookupId FROM #SrcRoom
UNION
SELECT DISTINCT FeatureLookupId FROM #SrcFeature
UNION
SELECT DISTINCT CurriculumLookupValueId FROM #SrcProfile WHERE CurriculumLookupValueId IS NOT NULL;

/* أنشئ الناقص إن سُمح بذلك / create what is missing, when allowed */
IF @CreateMissingLookups = 1 AND @WhatIf = 0
BEGIN
    INSERT INTO core.LookupCategory (SchoolId, Code, NameAr, NameEn, Tier, IsActive,
                                     CreatedByUserId, CreatedAtUtc)
    SELECT DISTINCT @TargetSchoolId, sl.CategoryCode, sl.CatNameAr, sl.CatNameEn, sl.CatTier, 1,
           1, SYSUTCDATETIME()
    FROM #SrcLookup sl
    JOIN #NeededLookup n ON n.SrcId = sl.Id
    WHERE NOT EXISTS (SELECT 1 FROM core.LookupCategory tc
                      WHERE tc.SchoolId = @TargetSchoolId AND tc.Code = sl.CategoryCode);

    INSERT INTO core.LookupValue (SchoolId, LookupCategoryId, Code, NameAr, NameEn, SortOrder,
                                  IsActive, CreatedByUserId, CreatedAtUtc)
    SELECT @TargetSchoolId, tc.Id, sl.ValueCode, sl.ValNameAr, sl.ValNameEn, sl.SortOrder,
           sl.IsActive, 1, SYSUTCDATETIME()
    FROM #SrcLookup sl
    JOIN #NeededLookup n ON n.SrcId = sl.Id
    JOIN core.LookupCategory tc ON tc.SchoolId = @TargetSchoolId AND tc.Code = sl.CategoryCode
    WHERE NOT EXISTS (SELECT 1 FROM core.LookupValue tv
                      WHERE tv.LookupCategoryId = tc.Id AND tv.Code = sl.ValueCode);
END;

INSERT INTO #LookupMap (SrcId, TgtId)
SELECT sl.Id, tv.Id
FROM #SrcLookup sl
JOIN #NeededLookup n  ON n.SrcId = sl.Id
JOIN core.LookupCategory tc ON tc.SchoolId = @TargetSchoolId AND tc.Code = sl.CategoryCode
JOIN core.LookupValue    tv ON tv.LookupCategoryId = tc.Id  AND tv.Code = sl.ValueCode;

/* في وضع المعاينة مع السماح بالإنشاء، الناقص سيُنشأ عند التنفيذ الفعلي — لا توقف.
   In preview with creation allowed, what is missing would be created on the real
   run, so report it as a plan rather than aborting. */
IF EXISTS (SELECT 1 FROM #NeededLookup WHERE SrcId NOT IN (SELECT SrcId FROM #LookupMap))
BEGIN
    PRINT N'--- قوائم مرجعية غير موجودة في القاعدة الهدف / lookup values missing in the target ---';
    SELECT sl.CategoryCode, sl.ValueCode, sl.ValNameEn, sl.ValNameAr,
           CASE WHEN @CreateMissingLookups = 1 THEN N'WILL BE CREATED' ELSE N'MISSING' END AS [Plan]
    FROM #SrcLookup sl
    JOIN #NeededLookup n ON n.SrcId = sl.Id
    WHERE sl.Id NOT IN (SELECT SrcId FROM #LookupMap);

    IF @CreateMissingLookups = 0
    BEGIN
        RAISERROR(N'ABORT: the source references lookup values missing from the target. Set @CreateMissingLookups = 1 or create them first. Nothing was changed.',
                  16, 1);
        RETURN;
    END;
END;

/*--------------------------------------------------------------- ربط المواد
  Map subjects by Code. Subjects are not copied — the target keeps its own
  catalogue — so an offering naming a code the target lacks is a hard stop.
--------------------------------------------------------------------------- */
CREATE TABLE #SubjectMap (SrcId int PRIMARY KEY, TgtId int NOT NULL);

INSERT INTO #SubjectMap (SrcId, TgtId)
SELECT ss.Id, ts.Id
FROM #SrcSubject ss
JOIN core.Subject ts ON ts.Code = ss.Code AND ts.SchoolId = @TargetSchoolId
WHERE ss.SchoolId = @SourceSchoolId;

IF EXISTS (SELECT 1 FROM #SrcOffering o WHERE o.SubjectId NOT IN (SELECT SrcId FROM #SubjectMap))
BEGIN
    PRINT N'--- مواد غير موجودة في القاعدة الهدف / subjects missing in the target ---';
    SELECT DISTINCT N'MISSING Subject.Code' AS Problem, ss.Code
    FROM #SrcSubject ss
    WHERE ss.Id IN (SELECT SubjectId FROM #SrcOffering)
      AND ss.Id NOT IN (SELECT SrcId FROM #SubjectMap);

    RAISERROR(N'ABORT: the source curriculum plan names subject codes the target does not have. Create them in core.Subject first. Nothing was changed.',
              16, 1);
    RETURN;
END;

/*------------------------------------------------------ حراسة الجداول التابعة
  Dependent-row guards, evaluated against the rows the delete would remove.
  core.RoomFeature and core.CurriculumOffering are absent from this list on
  purpose: the script copies them, so deleting them replaces rather than loses.
--------------------------------------------------------------------------- */
DECLARE @enrollments int =
    (SELECT COUNT(*) FROM ppl.Enrollment e
     JOIN core.GradeYearProfile p ON p.Id = e.GradeYearProfileId
     WHERE p.SchoolId = @TargetSchoolId);

IF @enrollments > 0
BEGIN
    RAISERROR(N'ABORT: the target holds %d student enrollment(s) on the grade-year profiles this script would delete. Student data is never dropped as a side effect. Nothing was changed.',
              16, 1, @enrollments);
    RETURN;
END;

IF OBJECT_ID('tempdb..#Dependent') IS NOT NULL DROP TABLE #Dependent;
CREATE TABLE #Dependent (TableName sysname PRIMARY KEY, Rows int NOT NULL);

INSERT INTO #Dependent (TableName, Rows) VALUES
 (N'core.SectionMembership',        (SELECT COUNT(*) FROM core.SectionMembership m
                                     JOIN core.Section s ON s.Id = m.SectionId
                                     WHERE s.SchoolId = @TargetSchoolId)),
 (N'core.HomeroomAssignment',       (SELECT COUNT(*) FROM core.HomeroomAssignment h
                                     JOIN core.Section s ON s.Id = h.SectionId
                                     WHERE s.SchoolId = @TargetSchoolId)),
 (N'core.RoomBooking',              (SELECT COUNT(*) FROM core.RoomBooking b
                                     JOIN core.Room r ON r.Id = b.RoomId
                                     WHERE r.SchoolId = @TargetSchoolId)),
 (N'core.RoomAvailabilityException',(SELECT COUNT(*) FROM core.RoomAvailabilityException x
                                     JOIN core.Room r ON r.Id = x.RoomId
                                     WHERE r.SchoolId = @TargetSchoolId)),
 (N'core.ExamSitting',              (SELECT COUNT(*) FROM core.ExamSitting e
                                     JOIN core.Room r ON r.Id = e.RoomId
                                     WHERE r.SchoolId = @TargetSchoolId)),
 (N'core.Placement',                (SELECT COUNT(*) FROM core.Placement pl
                                     JOIN core.Room r ON r.Id = pl.RoomId
                                     WHERE r.SchoolId = @TargetSchoolId)),
 (N'core.Session (OverrideRoomId)', (SELECT COUNT(*) FROM core.Session se
                                     JOIN core.Room r ON r.Id = se.OverrideRoomId
                                     WHERE r.SchoolId = @TargetSchoolId));

IF @PurgeDependents = 0 AND EXISTS (SELECT 1 FROM #Dependent WHERE Rows > 0)
BEGIN
    PRINT N'--- صفوف تابعة تمنع الحذف / dependent rows blocking the delete ---';
    SELECT TableName, Rows FROM #Dependent WHERE Rows > 0 ORDER BY TableName;

    RAISERROR(N'ABORT: dependent rows listed above reference the structure this script would delete. Set @PurgeDependents = 1 to remove them too, or clear them first. Nothing was changed.',
              16, 1);
    RETURN;
END;

/*------------------------------------------------------------------ المعاينة
  Report what the run would do.
--------------------------------------------------------------------------- */
PRINT N'================================================================';
PRINT N'  المصدر / SOURCE : ' + @SourceDb + N'  (SchoolId ' + CAST(@SourceSchoolId AS nvarchar(12)) + N')';
PRINT N'  الهدف  / TARGET : ' + DB_NAME() + N'  (SchoolId ' + CAST(@TargetSchoolId AS nvarchar(12)) + N')';
PRINT N'  الوضع  / MODE   : ' + CASE WHEN @WhatIf = 1 THEN N'PREVIEW — nothing will be written'
                                     ELSE N'EXECUTE — the target will be rewritten' END;
PRINT N'================================================================';

SELECT TableName          = d.n,
       [يُحذف / Deleted]  = d.del,
       [يُنسخ / Copied]   = d.ins
FROM (
    SELECT N'1. core.Building'         AS n, (SELECT COUNT(*) FROM core.Building         WHERE SchoolId = @TargetSchoolId) AS del, (SELECT COUNT(*) FROM #SrcBuilding) AS ins, 1 AS o
    UNION ALL SELECT N'2. core.Floor',       (SELECT COUNT(*) FROM core.Floor            WHERE SchoolId = @TargetSchoolId), (SELECT COUNT(*) FROM #SrcFloor),   2
    UNION ALL SELECT N'3. core.Room',        (SELECT COUNT(*) FROM core.Room             WHERE SchoolId = @TargetSchoolId), (SELECT COUNT(*) FROM #SrcRoom),    3
    UNION ALL SELECT N'4. core.RoomFeature', (SELECT COUNT(*) FROM core.RoomFeature      WHERE SchoolId = @TargetSchoolId), (SELECT COUNT(*) FROM #SrcFeature), 4
    UNION ALL SELECT N'5. core.Stage',       (SELECT COUNT(*) FROM core.Stage            WHERE SchoolId = @TargetSchoolId), (SELECT COUNT(*) FROM #SrcStage),   5
    UNION ALL SELECT N'6. core.GradeLevel',  (SELECT COUNT(*) FROM core.GradeLevel       WHERE SchoolId = @TargetSchoolId), (SELECT COUNT(*) FROM #SrcGrade),   6
    UNION ALL SELECT N'7. core.GradeYearProfile', (SELECT COUNT(*) FROM core.GradeYearProfile WHERE SchoolId = @TargetSchoolId), (SELECT COUNT(*) FROM #SrcProfile), 7
    UNION ALL SELECT N'8. core.CurriculumOffering', (SELECT COUNT(*) FROM core.CurriculumOffering WHERE SchoolId = @TargetSchoolId), (SELECT COUNT(*) FROM #SrcOffering), 8
    UNION ALL SELECT N'9. core.Section',     (SELECT COUNT(*) FROM core.Section          WHERE SchoolId = @TargetSchoolId), (SELECT COUNT(*) FROM #SrcSection), 9
) d
ORDER BY d.o;

IF @WhatIf = 1
BEGIN
    PRINT N'';
    PRINT N'معاينة فقط — لم يتغيّر شيء. اجعل @WhatIf = 0 للتنفيذ.';
    PRINT N'PREVIEW ONLY — nothing was changed. Set @WhatIf = 0 to run it.';
    RETURN;
END;

/*------------------------------------------------------------------- التنفيذ
  Execute: one transaction, delete children-first, insert parents-first.
--------------------------------------------------------------------------- */
BEGIN TRY
    BEGIN TRANSACTION;

    /* ---- الحذف / DELETE, children first ---- */

    UPDATE se SET se.OverrideRoomId = NULL
    FROM core.Session se JOIN core.Room r ON r.Id = se.OverrideRoomId
    WHERE r.SchoolId = @TargetSchoolId;

    DELETE m FROM core.SectionMembership m
        JOIN core.Section s ON s.Id = m.SectionId WHERE s.SchoolId = @TargetSchoolId;
    DELETE h FROM core.HomeroomAssignment h
        JOIN core.Section s ON s.Id = h.SectionId WHERE s.SchoolId = @TargetSchoolId;
    DELETE e FROM core.ExamSitting e
        JOIN core.Room r ON r.Id = e.RoomId WHERE r.SchoolId = @TargetSchoolId;
    DELETE pl FROM core.Placement pl
        JOIN core.Room r ON r.Id = pl.RoomId WHERE r.SchoolId = @TargetSchoolId;
    DELETE b FROM core.RoomBooking b
        JOIN core.Room r ON r.Id = b.RoomId WHERE r.SchoolId = @TargetSchoolId;
    DELETE x FROM core.RoomAvailabilityException x
        JOIN core.Room r ON r.Id = x.RoomId WHERE r.SchoolId = @TargetSchoolId;
    /* هذان الجدولان يُحذفان ثم يُنسخان من المصدر، فلا خسارة صافية.
       These two are deleted and then re-copied from the source — no net loss. */
    DELETE FROM core.RoomFeature        WHERE SchoolId = @TargetSchoolId;
    DELETE FROM core.CurriculumOffering WHERE SchoolId = @TargetSchoolId;

    DELETE FROM core.Section          WHERE SchoolId = @TargetSchoolId;
    DELETE FROM core.GradeYearProfile WHERE SchoolId = @TargetSchoolId;

    /* المفتاح الذاتي للصف يُفكّ قبل الحذف / break the self-FK before deleting grades */
    UPDATE core.GradeLevel SET PromotionTargetGradeLevelId = NULL WHERE SchoolId = @TargetSchoolId;
    DELETE FROM core.GradeLevel       WHERE SchoolId = @TargetSchoolId;
    DELETE FROM core.Stage            WHERE SchoolId = @TargetSchoolId;

    DELETE FROM core.Room             WHERE SchoolId = @TargetSchoolId;
    DELETE FROM core.Floor            WHERE SchoolId = @TargetSchoolId;
    DELETE FROM core.Building         WHERE SchoolId = @TargetSchoolId;

    /* ---- النسخ / INSERT, parents first, Ids preserved ---- */

    SET IDENTITY_INSERT core.Building ON;
    INSERT INTO core.Building (Id, SchoolId, NameAr, NameEn, IsActive,
                               CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc)
    SELECT Id, @TargetSchoolId, NameAr, NameEn, IsActive,
           CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM #SrcBuilding;
    SET IDENTITY_INSERT core.Building OFF;

    SET IDENTITY_INSERT core.Floor ON;
    INSERT INTO core.Floor (Id, SchoolId, BuildingId, NameAr, NameEn, SequenceOrder, IsActive,
                            CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc)
    SELECT Id, @TargetSchoolId, BuildingId, NameAr, NameEn, SequenceOrder, IsActive,
           CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM #SrcFloor;
    SET IDENTITY_INSERT core.Floor OFF;

    SET IDENTITY_INSERT core.Room ON;
    INSERT INTO core.Room (Id, SchoolId, FloorId, Code, NameAr, NameEn, RoomTypeLookupId,
                           StandardCapacity, ExamCapacity, WingTag, IsActive,
                           CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc)
    SELECT r.Id, @TargetSchoolId, r.FloorId, r.Code, r.NameAr, r.NameEn, lm.TgtId,
           r.StandardCapacity, r.ExamCapacity, r.WingTag, r.IsActive,
           r.CreatedByUserId, r.CreatedAtUtc, r.ModifiedByUserId, r.ModifiedAtUtc
    FROM #SrcRoom r
    JOIN #LookupMap lm ON lm.SrcId = r.RoomTypeLookupId;
    SET IDENTITY_INSERT core.Room OFF;

    SET IDENTITY_INSERT core.RoomFeature ON;
    INSERT INTO core.RoomFeature (Id, SchoolId, RoomId, FeatureLookupId,
                                  CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc)
    SELECT f.Id, @TargetSchoolId, f.RoomId, lm.TgtId,
           f.CreatedByUserId, f.CreatedAtUtc, f.ModifiedByUserId, f.ModifiedAtUtc
    FROM #SrcFeature f
    JOIN #LookupMap lm ON lm.SrcId = f.FeatureLookupId;
    SET IDENTITY_INSERT core.RoomFeature OFF;

    SET IDENTITY_INSERT core.Stage ON;
    INSERT INTO core.Stage (Id, SchoolId, NameAr, NameEn, SequenceOrder, DefaultGenderPolicy,
                            IsActive, CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc)
    SELECT Id, @TargetSchoolId, NameAr, NameEn, SequenceOrder, DefaultGenderPolicy,
           IsActive, CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM #SrcStage;
    SET IDENTITY_INSERT core.Stage OFF;

    /* الصف على مرحلتين: المفتاح الذاتي يُملأ بعد اكتمال الصفوف كلها
       GradeLevel in two passes — the self-FK is filled once all rows exist,
       so the source's promotion order never has to be a topological one. */
    SET IDENTITY_INSERT core.GradeLevel ON;
    INSERT INTO core.GradeLevel (Id, SchoolId, StageId, Code, NameAr, NameEn, SequenceOrder,
                                 PromotionTargetGradeLevelId, IsGraduating, IsActive,
                                 CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc)
    SELECT Id, @TargetSchoolId, StageId, Code, NameAr, NameEn, SequenceOrder,
           NULL, IsGraduating, IsActive,
           CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc
    FROM #SrcGrade;
    SET IDENTITY_INSERT core.GradeLevel OFF;

    UPDATE g SET g.PromotionTargetGradeLevelId = s.PromotionTargetGradeLevelId
    FROM core.GradeLevel g
    JOIN #SrcGrade s ON s.Id = g.Id
    WHERE g.SchoolId = @TargetSchoolId AND s.PromotionTargetGradeLevelId IS NOT NULL;

    SET IDENTITY_INSERT core.GradeYearProfile ON;
    INSERT INTO core.GradeYearProfile (Id, SchoolId, AcademicYearId, GradeLevelId,
                                       CurriculumLookupValueId, GenderPolicy, MinAgeAtCutoff,
                                       MaxAgeAtCutoff, AgeCutoffDate, TargetSections,
                                       TargetSectionSize, IsActive,
                                       CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc)
    SELECT p.Id, @TargetSchoolId, ym.TgtId, p.GradeLevelId,
           lm.TgtId, p.GenderPolicy, p.MinAgeAtCutoff,
           p.MaxAgeAtCutoff, p.AgeCutoffDate, p.TargetSections,
           p.TargetSectionSize, p.IsActive,
           p.CreatedByUserId, p.CreatedAtUtc, p.ModifiedByUserId, p.ModifiedAtUtc
    FROM #SrcProfile p
    JOIN #YearMap ym    ON ym.SrcId = p.AcademicYearId
    LEFT JOIN #LookupMap lm ON lm.SrcId = p.CurriculumLookupValueId;
    SET IDENTITY_INSERT core.GradeYearProfile OFF;

    SET IDENTITY_INSERT core.CurriculumOffering ON;
    INSERT INTO core.CurriculumOffering (Id, SchoolId, AcademicYearId, GradeYearProfileId, SubjectId,
                                         WeeklyPeriods, IsAssessable, GpaWeight, IsElective,
                                         ElectiveGroupTag, EffectiveFromUtc, EffectiveToUtc,
                                         CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc)
    SELECT o.Id, @TargetSchoolId, ym.TgtId, o.GradeYearProfileId, sm.TgtId,
           o.WeeklyPeriods, o.IsAssessable, o.GpaWeight, o.IsElective,
           o.ElectiveGroupTag, o.EffectiveFromUtc, o.EffectiveToUtc,
           o.CreatedByUserId, o.CreatedAtUtc, o.ModifiedByUserId, o.ModifiedAtUtc
    FROM #SrcOffering o
    JOIN #YearMap    ym ON ym.SrcId = o.AcademicYearId
    JOIN #SubjectMap sm ON sm.SrcId = o.SubjectId;
    SET IDENTITY_INSERT core.CurriculumOffering OFF;

    SET IDENTITY_INSERT core.Section ON;
    INSERT INTO core.Section (Id, SchoolId, AcademicYearId, GradeYearProfileId, NameAr, NameEn,
                              Capacity, GenderPolicy, DefaultClassroomId, Status,
                              CreatedByUserId, CreatedAtUtc, ModifiedByUserId, ModifiedAtUtc)
    SELECT s.Id, @TargetSchoolId, ym.TgtId, s.GradeYearProfileId, s.NameAr, s.NameEn,
           s.Capacity, s.GenderPolicy, s.DefaultClassroomId, s.Status,
           s.CreatedByUserId, s.CreatedAtUtc, s.ModifiedByUserId, s.ModifiedAtUtc
    FROM #SrcSection s
    JOIN #YearMap ym ON ym.SrcId = s.AcademicYearId;
    SET IDENTITY_INSERT core.Section OFF;

    COMMIT TRANSACTION;
    PRINT N'تم النسخ بنجاح / copy committed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;

    DECLARE @msg nvarchar(2048) = ERROR_MESSAGE();
    DECLARE @line int = ERROR_LINE();
    PRINT N'تراجعت العملية بالكامل / rolled back — the target keeps its original rows.';
    RAISERROR(N'FAILED at line %d: %s', 16, 1, @line, @msg);
    RETURN;
END CATCH;

/*------------------------------------------------- إعادة ضبط عدّادات IDENTITY
  Reseed, or the app's next insert collides with a copied Id.
--------------------------------------------------------------------------- */
IF @ResetIdentity = 1
BEGIN
    DBCC CHECKIDENT ('core.Building',         RESEED) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('core.Floor',            RESEED) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('core.Room',               RESEED) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('core.RoomFeature',        RESEED) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('core.Stage',              RESEED) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('core.GradeLevel',         RESEED) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('core.GradeYearProfile',   RESEED) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('core.CurriculumOffering', RESEED) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('core.Section',            RESEED) WITH NO_INFOMSGS;
END;

/*-------------------------------------------------------------------- النتيجة
  Final counts.
--------------------------------------------------------------------------- */
SELECT TableName = d.n, [الصفوف / Rows] = d.c
FROM (
    SELECT N'1. core.Building' AS n, COUNT(*) AS c, 1 AS o FROM core.Building             WHERE SchoolId = @TargetSchoolId
    UNION ALL SELECT N'2. core.Floor',              COUNT(*), 2 FROM core.Floor              WHERE SchoolId = @TargetSchoolId
    UNION ALL SELECT N'3. core.Room',               COUNT(*), 3 FROM core.Room               WHERE SchoolId = @TargetSchoolId
    UNION ALL SELECT N'4. core.RoomFeature',        COUNT(*), 4 FROM core.RoomFeature        WHERE SchoolId = @TargetSchoolId
    UNION ALL SELECT N'5. core.Stage',              COUNT(*), 5 FROM core.Stage              WHERE SchoolId = @TargetSchoolId
    UNION ALL SELECT N'6. core.GradeLevel',         COUNT(*), 6 FROM core.GradeLevel         WHERE SchoolId = @TargetSchoolId
    UNION ALL SELECT N'7. core.GradeYearProfile',   COUNT(*), 7 FROM core.GradeYearProfile   WHERE SchoolId = @TargetSchoolId
    UNION ALL SELECT N'8. core.CurriculumOffering', COUNT(*), 8 FROM core.CurriculumOffering WHERE SchoolId = @TargetSchoolId
    UNION ALL SELECT N'9. core.Section',            COUNT(*), 9 FROM core.Section            WHERE SchoolId = @TargetSchoolId
) d
ORDER BY d.o;
