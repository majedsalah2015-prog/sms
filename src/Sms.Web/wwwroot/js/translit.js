// Arabic → English name transliteration (client-side convenience).
// Usage: <input data-translit-from="#FirstNameAr" /> on an English field.
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

    function wire(target) {
        var sel = target.getAttribute('data-translit-from');
        var source = sel ? document.querySelector(sel) : null;
        if (!source) { return; }
        var applying = false;
        source.addEventListener('input', function () {
            if (target.getAttribute('data-translit-manual') === '1') { return; }
            applying = true;
            target.value = transliterate(source.value);
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

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-translit-from]').forEach(wire);
    });
})();
