// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// ── Content editor: format & validate ────────────────────────────────────────
(function () {
  var YAML_CDN = 'https://cdn.jsdelivr.net/npm/js-yaml@4.1.0/dist/js-yaml.min.js';
  var TOML_CDN = 'https://cdn.jsdelivr.net/npm/@iarna/toml@2.2.5/toml-browser.js';

  function loadScript(src, callback) {
    if (document.querySelector('script[data-lib-src="' + src + '"]')) {
      callback();
      return;
    }
    var s = document.createElement('script');
    s.setAttribute('data-lib-src', src);
    s.src = src;
    s.onload = callback;
    s.onerror = function () { callback(new Error('Failed to load: ' + src)); };
    document.head.appendChild(s);
  }

  // Simple XML indenter (pure JS, no library needed)
  function indentXml(xml) {
    var pad = 0;
    var INDENT = '  ';
    return xml
      .replace(/(>)(<)(\/*)/g, '$1\n$2$3')
      .split('\n')
      .map(function (node) {
        var indent = 0;
        if (node.match(/.+<\/\w[^>]*>$/)) {
          indent = 0;
        } else if (node.match(/^<\/\w/) && pad > 0) {
          pad -= 1;
        } else if (node.match(/^<\w[^>]*[^\/]>.*$/) && !node.match(/^<\?/)) {
          indent = 1;
        } else {
          indent = 0;
        }
        var line = new Array(pad + 1).join(INDENT) + node;
        pad += indent;
        return line;
      })
      .join('\n');
  }

  function doFormat(type, text) {
    if (type === 'JSON') {
      return JSON.stringify(JSON.parse(text), null, 2);
    }
    if (type === 'XML') {
      var parser = new DOMParser();
      var doc = parser.parseFromString(text, 'text/xml');
      var err = doc.querySelector('parsererror');
      if (err) throw new Error(err.textContent);
      return indentXml(text.replace(/\r?\n/g, '').replace(/>\s+</g, '><'));
    }
    return null;
  }

  function doValidate(type, text, callback) {
    if (type === 'TEXT') {
      callback(null);
      return;
    }
    if (type === 'JSON') {
      try { JSON.parse(text); callback(null); } catch (e) { callback(e.message); }
      return;
    }
    if (type === 'XML') {
      var xDoc = new DOMParser().parseFromString(text, 'text/xml');
      var xErr = xDoc.querySelector('parsererror');
      callback(xErr ? xErr.textContent : null);
      return;
    }
    if (type === 'HTML') {
      var hDoc = new DOMParser().parseFromString(text, 'text/html');
      // DOMParser for HTML never errors; check for body content at minimum
      callback(hDoc.body ? null : 'Empty or invalid HTML.');
      return;
    }
    if (type === 'YAML') {
      loadScript(YAML_CDN, function (err) {
        if (err) { callback('Cannot load YAML library: ' + err.message); return; }
        try { window.jsyaml.load(text); callback(null); } catch (e) { callback(e.message); }
      });
      return;
    }
    if (type === 'TOML') {
      loadScript(TOML_CDN, function (err) {
        if (err) { callback('Cannot load TOML library: ' + err.message); return; }
        try { window.TOML.parse(text); callback(null); } catch (e) { callback(e.message); }
      });
      return;
    }
    if (type === 'Properties') {
      var lines = text.split(/\r?\n/);
      for (var i = 0; i < lines.length; i++) {
        var line = lines[i].trim();
        if (line === '' || line.charAt(0) === '#' || line.charAt(0) === '!') continue;
        if (!/^[^=:\s][^=:]*[=:]/.test(line)) {
          callback('Line ' + (i + 1) + ': invalid property — expected key=value or key:value');
          return;
        }
      }
      callback(null);
      return;
    }
    callback(null);
  }

  function initContentEditor() {
    document.querySelectorAll('[data-content-type-select]').forEach(function (select) {
      var id = select.getAttribute('data-content-type-select');
      var textarea = document.querySelector('[data-content-editor="' + id + '"]');
      var feedback = document.querySelector('[data-content-feedback="' + id + '"]');
      var btnFormat = document.querySelector('[data-content-format="' + id + '"]');
      var btnValidate = document.querySelector('[data-content-validate="' + id + '"]');
      if (!textarea || !feedback || !btnFormat || !btnValidate) return;

      function showFeedback(msg, isError) {
        feedback.textContent = msg;
        feedback.className = 'content-feedback ' + (isError ? 'content-feedback-error' : 'content-feedback-ok');
        feedback.style.display = msg ? '' : 'none';
      }

      function updateToolbar() {
        var type = select.value;
        var canFormat = type === 'JSON' || type === 'XML';
        btnFormat.style.display = canFormat ? '' : 'none';
        showFeedback('', false);
      }

      select.addEventListener('change', updateToolbar);
      updateToolbar();

      btnFormat.addEventListener('click', function (e) {
        e.preventDefault();
        try {
          var result = doFormat(select.value, textarea.value);
          if (result !== null) {
            textarea.value = result;
            showFeedback('Formatted.', false);
          }
        } catch (ex) {
          showFeedback('Format error: ' + ex.message, true);
        }
      });

      btnValidate.addEventListener('click', function (e) {
        e.preventDefault();
        showFeedback('Validating\u2026', false);
        doValidate(select.value, textarea.value, function (err) {
          if (err) showFeedback('Invalid: ' + err, true);
          else showFeedback('Valid ' + select.value + '.', false);
        });
      });
    });
  }

  document.addEventListener('DOMContentLoaded', initContentEditor);
})();
