/* ---------------------------------------------------------------------------
   The permissions the enrollment row-actions on /students/{id} (academic tab)
   are gated on.

   Adds two rows:
     STU / Enrollment / Edit(3)        -> the "تعديل" button (correct the grade)
     STU / Enrollment / Deactivate(4)  -> the "حذف"  button (remove the enrollment)

   It deliberately does NOT touch SEC / Roster / Edit(3) — the "إخراج من الشعبة"
   button is gated on that, and every seeded database already holds it, granted
   to SYSADMIN, REGISTRAR and HOMEROOM_TEACHER.

   This is the permission half of what tools/Sms.Seeder does, and nothing else:
   no demo tenant, no demo staff, no portal accounts, no sysadmin account. Use it
   where running the whole seeder would be wrong — a database that carries real
   school data.

   sec.Permission is not school-scoped; sec.Role and sec.RolePermission are, so
   the grant takes its SchoolId from the role it grants to.

   Idempotent: inserts only what is missing, so it is safe to run repeatedly.
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* 1) The permission rows themselves. */
INSERT INTO sec.Permission (ModuleCode, ScreenCode, Action, CreatedByUserId, CreatedAtUtc)
SELECT v.ModuleCode, v.ScreenCode, v.Action, 0, SYSUTCDATETIME()
FROM (VALUES ('STU', 'Enrollment', 3),
             ('STU', 'Enrollment', 4)) AS v (ModuleCode, ScreenCode, Action)
WHERE NOT EXISTS (SELECT 1
                  FROM sec.Permission p
                  WHERE p.ModuleCode = v.ModuleCode
                    AND p.ScreenCode = v.ScreenCode
                    AND p.Action     = v.Action);

/* 2) Grant them.

   SYSADMIN alone is exactly what PermissionSeedContributor does: it tops up the
   system administrator on every run and leaves staff roles as the school curated
   them, because revoking from a role is a decision and this is not.

   Uncomment REGISTRAR to let the front desk re-grade and remove enrollments as
   well — that is the decision the split between Create and Edit/Deactivate
   exists to let a school make. */
DECLARE @Roles TABLE (Code nvarchar(64) PRIMARY KEY);
INSERT INTO @Roles (Code) VALUES ('SYSADMIN');
-- INSERT INTO @Roles (Code) VALUES ('REGISTRAR');

INSERT INTO sec.RolePermission (SchoolId, RoleId, PermissionId, CreatedByUserId, CreatedAtUtc)
SELECT r.SchoolId, r.Id, p.Id, 0, SYSUTCDATETIME()
FROM sec.Permission p
CROSS JOIN sec.Role r
WHERE p.ModuleCode = 'STU'
  AND p.ScreenCode = 'Enrollment'
  AND p.Action IN (3, 4)
  AND r.Code IN (SELECT Code FROM @Roles)
  AND NOT EXISTS (SELECT 1
                  FROM sec.RolePermission rp
                  WHERE rp.RoleId       = r.Id
                    AND rp.PermissionId = p.Id
                    AND rp.SchoolId     = r.SchoolId);

COMMIT TRANSACTION;

/* 3) What the database holds now — the two rows and who can reach them. */
SELECT r.Code AS RoleCode, p.ModuleCode, p.ScreenCode, p.Action
FROM sec.RolePermission rp
JOIN sec.Role       r ON r.Id = rp.RoleId
JOIN sec.Permission p ON p.Id = rp.PermissionId
WHERE p.ModuleCode = 'STU' AND p.ScreenCode = 'Enrollment' AND p.Action IN (3, 4)
ORDER BY r.Code, p.Action;
