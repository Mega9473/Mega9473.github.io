(() => {
  // src / poster are relative to code/unity.html. Leave src empty to hide the video.
  // Example: src: '../assets/videos/unity-polishing.mp4'
  const videos = {
    u1: {
      src: '../assets/videos/unity-polishing.mp4',
      poster: '../assets/videos/unity-polishing.jpg',
      caption: '研磨工具の操作とVR内の表示（左：VR映像，右：実際の操作）'
    },
    u2: {
      src: '../assets/videos/unity-sensor-vr.mp4',
      poster: '../assets/videos/unity-sensor-vr.jpg',
      caption: '装置の操作に連動するVR内の表示（左：VR映像，右：実際の操作）'
    },
    u3: {
      src: '',
      poster: '',
      caption: '実験条件の提示と主観評価の記録の動作例'
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
