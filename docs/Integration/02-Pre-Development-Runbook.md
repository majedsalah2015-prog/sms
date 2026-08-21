# دليل التنفيذ: ما نعمله الآن قبل استئناف تطوير المدرسة

**التاريخ:** 2026-08-21 · **يسبق:** [01-Embedded-Accounting-Plan.md](01-Embedded-Accounting-Plan.md) · **الطبيعة:** خطوات تنفيذية مرتّبة، كل خطوة لها بوابة قبول

> **القاعدة:** لا تُنفَّذ خطوة قبل أن تمرّ بوابة سابقتها. الترتيب ليس اقتراحاً — الخطوة 6 (الحذف) تدمّر ما لم تُنقذه الخطوات 2–5.

---

## 🔴 الخطوة 0 — أخطر ما في المشروع: لا نسخة احتياطية للمدرسة

**الاكتشاف:** مستودع `E:\school2028\sms` **ليس له أي remote**. لا GitHub، لا نسخة، لا شيء.

```
sms      →  git remote -v  →  (فارغ)
ERP_2028 →  origin  https://github.com/majedsalah2015-prog/erp-2028.git ✅
```

يعني: نظام المدرسة كاملاً — 20+ إيداعاً، 1236 اختباراً، شهور عمل — موجود **على هذا القرص فقط**. عطل قرص أو حذف بالخطأ = فقدان كل شيء. الـ ERP محميّ، المدرسة لا.

**العمل:**
1. إنشاء مستودع **خاص** على GitHub باسم `sms-2028` (أو `school-2028`).
2. ربطه ودفع `main`.

```bash
git -C "E:/school2028/sms" remote add origin <رابط المستودع الخاص>
```
```bash
git -C "E:/school2028/sms" push -u origin main
```

**⚠️ خاص لا عام** — النظام تجاري ويحوي أسرار عملاء محتملة.

**بوابة القبول:** `git -C sms remote -v` يعرض origin، و`git log origin/main -1` يطابق المحلي.

---

## الخطوة 1 — تحرير قفل البناء

عملية `Sms.Web.exe` (PID 4620، بدأت 9:24 صباحاً) ما زالت شغّالة وتحتجز `bin\Debug\net5.0\Sms.Web.exe`، فيفشل بناء الحلّ كاملاً بخطأ نسخ ملف (لا خطأ كود).

```bash
taskkill //PID 4620 //F
```

**بوابة القبول:** `dotnet build "E:/school2028/sms/Sms.sln"` → **0 خطأ / 0 تحذير**.

---

## الخطوة 2 — تأمين شجرة العمل (61 معدَّلاً + 9 مصادر غير متتبَّعة)

### 2-أ. حذف الخردة أولاً — فيها رموز جلسات

عشرة ملفات في جذر المستودع **غير متجاهَلة في `.gitignore`**، وبعضها رموز مصادقة من اختبار يدوي. لو أُودعت بالخطأ لصارت أسراراً في التاريخ:

```
token.txt  etoken.txt  cj.txt  ecj.txt
after.html  eafter.html  home.html  ehome.html  login.html  elogin.html
```

احذفها، ثم أضف إلى `.gitignore`:

```gitignore
# مخرجات اختبار يدوي (cookie jars, رموز، صفحات محفوظة)
/*.html
/token.txt
/etoken.txt
/cj.txt
/ecj.txt
```

### 2-ب. إيداع العمل الحقيقي

| الفئة | المحتوى |
|---|---|
| 61 ملفاً معدَّلاً (+531/−407) | تكملة سويب الإتاحة WCAG: `aria-label` على `<select>` بلا تسمية، ربط `<label for>` بـ `id` |
| تعديل وظيفي | `PortalController.Ping()` — نقطة نهاية تمديد الجلسة (WCAG 2.2.1، رفيقة BR-SEC-013) |
| **9 ملفات مصدر غير متتبَّعة** | `DiscountsController.cs` · `DiscountsViewModels.cs` · `InstallmentsViewModels.cs` · 4 مشاهد `Attendance` · مجلد `Views/Discounts/` · `_DiscountsNav.cshtml` |

> ⚠️ التسعة تُترجم فعلاً ضمن البناء الناجح (globs الـ SDK) — أي أن النظام يعتمد عليها اليوم وهي **خارج نظام الإصدارات**. أي `git clean` يمحوها.

يُفضَّل إيداعان منفصلان: واحد لشاشات الخصومات/الحضور، وواحد لسويب WCAG.

**بوابة القبول:** `git status --short` **فارغ**، والاختبارات 1236/1236 خضراء، والتغييرات مدفوعة إلى origin.

---

## الخطوة 3 — إنقاذ وثائق المدرسة

~80 وثيقة في `E:\school2028\docs` **خارج أي مستودع**. مستودع `sms` يتتبّع `src` و`tests` و`tools` فقط.

**ينتقل إلى `E:\school2028\sms\docs\`:**

| المحتوى | العدد |
|---|---|
| `00-Project-Vision.md` … `10-Attachments.md` | 11 |
| `Modules/` (مواصفات الوحدات 01–36) | 37 |
| `Implementation/` (بوابات IP-0…IP-7 + Spike + PDFs) | 17 |
| `Database/` · `UI/` · `Future/` · `Reports/` | 16 |
| `Integration/` (التحليل + الخطة + هذا الدليل) | 3 |
| `README.md` | 1 |

**يبقى ويُحذف مع النسخة (الخطوة 6):** `Api` · `Architecture` · `Communication` · `DesignSystem` · `Dimensions` · `Finance` · `FixedAssets` · `Frontend` · `Inventory` · `Partners` · `Purchasing` · `Sales` · `Sprints` · `System` — كلها نسخ من وثائق الـ ERP، **متطابقة تماماً** مع الأصل (عدا `Inventory` الأقدم).

**ينتقل أيضاً:**
- `E:\school2028\.claude\settings.local.json` → `sms\.claude\` (فيه 32 صلاحية خاصة بالمدرسة)
- `SQLQuery3.sql` + `SQLQuery4.sql` → `sms\tools\` (سكربتا تنظيف بيانات مدرسة، فريدان)

> **ملاحظة:** تعليقات في كود المدرسة تستشهد بوثائق الـ ERP (مثل `Startup.cs:624` يذكر `doc/DesignSystem/01`). هذه استشهادات نصية لا تبعية تشغيل — المرجع بعد الحذف يصبح `E:\ERP_2028\docs\`.

**بوابة القبول:** `git -C sms ls-files docs | wc -l` ≈ 85، ومدفوع إلى origin.

---

## الخطوة 4 — إنقاذ عمل الـ ERP الفريد الموجود في النسخة

هذه أهم خطوة يسهل نسيانها. النسخة تحوي **عملاً حقيقياً غير موجود في `E:\ERP_2028` ولا في تاريخه**.

### 4-أ. ميزة POS Sales Inquiry — 8 ملفات، ~450 سطراً

`PosOrderTotals` + `GetTotalsAsync` (تجميع في قاعدة البيانات) + `GetAllAsync` (تصدير غير مُصفَّح) + تصدير CSV + زر "فتح في نقطة البيع" + نقطة نهاية `TillController.Load`.

```
src/Modules/Sales/…Domain/Repositories/Repositories.cs                    +33
src/Modules/Sales/…Application/Pos/PosOrderService.cs                     +14
src/Modules/Sales/…Infrastructure/…/PosRepositories.cs                    +49/−8
src/Modules/Sales/…Web/Areas/POS/Controllers/PosOrdersController.cs      +136/−4
src/Modules/Sales/…Web/Areas/POS/Controllers/TillController.cs            +12
src/Modules/Sales/…Web/Areas/POS/Views/PosOrders/Index.cshtml            +135/−4
src/Modules/Sales/…Web/Areas/POS/Views/PosOrders/Details.cshtml           +32
src/Bootstrap/ERP2028.Web/wwwroot/js/pos-till.js                          +39
```

النسخ المباشر آمن: الـ ERP لم يمسّ أياً من الثمانية منذ الانفصال (آخر تعديل 08-03…08-10، الانفصال ~08-13).

> ⛔ **لا تنسخ** `TerminalsController.cs` · `ShiftService.cs` · `Terminals/Form.cshtml` — الـ ERP **أحدث** فيها.

### 4-ب. رابط النظامين — 3 مواضع

`appsettings.json`:
```json
"ExternalApps": { "SmsUrl": "http://localhost:5000" },
```
`_Layout.cshtml`: حقن `IConfiguration` + قراءة `ExternalApps:SmsUrl` + زر `bi-mortarboard`.

### 4-ج. سبعة سطور ترجمة — **يدوياً، لا نسخ الملف**

`SharedResource.ar.resx` في النسخة **أقدم** وينقصه ~150 سطر Manufacturing. تُضاف السبعة فقط:
`School Management System` · `Pay` · `Open in till` · `This page` · `{0} sales` · `{0} cancelled` · `Sale reopened for editing.`

**بوابة القبول:** `dotnet clean && dotnet build "E:/ERP_2028/ERP2028.sln"` أخضر، ثم **إيداع ودفع إلى origin** — تاريخ الـ ERP فيه إيداعان فقط، فهو ليس شبكة أمان بعد.

---

## الخطوة 5 — توحيد الـ SDK

`E:\ERP_2028` بلا `global.json` → يُبنى بـ SDK 10.0.303 بينما `sms` مثبَّت على 5.0.409. الخلط يولّد فشل بناء وهمياً (`StaticWebAssets.xml` بنمط 5 مقابل `.cache` بنمط 10) — حدث فعلاً في الفحص وتطلّب `clean` كاملاً.

أنشئ `E:\ERP_2028\global.json`:
```json
{ "sdk": { "version": "5.0.409" } }
```

> الجهاز **لا يملك runtime لـ 5.0** (أحدث 10.0.11). `RollForward: LatestMajor` في `sms/Directory.Build.props` هو ما يجعل التشغيل ممكناً — تُنقل نفس الخاصية لمشاريع الـ ERP عند الاستضافة من `Sms.Web` (المرحلة P1).

**بوابة القبول:** بناءان متتاليان نظيفان في المستودعين دون `clean` بينهما.

---

## الخطوة 6 — حذف النسخة المكرّرة ⚠️

**لا تُنفَّذ إلا بعد مرور بوابات 0–5 كلها.**

يُحذف من `E:\school2028\`:

| العنصر | لماذا |
|---|---|
| `src\` · `tests\` · `tools\` · `ERP2028.sln` | نسخة ERP أقدم (تنقصها Manufacturing كاملة) |
| `docs\` — الـ14 مجلد ERP فقط | متطابقة مع الأصل |
| `erp2028` (64 MB) | نسخة `.bak` لقاعدة SQL Server — **md5 متطابق** مع الأصل |
| `SQLQuery1.sql` · `SQLQuery2.sql` · `std.txt` · `dell.txt` · `.applog.txt` · `ERP_Standard_Chart_of_Accounts_Sample.xlsx` · `README.md` · `.gitattributes` · `.gitignore` | md5 متطابق مع الأصل |
| `run-app.log` · `grep.exe.stackdump` · `.vs\` | ضجيج |

**قبل الحذف، ملاحظة من `run-app.log`:** خطأ متكرر يستحق تذكرة —
`UnknownJobException: No JobDefinition registered for code 'SnapshotDailyAttendanceSummary'` عند `Sms.Infrastructure/Jobs/JobRunner.cs:38` (المحاولة 8 من 10).

**بوابة القبول:** `E:\school2028\` يحوي `sms\` فقط، والمدرسة تُبنى وتُشغَّل بلا خلل.

---

## الخطوة 7 — البنية النهائية

```
E:\ERP_2028\          ← مستودع الـ ERP (origin على GitHub) — مرجع المحاسبة
E:\school2028\
   └── sms\           ← مستودع المدرسة (origin جديد)
        ├── docs\     ← كل وثائق المدرسة، مُصدَّرة الآن
        ├── src\  tests\  tools\
        └── .claude\
```

---

# ══════ بوابة استئناف تطوير المدرسة ══════

**بعد هذا السطر يمكن استئناف تطوير المدرسة بشكل مستقل وآمن.** الشروط الأربعة المتحقّقة:

| # | الشرط |
|---|---|
| 1 | كل شيء مُودَع **ومدفوع إلى remote** — لا عمل بلا نسخة احتياطية |
| 2 | كل الوثائق داخل المستودع — لا مخرجات تحليل بلا إصدارات |
| 3 | لا نسخة ERP مكرّرة — لا مكان يُعدَّل فيه الكود الخطأ |
| 4 | بناء نظيف + 1236 اختباراً أخضر في المستودعين |

**التطوير المستقل بعدها لا يتعارض مع دمج المحاسبة** لأن الدمج كله إضافات: مشروع جديد (`Sms.Erp.Bridge`)، ملفات جديدة، وأسطر في `Startup.cs`. صفر تعديل على `Sms.Domain` أو `Sms.Application` أو `Sms.Infrastructure`.

---

## ✅ الخطوة 8 — P1: هيكل الدمج — **منفَّذة** (`c20ca0d`)

1. ✅ `external/erp` submodule مثبَّت على `erp-2028 a86fb3a`.
2. ✅ مجلد حلّ `ERP` في `Sms.sln` بـ **15 مشروعاً** مرجعياً: اللبنات الخمس + مشاريع Accounting الخمسة + مشاريع Organization الخمسة (‏`Accounting.Application` يحقن `IBranchDirectory`، فـ Organization ليست اختيارية).
3. ✅ `src/Sms.Erp.Bridge` يعتمد على `Accounting.Contracts` **فقط**، ومعه أول مُهايئ (`ErpClockAdapter`) — أُدرج عمداً بدل هيكل فارغ لأنه لا يُترجم إلا إذا التقى تجريد من الـ ERP وتجريد من `Sms.Application` في تجميعة واحدة، وهذه هي الدعوى التي وُجدت هذه الخطوة لإثباتها.
4. ✅ `ErpBoundaryTests` يفرض الحدّ من الطرفين: طبقات المدرسة الثلاث لا تعتمد على أي `ERP2028.*`، والجسر لا يتجاوز `Accounting.Contracts`.
5. ✅ CI يسحب الـ submodules (وإلا لا يُستعاد الحل) **ويفشل** إن كان في `external/erp` أي تعديل محلي.
6. ✅ `external/Directory.Build.props` يوقف التوريث — لولاه لورث كود الـ ERP قاعدة `TreatWarningsAsErrors=true` الخاصة بهذا المستودع.

**بوابة القبول — مرّت:** البناء 0 تحذير / 0 خطأ · **1240 اختباراً ناجحاً** (1236 + أربعة اختبارات الحدّ الجديدة).

---

## قواعد العمل بعد ذلك

| # | القاعدة |
|---|---|
| 1 | **ممنوع تعديل أي ملف تحت `sms/external/erp`.** أي تعديل محاسبي يُكتب في `E:\ERP_2028` ويُودع هناك، فيصل للمنتجين معاً |
| 2 | الـ submodule **مثبَّت على commit** — أنت تختار متى تستوعب تغييرات المحاسبة |
| 3 | طقس المزامنة: `fetch` → `checkout <commit>` → `build` → `test` → إيداع مؤشّر الـ submodule. أخضر تتقدّم، أحمر تعرف أي عقد تغيّر |
| 4 | فرع لكل موجة دمج (`feature/erp-accounting`)، و`main` يبقى قابلاً للنشر |
| 5 | لا شيء يُودَع بلا دفع إلى remote في نفس الجلسة |

---

## الترتيب المختصر

```
✅ 0. remote + push للمدرسة
✅ 1. إنهاء Sms.Web.exe (PID 4620)
✅ 2. حذف الخردة + إيداع 61+9 ملفاً
✅ 3. نقل الوثائق إلى sms/docs           (85 وثيقة)
✅ 4. إنقاذ POS + الرابط + الترجمة إلى ERP_2028
✅ 5. global.json في ERP_2028
✅ 6. حذف النسخة المكرّرة
✅ 7. تثبيت البنية
──────── ✅ التطوير المستقل آمن من هنا ────────
✅ 8. P1: submodule + الجسر               c20ca0d
✅ 9. P2: التوافق + قاعدة واحدة + الهجرات  127e6e3
✅ 10. P3: الشاشات + جسر الصلاحيات        4a6393a
──────── التالي: P4 (الترحيل حيّاً) ────────
```

**حالة الدمج بعد P3:** مخططا `acc` و`org` داخل قاعدة `Sms` (149 حساباً، سنة مالية + 12 فترة، شركة + فرعان) · `/Accounting/*` و`/Organization/*` يُخدَمان من `Sms.Web` · 28 صلاحية ERP مُفهرسة وممنوحة لـ SYSADMIN · البناء 0 تحذير · 1240 اختباراً ناجحاً.

**ما تبيّن أثناء التنفيذ ولم يكن في الخطة:** مستودع `E:\ERP_2028` كان يحمل **125,229 سطراً غير مُودَعة** فوق إيداعين اثنين فقط، ولم يُدفع منها شيء — كل ما بُني منذ الإيداع الأول. أُودعت في نقطة تفتيش واحدة (`2b17c87`) ودُفعت. كان هذا خطراً أكبر من كل ما رصدته الخطة أصلاً.
