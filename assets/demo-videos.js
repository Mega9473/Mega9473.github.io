(() => {
  // src / poster are relative to code/unity.html. Leave src empty until recorded.
  // Example: src: '../assets/videos/unity-polishing.mp4'
  const videos = {
    u1: {
      src: '',
      poster: '',
      caption: '研磨面に対する工具の位置・姿勢計測の動作例。'
    },
    u2: {
      src: '',
      poster: '',
      caption: '実空間のセンサデータとVR空間の連動の動作例。'
    },
    u3: {
      src: '',
      poster: '',
      caption: '実験条件の提示と主観評価の記録の動作例。'
    }
  };

  for (const [id, settings] of Object.entries(videos)) {
    const src = settings.src.trim();
    if (!src) continue;

    const article = document.getElementById(id);
    const code = article?.querySelector('.code-disclosure');
    if (!code || article.querySelector('.code-video')) continue;

    const figure = document.createElement('figure');
    figure.className = 'code-video';
    const video = document.createElement('video');
    video.controls = true;
    video.playsInline = true;
    video.preload = 'metadata';
    video.setAttribute('aria-labelledby', `${id}-title`);
    video.src = src;
    if (settings.poster.trim()) video.poster = settings.poster.trim();

    const fallback = document.createElement('a');
    fallback.href = src;
    fallback.textContent = '動作動画を開く';
    video.append(fallback);
    figure.append(video);

    if (settings.caption.trim()) {
      const caption = document.createElement('figcaption');
      caption.id = `${id}-video-caption`;
      caption.textContent = settings.caption;
      video.setAttribute('aria-describedby', caption.id);
      figure.append(caption);
    }

    code.before(figure);
  }
})();
