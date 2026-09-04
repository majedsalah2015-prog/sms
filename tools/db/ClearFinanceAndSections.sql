/*
================================================================================
  SMS — تفريغ الحركات المالية والدفعات وإسناد الشُّعب
        Clear student finance, payments and section placement
================================================================================

  ما يفعله هذا السكريبت / What this script does
  ---------------------------------------------
  يمسح ما تحرّك على الطالب ماليًّا، وما قُبض منه، وتوزيعه على الشُّعب — ويُبقي
  الطالب وأسرته وتسجيله في السنة وكل كتالوجات الرسوم كما هي. الغرض: إعادة
  إدخال المالية والتوزيع من الصفر بلا إعادة بناء المدرسة.

  Erases what moved on a student's account, what was collected from them, and
  their placement into sections — keeping the student, their family, their
  enrollment in the year, and every fee catalogue untouched. The point: re-enter
  the money and the placement from scratch without rebuilding the school.

  يُحذَف / DELETED
      المالية   المطالبات، إشعارات الدائن، الأقساط وبنودها، إسناد خطط التقسيط،
                مراجعات الجدولة، حالات إعادة الجدولة، وعود السداد، أحداث
                المتابعة، إشعارات التحصيل، الخصومات الممنوحة ووثائقها،
                الإعفاءات، طابور التجديد، كشوف الحساب المُصدرة
      الدفعات   السندات، تسويات الدفع، سندات الاسترداد، الشيكات الآجلة،
                جلسات الصندوق
      الشُّعب    عضويات الطلبة في الشُّعب
      تبعًا لها  نُسخ سير العمل الجارية على هذه المستندات، وعدّادات الترقيم
                الخاصة بها (INV / RCP / RFD / CRN / DSC / STM / DUN)
      ERP       دفعة تصدير القيود وبنودها، وقيود اليومية وبنودها في acc
                (بالمفتاح ClearErpGl؛ يبقى دليل الحسابات والعملات والسنة المالية)

  يبقى / KEPT
      الطلبة وأولياء الأمور والروابط الأسرية، وتسجيل الطالب في السنة (ppl.Enrollment)
      الشُّعب نفسها وأمناؤها، المراحل والصفوف والمواد والتقويم
      كتالوج الرسوم وبنوده، قوالب التقسيط وأقساطها، أنواع الخصومات وبرامج المنح
      وقواعد الاستحقاق، حسابات التحصيل (ppl.CollectionAccount) وجهات الدفع (ppl.Payer)
      الحضور والدرجات والامتحانات والنقل والمقصف والمكتبة والصحة والسلوك
      سجلّ التدقيق كاملًا، الحسابات والصلاحيات، دليل حسابات ERP وربطها

  ملاحظات مهمة / Important notes
  ------------------------------
  ١) وضع المعاينة افتراضيًا (WhatIf = 1): يعرض ما سيُحذف ولا يغيّر شيئًا.
     اجعله 0 للتنفيذ الفعلي.
     Runs in preview mode by default. Set @WhatIf = 0 to actually delete.

  ٢) التنفيذ كله داخل معاملة واحدة، وتُعاد المفاتيح الأجنبية في النهاية بـ
     WITH CHECK — فإن نتج أي مرجع معلّق تفشل العملية وتتراجع بالكامل.
     Everything runs in one transaction; foreign keys are re-enabled WITH CHECK,
     so any dangling reference aborts and rolls the whole thing back.

  ٣) سجلّ التدقيق (aud) لا يُمسّ عمدًا: هو سجلّ ما حدث، وحذف المستند لا يُلغي
     أنه كان. ستبقى فيه قيود تشير إلى صفوف لم تعد موجودة — وهذا هو المقصود.
     The audit log is deliberately left alone: it records what happened, and
     deleting a document does not un-happen it. Entries pointing at rows that no
     longer exist are expected, not a defect.

  ٤) الاشتراكات والمبيعات الخدمية (النقل، المقصف، المتجر، غرامات المكتبة) تبقى
     صفوفها، ويُفرَّغ فيها عمود المطالبة/السند إلى NULL — فتصير «مشترك بلا
     مطالبة». إن كنت تريد حذفها هي أيضًا فهي ليست ضمن هذا السكريبت.
     Service subscriptions and sales keep their rows; their charge/receipt
     column is set to NULL, leaving them "subscribed but never charged". If you
     want those gone too, that is outside this script.

  ٥) هذا السكريبت لا يُنشئ نسخة احتياطية. خُذ نسخة قبل التنفيذ:
     This script takes no backup. Take one first:
         BACKUP DATABASE [Sms] TO DISK = N'C:\Temp\Sms_before_clear.bak' WITH INIT;

  ٦) للتفريغ الكامل (طلبة وموظفين وكل شيء) استعمل FactoryReset.sql بدلًا منه.
     For a full wipe use FactoryReset.sql instead.

  الاستخدام / Usage
  -----------------
      chcp 65001
      sqlcmd -S .\SQLEXPRESS -E -d Sms -f 65001 -i tools\db\ClearFinanceAndSections.sql

      أو افتحه في SSMS وشغّله هناك — أبسط، وترى جداول المعاينة كما هي.
      Or just open it in SSMS and run it there — simpler, and the preview grids
      come out readable.

      (الرسائل هنا عربية، ولإظهارها سليمة في سطر الأوامر يلزم الأمران معًا:
       chcp 65001 لترميز الشاشة، و -f 65001 ليقرأ sqlcmd الملف. أحدهما وحده
       لا يكفي. الملف محفوظ بـ UTF-8 مع BOM من أجل SSMS والمحرّرات — أبقِ الـ
       BOM عند تعديله.
       The messages are Arabic. Rendering them in a console needs BOTH: chcp
       65001 for the screen and -f 65001 for sqlcmd's reader — either one alone
       still gives mojibake. The file is UTF-8 with a BOM for SSMS and editors;
       keep the BOM when editing it.)
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
DECLARE @ClearFinance    bit     = 1;        -- 1 = حذف المطالبات والأقساط والخصومات
DECLARE @ClearPayments   bit     = 1;        -- 1 = حذف السندات والتسويات وجلسات الصندوق
DECLARE @ClearSections   bit     = 1;        -- 1 = حذف عضويات الطلبة في الشُّعب
DECLARE @ClearErpGl      bit     = 1;        -- 1 = حذف قيود ERP الناتجة عن هذه الحركات
DECLARE @ResetNumbering  bit     = 1;        -- 1 = تصفير عدّادات ترقيم المستندات المحذوفة
DECLARE @CloseWorkflows  bit     = 1;        -- 1 = حذف نُسخ سير العمل المعلّقة على المحذوف
DECLARE @ResetIdentity   bit     = 1;        -- 1 = إعادة عدّادات الهوية إلى 1

/*--------------------------------------------------------------- حارس القاعدة
  Guard: refuse to run against the wrong database.
--------------------------------------------------------------------------- */
IF DB_NAME() <> @ConfirmDatabase
BEGIN
    RAISERROR(N'ABORT: connected to [%s] but @ConfirmDatabase is [%s]. Nothing was changed.',
              16, 1, @@SERVERNAME, @ConfirmDatabase);
    RETURN;
END;

/*------------------------------------------------------- الجداول التي تُفرَّغ
  An explicit DELETE list — nothing is emptied by omission. A table added to the
  model later is NOT cleared until it is named here, which is the safe default
  for a targeted purge (FactoryReset.sql takes the opposite, keep-list approach).
--------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#Wanted') IS NOT NULL DROP TABLE #Wanted;
CREATE TABLE #Wanted (SchemaName sysname NOT NULL, TableName sysname NOT NULL,
                      Origin varchar(3) NOT NULL, PRIMARY KEY (SchemaName, TableName));

IF @ClearFinance = 1
    INSERT INTO #Wanted (SchemaName, TableName, Origin) VALUES
        -- المطالبات وإشعارات الدائن / charges and credit notes
        (N'ppl', N'Charge',                'FIN'), (N'ppl', N'CreditNote',            'FIN'),
        -- التقسيط / installment plans and their revisions
        (N'ppl', N'Installment',           'FIN'), (N'ppl', N'InstallmentChargeLine', 'FIN'),
        (N'ppl', N'PlanAssignment',        'FIN'), (N'ppl', N'ScheduleRevision',      'FIN'),
        (N'ppl', N'RescheduleCase',        'FIN'),
        -- التحصيل والمتابعة / collection and dunning
        (N'ppl', N'PromiseToPay',          'FIN'), (N'ppl', N'DunningEvent',          'FIN'),
        (N'ppl', N'CollectionNotice',      'FIN'), (N'ppl', N'StatementIssue',        'FIN'),
        -- الخصومات والإعفاءات / discounts and waivers
        (N'ppl', N'DiscountGrant',         'FIN'), (N'ppl', N'DiscountDocument',      'FIN'),
        (N'ppl', N'Waiver',                'FIN'), (N'ppl', N'RenewalQueueItem',      'FIN');

IF @ClearPayments = 1
    INSERT INTO #Wanted (SchemaName, TableName, Origin) VALUES
        (N'ppl', N'Receipt',               'PAY'), (N'ppl', N'PaymentAllocation',     'PAY'),
        (N'ppl', N'RefundVoucher',         'PAY'), (N'ppl', N'Pdc',                   'PAY'),
        (N'ppl', N'TillSession',           'PAY');

IF @ClearSections = 1
    INSERT INTO #Wanted (SchemaName, TableName, Origin) VALUES
        (N'core', N'SectionMembership',    'SEC');

/* ERP: قيود اليومية ودفعة التصدير التي أنتجتها هذه الحركات. دليل الحسابات
   والعملات والسنة المالية والربط كلها تبقى.
   ERP: the journal this finance produced, and the export batch that carried it.
   The chart of accounts, currencies, fiscal calendar and mappings all stay. */
IF @ClearErpGl = 1
    INSERT INTO #Wanted (SchemaName, TableName, Origin) VALUES
        (N'fin', N'GlJournalLine',         'ERP'), (N'fin', N'GlExportBatch',         'ERP'),
        (N'acc', N'JournalEntryLines',     'ERP'), (N'acc', N'JournalEntries',        'ERP');

/* الجداول الموجودة فعلًا فقط — قاعدة بلا سكيمات ERP تتخطّاها بهدوء.
   Only tables that actually exist — a database without the ERP schemas skips
   them quietly instead of failing. */
IF OBJECT_ID('tempdb..#Purge') IS NOT NULL DROP TABLE #Purge;
CREATE TABLE #Purge (SchemaName sysname NOT NULL, TableName sysname NOT NULL,
                     Origin varchar(3) NOT NULL, PRIMARY KEY (SchemaName, TableName));
INSERT INTO #Purge (SchemaName, TableName, Origin)
SELECT w.SchemaName, w.TableName, w.Origin
FROM #Wanted w
WHERE EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
              WHERE s.name = w.SchemaName AND t.name = w.TableName);

IF NOT EXISTS (SELECT 1 FROM #Purge)
BEGIN
    RAISERROR(N'ABORT: every switch is off — there is nothing to clear.', 16, 1);
    RETURN;
END;

/*----------------------------------------- الكيانات المحذوفة بالاسم المنطقي
  Entity names, used for the two row-level cleanups that key on text rather
  than a foreign key: workflow instances and numbering counters. In this model
  the table name IS the entity name, so it is derived, not re-typed.
--------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#PurgedEntity') IS NOT NULL DROP TABLE #PurgedEntity;
CREATE TABLE #PurgedEntity (EntityName sysname PRIMARY KEY);
INSERT INTO #PurgedEntity (EntityName)
SELECT DISTINCT TableName FROM #Purge WHERE Origin IN ('FIN', 'PAY', 'SEC');
/* دفعة التصدير كيان مُرقَّم في SMS رغم أن أثرها في ERP.
   The export batch is an SMS-numbered entity even though its effect is in ERP. */
IF @ClearErpGl = 1 AND NOT EXISTS (SELECT 1 FROM #PurgedEntity WHERE EntityName = N'GlExportBatch')
    INSERT INTO #PurgedEntity (EntityName) VALUES (N'GlExportBatch');

/*--------------------------------- الأعمدة الاختيارية التي ستُفرَّغ إلى NULL
  A kept table may hold a nullable FK into a purged one — a transport or meal
  subscription pointing at its charge, a store sale at its receipt. Those are
  set to NULL so the FK re-check passes. Multi-column FKs are reported, never
  guessed at.
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

/*------------------------------- المراجع الإلزامية من جدول باقٍ إلى جدول محذوف
  A REQUIRED foreign key from a kept table into a purged one cannot be resolved
  by nulling: the kept row would have to go too. That means the delete list is
  wrong, so the script refuses rather than widening its own scope. Same for a
  multi-column FK, which is not safe to null blindly.
--------------------------------------------------------------------------- */
IF OBJECT_ID('tempdb..#Blocked') IS NOT NULL DROP TABLE #Blocked;
CREATE TABLE #Blocked (Reason varchar(20), FkName sysname, Detail nvarchar(400), ParentRows bigint);

INSERT INTO #Blocked (Reason, FkName, Detail, ParentRows)
SELECT DISTINCT
       CASE WHEN 1 < (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id = fk.object_id)
            THEN 'MULTI-COLUMN' ELSE 'REQUIRED' END,
       fk.name,
       ps.name + N'.' + pt.name + N'.' + pc.name + N' -> ' + rs.name + N'.' + rt.name,
       (SELECT ISNULL(SUM(pa.rows), 0) FROM sys.partitions pa
        WHERE pa.object_id = pt.object_id AND pa.index_id IN (0, 1))
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables  pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables  rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
WHERE EXISTS     (SELECT 1 FROM #Purge p WHERE p.SchemaName = rs.name AND p.TableName = rt.name)
  AND NOT EXISTS (SELECT 1 FROM #Purge p WHERE p.SchemaName = ps.name AND p.TableName = pt.name)
  AND (pc.is_nullable = 0
       OR 1 < (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id = fk.object_id));

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

    SELECT [Group] = Origin, SchemaName, TableName, RowsNow
    FROM #PurgeRows
    ORDER BY CASE Origin WHEN 'FIN' THEN 1 WHEN 'PAY' THEN 2 WHEN 'SEC' THEN 3 ELSE 4 END,
             RowsNow DESC, SchemaName, TableName;

    SELECT [Tables to empty] = COUNT(*),
           [Of which non-empty] = SUM(CASE WHEN RowsNow > 0 THEN 1 ELSE 0 END),
           [Rows to delete] = SUM(RowsNow)
    FROM #PurgeRows;

    IF @CloseWorkflows = 1
        SELECT [Workflow instances deleted] = wi.Id, wi.EntityTypeName, wi.EntityId,
               wi.BusinessKey, wi.IsClosed
        FROM wf.WorkflowInstance wi
        JOIN #PurgedEntity e ON e.EntityName = wi.EntityTypeName;

    IF @ResetNumbering = 1
        SELECT [Numbering counters reset] = ns.Code, ns.EntityName,
               ss.ResetKey, [LastIssued] = ss.LastIssuedSequence
        FROM core.SeriesState ss
        JOIN core.NumberingSeries ns ON ns.Id = ss.NumberingSeriesId
        JOIN #PurgedEntity e ON e.EntityName = ns.EntityName;

    SELECT [Columns set to NULL] = SchemaName + N'.' + TableName + N'.' + ColumnName,
           [Because it points at] = Target
    FROM #NullOut ORDER BY 1;

    IF EXISTS (SELECT 1 FROM #Blocked)
        SELECT [BLOCKED — a kept table requires a purged row] = Reason,
               FkName, Detail, ParentRows FROM #Blocked ORDER BY 1, 3;
    ELSE
        PRINT N'-- لا مرجع إلزامي من جدول باقٍ إلى محذوف / no required reference from a kept table.';

    RETURN;
END;

/*========================================================== التنفيذ الفعلي ===
  Execute. One transaction; FKs re-checked at the end.
--------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM #Blocked)
BEGIN
    RAISERROR(N'ABORT: a kept table holds a required reference into a table this script would empty. Run with @WhatIf = 1 to see which, then narrow or widen the delete list deliberately.', 16, 1);
    RETURN;
END;

DECLARE @sql       nvarchar(max);
DECLARE @schema    sysname;
DECLARE @table     sysname;
DECLARE @column    sysname;
DECLARE @deleted   bigint = 0;
DECLARE @affected  bigint;
DECLARE @tableCount int = 0;

/* المفاتيح الأجنبية التي قد ينتهكها الحذف هي وحدها التي تُعطَّل: كل مفتاح
   طرفه المرجعي جدولٌ سنُفرِّغه. نسجّل حالته السابقة — ما كان غير موثوق قبل
   التشغيل يعود كما كان بلا فحص، حتى لا يفشل السكريبت بخلل سابق لا يخصّه.
   Only the foreign keys this delete could violate are disabled: those whose
   REFERENCED end is a table we empty. Each keeps its prior state — one that was
   already untrusted comes back untrusted, so a pre-existing violation unrelated
   to this clear cannot fail the run. */
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
WHERE EXISTS (SELECT 1 FROM #Purge p WHERE p.SchemaName = rs.name AND p.TableName = rt.name);

PRINT N'=============================================================';
PRINT N'  CLEAR FINANCE / PAYMENTS / SECTIONS — ' + DB_NAME()
     + N' @ ' + CONVERT(nvarchar(30), SYSUTCDATETIME(), 126) + N'Z';
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

    ------------------------------------------------- ٣) نُسخ سير العمل المعلّقة
    /* wf.WorkflowInstance تشير إلى مستندها بالاسم والمعرّف نصًّا، لا بمفتاح
       أجنبي — فلا يمنعها شيء من البقاء معلّقة في صندوق الاعتماد بعد حذفه.
       WorkflowInstance points at its document by name and id as text, not by a
       foreign key, so nothing stops it from lingering in an approver's inbox
       after the document is gone. */
    IF @CloseWorkflows = 1
    BEGIN
        PRINT N'-- سير العمل المعلّق على مستندات محذوفة / workflow instances on deleted documents...';

        DELETE s
        FROM wf.WorkflowStep s
        JOIN wf.WorkflowInstance wi ON wi.Id = s.WorkflowInstanceId
        JOIN #PurgedEntity e ON e.EntityName = wi.EntityTypeName;
        PRINT N'   wf.WorkflowStep : ' + CONVERT(nvarchar(20), @@ROWCOUNT);

        DELETE wi
        FROM wf.WorkflowInstance wi
        JOIN #PurgedEntity e ON e.EntityName = wi.EntityTypeName;
        PRINT N'   wf.WorkflowInstance : ' + CONVERT(nvarchar(20), @@ROWCOUNT);
    END;

    ------------------------------------------------------ ٤) عدّادات الترقيم
    /* السلاسل بسياسة Strict تضمن تسلسلًا بلا فجوات؛ ترك العدّاد على ٧ بعد حذف
       المطالبات السبع يترك فجوة دائمة من ١ إلى ٧. حذف صف الحالة يُعيد الترقيم
       إلى ١ ويُنشئه المُصدِر عند أول مستند جديد.
       Strict series promise a gapless sequence; leaving the counter at 7 after
       deleting the seven charges leaves a permanent 1..7 hole. Deleting the
       state row restarts at 1, and the issuer recreates it on the next document. */
    IF @ResetNumbering = 1
    BEGIN
        PRINT N'-- تصفير عدّادات ترقيم المستندات المحذوفة / resetting document counters...';
        DELETE ss
        FROM core.SeriesState ss
        JOIN core.NumberingSeries ns ON ns.Id = ss.NumberingSeriesId
        JOIN #PurgedEntity e ON e.EntityName = ns.EntityName;
        PRINT N'   core.SeriesState : ' + CONVERT(nvarchar(20), @@ROWCOUNT);
    END;

    ------------------------------------------- ٥) تفريغ المراجع الاختيارية
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
                PRINT N'   ' + @schema + N'.' + @table + N'.' + @column + N' : ' + CONVERT(nvarchar(20), @@ROWCOUNT);
            FETCH NEXT FROM nulls INTO @schema, @table, @column;
        END;
        CLOSE nulls; DEALLOCATE nulls;
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
PRINT N'الطلبة وأولياء الأمور وتسجيلهم في السنة لم تُمسّ. أعد التوزيع على الشُّعب';
PRINT N'من شاشة الشُّعب، ثم أصدر الرسوم من جديد.';
PRINT N'Students, parents and their enrollment are untouched. Re-do the section';
PRINT N'placement from the Sections screen, then re-issue the fees.';
