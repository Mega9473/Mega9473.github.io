(() => {
  const duration = 360;
  let frame = 0;
  const header = document.querySelector('.site-header');
  const updateHeaderOffset = () => {
    if (header) document.documentElement.style.setProperty('--header-offset', `${Math.ceil(header.getBoundingClientRect().height) + 24}px`);
  };
  updateHeaderOffset();
  if (header && 'ResizeObserver' in window) new ResizeObserver(updateHeaderOffset).observe(header);
  else window.addEventListener('resize', updateHeaderOffset, {passive: true});

  const cancel = () => {
    if (frame) cancelAnimationFrame(frame);
    frame = 0;
  };

  document.addEventListener('click', event => {
    const link = event.target.closest?.('.site-header a[href^="#"]');
    if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    const hash = link.getAttribute('href');
    const target = document.getElementById(decodeURIComponent(hash.slice(1)));
    if (!target) return;

    event.preventDefault();
    cancel();
    const start = window.scrollY;
    const offset = parseFloat(getComputedStyle(document.documentElement).scrollPaddingTop) || 0;
    const limit = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
    const end = Math.max(0, Math.min(limit, start + target.getBoundingClientRect().top - offset));

    const finish = () => {
      frame = 0;
      window.scrollTo(0, end);
      if (window.location.hash !== hash) {
        try { history.pushState(null, '', hash); } catch (_) { /* Some embedded previews restrict history. */ }
      }
      target.focus({preventScroll: true});
    };

    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches || Math.abs(end - start) < 2) {
      finish();
      return;
    }

    let started;
    const step = now => {
      if (started === undefined) started = now;
      const progress = Math.min(1, (now - started) / duration);
      const eased = 1 - Math.pow(1 - progress, 3);
      window.scrollTo(0, start + (end - start) * eased);
      if (progress < 1) frame = requestAnimationFrame(step);
      else finish();
    };
    frame = requestAnimationFrame(step);
  });

  window.addEventListener('wheel', cancel, {passive: true});
  window.addEventListener('touchstart', cancel, {passive: true});
  window.addEventListener('pointerdown', cancel, {passive: true});
  window.addEventListener('keydown', event => {
    if (['ArrowUp', 'ArrowDown', 'PageUp', 'PageDown', 'Home', 'End', ' ', 'Escape', 'Tab'].includes(event.key)) cancel();
  });
})();
