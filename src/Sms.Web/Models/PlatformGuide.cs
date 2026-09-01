using System.Collections.Generic;

namespace Sms.Web.Models
{
    /// <summary>
    /// The prose behind the help button in the top bar: how this product's screens are driven.
    /// <para>
    /// Every screen already carries its own panel, and <c>Sms.Web.Tests/HelpCoverageTests</c> fails
    /// the build when one does not. What no per-screen panel can say is the part that is true of all
    /// of them at once — that the menu is a different size for every role, that nothing is ever
    /// deleted, that a not-found is usually a permission and not a broken link, that the working year
    /// decides what a screen is even looking at. That knowledge lived in <c>docs/</c>, which the
    /// person filling in the form does not have open, and in whoever trained them, who leaves.
    /// </para>
    /// <para>
    /// Written as content rather than markup for the same reasons <see cref="SetupHelp"/> is: a
    /// reviewer should be able to read the sentences without parsing Razor, both languages sit
    /// beside each other where a missing translation is visible, and the guide is one file to keep
    /// true rather than a page to re-edit.
    /// </para>
    /// <para>
    /// Sourced from <c>doc/UI/02-Screen-Patterns.md</c> §1 (the thirteen screen shapes) and §2, and
    /// from the global rules in <c>doc/03-Business-Rules.md</c>. Deviation, stated deliberately: the
    /// keyboard section describes only what the product actually answers to today. UI 02 §3 also
    /// specifies product-wide shortcuts (<c>/</c>, <c>Ctrl+K</c>, <c>Alt+A</c>, <c>Alt+N</c>,
    /// <c>Alt+Y</c>, <c>?</c>) and none of them is bound in <c>wwwroot/js</c>; documenting them here
    /// would have been the guide's first lie, so it says plainly that they are not built.
    /// </para>
    /// </summary>
    public static class PlatformGuide
    {
        private static string T(bool arabic, string en, string ar) => arabic ? ar : en;

        private static HelpPanelViewModel.Item I(bool arabic, string headingEn, string headingAr, string bodyEn, string bodyAr)
            => new(T(arabic, headingEn, headingAr), T(arabic, bodyEn, bodyAr));

        /// <summary>The staff guide — five chapters, read in order by someone new and dipped into after.</summary>
        public static IReadOnlyList<GuideSection> ForStaff(bool arabic) => new[]
        {
            new GuideSection(
                "getting-around",
                "bi-compass",
                T(arabic, "Finding your way", "التنقّل في النظام"),
                T(arabic,
                    "Two menus, one top bar, and a help button on every screen. Ten minutes here is the difference between hunting for a screen and knowing where it has to be.",
                    "قائمتان وشريط علوي وزر مساعدة في كل شاشة. وعشر دقائق هنا هي الفرق بين البحث عن الشاشة ومعرفة أين لا بد أن تكون."),
                new[]
                {
                    I(arabic,
                        "Two menus onto the same screens",
                        "قائمتان إلى الشاشات نفسها",
                        "The side menu groups screens the way the product is built — module by module, in the order the documentation numbers them. The home page groups the very same screens the way a school is staffed: finance, student affairs, the secretariat, the teaching staff. Neither is a subset of the other, and a cashier is right not to think of fees, instalments, payments and discounts as four separate modules.",
                        "القائمة الجانبية تجمع الشاشات كما بُني المنتج — وحدةً وحدة، بالترتيب الذي ترقّمها به الوثائق. والصفحة الرئيسية تجمع الشاشات نفسها كما تُوظَّف المدرسة: المالية وشؤون الطلاب والسكرتارية وهيئة التدريس. وليست إحداهما جزءاً من الأخرى، وأمين الصندوق محقّ إذ لا يرى الرسوم والتقسيط والمدفوعات والخصومات أربع وحدات منفصلة."),

                    I(arabic,
                        "What you see is yours alone",
                        "ما تراه أنت وحدك",
                        "The menu is filtered twice before it reaches you: by what your roles grant (BR-GLB-070), and by which modules this school switched on (BR-SET-006). A screen you were not granted is not greyed out — it is not drawn at all, and typing its address answers not-found rather than announcing that it exists. So a colleague's menu is legitimately a different size from yours, and a missing entry is a role or a feature toggle, not a fault.",
                        "القائمة تُرشَّح مرتين قبل أن تصلك: بما تمنحه أدوارك (BR-GLB-070)، وبالوحدات التي شغّلتها هذه المدرسة (BR-SET-006). والشاشة التي لم تُمنَحها لا تظهر باهتة — بل لا تُرسم أصلاً، وكتابة عنوانها تُجيب «غير موجود» بدل أن تُعلن أنها موجودة. فقائمة زميلك تختلف عن قائمتك بحق، والمدخل الغائب دورٌ أو مفتاح ميزة لا خلل."),

                    I(arabic,
                        "The top bar",
                        "الشريط العلوي",
                        "The language button switches the whole interface, its direction with it, and leaves you on the page you were reading. The tray is your approvals inbox — whatever is waiting on your decision. The pulse is the background-jobs board. Your name opens change-password and sign-out. This guide is the question mark beside them, and it is on every screen.",
                        "زر اللغة يبدّل الواجهة كلها، واتجاهها معها، ويتركك في الصفحة التي كنت تقرؤها. والصندوق هو وارد الموافقات — كل ما ينتظر قرارك. والنبض لوحة المهام الخلفية. واسمك يفتح تغيير كلمة المرور وتسجيل الخروج. وهذا الدليل هو علامة الاستفهام بجانبها، وهي في كل شاشة."),

                    I(arabic,
                        "This guide, and the button on the screen itself",
                        "هذا الدليل والزر الذي على الشاشة نفسها",
                        "Every screen in the product carries its own help button beside its title, and that one explains that screen: why a field is locked, why a status will not go backwards, why a particular save was refused. This guide explains what is true of all of them. When one specific screen refuses you, its own button is the shorter path.",
                        "كل شاشة في المنتج تحمل زر مساعدة خاصاً بها بجانب عنوانها، وهو يشرح تلك الشاشة: لماذا حقلٌ مقفل، ولماذا لا تعود حالة إلى الوراء، ولماذا رُفض حفظ بعينه. وهذا الدليل يشرح ما يصحّ عليها جميعاً. فإذا رفضتك شاشة بذاتها فزرّها هو الطريق الأقصر."),
                }),

            new GuideSection(
                "everywhere",
                "bi-check2-circle",
                T(arabic, "True on every screen", "قواعد تسري على كل شاشة"),
                T(arabic,
                    "Seven rules the whole product obeys. Most of what looks like a defect on a screen you have not met before is one of them working exactly as designed.",
                    "سبع قواعد يلتزم بها المنتج كله. ومعظم ما يبدو خللاً في شاشة لم تعرفها من قبل هو إحداها تعمل كما صُمّمت تماماً."),
                new[]
                {
                    I(arabic,
                        "You are always working inside one academic year",
                        "أنت تعمل دائماً داخل عام دراسي واحد",
                        "Enrolments, marks, attendance, fees and timetables all belong to a year, and your screens read and write the one you are working in. When that is not the school's Active year — while next year is being prepared, for instance — the screen says so in a yellow bar (BR-AYR-010). Read that bar before wondering where today's data went.",
                        "التسجيل والدرجات والحضور والرسوم والجداول كلها تخصّ عاماً، وشاشاتك تقرأ من العام الذي تعمل فيه وتكتب فيه. فإن لم يكن هو العام النشط للمدرسة — أثناء تحضير العام القادم مثلاً — قالت الشاشة ذلك في شريط أصفر (BR-AYR-010). فاقرأ ذلك الشريط قبل أن تتساءل أين ذهبت بيانات اليوم."),

                    I(arabic,
                        "Nothing is deleted here",
                        "لا شيء يُحذف هنا",
                        "There is no delete button in this product (BR-GLB-005). Master data is deactivated, documents are voided, requests are cancelled, assignments are ended. A deactivated row stops being offered in the pickers from that moment, while every record that already refers to it keeps reading correctly — which is the entire reason for the rule: last year's receipt has to go on naming the fee it charged.",
                        "لا يوجد زر حذف في هذا المنتج (BR-GLB-005). فالبيانات الأساسية تُعطَّل، والمستندات تُلغى، والطلبات تُسحب، والإسنادات تُنهى. والسطر المعطَّل يتوقف عن الظهور في قوائم الاختيار من تلك اللحظة، بينما يبقى كل سجل يشير إليه سليم القراءة — وهذا هو سبب القاعدة كله: إيصال العام الماضي يجب أن يظل يسمّي الرسم الذي حصّله."),

                    I(arabic,
                        "A refusal is a sentence, in your language",
                        "الرفض جملة مفهومة بلغتك",
                        "The server decides what may be saved, never the browser (BR-GLB-110), so a rule holds whether you reach it through a screen, an import or a link somebody sent you. When it refuses, a red bar at the top of the page says what happened and what to do about it, and the field at fault says it too (BR-GLB-111). An amber warning is a different thing: it can sometimes be overridden by whoever holds that right, and an override always asks for a reason before it will save.",
                        "الخادم هو من يقرر ما يُحفَظ لا المتصفّح (BR-GLB-110)، فالقاعدة تسري سواء وصلتَ عبر شاشة أو استيراد أو رابط أرسله إليك أحد. وحين يرفض يقول شريط أحمر أعلى الصفحة ما الذي حدث وما العمل، ويقوله الحقل المسؤول أيضاً (BR-GLB-111). أما التحذير الكهرماني فشيء آخر: قد يتجاوزه من يملك هذا الحق، والتجاوز يسأل عن السبب دائماً قبل أن يحفظ."),

                    I(arabic,
                        "Referential fields are picked, never typed",
                        "الحقول المرجعية تُختار ولا تُكتب",
                        "A grade, a section, a fee, a parent — anything the system already knows — arrives through a picker (BR-GLB-112), because a name typed twice is two records by tomorrow. Where the list is missing what you need, the + beside it adds the entry without losing what you have already filled in.",
                        "الصف والشعبة والرسم وولي الأمر — كل ما يعرفه النظام أصلاً — يصل عبر قائمة اختيار (BR-GLB-112)، لأن اسماً يُكتب مرتين يصير سجلَّين غداً. وحين تنقص القائمةَ حاجتُك فزرّ + بجانبها يضيف المدخل دون أن تفقد ما ملأته."),

                    I(arabic,
                        "Two names, side by side",
                        "اسمان متجاوران",
                        "Anything with a name carries an Arabic one and an English one, and both are required before it can be activated (BR-GLB-001) — a record named in one language is blank to half the school. The transliteration button between the pair suggests the other spelling; it assists and never commits, so read what it wrote before you save.",
                        "كل ما له اسم يحمل اسماً عربياً وآخر إنجليزياً، وكلاهما مطلوب قبل التفعيل (BR-GLB-001) — فالسجل المسمّى بلغة واحدة فارغ في عين نصف المدرسة. وزر النقل الحرفي بين الحقلين يقترح الكتابة الأخرى؛ يساعد ولا يعتمد، فاقرأ ما كتبه قبل الحفظ."),

                    I(arabic,
                        "Dates are Gregorian; amounts read left to right",
                        "التواريخ ميلادية والمبالغ تُقرأ من اليسار",
                        "Dates are entered as Gregorian in both languages, with a Hijri label shown alongside where the school has asked for one — switching the interface to Arabic never switches the calendar underneath you. Amounts keep Latin digits and right alignment in both directions, in the school's own currency and its rounding (BR-GLB-060).",
                        "التواريخ تُدخَل ميلادية في اللغتين، مع وسم هجري بجانبها حيث طلبت المدرسة ذلك — وتحويل الواجهة إلى العربية لا يبدّل التقويم من تحتك أبداً. والمبالغ تحتفظ بالأرقام اللاتينية وبالمحاذاة إلى اليمين في الاتجاهين، بعملة المدرسة نفسها وتقريبها (BR-GLB-060)."),

                    I(arabic,
                        "Every change carries a name and a time",
                        "لكل تغيير اسم ووقت",
                        "Records show who created them and who changed them last, and the history behind that is one click away for whoever may read it (BR-AUD-008). The changes that matter most — a mark after results are published, an amount already posted — will not save until you type why, and the reason is stored beside the change rather than in somebody's memory.",
                        "تُظهر السجلات من أنشأها ومن غيّرها آخر مرة، وسجلّ التغييرات خلف ذلك على بُعد نقرة لمن يحق له قراءته (BR-AUD-008). أما أهم التغييرات — درجة بعد إعلان النتائج، أو مبلغ رُحِّل — فلا تُحفَظ حتى تكتب السبب، ويُخزَّن السبب بجانب التغيير لا في ذاكرة أحد."),
                }),

            new GuideSection(
                "screen-kinds",
                "bi-columns-gap",
                T(arabic, "The kinds of screen", "أنواع الشاشات"),
                T(arabic,
                    "Thirteen shapes cover the whole product (doc UI 02 §1). Recognising the shape tells you where the actions are before you have read a single label on it.",
                    "ثلاثة عشر شكلاً تغطي المنتج كله (الوثيقة UI 02 §1). ومعرفة الشكل تدلّك على مواضع الأوامر قبل أن تقرأ عنواناً واحداً عليها."),
                new[]
                {
                    I(arabic,
                        "Register",
                        "السجل",
                        "A filter bar over a table: students, charges, incidents, catalogues. Narrow it with the filters first, then act — one row at a time from the action at the end of the row, or on many at once from the bar that appears when you tick them. Financial registers total at the foot, and where a print or an export is offered it is a right of its own, separate from reading the list.",
                        "شريط تصفية فوق جدول: الطلاب أو المطالبات أو المخالفات أو القوائم المرجعية. ضيّقه بالمرشّحات أولاً ثم نفّذ — سطراً سطراً من أمر آخر السطر، أو على أسطر كثيرة دفعةً من الشريط الذي يظهر عند تأشيرها. والسجلات المالية تُجمَّع في أسفلها، وحيث تُتاح طباعة أو تصدير فهما حق مستقل عن قراءة السجل."),

                    I(arabic,
                        "Record file",
                        "ملف السجل",
                        "A student, a parent, an employee, a programme: an identity header carrying the photo, the number, the status and the alerts, then a set of tabs. The header is identical on every file so a status is always in the same place; the detail lives in the tabs, and a tab with unsaved changes keeps a dot on it until you save.",
                        "طالب أو ولي أمر أو موظف أو برنامج: ترويسة هوية تحمل الصورة والرقم والحالة والتنبيهات، ثم مجموعة ألسنة. والترويسة واحدة في كل ملف لتكون الحالة في الموضع نفسه دائماً؛ والتفصيل داخل الألسنة، واللسان الذي فيه تغيير غير محفوظ تبقى عليه نقطة حتى تحفظ."),

                    I(arabic,
                        "Wizard",
                        "المعالج",
                        "Setup, registration, the year rollover, a stocktake: numbered steps, each validated as you leave it, and a review step before anything is committed. Saving a step carries you to the next, but the step list jumps anywhere and a completed step reopens. The commit at the end is one transaction — it all lands or none of it does.",
                        "الإعداد والتسجيل وترحيل العام والجرد: خطوات مرقّمة، تُتحقَّق كلٌّ منها عند مغادرتها، وخطوة مراجعة قبل أن يُعتمَد شيء. وحفظ الخطوة ينقلك إلى التي تليها، لكن قائمة الخطوات تنقلك إلى أي منها، والخطوة المكتملة تُفتح من جديد. والاعتماد في النهاية عملية واحدة — إما أن يقع كله أو لا يقع منه شيء."),

                    I(arabic,
                        "Status board",
                        "لوحة الحالات",
                        "Admissions, discipline cases, post-dated cheques: the columns are the stages a thing passes through and the cards are the things. Dragging a card is the transition itself, so it obeys the same rules and needs the same permission the buttons would — and asks for the same reason wherever the rule asks for one.",
                        "القبول وقضايا السلوك والشيكات الآجلة: الأعمدة هي المراحل التي يمرّ بها الشيء والبطاقات هي الأشياء. وسحب البطاقة هو الانتقال نفسه، فيخضع لقواعد الأزرار ويحتاج صلاحيتها — ويسأل عن السبب حيثما تسأله القاعدة."),

                    I(arabic,
                        "Capture sheet",
                        "ورقة الرصد",
                        "Attendance and marksheets: the roster down one side, the columns to fill across. Built for typing rather than clicking, so the keyboard carries you down a column at the rhythm a register is called in. It keeps a draft as you go, and a draft affects nothing anywhere else until you submit the sheet (BR-GLB-031).",
                        "الحضور وكشوف الدرجات: القائمة إلى جانب والأعمدة تُملأ عرضاً. مبنيّة للكتابة لا للنقر، فلوحة المفاتيح تنقلك في العمود بإيقاع مناداة الكشف. وتحفظ مسودة أولاً بأول، والمسودة لا تؤثر في شيء في أي مكان آخر حتى تعتمد الورقة (BR-GLB-031)."),

                    I(arabic,
                        "Till",
                        "نقطة البيع",
                        "The cashier's screen in the cafeteria, the store and the fees office: who is paying on one side, what they are taking in the middle, the basket and the tender on the other. It is keyboard-first and its own key list is printed on the screen. Finishing a sale prints a receipt and moves money, which is a separate right from ringing one up.",
                        "شاشة أمين الصندوق في المقصف والمتجر ومكتب الرسوم: من يدفع في جانب، وما يأخذه في الوسط، والسلة والسداد في الجانب الآخر. تُدار بلوحة المفاتيح أولاً، وقائمة مفاتيحها مطبوعة على الشاشة نفسها. وإنهاء البيع يطبع إيصالاً ويحرّك مالاً، وهو حق مستقل عن تسجيل عملية."),

                    I(arabic,
                        "Timetable grid",
                        "شبكة الجدول",
                        "The calendar board, the timetable builder, the exam schedule: time down one axis, and rooms, sections or teachers across the other. Placing something validates it where it lands and lists the conflicts beside the grid instead of refusing silently — that conflict list is the screen's real output.",
                        "لوحة التقويم وبنّاء الجدول وجدول الاختبارات: الزمن على محور، والقاعات أو الشعب أو المعلمون على الآخر. ووضع شيء يتحقّق منه في موضعه ويسرد التعارضات بجانب الشبكة بدل الرفض الصامت — وقائمة التعارضات تلك هي مُخرَج الشاشة الحقيقي."),

                    I(arabic,
                        "Operations console",
                        "لوحة التشغيل",
                        "Today's cover rota, the attendance monitor, the trip board: live tiles across the top and a queue of exceptions underneath. Every exception row carries the one action that resolves it, so the screen is worked from the top of the queue downwards rather than read.",
                        "احتياط اليوم ومراقبة الحضور ولوحة الرحلات: بطاقات حيّة في الأعلى وطابور استثناءات تحتها. وكل سطر استثناء يحمل الإجراء الواحد الذي يعالجه، فالشاشة تُعالَج من أعلى الطابور إلى أسفله لا تُقرأ."),

                    I(arabic,
                        "Approvals inbox",
                        "وارد الموافقات",
                        "The tray in the top bar, and the message threads: the list on one side and the item open beside it. Approve, return or reject from the preview without leaving the list — each of those asks for its reason wherever the rule requires one, so the keys accelerate the triage and never skip the governance.",
                        "الصندوق في الشريط العلوي، وخيوط الرسائل: القائمة في جانب والعنصر مفتوح بجانبها. اعتمد أو أعِد أو ارفض من المعاينة دون مغادرة القائمة — وكلٌّ منها يسأل عن سببه حيثما تطلبه القاعدة، فالسرعة في الفرز لا في تخطّي الحوكمة."),

                    I(arabic,
                        "Configuration matrix",
                        "مصفوفة الضبط",
                        "The permission tree, the subscription matrix, the discount stacking rules: rows against toggles. A cell that is locked and carries an explanation is a floor the product sets, not a right you are missing — the explanation names the rule holding it.",
                        "شجرة الصلاحيات ومصفوفة الاشتراكات وقواعد تراكم الخصومات: صفوف في مقابل مفاتيح. والخانة المقفلة المرفقة بتفسير هي حدٌّ يضعه المنتج لا صلاحية تنقصك — والتفسير يسمّي القاعدة التي تمسكها."),

                    I(arabic,
                        "Dashboard",
                        "لوحة المؤشرات",
                        "A grid of widgets, each reading your working year and your own data scope. A number here is the same number the register behind it would show — when the two disagree, it is the filter or the year that differs, not the arithmetic.",
                        "شبكة من العناصر، يقرأ كلٌّ منها عام عملك ونطاق بياناتك أنت. والرقم هنا هو الرقم نفسه الذي يعرضه السجل خلفه — فإن اختلفا فالمرشِّح أو العام هو المختلف لا الحساب."),

                    I(arabic,
                        "Statement",
                        "كشف الحساب",
                        "A family's position or a wallet's history: an identity header, then every charge and payment in order with a running balance, and the document behind each line one click away. It reads across years, so an old balance sits on the same page as this term's.",
                        "موقف أسرة أو تاريخ محفظة: ترويسة هوية، ثم كل مطالبة ودفعة بالترتيب مع رصيد متحرّك، والمستند خلف كل سطر على بُعد نقرة. ويقرأ عبر الأعوام، فالرصيد القديم يقع في الصفحة نفسها مع رصيد هذا الفصل."),

                    I(arabic,
                        "Launcher",
                        "لوحة الإطلاق",
                        "The home page and the department pages under it: large tiles, one per department you can open at least one screen of. A department that comes down to a single screen opens that screen instead of a page holding one card.",
                        "الصفحة الرئيسية وصفحات الأقسام تحتها: بطاقات كبيرة، واحدة لكل قسم تستطيع فتح شاشة واحدة منه على الأقل. والقسم الذي ينتهي إلى شاشة واحدة يفتح تلك الشاشة بدل صفحة تحمل بطاقة واحدة."),
                }),

            new GuideSection(
                "refusals",
                "bi-shield-exclamation",
                T(arabic, "When a screen refuses you", "حين ترفضك شاشة"),
                T(arabic,
                    "Five reasons, in the order they are worth checking. Between them they account for almost every \"it will not let me\".",
                    "خمسة أسباب مرتّبة بترتيب جدارتها بالفحص. وهي تفسّر بينها كل «لا يسمح لي» تقريباً."),
                new[]
                {
                    I(arabic,
                        "The screen is not there at all",
                        "الشاشة ليست موجودة أصلاً",
                        "A link someone sent you answers not-found. Either your roles do not include it (BR-GLB-070) or the module is switched off for this school (BR-SET-006). It says not-found rather than access-denied deliberately: a refusal that names what exists is itself information. The screen cannot tell you which of the two it was — the person who assigns roles can.",
                        "رابط أرسله إليك أحدهم يُجيب «غير موجود». فإما أن أدوارك لا تشمله (BR-GLB-070)، وإما أن الوحدة مطفأة في هذه المدرسة (BR-SET-006). ويقول «غير موجود» لا «ممنوع» عن قصد: فالرفض الذي يسمّي الموجود معلومة في ذاته. والشاشة لا تستطيع أن تقول أيّ السببين كان — ومن يُسنِد الأدوار يستطيع."),

                    I(arabic,
                        "The screen opens but the button is missing",
                        "الشاشة تفتح والزر غائب",
                        "Reading and changing are separate rights on the same screen, and so are approving, posting money, printing, exporting and importing. A register you may read but not export is a deliberate arrangement, not a half-finished screen. The index at the foot of this guide lists, screen by screen, exactly which of those you hold.",
                        "القراءة والتعديل حقّان منفصلان على الشاشة نفسها، وكذلك الاعتماد وترحيل المال والطباعة والتصدير والاستيراد. فسجلٌّ تقرؤه ولا تصدّره ترتيبٌ مقصود لا شاشة ناقصة. والفهرس في آخر هذا الدليل يسرد، شاشةً شاشة، ما تملكه من ذلك بالضبط."),

                    I(arabic,
                        "The year is closed",
                        "العام مغلق",
                        "A closed or archived academic year is read-only, for everyone, whatever their role. Look at which year you are working in before assuming a permission problem — it is the commonest cause of a screen that opens and then saves nothing.",
                        "العام الدراسي المغلق أو المؤرشف للقراءة فقط، للجميع، مهما كان دورهم. فانظر في أي عام تعمل قبل أن تفترض مشكلة صلاحيات — فهو أشيع أسباب شاشة تُفتح ثم لا تحفظ شيئاً."),

                    I(arabic,
                        "The record's own state forbids it",
                        "حالة السجل نفسها تمنع",
                        "Posted, approved, published, locked: states that close the door behind them by design, so that what other work has been built on cannot be edited underneath it. The correction path is a reversing document — a void, a credit note, a re-mark carrying its reason — and not an edit.",
                        "مُرحَّل أو معتمَد أو معلَن أو مقفل: حالات تغلق الباب خلفها قصداً، حتى لا يُعدَّل من تحته ما بُني عليه عمل آخر. وطريق التصحيح مستند عكسي — إلغاء أو إشعار دائن أو إعادة رصد تحمل سببها — لا تعديل."),

                    I(arabic,
                        "It is asking for a reason, not refusing",
                        "إنه يطلب سبباً لا يرفض",
                        "A box demanding why before it will save is not a refusal; the save is waiting on the sentence that will be stored beside it. Write what a colleague reading the history a year from now would need to know — \"correction\" tells them nothing that the change itself did not.",
                        "الصندوق الذي يطلب السبب قبل الحفظ ليس رفضاً؛ فالحفظ ينتظر الجملة التي ستُخزَّن بجانبه. فاكتب ما يحتاج زميل يقرأ السجل بعد عام أن يعرفه — فكلمة «تصحيح» لا تقول له شيئاً لم يقله التغيير نفسه."),
                }),

            new GuideSection(
                "keyboard-print",
                "bi-printer",
                T(arabic, "Keyboard, paper and files", "لوحة المفاتيح والورق والملفات"),
                T(arabic,
                    "What the keyboard answers to today, and the difference between putting something on paper and taking it out of the system.",
                    "ما تستجيب له لوحة المفاتيح اليوم، والفرق بين وضع شيء على الورق وإخراجه من النظام."),
                new[]
                {
                    I(arabic,
                        "What the keyboard does today",
                        "ما تفعله لوحة المفاتيح اليوم",
                        "Tab moves between controls and Enter or Space activates whichever has focus — the boards and grids are built from real buttons, so they answer the keyboard and a screen reader alike. Escape closes a dialog, and the menu drawer on a phone. The till is the one screen designed keyboard-first, and its key list is printed on the screen itself. There are no product-wide shortcut keys yet: the ones the design catalogue lists are not built, so nothing is lost by not hunting for them.",
                        "«Tab» ينقلك بين العناصر و«Enter» أو المسافة يفعّل ما عليه التركيز — فاللوحات والشبكات مبنيّة من أزرار حقيقية، فتستجيب للوحة المفاتيح ولقارئ الشاشة سواء. و«Esc» يغلق نافذة الحوار، ويغلق درج القائمة على الهاتف. ونقطة البيع هي الشاشة الوحيدة المصمَّمة للوحة المفاتيح أولاً، وقائمة مفاتيحها مطبوعة على الشاشة نفسها. ولا توجد بعد اختصارات عامة على مستوى المنتج: فالمذكورة في دليل التصميم لم تُبنَ، ولا شيء يفوتك إن لم تبحث عنها."),

                    I(arabic,
                        "Printing is not exporting",
                        "الطباعة ليست تصديراً",
                        "Print produces the school's own paper — a roster, a receipt, a report card — laid out for A4 and stamped with who printed it and when. Export hands a file out of the system, which is why it is its own permission and is recorded as one. Neither of them is a screenshot: when a list has to leave the screen, one of those two buttons exists for it.",
                        "الطباعة تُنتج ورق المدرسة نفسه — كشفاً أو إيصالاً أو شهادة درجات — مهيّأً لمقاس A4 وموسوماً بمن طبعه ومتى. والتصدير يُخرِج ملفاً من النظام، ولذلك كان صلاحية مستقلة ومسجَّلاً بذاته. وليس أيٌّ منهما لقطة شاشة: فحين تحتاج قائمة إلى مغادرة الشاشة فأحد الزرّين موجود لأجلها."),

                    I(arabic,
                        "The interface follows the language",
                        "الواجهة تتبع اللغة",
                        "Switching to Arabic mirrors the whole layout — the menu moves to the right and the tables move with it — and the same screen in English is the same screen, not another one. The one thing that never mirrors is a number: amounts, account codes and identity numbers keep their Latin digits and their direction, so that they cannot be misread.",
                        "التحويل إلى العربية يعكس التخطيط كله — فتنتقل القائمة إلى اليمين وتنتقل الجداول معها — والشاشة نفسها بالإنجليزية هي الشاشة نفسها لا أخرى. والشيء الوحيد الذي لا ينعكس أبداً هو الرقم: فالمبالغ وأرقام الحسابات وأرقام الهوية تحتفظ بأرقامها اللاتينية وباتجاهها حتى لا تُقرأ خطأً."),
                }),
        };

        /// <summary>
        /// The parent-and-student guide. Deliberately not a subset of the staff one: it describes the
        /// three portal pages and nothing else, because BR-SEC-010 keeps staff screens not merely
        /// closed to a portal account but unannounced to it, and a guide is an announcement.
        /// </summary>
        public static IReadOnlyList<GuideSection> ForPortal(bool arabic) => new[]
        {
            new GuideSection(
                "portal-around",
                "bi-house-heart",
                T(arabic, "Your portal", "بوابتك"),
                T(arabic,
                    "Three pages and a top bar. Everything here is your family's own record, as the school has published it.",
                    "ثلاث صفحات وشريط علوي. وكل ما هنا سجلّ أسرتك أنت، كما نشرته المدرسة."),
                new[]
                {
                    I(arabic,
                        "Three pages",
                        "ثلاث صفحات",
                        "My family lists your children, their sections and their published results. My statement is what has been charged and what has been paid. Announcements is what the school has sent. The buttons at the top move between them, and your child's name opens their own page.",
                        "«عائلتي» تسرد أبناءك وشعبهم ونتائجهم المعلَنة. و«كشف حسابي» ما استُحقّ وما دُفع. و«الإعلانات» ما أرسلته المدرسة. والأزرار في الأعلى تنقلك بينها، واسم ابنك يفتح صفحته الخاصة."),

                    I(arabic,
                        "The top bar",
                        "الشريط العلوي",
                        "The language button switches the whole interface, its direction with it, and leaves you on the page you were reading. Your name opens change-password and sign-out.",
                        "زر اللغة يبدّل الواجهة كلها، واتجاهها معها، ويتركك في الصفحة التي كنت تقرؤها. واسمك يفتح تغيير كلمة المرور وتسجيل الخروج."),
                }),

            new GuideSection(
                "portal-visibility",
                "bi-eye",
                T(arabic, "What appears here, and what does not", "ما يظهر هنا وما لا يظهر"),
                T(arabic,
                    "The portal shows finished work only. That is a rule, not an omission, and it explains most of what a parent expects to find and does not.",
                    "البوابة تعرض العمل المنتهي وحده. وتلك قاعدة لا إغفال، وهي تفسّر معظم ما يتوقّع وليّ الأمر أن يجده فلا يجده."),
                new[]
                {
                    I(arabic,
                        "Published only",
                        "المُعلَن فقط",
                        "A result the school has not yet published, and an invoice not yet issued, do not appear here (BR-SEC-012) — not because they are hidden from you, but because they are not final. A mark you expected to see is a question for the school office, not a fault in the portal.",
                        "النتيجة التي لم تُعلنها المدرسة بعد، والفاتورة التي لم تُصدَر، لا تظهران هنا (BR-SEC-012) — لا إخفاءً عنك بل لأنهما غير نهائيتين. والدرجة التي توقّعت رؤيتها سؤالٌ لمكتب المدرسة لا خلل في البوابة."),

                    I(arabic,
                        "The staff screens are not yours",
                        "شاشات الموظفين ليست لك",
                        "A staff address opened from this account answers not-found (BR-SEC-010). That is the design rather than a broken link — this account reaches the portal, and the portal is complete in itself.",
                        "عنوان من شاشات الموظفين يُفتَح بهذا الحساب يُجيب «غير موجود» (BR-SEC-010). وهذا هو التصميم لا رابط معطّل — فهذا الحساب يصل إلى البوابة، والبوابة تامّة بذاتها."),
                }),

            new GuideSection(
                "portal-statement",
                "bi-receipt",
                T(arabic, "Reading the statement", "قراءة كشف الحساب"),
                T(arabic,
                    "One page carrying the whole financial history of the family, oldest first.",
                    "صفحة واحدة تحمل تاريخ الأسرة المالي كله، أقدمه أولاً."),
                new[]
                {
                    I(arabic,
                        "The number at the end of a line is the balance",
                        "الرقم في آخر السطر هو الرصيد",
                        "Every charge and every payment appears in order with a running balance beside it, so the figure at the end of a line is what remained at that moment rather than the line's own total. The last line is what is owed today.",
                        "تظهر كل مطالبة وكل دفعة بالترتيب ومعها رصيد متحرّك، فالرقم في آخر السطر هو ما بقي في تلك اللحظة لا مجموع السطر نفسه. والسطر الأخير هو المستحقّ اليوم."),

                    I(arabic,
                        "It runs across years",
                        "يقرأ عبر الأعوام",
                        "The statement is not reset by a new academic year, so a balance carried from last year sits on the same page as this term's charges. Amounts keep Latin digits and right alignment in both languages, so they read the same either way.",
                        "لا يُصفَّر الكشف بعام دراسي جديد، فالرصيد المرحَّل من العام الماضي يقع في الصفحة نفسها مع مطالبات هذا الفصل. والمبالغ تحتفظ بالأرقام اللاتينية وبالمحاذاة إلى اليمين في اللغتين، فتُقرأ كما هي في الحالين."),
                }),

            new GuideSection(
                "portal-session",
                "bi-clock-history",
                T(arabic, "Your session", "جلستك"),
                T(arabic,
                    "The portal carries school records about children, so it does not stay open unattended.",
                    "البوابة تحمل سجلات مدرسية عن أبناء، فلا تبقى مفتوحة بلا رقيب."),
                new[]
                {
                    I(arabic,
                        "Signed out after fifteen idle minutes",
                        "خروج بعد خمس عشرة دقيقة من السكون",
                        "A window warns you two minutes ahead with a \"stay signed in\" button (BR-SEC-013). If it does close on you, sign in again — nothing you were reading is lost, because the portal displays rather than edits.",
                        "تنبّهك نافذة قبل دقيقتين وفيها زر «البقاء متصلاً» (BR-SEC-013). فإن أُغلقت الجلسة عليك فأعد الدخول — ولا شيء مما كنت تقرؤه يضيع، لأن البوابة تعرض ولا تحرّر."),
                }),
        };
    }
}
