window.AudioPlayer = {
    _dotNetRef: null,

    init: function (dotNetRef) {
        AudioPlayer._dotNetRef = dotNetRef;
    },

    updateProgress: function (audio) {
        if (audio.duration && AudioPlayer._dotNetRef) {
            var progress = (audio.currentTime / audio.duration) * 100;
            AudioPlayer._dotNetRef.invokeMethodAsync('UpdateProgress', progress, audio.currentTime, audio.duration);
        }
    },

    onEnded: function () {
        if (AudioPlayer._dotNetRef) {
            AudioPlayer._dotNetRef.invokeMethodAsync('OnEnded');
        }
    },

    play: function (url) {
        var audio = document.querySelector('.audio-player audio');
        if (!audio) return;
        audio.src = url;
        audio.play().catch(function (e) { console.warn('Audio play failed:', e); });
    },

    pause: function () {
        var audio = document.querySelector('.audio-player audio');
        if (audio) audio.pause();
    },

    stop: function () {
        var audio = document.querySelector('.audio-player audio');
        if (audio) { audio.pause(); audio.currentTime = 0; }
    },

    seek: function (percent) {
        var audio = document.querySelector('.audio-player audio');
        if (audio && audio.duration) {
            audio.currentTime = (percent / 100) * audio.duration;
        }
    }
};
