window.ZoneGuideApp = {
  HISTORY_KEY: 'zg_history',
  SETTINGS_KEY: 'zg_settings',

  getHistory: function () {
    try { return JSON.parse(localStorage.getItem(this.HISTORY_KEY)) || []; }
    catch (e) { return []; }
  },

  saveHistory: function (items) {
    localStorage.setItem(this.HISTORY_KEY, JSON.stringify(items));
  },

  addHistoryEntry: function (entry) {
    if (typeof entry === 'string') { try { entry = JSON.parse(entry); } catch (e) { return; } }
    var items = this.getHistory();
    items = items.filter(function (e) { return e.id !== entry.id || e.timestamp !== entry.timestamp; });
    items.unshift(entry);
    if (items.length > 200) items = items.slice(0, 200);
    this.saveHistory(items);
  },

  deleteHistoryEntry: function (id, timestamp) {
    var items = this.getHistory();
    items = items.filter(function (e) { return !(e.id === id && e.timestamp === timestamp); });
    this.saveHistory(items);
    return JSON.stringify(items);
  },

  clearHistory: function () {
    localStorage.removeItem(this.HISTORY_KEY);
  },

  getSettings: function () {
    try {
      var defaults = { language: 'vi', autoPlay: true, ttsSpeed: 1.0 };
      var saved = JSON.parse(localStorage.getItem(this.SETTINGS_KEY)) || {};
      for (var k in defaults) { if (saved[k] === undefined) saved[k] = defaults[k]; }
      return saved;
    } catch (e) {
      return { language: 'vi', autoPlay: true, ttsSpeed: 1.0 };
    }
  },

  saveSettings: function (settings) {
    if (typeof settings === 'string') {
      localStorage.setItem(this.SETTINGS_KEY, settings);
    } else {
      localStorage.setItem(this.SETTINGS_KEY, JSON.stringify(settings));
    }
  },

  getHistoryJson: function () {
    return JSON.stringify(this.getHistory());
  },

  getSettingsJson: function () {
    return JSON.stringify(this.getSettings());
  }
};
