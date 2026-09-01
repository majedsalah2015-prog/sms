// The calculator a money field puts beside itself: press the button, work the sum out, press OK,
// and the answer lands in the field. Pairs with Views/Shared/_Calculator.cshtml.
//
// Behaviour only — every string on the dialog is authored in Razor, in both languages, so there is
// nothing here to translate. This file knows the dialog by its id and its keys by their data
// attributes, and nothing else about the screen it is used on.
//
// Wiring is delegated from the document rather than bound per button: the fee structure grid draws
// a field in every cell of a grade × category table, and a per-button binding would be a listener
// per cell for no gain — and would leave anything rendered later with a dead calculator.
//
// Deliberately not the embedded ERP's erp-calculator.js, which does the same job on its own
// screens: that file is a build-time copy out of the read-only submodule, and a fees screen that
// depended on it would stop working the day Sms.Erp.Bridge is deleted. The school system stands
// on its own.
(function () {
    'use strict';

    // The partial may be rendered by two screens composed onto one page; the listener below is on
    // the document, so a second copy would answer every click twice.
    if (window.__smsCalculator) { return; }
    window.__smsCalculator = true;

    var DIALOG_ID = 'smsCalculator';

    var dialog = null;      // the dialog element, looked up on first use
    var target = null;      // the input the open button sits beside
    var display = null;
    var expression = null;
    var scale = 2;          // decimals the target field accepts, read from its step

    // A plain chain calculator: what is on the display, the operand waiting on an operator, and
    // whether the next digit starts a new number or extends the one shown.
    var shown = '0';
    var pending = null;
    var pendingOp = null;
    var startFresh = true;

    var OPERATORS = { '+': true, '-': true, '*': true, '/': true };
    var SYMBOLS = { '+': '+', '-': '−', '*': '×', '/': '÷' };

    /// How many decimals the field will take, read off its own `step` rather than fixed here: money
    /// fields on these screens are step="0.01", and an answer carried in at full float precision
    /// would be refused by the field and would not be what the user saw on the display either.
    function scaleOf(input) {
        var step = input && input.getAttribute('step');
        if (!step || step === 'any') { return 2; }
        var dot = String(step).indexOf('.');
        if (dot < 0) { return 0; }
        return Math.min(6, String(step).length - dot - 1);
    }

    function round(value) {
        var factor = Math.pow(10, scale);
        return Math.round((value + Number.EPSILON) * factor) / factor;
    }

    /// Trailing zeros are dropped so the display reads 12.5 rather than 12.50 — the field stores the
    /// same number either way, and the shorter form is what the user typed.
    function format(value) {
        if (!isFinite(value)) { return '0'; }
        return String(round(value));
    }

    function render() {
        display.textContent = shown;
        expression.textContent = pendingOp === null
            ? ' '
            : format(pending) + ' ' + SYMBOLS[pendingOp];
    }

    /// A refused sum has to look refused. Colour rather than a message: it says "that key did
    /// nothing" without a string to translate, and the display already shows what was typed.
    function refuse() {
        if (!display) { return; }
        display.classList.add('text-danger');
        window.setTimeout(function () { display.classList.remove('text-danger'); }, 600);
    }

    function reset(seed) {
        shown = seed;
        pending = null;
        pendingOp = null;
        startFresh = true;
        render();
    }

    /// Applies the waiting operator, or returns the display untouched when there is none.
    /// Dividing by zero is refused rather than answered: the operator stays pending so another
    /// divisor can be typed, and nothing wrong is written where an amount is expected.
    function resolve() {
        if (pendingOp === null) { return true; }

        // An operator with nothing typed after it is dropped, not applied to the number it was
        // typed against: 12 × then OK means 12, not 144. It is also what makes changing your mind
        // about the operator work — 12 × ÷ leaves 12 ÷ rather than squaring anything.
        if (startFresh) { pending = null; pendingOp = null; return true; }

        var right = parseFloat(shown) || 0;
        if (pendingOp === '/' && right === 0) { refuse(); return false; }

        var result;
        switch (pendingOp) {
            case '+': result = pending + right; break;
            case '-': result = pending - right; break;
            case '*': result = pending * right; break;
            default: result = pending / right; break;
        }

        shown = format(result);
        pending = null;
        pendingOp = null;
        return true;
    }

    function press(key) {
        if (key >= '0' && key <= '9') {
            shown = (startFresh || shown === '0') ? key : shown + key;
            startFresh = false;
        } else if (key === '.') {
            if (startFresh) { shown = '0.'; startFresh = false; }
            else if (shown.indexOf('.') < 0) { shown += '.'; }
        } else if (OPERATORS[key]) {
            // Chaining (2 + 3 + 4) resolves the first sum before taking the second operator, so the
            // running total is on the display the whole way through.
            if (!resolve()) { return; }
            pending = parseFloat(shown) || 0;
            pendingOp = key;
            startFresh = true;
        } else if (key === '=') {
            if (!resolve()) { return; }
            startFresh = true;
        } else if (key === 'clear') {
            reset('0');
            return;
        } else if (key === 'back') {
            if (!startFresh) {
                shown = shown.length > 1 ? shown.slice(0, -1) : '0';
                if (shown === '' || shown === '-') { shown = '0'; }
            }
        }

        render();
    }

    /// OK: finish any half-typed sum first, so pressing 12 × 5 then OK writes 60 rather than 5.
    function accept() {
        if (!resolve()) { return; }

        var value = parseFloat(shown);
        if (!isFinite(value)) { return; }

        if (target) {
            target.value = String(round(value));

            // Assigning to `.value` fires nothing, so a screen that recomputes a total from an
            // `input` listener would never hear this. `change` follows for anything waiting on the
            // field to settle.
            target.dispatchEvent(new Event('input', { bubbles: true }));
            target.dispatchEvent(new Event('change', { bubbles: true }));
        }

        close();
    }

    function close() {
        if (window.bootstrap && dialog) {
            var instance = window.bootstrap.Modal.getInstance(dialog);
            if (instance) { instance.hide(); return; }
        }

        // No Bootstrap on the page: the dialog was never opened as a modal either, so there is
        // nothing to hide and the button simply did nothing. Leave the field alone.
        if (dialog) { dialog.classList.remove('show'); dialog.style.display = ''; }
    }

    function onKeyDown(e) {
        var key = e.key;

        if (key === 'Enter') { e.preventDefault(); accept(); return; }
        if (key === 'Escape') { return; }               // Bootstrap closes it; nothing to add
        if (key === 'Backspace') { e.preventDefault(); press('back'); return; }
        if (key === 'Delete') { e.preventDefault(); press('clear'); return; }
        if (key === 'x' || key === 'X') { e.preventDefault(); press('*'); return; }

        if ((key >= '0' && key <= '9') || key === '.' || key === '=' || OPERATORS[key]) {
            e.preventDefault();
            press(key);
        }
    }

    function open(button) {
        dialog = document.getElementById(DIALOG_ID);
        if (!dialog || !window.bootstrap) { return; }

        // The field this belongs to is the one it shares an input group with — no id to keep in
        // step, which matters in a grid whose cells are named from the row and column they sit in.
        var group = button.closest('.input-group');
        target = group ? group.querySelector('input') : null;
        if (!target) { return; }

        scale = scaleOf(target);
        display = dialog.querySelector('[data-sms-calc-display]');
        expression = dialog.querySelector('[data-sms-calc-expression]');
        if (!display || !expression) { return; }

        // Opens on what the field already holds, so a figure being revised can be adjusted rather
        // than retyped. A blank or zero field opens at 0, with the first digit replacing it.
        var seed = parseFloat(target.value);
        reset(isFinite(seed) && seed !== 0 ? format(seed) : '0');

        document.addEventListener('keydown', onKeyDown);
        dialog.addEventListener('hidden.bs.modal', function once() {
            dialog.removeEventListener('hidden.bs.modal', once);
            document.removeEventListener('keydown', onKeyDown);

            // Back to the field that was being filled in, so the line can be finished from the
            // keyboard — ↵ in these cells is what saves it.
            if (target) { target.focus(); }
        });

        window.bootstrap.Modal.getOrCreateInstance(dialog).show();
    }

    document.addEventListener('click', function (e) {
        if (!e.target || typeof e.target.closest !== 'function') { return; }

        var opener = e.target.closest('[data-sms-calc]');
        if (opener) { e.preventDefault(); open(opener); return; }

        var key = e.target.closest('[data-sms-calc-key]');
        if (key && key.closest('#' + DIALOG_ID)) { e.preventDefault(); press(key.dataset.smsCalcKey); return; }

        var ok = e.target.closest('[data-sms-calc-accept]');
        if (ok && ok.closest('#' + DIALOG_ID)) { e.preventDefault(); accept(); }
    });
})();
