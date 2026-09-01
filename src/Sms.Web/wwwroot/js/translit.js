// Arabic → English filling of the second half of a bilingual pair.
//
// Two jobs, because a person and a thing need opposite treatments. A person's
// name is transliterated — "خالد" is Khalid and never anything else. A thing's
// name is translated — "الصف الثالث" is Grade 3, and transliterating it to
// "Alsaf Althalth" would put a string in the English column that no English
// reader can use and no report can group by.
//
// Usage: <input data-translit-from="#FirstNameAr" /> on an English field,
// adding data-translit-mode="term" where the field names a thing rather than
// a person.
// While the source Arabic field is typed into, the English field is filled
// automatically; as soon as a human edits the English field itself the
// auto-fill stops for that field (data-translit-manual="1"). Rule-based with
// a dictionary of common names — it is a starting point, not an authority;
// the user can always correct the result before submitting.
(function () {
    var DICT = {
        'محمد': 'Mohammed', 'أحمد': 'Ahmed', 'احمد': 'Ahmed', 'محمود': 'Mahmoud', 'مصطفى': 'Mustafa',
        'علي': 'Ali', 'عمر': 'Omar', 'عثمان': 'Othman', 'خالد': 'Khalid', 'عبدالله': 'Abdullah', 'عبد': 'Abd',
        'الله': 'Allah', 'عبدالرحمن': 'Abdulrahman', 'عبدالعزيز': 'Abdulaziz', 'عبدالكريم': 'Abdulkarim',
        'عبدالمجيد': 'Abdulmajeed', 'عبداللطيف': 'Abdullatif', 'عبدالحميد': 'Abdulhamid', 'عبدالرحيم': 'Abdulrahim',
        'الرحمن': 'Alrahman', 'العزيز': 'Alaziz', 'الكريم': 'Alkarim',
        'حسن': 'Hassan', 'حسين': 'Hussein', 'الحسن': 'Alhassan', 'الحسين': 'Alhussein', 'حمزة': 'Hamza',
        'يوسف': 'Yousef', 'إبراهيم': 'Ibrahim', 'ابراهيم': 'Ibrahim', 'إسماعيل': 'Ismail', 'اسماعيل': 'Ismail',
        'إسحاق': 'Ishaq', 'يعقوب': 'Yaqoub', 'موسى': 'Musa', 'عيسى': 'Issa', 'داود': 'Dawood', 'داوود': 'Dawood',
        'سليمان': 'Suleiman', 'هارون': 'Haroun', 'زكريا': 'Zakaria', 'يحيى': 'Yahya', 'يونس': 'Younis',
        'أيوب': 'Ayoub', 'آدم': 'Adam', 'نوح': 'Noah', 'إلياس': 'Elias', 'الياس': 'Elias', 'إدريس': 'Idris',
        'سعيد': 'Saeed', 'سالم': 'Salem', 'ناصر': 'Nasser', 'فهد': 'Fahad', 'سلطان': 'Sultan', 'سعود': 'Saud',
        'بدر': 'Badr', 'فيصل': 'Faisal', 'تركي': 'Turki', 'نايف': 'Naif', 'منصور': 'Mansour', 'ماجد': 'Majed',
        'راشد': 'Rashid', 'جاسم': 'Jassim', 'حمد': 'Hamad', 'حمود': 'Hamoud', 'مشعل': 'Mishal', 'مشاري': 'Mishari',
        'طارق': 'Tariq', 'عادل': 'Adel', 'عمار': 'Ammar', 'أنس': 'Anas', 'بلال': 'Bilal', 'زياد': 'Ziad',
        'أيمن': 'Ayman', 'كريم': 'Karim', 'رامي': 'Rami', 'سامي': 'Sami', 'هاني': 'Hani', 'وليد': 'Waleed',
        'ياسر': 'Yasser', 'نادر': 'Nader', 'جمال': 'Jamal', 'كمال': 'Kamal', 'صالح': 'Saleh', 'معاذ': 'Muath',
        'لؤي': 'Louay', 'ريان': 'Rayan', 'سمير': 'Samir', 'أمير': 'Amir', 'باسم': 'Bassem', 'حاتم': 'Hatem',
        'فارس': 'Faris', 'غسان': 'Ghassan', 'مازن': 'Mazen', 'مروان': 'Marwan', 'نبيل': 'Nabil', 'هشام': 'Hisham',
        'وائل': 'Wael', 'أسامة': 'Osama', 'اسامة': 'Osama', 'طلال': 'Talal', 'ثامر': 'Thamer', 'عبيد': 'Obaid',
        'عايض': 'Ayedh', 'سلمان': 'Salman', 'صلاح': 'Salah', 'رائد': 'Raed', 'زيد': 'Zaid', 'بندر': 'Bandar',
        'فاطمة': 'Fatima', 'عائشة': 'Aisha', 'مريم': 'Maryam', 'سارة': 'Sarah', 'ساره': 'Sarah', 'نور': 'Noor',
        'نورة': 'Noura', 'نوره': 'Noura', 'زينب': 'Zainab', 'خديجة': 'Khadija', 'ليلى': 'Layla', 'هدى': 'Huda',
        'أمل': 'Amal', 'ريم': 'Reem', 'منى': 'Mona', 'هند': 'Hind', 'سلمى': 'Salma', 'لمى': 'Lama', 'لما': 'Lama',
        'دانة': 'Dana', 'دانه': 'Dana', 'جود': 'Joud', 'جوري': 'Joury', 'غادة': 'Ghada', 'رنا': 'Rana',
        'رهف': 'Rahaf', 'شهد': 'Shahad', 'لين': 'Leen', 'لينا': 'Lina', 'أسماء': 'Asma', 'اسماء': 'Asma',
        'آمنة': 'Amna', 'امنة': 'Amna', 'حصة': 'Hessa', 'موضي': 'Moudhi', 'الجوهرة': 'Aljawhara', 'العنود': 'Alanoud',
        'البندري': 'Albandari', 'أروى': 'Arwa', 'اروى': 'Arwa', 'بشرى': 'Bushra', 'تهاني': 'Tahani', 'حنان': 'Hanan',
        'رغد': 'Raghad', 'روان': 'Rawan', 'شيماء': 'Shaima', 'عبير': 'Abeer', 'غدير': 'Ghadeer', 'ندى': 'Nada',
        'نجلاء': 'Najla', 'هيا': 'Haya', 'هيفاء': 'Haifa', 'وفاء': 'Wafa', 'يارا': 'Yara', 'إيمان': 'Iman', 'ايمان': 'Iman',
        'أبو': 'Abu', 'ابو': 'Abu', 'بن': 'Bin', 'بنت': 'Bint', 'آل': 'Al', 'ال': 'Al', 'الدين': 'Aldin', 'نور الدين': 'Noureddine'
    };

    // Letter map: [latin, isVowel]
    var MAP = {
        'ا': ['a', true], 'أ': ['a', true], 'إ': ['i', true], 'آ': ['a', true], 'ٱ': ['a', true],
        'ب': ['b', false], 'ت': ['t', false], 'ث': ['th', false], 'ج': ['j', false], 'ح': ['h', false],
        'خ': ['kh', false], 'د': ['d', false], 'ذ': ['th', false], 'ر': ['r', false], 'ز': ['z', false],
        'س': ['s', false], 'ش': ['sh', false], 'ص': ['s', false], 'ض': ['d', false], 'ط': ['t', false],
        'ظ': ['z', false], 'ع': ['a', true], 'غ': ['gh', false], 'ف': ['f', false], 'ق': ['q', false],
        'ك': ['k', false], 'ل': ['l', false], 'م': ['m', false], 'ن': ['n', false], 'ه': ['h', false],
        'و': ['w', false], 'ي': ['y', false], 'ى': ['a', true], 'ة': ['a', true], 'ء': ['', true],
        'ئ': ['e', true], 'ؤ': ['o', true], 'گ': ['g', false], 'پ': ['p', false], 'چ': ['ch', false], 'ڤ': ['v', false]
    };
    var HARAKAT = { 'َ': 'a', 'ُ': 'u', 'ِ': 'i', 'ً': 'an', 'ٌ': 'un', 'ٍ': 'in' };
    var SHADDA = 'ّ', SUKUN = 'ْ', TATWEEL = 'ـ';

    function isVowelChar(c) { return /[aeiou]/.test(c); }

    function word(w) {
        if (!w) { return ''; }
        if (DICT[w]) { return DICT[w]; }
        var prefix = '';
        var core = w;
        if (core.length > 3 && core.indexOf('ال') === 0) { prefix = 'Al'; core = core.slice(2); if (DICT[core]) { return prefix + DICT[core].toLowerCase(); } }

        var out = '';
        var chars = Array.from(core);
        for (var i = 0; i < chars.length; i++) {
            var c = chars[i];
            if (c === TATWEEL || c === SUKUN) { continue; }
            if (c === SHADDA) { if (out.length) { out += out[out.length - 1]; } continue; }
            if (HARAKAT[c]) { out += HARAKAT[c]; continue; }
            var m = MAP[c];
            if (!m) { out += c; continue; }
            var latin = m[0], vowel = m[1];
            var last = out.length ? out[out.length - 1] : '';
            var nextCh = chars[i + 1];
            var nextM = nextCh ? MAP[nextCh] : null;
            var nextIsVowel = nextM ? nextM[1] : (nextCh && HARAKAT[nextCh] ? true : false);
            var atEnd = i === chars.length - 1;
            var atStart = out.length === 0;

            if (c === 'و') {
                // consonant w at word start or before a vowel; otherwise long vowel u
                latin = (atStart || nextIsVowel) ? 'w' : (atEnd ? 'u' : (last && !isVowelChar(last) ? 'ou' : 'w'));
                vowel = latin !== 'w';
            } else if (c === 'ي') {
                latin = (atStart || (nextIsVowel && !atEnd)) ? 'y' : (atEnd ? 'i' : (last && !isVowelChar(last) ? 'i' : 'y'));
                vowel = latin !== 'y';
            } else if (c === 'ة') {
                latin = (last && isVowelChar(last)) ? 'h' : 'a';
            } else if (c === 'ع' && (nextIsVowel || (last && isVowelChar(last)))) {
                latin = ''; // ayn next to a vowel — let the vowel carry it (سعيد → Said)
            } else if (c === 'ا' && atStart) {
                latin = 'a';
            } else if (c === 'ا' && last && isVowelChar(last)) {
                latin = ''; // avoid "aa"
            }

            // Break up consonant clusters: insert 'a' when this consonant follows
            // a consonant that itself follows a consonant (3-cluster), or when
            // two consonants open the word (Arabic words do not start with CC).
            if (!vowel && latin && last && !isVowelChar(last)) {
                var prev2 = out.length > 1 ? out[out.length - 2] : '';
                var prevWasDigraph = /^(kh|sh|th|gh|ch)$/.test(out.slice(-2));
                var prevConsonantStart = prevWasDigraph ? (out.length > 2 ? out[out.length - 3] : '') : prev2;
                var openingCluster = (prevWasDigraph ? out.length === 2 : out.length === 1);
                if (openingCluster || (prevConsonantStart && !isVowelChar(prevConsonantStart)) || (!atEnd && !nextIsVowel)) {
                    out += 'a';
                }
            }
            out += latin;
        }
        if (!out) { return prefix; }
        out = out.charAt(0).toUpperCase() + out.slice(1);
        return prefix ? prefix + out.toLowerCase() : out;
    }

    function transliterate(text) {
        return String(text || '').trim().split(/\s+/).filter(Boolean).map(word).join(' ');
    }

    window.smsTransliterate = transliterate;

    // ---------------------------------------------------------------- terms
    //
    // Structural vocabulary: stages, grades, sections, and the words that
    // surround them. Small and closed on purpose — a school's ladder uses the
    // same two dozen phrases everywhere, so an exact match handles almost every
    // real entry and the patterns below handle the rest.

    var ORDINALS = {
        'الأول': 1, 'الاول': 1, 'أول': 1, 'اول': 1, 'الأولى': 1, 'الاولى': 1, 'أولى': 1, 'اولى': 1, 'الحادي': 1,
        'الثاني': 2, 'ثاني': 2, 'الثانية': 2, 'ثانية': 2,
        'الثالث': 3, 'ثالث': 3, 'الثالثة': 3,
        'الرابع': 4, 'رابع': 4, 'الرابعة': 4,
        'الخامس': 5, 'خامس': 5, 'الخامسة': 5,
        'السادس': 6, 'سادس': 6, 'السادسة': 6,
        'السابع': 7, 'سابع': 7, 'السابعة': 7,
        'الثامن': 8, 'ثامن': 8, 'الثامنة': 8,
        'التاسع': 9, 'تاسع': 9, 'التاسعة': 9,
        'العاشر': 10, 'عاشر': 10, 'العاشرة': 10
    };

    var TERMS = {
        // stages
        'رياض الأطفال': 'Kindergarten', 'رياض الاطفال': 'Kindergarten', 'الروضة': 'Kindergarten', 'روضة': 'Kindergarten',
        'التمهيدي': 'Preschool', 'تمهيدي': 'Preschool',
        'المرحلة الابتدائية': 'Elementary Stage', 'الابتدائية': 'Elementary', 'ابتدائية': 'Elementary', 'ابتدائي': 'Elementary',
        'المرحلة المتوسطة': 'Intermediate Stage', 'المتوسطة': 'Intermediate', 'متوسطة': 'Intermediate', 'متوسط': 'Intermediate',
        'المرحلة الإعدادية': 'Preparatory Stage', 'المرحلة الاعدادية': 'Preparatory Stage',
        'الإعدادية': 'Preparatory', 'الاعدادية': 'Preparatory', 'إعدادية': 'Preparatory', 'اعدادية': 'Preparatory',
        'المرحلة الثانوية': 'Secondary Stage', 'الثانوية': 'Secondary', 'ثانوية': 'Secondary', 'ثانوي': 'Secondary',
        'المرحلة الأساسية': 'Basic Stage', 'المرحلة الاساسية': 'Basic Stage', 'الأساسية': 'Basic', 'الاساسية': 'Basic', 'أساسية': 'Basic', 'اساسية': 'Basic',

        // the words the ladder is built from
        'المرحلة': 'Stage', 'مرحلة': 'Stage', 'الصف': 'Grade', 'صف': 'Grade',
        'الشعبة': 'Section', 'شعبة': 'Section', 'الفصل': 'Class', 'فصل': 'Class',
        'القسم': 'Department', 'قسم': 'Department', 'المسار': 'Track', 'مسار': 'Track',

        // streams a secondary ladder splits into
        'علمي': 'Science', 'العلمي': 'Science', 'أدبي': 'Literary', 'الأدبي': 'Literary', 'ادبي': 'Literary',
        'تجاري': 'Commercial', 'التجاري': 'Commercial', 'صناعي': 'Industrial', 'الصناعي': 'Industrial',
        'شرعي': 'Sharia', 'الشرعي': 'Sharia', 'تقني': 'Technical', 'التقني': 'Technical',

        // qualifiers that show up beside them
        'بنين': 'Boys', 'البنين': 'Boys', 'بنات': 'Girls', 'البنات': 'Girls',
        'مختلط': 'Mixed', 'المختلط': 'Mixed', 'ذكور': 'Boys', 'إناث': 'Girls', 'اناث': 'Girls',
        'صباحي': 'Morning', 'الصباحي': 'Morning', 'مسائي': 'Evening', 'المسائي': 'Evening',

        // ---- the building the ladder is taught in: rooms, floors, wings.
        // The same closed-vocabulary bet as above — a school names its rooms out
        // of about thirty words, and the ordinal rules already turn "الطابق
        // الأول" into "Floor 1" without any of them.
        'المبنى الرئيسي': 'Main Building', 'الطابق الأرضي': 'Ground Floor', 'الطابق الارضي': 'Ground Floor',
        'غرفة المعلمين': 'Teachers Room', 'غرفة المعلمات': 'Teachers Room', 'مختبر الحاسوب': 'Computer Lab',
        'مختبر العلوم': 'Science Lab', 'قاعة الاجتماعات': 'Meeting Hall', 'غرفة المصادر': 'Resource Room',

        'مبنى': 'Building', 'المبنى': 'Building', 'طابق': 'Floor', 'الطابق': 'Floor', 'دور': 'Floor', 'الدور': 'Floor',
        'قاعة': 'Room', 'القاعة': 'Room', 'غرفة': 'Room', 'الغرفة': 'Room', 'صالة': 'Hall', 'الصالة': 'Hall',
        'مختبر': 'Lab', 'المختبر': 'Lab', 'معمل': 'Lab', 'المعمل': 'Lab', 'ورشة': 'Workshop', 'الورشة': 'Workshop',
        'مكتبة': 'Library', 'المكتبة': 'Library', 'مسرح': 'Theatre', 'المسرح': 'Theatre',
        'ملعب': 'Playground', 'الملعب': 'Playground', 'مقصف': 'Cafeteria', 'المقصف': 'Cafeteria',
        'عيادة': 'Clinic', 'العيادة': 'Clinic', 'مصلى': 'Prayer Room', 'المصلى': 'Prayer Room',
        'مستودع': 'Store', 'المستودع': 'Store', 'مخزن': 'Store', 'المخزن': 'Store',
        'إدارة': 'Administration', 'الإدارة': 'Administration', 'ادارة': 'Administration', 'الادارة': 'Administration',
        'مدير': 'Principal', 'المدير': 'Principal', 'سكرتارية': 'Secretariat', 'استقبال': 'Reception', 'الاستقبال': 'Reception',
        'جناح': 'Wing', 'الجناح': 'Wing', 'مدخل': 'Entrance', 'المدخل': 'Entrance', 'ممر': 'Corridor', 'الممر': 'Corridor',
        'مصادر': 'Resources', 'المصادر': 'Resources', 'نشاط': 'Activity', 'النشاط': 'Activity',
        'معلمين': 'Teachers', 'المعلمين': 'Teachers', 'معلمات': 'Teachers', 'المعلمات': 'Teachers',
        'اجتماعات': 'Meetings', 'الاجتماعات': 'Meetings', 'اجتماع': 'Meeting', 'الاجتماع': 'Meeting',
        'حاسوب': 'Computer', 'الحاسوب': 'Computer', 'حاسب': 'Computer', 'الحاسب': 'Computer',
        'علوم': 'Science', 'العلوم': 'Science', 'كيمياء': 'Chemistry', 'الكيمياء': 'Chemistry',
        'فيزياء': 'Physics', 'الفيزياء': 'Physics', 'أحياء': 'Biology', 'الأحياء': 'Biology',
        'لغات': 'Languages', 'اللغات': 'Languages', 'فنون': 'Arts', 'الفنون': 'Arts',
        'رياضة': 'Sports', 'الرياضة': 'Sports', 'موسيقى': 'Music', 'الموسيقى': 'Music',

        // ---- what a school charges for: fee categories, discounts, the money words.
        // Same closed-vocabulary bet again, and the payoff is larger here than on the
        // ladder: a fee category's English name is what a parent reads on an invoice
        // and what the GL export groups by, so "Alrsom Aldrasyh" would be wrong in two
        // places at once. Longest phrases first — the matcher takes the longest hit.
        'الرسوم الدراسية': 'Tuition Fees', 'رسوم دراسية': 'Tuition Fees', 'الرسوم الدراسيه': 'Tuition Fees',
        'رسوم التسجيل': 'Registration Fees', 'رسوم تسجيل': 'Registration Fees',
        'رسوم النقل': 'Transport Fees', 'رسوم المواصلات': 'Transport Fees',
        'الزي المدرسي': 'Uniform', 'زي مدرسي': 'Uniform', 'الزي الموحد': 'Uniform',
        'الكتب المدرسية': 'Textbooks', 'كتب مدرسية': 'Textbooks',
        'القرطاسية': 'Stationery', 'قرطاسية': 'Stationery',
        'الأنشطة اللاصفية': 'Extracurricular Activities', 'الانشطة اللاصفية': 'Extracurricular Activities',
        'الرحلات المدرسية': 'School Trips', 'رحلات مدرسية': 'School Trips',
        'التأمين الصحي': 'Health Insurance', 'التامين الصحي': 'Health Insurance',
        'رسوم الامتحانات': 'Examination Fees', 'رسوم الاختبارات': 'Examination Fees',
        'رسوم الشهادات': 'Certificate Fees', 'بدل فاقد': 'Replacement Charge',
        'غرامة تأخير': 'Late Fee', 'غرامة التأخير': 'Late Fee', 'غرامة ارتداد': 'Bounce Fee',
        'وجبة مدرسية': 'School Meal', 'الوجبات المدرسية': 'School Meals',

        // what a school gives back: discount types and scholarship programmes. The
        // English name here lands on the discount register and the owner's revenue
        // report, so the same argument applies — translate, never transliterate.
        'خصم الإخوة': 'Sibling Discount', 'خصم الاخوة': 'Sibling Discount', 'خصم الأخوة': 'Sibling Discount',
        'خصم أبناء الموظفين': 'Staff Children Discount', 'خصم ابناء الموظفين': 'Staff Children Discount',
        'خصم الموظفين': 'Staff Discount', 'خصم المعلمين': 'Teachers Discount',
        'منحة التفوق': 'Merit Scholarship', 'منحة تفوق': 'Merit Scholarship', 'منحة التفوق الدراسي': 'Academic Merit Scholarship',
        'منحة الأيتام': 'Orphans Scholarship', 'منحة الايتام': 'Orphans Scholarship',
        'منحة الحالات الاجتماعية': 'Hardship Scholarship', 'الحالة الاجتماعية': 'Hardship',
        'خصم الدفع المبكر': 'Early Payment Discount', 'الدفع المبكر': 'Early Payment',
        'خصم الدفعة الواحدة': 'Lump Sum Discount', 'حفظة القرآن': 'Quran Memorisation',
        'ذوي الاحتياجات الخاصة': 'Special Needs', 'الاحتياجات الخاصة': 'Special Needs',

        'إخوة': 'Siblings', 'الإخوة': 'Siblings', 'اخوة': 'Siblings', 'الاخوة': 'Siblings',
        'أيتام': 'Orphans', 'الأيتام': 'Orphans', 'ايتام': 'Orphans', 'الايتام': 'Orphans',
        'يتيم': 'Orphan', 'اليتيم': 'Orphan', 'تفوق': 'Merit', 'التفوق': 'Merit',
        'موظفين': 'Staff', 'الموظفين': 'Staff', 'موظف': 'Staff', 'الموظف': 'Staff',
        'أبناء': 'Children', 'الأبناء': 'Children', 'ابناء': 'Children', 'الابناء': 'Children',
        'اجتماعية': 'Social', 'الاجتماعية': 'Social', 'اجتماعي': 'Social', 'الاجتماعي': 'Social',
        'مبكر': 'Early', 'المبكر': 'Early', 'كامل': 'Full', 'الكامل': 'Full', 'جزئي': 'Partial', 'الجزئي': 'Partial',
        'برنامج': 'Programme', 'البرنامج': 'Programme', 'كفالة': 'Sponsorship', 'الكفالة': 'Sponsorship',
        'راعي': 'Sponsor', 'الراعي': 'Sponsor', 'داعم': 'Sponsor', 'الداعم': 'Sponsor',

        'رسوم': 'Fees', 'الرسوم': 'Fees', 'رسم': 'Fee', 'الرسم': 'Fee',
        'قسط': 'Instalment', 'القسط': 'Instalment', 'أقساط': 'Instalments', 'الأقساط': 'Instalments',
        'تسجيل': 'Registration', 'التسجيل': 'Registration', 'قبول': 'Admission', 'القبول': 'Admission',
        'نقل': 'Transport', 'النقل': 'Transport', 'مواصلات': 'Transport', 'المواصلات': 'Transport',
        'سكن': 'Boarding', 'السكن': 'Boarding', 'إقامة': 'Boarding', 'الإقامة': 'Boarding',
        'كتب': 'Books', 'الكتب': 'Books', 'زي': 'Uniform', 'الزي': 'Uniform',
        'أنشطة': 'Activities', 'الأنشطة': 'Activities', 'انشطة': 'Activities', 'الانشطة': 'Activities',
        'رحلة': 'Trip', 'الرحلة': 'Trip', 'رحلات': 'Trips', 'الرحلات': 'Trips',
        'تأمين': 'Insurance', 'التأمين': 'Insurance', 'تامين': 'Insurance', 'التامين': 'Insurance',
        'امتحان': 'Examination', 'الامتحان': 'Examination', 'امتحانات': 'Examinations', 'الامتحانات': 'Examinations',
        'اختبار': 'Examination', 'الاختبار': 'Examination', 'اختبارات': 'Examinations', 'الاختبارات': 'Examinations',
        'شهادة': 'Certificate', 'الشهادة': 'Certificate', 'شهادات': 'Certificates', 'الشهادات': 'Certificates',
        'وجبة': 'Meal', 'الوجبة': 'Meal', 'وجبات': 'Meals', 'الوجبات': 'Meals',
        'غرامة': 'Fine', 'الغرامة': 'Fine', 'خصم': 'Discount', 'الخصم': 'Discount',
        'منحة': 'Scholarship', 'المنحة': 'Scholarship', 'منح': 'Scholarships', 'المنح': 'Scholarships',
        'إعفاء': 'Waiver', 'الإعفاء': 'Waiver', 'اعفاء': 'Waiver', 'الاعفاء': 'Waiver',
        'تبرع': 'Donation', 'التبرع': 'Donation', 'تأمينات': 'Deposits', 'التأمينات': 'Deposits',
        'صيانة': 'Maintenance', 'الصيانة': 'Maintenance', 'خدمات': 'Services', 'الخدمات': 'Services',
        'إضافي': 'Additional', 'الإضافي': 'Additional', 'اضافي': 'Additional', 'الاضافي': 'Additional',
        'اختياري': 'Optional', 'الاختياري': 'Optional', 'إلزامي': 'Mandatory', 'الإلزامي': 'Mandatory', 'الزامي': 'Mandatory',
        'سنوي': 'Annual', 'السنوي': 'Annual', 'سنوية': 'Annual', 'السنوية': 'Annual',
        'شهري': 'Monthly', 'الشهري': 'Monthly', 'شهرية': 'Monthly', 'الشهرية': 'Monthly',
        'فصلي': 'Termly', 'الفصلي': 'Termly', 'فصلية': 'Termly', 'الفصلية': 'Termly',
        'مدرسي': 'School', 'المدرسي': 'School', 'مدرسية': 'School', 'المدرسية': 'School',

        // ---- who reports to whom: the administrative tree an org unit is named from.
        // The English name here is read by the ministry return, the payroll-prep register
        // and every list that groups staff by unit, so "Almward Albshryh" would be wrong
        // everywhere at once. Noun-adjective order flips between the languages — "الشؤون
        // الأكاديمية" is Academic Affairs, not Affairs Academic — so the pairs a school
        // actually uses are held whole and the matcher takes the longest one.
        'الشؤون الأكاديمية': 'Academic Affairs', 'الشؤون الاكاديمية': 'Academic Affairs', 'الشئون الأكاديمية': 'Academic Affairs',
        'الشؤون الإدارية': 'Administrative Affairs', 'الشؤون الادارية': 'Administrative Affairs',
        'الشؤون المالية': 'Financial Affairs', 'الشؤون التعليمية': 'Educational Affairs', 'الشؤون القانونية': 'Legal Affairs',
        // two adjectives sharing one noun invert further than the rules below reach —
        // "Financial and Administrative Affairs", not Affairs Financial and Administrative
        'الشؤون المالية والإدارية': 'Financial and Administrative Affairs', 'الشؤون المالية والادارية': 'Financial and Administrative Affairs',
        'الشؤون الإدارية والمالية': 'Administrative and Financial Affairs', 'الشؤون الادارية والمالية': 'Administrative and Financial Affairs',
        'شؤون الطلاب': 'Student Affairs', 'شئون الطلاب': 'Student Affairs', 'شؤون الطلبة': 'Student Affairs',
        'شؤون الموظفين': 'Personnel Affairs', 'شؤون المعلمين': 'Teacher Affairs',
        'الموارد البشرية': 'Human Resources', 'التوجيه والإرشاد': 'Guidance and Counselling', 'التوجيه والارشاد': 'Guidance and Counselling',
        'الإرشاد الطلابي': 'Student Counselling', 'الارشاد الطلابي': 'Student Counselling',
        'الإرشاد الأكاديمي': 'Academic Counselling', 'الارشاد الاكاديمي': 'Academic Counselling',
        'تقنية المعلومات': 'Information Technology', 'تكنولوجيا المعلومات': 'Information Technology',
        'الأمن والسلامة': 'Safety and Security', 'الامن والسلامة': 'Safety and Security',
        'العلاقات العامة': 'Public Relations', 'ضمان الجودة': 'Quality Assurance', 'التطوير المهني': 'Professional Development',
        'التدريب والتطوير': 'Training and Development', 'القبول والتسجيل': 'Admissions and Registration',
        'الأنشطة الطلابية': 'Student Activities', 'الانشطة الطلابية': 'Student Activities',
        'الخدمات الطلابية': 'Student Services', 'الخدمات المساندة': 'Support Services',
        'النقل المدرسي': 'School Transport', 'الصحة المدرسية': 'School Health', 'الرعاية الاجتماعية': 'Social Welfare',
        'الإدارة المدرسية': 'School Administration', 'الادارة المدرسية': 'School Administration',
        'إدارة المدرسة': 'School Administration', 'ادارة المدرسة': 'School Administration',
        'مدير المدرسة': 'School Principal', 'نائب المدير': 'Deputy Principal', 'وكيل المدرسة': 'Vice Principal',
        'رئيس القسم': 'Head of Department', 'أمين المكتبة': 'Librarian', 'امين المكتبة': 'Librarian',
        'أمين المستودع': 'Storekeeper', 'امين المستودع': 'Storekeeper',
        'المستودعات والمشتريات': 'Stores and Procurement', 'المرافق والصيانة': 'Facilities and Maintenance',

        'شؤون': 'Affairs', 'الشؤون': 'Affairs', 'شئون': 'Affairs', 'الشئون': 'Affairs',
        'وحدة': 'Unit', 'الوحدة': 'Unit', 'وحدات': 'Units', 'الوحدات': 'Units',
        'دائرة': 'Division', 'الدائرة': 'Division', 'مكتب': 'Office', 'المكتب': 'Office',
        'لجنة': 'Committee', 'اللجنة': 'Committee', 'مجلس': 'Council', 'المجلس': 'Council',
        'أقسام': 'Departments', 'الأقسام': 'Departments', 'اقسام': 'Departments', 'الاقسام': 'Departments',
        'إدارات': 'Departments', 'الإدارات': 'Departments', 'ادارات': 'Departments', 'الادارات': 'Departments',
        'أكاديمية': 'Academic', 'الأكاديمية': 'Academic', 'اكاديمية': 'Academic', 'الاكاديمية': 'Academic',
        'أكاديمي': 'Academic', 'الأكاديمي': 'Academic', 'اكاديمي': 'Academic', 'الاكاديمي': 'Academic',
        'إدارية': 'Administrative', 'الإدارية': 'Administrative', 'ادارية': 'Administrative', 'الادارية': 'Administrative',
        'إداري': 'Administrative', 'الإداري': 'Administrative', 'اداري': 'Administrative', 'الاداري': 'Administrative',
        'مالية': 'Finance', 'المالية': 'Finance', 'مالي': 'Financial', 'المالي': 'Financial',
        'محاسبة': 'Accounting', 'المحاسبة': 'Accounting', 'حسابات': 'Accounts', 'الحسابات': 'Accounts',
        'موارد': 'Resources', 'الموارد': 'Resources', 'بشرية': 'Human', 'البشرية': 'Human',
        'طلاب': 'Students', 'الطلاب': 'Students', 'طلبة': 'Students', 'الطلبة': 'Students',
        'طالبات': 'Students', 'الطالبات': 'Students', 'طالب': 'Student', 'الطالب': 'Student',
        'طلابي': 'Student', 'الطلابي': 'Student', 'طلابية': 'Student', 'الطلابية': 'Student',
        'معلم': 'Teacher', 'المعلم': 'Teacher',
        'توجيه': 'Guidance', 'التوجيه': 'Guidance', 'إرشاد': 'Counselling', 'الإرشاد': 'Counselling',
        'ارشاد': 'Counselling', 'الارشاد': 'Counselling',
        'تقنية': 'Technology', 'التقنية': 'Technology', 'تكنولوجيا': 'Technology', 'التكنولوجيا': 'Technology',
        'معلومات': 'Information', 'المعلومات': 'Information',
        'أمن': 'Security', 'الأمن': 'Security', 'امن': 'Security', 'الامن': 'Security',
        'سلامة': 'Safety', 'السلامة': 'Safety', 'علاقات': 'Relations', 'العلاقات': 'Relations',
        'عامة': 'Public', 'العامة': 'Public', 'جودة': 'Quality', 'الجودة': 'Quality',
        'تطوير': 'Development', 'التطوير': 'Development',
        'مهني': 'Professional', 'المهني': 'Professional', 'مهنية': 'Professional', 'المهنية': 'Professional',
        'تدريب': 'Training', 'التدريب': 'Training',
        'مناهج': 'Curriculum', 'المناهج': 'Curriculum', 'منهج': 'Curriculum', 'المنهج': 'Curriculum',
        'مشتريات': 'Procurement', 'المشتريات': 'Procurement',
        'مستودعات': 'Stores', 'المستودعات': 'Stores', 'مخازن': 'Stores', 'المخازن': 'Stores',
        'تغذية': 'Nutrition', 'التغذية': 'Nutrition',
        'صحة': 'Health', 'الصحة': 'Health', 'صحية': 'Health', 'الصحية': 'Health',
        'رعاية': 'Welfare', 'الرعاية': 'Welfare', 'دعم': 'Support', 'الدعم': 'Support',
        'مساندة': 'Support', 'المساندة': 'Support', 'مرافق': 'Facilities', 'المرافق': 'Facilities',
        'تربية': 'Education', 'التربية': 'Education', 'تعليم': 'Education', 'التعليم': 'Education',
        'تعليمية': 'Educational', 'التعليمية': 'Educational', 'تعليمي': 'Educational', 'التعليمي': 'Educational',
        'قانونية': 'Legal', 'القانونية': 'Legal', 'قانوني': 'Legal', 'القانوني': 'Legal',
        'إشراف': 'Supervision', 'الإشراف': 'Supervision', 'اشراف': 'Supervision', 'الاشراف': 'Supervision',
        'مشرف': 'Supervisor', 'المشرف': 'Supervisor', 'منسق': 'Coordinator', 'المنسق': 'Coordinator',
        'رئيس': 'Head', 'الرئيس': 'Head', 'نائب': 'Deputy', 'النائب': 'Deputy',
        'وكيل': 'Vice Principal', 'الوكيل': 'Vice Principal', 'مساعد': 'Assistant', 'المساعد': 'Assistant',
        'سكرتير': 'Secretary', 'السكرتير': 'Secretary',
        'مختبرات': 'Labs', 'المختبرات': 'Labs', 'معامل': 'Labs', 'المعامل': 'Labs',
        'قاعات': 'Rooms', 'القاعات': 'Rooms', 'غرف': 'Rooms', 'الغرف': 'Rooms',
        'مقاصف': 'Cafeterias', 'المقاصف': 'Cafeterias',
        'أطفال': 'Children', 'الأطفال': 'Children', 'اطفال': 'Children', 'الاطفال': 'Children',
        'حضانة': 'Nursery', 'الحضانة': 'Nursery',
        'أرشيف': 'Archive', 'الأرشيف': 'Archive', 'ارشيف': 'Archive', 'الارشيف': 'Archive',
        'وثائق': 'Records', 'الوثائق': 'Records',
        'إعلام': 'Media', 'الإعلام': 'Media', 'اعلام': 'Media', 'الاعلام': 'Media',
        'تسويق': 'Marketing', 'التسويق': 'Marketing', 'تواصل': 'Communication', 'التواصل': 'Communication',
        'مركز': 'Centre', 'المركز': 'Centre', 'انضباط': 'Discipline', 'الانضباط': 'Discipline',
        'أمناء': 'Trustees', 'الأمناء': 'Trustees', 'امناء': 'Trustees', 'الامناء': 'Trustees',
        'تربوي': 'Educational', 'التربوي': 'Educational', 'تربوية': 'Educational', 'التربوية': 'Educational',
        'تعلم': 'Learning', 'التعلم': 'Learning', 'دراسات': 'Studies', 'الدراسات': 'Studies',
        'آباء': 'Parents', 'الآباء': 'Parents', 'اباء': 'Parents', 'الاباء': 'Parents',
        'أولياء': 'Guardians', 'الأولياء': 'Guardians', 'اولياء': 'Guardians',

        // the subjects a department is named after, in the ministry's English
        'اللغة العربية': 'Arabic Language', 'اللغة الإنجليزية': 'English Language', 'اللغة الانجليزية': 'English Language',
        'الدراسات الإسلامية': 'Islamic Studies', 'الدراسات الاسلامية': 'Islamic Studies',
        'التربية الإسلامية': 'Islamic Education', 'التربية الاسلامية': 'Islamic Education',
        'الدراسات الاجتماعية': 'Social Studies', 'التربية البدنية': 'Physical Education',
        'التربية الفنية': 'Art Education', 'الحاسب الآلي': 'Computer Science', 'الحاسب الالي': 'Computer Science',
        'أولياء الأمور': 'Parents', 'اولياء الامور': 'Parents', 'مصادر التعلم': 'Learning Resources',
        'لغة': 'Language', 'اللغة': 'Language', 'عربية': 'Arabic', 'العربية': 'Arabic',
        'إنجليزية': 'English', 'الإنجليزية': 'English', 'انجليزية': 'English', 'الانجليزية': 'English',
        'رياضيات': 'Mathematics', 'الرياضيات': 'Mathematics', 'اجتماعيات': 'Social Studies', 'الاجتماعيات': 'Social Studies',
        'إسلامية': 'Islamic', 'الإسلامية': 'Islamic', 'اسلامية': 'Islamic', 'الاسلامية': 'Islamic',
        'بدنية': 'Physical', 'البدنية': 'Physical', 'فنية': 'Art', 'الفنية': 'Art', 'آلي': 'Computer', 'الآلي': 'Computer',

        // where and which one — the adjectives that finish a building's name
        'رئيسي': 'Main', 'الرئيسي': 'Main', 'رئيسية': 'Main', 'الرئيسية': 'Main',
        'أرضي': 'Ground', 'الأرضي': 'Ground', 'ارضي': 'Ground', 'الارضي': 'Ground',
        'علوي': 'Upper', 'العلوي': 'Upper', 'سفلي': 'Lower', 'السفلي': 'Lower',
        'شمالي': 'North', 'الشمالي': 'North', 'جنوبي': 'South', 'الجنوبي': 'South',
        'شرقي': 'East', 'الشرقي': 'East', 'غربي': 'West', 'الغربي': 'West',
        'جديد': 'New', 'الجديد': 'New', 'جديدة': 'New', 'الجديدة': 'New',
        'قديم': 'Old', 'القديم': 'Old', 'قديمة': 'Old', 'القديمة': 'Old'
    };

    function normalize(text) {
        return String(text || '')
            .replace(/[ً-ْـ]/g, '')   // harakat and tatweel: decoration, never meaning
            .replace(/\s+/g, ' ')
            .trim();
    }

    // "الثاني عشر" is one ordinal written as two words, so the teens are read
    // before anything else — otherwise "عشر" is dropped and grade 12 becomes 2.
    function readOrdinal(words, i) {
        var first = ORDINALS[words[i]];
        if (first === undefined) { return null; }
        var next = words[i + 1];
        if (next === 'عشر' || next === 'عشرة') {
            return { value: first === 1 ? 11 : first + 10, length: 2 };
        }
        return { value: first, length: 1 };
    }

    // A unit is named for the container and then what it contains — "قسم اللغات" — and
    // English says those two the other way round: Languages Department, never Department
    // Languages. That is the rule for every one of these container words rather than a
    // fact about any one subject, so it is read once here instead of enumerated as a
    // phrase per department. إدارة is a Department when it heads a name and the school's
    // own Administration when it stands alone, which is why the whole-name lookup above
    // gets first refusal.
    var HEADS = {
        'قسم': 'Department', 'القسم': 'Department',
        'إدارة': 'Department', 'الإدارة': 'Department', 'ادارة': 'Department', 'الادارة': 'Department',
        'وحدة': 'Unit', 'الوحدة': 'Unit', 'دائرة': 'Division', 'الدائرة': 'Division',
        'مكتب': 'Office', 'المكتب': 'Office', 'لجنة': 'Committee', 'اللجنة': 'Committee',
        'مجلس': 'Council', 'المجلس': 'Council', 'مركز': 'Centre', 'المركز': 'Centre'
    };

    // The same flip for the other half of the grammar: a noun followed by an adjective —
    // "التغذية المدرسية" — is read adjective first in English, School Nutrition. Only these
    // known adjectives move, and only when both halves are catalogued, so a second word
    // nobody has translated is still left where the writer put it rather than reordered on
    // a guess.
    var ADJECTIVES = {
        'المدرسية': 1, 'المدرسي': 1, 'التربوي': 1, 'التربوية': 1, 'التعليمية': 1, 'التعليمي': 1,
        'الطلابي': 1, 'الطلابية': 1, 'الأكاديمي': 1, 'الأكاديمية': 1, 'الاكاديمي': 1, 'الاكاديمية': 1,
        'الإداري': 1, 'الإدارية': 1, 'الاداري': 1, 'الادارية': 1, 'المهني': 1, 'المهنية': 1,
        'الصحية': 1, 'القانوني': 1, 'القانونية': 1, 'الاجتماعي': 1, 'الاجتماعية': 1, 'البشرية': 1,
        'الفنية': 1, 'البدنية': 1, 'الإسلامية': 1, 'الاسلامية': 1, 'العربية': 1,
        'الإنجليزية': 1, 'الانجليزية': 1, 'العامة': 1, 'المالية': 1, 'المالي': 1
    };

    function translateTerm(text) {
        var whole = normalize(text);
        if (!whole) { return ''; }
        if (TERMS[whole]) { return TERMS[whole]; }

        var words = whole.split(' ');

        if (words.length === 2 && ADJECTIVES[words[1]] && TERMS[words[0]] && TERMS[words[1]]) {
            return TERMS[words[1]] + ' ' + TERMS[words[0]];
        }

        if (words.length > 1 && HEADS[words[0]]) {
            var rest = translateTerm(words.slice(1).join(' '));
            // Not when a number follows: a container numbered rather than named keeps the
            // English order it already had — Unit 3, the way Grade 3 and Floor 1 do.
            if (rest && !/^\d/.test(rest)) { return rest + ' ' + HEADS[words[0]]; }
        }

        var out = [];
        for (var i = 0; i < words.length; i++) {
            // Longest phrase first: "المرحلة الابتدائية" beats "المرحلة" + "الابتدائية",
            // which would read "Stage Elementary".
            var matched = false;
            for (var take = Math.min(3, words.length - i); take >= 2; take--) {
                var phrase = words.slice(i, i + take).join(' ');
                if (TERMS[phrase]) { out.push(TERMS[phrase]); i += take - 1; matched = true; break; }
            }
            if (matched) { continue; }

            var ordinal = readOrdinal(words, i);
            if (ordinal) { out.push(String(ordinal.value)); i += ordinal.length - 1; continue; }

            // A number already written in digits stays as it is, in Western digits.
            var digits = words[i].replace(/[٠-٩]/g, function (d) { return String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)); });
            if (/^\d+$/.test(digits)) { out.push(digits); continue; }

            // Arabic writes "and" joined to the word it introduces, so "التوجيه والإرشاد"
            // arrives here as a word no dictionary can hold — and a unit named after two
            // things is always written that way. Read the conjunction off and translate
            // what is left. The whole word is looked up first, so وحدة and وكيل, which
            // only begin with the same letter, are left alone.
            var w = words[i];
            if (!TERMS[w] && w.length > 2 && w.charAt(0) === 'و' && TERMS[w.slice(1)]) {
                out.push('and', TERMS[w.slice(1)]);
                continue;
            }

            // Unknown word: transliterate rather than drop it. A name nobody
            // catalogued still has to come out as something a reader recognises.
            out.push(TERMS[w] || word(w));
        }

        return out.join(' ');
    }

    window.smsTranslateTerm = translateTerm;
    window.smsTransliterate = transliterate;

    function renderer(target) {
        return target.getAttribute('data-translit-mode') === 'term' ? translateTerm : transliterate;
    }

    // Fill one English field from its Arabic partner right now, whatever it holds.
    // Only ever called from the helper button below: an explicit click is the one
    // moment overwriting a spelling already in the box is the thing being asked
    // for. The dispatched event lets the field's own listener mark itself manual,
    // so typing in the Arabic afterwards does not quietly undo the result.
    function fill(target) {
        var sel = target.getAttribute('data-translit-from');
        var source = sel ? document.querySelector(sel) : null;
        if (!source) { return; }
        target.value = renderer(target)(source.value);
        target.dispatchEvent(new Event('input', { bubbles: true }));
    }

    // The copy-transliterate helper button of docs/UI/01 §"Bilingual entry pattern":
    // assist only, never auto-commit. It writes the form's English boxes from the
    // Arabic ones in one click and stops there — the typist reads the result,
    // corrects what the dictionary got wrong, and saves. Nothing reaches the
    // database that a human did not submit.
    //
    // Usage: <button type="button" data-translit-fill="#name-block"> where the
    // selector names the block whose [data-translit-from] fields it fills.
    function wireFillButton(button) {
        button.addEventListener('click', function (e) {
            e.preventDefault();
            var scope = document.querySelector(button.getAttribute('data-translit-fill'));
            if (!scope) { return; }
            scope.querySelectorAll('[data-translit-from]').forEach(fill);
        });
    }

    function wire(target) {
        var sel = target.getAttribute('data-translit-from');
        var source = sel ? document.querySelector(sel) : null;
        if (!source) { return; }
        var applying = false;
        var render = renderer(target);
        source.addEventListener('input', function () {
            if (target.getAttribute('data-translit-manual') === '1') { return; }
            applying = true;
            target.value = render(source.value);
            target.dispatchEvent(new Event('input', { bubbles: true }));
            applying = false;
        });
        target.addEventListener('input', function () {
            if (applying) { return; }
            // A human typed here: respect it. Clearing the field re-arms auto-fill.
            target.setAttribute('data-translit-manual', target.value ? '1' : '0');
        });
        if (target.value) { target.setAttribute('data-translit-manual', '1'); }
    }

    // ------------------------------------------------- typing Latin on an Arabic keyboard
    //
    // A browser cannot change the operating system's keyboard, so the field has to
    // meet the typist where they are: the letters of the Arabic layout are mapped
    // back to the Latin letter engraved on the same physical key, so a name typed
    // without switching layouts comes out as the name that was meant.
    //
    // Only what is typed is remapped. A paste arrives as insertFromPaste and is
    // left exactly as it was — pasting Arabic into an English field is a decision,
    // not a slip.
    var LAYOUT = {
        'ض': 'q', 'ص': 'w', 'ث': 'e', 'ق': 'r', 'ف': 't', 'غ': 'y', 'ع': 'u', 'ه': 'i', 'خ': 'o', 'ح': 'p',
        'ج': '[', 'د': ']', 'ذ': '`',
        'ش': 'a', 'س': 's', 'ي': 'd', 'ب': 'f', 'ل': 'g', 'ا': 'h', 'ت': 'j', 'ن': 'k', 'م': 'l', 'ك': ';', 'ط': "'",
        'ئ': 'z', 'ء': 'x', 'ؤ': 'c', 'ر': 'v', 'ى': 'n', 'ة': 'm', 'و': ',', 'ز': '.', 'ظ': '/',
        'َ': 'Q', 'ً': 'W', 'ُ': 'E', 'ٌ': 'R', 'إ': 'Y', 'ْ': 'X', 'ّ': '~', 'أ': 'H', 'آ': 'N',
        '،': 'K', 'ٍ': 'S', 'ِ': 'A'
    };

    function toLatinKeys(text) {
        // "لا" sits on one key, and typing it produces two characters: read it as
        // that key before the letters are taken one at a time.
        var out = String(text).replace(/لا/g, 'b').replace(/لأ/g, 'B').replace(/لإ/g, 'T');
        return Array.from(out).map(function (ch) {
            return Object.prototype.hasOwnProperty.call(LAYOUT, ch) ? LAYOUT[ch] : ch;
        }).join('');
    }

    window.smsToLatinKeys = toLatinKeys;

    function wireLatinKeys(el) {
        el.setAttribute('lang', 'en');
        el.addEventListener('beforeinput', function (e) {
            if (e.inputType !== 'insertText' || !e.data) { return; }
            var mapped = toLatinKeys(e.data);
            if (mapped === e.data) { return; }
            e.preventDefault();
            var start = el.selectionStart, end = el.selectionEnd;
            if (typeof el.setRangeText === 'function') {
                el.setRangeText(mapped, start, end, 'end');
            } else {
                el.value = el.value.slice(0, start) + mapped + el.value.slice(end);
            }
            el.dispatchEvent(new Event('input', { bubbles: true }));
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-translit-from]').forEach(wire);
        document.querySelectorAll('[data-translit-fill]').forEach(wireFillButton);

        // Every English half of a bilingual pair, plus anything asking for it by
        // name. The Arabic halves are labelled too, so a screen reader and the
        // browser's own spellcheck both know which language they are looking at.
        document.querySelectorAll('[data-translit-from], [data-latin-keys]').forEach(wireLatinKeys);
        document.querySelectorAll('input[dir="rtl"], textarea[dir="rtl"]').forEach(function (el) {
            el.setAttribute('lang', 'ar');
        });
    });
})();
