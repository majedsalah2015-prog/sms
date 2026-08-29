/*
================================================================================
  SMS — ضبط المصنع  /  Factory Reset
================================================================================

  ما يفعله هذا السكريبت / What this script does
  ---------------------------------------------
  يُفرِّغ كل بيانات العمل ويُبقي على هيكل المدرسة وإعداداتها، فتعود القاعدة إلى
  حالة "مدرسة مُهيّأة بلا بيانات".

  Empties every operational row and keeps the school's structure and settings,
  leaving the database in a "configured school, no data" state.

  يُحذَف / DELETED
      الطلبة، أولياء الأمور، الروابط الأسرية، طلبات القبول وقوائم الانتظار
      الموظفون، عقودهم، مؤهلاتهم، تكليفاتهم، ملفات المعلمين وتخصصاتهم
      التسجيلات، عضويات الشعب، توزيع الحصص، النُّوَب، الغياب، الاستئذان
      الدرجات وكشوفها، الامتحانات وجلساتها، نتائج الفصول والسنوات، الترحيل
      الرسوم المستحقة، الأقساط، الخصومات الممنوحة، السندات، الدفعات، التسويات
      الشيكات الآجلة، المطالبات، كشوف الحساب، الاسترداد، جلسات الصندوق
      اشتراكات النقل، الرحلات وسجلاتها، خطوط السير وسائقوها (انظر ملاحظة ٣)
      الإعارات والحجوزات وسجلات المكتبة، مبيعات المقصف والمتجر والمخزون المتحرك
      المخالفات والقضايا السلوكية، الملفات الصحية والزيارات والتطعيمات
      الأنشطة والمشاركات، الشهادات المُصدرة، المرفقات، الرسائل والإشعارات
      سجلّ التدقيق كاملًا، تشغيلات الوظائف، عدّادات الترقيم، حسابات المستخدمين
      عدا المستثنى، جلساتهم ومحاولات دخولهم
      قيود دفتر الأستاذ في ERP وكل حركة محاسبية/مخزنية/بيعية/شرائية

  يبقى / KEPT
      المدرسة وإعداداتها وقائمة التحقق والمُوقِّعون
      السنوات الدراسية والفصول والفترات والتقويم الدراسي
      المراحل، الصفوف، الشعب، المواد، الخطط الدراسية، الأقسام، الوحدات التنظيمية
      المباني والطوابق والغرف وخصائصها، وقوالب الجداول والحصص
      سلالم التقدير ونطاقاتها، مخططات الامتحانات، معايير الترفيع، أنواع الامتحانات
      كتالوج الرسوم وبنودها، قوالب التقسيط، أنواع الخصومات، برامج المنح وقواعد الاستحقاق
      كتالوج المقصف والمتجر (الأصناف، المتغيّرات، قوائم الأسعار، الحزم، الوجبات)
      كتالوج المكتبة (العناوين والنسخ وسياسات الأعضاء)، الباصات ووثائقها
      كتالوج السلوك (المخالفات، العقوبات، سلّم التدرّج، أنواع التميّز)
      القوائم المرجعية، الجغرافيا، حزمة الدولة، سلاسل الترقيم (بلا عدّاداتها)
      الصلاحيات والأدوار وربطها، حساب المدير وتكليفاته
      تعريفات التقارير والويدجت وقوالب الشاشات، تعريفات سير العمل
      قوالب المراسلات ومزوّدوها ومصفوفة الاتصال، تعريفات الوظائف وسياسات النسخ
      دليل حسابات ERP والعملات والسنة المالية والوحدات والمستودعات والفروع

  ملاحظات مهمة / Important notes
  ------------------------------
  ١) السكريبت يعمل بوضع المعاينة افتراضيًا (@WhatIf = 1): يعرض ما سيُحذف ولا
     يغيّر شيئًا. اجعله 0 للتنفيذ الفعلي.
     Runs in preview mode by default. Set @WhatIf = 0 to actually delete.

  ٢) التنفيذ كله داخل معاملة واحدة، وتُعاد المفاتيح الأجنبية في النهاية بـ
     WITH CHECK — فإن نتج أي مرجع معلّق تفشل العملية وتتراجع بالكامل.
     Everything runs in one transaction; foreign keys are re-enabled WITH CHECK,
     so any dangling reference aborts and rolls the whole reset back.

  ٣) خطوط سير الباصات تُحذف رغم أنها "هيكل": العمود svc.Route.DriverId إلزامي
     ويشير إلى svc.TransportStaff، وسائقو النقل بيانات موظفين تُحذف. لا يمكن
     إبقاء خط سير بلا سائق في هذا المخطط. الباصات نفسها ووثائقها تبقى.
     Bus routes are deleted even though they are "structure": svc.Route.DriverId
     is NOT NULL and points at svc.TransportStaff, whose rows are staff data.
     The schema forbids a driverless route. The buses themselves are kept.

  ٤) بعد التنفيذ، إعادة تشغيل tools/Sms.Seeder ستُعيد إنشاء بيانات العرض
     التجريبية (طلبة وموظفون وحسابا parent/student)، لأن مساهمات Demo مسجّلة
     دون شرط في tools/Sms.Seeder/Program.cs. شغّله فقط إن أردت ذلك.
     Re-running tools/Sms.Seeder re-creates the DEMO students, staff and the
     parent/student portal accounts — the Demo contributors are registered
     unconditionally. Only run it if you want them back.

  ٥) هذا السكريبت لا يُنشئ نسخة احتياطية. خُذ نسخة قبل التنفيذ:
     This script takes no backup. Take one first:
         BACKUP DATABASE [Sms] TO DISK = N'C:\Temp\Sms_before_reset.bak' WITH INIT;

  الاستخدام / Usage
  -----------------
      sqlcmd -S .\SQLEXPRESS -E -d Sms -i tools\db\FactoryReset.sql

  فحص بيئي واحد: 2026-08-26 على SQL Server 2017 Express، قاعدة Sms، 257 جدولًا
  في سكيمات المدرسة و376 مفتاحًا أجنبيًا — صفر مرجع معلّق بعد الحذف.
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
DECLARE @ConfirmDatabase sysname = N'Sms';   -- يجب أن يطابق اسم القاعدة الحالية
DECLARE @WhatIf          bit     = 1;        -- 1 = معاينة فقط، 0 = تنفيذ فعلي
DECLARE @ResetErp        bit     = 1;        -- 1 = تفريغ حركات ERP المحاسبية
DECLARE @ClearHangfire   bit     = 0;        -- 1 = تنظيف طابور Hangfire أيضًا
DECLARE @ResetIdentity   bit     = 1;        -- 1 = إعادة العدّادات إلى 1

/* حسابات المستخدمين التي تبقى. أضف اسمك هنا إن كان لك حساب شخصي.
   User accounts to keep. Add your own username if you have a personal one. */
DECLARE @KeepUsers TABLE (UserName sysname PRIMARY KEY);
INSERT INTO @KeepUsers (UserName) VALUES (N'admin');
INSERT INTO @KeepUsers (UserName) VALUES (N'majed');   -- حسابك الشخصي / your own login

/*--------------------------------------------------------------- حارس القاعدة
  Guard: refuse to run against the wrong database.
--------------------------------------------------------------------------- */
IF DB_NAME() <> @ConfirmDatabase
BEGIN
    RAISERROR(N'ABORT: connected to [%s] but @ConfirmDatabase is [%s]. Nothing was changed.',
              16, 1, @@SERVERNAME, @ConfirmDatabase);
    RETURN;
END;

DECLARE @keptUserCount int = (SELECT COUNT(*) FROM sec.UserAccount u JOIN @KeepUsers k ON k.UserName = u.UserName);
IF @keptUserCount = 0
BEGIN
    RAISERROR(N'ABORT: none of the usernames in @KeepUsers exists in sec.UserAccount. Continuing would leave nobody able to sign in.',
              16, 1);
    RETURN;
END;

/*---------------------------------------------------- سكيمات المدرسة وسكيمات ERP
--------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#SmsSchema') IS NOT NULL DROP TABLE #SmsSchema;
CREATE TABLE #SmsSchema (SchemaName sysname PRIMARY KEY);
INSERT INTO #SmsSchema (SchemaName) VALUES
    (N'core'), (N'ppl'), (N'svc'), (N'sec'), (N'wf'),
    (N'fin'),  (N'doc'), (N'msg'), (N'aud'), (N'rpt'), (N'ops');

/*------------------------------------------------------------ ما يبقى من SMS
  KEEP list. Anything in an SMS schema that is NOT listed here gets emptied —
  so a newly added table is cleaned by default. Check the preview output after
  adding tables to the model.
--------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#Keep') IS NOT NULL DROP TABLE #Keep;
CREATE TABLE #Keep (SchemaName sysname NOT NULL, TableName sysname NOT NULL,
                    PRIMARY KEY (SchemaName, TableName));
INSERT INTO #Keep (SchemaName, TableName) VALUES
    -- core: هيكل المدرسة والسنة والتقويم / school, year and calendar structure
    (N'core', N'School'),               (N'core', N'SchoolGroup'),
    (N'core', N'SchoolSetting'),        (N'core', N'SetupChecklist'),
    (N'core', N'Signatory'),            (N'core', N'Department'),
    (N'core', N'AcademicYear'),         (N'core', N'Semester'),
    (N'core', N'Term'),
    (N'core', N'CalendarVersion'),      (N'core', N'CalendarDay'),
    (N'core', N'CalendarEvent'),
    -- core: الهيكل الأكاديمي / academic structure
    (N'core', N'Stage'),                (N'core', N'GradeLevel'),
    (N'core', N'Section'),              (N'core', N'Subject'),
    (N'core', N'CurriculumOffering'),   (N'core', N'GradeYearProfile'),
    (N'core', N'PromotionCriteria'),
    -- core: التقدير والامتحانات (التعريفات لا النتائج) / grading definitions
    (N'core', N'GradingScale'),         (N'core', N'ScaleBand'),
    (N'core', N'Blueprint'),            (N'core', N'BlueprintComponent'),
    (N'core', N'ExamType'),
    -- core: المرافق والجداول / facilities and timetable shape
    (N'core', N'Building'),             (N'core', N'Floor'),
    (N'core', N'Room'),                 (N'core', N'RoomFeature'),
    (N'core', N'TimetableShape'),       (N'core', N'PeriodSlot'),
    -- core: المرجعيات والمنصّات / reference data and platform catalogues
    (N'core', N'LookupCategory'),       (N'core', N'LookupValue'),
    (N'core', N'CountryPack'),          (N'core', N'Governorate'),
    (N'core', N'Neighbourhood'),        (N'core', N'ResidenceArea'),
    (N'core', N'NumberingSeries'),      (N'core', N'FeatureToggle'),
    (N'core', N'LicenseState'),         (N'core', N'ReportDefinition'),
    (N'core', N'WidgetDefinition'),     (N'core', N'LayoutTemplate'),
    (N'core', N'LayoutTemplateWidget'),
    -- ppl: كتالوجات الرسوم والخصوم والأنشطة / fee, discount and activity catalogues
    (N'ppl',  N'FeeCategory'),          (N'ppl',  N'FeeStructureLine'),
    (N'ppl',  N'PlanTemplate'),         (N'ppl',  N'TemplateInstallment'),
    (N'ppl',  N'DiscountType'),         (N'ppl',  N'ScholarshipProgram'),
    (N'ppl',  N'EligibilityRule'),      (N'ppl',  N'CertificateType'),
    (N'ppl',  N'ActivityType'),         (N'ppl',  N'OrgUnit'),
    -- svc: كتالوج السلوك / discipline catalogue
    (N'svc',  N'ViolationType'),        (N'svc',  N'BehaviorCode'),
    (N'svc',  N'ConsequenceType'),      (N'svc',  N'LadderStep'),
    (N'svc',  N'MeritType'),
    -- svc: كتالوج المقصف والمتجر / cafeteria and store catalogue
    (N'svc',  N'CafeteriaItem'),        (N'svc',  N'Menu'),
    (N'svc',  N'MenuLine'),             (N'svc',  N'MealPlan'),
    (N'svc',  N'PriceList'),            (N'svc',  N'PriceListLine'),
    (N'svc',  N'StoreItem'),            (N'svc',  N'Variant'),
    (N'svc',  N'Bundle'),               (N'svc',  N'BundleLine'),
    (N'svc',  N'StoreAccountChargePolicy'), (N'svc', N'StoreReturnPolicy'),
    -- svc: المكتبة والنقل والصحة / library holdings, fleet, health schedule
    (N'svc',  N'Title'),                (N'svc',  N'Copy'),
    (N'svc',  N'MemberPolicy'),         (N'svc',  N'Bus'),
    (N'svc',  N'BusDocument'),          (N'svc',  N'VaccinationScheduleEntry'),
    -- sec: الصلاحيات (والحسابات تُصفّى صفًّا صفًّا أدناه) / permissions
    (N'sec',  N'Permission'),           (N'sec',  N'Role'),
    (N'sec',  N'RolePermission'),       (N'sec',  N'UserAccount'),
    (N'sec',  N'RoleAssignment'),       (N'sec',  N'ScopeGrant'),
    -- wf: رسم سير العمل لا نسخه الجارية / workflow graph, not its instances
    (N'wf',   N'WorkflowDefinition'),   (N'wf',   N'WorkflowState'),
    (N'wf',   N'WorkflowTransition'),
    -- fin / doc / msg / aud / ops: التعريفات فقط / definitions only
    (N'fin',  N'GlAccountMapping'),
    (N'doc',  N'DocumentType'),
    (N'msg',  N'Template'),             (N'msg',  N'TemplateVersion'),
    (N'msg',  N'Provider'),             (N'msg',  N'CommunicationMatrix'),
    (N'msg',  N'SubscriptionRule'),
    (N'aud',  N'AnomalyRule'),
    (N'ops',  N'JobDefinition'),        (N'ops',  N'BackupPolicy'),
    (N'ops',  N'MaintenanceWindow');

/*----------------------------------------------------- حركات ERP التي تُفرَّغ
  ERP is an explicit DELETE list, not a keep-list: external/erp is read-only
  here and its model is not ours to assume about. Anything not listed is left
  untouched — that includes the chart of accounts, currencies, the fiscal
  calendar, units, warehouses, branches and every account mapping.
--------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#ErpDel') IS NOT NULL DROP TABLE #ErpDel;
CREATE TABLE #ErpDel (SchemaName sysname NOT NULL, TableName sysname NOT NULL,
                      PRIMARY KEY (SchemaName, TableName));
INSERT INTO #ErpDel (SchemaName, TableName) VALUES
    -- acc: القيود اليومية / journal
    (N'acc', N'JournalEntryLines'),       (N'acc', N'JournalEntries'),
    -- cash: الصندوق والبنوك / cash, cheques, vouchers, settlements
    (N'cash', N'ChequeEvents'),           (N'cash', N'Cheques'),
    (N'cash', N'OpenItemMovements'),      (N'cash', N'OpenItems'),
    (N'cash', N'PaymentAllocations'),     (N'cash', N'PaymentSources'),
    (N'cash', N'Payments'),               (N'cash', N'PaymentVoucherLines'),
    (N'cash', N'PaymentVouchers'),        (N'cash', N'ReceiptAllocations'),
    (N'cash', N'ReceiptDestinations'),    (N'cash', N'Receipts'),
    (N'cash', N'ReceiptVoucherLines'),    (N'cash', N'ReceiptVouchers'),
    (N'cash', N'Reconciliations'),        (N'cash', N'SettlementApplicationLines'),
    (N'cash', N'SettlementApplications'), (N'cash', N'Transfers'),
    (N'cash', N'VoucherAttachments'),
    -- inv: حركة المخزون والإنتاج والعروض / stock, production, promotions
    (N'inv', N'BarcodeScanLog'),          (N'inv', N'ItemPromotionLines'),
    (N'inv', N'ItemPromotions'),          (N'inv', N'PromotionUsages'),
    (N'inv', N'LabelPrintRunLines'),      (N'inv', N'LabelPrintRuns'),
    (N'inv', N'ProductionOrderComponents'),(N'inv', N'ProductionOrderCosts'),
    (N'inv', N'ProductionOrders'),        (N'inv', N'StockDocumentLines'),
    (N'inv', N'StockDocuments'),          (N'inv', N'StockLevels'),
    (N'inv', N'StockMovements'),
    -- org: أقفال الفترات / period locks
    (N'org', N'BranchPeriodLocks'),
    -- pos: نقاط البيع (الأجهزة تبقى) / POS shifts and orders, terminals kept
    (N'pos', N'PosOrderLines'),           (N'pos', N'PosOrders'),
    (N'pos', N'PosPayments'),             (N'pos', N'ShiftDeclarations'),
    (N'pos', N'ShiftMovements'),          (N'pos', N'Shifts'),
    -- ptn: الشركاء وحركاتهم / partners and their transactions
    (N'ptn', N'PartnerCapitalTransactions'), (N'ptn', N'PartnerCurrentTransactions'),
    (N'ptn', N'ProfitDistributionLines'), (N'ptn', N'ProfitDistributions'),
    (N'ptn', N'Partners'),
    -- pur: المشتريات والموردون / purchasing and vendors
    (N'pur', N'GoodsReceiptLines'),       (N'pur', N'GoodsReceipts'),
    (N'pur', N'PurchaseOrderLines'),      (N'pur', N'PurchaseOrders'),
    (N'pur', N'VendorBillLines'),         (N'pur', N'VendorBills'),
    (N'pur', N'Vendors'),
    -- sal: المبيعات والعملاء / sales and customers
    (N'sal', N'Customers'),               (N'sal', N'Deliveries'),
    (N'sal', N'DeliveryLines'),           (N'sal', N'SalesInvoiceLines'),
    (N'sal', N'SalesInvoices'),           (N'sal', N'SalesOrderLines'),
    (N'sal', N'SalesOrders');

/*-------------------------------------------------- الجداول التي ستُفرَّغ فعليًا
  #Purge = every SMS table outside #Keep, plus the ERP delete list when enabled.
  __EFMigrationsHistory is excluded everywhere — losing it breaks migrations.
--------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#Purge') IS NOT NULL DROP TABLE #Purge;
CREATE TABLE #Purge (SchemaName sysname NOT NULL, TableName sysname NOT NULL,
                     Origin varchar(4) NOT NULL, PRIMARY KEY (SchemaName, TableName));

INSERT INTO #Purge (SchemaName, TableName, Origin)
SELECT s.name, t.name, 'SMS'
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name IN (SELECT SchemaName FROM #SmsSchema)
  AND t.name <> N'__EFMigrationsHistory'
  AND t.is_ms_shipped = 0
  AND NOT EXISTS (SELECT 1 FROM #Keep k WHERE k.SchemaName = s.name AND k.TableName = t.name);

IF @ResetErp = 1
    INSERT INTO #Purge (SchemaName, TableName, Origin)
    SELECT s.name, t.name, 'ERP'
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN #ErpDel d ON d.SchemaName = s.name AND d.TableName = t.name
    WHERE t.name <> N'__EFMigrationsHistory';

IF @ClearHangfire = 1
    INSERT INTO #Purge (SchemaName, TableName, Origin)
    SELECT s.name, t.name, 'HF'
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = N'HangFire' AND t.name <> N'Schema';

/*--------------------------------- الأعمدة الاختيارية التي ستُفرَّغ إلى NULL
  A kept table may hold a nullable FK into a purged one. Those columns are set
  to NULL so the FK re-check passes. Multi-column FKs are reported, not guessed.
--------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#NullOut') IS NOT NULL DROP TABLE #NullOut;
CREATE TABLE #NullOut (SchemaName sysname, TableName sysname, ColumnName sysname,
                       Target nvarchar(300));

INSERT INTO #NullOut (SchemaName, TableName, ColumnName, Target)
SELECT DISTINCT ps.name, pt.name, pc.name, rs.name + N'.' + rt.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables  pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables  rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
WHERE pc.is_nullable = 1
  AND EXISTS     (SELECT 1 FROM #Purge p WHERE p.SchemaName = rs.name AND p.TableName = rt.name)
  AND NOT EXISTS (SELECT 1 FROM #Purge p WHERE p.SchemaName = ps.name AND p.TableName = pt.name)
  AND 1 = (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id = fk.object_id);

IF OBJECT_ID('tempdb..#Unsupported') IS NOT NULL DROP TABLE #Unsupported;
CREATE TABLE #Unsupported (FkName sysname, Detail nvarchar(400));
INSERT INTO #Unsupported (FkName, Detail)
SELECT fk.name, ps.name + N'.' + pt.name + N' -> ' + rs.name + N'.' + rt.name
FROM sys.foreign_keys fk
JOIN sys.tables  pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
JOIN sys.tables  rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
WHERE EXISTS     (SELECT 1 FROM #Purge p WHERE p.SchemaName = rs.name AND p.TableName = rt.name)
  AND NOT EXISTS (SELECT 1 FROM #Purge p WHERE p.SchemaName = ps.name AND p.TableName = pt.name)
  AND 1 < (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id = fk.object_id);

/*========================================================== وضع المعاينة =====
  Preview. Prints the plan and changes nothing.
--------------------------------------------------------------------------- */
IF @WhatIf = 1
BEGIN
    PRINT N'=============================================================';
    PRINT N'  PREVIEW ONLY — لم يُحذف شيء / nothing was deleted';
    PRINT N'  Database: ' + DB_NAME() + N'   Server: ' + @@SERVERNAME;
    PRINT N'  Set @WhatIf = 0 to execute.';
    PRINT N'=============================================================';

    IF OBJECT_ID('tempdb..#PurgeRows') IS NOT NULL DROP TABLE #PurgeRows;
    SELECT p.Origin, p.SchemaName, p.TableName,
           RowsNow = ISNULL(SUM(pa.rows), 0)
    INTO #PurgeRows
    FROM #Purge p
    LEFT JOIN sys.partitions pa
           ON pa.object_id = OBJECT_ID(QUOTENAME(p.SchemaName) + '.' + QUOTENAME(p.TableName))
          AND pa.index_id IN (0, 1)
    GROUP BY p.Origin, p.SchemaName, p.TableName;

    SELECT Origin, SchemaName, TableName, RowsNow
    FROM #PurgeRows
    WHERE RowsNow > 0
    ORDER BY RowsNow DESC, Origin, SchemaName, TableName;

    SELECT [Tables to empty] = COUNT(*),
           [Of which non-empty] = SUM(CASE WHEN RowsNow > 0 THEN 1 ELSE 0 END),
           [Rows to delete] = SUM(RowsNow)
    FROM #PurgeRows;

    SELECT [User accounts DELETED] = u.UserName, u.AccountType, u.Id
    FROM sec.UserAccount u
    WHERE NOT EXISTS (SELECT 1 FROM @KeepUsers k WHERE k.UserName = u.UserName);

    SELECT [User accounts KEPT] = u.UserName, u.AccountType, u.Id
    FROM sec.UserAccount u
    JOIN @KeepUsers k ON k.UserName = u.UserName;

    SELECT [Columns set to NULL] = SchemaName + N'.' + TableName + N'.' + ColumnName,
           [Because it points at] = Target
    FROM #NullOut ORDER BY 1;

    SELECT [Kept tables NOT emptied] = k.SchemaName + N'.' + k.TableName
    FROM #Keep k ORDER BY 1;

    IF EXISTS (SELECT 1 FROM #Unsupported)
        SELECT [UNSUPPORTED multi-column FK — fix the keep list] = FkName, Detail FROM #Unsupported;

    RETURN;
END;

/*========================================================== التنفيذ الفعلي ===
  Execute. One transaction; FKs re-checked at the end.
--------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM #Unsupported)
BEGIN
    RAISERROR(N'ABORT: a multi-column foreign key points from a kept table into a purged one. Run with @WhatIf = 1 to see it, then adjust #Keep.', 16, 1);
    RETURN;
END;

DECLARE @sql       nvarchar(max);
DECLARE @schema    sysname;
DECLARE @table     sysname;
DECLARE @column    sysname;
DECLARE @deleted   bigint = 0;
DECLARE @affected  bigint;
DECLARE @tableCount int = 0;

/* السكيمات المتأثرة — لا نلمس مفاتيح سكيما لم نحذف منها شيئًا.
   Only the schemas we actually purge get their constraints touched. */
IF OBJECT_ID('tempdb..#TouchedSchema') IS NOT NULL DROP TABLE #TouchedSchema;
CREATE TABLE #TouchedSchema (SchemaName sysname PRIMARY KEY);
INSERT INTO #TouchedSchema (SchemaName)
SELECT DISTINCT SchemaName FROM #Purge
UNION
SELECT DISTINCT SchemaName FROM #Keep;

/* كل مفتاح أجنبي يخص جدولًا في تلك السكيمات، في أي من الطرفين. نسجّل حالته
   السابقة: ما كان غير موثوق قبل التشغيل يعود كما كان بلا فحص، حتى لا يفشل
   السكريبت بسبب خلل سابق لا علاقة له بهذا الحذف.
   Every FK with either end inside a touched schema, with its prior state: one
   that was already untrusted comes back untrusted, so a pre-existing violation
   unrelated to this reset cannot fail the run. */
IF OBJECT_ID('tempdb..#Fk') IS NOT NULL DROP TABLE #Fk;
CREATE TABLE #Fk (FkObjectId int PRIMARY KEY, ParentSchema sysname, ParentTable sysname,
                  FkName sysname, WasTrusted bit, WasDisabled bit);
INSERT INTO #Fk (FkObjectId, ParentSchema, ParentTable, FkName, WasTrusted, WasDisabled)
SELECT fk.object_id, ps.name, pt.name, fk.name,
       CASE WHEN fk.is_not_trusted = 0 THEN 1 ELSE 0 END,
       fk.is_disabled
FROM sys.foreign_keys fk
JOIN sys.tables  pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
JOIN sys.tables  rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
WHERE ps.name IN (SELECT SchemaName FROM #TouchedSchema)
   OR rs.name IN (SELECT SchemaName FROM #TouchedSchema);

PRINT N'=============================================================';
PRINT N'  FACTORY RESET — ' + DB_NAME() + N' @ ' + CONVERT(nvarchar(30), SYSUTCDATETIME(), 126) + N'Z';
PRINT N'=============================================================';

BEGIN TRY
    BEGIN TRANSACTION;

    ---------------------------------------------------------------- ١) تعطيل
    PRINT N'-- إيقاف فحص المفاتيح الأجنبية / disabling foreign keys...';
    DECLARE fkOff CURSOR LOCAL FAST_FORWARD FOR
        SELECT ParentSchema, ParentTable, FkName FROM #Fk;
    OPEN fkOff;
    FETCH NEXT FROM fkOff INTO @schema, @table, @column;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table)
                 + N' NOCHECK CONSTRAINT ' + QUOTENAME(@column) + N';';
        EXEC sys.sp_executesql @sql;
        FETCH NEXT FROM fkOff INTO @schema, @table, @column;
    END;
    CLOSE fkOff; DEALLOCATE fkOff;

    ----------------------------------------------------------------- ٢) الحذف
    PRINT N'-- تفريغ الجداول / emptying tables...';
    DECLARE purge CURSOR LOCAL FAST_FORWARD FOR
        SELECT SchemaName, TableName FROM #Purge ORDER BY SchemaName, TableName;
    OPEN purge;
    FETCH NEXT FROM purge INTO @schema, @table;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'DELETE FROM ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) + N';';
        EXEC sys.sp_executesql @sql;
        SET @affected = @@ROWCOUNT;
        IF @affected > 0
            PRINT N'   ' + @schema + N'.' + @table + N' : ' + CONVERT(nvarchar(20), @affected);
        SET @deleted = @deleted + @affected;
        SET @tableCount = @tableCount + 1;
        FETCH NEXT FROM purge INTO @schema, @table;
    END;
    CLOSE purge; DEALLOCATE purge;

    -------------------------------------------------- ٣) الحذف على مستوى الصف
    PRINT N'-- الحسابات والتكليفات / trimming accounts and role assignments...';

    DELETE g
    FROM sec.ScopeGrant g
    WHERE NOT EXISTS (
        SELECT 1 FROM sec.RoleAssignment ra
        JOIN sec.UserAccount u ON u.Id = ra.UserAccountId
        JOIN @KeepUsers k ON k.UserName = u.UserName
        WHERE ra.Id = g.RoleAssignmentId);
    PRINT N'   sec.ScopeGrant : ' + CONVERT(nvarchar(20), @@ROWCOUNT);

    DELETE ra
    FROM sec.RoleAssignment ra
    WHERE NOT EXISTS (
        SELECT 1 FROM sec.UserAccount u
        JOIN @KeepUsers k ON k.UserName = u.UserName
        WHERE u.Id = ra.UserAccountId);
    PRINT N'   sec.RoleAssignment : ' + CONVERT(nvarchar(20), @@ROWCOUNT);

    DELETE u
    FROM sec.UserAccount u
    WHERE NOT EXISTS (SELECT 1 FROM @KeepUsers k WHERE k.UserName = u.UserName);
    PRINT N'   sec.UserAccount : ' + CONVERT(nvarchar(20), @@ROWCOUNT);

    /* الحساب الباقي لم يعد مرتبطًا بموظف محذوف. / The kept account no longer
       points at a deleted person row. */
    UPDATE sec.UserAccount SET PersonId = NULL
    WHERE PersonId IS NOT NULL
      AND EXISTS (SELECT 1 FROM @KeepUsers k WHERE k.UserName = sec.UserAccount.UserName);

    ------------------------------------------- ٤) تفريغ المراجع الاختيارية
    IF EXISTS (SELECT 1 FROM #NullOut)
    BEGIN
        PRINT N'-- تفريغ المراجع المعلّقة الاختيارية / nulling optional references...';
        DECLARE nulls CURSOR LOCAL FAST_FORWARD FOR
            SELECT SchemaName, TableName, ColumnName FROM #NullOut;
        OPEN nulls;
        FETCH NEXT FROM nulls INTO @schema, @table, @column;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @sql = N'UPDATE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table)
                     + N' SET ' + QUOTENAME(@column) + N' = NULL WHERE ' + QUOTENAME(@column) + N' IS NOT NULL;';
            EXEC sys.sp_executesql @sql;
            IF @@ROWCOUNT > 0
                PRINT N'   ' + @schema + N'.' + @table + N'.' + @column;
            FETCH NEXT FROM nulls INTO @schema, @table, @column;
        END;
        CLOSE nulls; DEALLOCATE nulls;
    END;

    ------------------------------------------------- ٥) صفر عدّادات ترقيم ERP
    IF @ResetErp = 1
    BEGIN
        PRINT N'-- تصفير عدّادات ترقيم ERP / resetting ERP document sequences...';
        DECLARE seqs CURSOR LOCAL FAST_FORWARD FOR
            SELECT s.name, t.name
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name IN (N'acc', N'cash', N'inv', N'ptn', N'pur', N'sal')
              AND t.name IN (N'DocumentSequences', N'LabelRunSequences', N'PromotionSequences');
        OPEN seqs;
        FETCH NEXT FROM seqs INTO @schema, @table;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @sql = N'UPDATE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table)
                     + N' SET NextNumber = 1 WHERE NextNumber <> 1;';
            EXEC sys.sp_executesql @sql;
            FETCH NEXT FROM seqs INTO @schema, @table;
        END;
        CLOSE seqs; DEALLOCATE seqs;
    END;

    ------------------------------------------------------- ٦) إعادة الفحص
    PRINT N'-- إعادة تفعيل المفاتيح مع الفحص / re-enabling foreign keys WITH CHECK...';
    DECLARE @wasTrusted bit, @wasDisabled bit;
    DECLARE fkOn CURSOR LOCAL FAST_FORWARD FOR
        SELECT ParentSchema, ParentTable, FkName, WasTrusted, WasDisabled FROM #Fk;
    OPEN fkOn;
    FETCH NEXT FROM fkOn INTO @schema, @table, @column, @wasTrusted, @wasDisabled;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table)
                 + CASE WHEN @wasDisabled = 1 THEN N' NOCHECK CONSTRAINT '
                        WHEN @wasTrusted  = 1 THEN N' WITH CHECK CHECK CONSTRAINT '
                        ELSE                       N' WITH NOCHECK CHECK CONSTRAINT '
                   END
                 + QUOTENAME(@column) + N';';
        EXEC sys.sp_executesql @sql;
        FETCH NEXT FROM fkOn INTO @schema, @table, @column, @wasTrusted, @wasDisabled;
    END;
    CLOSE fkOn; DEALLOCATE fkOn;

    COMMIT TRANSACTION;

    PRINT N'-------------------------------------------------------------';
    PRINT N'  COMMITTED — ' + CONVERT(nvarchar(20), @tableCount) + N' tables emptied, '
                            + CONVERT(nvarchar(20), @deleted) + N' rows deleted.';
    PRINT N'-------------------------------------------------------------';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    /* المفاتيح تُعاد خارج المعاملة أيضًا، فقد يكون الفشل أثناء إعادة التفعيل.
       Constraints are restored outside the transaction too — the failure may
       have happened mid re-enable. WITHOUT CHECK here keeps them enforcing new
       writes without validating the rolled-back data again. */
    BEGIN TRY
        DECLARE fkFix CURSOR LOCAL FAST_FORWARD FOR
            SELECT ParentSchema, ParentTable, FkName FROM #Fk WHERE WasDisabled = 0;
        OPEN fkFix;
        FETCH NEXT FROM fkFix INTO @schema, @table, @column;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @sql = N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table)
                     + N' CHECK CONSTRAINT ' + QUOTENAME(@column) + N';';
            EXEC sys.sp_executesql @sql;
            FETCH NEXT FROM fkFix INTO @schema, @table, @column;
        END;
        CLOSE fkFix; DEALLOCATE fkFix;
    END TRY
    BEGIN CATCH
    END CATCH;

    PRINT N'-------------------------------------------------------------';
    PRINT N'  ROLLED BACK — لم يتغيّر شيء / nothing changed.';
    PRINT N'-------------------------------------------------------------';
    THROW;
END CATCH;

/*------------------------------------------------- ٧) تصفير عدّادات الهوية
  Identity reseed runs after the commit: DBCC CHECKIDENT is not reliably
  transactional. Only empty tables are touched, so it can be re-run safely.
--------------------------------------------------------------------------- */
IF @ResetIdentity = 1
BEGIN
    PRINT N'-- تصفير عدّادات الهوية / reseeding identity columns...';
    DECLARE idents CURSOR LOCAL FAST_FORWARD FOR
        SELECT p.SchemaName, p.TableName
        FROM #Purge p
        WHERE EXISTS (SELECT 1 FROM sys.identity_columns ic
                      WHERE ic.object_id = OBJECT_ID(QUOTENAME(p.SchemaName) + '.' + QUOTENAME(p.TableName)));
    OPEN idents;
    FETCH NEXT FROM idents INTO @schema, @table;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'IF NOT EXISTS (SELECT 1 FROM ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) + N')'
                 + N' DBCC CHECKIDENT (''' + @schema + N'.' + @table + N''', RESEED, 0) WITH NO_INFOMSGS;';
        EXEC sys.sp_executesql @sql;
        FETCH NEXT FROM idents INTO @schema, @table;
    END;
    CLOSE idents; DEALLOCATE idents;
END;

PRINT N'';
PRINT N'تم / Done.';
PRINT N'الخطوة التالية: سجّل الدخول بحساب admin وتأكّد من الشاشات.';
PRINT N'Next: sign in as admin and check the screens.';
PRINT N'تحذير: تشغيل tools/Sms.Seeder سيُعيد بيانات العرض التجريبية.';
PRINT N'Warning: running tools/Sms.Seeder re-creates the demo data.';
