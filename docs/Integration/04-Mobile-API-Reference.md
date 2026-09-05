# مرجع واجهات API — كل دالة وبارامتراتها

**الحالة:** بُني في 2026‑08‑31. **ليس جزءًا من التحليل المعتمد Analysis v1.0.**

هذا الملف هو **المرجع الحرفي** لكل نقطة نهاية (endpoint) في المنتج: المسار، طريقة الطلب،
الصلاحية المطلوبة، البارامترات بأنواعها، وشكل الرد. أما *لماذا* بُنيت الواجهة بهذا الشكل —
قرار الجلسة بدل refresh token، ولماذا 404 بدل 403، وما الذي لم يُبنَ عمدًا — ففي
[`03-Mobile-API.md`](03-Mobile-API.md). لا تكرار بين الملفين: هذا **ماذا**، وذاك **لماذا**.

**75 دالة موزّعة على ثماني مجموعات** — أساسها اللقطة `8780f0c`، ثم فحص إصدار التطبيق
(2026‑09‑05). كل الصلاحيات موجودة أصلًا في `ScreenCatalog`، فلا حاجة لإعادة تشغيل
`tools/Sms.Seeder` قبل استخدام الواجهة.

| المجموعة | المسار | العدد | لمن |
|---|---|---|---|
| [١. تسجيل الدخول](#١-تسجيل-الدخول--apiv1auth) | `/api/v1/auth` | 5 | الجميع (موظف، ولي أمر، طالب) |
| [٢. ولي الأمر والطالب](#٢-ولي-الأمر-والطالب--apiv1portal) | `/api/v1/portal` | 10 | حسابات البوابة فقط |
| [٣. المدرّس](#٣-المدرس--apiv1learning) | `/api/v1/learning` | 16 | الموظفون |
| [٤. شؤون الطلاب](#٤-شؤون-الطلاب--apiv1students) | `/api/v1/students` | 9 | الموظفون |
| [٥. شؤون الموظفين](#٥-شؤون-الموظفين--apiv1employees--contracts--payroll) | `/api/v1/employees` · `/contracts` · `/payroll` | 14 | الموظفون |
| [٦. المالية](#٦-المالية--apiv1finance) | `/api/v1/finance` | 13 | الموظفون |
| [٧. الحسابات](#٧-الحسابات--apiv1accounting) | `/api/v1/accounting` | 7 | الموظفون |
| [٨. التقارير](#٨-التقارير) | — | 0 | لا توجد مجموعة تقارير — اقرأ القسم |
| [٩. إصدار التطبيق](#٩-إصدار-التطبيق--apiv1app) | `/api/v1/app` | 1 | **مجهول** — بلا تسجيل دخول |

---

## ٠. قواعد تسري على كل دالة

**العنوان الأساسي:** `/api/v1` — مثال كامل: `https://school.example.com/api/v1/students/12`.

**الترويسات (headers):**

| الترويسة | القيمة | متى |
|---|---|---|
| `Authorization` | `Bearer {sessionToken}` | كل الدوال عدا `login` و `two-factor` |
| `Accept-Language` | `ar-SA` أو `en-US` | اختيارية — تحدد لغة كل نص مقروء، **بما فيه نص الرفض** |
| `Content-Type` | `application/json` | مع كل `POST` / `PUT` يحمل جسمًا |

**تسمية الحقول في JSON:** camelCase — الخاصية `FirstNameAr` تصل كـ `firstNameAr`. جداول الحقول
أدناه مكتوبة بالشكل الذي يصل به فعلًا.

**البارامترات — ثلاثة أنواع فقط في هذه الواجهة:**

| النوع | كيف يُرسل | مثال |
|---|---|---|
| `path` | داخل المسار | `/api/v1/students/12` |
| `query` | بعد `?` | `/api/v1/students?q=أحمد&page=2` |
| `body` | جسم JSON | `{ "userName": "admin", "password": "…" }` |

**الترقيم (paging)** — كل دالة ترجع قائمة قابلة للنمو تقبل:

| البارامتر | النوع | الافتراضي | الحد |
|---|---|---|---|
| `page` | `int?` (query) | `1` | أقل من 1 يُرفع إلى 1 |
| `pageSize` | `int?` (query) | `25` | **200** — أي قيمة أكبر تُقصّ إلى 200 |

والرد يأتي دائمًا بهذا الغلاف (`ApiPage<T>`):

```json
{ "items": [ … ], "page": 1, "pageSize": 25, "total": 412, "totalPages": 17, "hasMore": true }
```

**المبالغ (`ApiMoney`)** — تصل رقمًا خامًا بصيغة invariant، غير منسّقة، والعملة معها دائمًا:

```json
{ "amount": 12000.50, "currency": "SAR", "text": "12000.5" }
```

**التواريخ** ميلادية بصيغة ISO‑8601 دائمًا. عرض الهجري قرار التطبيق نفسه (ADR‑4).

**الرفض** — غلاف واحد لكل رد غير ناجح:

```json
{ "error": { "code": "installment_not_open", "message": "القسط غير مفتوح.", "fields": null } }
```

`code` ثابت ولا يتغير باللغة — اربط منطق التطبيق به. `message` يصل بلغة المتصل — اعرضه كما هو.
`fields` يظهر فقط مع `validation_failed` وهو `{ "اسم الحقل": ["السبب"] }`.

**رمز 404 يعني «غير موجود» أو «لا صلاحية» — وبنفس الجسم تمامًا** (BR-SEC-010). ولي أمر يخمّن
رقم طالب ليس ابنه يحصل على نفس الرد الذي يحصل عليه لو كان الطالب غير موجود أصلًا.

قائمة رموز الرفض كاملة في [القسم ١٠](#١٠-رموز-الرفض-error-codes)، وقيم النصوص المقبولة
(الحالات، الجنس، طرق الدفع…) في [القسم ١١](#١١-القيم-النصية-المقبولة-enums).

---

## ١. تسجيل الدخول — `/api/v1/auth`

المجموعة الوحيدة التي تعمل **قبل** وجود جلسة. مؤشّرة `[PortalReachable]` كاملة، أي أن حسابات
ولي الأمر والطالب تصل إليها كما يصل الموظف.

### `POST /api/v1/auth/login`

| | |
|---|---|
| الصلاحية | **بدون** — التوقيع يسبق وجود من تُفحص صلاحيته |
| الجسم | `ApiLoginRequest` |
| الرد | `200 ApiLoginResponse` |

**بارامترات الجسم:**

| الحقل | النوع | إلزامي | ملاحظة |
|---|---|---|---|
| `userName` | `string` | ✔ | |
| `password` | `string` | ✔ | |
| `deviceName` | `string?` | — | يُفضَّل على `User-Agent` في شاشة جلسات المدرسة، لأن اسم مكتبة HTTP لا يقول للإداري شيئًا |

**الرد `ApiLoginResponse`:**

| الحقل | النوع | متى يمتلئ |
|---|---|---|
| `token` | `string?` | عند نجاح الدخول الكامل — هذا هو `Bearer` |
| `expiresAtUtc` | `DateTime?` | سقف الجلسة المطلق (12 ساعة افتراضًا، لا يمدّده النشاط) |
| `requiresTwoFactor` | `bool` | `true` ⇐ لم يكتمل الدخول، انتقل للخطوة الثانية |
| `twoFactorToken` | `string?` | رمز data-protection عمره خمس دقائق، يحمل رقم الحساب فقط ولا يمنح شيئًا |
| `mustChangePassword` | `bool` | `true` ⇐ كل الدوال ستُرفض بـ 403 حتى تغيير كلمة المرور |

**الرفض:** `401 invalid_credentials` · `423 account_locked` (BR-SEC-002، والدقائق في نص الرسالة).

### `POST /api/v1/auth/two-factor`

| | |
|---|---|
| الصلاحية | **بدون** — العامل الثاني من التوقيع نفسه |
| الجسم | `ApiTwoFactorRequest` |
| الرد | `200 ApiLoginResponse` (نفس الجدول أعلاه) |

| الحقل | النوع | إلزامي |
|---|---|---|
| `twoFactorToken` | `string` | ✔ — القادم من رد `login` |
| `code` | `string` | ✔ — رمز TOTP |

**الرفض:** `401 two_factor_token_expired` (مضى أكثر من خمس دقائق) · `401 invalid_two_factor_code`.

### `POST /api/v1/auth/change-password`

| | |
|---|---|
| الصلاحية | **بدون** — خدمة ذاتية على بيانات المتصل نفسه |
| الجسم | `ApiChangePasswordRequest` |
| الرد | **`204`** بلا جسم |

| الحقل | النوع | إلزامي |
|---|---|---|
| `currentPassword` | `string` | ✔ |
| `newPassword` | `string` | ✔ |

**الرفض:** `422 password_policy` — و `fields` يحمل سببًا مترجمًا لكل شرط لم يتحقق.

### `POST /api/v1/auth/logout`

| | |
|---|---|
| الصلاحية | **بدون** |
| البارامترات | **لا شيء** |
| الرد | **`204`** — تُنهى الجلسة على الخادم، والرمز يصبح ميتًا فورًا |

### `GET /api/v1/auth/me`

**أول نداء يفعله التطبيق.** يصف المتصل لنفسه ولا يكشف شيئًا لا يملكه أصلًا.

| | |
|---|---|
| الصلاحية | **بدون** |
| البارامترات | **لا شيء** |
| الرد | `200 ApiMeResponse` |

| الحقل | النوع | ملاحظة |
|---|---|---|
| `userAccountId` | `int` | |
| `userName` | `string` | |
| `accountType` | `string` | نوع الحساب: موظف / ولي أمر / طالب |
| `schoolId` | `int` | |
| `schoolNameAr` / `schoolNameEn` | `string` | الاسمان معًا — بيانات مخزّنة لا تسمية اختارها الخادم |
| `workingAcademicYearId` | `int` | |
| `workingAcademicYearName` | `string?` | |
| `mustChangePassword` | `bool` | |
| `twoFactorEnabled` | `bool` | |
| `sessionExpiresAtUtc` | `DateTime` | |
| `subject` | `ApiMeSubject?` | الشخص خلف الحساب |
| `children` | `ApiMeChild[]` | الطلاب الذين يحق لهذا المتصل قراءتهم (BR-SEC-011) |
| `permissions` | `string[]` | **كل** صلاحية مفهرسة يملكها، بصيغة `MODULE/Screen/Verb` |

`ApiMeSubject`: `kind` · `id` · `nameAr` · `nameEn` · `reference` (رقم الطالب أو الموظف).
`ApiMeChild`: `studentId` · `studentNo` · `nameAr` · `nameEn`.

> يبني التطبيق قائمته من `permissions` بدل أن يجرّب الدوال ليرى أيّها يرد 404. القائمة يقيّمها
> نفس `IPermissionService` الذي تستعمله الحرّاس، فلا يمكن أن يفترقا.

---

## ٢. ولي الأمر والطالب — `/api/v1/portal`

المجموعة الوحيدة — بعد `auth` — المؤشّرة `[PortalReachable]`. حساب ولي الأمر أو الطالب يصل
إليها وحدها؛ كل ما عداها يرد **404** عليه.

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `GET children` | — | `POR/Home/View` | `ApiPortalChild[]` |
| `GET students/{id}/attendance` | `id` (path, int) | `POR/Child/View` | `ApiPortalAttendance` |
| `GET students/{id}/results` | `id` (path, int) | `POR/Child/View` | `ApiPortalResult[]` |
| `GET students/{id}/timetable` | `id` (path, int) | `POR/Child/View` | `ApiPortalTimetable` |
| `GET students/{id}/fees` | `id` (path, int) | `POR/Statement/View` | `ApiPortalFees` |
| `GET statement` | — | `POR/Statement/View` | `ApiPortalStatement` |
| `GET students/{id}/homework` | `id` (path, int) | `POR/Work/View` | `ApiPortalHomework[]` |
| `GET students/{id}/lessons` | `id` (path, int) | `POR/Lessons/View` | `ApiPortalLesson[]` |
| `GET resources/{resourceId}/file` | `resourceId` (path, int) | `POR/Lessons/View` | **ملف ثنائي** — لا JSON |
| `GET announcements` | `page` · `pageSize` (query, int?) | `POR/Announcements/View` | `ApiPage<ApiPortalAnnouncement>` |

**`ApiPortalChild`** — صف الأبناء في الشاشة الرئيسية:

| الحقل | النوع | ملاحظة |
|---|---|---|
| `studentId` · `studentNo` | `int` · `string` | |
| `nameAr` · `nameEn` | `string` | |
| `isSelf` | `bool` | `true` حين يكون المتصل هو الطالب نفسه لا ولي أمره |
| `gradeCode` · `gradeName` · `sectionName` | `string?` | `gradeName` بلغة المتصل — تسمية اختارها الخادم |
| `attendancePercent` | `decimal?` | نسبة هذا العام |
| `feeBalance` | `decimal?` | الرصيد المستحق |

> `attendancePercent` و `feeBalance` يُطلب كلٌّ منهما على حدة وقد يُرفض وحده: ولي أمر يرى الابن
> ولا يرى المال حالة إعداد حقيقية، والصف يظهر رغم ذلك بالحقل فارغًا. **نداء واحد يكفي للشاشة الرئيسية.**

**`ApiPortalAttendance`:** `studentId` · `scheduledDays` · `exemptedDays` · `absentDays` · `attendancePercent`.

**`ApiPortalResult`:** `curriculumOfferingId` · `subjectNameAr` · `subjectNameEn` · `termId` ·
`termName` · `scorePercent` · `bandCode` · `publishedAtUtc`.

**`ApiPortalFees`:** `studentId` · `position` · `grossCharges` · `discounts` · `currency` ·
`charges[]` — وكل سطر `chargeNo` · `grossAmount` · `postedAtUtc`.

**`ApiPortalStatement`:** `total` · `currency` · `students[]` (كل عنصر `ApiPortalFees`) — كشف
العائلة كاملة في نداء واحد.

**`ApiPortalHomework`:** `homeworkId` · `titleAr` · `titleEn` · `instructionsAr` · `instructionsEn` ·
`subjectNameAr` · `subjectNameEn` · `dueDate` · `maxMarks` · `latePenaltyApplies` · `latePenaltyPercent`.

**`ApiPortalLesson`:** `lessonId` · `weekNumber` · `titleAr` · `titleEn` · `objectivesAr` ·
`objectivesEn` · `subjectNameAr` · `subjectNameEn` · `publishedAtUtc` · `resources[]`
(`resourceId` · `titleAr` · `titleEn` · `displayOrder` · `downloadUrl`).

> نوع المرفق (`typeAr` / `typeEn`) **ليس في الرد**: بُني وأُخرج مع دالة السكن في `8780f0c` لنفس
> السبب — يتّكئ على عمل في فرع آخر. يعرض التطبيق أيقونة من امتداد الملف حتى يعود.

**`ApiPortalTimetable`:** `studentId` · `sectionName` · `gradeCode` · `weekStart` · `entries[]` —
وكل حصة `dayOfWeek` · `periodSequence` · `startTime` · `endTime` · `subjectNameAr` · `subjectNameEn` ·
`teacherNameAr` · `teacherNameEn` · `roomName` · `sectionName` · `changeKind`.

**`ApiPortalAnnouncement`:** `id` · `titleAr` · `titleEn` · `bodyAr` · `bodyEn` · `sentAtUtc`.

**`GET resources/{resourceId}/file`** يرد الملف نفسه (`Content-Type` و `Content-Disposition` من
المرفق)، ويرفض بـ `409 resource_not_available` إن كان المرفق مسحوبًا أو لم يجتز فحص الفيروسات.

---

## ٣. المدرّس — `/api/v1/learning`

جانب المعلّم من التعلّم الإلكتروني: الدروس، مرفقاتها، والواجبات. **مدى التدريس (BR-LRN-002)
تحلّه المنافذ لا هذه الطبقة**، تمامًا كما في `LearningController`؛ ما خرج عن مدى المعلّم يُرفض
بـ `403 outside_teaching_reach`.

### مدى التدريس

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `GET reach/offerings` | — | `LRN/Planner/View` | `ApiTeachingReach[]` |
| `GET reach/sections` | — | `LRN/Homework/View` | `ApiTeachingReach[]` |

`ApiTeachingReach`: `curriculumOfferingId` · `sectionId` · `subjectNameAr` · `subjectNameEn` ·
`sectionName` · `gradeCode`. **ابدأ منها**: هي التي تملأ قوائم الاختيار في شاشة إنشاء درس أو واجب.

### الدروس

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `GET lessons` | `offeringId` · `week` (int?) · `status` (string?) · `page` · `pageSize` — كلها query | `LRN/Planner/View` | `ApiPage<ApiLesson>` |
| `GET lessons/{id}` | `id` (path, int) | `LRN/Planner/View` | `ApiLesson` |
| `POST lessons` | `ApiCreateLessonRequest` | `LRN/Planner/Create` | `200 ApiLesson` |
| `PUT lessons/{id}` | `id` (path) + `ApiUpdateLessonRequest` | `LRN/Planner/Edit` | `200 ApiLesson` |
| `POST lessons/{id}/publish` | `id` (path) | `LRN/Planner/Approve` | **`204`** |
| `POST lessons/{id}/retire` | `id` (path) + `ApiReasonRequest` | `LRN/Planner/Deactivate` | **`204`** |

`status` في `GET lessons` قيمة من `LessonStatus`: `Draft` · `Published` · `Retired` (غير حسّاسة
لحالة الأحرف). القيمة غير المعروفة **تُتجاهل** ولا ترفض الطلب.

**`ApiCreateLessonRequest`:**

| الحقل | النوع | إلزامي |
|---|---|---|
| `curriculumOfferingId` | `int` | ✔ |
| `weekNumber` | `int` | ✔ |
| `titleAr` | `string` | ✔ |
| `titleEn` | `string` | ✔ |
| `objectivesAr` · `objectivesEn` | `string?` | — |
| `sessionId` | `int?` | — ربط الدرس بحصة جدول؛ حصة من مادة أخرى تُرفض بـ `lesson_session_mismatch` |

**`ApiUpdateLessonRequest`** نفس الحقول **بلا** `curriculumOfferingId` — العرض المنهجي لا يتغيّر
بعد الإنشاء.

**`ApiReasonRequest`:** `reason` (`string`, إلزامي) — نص السبب المخزَّن مع سحب الدرس.

**`ApiLesson`:** `lessonId` · `curriculumOfferingId` · `sessionId` · `weekNumber` · `titleAr` ·
`titleEn` · `objectivesAr` · `objectivesEn` · `subjectNameAr` · `subjectNameEn` · `status` ·
`publishedAtUtc` · `retiredReason` · `resources[]`.

### مرفقات الدرس

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `POST lessons/{id}/resources` | `id` (path) + `ApiAttachResourceRequest` | `LRN/Resources/Create` | `200 ApiLessonResource` |
| `POST resources/{resourceId}/withdraw` | `resourceId` (path) | `LRN/Resources/Deactivate` | **`204`** |
| `GET resources/{resourceId}/file` | `resourceId` (path) | `LRN/Resources/View` | **ملف ثنائي** |

**`ApiAttachResourceRequest`:** `attachmentId` (`int`, ✔) · `titleAr` (✔) · `titleEn` (✔) ·
`displayOrder` (`int`).

> **الواجهة لا تستقبل بايتات ملف.** `attachmentId` يشير إلى `doc.Attachment` **مرفوع مسبقًا**؛
> رفع الملفات نفسه غير مبنيّ عمدًا (انظر `03-Mobile-API.md §6`). المرفق الذي لم يجتز الفحص
> يُرفض بـ `409 resource_not_scan_clean`.

**`ApiLessonResource`:** `resourceId` · `attachmentId` · `titleAr` · `titleEn` · `displayOrder` ·
`isScanClean` · `downloadUrl`.

### الواجبات

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `GET homework` | `sectionId` · `offeringId` (int?) · `status` (string?) · `page` · `pageSize` — query | `LRN/Homework/View` | `ApiPage<ApiHomework>` |
| `POST homework` | `ApiCreateHomeworkRequest` | `LRN/Homework/Create` | `200 ApiHomework` |
| `PUT homework/{id}` | `id` (path) + `ApiUpdateHomeworkRequest` | `LRN/Homework/Edit` | `200 ApiHomework` |
| `POST homework/{id}/issue` | `id` (path) | `LRN/Homework/Approve` | **`204`** |
| `POST homework/{id}/withdraw` | `id` (path) + `ApiReasonRequest` | `LRN/Homework/Deactivate` | **`204`** |

`status` من `HomeworkStatus`: `Draft` · `Issued` · `Collecting` · `Marking` · `Released` · `Withdrawn`.

**`ApiCreateHomeworkRequest`:**

| الحقل | النوع | إلزامي | ملاحظة |
|---|---|---|---|
| `curriculumOfferingId` | `int` | ✔ | |
| `sectionId` | `int` | ✔ | |
| `titleAr` · `titleEn` | `string` | ✔ | |
| `dueDate` | `DateTime` | ✔ | |
| `instructionsAr` · `instructionsEn` | `string?` | — | |
| `maxMarks` | `decimal?` | — | |
| `blueprintComponentId` | `int?` | — | ربط الواجب بمكوّن من مخطط الدرجات |
| `latenessPolicy` | `string?` | — | `AcceptWithoutPenalty` أو `AcceptWithPenalty`؛ الفارغ يأخذ الافتراضي |
| `latePenaltyPercent` | `decimal?` | — | مع `AcceptWithPenalty` |

**`ApiUpdateHomeworkRequest`** نفس الحقول **بلا** `curriculumOfferingId` و `sectionId`.

**`ApiHomework`:** `homeworkId` · `curriculumOfferingId` · `sectionId` · `titleAr` · `titleEn` ·
`instructionsAr` · `instructionsEn` · `subjectNameAr` · `subjectNameEn` · `sectionName` ·
`dueDate` · `maxMarks` · `blueprintComponentId` · `latenessPolicy` · `latePenaltyPercent` ·
`status` · `issuedAtUtc` · `withdrawnReason`.

> **تسليم الطالب للواجب وتصحيحه غير مبنيّين** — لا يوجد كيان تسليم في النطاق أصلًا. هذه فجوة في
> الوحدة 37 لا في الواجهة، ولا يمكن تلفيقها في طبقة النقل.

---

## ٤. شؤون الطلاب — `/api/v1/students`

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `GET ""` | `q` · `status` (string?) · `gradeLevelId` · `sectionId` · `page` · `pageSize` (int?) — query | `STU/Directory/View` | `ApiPage<ApiStudentRow>` |
| `GET {id}` | `id` (path, int) | `STU/File/View` | `ApiStudentFile` |
| `POST ""` | `ApiRegisterStudentRequest` | `STU/Directory/Create` | `200 ApiStudentFile` |
| `PUT {id}` | `id` (path) + `ApiUpdateStudentRequest` | `STU/File/Edit` | `200 ApiStudentFile` |
| `POST {id}/status` | `id` (path) + `ApiChangeStudentStatusRequest` | `STU/File/Approve` | **`204`** |
| `POST {id}/guardians` | `id` (path) + `ApiLinkGuardianRequest` | `STU/Guardians/Edit` | `200 { linkId }` |
| `POST guardians/{linkId}/unlink` | `linkId` (path) + `ApiUnlinkGuardianRequest?` | `STU/Guardians/Deactivate` | **`204`** |
| `POST {id}/emergency-contacts` | `id` (path) + `ApiEmergencyContactRequest` | `STU/File/Edit` | `200 { emergencyContactId }` |
| `POST {id}/enrollments` | `id` (path) + `ApiEnrollRequest` | `STU/Enrollment/Create` | `200 { enrollmentId }` |

`q` يبحث في **أجزاء الاسم الأربعة باللغتين ورقم الطالب**، و**كل كلمة تضيّق ما قبلها** لا تبدأ
بحثًا جديدًا: «محمد أحمد» تعني محمدًا أبوه أحمد، لا كل محمد وكل ابن أحمد. المطابقة غير حسّاسة
لحالة الأحرف على المزوّدين معًا. `status` من `StudentStatus`: `Enrolled` · `Suspended` ·
`Withdrawn` · `Graduated` · `Transferred` · `Alumni` — والقيمة غير المعروفة تُتجاهل بلا رفض.

**`ApiRegisterStudentRequest`:**

| الحقل | النوع | إلزامي |
|---|---|---|
| `firstNameAr` · `fatherNameAr` · `grandfatherNameAr` · `familyNameAr` | `string` | ✔ (الأربعة) |
| `firstNameEn` · `fatherNameEn` · `grandfatherNameEn` · `familyNameEn` | `string` | ✔ (الأربعة) |
| `gender` | `string` | ✔ — `Male` أو `Female`، وإلا `422 invalid_gender` |
| `dateOfBirth` | `DateTime` | ✔ |
| `nationalityLookupId` | `int` | ✔ |
| `primaryIdTypeLookupId` | `int?` | — |
| `primaryIdNo` | `string?` | — |
| `primaryIdExpiry` | `DateTime?` | — |

**`ApiUpdateStudentRequest`** = كل حقول التسجيل أعلاه **زائد** `reason` (`string`, ✔) — ملف
الطالب من الفئة T1، فالتعديل بلا سبب يُرفض بـ `422 audit_reason_required`.

**`ApiChangeStudentStatusRequest`:** `status` (`string`, ✔ — من `StudentStatus`) · `reason` (`string?`).
الانتقال غير المسموح يُرفض بـ `409 invalid_student_status_transition`.

> **`PUT students/{id}/residence` غير موجودة.** بُنيت ثم أُخرجت قبل الدمج في `8780f0c`: كانت
> تتّكئ على عمل ما زال في شجرة فرع آخر، ولقطة تُصرّف فقط بعد أن يدمج شخص آخر عمله لقطة مكسورة.
> تعود متابعةً من سطرين يوم تهبط تلك الشريحة. عنوان سكن الطالب يُحرَّر اليوم من المتصفح.

**`ApiLinkGuardianRequest`:**

| الحقل | النوع | ملاحظة |
|---|---|---|
| `parentId` | `int` | ولي الأمر يجب أن يكون مسجّلًا مسبقًا |
| `relationshipLookupId` | `int` | صلة القرابة |
| `isPrimaryContact` | `bool` | |
| `isFinanciallyResponsible` | `bool` | |
| `isPickupAuthorized` | `bool` | |
| `isPortalVisible` | `bool` | يقرر ظهور الطالب لهذا الحساب في البوابة |
| `effectiveFromUtc` | `DateTime?` | الفارغ = الآن |
| `guardianshipDocAttachmentId` | `int?` | مرفق إثبات الولاية |

**`ApiUnlinkGuardianRequest`** (اختياري كليًّا — يجوز إرسال الطلب بلا جسم): `effectiveToUtc` (`DateTime?`).
فكّ آخر ولي أمر مسؤول ماليًّا يُرفض بـ `409 last_financially_responsible_guardian`.

**`ApiEmergencyContactRequest`:** `nameAr` (✔) · `nameEn` (✔) · `phone` (✔) ·
`isPickupAuthorized` (`bool`) · `relationshipLookupId` (`int?`).

**`ApiEnrollRequest`:** `gradeYearProfileId` (`int`, ✔) · `enrollmentDate` (`DateTime?`) ·
`sourceType` (`string?` — `Admission` · `Rollover` · `Reinstatement`؛ الفارغ يأخذ الافتراضي).
تسجيل الطالب مرتين في نفس العام يُرفض بـ `409 duplicate_enrollment`.

**`ApiStudentRow`** (صف الدليل): `studentId` · `studentNo` · `nameAr` · `nameEn` · `status` ·
`gradeCode` · `gradeName` · `sectionName` · `mobile`.

**`ApiStudentFile`** (الملف الكامل): كل حقول التسجيل، زائد `studentId` · `studentNo` ·
`nationalityName` · `status` · `mobile` · `hasPhoto` · `placement` · `guardians[]` · `emergencyContacts[]`.

- `placement` (`ApiStudentPlacement`): `enrollmentId` · `academicYearId` · `gradeLevelId` ·
  `gradeCode` · `gradeName` · `sectionId` · `sectionName` · `enrollmentDate`.
- `guardians[]` (`ApiStudentGuardian`): `linkId` · `parentId` · `nameAr` · `nameEn` · `mobile` ·
  `relationshipLookupId` · `relationship` · `isPrimaryContact` · `isFinanciallyResponsible` ·
  `isPickupAuthorized` · `isPortalVisible` · `effectiveFromUtc`.
- `emergencyContacts[]` (`ApiEmergencyContact`): `id` · `nameAr` · `nameEn` · `phone` ·
  `isPickupAuthorized` · `relationshipLookupId`.

> **الملف الاجتماعي غائب عمدًا.** BR-GLB-072 يجعله فئة مقيّدة لها صلاحية شاشة مستقلة، تحديدًا
> ليُحجب عن أدوار تملك بقية الملف. كشفه خلف `STU/File/View` يسلّمه لكل من يحجبه عنه المتصفح.
> إن احتاجه التطبيق فله دالة خاصة به تحت `STU/SocialProfile`.

---

## ٥. شؤون الموظفين — `/api/v1/employees` · `/contracts` · `/payroll`

**الأجر فئة مقيّدة** (BR-EMP-003، BR-EMP-010): ملف الموظف **لا يحمل راتبًا**، والعقود والسجل
وقسائم الراتب كلٌّ خلف صلاحيته التي يستعملها المتصفح.

### الملف

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `GET employees` | `q` · `status` (string?) · `orgUnitId` · `page` · `pageSize` (int?) — query | `EMP/Directory/View` | `ApiPage<ApiEmployeeRow>` |
| `GET employees/{id}` | `id` (path, int) | `EMP/File/View` | `ApiEmployeeFile` |
| `POST employees` | `ApiRegisterEmployeeRequest` | `EMP/Directory/Create` | `200 ApiEmployeeFile` |
| `PUT employees/{id}` | `id` (path) + `ApiUpdateEmployeeRequest` | `EMP/File/Edit` | `200 ApiEmployeeFile` |
| `POST employees/{id}/status` | `id` (path) + `ApiChangeEmployeeStatusRequest` | `EMP/File/Approve` | **`204`** |
| `POST employees/{id}/assignments` | `id` (path) + `ApiAssignPositionRequest` | `EMP/File/Edit` | `200 { assignmentId }` |
| `POST employees/{id}/qualifications` | `id` (path) + `ApiQualificationRequest` | `EMP/File/Edit` | `200 { qualificationId }` |

`q` يبحث في **رقم الموظف وأجزاء الاسم الثمانية باللغتين**، وكل كلمة تضيّق ما قبلها، وغير حسّاس
لحالة الأحرف. `status` من `EmployeeStatus`: `Active` · `Suspended` · `Terminated`.

**`ApiRegisterEmployeeRequest`:** الأسماء الثمانية (عربي وإنجليزي، كلها إلزامية) · `gender` (✔) ·
`dateOfBirth` (✔) · `nationalityLookupId` (✔) · `userAccountId` (`int?` — ربط الموظف بحساب دخول) ·
`primaryIdTypeLookupId` · `primaryIdNo` · `primaryIdExpiry` · `mobile` · `whatsAppNumber` (كلها اختيارية).

**`ApiUpdateEmployeeRequest`** = ما سبق **زائد** `reason` (`string`, ✔).

**`ApiChangeEmployeeStatusRequest`:** `status` (✔) · `reason` (`string?`).

**`ApiAssignPositionRequest`:** `orgUnitId` (`int`, ✔) · `positionLookupId` (`int`, ✔) ·
`managerEmployeeId` (`int?`) · `effectiveFromUtc` (`DateTime?` — الفارغ = الآن).

**`ApiQualificationRequest`:** `titleAr` · `titleEn` · `dateAwarded` · `isTeachingRelevant` (`bool`) ·
`institutionName` · `documentAttachmentId` · `educationLookupId` · `universityLookupId` ·
`specializationLookupId` · `academicGradeLookupId` · `gpa` (`decimal?`).

**`ApiEmployeeRow`:** `employeeId` · `employeeNo` · `nameAr` · `nameEn` · `status` · `mobile` ·
`orgUnitName` · `positionName` (الاسمان الأخيران بلغة المتصل).

**`ApiEmployeeFile`:** حقول التسجيل، زائد `employeeId` · `employeeNo` · `nationalityName` ·
`status` · `hasPhoto` · `assignment` · `qualifications[]` — **ولا حقل راتب واحد**.

- `assignment` (`ApiEmployeeAssignment`): `assignmentId` · `orgUnitId` · `orgUnitName` ·
  `positionLookupId` · `positionName` · `managerEmployeeId` · `effectiveFromUtc`.
- `qualifications[]` (`ApiQualification`): `qualificationId` · `titleAr` · `titleEn` ·
  `institutionName` · `dateAwarded` · `isTeachingRelevant` · `educationLookupId` ·
  `universityLookupId` · `specializationLookupId` · `academicGradeLookupId` · `gpa` ·
  `documentAttachmentId`.

### العقود

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `GET employees/{id}/contracts` | `id` (path, int) | `EMP/Contracts/View` | `ApiContract[]` |
| `POST employees/{id}/contracts` | `id` (path) + `ApiContractRequest` | `EMP/Contracts/Create` | `200 ApiContract` |
| `PUT contracts/{contractId}` | `contractId` (path) + `ApiContractRequest` | `EMP/Contracts/Edit` | `200 ApiContract` |
| `POST contracts/{contractId}/status` | `contractId` (path) + `ApiContractStatusRequest` | `EMP/Contracts/Approve` | **`204`** |

**`ApiContractRequest`:** `type` (`string`, ✔ — `FullTime` · `PartTime` · `Term`) ·
`startDate` (✔) · `endDate` (✔) · `salaryBasic` (`decimal`, ✔) · `salaryAllowances` (`decimal?`).
نوع غير معروف ⇐ `422 invalid_contract_type`. تداخل عقدين ⇐ `409 overlapping_contract`.
تعديل عقد ليس مسوّدة ⇐ `409 contract_not_editable`.

**`ApiContractStatusRequest`:** `status` (`string`, ✔ — `Draft` · `Active` · `Terminated`).
الانتقال غير المسموح ⇐ `409 invalid_contract_status_transition`.

**`ApiContract`:** `contractId` · `employeeId` · `type` · `startDate` · `endDate` ·
`salaryBasic` · `salaryAllowances` · `status` · `currency`.

### الرواتب

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `GET payroll/runs/{runId}/register` | `runId` (path, int) | `EMP/Payroll/View` | `ApiPayrollRegister` |
| `GET payroll/lines/{lineId}/payslip` | `lineId` (path, int) | `EMP/Payroll/View` | `ApiPayslip` |
| `GET employees/{id}/payslips` | `id` (path) · `page` · `pageSize` (query, int?) | `EMP/Payroll/View` | `ApiPage<ApiPayrollRegisterLine>` |

**`ApiPayrollRegister`:** `runId` · `runNo` · `periodYear` · `periodMonth` · `paymentDate` ·
`status` · `currency` · `totalBasic` · `totalAllowances` · `totalAdditions` · `totalDeductions` ·
`totalAdvanceDeduction` · `totalGross` · `totalNet` · `lines[]`.

**`ApiPayrollRegisterLine`:** `lineId` · `employeeId` · `employeeNo` · `nameAr` · `nameEn` ·
`basicSalary` · `allowances` · `additionsTotal` · `deductionsTotal` · `advanceDeduction` ·
`grossPay` · `netPay`.

**`ApiPayslip`:** كل حقول السطر أعلاه، زائد `runId` · `runNo` · `periodYear` · `periodMonth` ·
`paymentDate` · `runStatus` · `bankName` · `bankAccountNo` · `currency` · `notes` ·
`adjustments[]` (`kind` · `description` · `amount`) ·
`advanceInstallments[]` (`advanceNo` · `sequenceNo` · `installmentCount` · `amount` · `remainingAfterThis`).

> **«قسيمة راتبي أنا» غير مبنيّة.** تحتاج صلاحية لا يعرّفها `ScreenCatalog`، واختراعها على طبقة
> نقل ثانية قرار أمني يُتخذ بالصدفة. اليوم يقرأ الموظف قسيمته عبر دور يحمل `EMP/Payroll/View`،
> وهو منح رواتب المدرسة كلها؛ تضييقه تغييرٌ في `ScreenCatalog` وشريحة عمل مستقلة.

---

## ٦. المالية — `/api/v1/finance`

**لا يُحسب أي رصيد هنا.** `IStatementService` و `IFeeAdmin.ComputeStudentPositionAsync` هما
الحساب المركزي الواحد الذي يفرضه BR-FEE-008؛ حسابٌ ثانٍ على طبقة نقل ثانية هو الطريق إلى أن
يختلف الهاتف والكشف المطبوع على ما تدين به عائلة.

### الرسوم والوضع المالي

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `GET fee-categories` | `includeInactive` (query, bool = `false`) | `FEE/Categories/View` | `ApiFeeCategory[]` |
| `GET fee-structure` | `academicYearId` · `gradeYearProfileId` (query, int?) | `FEE/Structure/View` | `ApiFeeStructureLine[]` |
| `GET students/{studentId}/position` | `studentId` (path, int) | `FEE/StudentFinance/View` | `ApiMoney` |
| `GET students/{studentId}/statement` | `studentId` (path) · `asOfUtc` (query, DateTime?) | `FEE/StudentFinance/View` | `ApiStatement` |
| `GET payers/{payerId}/statement` | `payerId` (path) · `asOfUtc` (query, DateTime?) | `FEE/Position/View` | `ApiStatement` |

`asOfUtc` الفارغ = الآن. `includeInactive=true` يعيد الفئات المعطّلة أيضًا — لازم لعرض رسم
قديم على قيد سابق، لا لقوائم الاختيار.

**`ApiFeeCategory`:** `feeCategoryId` · `nameAr` · `nameEn` · `vatRate` · `isMandatory` ·
`isRefundable` · `isServiceLinked` · `isActive`.

**`ApiFeeStructureLine`:** `feeStructureLineId` · `academicYearId` · `gradeYearProfileId` ·
`gradeCode` · `gradeName` · `feeCategoryId` · `categoryNameAr` · `categoryNameEn` · `amount` ·
`currency` · `status`.

**`ApiStatement`:** `payerId` · `studentId` · `asOfUtc` · `currency` · `grossCharges` ·
`discounts` · `creditNotes` · `payments` · `refunds` · `netCharges` · `closingBalance` · `lines[]`
— وكل سطر `dateUtc` · `kind` · `documentNo` · `description` · `debit` · `credit` · `runningBalance`.

### القيود والإشعارات

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `GET students/{studentId}/charges` | `studentId` (path) · `page` · `pageSize` (query) | `FEE/Charges/View` | `ApiPage<ApiCharge>` |
| `POST charges` | `ApiPostChargeRequest` | `FEE/Charges/Post` | `200 ApiCharge` |
| `POST charges/{chargeId}/credit-notes` | `chargeId` (path) + `ApiCreditNoteRequest` | `FEE/Charges/Deactivate` | `200 { creditNoteId }` |
| `GET students/{studentId}/installments` | `studentId` (path, int) | `INS/Schedule/View` | `ApiInstallment[]` |

**`ApiPostChargeRequest`:** `studentId` (`int`, ✔) · `feeStructureLineId` (`int`, ✔) ·
`sourceType` (`string?`).

> `sourceType` يقبل **ثلاث قيم فقط** من `ChargeSourceType`: `Registration` · `ReRegistration` ·
> `ServiceAssignment`. أي شيء آخر — بما فيه `Manual` و `OpeningBalance` وقيمة غير معروفة —
> **يسقط بصمت إلى `Registration`** ولا يُرفض. القيد اليدوي والرصيد الافتتاحي لهما مساراهما، ولا
> يُصنعان من هاتف.

الرفوض الخاصة بهذه الدالة:
- `404 not_found` — `feeStructureLineId` لا يقابل سطر تعرفة.
- `409 student_has_no_payer` — لا يوجد ولي أمر مسؤول ماليًّا.
- `409 category_already_charged` — **حارس التكرار نفسه الذي في شاشة الكاشير**، أُعيد تطبيقه هنا
  لأنه يعيش في المتحكّم لا في `IFeeAdmin`؛ واجهة تتخطّاه تجعل الهاتف يحمّل العائلة مرتين حيث
  يرفض المتصفح. يفحص قيدًا مرحَّلًا لنفس الفئة ونفس العام، **ويستثني القيود اليدوية**.
- `409 fee_line_not_approved` — سطر التعرفة ليس معتمدًا.

**`ApiCreditNoteRequest`:** `amount` (`decimal`, ✔) · `reason` (`string`, ✔). المبلغ الأكبر من
القيد ⇐ `422 credit_note_exceeds_charge`. القيد غير المرحّل ⇐ `409 charge_not_posted`.

**`ApiCharge`:** `chargeId` · `chargeNo` · `studentId` · `payerId` · `feeCategoryId` ·
`categoryNameAr` · `categoryNameEn` · `netAmount` · `vatAmount` · `grossAmount` ·
`vatRateSnapshot` · `currency` · `status` · `postedAtUtc`.

**`ApiInstallment`:** `installmentId` · `sequenceNumber` · `dueDate` · `amount` · `paid` ·
`outstanding` · `status` · `isPdcCovered` · `currency`.

### الصندوق والسندات

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `POST receipts` | `ApiCaptureReceiptRequest` | `PAY/Cashier/Create` | `200 ApiReceipt` |
| `GET payers/{payerId}/receipts` | `payerId` (path) · `page` · `pageSize` (query) | `PAY/Cashier/View` | `ApiPage<ApiReceipt>` |
| `POST till/open` | `ApiOpenTillRequest` | `PAY/Till/Create` | `200 { tillSessionId, tillCode }` |
| `POST till/{tillSessionId}/close` | `tillSessionId` (path) + `ApiCloseTillRequest` | `PAY/Till/Post` | **`204`** |

**`ApiCaptureReceiptRequest`:**

| الحقل | النوع | إلزامي | ملاحظة |
|---|---|---|---|
| `payerId` | `int` | ✔ | |
| `method` | `string` | ✔ | `Cash` · `Card` · `BankTransfer` · `Cheque` · `Pdc` — وإلا `422 invalid_payment_method` |
| `amount` | `decimal` | ✔ | |
| `tillSessionId` | `int?` | — | **اختياري** — لكن إن أُرسل وجب أن تكون الجلسة مفتوحة، وإلا `409 till_session_not_open` |
| `methodRefNo` | `string?` | — | رقم العملية / الشيك |

**`ApiOpenTillRequest`:** `tillCode` (`string?` — **الفارغ = الإسناد التلقائي**) · `floatAmount`
(`decimal`) · `cashierUserId` (`int?` — الفارغ = المتصل نفسه).

> **رمز الصندوق يُسنَد ولا يُطلَب (BR-PAY-001).** اترك `tillCode` فارغاً وهو الوضع المعتاد:
> يعيد الخادم للأمين صندوقه المعتاد إن سبق له فتح وردية، وإلا سكّ له `TILL-n` جديداً لم
> يُستخدم قط، ويردّ الرمز المُسنَد في `tillCode` ضمن الرد. لا تُرسل رمزاً إلا لوضع أمين على
> درج بعينه. الوردية أمين واحد وصندوق واحد ويوم واحد، ويحرس المنفذ الطرفين: أمين له وردية
> مفتوحة يُرفض بـ `409 cashier_till_already_open`، وصندوق مفتوح لدى غيره يُرفض بـ
> `409 till_already_open` (والمقارنة لا تفرّق بين حالات الأحرف).

**`ApiCloseTillRequest`:** `countedTotal` (`decimal`) · `varianceReason` (`string?`).

> عند الإقفال يحسب النظام `systemTotal` من السندات المرحَّلة على الجلسة ويخزّنه مع المعدود
> والسبب. **`varianceReason` لا يفرضه المنفذ** حتى مع وجود فرق — من أراد إلزامه فذلك حارس
> يُضاف في `IPaymentAdmin`، لا في طبقة النقل، وإلا اختلف الهاتف عن المتصفح. جلسة غير مفتوحة
> تُرفض بـ `409 till_session_not_open`.

**`ApiReceipt`:** `receiptId` · `receiptNo` · `payerId` · `method` · `methodRefNo` · `amount` ·
`currency` · `status` · `issuedAtUtc`.

---

## ٧. الحسابات — `/api/v1/accounting`

**للقراءة فقط، بقرار لا بتدرّج.** هذا النظام يحاسب ويحصّل، والمنتج المحاسبي يمسك الدفاتر،
والكتابة الوحيدة التي تعبر الحدّ هي دفعة تصدير GL ولها شاشتها. قيد يومية يُكتب من جهة المدرسة
مكانه شاشات المنتج المحاسبي.

يُوصل إليها عبر `Sms.Erp.Bridge` وحده، على `ILedgerAnalytics` و `IChartOfAccountsDirectory` —
كلاهما عقد استعلام مصرَّح به.

| الدالة | البارامترات | الصلاحية | الرد |
|---|---|---|---|
| `GET status` | — | `FEE/GlExport/View` | `ApiLedgerStatus` |
| `GET accounts` | — | `FEE/GlExport/View` | `ApiGlAccount[]` |
| `GET trial-balance` | `asOf` (query, DateTime?) | `FEE/GlExport/View` | `ApiTrialBalance` |
| `GET accounts/balance` | `codes` (query, string[] — **مكرَّر**) · `asOf` (DateTime?) | `FEE/GlExport/View` | `ApiMoney` |
| `GET entries/recent` | `count` (query, int = `20`) | `FEE/GlExport/View` | `ApiGlEntry[]` |
| `GET entries/drafts` | `count` (query, int = `20`) | `FEE/GlExport/View` | `ApiGlEntry[]` |
| `GET result` | `from` · `to` (DateTime?) · `months` (int = `12`) — query | `DSH/Statistics/View` | `ApiLedgerResult` |

`codes` تُرسل مكرَّرة: `?codes=1101&codes=1102`. قائمة فارغة ⇐ `422 no_account_codes`.
`count` محصور بين **1 و 100**؛ خارج المدى يُصحَّح إلى 20 أو 100.

**`ApiLedgerStatus`:** `isAttached` (`bool`) · `supportsResultSummary` (`bool`).
**نادِها أولًا** لتُخفي القسم كله بدل عرض شاشة ترد 503.

**`ApiGlAccount`:** `code` · `name` (بلغة المتصل) · `nature`.

**`ApiTrialBalance`:** `asOf` · `debit` · `credit` · `difference` · `isBalanced` (`bool`) ·
`accountCount` · `currency`.

**`ApiGlEntry`:** `number` · `entryDate` · `description` · `reference` · `sourceModule` ·
`amount` · `currency` · `state` · `createdBy`.

**`ApiLedgerResult`:** `fromDate` · `toDate` · `currency` · `revenue` · `expenses` · `net` ·
`months[]` — وكل شهر `year` · `month` · `revenue` · `expenses` · `net`.

> على تركيب بلا جسر ERP ترد **كل** دوال الحسابات بـ **`503 ledger_not_attached`**. لا ترد صفرًا
> أبدًا: «الدفاتر فارغة» و«لم يسأل أحد الدفاتر» جملتان مختلفتان، وواحدة منهما فقط صحيحة.
>
> الصلاحيات هنا **مُعاد استعمالها لا جديدة**: من يبني دفعة GL ويصدّرها هو بعينه من يقرأ الدفتر
> الذي تهبط فيه. صلاحية جديدة تعني 404 للجميع — مدير النظام معهم — حتى يُعاد تشغيل البذّار.

---

## ٨. التقارير

**لا توجد مجموعة `/api/v1/reports`، وهذا يجب أن يُقال صراحة.** منصّة التقارير في هذا المنتج
مبنيّة حول تعريفات مبذورة تُصيَّر شاشات ويُصدَّر منها PDF/Excel، ولم تُكشف على الواجهة: كشفها
يعني نقل محرّك التصيير والتصدير إلى طبقة نقل ثانية، وهي شريحة عمل قائمة بذاتها لا سطر في مرجع.

ما هو متاح اليوم من بيانات **ذات شكل تقرير**، وكلٌّ منها بصلاحيته:

| التقرير المطلوب عادةً | الدالة التي تعطي بياناته | الصلاحية |
|---|---|---|
| كشف حساب طالب | `GET /finance/students/{id}/statement` | `FEE/StudentFinance/View` |
| كشف حساب وليّ أمر (العائلة) | `GET /finance/payers/{id}/statement` | `FEE/Position/View` |
| الوضع المالي للطالب (رقم واحد) | `GET /finance/students/{id}/position` | `FEE/StudentFinance/View` |
| جدول الأقساط | `GET /finance/students/{id}/installments` | `INS/Schedule/View` |
| سجل رواتب دورة | `GET /payroll/runs/{runId}/register` | `EMP/Payroll/View` |
| قسيمة راتب | `GET /payroll/lines/{lineId}/payslip` | `EMP/Payroll/View` |
| ميزان المراجعة | `GET /accounting/trial-balance` | `FEE/GlExport/View` |
| الإيراد والمصروف والاتجاه الشهري | `GET /accounting/result` | `DSH/Statistics/View` |
| حضور الطالب (ملخّص العام) | `GET /portal/students/{id}/attendance` | `POR/Child/View` |
| نتائج الطالب | `GET /portal/students/{id}/results` | `POR/Child/View` |

كلها ترد **بيانات JSON** لا ملفًّا؛ التنسيق والطباعة على التطبيق. من يحتاج تقريرًا مبذورًا
بعينه — بمعاييره وتجميعاته وتصديره — فتلك دالة جديدة تُبنى فوق منصّة التقارير القائمة، وتُذكر
هنا حين تُبنى.

---

## ٩. إصدار التطبيق — `/api/v1/app`

المجموعة الوحيدة **المجهولة بالكامل** (`[AllowAnonymous]`)، وعمدًا: الحالة التي وُجدت من أجلها في
أقسى صورها هي نسخة قديمة إلى حد أنها لا تستطيع تسجيل الدخول أصلًا — ولو طلبت رمزًا لأجابت ذلك
الهاتف بفشل دخول بدل الرسالة الوحيدة التي تنفعه. وهي كذلك أول نداء عند الإقلاع البارد، قبل قراءة
مخزن المفاتيح. ما تكشفه رقم إصدار ومسار تسلّمهما المدرسة لكل أسرة أصلًا — لا اسم مدرسة ولا شخص
ولا سجل. أما ملف التطبيق نفسه فيبقى خلف تسجيل الدخول كما كان.

### `GET /api/v1/app/version`

| | |
|---|---|
| الصلاحية | **بدون** — برنامج المدرسة يعرّف بنفسه، وليس سجلًّا |
| البارامترات | `version`, `build` (query) |
| الرد | `200 ApiAppVersionResponse` |

**بارامترات الاستعلام:**

| الحقل | النوع | إلزامي | ملاحظة |
|---|---|---|---|
| `version` | `string?` | — | إصدار النسخة العاملة، `1.1.0` |
| `build` | `int?` | — | رقم البناء (`versionCode`)، `2` |

بارامتران لا واحد: علامة `+` في سلسلة الاستعلام تُفكّ إلى مسافة، فصيغة `1.1.0+2` كانت ستحتاج
ترميزًا بالنسبة المئوية من كل عميل وإلى الأبد، ونسيانه مرّة واحدة يبدو كنسخة بلا رقم بناء لا
كخطأ. و`version` غير مقروء **يُجاب لا يُرفض**: دالة فحص ترد `400` هي دالة تتوقف بصمت عن العمل،
والحقائق تظل تستحق الإرسال — يبقى الحكمان `false` فقط، لأن نسخة لا يمكن ترتيبها لا يمكن وصفها
بالقِدَم بصدق.

**الرد `ApiAppVersionResponse`:**

| الحقل | النوع | متى يمتلئ |
|---|---|---|
| `published` | `bool` | `false` ⇐ لم تنشر المدرسة أي حزمة بعد، وكل ما تحته فارغ |
| `latestVersion` | `string?` | إصدار أحدث حزمة منشورة، من اسم الملف نفسه (`sms-portal-1.4.0+12.apk`) |
| `latestBuild` | `int?` | رقم البناء، حين يحمله اسم الملف |
| `publishedAtUtc` | `DateTime?` | تاريخ وضع الملف في المجلد — يُعرض ولا يُقارن |
| `minimumVersion` | `string?` | أقدم نسخة تقبلها المدرسة، من الإعداد `MobileApp:MinimumSupportedVersion` — فارغ افتراضًا |
| `minimumBuild` | `int?` | نصف رقم البناء من الإعداد نفسه |
| `updateAvailable` | `bool` | `true` ⇐ توجد نسخة أحدث. تنبيه قابل للإخفاء |
| `updateRequired` | `bool` | `true` ⇐ النسخة العاملة أقدم من حدّ المدرسة. شاشة لا يمكن تجاوزها |
| `installUrl` | `string` | `/portal/app` — صفحة التطبيق في البوابة، بحجم الملف وتاريخه وخطوات التثبيت باللغتين |

**الحكم للخادم لا للتطبيق.** نسخة تقرّر بنفسها تقرّر بالمقارنة التي شحنتها هي — وهي النسخة
القديمة موضع السؤال — فلا يمكن تصحيح خطئها من جهة المدرسة أبدًا. إرسال `updateAvailable` و
`updateRequired` محسومَين هو ما يتيح لمدرسة أن تبدأ إلزام التحديث دون أن يثبّت أحد شيئًا أولًا.
أما **النص** فللتطبيق: التنبيه عنوان على شاشة يملكها، وله نصفاه في `lib/l10n/strings.dart`،
وجملة تُجلب عبر الشبكة هي الشيء الوحيد الذي يعود فارغًا على اتصال بطيء.

**حارسان على `updateRequired`:**

- **لا يُطلب تحديث لا تُشبعه حزمة منشورة.** الخطأ المعتاد رفع الإعداد قبل رفع ملف الـ APK،
  وتطبيقه حينها يُفرغ التطبيق من كل الأسر دفعة واحدة ويحيلهم إلى صفحة تعرض نسخة لا تكفي.
- **حدّ لا يمكن قراءته يُعامل كعدم وجود حدّ** — خطأ إملائي في مفتاح إعداد واحد يجب ألا يقدر على
  إغلاق التطبيق في وجه كل الأسر.

وكلا الحالتين تُسجَّل تحذيرًا يسمّي الإعداد.

**الرفض:** لا شيء. الدالة لا ترفض — تجيب دائمًا `200`، حتى حين لا يوجد ما يقال.

---

## ١٠. رموز الرفض (Error codes)

`code` ثابت ولا يتغيّر باللغة. اربط منطق التطبيق به وحده، واعرض `message` كما يصل.

**عامّة وأمنية**

| الرمز | الحالة | المعنى |
|---|---|---|
| `unauthenticated` | 401 | لا رمز، أو رمز منتهٍ أو ملغى |
| `invalid_credentials` | 401 | اسم مستخدم أو كلمة مرور خاطئة |
| `invalid_two_factor_code` | 401 | رمز TOTP خاطئ |
| `two_factor_token_expired` | 401 | مضت خمس دقائق على `twoFactorToken` |
| `account_locked` | 423 | BR-SEC-002 — والدقائق في نص الرسالة |
| `must_change_password` | 403 | BR-SEC-005 — لا شيء يعمل قبل تغييرها |
| `password_policy` | 422 | كلمة المرور الجديدة لم تحقق السياسة؛ `fields` يفصّل |
| `forbidden` | 403 | ممنوع لسبب غير الصلاحية |
| `cross_school_write` | 403 | كتابة عبر مدرسة أخرى |
| `outside_teaching_reach` | 403 | خارج مدى تدريس المعلّم (BR-LRN-002) |
| `not_found` | 404 | غير موجود **أو** لا صلاحية — بنفس الجسم تمامًا |
| `validation_failed` | 400 | الجسم لم يرتبط أو خالف قاعدة حقل؛ `fields` يفصّل |
| `audit_reason_required` | 422 | تعديل على كيان T1 بلا `reason` |
| `hard_delete_forbidden` | 409 | لا يوجد حذف في هذا المنتج (BR-GLB-005) — عطّل أو ألغِ |
| `ledger_not_attached` | 503 | تركيب بلا جسر ERP |

**الطلاب** — `invalid_gender` (422) · `invalid_student_status` (422) ·
`invalid_student_status_transition` (409) · `duplicate_enrollment` (409) ·
`last_financially_responsible_guardian` (409).

**الموظفون** — `invalid_employee_status` · `invalid_employee_status_transition` ·
`invalid_contract_type` · `invalid_contract_status` · `invalid_contract_status_transition` ·
`overlapping_contract` · `contract_not_editable` · `qualification_not_found` · `org_unit_in_use`.

**التعلّم** — `lesson_transition_refused` · `lesson_retired` · `lesson_session_mismatch` ·
`homework_transition_refused` · `homework_issue_refused` · `homework_withdrawal_blocked` ·
`resource_not_scan_clean` · `resource_not_available`.

**المالية** — `student_has_no_payer` · `category_already_charged` · `charge_not_posted` ·
`charge_has_activity` · `credit_note_exceeds_charge` · `fee_category_in_use` ·
`fee_line_already_exists` · `fee_line_in_use` · `fee_line_not_approved` · `fee_line_not_draft` ·
`invalid_fee_line_status_transition` · `invalid_payment_method` · `till_session_not_open` ·
`cashier_till_already_open` · `till_already_open` ·
`installment_not_open` · `installment_not_overdue` · `no_charges_to_schedule` ·
`refund_exceeds_position` · `invalid_refund_status_transition` · `pdc_not_coverable` ·
`invalid_pdc_status_transition` · `promise_date_out_of_range` · `reschedule_case_not_pending` ·
`reschedule_remainder_mismatch` · `plan_assignment_exists` · `plan_template_not_approved` ·
`plan_template_not_draft` · `invalid_template_split` · `template_category_not_mandatory` ·
`assignment_reason_required` (سبب إسناد **استثناء قسط** — لا علاقة له بتعيين وظيفة موظف).

> الجدول أعلاه يعدّ رموز الرفض التي تعرفها طبقة الترجمة كلها. جزء منها يخصّ عمليات أقساط
> وخطط سداد **لا دالة لها في هذه الواجهة اليوم**؛ أُبقيت مذكورة لأنها تصل من المنافذ نفسها
> متى بُنيت تلك الدوال، ولأن تطبيقًا يتعامل مع `code` غير معروف بأن يعرض `message` كما هو
> يبقى صحيحًا في كل الحالات.

**الحسابات** — `no_account_codes` · `ledger_not_attached`.

---

## ١١. القيم النصية المقبولة (Enums)

كل حقل نصّي من هذه القوائم **غير حسّاس لحالة الأحرف** (`enrolled` = `Enrolled`).

| النوع | القيم | يُستعمل في |
|---|---|---|
| `Gender` | `Male` · `Female` | تسجيل الطالب والموظف |
| `StudentStatus` | `Enrolled` · `Suspended` · `Withdrawn` · `Graduated` · `Transferred` · `Alumni` | فلترة الدليل، تغيير الحالة |
| `EmployeeStatus` | `Active` · `Suspended` · `Terminated` | فلترة دليل الموظفين، تغيير الحالة |
| `ContractType` | `FullTime` · `PartTime` · `Term` | تعريف العقد |
| `ContractStatus` | `Draft` · `Active` · `Terminated` | تغيير حالة العقد |
| `EnrollmentSourceType` | `Admission` · `Rollover` · `Reinstatement` | `POST students/{id}/enrollments` |
| `ChargeSourceType` | `Registration` · `ReRegistration` · `ServiceAssignment` · `Manual` · `OpeningBalance` | `POST finance/charges` |
| `PaymentMethod` | `Cash` · `Card` · `BankTransfer` · `Cheque` · `Pdc` | `POST finance/receipts` |
| `LessonStatus` | `Draft` · `Published` · `Retired` | فلترة الدروس |
| `HomeworkStatus` | `Draft` · `Issued` · `Collecting` · `Marking` · `Released` · `Withdrawn` | فلترة الواجبات |
| `LatenessPolicy` | `AcceptWithoutPenalty` · `AcceptWithPenalty` | إنشاء وتعديل الواجب |

**فرق جوهري في المعاملة:** القيمة غير المعروفة في **فلتر query** (`status` في قوائم الطلاب
والموظفين والدروس والواجبات) **تُتجاهل** ويعود الطلب بالقائمة كاملة. أما في **جسم الطلب**
(`gender`, `status`, `type`, `method`) فتُرفض بـ `422` ورمز خاص بها. الفلتر تخمين، والجسم قرار.

---

## ١٢. ملخّص سريع — كل الدوال في جدول واحد

| # | الدالة | الصلاحية |
|---|---|---|
| 1 | `POST /api/v1/auth/login` | — |
| 2 | `POST /api/v1/auth/two-factor` | — |
| 3 | `POST /api/v1/auth/change-password` | — |
| 4 | `POST /api/v1/auth/logout` | — |
| 5 | `GET /api/v1/auth/me` | — |
| 6 | `GET /api/v1/portal/children` | `POR/Home/View` |
| 7 | `GET /api/v1/portal/students/{id}/attendance` | `POR/Child/View` |
| 8 | `GET /api/v1/portal/students/{id}/results` | `POR/Child/View` |
| 9 | `GET /api/v1/portal/students/{id}/timetable` | `POR/Child/View` |
| 10 | `GET /api/v1/portal/students/{id}/fees` | `POR/Statement/View` |
| 11 | `GET /api/v1/portal/statement` | `POR/Statement/View` |
| 12 | `GET /api/v1/portal/students/{id}/homework` | `POR/Work/View` |
| 13 | `GET /api/v1/portal/students/{id}/lessons` | `POR/Lessons/View` |
| 14 | `GET /api/v1/portal/resources/{id}/file` | `POR/Lessons/View` |
| 15 | `GET /api/v1/portal/announcements` | `POR/Announcements/View` |
| 16 | `GET /api/v1/learning/reach/offerings` | `LRN/Planner/View` |
| 17 | `GET /api/v1/learning/reach/sections` | `LRN/Homework/View` |
| 18 | `GET /api/v1/learning/lessons` | `LRN/Planner/View` |
| 19 | `GET /api/v1/learning/lessons/{id}` | `LRN/Planner/View` |
| 20 | `POST /api/v1/learning/lessons` | `LRN/Planner/Create` |
| 21 | `PUT /api/v1/learning/lessons/{id}` | `LRN/Planner/Edit` |
| 22 | `POST /api/v1/learning/lessons/{id}/publish` | `LRN/Planner/Approve` |
| 23 | `POST /api/v1/learning/lessons/{id}/retire` | `LRN/Planner/Deactivate` |
| 24 | `POST /api/v1/learning/lessons/{id}/resources` | `LRN/Resources/Create` |
| 25 | `POST /api/v1/learning/resources/{id}/withdraw` | `LRN/Resources/Deactivate` |
| 26 | `GET /api/v1/learning/resources/{id}/file` | `LRN/Resources/View` |
| 27 | `GET /api/v1/learning/homework` | `LRN/Homework/View` |
| 28 | `POST /api/v1/learning/homework` | `LRN/Homework/Create` |
| 29 | `PUT /api/v1/learning/homework/{id}` | `LRN/Homework/Edit` |
| 30 | `POST /api/v1/learning/homework/{id}/issue` | `LRN/Homework/Approve` |
| 31 | `POST /api/v1/learning/homework/{id}/withdraw` | `LRN/Homework/Deactivate` |
| 32 | `GET /api/v1/students` | `STU/Directory/View` |
| 33 | `GET /api/v1/students/{id}` | `STU/File/View` |
| 34 | `POST /api/v1/students` | `STU/Directory/Create` |
| 35 | `PUT /api/v1/students/{id}` | `STU/File/Edit` |
| 36 | `POST /api/v1/students/{id}/status` | `STU/File/Approve` |
| 37 | `POST /api/v1/students/{id}/guardians` | `STU/Guardians/Edit` |
| 38 | `POST /api/v1/students/guardians/{linkId}/unlink` | `STU/Guardians/Deactivate` |
| 39 | `POST /api/v1/students/{id}/emergency-contacts` | `STU/File/Edit` |
| 40 | `POST /api/v1/students/{id}/enrollments` | `STU/Enrollment/Create` |
| 41 | `GET /api/v1/employees` | `EMP/Directory/View` |
| 42 | `GET /api/v1/employees/{id}` | `EMP/File/View` |
| 43 | `POST /api/v1/employees` | `EMP/Directory/Create` |
| 44 | `PUT /api/v1/employees/{id}` | `EMP/File/Edit` |
| 45 | `POST /api/v1/employees/{id}/status` | `EMP/File/Approve` |
| 46 | `POST /api/v1/employees/{id}/assignments` | `EMP/File/Edit` |
| 47 | `POST /api/v1/employees/{id}/qualifications` | `EMP/File/Edit` |
| 48 | `GET /api/v1/employees/{id}/contracts` | `EMP/Contracts/View` |
| 49 | `POST /api/v1/employees/{id}/contracts` | `EMP/Contracts/Create` |
| 50 | `PUT /api/v1/contracts/{contractId}` | `EMP/Contracts/Edit` |
| 51 | `POST /api/v1/contracts/{contractId}/status` | `EMP/Contracts/Approve` |
| 52 | `GET /api/v1/payroll/runs/{runId}/register` | `EMP/Payroll/View` |
| 53 | `GET /api/v1/payroll/lines/{lineId}/payslip` | `EMP/Payroll/View` |
| 54 | `GET /api/v1/employees/{id}/payslips` | `EMP/Payroll/View` |
| 55 | `GET /api/v1/finance/fee-categories` | `FEE/Categories/View` |
| 56 | `GET /api/v1/finance/fee-structure` | `FEE/Structure/View` |
| 57 | `GET /api/v1/finance/students/{id}/position` | `FEE/StudentFinance/View` |
| 58 | `GET /api/v1/finance/students/{id}/statement` | `FEE/StudentFinance/View` |
| 59 | `GET /api/v1/finance/payers/{id}/statement` | `FEE/Position/View` |
| 60 | `GET /api/v1/finance/students/{id}/charges` | `FEE/Charges/View` |
| 61 | `POST /api/v1/finance/charges` | `FEE/Charges/Post` |
| 62 | `POST /api/v1/finance/charges/{id}/credit-notes` | `FEE/Charges/Deactivate` |
| 63 | `GET /api/v1/finance/students/{id}/installments` | `INS/Schedule/View` |
| 64 | `POST /api/v1/finance/receipts` | `PAY/Cashier/Create` |
| 65 | `GET /api/v1/finance/payers/{id}/receipts` | `PAY/Cashier/View` |
| 66 | `POST /api/v1/finance/till/open` | `PAY/Till/Create` |
| 67 | `POST /api/v1/finance/till/{id}/close` | `PAY/Till/Post` |
| 68 | `GET /api/v1/accounting/status` | `FEE/GlExport/View` |
| 69 | `GET /api/v1/accounting/accounts` | `FEE/GlExport/View` |
| 70 | `GET /api/v1/accounting/trial-balance` | `FEE/GlExport/View` |
| 71 | `GET /api/v1/accounting/accounts/balance` | `FEE/GlExport/View` |
| 72 | `GET /api/v1/accounting/entries/recent` | `FEE/GlExport/View` |
| 73 | `GET /api/v1/accounting/entries/drafts` | `FEE/GlExport/View` |
| 74 | `GET /api/v1/accounting/result` | `DSH/Statistics/View` |
| 75 | `GET /api/v1/app/version` | — (مجهول) |

---

## ١٣. وثيقة OpenAPI الحيّة

على جهاز التطوير وحده: **`/api/docs`** — تعرض نفس الـ 75 دالة بحقولها ورموز رفضها، مولَّدة من
الكود لا مكتوبة بيد، فهي المرجع الذي لا يتقادم إن اختلف مع هذا الملف. غير مُفعَّلة خارج
Development عمدًا: وثيقة تعدّد كل حقل ورمز في المنتج راحةٌ على جهاز مطوّر واستطلاعٌ على خادم مدرسة.
