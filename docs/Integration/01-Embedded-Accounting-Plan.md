# الخطة الكاملة: نظام محاسبة مستقل + مُضمَّن داخل نظام المدرسة

**التاريخ:** 2026-08-21 · **يكمّل:** [00-ERP-SMS-Integration-Analysis.md](00-ERP-SMS-Integration-Analysis.md) · **الحالة:** خطة للموافقة — لم يُكتب كود

---

## 1. المبدأ الحاكم

> **مصدر واحد للكود، مُضيفان.** (One codebase, two composition roots)

| | المنتج | المُضيف | ما يحويه |
|---|---|---|---|
| **أ** | **ERP 2028** — نظام محاسبة/ERP مستقل يُباع وحده | `ERP2028.Web` | كل الوحدات العشر — **لا يتغيّر شيء فيه** |
| **ب** | **SMS** — نظام إدارة مدارس بمحاسبة مدمجة | `Sms.Web` | نظام المدرسة + **نفس تجميعات** وحدات المحاسبة |

تجميعات المحاسبة (`ERP2028.Modules.Accounting.*.dll`) **متطابقة بايت ببايت** في المُضيفين. لا نسخ، لا fork، لا فرع موازٍ. تصحيح خطأ محاسبي واحد يصلح المنتجين معاً.

**هذا ليس تصميماً جديداً — هو التصميم القائم مستخدَماً كما قُصد له.**

---

## 2. لماذا هذا ممكن — الأدلة من الكود نفسه

تحقّقتُ من كل بند أدناه في المصدر:

| # | الدليل | المرجع |
|---|---|---|
| 1 | كل وحدة ERP = **مكتبات صنف**، والمُضيف رفيع. تسجيل الوحدة = **3 دوال امتداد + سطر ApplicationPart + سطر صلاحيات + كتلتان في DbInitializer** | `AppStartup.cs:185-305`, `DbInitializer.cs:52-192` |
| 2 | `AccountingWebRegistration` يقول حرفياً إن غرضه *"الحفاظ على اصطلاح التسجيلات الثلاثة وأن يكون علامة التجميعة لـ AddApplicationPart"* | `AccountingWebRegistration.cs` |
| 3 | `AccountingDbContext` يُبنى على **نسخة الاتصال** لا على نص اتصال — أي مُضيف يوفّر `ISharedDbConnection` يحصل على الوحدة | `AccountingInfrastructureRegistration.cs:26-29` |
| 4 | كل وحدة لها **مخطط خاص + جدول هجرات خاص** (`__EFMigrationsHistory` داخل `acc`) — لا تعارض مع هجرات المدرسة | نفس الملف |
| 5 | شجرة اعتماد المحاسبة **ضيّقة جداً**: 5 مشاريع + 5 لبنات + `Organization.Contracts` فقط | جرد `.csproj` كامل (§4.2) |
| 6 | `Accounting.Web` مشروع **Razor Class Library** (`Sdk.Razor` + `AddRazorSupportForMvc`) بـ `Areas/Accounting/**` — مصمَّم أصلاً للاستضافة من الخارج | `ERP2028.Modules.Accounting.Web.csproj` |
| 7 | `_ViewStart` في الوحدة يضع `Layout = "_Layout"` **بالاسم فقط** — أي أن قالب المُضيف هو ما يُستخدم. شاشات المحاسبة سترث قشرة المدرسة تلقائياً | `Areas/Accounting/Views/_ViewStart.cshtml` |
| 8 | **أسماء المخططات لا تتعارض** — المدرسة `aud core doc fin msg ops ppl rpt sec svc wf` / الـ ERP `acc cash comm fa identity inv org ptn pur sal` | فحص `HasDefaultSchema` + `ToTable` |
| 9 | نفس `net5.0` ونفس `EF Core 5.0.17` في الطرفين | `.csproj` الطرفين |
| 10 | نص اتصال المدرسة **يحمل `MultipleActiveResultSets=true` أصلاً** — وهو شرط لازم لمشاركة اتصال واحد بين سياقين | `sms/src/Sms.Web/appsettings.json` |
| 11 | **الحالة الصحية:** المدرسة تُترجم بصفر خطأ/صفر تحذير و **1236/1236 اختباراً ناجحاً**؛ الـ ERP يُترجم نظيفاً | فحص بناء فعلي |

---

## 3. المعمارية المستهدفة

```
┌─ مستودع ERP_2028 (المرجع الوحيد لكود المحاسبة) ──────────────────────┐
│                                                                       │
│  BuildingBlocks/            Modules/Accounting/      Modules/Organization/
│   SharedKernel               .Domain                  .Domain          │
│   Common                     .Contracts               .Contracts       │
│   Application.Abstractions   .Application             .Application     │
│   Infrastructure.Shared      .Infrastructure          .Infrastructure  │
│   Web.Shared                 .Web  (RCL)              .Web  (RCL)      │
│                                                                       │
│  Bootstrap/ERP2028.Web  ← المُضيف (أ): المنتج المحاسبي المستقل         │
└───────────────────────────────┬───────────────────────────────────────┘
                                │ نفس التجميعات (submodule ثم NuGet)
┌───────────────────────────────▼───────────────────────────────────────┐
│ مستودع sms                                                            │
│                                                                       │
│  Sms.Domain / Sms.Application / Sms.Infrastructure   (لا تتغيّر بنيتها)│
│                        │                                              │
│                        │ IGlPostingPort (منفذ تعرّفه المدرسة)          │
│                        ▼                                              │
│  ┌─────────────── Sms.Erp.Bridge (مشروع جديد) ──────────────────────┐ │
│  │  • مُهايئات: ICurrentUser · IDateTime · IPermissionCatalog …     │ │
│  │  • جسر المعاملة: AppDbContext على الاتصال المشترك                │ │
│  │  • ErpGlPostingAdapter : IGlPostingPort → IPostingService        │ │
│  │  • ErpAccountValidator → IChartOfAccountsDirectory               │ │
│  └─────────────────────────────────────────────────────────────────┘ │
│                                                                       │
│  Sms.Web  ← المُضيف (ب): مدرسة + محاسبة مدمجة                         │
│            /Students … /Fees … /Payments …  +  /Accounting/…          │
└───────────────────────────────────────────────────────────────────────┘
                          قاعدة بيانات SQL Server واحدة
```

**قاعدة الاتجاه الذهبية:** `Sms.Domain` و`Sms.Application` و`Sms.Infrastructure` **لا تعرف أن الـ ERP موجود**. الجسر وحده يعرف الطرفين، والـ ERP لا يعرف المدرسة إطلاقاً. أي أن حذف مشروع الجسر يُعيد المدرسة لحالتها المستقلة الحالية — **وهذا هو معنى "مرن"**.

---

## 4. الطبقة 1 — آلية استهلاك كود الـ ERP

### 4.1 الخيارات

| المستوى | الآلية | متى | العيب |
|---|---|---|---|
| **L0** | `git submodule` + `ProjectReference` بمسار نسبي | **الآن** — التطوير والتصحيح | يجب تثبيت commit صراحة |
| **L1** | حزم **NuGet** يُنتجها مستودع الـ ERP (`ERP2028.Modules.Accounting.*`) | عند استقرار الإصدارات | يحتاج مخزن حزم + انضباط إصدارات |
| **L2** | نسخ الملفات | ❌ **مرفوض** | هو بالضبط ما أوقعنا في المشكلة الحالية |

**التوصية: ابدأ بـ L0، وانتقل إلى L1 عند أول إصدار إنتاجي.**

### 4.2 التخطيط الفعلي لـ L0

```
E:\school2028\sms\
  external\erp\                  ← git submodule → ERP_2028 (مثبَّت على commit)
  src\Sms.Erp.Bridge\            ← مشروع جديد
  Sms.sln                        ← يضيف مجلد حلّ "ERP" يضمّ المشاريع المرجعية
```

**المشاريع المرجعية — الحد الأدنى المُتحقَّق منه (13 مشروعاً):**

| الفئة | المشاريع |
|---|---|
| اللبنات (5) | `ERP2028.Common` · `ERP2028.SharedKernel` · `ERP2028.Application.Abstractions` · `ERP2028.Infrastructure.Shared` · `ERP2028.Web.Shared` |
| المحاسبة (5) | `.Domain` · `.Contracts` · `.Application` · `.Infrastructure` · `.Web` |
| التنظيم (3) | `Organization.Contracts` · `.Domain` + `.Application` + `.Infrastructure` **(إن ضُمِّنت الوحدة)** |

> ⚠️ **لماذا Organization إلزامية.** `PostingService` يحقن `IBranchDirectory` من `Organization.Contracts` في مُنشئه (`PostingService.cs:44`) ويستخدمه للتحقق من `BranchCode`. أمامك خياران:
> - **(أ) ضمّ وحدة Organization كاملة** — تحصل على شركة/فروع/مدن/دول + إعدادات فرع. المدرسة تصبح فرعاً من نوع `BranchType.School`. **موصى به.**
> - **(ب) كتابة `IBranchDirectory` وهمي** في الجسر يرجّع فرعاً واحداً افتراضياً. أخفّ، لكن يقفل الباب أمام تعدّد المدارس على مستوى الأستاذ العام لاحقاً.

### 4.3 توحيد الـ SDK — نقطة اكتُشفت بالفحص الفعلي

- `sms/global.json` يثبّت SDK على **5.0.409**.
- `ERP_2028` **لا يحوي `global.json`** → يُبنى بـ SDK **10.0.303**، وهذا يولّد حالة بناء تزايدي بائتة (`StaticWebAssets.xml` بنمط 5 مقابل `.cache` بنمط 10) أدّت إلى فشل بناء وهمي في المحاولة الأولى.
- **إجراء إلزامي في المرحلة 1:** إضافة `global.json` يثبّت `5.0.409` في `E:\ERP_2028` — وإلا سيبني المُضيفان بنمطين مختلفين.
- الجهاز **لا يملك runtime لـ 5.0** (أحدث 10.0.11). `RollForward: LatestMajor` في `sms/Directory.Build.props` هو ما يجعل التشغيل ممكناً؛ يجب نقل نفس الخاصية إلى مشاريع الـ ERP المرجعية عند الاستضافة من `Sms.Web`.

---

## 5. الطبقة 2 — طبقة التوافق (`Sms.Erp.Bridge`)

الـ ERP يطلب من مُضيفه مجموعة صغيرة جداً من التجريدات. هذا الجدول هو **قلب الآلية المرنة**:

| تجريد الـ ERP | التوقيع | تنفيذه في مُضيف المدرسة | الحجم |
|---|---|---|---|
| `IDateTime` | `DateTime UtcNow` | غلاف حول `Sms.Application.Common.Interfaces.IClock` | 5 أسطر |
| `ICurrentUser` | `IsAuthenticated`, `int? UserId`, `UserName`, `IReadOnlyCollection<string> Permissions`, `HasPermission(string)` | يقرأ من `HttpContext.User` (نفس ملفات تعريف جلسة المدرسة) + جسر الصلاحيات أدناه | ~40 سطراً |
| `ISharedDbConnection` + `IAmbientTransaction` | — | **لا تُنفَّذ** — استخدم تنفيذ الـ ERP نفسه: `services.AddSharedRequestConnection(() => new SqlConnection(cs))` | سطر واحد |
| `IPermissionCatalog` | كتالوج الصلاحيات | يدمج `AccountingPermissions.All` (+ `OrganizationPermissions.All`) | ~10 أسطر |
| `IUserDirectory` | تسمية المستخدمين | يقرأ من جدول مستخدمي المدرسة (`sec`) | ~20 سطراً |
| `IFileStore` | مرفقات | إما تنفيذ الـ ERP كما هو، أو غلاف حول مرفقات المدرسة (`doc`) | ~30 سطراً |
| `IBranchDirectory` / `IBranchContext` | الفروع | من وحدة Organization المُضمَّنة (الخيار أ) أو وهمي (ب) | 0 أو ~40 سطراً |

**التقدير الإجمالي لطبقة التوافق: أقل من 200 سطر.** هذه هي كل "الضريبة المعمارية" لدمج نظام محاسبة كامل.

### 5.1 جسر الصلاحيات — النقطة الوحيدة التي تحتاج قراراً

نموذجان مختلفان:

| | المدرسة | الـ ERP |
|---|---|---|
| الشكل | `(moduleCode, screenCode, ActionVerb)` | نص مسطّح `"Accounting.Accounts.View"` |
| الحارس | `RequirePermissionAttribute : TypeFilterAttribute` → `IAsyncActionFilter` | `[HasPermission]` عبر `IAuthorizationPolicyProvider` مخصّص |
| الرفض | `NotFound` (السطح يختفي — BR-SEC-010) | `AccessDenied` |

**الحل الموصى به:** عامِل صلاحيات الـ ERP كـ **نصوص معتمة** تُمنح لأدوار المدرسة تحت رمز وحدة محجوز (`"ERP"`). عندها:
- `ICurrentUser.Permissions` يرجّع مجموعة النصوص الممنوحة للدور.
- شاشات المحاسبة تبقى محميّة بـ `[HasPermission]` الأصلي دون تعديل سطر واحد فيها.
- شاشة إدارة أدوار المدرسة تعرض قسماً جديداً "المحاسبة" مصدره `AccountingPermissions.All`.

> ⚠️ **نقطة تحقّق إلزامية:** `AddPermissionAuthorization()` يسجّل `IAuthorizationPolicyProvider` مخصّصاً، والمدرسة تستدعي `services.AddAuthorization(...)` بسياسات مسمّاة (`Startup.cs:159`). المزوّد المخصّص يجب أن يفوّض للمزوّد الافتراضي عند اسم سياسة غير معروف. يُختبَر في المرحلة 3 قبل أي شيء آخر.

---

## 6. الطبقة 3 — قاعدة بيانات واحدة ومعاملة واحدة

### 6.1 قاعدة البيانات

- **قاعدة واحدة** تحوي 21 مخططاً (11 للمدرسة + 10 للـ ERP، أو 3 فقط إن ضُمِّنت المحاسبة والتنظيم وحدهما).
- كل سياق يحتفظ بـ `__EFMigrationsHistory` **داخل مخططه** → لا تعارض بين هجرات الطرفين.
- نص الاتصال يجب أن يحمل `MultipleActiveResultSets=true` — **موجود أصلاً**.

### 6.2 ترتيب الهجرات في `Sms.Web/Program.cs`

```
1. OrganizationDbContext.MigrateAsync()   ← أولاً: كل BranchCode يُتحقَّق مقابل org.Branches
2. AccountingDbContext.MigrateAsync()
3. AppDbContext.MigrateAsync()            ← هجرات المدرسة كما هي اليوم
ثم: AccountingDataSeeder → SchoolGlMappingSeeder
```

### 6.3 جسر المعاملة الذرّية — النقطة التقنية الوحيدة الصعبة

`AppDbContext` في المدرسة **لا يرث** `ModuleDbContextBase`. للانضمام لمعاملة الـ ERP يلزم **خمسة بنود مُتحقَّق منها**:

| # | المطلوب | لماذا |
|---|---|---|
| 1 | يُبنى بـ `UseSqlServer(sp.GetRequiredService<ISharedDbConnection>().Connection)` — **النسخة**، لا نص الاتصال | اتصالان لا يتشاركان معاملة |
| 2 | مسجَّل **Scoped** في نفس نطاق `ISharedDbConnection` | — |
| 3 | ينضم يدوياً قبل أول أمر: `Database.UseTransactionAsync(ambient.Current)` مع حارس `ShouldEnlist` | استدعاء `UseTransaction` مرتين يرمي |
| 4 | **`DbCommandInterceptor` خاص به** — اعتراض الـ ERP `internal` ولا يمكن بناؤه من الخارج | على اتصال بمعاملة معلّقة، كل أمر بلا معاملة يُرفض |
| 5 | **يفصل نفسه يدوياً بعد الالتزام**: `UseTransactionAsync((DbTransaction?)null)` — `IAmbientTransactionEnlistment` أيضاً `internal` | وإلا حمل السياق معاملة مكتملة للطلب التالي |

**بديل أبسط (مرحلة أولى مقبولة):** لا تلمس `IAmbientTransaction` إطلاقاً. عندها يفتح `PostingEngine` معاملته الخاصة ويلتزمها. تخسر الذرّية بين كتابة المدرسة والقيد، وتحتاج تعويضاً لاحقاً. **مقبول للنمط "دفعة ملخّصة" (§8) لأن الدفعة تُولَّد بعد أن استقرّت المستندات، لا معها.**

> **توصية عملية:** ابدأ بالبديل الأبسط في المرحلة 4، ونفّذ البنود الخمسة فقط إن انتقلت لاحقاً لترحيل لحظي لكل مستند.

### 6.4 مصنع وقت التصميم

هجرات المدرسة (`dotnet ef`) يجب أن تستمر بالعمل. الحل الجاهز في الـ ERP: مصنع وقت تصميم يمرّر `NullAmbientTransaction.Instance` (`AccountingDbContextFactory.cs:28-29`) — يُنسخ نمطه لمصنع `AppDbContext`.

---

## 7. الطبقة 4 — شاشات المحاسبة داخل قشرة المدرسة

ما يلزم في `Sms.Web/Startup.cs` — **كل ما يلي إضافات، صفر تعديل على كود الـ ERP**:

| # | السطر | الغرض |
|---|---|---|
| 1 | `services.AddSharedRequestConnection(() => new SqlConnection(sharedCs))` | الاتصال والمعاملة المشتركان |
| 2 | `services.AddAccountingApplication()` · `AddAccountingInfrastructure(cs)` · `AddAccountingWeb()` | الوحدة |
| 3 | `services.AddOrganization*()` ×3 | الفروع (الخيار أ) |
| 4 | `services.AddLocalization()` | شاشات الـ ERP تحقن `IStringLocalizer<SharedResource>` — **المدرسة لا تستدعيه اليوم** |
| 5 | `services.AddPermissionAuthorization()` + `AddErpNavigation()` | حارس `[HasPermission]` + مزوّدو التنقّل |
| 6 | `.AddApplicationPart(typeof(AccountingWebRegistration).Assembly)` | اكتشاف المتحكّمات والمشاهد |
| 7 | `endpoints.MapAreaControllerRoute(...)` أو `MapControllerRoute("areas", "{area:exists}/{controller}/{action}/{id?}")` | توجيه المناطق — **المدرسة تسجّل مساراً واحداً فقط اليوم (`Startup.cs:646`)** |
| 8 | كتالوج الصلاحيات `.Concat(AccountingPermissions.All)` | ظهور الصلاحيات في شاشة الأدوار |

**القالب:** `_ViewStart` في الوحدة يطلب `Layout = "_Layout"` بالاسم، ومحرّك المشاهد يحلّه إلى `Sms.Web/Views/Shared/_Layout.cshtml`. النتيجة المرغوبة تلقائياً: **شاشات المحاسبة داخل شريط المدرسة الجانبي، بلغتها وباتجاهها (RTL)**.

**الأصول الثابتة:** مشاهد الـ ERP تستخدم وسوم `erp-*` (`ERP2028.Web.Shared/Components/*TagHelper.cs`) التي تولّد أصناف `erp-theme.css`، وأيقونات Bootstrap Icons. يجب نسخ `erp-theme.css` (أو استيراده) إلى `Sms.Web/wwwroot` وربطه في `_Layout`. **هذه هي مهمة التوفيق البصري الوحيدة الحقيقية.**

---

## 8. الطبقة 5 — الترحيل المالي الحقيقي

### 8.1 المنفذ والمُهايئ

```csharp
// Sms.Application/GlExport/IGlPostingPort.cs — تعرّفه المدرسة، لا تعرف من ينفّذه
public interface IGlPostingPort
{
    Task<GlPostResult> PostBatchAsync(GlExportBatch batch, CancellationToken ct = default);
    Task<GlPostResult> ReverseBatchAsync(GlExportBatch batch, string reason, CancellationToken ct = default);
}

// Sms.Application/GlExport/IGlAccountValidator.cs
public interface IGlAccountValidator
{
    Task<bool> IsPostableAsync(string accountCode, CancellationToken ct = default);
}
```

`GlExportService.GenerateAsync` يستدعي `IGlPostingPort` بعد بناء الدفعة؛ `DefineMappingAsync` يستدعي `IGlAccountValidator` قبل الحفظ. **إن لم يُسجَّل المنفذ (نشر مدرسة بلا محاسبة)، تعمل المدرسة كما اليوم وتُخرج CSV.** هذا هو الخيار المرن.

### 8.2 قواعد إلزامية للمُهايئ — مستخلَصة من قراءة المحرك

| # | القاعدة | السبب |
|---|---|---|
| 1 | `SourceModule = "SMS"` **بحالة أحرف موحّدة** | مطابقة الفهرس غير حسّاسة للحالة؛ `"Sms"` و`"SMS"` يتصادمان |
| 2 | `SourceDocumentType = "GlExportBatch"` · `SourceDocumentId = BatchNo` (≤ 200 حرفاً) | منع التكرار بفهرس فريد مُرشَّح `UX_JournalEntries_Source` |
| 3 | **العكس = طلب ترحيل ثانٍ** بـ `SourceDocumentType = "GlExportBatchReversal"` ونفس `SourceDocumentId` | لا توجد `ReverseAsync` على العقد؛ هذا نمط `VoucherReversalService` المعتمد |
| 4 | `GroupBy` على السداسية `(Code, Branch, CostCentre, Project, PartyType, PartyCode)` قبل الإرسال | حارس التكرار داخل الطلب يرفض حسابين متطابقي الأبعاد |
| 5 | `Description ≤ 500` · `Reference ≤ 50` · تقريب `decimal` لـ 4 خانات | تُرمى كاستثناءات مجال لا كـ `Result` |
| 6 | **`try/catch (AccountingDomainException)`** حول الاستدعاء | `PostingService` لا يلفّ المحرك؛ خطأ طول أو تاريخ يصبح 500 |
| 7 | معالجة `DbUpdateException` (SQL 2601) | قيد محذوف ناعماً غير مرئي للفحص المسبق لكنه ما زال في الفهرس |
| 8 | فحص مسبق للفترة عبر `IFiscalCalendarDirectory` + معالجة `Accounting.Period.Closed` | الرفض قبل الكتابة أرخص |
| 9 | `PartyType`/`PartyCode` **تُترك فارغة** في نمط الدفعة الملخّصة | القيد ملخّص لا يخصّ طرفاً؛ ونصف طرف مرفوض |
| 10 | `BranchCode` = رمز الفرع المقابل للمدرسة | يجب أن يوجد في `org.Branches` وإلا `BranchNotFound` |

### 8.3 الشروط المسبقة قبل أول ترحيل

1. **سنة مالية** تغطّي التاريخ — و`StartDate` **يجب أن يكون أول يوم في شهر** (I-13). الفترات الاثنتا عشرة تُولَّد تلقائياً وكلها `Open`.
2. **فرع** من نوع `BranchType.School` لكل `School` في المدرسة.
3. **جدول ربط الحسابات مكتمل** — كل مفتاح في `GlAccountKeys` + مفتاح إيراد لكل `FeeCategory`. النقص يرفع `GlMappingMissingException` مُعدِّداً كل مفتاح مفقود.
4. **الحسابات الناقصة مضافة** لدليل حسابات الـ ERP (§7.3 في وثيقة التحليل) — عبر `DefaultChartOfAccounts` في وحدة Accounting نفسها، لا من المدرسة.

---

## 9. الفجوات المحاسبية المكتشفة — يجب سدّها قبل اعتبار الأستاذ العام صحيحاً

تدقيق كامل لكل حدث مالي في المدرسة كشف **16 فجوة**. أخطرها ليست في الربط بل في `GlExport` نفسه:

### 9.1 حرجة — تُفسد الميزان

| الرمز | الفجوة | الأثر |
|---|---|---|
| **G-1** | **مبيعات المتجر (M28) بالمحفظة لا تُرحَّل إطلاقاً.** `GlExportService` لا يقرأ `_db.StoreSales` أبداً. البيع يخصم المحفظة ولا شيء يعترف بالإيراد | `WalletLiability` **مبالغ فيه دائماً**، وإيراد المتجر **لا يُعترف به أبداً**. (مبيعات المقصف مغطّاة — المسار مختلف) |
| **G-11** | **الخصومات لا تعكس ضريبة المخرجات.** المبلغ إجمالي شامل الضريبة ويُرحَّل كاملاً إلى `Discounts` مقابل `Receivables` | `VatOutput` يحتفظ بضريبة على إيراد لم يتحقق. إشعارات الدائن تفصل الضريبة صحيحاً — عدم تماثل غير موثّق |
| **G-12** | **ازدواج ضريبي في الاسترداد (claw-back).** يُمرَّر مبلغ إجمالي عبر مسار يعيد تطبيق ضريبة الفئة | ضريبة مضاعفة على كل استرداد خصم |
| **G-16** | **النظام كله خامل.** لا متحكّم يحقن `IGlExportService`، ولا بذرة تنشئ `GlAccountMapping` | في التطبيق العامل **لا توجد وسيلة** لتعريف ربط أو توليد دفعة |

### 9.2 مهمة — سلوك غير صحيح محاسبياً

| الرمز | الفجوة |
|---|---|
| **G-4** | **إلغاء عبر الفترات لا يُعكَس.** إلغاء رسم/بيع بعد تصدير فترته: يختفي من الاستعلامات المستقبلية والدفعة الصادرة تحتفظ به → مبالغة دائمة |
| **G-6** | **شطب الأقساط (ديون معدومة)** علَم + سبب فقط — لا إشعار دائن ولا حساب ديون معدومة. الذمة تبقى في الميزان للأبد |
| **G-10** | **الدفعات المقدمة لا تُصفّى أبداً.** لا واجهة "تطبيق مقدَّم على رسم لاحق". `AdvancesReceived` ينمو ولا ينفك، و`Receivables` ينتفخ |
| **G-5** | **فروق الصندوق لا تُرحَّل.** فرق الجرد (`CountedTotal − SystemTotal`) مطلوب في الواجهة ولا يصل الأستاذ. لا مفتاح `CashOverShort` |
| **G-3** | **تسويات المحفظة** تغيّر التزاماً بسبب إلزامي ولا تنتج قيداً |
| **G-14** | **فخّ الفئة المعطَّلة:** تعطيل فئة رسوم يجعل الفترة **غير قابلة للتصدير** (فشل صلب) لأن المفتاح الاحتياطي غير مبذور |
| **G-2** | **مبيعات المقصف بلا ضريبة** — `CafeteriaItem` لا يحمل نسبة ضريبة أصلاً (بخلاف `StoreItem`) |

### 9.3 مقبولة/مؤجَّلة

`G-7` رسم طلب القبول لا يُحوَّل لمستند مالي · `G-8` غرامات التأخير والارتداد غير مُنفَّذة (مؤجَّلة صراحةً) · `G-9` إلغاء سند القبض معلَن في `enum` وغير مُنفَّذ في أي مسار · `G-13` الإعفاءات تُخصم من الإيراد بدل حساب مصروف تقديري · `G-15` لا جانب مصروفات إطلاقاً (رواتب، مشتريات، هدر)

> **الخبر الجيد:** `G-15` (غياب جانب المصروفات) هو بالضبط ما يحلّه هذا الدمج — وحدات `Cash` و`Purchasing` و`FixedAssets` في الـ ERP تملأ الفراغ دون كتابة سطر جديد.

---

## 10. المراحل التنفيذية

| المرحلة | المخرج | حجم تقريبي | البوابة |
|---|---|---|---|
| **P0 — الإنقاذ والتنظيف** | نقل وثائق المدرسة إلى `sms/docs` وإيداعها · إنقاذ ميزة POS الثمانية + رابط SMS + 7 سطور ترجمة إلى `ERP_2028` وإيداعها · إيداع الـ 61 ملفاً المعدَّلة والـ 9 مصادر غير المتتبَّعة في `sms` · حذف ملفات الرموز/الجلسات من الجذر · **ثم** حذف نسخة الـ ERP المكرّرة | 0.5 يوم | لا شيء فريد ضاع؛ الحلّان يُبنيان |
| **P1 — الهيكل** | `global.json` في ERP_2028 · submodule + مجلد حلّ ERP في `Sms.sln` · مشروع `Sms.Erp.Bridge` فارغ يُترجم | 0.5 يوم | `dotnet build Sms.sln` أخضر مع 13 مشروع ERP مرجعياً |
| **P2 — التوافق والقاعدة** | مُهايئات §5 · `AddSharedRequestConnection` · قاعدة واحدة · ترتيب الهجرات · مصنع وقت التصميم | 1–2 يوم | `AccountingDbContext` يُهاجَر ويُبذَر داخل `Sms.Web`؛ **اختبارات المدرسة الـ1236 ما زالت خضراء** |
| **P3 — الشاشات** | 8 أسطر §7 · توجيه المناطق · `erp-theme.css` · جسر الصلاحيات · قسم "المحاسبة" في التنقّل | 1–2 يوم | `/Accounting/Accounts` يفتح داخل قشرة المدرسة، محمياً بأدوار المدرسة، بالعربية RTL |
| **P4 — الترحيل حيّاً** | `IGlPostingPort` + `ErpGlPostingAdapter` · شاشة ربط الحسابات (**تسدّ G-16**) · بذرة الربط الافتراضي · حسابات المدرسة في دليل الـ ERP · سنة مالية + فرع مدرسة | 2–3 أيام | دفعة `GLX` تُنتج قيد `SY-…`؛ **ميزان المراجعة متوازن**؛ إعادة الترحيل مرفوضة؛ إلغاء الدفعة يُنتج قيداً عكسياً |
| **P5 — سدّ الفجوات** | G-1 · G-11 · G-12 · G-14 أولاً؛ ثم G-4 · G-10 · G-5 · G-6 · G-3 · G-2 | 3–5 أيام | كل حدث مالي له قيد؛ اختبارات لكل فجوة |
| **P6 — التمنيج** | حزم NuGet (L1) · علَم تشغيل `Features:Accounting` · اختبار انحدار كامل للـ ERP المستقل | 1–2 يوم | المنتجان يُبنيان ويُنشران منفصلين من نفس المصدر |

**الإجمالي التقريبي: 9–15 يوم عمل** لمحاسبة كاملة مدمجة، مقابل أشهر لبناء محاسبة من الصفر داخل المدرسة.

---

## 11. نقاط الخطر التقنية المحدّدة

| # | الخطر | التخفيف |
|---|---|---|
| X1 | تعارض `IAuthorizationPolicyProvider` بين المدرسة والـ ERP | يُختبَر **أولاً** في P3 قبل أي شيء آخر |
| X2 | خلط SDK 5 و10 يولّد فشل بناء وهمي | `global.json` في المستودعين + `dotnet clean` عند التبديل |
| X3 | `AmbientTransactionCommandInterceptor` و`IAmbientTransactionEnlistment` **`internal`** — غير قابلة لإعادة الاستخدام من الخارج | كتابة مكافئ في الجسر (6 أسطر)، أو تجنّب المعاملة المشتركة كلياً (§6.3) |
| X4 | قيد محذوف ناعماً يكسر فحص التكرار المسبق ويرفع خطأ فهرس | معالجة `DbUpdateException` 2601 كـ "مُرحَّل مسبقاً" |
| X5 | تعارض أنماط `_Layout` وأصناف CSS بين النظامين | P3 مكرّس لهذا؛ الحل الأدنى: تحميل `erp-theme.css` داخل منطقة المحاسبة فقط |
| X6 | ترقيم المستندات يستخدم **رمز السنة المالية** لا السنة الميلادية | توحيد رموز السنوات المالية على `"2026"` لا `"FY26"` |
| X7 | `BranchPeriodLocked` **مُعلَن وغير مُستدعى** في الـ ERP | لا تعتمد عليه؛ الاعتماد على حالة الفترة فقط |
| X8 | 61 ملفاً معدَّلاً + 9 مصادر غير متتبَّعة في `sms` | تُودَع في P0 قبل أي تفرّع |

---

## 12. القرارات المطلوبة

| # | القرار | الخيارات | التوصية |
|---|---|---|---|
| **1** | **آلية الاستهلاك** | submodule · NuGet · نسخ | **submodule الآن → NuGet في P6** |
| **2** | **وحدة Organization** | تُضمَّن كاملة · `IBranchDirectory` وهمي | **تُضمَّن كاملة** — مدرسة = فرع |
| **3** | **الوحدات المُضمَّنة (Tier 1)** | المحاسبة فقط · + Cash · + Purchasing/Inventory/FixedAssets | **المحاسبة + Organization أولاً**؛ Cash في موجة ثانية (يسدّ جزءاً من G-15) |
| **4** | **الهوية** | هوية المدرسة + جسر صلاحيات · ضمّ وحدة Identity الـ ERP | **هوية المدرسة + جسر** — دخول واحد، بلا تكرار مستخدمين |
| **5** | **الذرّية** | البنود الخمسة الآن · معاملتان منفصلتان | **معاملتان الآن** (كافٍ للدفعة الملخّصة)؛ البنود الخمسة عند الانتقال للترحيل اللحظي |
| **6** | **مصير الـ CSV** | يُحذف · يبقى كمخرج ثانوي | **يبقى** — هو خطة الطوارئ ودليل التدقيق |
| **7** | **أولوية سدّ الفجوات** | قبل P4 · بعده | **G-14 و G-16 قبل P4** (بلا شاشة ربط وبلا معالجة الفئة المعطَّلة لا يعمل الترحيل أصلاً)؛ الباقي في P5 |
| **8** | **علَم التشغيل** | المحاسبة دائماً مدمجة · قابلة للإطفاء | **قابلة للإطفاء** — يبقى بيع المدرسة وحدها ممكناً |

---

## 13. ملحق — الوضع الصحي المُتحقَّق منه (2026-08-21)

| البند | النتيجة |
|---|---|
| `Sms.sln` | **0 خطأ / 0 تحذير** (رغم `TreatWarningsAsErrors=true`) |
| اختبارات المدرسة | **1236 / 1236 ناجحة** — 0 فاشلة، 0 متخطّاة |
| `ERP_2028\ERP2028.sln` | يُبنى نظيفاً (بعد `clean`)؛ 7 تحذيرات كود لا تفشل البناء |
| شجرة عمل `sms` | 61 معدَّلاً + 19 غير متتبَّع (منها **9 ملفات مصدر حقيقية** و10 ملفات خردة تشمل رموز جلسات) |
| SDK | لا runtime لـ 5.0 على الجهاز؛ `RollForward: LatestMajor` هو ما يُشغّل النظام |
