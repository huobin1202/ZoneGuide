/**
 * Text-to-Speech qua Web Speech API — chọn giọng theo ngôn ngữ, tốc độ tự nhiên.
 */
(function () {
    'use strict';

    function prepareText(text) {
        if (!text) return '';
        var s = String(text).trim().replace(/\s+/g, ' ');
        s = s.replace(/—/g, ' — ').replace(/–/g, ' – ');
        s = s.replace(/\s*([.,;:!?])\s*/g, '$1 ');
        s = s.replace(/\s*-\s*/g, ' - ');
        return s.trim();
    }

    function normalizeLang(lang) {
        return (lang || 'vi-VN').trim().replace(/_/g, '-');
    }

    function langPrimary(lang) {
        var p = normalizeLang(lang).split('-')[0];
        return p ? p.toLowerCase() : 'vi';
    }

    function getVoices() {
        return window.speechSynthesis ? window.speechSynthesis.getVoices() : [];
    }

    function ensureVoicesLoaded() {
        return new Promise(function (resolve) {
            var voices = getVoices();
            if (voices.length > 0) {
                resolve(voices);
                return;
            }
            var done = false;
            var finish = function () {
                if (done) return;
                done = true;
                window.speechSynthesis.onvoiceschanged = null;
                resolve(getVoices());
            };
            var t = setTimeout(finish, 2500);
            window.speechSynthesis.onvoiceschanged = function () {
                clearTimeout(t);
                finish();
            };
        });
    }

    function pickVoice(voices, lang, preferredVoiceUri) {
        if (!voices || voices.length === 0) return null;
        var L = normalizeLang(lang);
        var primary = langPrimary(L);
        var parts = L.split('-');
        var region = parts.length > 1 && parts[1].length >= 2 ? parts[1].toUpperCase() : null;

        function matchesPrimary(v) {
            var vl = (v.lang || '').replace(/_/g, '-').toLowerCase();
            if (vl === L.toLowerCase()) return true;
            if (vl === primary) return true;
            if (vl.indexOf(primary + '-') === 0) return true;
            return false;
        }

        var list = voices.filter(matchesPrimary);
        if (list.length === 0) {
            list = voices.filter(function (v) {
                return (v.lang || '').toLowerCase().indexOf(primary) === 0;
            });
        }
        if (list.length === 0) return null;

        function firstMatch(arr, pred) {
            for (var i = 0; i < arr.length; i++) {
                if (pred(arr[i])) return arr[i];
            }
            return null;
        }

        if (preferredVoiceUri) {
            var byUri = firstMatch(list, function (v) { return v.voiceURI === preferredVoiceUri; });
            if (byUri) return byUri;
        }

        // Ưu tiên 1: Cố gắng lấy giọng Neural/Premium của ngôn ngữ này
        var neural = firstMatch(list, function (v) {
            return /google|microsoft|natural|neural|premium|enhanced/i.test(v.name);
        });
        if (neural) return neural;

        // Ưu tiên 2: Khớp chính xác hoàn toàn (cả quốc gia)
        var exact = firstMatch(list, function (v) {
            return (v.lang || '').replace(/_/g, '-').toLowerCase() === L.toLowerCase();
        });
        if (exact) return exact;

        // Ưu tiên 3: Khớp tên viết tắt quốc gia (VD: VN)
        if (region) {
            var byReg = firstMatch(list, function (v) {
                var vl = (v.lang || '').replace(/_/g, '-');
                return vl.toUpperCase().indexOf('-' + region) > 0 ||
                    vl.toUpperCase().slice(-(region.length + 1)) === '-' + region;
            });
            if (byReg) return byReg;
        }

        // Gỡ lại giọng đầu tiên tìm được của ngôn ngữ đó
        return list[0];
    }

    window.zoneGuideTts = {
        prepareText: prepareText,

        /**
         * @returns {Promise<{ ok: boolean, error?: string }>}
         */
        speak: function (text, lang, options) {
            options = options || {};
            if (!('speechSynthesis' in window)) {
                return Promise.resolve({ ok: false, error: 'no-tts' });
            }

            var rate = typeof options.rate === 'number' ? options.rate : 1;
            rate = Math.min(2, Math.max(0.5, rate));
            var pitch = typeof options.pitch === 'number' ? options.pitch : 1;
            pitch = Math.min(2, Math.max(0.5, pitch));
            var volume = typeof options.volume === 'number' ? options.volume : 1;
            volume = Math.min(1, Math.max(0, volume));
            var voiceUri = options.voiceUri || null;

            window.speechSynthesis.cancel();

            var langNorm = normalizeLang(lang);
            var textPrep = prepareText(text);
            if (!textPrep) {
                return Promise.resolve({ ok: false, error: 'empty-text' });
            }

            return ensureVoicesLoaded().then(function (voices) {
                try {
                    var u = new SpeechSynthesisUtterance(textPrep);
                    u.lang = langNorm;
                    var voice = pickVoice(voices, langNorm, voiceUri);
                    if (voice) {
                        u.voice = voice;
                        if (voice.lang) u.lang = voice.lang;
                    }
                    u.rate = rate;
                    u.pitch = pitch;
                    u.volume = volume;
                    window.speechSynthesis.speak(u);
                    return { ok: true };
                } catch (e) {
                    return { ok: false, error: e && e.message ? e.message : 'speak-failed' };
                }
            });
        },

        stop: function () {
            if ('speechSynthesis' in window) {
                window.speechSynthesis.cancel();
            }
        },

        /**
         * @returns {Promise<Array<{ name, lang, voiceURI, localService, isDefault }>>}
         */
        listVoices: function () {
            if (!('speechSynthesis' in window)) {
                return Promise.resolve([]);
            }
            return ensureVoicesLoaded().then(function (voices) {
                return voices.map(function (v) {
                    return {
                        name: v.name,
                        lang: v.lang,
                        voiceURI: v.voiceURI,
                        localService: v.localService,
                        isDefault: v.default === true
                    };
                });
            });
        }
    };
})();
