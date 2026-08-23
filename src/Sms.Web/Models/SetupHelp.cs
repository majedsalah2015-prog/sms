using System.Collections.Generic;
using Sms.Application.Setup;

namespace Sms.Web.Models
{
    /// <summary>
    /// doc/Modules/01 §8.1 — what each setup screen and each wizard step is actually for.
    /// <para>
    /// The wizard is the one screen whose reader has never seen the product before: it is the first
    /// thing a new school opens, and every field on it decides something the school will live with
    /// for years. A one-line hint under a label cannot say that a currency is what every amount in
    /// the system will be stored in, or that a country pack quietly sets the VAT rate, the accepted
    /// ID types and the retention period at once.
    /// </para>
    /// <para>
    /// Kept out of the views because the same panel is opened from several of them, because it is
    /// prose that a reviewer should be able to read without parsing Razor, and because a step whose
    /// help is missing is then a compile-time hole rather than a blank modal.
    /// </para>
    /// </summary>
    public static class SetupHelp
    {
        private static string T(bool arabic, string en, string ar) => arabic ? ar : en;

        /// <summary>The wizard's own overview — the index screen.</summary>
        public static HelpPanelViewModel Wizard(bool arabic) => new()
        {
            Id = "help-setup-wizard",
            Title = T(arabic, "The setup wizard", "معالج الإعداد"),
            Intro = T(arabic,
                "Nine steps a school completes before its first academic year can be activated (BR-SET-003). Nothing here is one-way: every step can be revisited until you declare setup complete, and even then the settings hub keeps editing them.",
                "تسع خطوات تكملها المدرسة قبل أن يمكن تفعيل أول عام دراسي (BR-SET-003). ولا شيء هنا نهائي: كل خطوة يمكن العودة إليها حتى تعلن اكتمال الإعداد، وبعدها يبقى مركز الإعدادات يعدّلها."),
            Items = new List<HelpPanelViewModel.Item>
            {
                new(T(arabic, "The order is a suggestion, not a lock", "الترتيب اقتراح لا قيد"),
                    T(arabic,
                        "Saving a step carries you to the one after it, because that is what a wizard is for. But the list on the right jumps anywhere, and a step already green can be opened and changed again.",
                        "حفظ الخطوة ينقلك إلى التي تليها، فهذا عمل المعالج. لكن القائمة على الجانب تنقلك إلى أي خطوة، والخطوة المكتملة يمكن فتحها وتغييرها من جديد.")),

                new(T(arabic, "What \"complete\" costs you", "ماذا يعني «اكتمل»"),
                    T(arabic,
                        "Declaring setup complete needs every mandatory step green. It unlocks activating the first academic year; it does not freeze anything. After go-live, changes go through the settings hub and are audited — except the country pack, which needs product-support permission (BR-SET-004).",
                        "إعلان اكتمال الإعداد يحتاج أن تكون كل خطوة إلزامية خضراء. وهو يفتح تفعيل أول عام دراسي، ولا يُجمّد شيئاً. وبعد التشغيل تمرّ التغييرات عبر مركز الإعدادات وتُدقَّق — عدا حزمة الدولة، فتحتاج صلاحية دعم المنتج (BR-SET-004).")),

                new(T(arabic, "Both languages, every time", "اللغتان في كل مرة"),
                    T(arabic,
                        "Arabic and English fields sit side by side because both are required (BR-GLB-001). A name saved in one language shows as blank to half the school.",
                        "حقول العربية والإنجليزية متجاورة لأن كليهما مطلوب (BR-GLB-001). والاسم المحفوظ بلغة واحدة يظهر فارغاً لنصف المدرسة.")),
            },
        };

        /// <summary>Per-step help. Every step in <see cref="SetupWizardSteps"/> has an entry.</summary>
        public static HelpPanelViewModel ForStep(string stepCode, bool arabic)
        {
            var (title, intro, items) = Content(stepCode, arabic);
            return new HelpPanelViewModel
            {
                Id = "help-setup-step",
                Title = title,
                Intro = intro,
                Items = items,
            };
        }

        private static (string Title, string? Intro, IReadOnlyList<HelpPanelViewModel.Item> Items) Content(string stepCode, bool arabic) => stepCode switch
        {
            SetupWizardSteps.Profile => (
                T(arabic, "School profile", "ملف المدرسة"),
                T(arabic,
                    "The school's identity as it will appear on every certificate, receipt and official letter the product prints.",
                    "هوية المدرسة كما ستظهر على كل شهادة وإيصال وخطاب رسمي يطبعه النظام."),
                new HelpPanelViewModel.Item[]
                {
                    new(T(arabic, "Why the name is asked twice", "لماذا يُطلب الاسم مرتين"),
                        T(arabic, "An Arabic certificate and an English transcript are both issued by this school. Neither name is derived from the other.", "الشهادة العربية وكشف الدرجات الإنجليزي كلاهما يصدر عن هذه المدرسة. ولا يُشتق أي اسم من الآخر.")),
                    new(T(arabic, "License and ministry code", "الترخيص والرمز الوزاري"),
                        T(arabic, "Required: they identify the school to the ministry and appear on statutory reports. Changing either later asks for an audit reason (BR-SCH-002).", "مطلوبان: بهما تُعرَف المدرسة لدى الوزارة، ويظهران في التقارير النظامية. وتغيير أيٍّ منهما لاحقاً يطلب سبباً للتدقيق (BR-SCH-002).")),
                    new(T(arabic, "The rest is contact detail", "والبقية بيانات اتصال"),
                        T(arabic, "Address, phone, email and website are optional here and editable at any time from the settings hub.", "العنوان والهاتف والبريد والموقع اختيارية هنا، وقابلة للتعديل في أي وقت من مركز الإعدادات.")),
                }),

            SetupWizardSteps.CountryPack => (
                T(arabic, "Country pack", "حزمة الدولة"),
                T(arabic,
                    "One choice that sets several defaults at once, so a school in a new country is configuration rather than code (BR-SET-004).",
                    "اختيار واحد يضبط عدة افتراضيات معاً، فتكون المدرسة في بلد جديد إعداداً لا برمجة (BR-SET-004)."),
                new HelpPanelViewModel.Item[]
                {
                    new(T(arabic, "What the pack binds", "ما الذي تربطه الحزمة"),
                        T(arabic, "The VAT default, which ID types are accepted and required (BR-GLB-003), whether Hijri dates show by default, the retention periods an audit keeps to (BR-AUD-006), and the statutory report set.", "افتراضي ضريبة القيمة المضافة، وأنواع الهوية المقبولة والمطلوبة (BR-GLB-003)، وهل تُعرض التواريخ الهجرية افتراضياً، ومُدد الاحتفاظ التي يلتزم بها التدقيق (BR-AUD-006)، ومجموعة التقارير النظامية.")),
                    new(T(arabic, "Defaults, not locks", "افتراضيات لا أقفال"),
                        T(arabic, "Everything the pack sets can be overridden afterwards from the settings hub. The pack decides where you start, not where you stay.", "كل ما تضبطه الحزمة يمكن تجاوزه بعدها من مركز الإعدادات. الحزمة تقرّر نقطة البداية لا نقطة الاستقرار.")),
                    new(T(arabic, "Changing it after go-live", "تغييرها بعد التشغيل"),
                        T(arabic, "Needs product-support permission and is audited at tier 1, because live data was already recorded under the old pack's rules.", "يحتاج صلاحية دعم المنتج ويُدقَّق من الفئة الأولى، لأن بيانات فعلية سُجِّلت بالفعل تحت قواعد الحزمة القديمة.")),
                }),

            SetupWizardSteps.Currency => (
                T(arabic, "Currency", "العملة"),
                T(arabic,
                    "The currency every amount in the product is stored and reported in — fees, salaries, purchase orders, the general ledger.",
                    "العملة التي يُخزَّن ويُعرض بها كل مبلغ في النظام — الرسوم والرواتب وأوامر الشراء والأستاذ العام."),
                new HelpPanelViewModel.Item[]
                {
                    new(T(arabic, "From the ISO list only", "من قائمة ISO فقط"),
                        T(arabic, "Free-typed currency codes are refused (BR-GLB-112). The list is a product-seeded lookup.", "رموز العملات المكتوبة يدوياً مرفوضة (BR-GLB-112). والقائمة مرجعية مزوَّدة من المنتج.")),
                    new(T(arabic, "Pick it before money moves", "اخترها قبل أن تتحرك الأموال"),
                        T(arabic, "Amounts are not converted when this changes — they are reinterpreted. Once fees are charged or a receipt is posted, changing the currency rewrites the meaning of every stored figure.", "المبالغ لا تُحوَّل عند تغييرها — بل يُعاد تفسيرها. وبمجرد فرض رسوم أو ترحيل سند قبض، يعيد تغيير العملة معنى كل رقم مخزَّن.")),
                    new(T(arabic, "Display stays LTR", "العرض يبقى من اليسار"),
                        T(arabic, "Money is shown with Western digits and right-aligned in both languages, so a column of figures reads as a column in either direction.", "تُعرض الأموال بالأرقام اللاتينية ومحاذاة لليمين في اللغتين، فيبقى عمود الأرقام عموداً في الاتجاهين.")),
                }),

            SetupWizardSteps.TimeZone => (
                T(arabic, "Time zone", "المنطقة الزمنية"),
                T(arabic,
                    "Every timestamp is stored in UTC and shown in this zone. It decides when \"today\" starts for attendance, for a late fee, and for a scheduled job.",
                    "كل طابع زمني يُخزَّن بتوقيت UTC ويُعرض بهذه المنطقة. وهي التي تقرّر متى يبدأ «اليوم» للحضور، ولغرامة التأخير، وللمهام المجدولة."),
                new HelpPanelViewModel.Item[]
                {
                    new(T(arabic, "Why it matters more than it looks", "لماذا هي أهم مما تبدو"),
                        T(arabic, "An attendance record taken at 07:30 and a cut-off at 08:00 are the same day only if the server and the school agree on which day it is.", "سجل حضور بالساعة 7:30 وحدّ نهائي بالساعة 8:00 يقعان في اليوم نفسه فقط إن اتفق الخادم والمدرسة على أيّ يوم هو.")),
                    new(T(arabic, "Changing it later", "تغييرها لاحقاً"),
                        T(arabic, "Recorded timestamps do not move — they were UTC all along. Only their display shifts.", "الطوابع المسجَّلة لا تتحرك — فقد كانت UTC منذ البداية. ما يتغير هو عرضها فقط.")),
                }),

            SetupWizardSteps.WorkingWeek => (
                T(arabic, "Working week", "أسبوع العمل"),
                T(arabic,
                    "Which days the school works. The academic calendar, the timetable and attendance all read this: a day outside it is a weekend, and nobody is marked absent on it.",
                    "أيام عمل المدرسة. التقويم الأكاديمي والجدول الدراسي والحضور كلها تقرأ هذا: واليوم خارجه عطلة، ولا يُسجَّل فيه غياب على أحد."),
                new HelpPanelViewModel.Item[]
                {
                    new(T(arabic, "At least four days", "أربعة أيام على الأقل"),
                        T(arabic, "Fewer is refused (doc §9). It is not a policy judgement — a week with three teaching days breaks the timetable generator and the attendance percentages built on it.", "أقلّ من ذلك مرفوض (الوثيقة §9). وليست مسألة سياسة — فأسبوع بثلاثة أيام تدريس يكسر مولّد الجدول ونسب الحضور المبنية عليه.")),
                    new(T(arabic, "First day of the week", "أول أيام الأسبوع"),
                        T(arabic, "Decides where a calendar grid starts. Saturday and Sunday are both common and neither is assumed.", "يقرّر من أين تبدأ شبكة التقويم. السبت والأحد كلاهما شائع ولا يُفترض أحدهما.")),
                    new(T(arabic, "It can differ per year", "قد يختلف من عام لآخر"),
                        T(arabic, "This setting is year-versionable (BR-SET-005): a change applies to the year you set it for, and past records keep displaying the week that was in force at their date.", "هذا الإعداد قابل للإصدار السنوي (BR-SET-005): التغيير يسري على العام الذي ضبطته له، والسجلات السابقة تبقى تعرض الأسبوع الذي كان سارياً في تاريخها.")),
                }),

            SetupWizardSteps.Languages => (
                T(arabic, "Languages", "اللغات"),
                T(arabic,
                    "Which languages the interface offers, and which one a user who has expressed no preference sees first.",
                    "اللغات التي تعرضها الواجهة، وأيّها يرى المستخدم الذي لم يُبدِ تفضيلاً."),
                new HelpPanelViewModel.Item[]
                {
                    new(T(arabic, "Turning one off changes nothing in the data", "إيقاف لغة لا يغيّر شيئاً في البيانات"),
                        T(arabic, "Both names are still stored on every record (BR-GLB-001). This decides what the interface offers, not what is kept.", "الاسمان يبقيان مخزَّنين على كل سجل (BR-GLB-001). وهذا يقرّر ما تعرضه الواجهة لا ما يُحفظ.")),
                    new(T(arabic, "Arabic brings RTL with it", "العربية تجلب معها الاتجاه"),
                        T(arabic, "Choosing Arabic flips the whole layout right-to-left. It does not switch the calendar to Hijri — that is the next step, and deliberately separate.", "اختيار العربية يقلب التخطيط كله من اليمين لليسار. ولا يبدّل التقويم إلى الهجري — فذاك الخطوة التالية، ومنفصل عن قصد.")),
                }),

            SetupWizardSteps.CalendarType => (
                T(arabic, "Calendar type", "نوع التقويم"),
                T(arabic,
                    "Which calendar the school reads dates in. Dates are entered and stored as Gregorian either way; this decides what is displayed beside them.",
                    "بأيّ تقويم تقرأ المدرسة التواريخ. التواريخ تُدخَل وتُخزَّن ميلادية في الحالتين؛ وهذا يقرّر ما يُعرض بجانبها."),
                new HelpPanelViewModel.Item[]
                {
                    new(T(arabic, "\"Both\" is the usual answer", "«كلاهما» هو الجواب المعتاد"),
                        T(arabic, "Gregorian for entry and for anything the ministry receives, with the Hijri date shown alongside for the reader who thinks in it.", "الميلادي للإدخال ولكل ما يصل الوزارة، مع عرض التاريخ الهجري بجانبه لمن يفكّر به.")),
                    new(T(arabic, "The language never switches the calendar", "اللغة لا تبدّل التقويم أبداً"),
                        T(arabic, "Reading the screen in Arabic does not make the dates Hijri. That behaviour was removed on purpose — a date that changes meaning when you switch language is a date nobody can rely on.", "قراءة الشاشة بالعربية لا تجعل التواريخ هجرية. وقد أُزيل ذلك السلوك عمداً — فالتاريخ الذي يتغير معناه بتبديل اللغة تاريخ لا يُعوَّل عليه.")),
                }),

            SetupWizardSteps.NumberingSeries => (
                T(arabic, "Numbering series", "سلاسل الترقيم"),
                T(arabic,
                    "The formats behind every document number the school issues — student numbers, receipts, invoices, certificates. This step confirms the catalogue is present; it does not create it.",
                    "الصيغ التي تقف خلف كل رقم مستند تصدره المدرسة — أرقام الطلاب والإيصالات والفواتير والشهادات. وهذه الخطوة تتأكد من وجود الكتالوج، ولا تُنشئه."),
                new HelpPanelViewModel.Item[]
                {
                    new(T(arabic, "Why there is nothing to fill in", "لماذا لا يوجد ما يُملأ"),
                        T(arabic, "The catalogue ships with the product (doc 08 §4) so that two schools issue receipt numbers the same way. If the table below is empty, the product seeder has not run.", "الكتالوج يأتي مع المنتج (الوثيقة 08 §4) لتُصدِر مدرستان أرقام إيصالات بالطريقة نفسها. وإن كان الجدول أدناه فارغاً فبيانات المنتج لم تُبذَر.")),
                    new(T(arabic, "Where formats are actually changed", "أين تُغيَّر الصيغ فعلاً"),
                        T(arabic, "In the numbering registry, not here: a format change mid-year is a cutover with a new sequence, not an edit, because numbers already issued must stay valid (BR-NUM-005).", "في سجل الترقيم لا هنا: فتغيير الصيغة في منتصف العام تحويلٌ بتسلسل جديد لا تعديل، لأن الأرقام الصادرة يجب أن تبقى صحيحة (BR-NUM-005).")),
                }),

            SetupWizardSteps.StageStructure => (
                T(arabic, "Stage structure", "الهيكل الأكاديمي"),
                T(arabic,
                    "The school's ladder: stages, and the grades inside each one. Everything academic hangs off this — sections, timetables, fee structures and promotion all name a grade.",
                    "سلّم المدرسة: المراحل، والصفوف داخل كل مرحلة. وكل ما هو أكاديمي معلَّق عليه — الشُّعب والجداول وهياكل الرسوم والترفيع كلها تسمّي صفاً."),
                new HelpPanelViewModel.Item[]
                {
                    new(T(arabic, "Enter the whole ladder before moving on", "أدخل السلّم كاملاً قبل المتابعة"),
                        T(arabic, "Use \"Save and add another\" for each stage and each grade — it saves the row and leaves you here. \"Save and continue\" is for when the ladder is finished.", "استخدم «حفظ وإضافة آخر» لكل مرحلة وكل صف — فهو يحفظ الصف ويُبقيك هنا. و«حفظ ومتابعة» لحين اكتمال السلّم.")),
                    new(T(arabic, "Rows are editable in place", "الصفوف قابلة للتعديل في مكانها"),
                        T(arabic, "A stage or grade already entered can be renamed, reordered, or moved to another stage from its own row. A typo does not have to survive the year.", "المرحلة أو الصف المُدخَل يمكن إعادة تسميته أو ترتيبه أو نقله إلى مرحلة أخرى من صفّه نفسه. والخطأ المطبعي لا يجب أن يعيش العام كله.")),
                    new(T(arabic, "The code is the identity", "الرمز هو الهوية"),
                        T(arabic, "A grade's code (G1, G2…) is what timetables and fee structures point at. Keep it short and keep it stable.", "رمز الصف (G1، G2…) هو ما تشير إليه الجداول وهياكل الرسوم. اجعله قصيراً واجعله ثابتاً.")),
                    new(T(arabic, "What belongs to Module 05 instead", "وما ينتمي إلى الوحدة 05 بدلاً من هنا"),
                        T(arabic, "Promotion targets, graduating flags and the per-year grade profiles are the grades module's job. This step only needs the ladder to exist.", "أهداف الترفيع وأعلام التخرّج وملفات الصف السنوية من عمل وحدة الصفوف. وهذه الخطوة تحتاج فقط أن يوجد السلّم.")),
                }),

            _ => (
                T(arabic, "This step", "هذه الخطوة"),
                null,
                new HelpPanelViewModel.Item[]
                {
                    new(T(arabic, "No guidance written yet", "لم تُكتب إرشادات بعد"),
                        T(arabic, "Add it to SetupHelp so the next administrator does not have to guess.", "أضِفها إلى SetupHelp حتى لا يضطر المسؤول التالي إلى التخمين.")),
                }),
        };

        /// <summary>Lookup lists — the two tiers and why one of them is read-only.</summary>
        public static HelpPanelViewModel Lookups(bool arabic) => new()
        {
            Id = "help-setup-lookups",
            Title = T(arabic, "Lookup lists", "القوائم المرجعية"),
            Intro = T(arabic,
                "Every drop-down in the product reads one of these lists. They come in two tiers, and which tier a list belongs to decides whether the school may change it (BR-SET-001).",
                "كل قائمة منسدلة في النظام تقرأ إحدى هذه القوائم. وهي مستويان، والمستوى هو ما يقرّر هل للمدرسة أن تغيّرها (BR-SET-001)."),
            Items = new List<HelpPanelViewModel.Item>
            {
                new(T(arabic, "Product-seeded lists", "القوائم المزوَّدة من المنتج"),
                    T(arabic, "Nationalities, ISO currencies, blood types, ID types, relationship types. Shared meaning across every school, so product releases own them — the grid shows them read-only. Nationalities are the exception: schools legitimately extend and correct that one, and it has its own editor.", "الجنسيات وعملات ISO وفصائل الدم وأنواع الهوية وصلات القرابة. معناها مشترك بين كل المدارس، فتملكها إصدارات المنتج — ويعرضها الجدول للقراءة فقط. والجنسيات استثناء: المدارس توسّعها وتصحّحها بحق، ولها محرّرها الخاص.")),

                new(T(arabic, "School-managed lists", "قوائم تديرها المدرسة"),
                    T(arabic, "Housing types, referral sources, your own tags. Add a category, add values, correct them, retire them — this half of the screen is yours.", "أنواع السكن ومصادر الإحالة ووسومكم الخاصة. أضف فئة، وأضف قيماً، وصحّحها، وأحِلها للتقاعد — هذا النصف من الشاشة لكم.")),

                new(T(arabic, "Nothing is ever deleted", "لا شيء يُحذف أبداً"),
                    T(arabic, "A value is deactivated, which takes it out of the pickers and leaves it readable on every record that already points at it (BR-SET-002, BR-GLB-006). Retiring the wrong one is reversible — reactivate it from the same row.", "القيمة تُعطَّل، فتخرج من قوائم الاختيار وتبقى مقروءة على كل سجل يشير إليها (BR-SET-002، BR-GLB-006). وتقاعد القيمة الخطأ قابل للتراجع — أعِد تفعيلها من الصف نفسه.")),

                new(T(arabic, "Sort order is what users see", "الترتيب هو ما يراه المستخدمون"),
                    T(arabic, "Values appear in a picker in sort order, then by code. Put the common answers at the top and a hundred data-entry seconds a day disappear.", "تظهر القيم في القائمة حسب الترتيب ثم الرمز. ضع الإجابات الشائعة في الأعلى فتختفي مئة ثانية إدخال يومياً.")),
            },
        };

        /// <summary>The settings hub.</summary>
        public static HelpPanelViewModel Settings(bool arabic) => new()
        {
            Id = "help-setup-settings",
            Title = T(arabic, "System settings", "إعدادات النظام"),
            Intro = T(arabic,
                "The same values the wizard collected, plus everything it did not ask about — grouped by what they affect rather than by when you first met them.",
                "القيم نفسها التي جمعها المعالج، ومعها كل ما لم يسأل عنه — مجمَّعة حسب ما تؤثر فيه لا حسب متى قابلتها أول مرة."),
            Items = new List<HelpPanelViewModel.Item>
            {
                new(T(arabic, "Each key validates on save", "كل مفتاح يُتحقَّق منه عند الحفظ"),
                    T(arabic, "A refused value names what it would have accepted. The validation is the same code the wizard runs — there is no second, laxer path in through this screen.", "القيمة المرفوضة تُسمّي ما كان سيُقبل. والتحقّق هو الكود نفسه الذي يشغّله المعالج — فلا يوجد مسار ثانٍ أكثر تساهلاً من هذه الشاشة.")),

                new(T(arabic, "Some settings belong to a year", "بعض الإعدادات تخصّ عاماً"),
                    T(arabic, "Working week, VAT rate and thresholds are effective-dated per academic year (BR-SET-005). Set one against a year and last year's records keep showing last year's value — which is the only way a historical receipt can still be explained.", "أسبوع العمل ونسبة الضريبة والحدود مؤرَّخة السريان لكل عام دراسي (BR-SET-005). اضبط واحداً على عام وستبقى سجلات العام الماضي تعرض قيمة العام الماضي — وهي الطريقة الوحيدة لتفسير إيصال قديم.")),

                new(T(arabic, "Every change is audited", "كل تغيير مدقَّق"),
                    T(arabic, "Settings are tier-1 audited (BR-SET-007): the old value, the new one, who and when. Financial keys also notify the principal and the finance manager.", "الإعدادات مدقَّقة من الفئة الأولى (BR-SET-007): القيمة القديمة والجديدة ومن ومتى. والمفاتيح المالية تُشعِر المدير والمدير المالي كذلك.")),
            },
        };

        /// <summary>Feature toggles.</summary>
        public static HelpPanelViewModel Features(bool arabic) => new()
        {
            Id = "help-setup-features",
            Title = T(arabic, "Feature toggles", "مفاتيح الميزات"),
            Intro = T(arabic,
                "Which modules this school actually uses. A school with no buses should not be reading a transport menu (BR-SET-006).",
                "أي الوحدات تستخدمها هذه المدرسة فعلاً. فالمدرسة بلا حافلات لا ينبغي أن تقرأ قائمة نقل (BR-SET-006)."),
            Items = new List<HelpPanelViewModel.Item>
            {
                new(T(arabic, "Off means invisible, not deleted", "الإيقاف يعني الإخفاء لا الحذف"),
                    T(arabic, "Turning a module off hides it from the menus and from the permission catalogue. Its data stays exactly where it was and comes back untouched when you turn it on again.", "إيقاف الوحدة يخفيها من القوائم ومن كتالوج الصلاحيات. وبياناتها تبقى كما هي وتعود سليمة عند إعادة تشغيلها.")),

                new(T(arabic, "Some features need others", "بعض الميزات تحتاج غيرها"),
                    T(arabic, "Transport fees have nothing to attach to without transport. The screen warns rather than silently producing a module that half-works.", "رسوم النقل لا شيء تتعلق به بلا نقل. والشاشة تُحذّر بدل أن تُنتج بصمت وحدةً تعمل نصف عمل.")),
            },
        };

        /// <summary>Country pack viewer.</summary>
        public static HelpPanelViewModel Pack(bool arabic) => new()
        {
            Id = "help-setup-pack",
            Title = T(arabic, "Country pack", "حزمة الدولة"),
            Intro = T(arabic,
                "What the bound pack actually contains, so the defaults it set can be read rather than guessed at.",
                "ما تحتويه الحزمة المرتبطة فعلاً، فتُقرأ الافتراضيات التي ضبطتها بدل تخمينها."),
            Items = new List<HelpPanelViewModel.Item>
            {
                new(T(arabic, "Bound once, read often", "تُربط مرة وتُقرأ كثيراً"),
                    T(arabic, "The pack was chosen in the wizard. This screen shows what that choice implied — VAT, ID types, Hijri display, retention.", "اختيرت الحزمة في المعالج. وهذه الشاشة تعرض ما الذي عناه ذلك الاختيار — الضريبة وأنواع الهوية وعرض الهجري والاحتفاظ.")),

                new(T(arabic, "Changing it is support-gated", "تغييرها محكوم بالدعم"),
                    T(arabic, "After go-live it needs product-support permission and is tier-1 audited (BR-SET-004), because live records were written under the previous pack's rules.", "بعد التشغيل يحتاج صلاحية دعم المنتج ويُدقَّق من الفئة الأولى (BR-SET-004)، لأن سجلات فعلية كُتبت تحت قواعد الحزمة السابقة.")),
            },
        };
    }
}
