// Roblox Network Tuner - Showcase Web Application
// Zero-dependency interactive client script

document.addEventListener('DOMContentLoaded', () => {
  initHeroTelemetry();
  initSimulator();
  initClipboard();
  initFaqAccordion();
  fetchGitHubReleaseInfo();
});

// 1. Live Hero Telemetry Fluctuations
function initHeroTelemetry() {
  const pingValEl = document.getElementById('heroPingVal');
  const jitterValEl = document.getElementById('heroJitterVal');
  if (!pingValEl || !jitterValEl) return;

  setInterval(() => {
    // Realistic micro-jitter around 18.8ms
    const base = 18.4;
    const rtt = (base + Math.random() * 0.9).toFixed(1);
    const jitter = (0.28 + Math.random() * 0.2).toFixed(2);
    
    pingValEl.textContent = `${rtt} ms`;
    jitterValEl.textContent = `±${jitter} ms`;
  }, 1600);
}

// 2. Interactive Bufferbloat & Latency Simulator
function initSimulator() {
  const tabs = document.querySelectorAll('.sim-tab');
  const idleRttEl = document.getElementById('simIdleRtt');
  const loadedRttEl = document.getElementById('simLoadedRtt');
  const deltaEl = document.getElementById('simDelta');
  const jitterEl = document.getElementById('simJitter');
  const gradeEl = document.getElementById('simGrade');
  const testBtn = document.getElementById('simRunTestBtn');
  const svgPath = document.getElementById('simSvgPath');
  const svgArea = document.getElementById('simSvgArea');

  // Simulation presets
  const presets = {
    'stock-wifi': {
      label: 'Stock Windows (Wi-Fi)',
      idle: 42.5,
      loaded: 184.2,
      jitter: 18.6,
      grade: 'D',
      gradeColor: '#f87171',
      gradeBg: 'rgba(248, 113, 113, 0.15)',
      gradeBorder: 'rgba(248, 113, 113, 0.35)',
      points: [42, 45, 41, 48, 130, 185, 172, 190, 145, 95, 60, 44]
    },
    'tuned-wifi': {
      label: 'Roblox Tuner (Wi-Fi)',
      idle: 34.2,
      loaded: 36.8,
      jitter: 0.65,
      grade: 'A+',
      gradeColor: '#34d399',
      gradeBg: 'rgba(52, 211, 153, 0.15)',
      gradeBorder: 'rgba(52, 211, 153, 0.35)',
      points: [34, 35, 34, 36, 37, 36, 37, 36, 35, 35, 34, 34]
    },
    'tuned-eth': {
      label: 'Roblox Tuner (Ethernet)',
      idle: 16.4,
      loaded: 17.5,
      jitter: 0.22,
      grade: 'A+',
      gradeColor: '#34d399',
      gradeBg: 'rgba(52, 211, 153, 0.15)',
      gradeBorder: 'rgba(52, 211, 153, 0.35)',
      points: [16, 17, 16, 16, 18, 17, 18, 17, 17, 16, 16, 16]
    }
  };

  let currentPreset = 'tuned-wifi';

  function renderPreset(presetKey, isAnimating = false) {
    const data = presets[presetKey];
    if (!data) return;

    const delta = (data.loaded - data.idle).toFixed(1);

    idleRttEl.textContent = `${data.idle.toFixed(1)} ms`;
    loadedRttEl.textContent = `${data.loaded.toFixed(1)} ms`;
    deltaEl.textContent = `+${delta} ms`;
    jitterEl.textContent = `±${data.jitter.toFixed(2)} ms`;
    gradeEl.textContent = `[ ${data.grade} ]`;
    gradeEl.style.color = data.gradeColor;
    gradeEl.style.backgroundColor = data.gradeBg;
    gradeEl.style.borderColor = data.gradeBorder;

    // Draw SVG Wave
    drawGraph(data.points, data.gradeColor);
  }

  function drawGraph(points, color) {
    if (!svgPath) return;
    const width = 460;
    const height = 150;
    const maxVal = 200;
    const step = width / (points.length - 1);

    const coords = points.map((p, i) => {
      const x = i * step;
      const y = height - (p / maxVal) * (height - 20) - 10;
      return { x, y };
    });

    let d = `M ${coords[0].x} ${coords[0].y}`;
    for (let i = 1; i < coords.length; i++) {
      const prev = coords[i - 1];
      const curr = coords[i];
      const cx = (prev.x + curr.x) / 2;
      d += ` C ${cx} ${prev.y}, ${cx} ${curr.y}, ${curr.x} ${curr.y}`;
    }

    svgPath.setAttribute('d', d);
    svgPath.setAttribute('stroke', color);

    if (svgArea) {
      const areaD = `${d} L ${width} ${height} L 0 ${height} Z`;
      svgArea.setAttribute('d', areaD);
      svgArea.setAttribute('fill', color);
    }
  }

  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      tabs.forEach(t => t.classList.remove('active'));
      tab.classList.add('active');
      currentPreset = tab.dataset.preset;
      renderPreset(currentPreset);
    });
  });

  if (testBtn) {
    testBtn.addEventListener('click', () => {
      testBtn.disabled = true;
      testBtn.innerHTML = `
        <svg class="animate-spin" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="12" cy="12" r="10" stroke-opacity="0.25"></circle>
          <path d="M12 2a10 10 0 0 1 10 10"></path>
        </svg> Simulating 5MB Burst...
      `;

      let progress = 0;
      const interval = setInterval(() => {
        progress += 25;
        if (progress >= 100) {
          clearInterval(interval);
          testBtn.disabled = false;
          testBtn.innerHTML = `
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polygon points="5 3 19 12 5 21 5 3"></polygon>
            </svg> Run Bufferbloat Test
          `;
          renderPreset(currentPreset);
          showToast(`Bufferbloat diagnostic finished: Grade ${presets[currentPreset].grade}`);
        }
      }, 250);
    });
  }

  // Initial draw
  renderPreset(currentPreset);
}

// 3. Clipboard Functionality & Toast Notification
function initClipboard() {
  const copyButtons = document.querySelectorAll('[data-copy]');
  copyButtons.forEach(btn => {
    btn.addEventListener('click', () => {
      const textToCopy = btn.dataset.copy;
      navigator.clipboard.writeText(textToCopy).then(() => {
        const originalText = btn.innerHTML;
        btn.innerHTML = '✓ Copied';
        showToast(`Copied to clipboard: "${textToCopy.length > 28 ? textToCopy.slice(0, 28) + '...' : textToCopy}"`);
        setTimeout(() => {
          btn.innerHTML = originalText;
        }, 2000);
      }).catch(() => {
        showToast('Failed to copy to clipboard');
      });
    });
  });
}

function showToast(message) {
  let toast = document.getElementById('siteToast');
  if (!toast) {
    toast = document.createElement('div');
    toast.id = 'siteToast';
    toast.className = 'toast';
    document.body.appendChild(toast);
  }

  toast.innerHTML = `
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#10b981" stroke-width="2">
      <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
      <polyline points="22 4 12 14.01 9 11.01"></polyline>
    </svg>
    <span>${message}</span>
  `;

  toast.classList.add('show');
  setTimeout(() => {
    toast.classList.remove('show');
  }, 2600);
}

// 4. FAQ Accordion
function initFaqAccordion() {
  const items = document.querySelectorAll('.faq-item');
  items.forEach(item => {
    const header = item.querySelector('.faq-header');
    const body = item.querySelector('.faq-body');
    if (!header || !body) return;

    header.addEventListener('click', () => {
      const isOpen = item.classList.contains('open');
      
      // Close others for clean single-open behavior
      items.forEach(other => {
        if (other !== item) {
          other.classList.remove('open');
          const otherBody = other.querySelector('.faq-body');
          if (otherBody) otherBody.style.maxHeight = null;
        }
      });

      if (isOpen) {
        item.classList.remove('open');
        body.style.maxHeight = null;
      } else {
        item.classList.add('open');
        body.style.maxHeight = body.scrollHeight + 'px';
      }
    });
  });
}

// 5. Dynamic GitHub Release Telemetry
function fetchGitHubReleaseInfo() {
  const repo = 'getsentrix/RBLX-Network-Tuner';
  const apiEndpoint = `https://api.github.com/repos/${repo}/releases/latest`;

  fetch(apiEndpoint)
    .then(res => {
      if (!res.ok) throw new Error('API limit or not found');
      return res.json();
    })
    .then(data => {
      if (data && data.tag_name) {
        const releaseTagPill = document.querySelectorAll('.live-release-tag');
        releaseTagPill.forEach(pill => {
          pill.textContent = data.tag_name;
        });

        // Sum download counts if assets exist
        if (Array.isArray(data.assets)) {
          let totalDownloads = 0;
          data.assets.forEach(asset => {
            totalDownloads += asset.download_count || 0;
          });
          const downloadCountEl = document.getElementById('statDownloads');
          if (downloadCountEl && totalDownloads > 0) {
            downloadCountEl.textContent = `${totalDownloads.toLocaleString()}+`;
          }
        }
      }
    })
    .catch(() => {
      // Graceful offline fallback: keep hardcoded v2.4.6 values
    });
}
