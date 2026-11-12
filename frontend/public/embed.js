(function () {
  var script = document.currentScript;
  if (!script || !script.src) return;

  var origin = new URL(script.src, window.location.href).origin;
  var targetSelector = script.getAttribute('data-target');
  var target = null;
  if (targetSelector) {
    try {
      target = document.querySelector(targetSelector);
    } catch (e) {
      target = null;
    }
  }

  var theme = script.getAttribute('data-theme');
  if (theme !== 'dark' && theme !== 'auto') theme = 'light';

  var maxWidth = script.getAttribute('data-max-width') || '720px';

  var iframe = document.createElement('iframe');
  iframe.src = origin + '/embed/enroll?theme=' + theme;
  iframe.title = script.getAttribute('data-title') || 'Membership application';
  iframe.loading = 'lazy';
  iframe.referrerPolicy = 'origin';
  iframe.setAttribute('scrolling', 'no');
  iframe.style.cssText =
    'width:100%;max-width:' + maxWidth + ';height:480px;border:0;display:block;margin:0 auto';

  if (target) {
    target.appendChild(iframe);
  } else {
    var wrapper = document.createElement('div');
    wrapper.style.cssText = 'position:relative;isolation:isolate;z-index:1;width:100%';
    wrapper.appendChild(iframe);
    script.parentNode.insertBefore(wrapper, script.nextSibling);
  }

  window.addEventListener('message', function (event) {
    if (event.origin !== origin || event.source !== iframe.contentWindow) return;
    var data = event.data;
    if (!data || data.type !== 'uv-enroll-height' || typeof data.height !== 'number') return;
    iframe.style.height = Math.max(0, Math.ceil(data.height)) + 'px';
  });
})();
