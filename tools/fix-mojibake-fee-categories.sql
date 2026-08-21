-- Repairs two ppl.FeeCategory rows whose Arabic names were stored as U+FFFD
-- replacement characters.
--
-- Cause, for the record: both rows were created 2026-08-19 19:32, one second
-- apart, by an authenticated session — manual curl smoke-testing of the E-303
-- screens. A Windows console sends form bytes in the OEM/ANSI codepage, ASP.NET
-- decodes the request as UTF-8, and every Arabic character lands as U+FFFD. The
-- application itself is not at fault: the columns are nvarchar, and every other
-- Arabic-bearing table (Student, Parent, PlanTemplate) has zero damaged rows.
--
-- Run with an explicit input codepage, or sqlcmd will re-create the same problem
-- while reading this file:
--     sqlcmd -S .\SQLEXPRESS -E -d Sms -f 65001 -i tools\fix-mojibake-fee-categories.sql
--
-- Verify afterwards — UNICODE() of an Arabic first letter is 1536..1791:
--     SELECT Id, NameEn, UNICODE(NameAr) FROM ppl.FeeCategory;

SET NOCOUNT ON;

UPDATE ppl.FeeCategory SET NameAr = N'رسوم دراسية (اختبار E303)' WHERE Id = 3 AND UNICODE(NameAr) = 65533;
UPDATE ppl.FeeCategory SET NameAr = N'كتب (اختبار E303)'         WHERE Id = 4 AND UNICODE(NameAr) = 65533;

SELECT 'Id=' + CAST(Id AS varchar) + ' | ' + NameEn + ' | codepoint=' + CAST(UNICODE(NameAr) AS varchar)
     + ' | ' + CASE WHEN UNICODE(NameAr) BETWEEN 1536 AND 1791 THEN 'ARABIC-OK' ELSE 'STILL DAMAGED' END
FROM ppl.FeeCategory
ORDER BY Id;
