const snapshotListEl = document.getElementById("snapshotList");
const fileListEl = document.getElementById("fileList");
const viewerTitleEl = document.getElementById("viewerTitle");
const viewerMetaEl = document.getElementById("viewerMeta");
const viewerContentEl = document.getElementById("viewerContent");
const messageEl = document.getElementById("message");
const fileCountHintEl = document.getElementById("fileCountHint");
const refreshButton = document.getElementById("refreshSnapshots");

let snapshots = [];
let files = [];
let selectedSnapshotId = null;
let selectedFilePath = null;

refreshButton.addEventListener("click", () => loadSnapshots(true));

async function loadSnapshots(force = false) {
  setMessage("スナップショットを読み込み中…");
  try {
    const url = force ? "/api/snapshots?refresh=true" : "/api/snapshots";
    const res = await fetch(url);
    if (!res.ok) throw new Error("スナップショットの取得に失敗しました");
    snapshots = await res.json();
    renderSnapshotList();
    if (snapshots.length === 0) {
      setMessage("有効なスナップショットが見つかりませんでした。");
    } else {
      setMessage(`${snapshots.length} 件のスナップショットを読み込みました。`);
    }
  } catch (err) {
    setMessage(err.message, true);
  }
}

function renderSnapshotList() {
  snapshotListEl.innerHTML = "";
  snapshots.forEach((snapshot) => {
    const li = document.createElement("li");
    li.className = snapshot.id === selectedSnapshotId ? "active" : "";
    const title = document.createElement("div");
    title.className = "title";
    title.textContent = snapshot.name;
    const meta = document.createElement("div");
    meta.className = "meta";
    meta.textContent = `${formatDate(snapshot.lastModifiedUtc)} / ${formatBytes(snapshot.totalSizeBytes)} / ${snapshot.fileCount} files`;
    li.append(title, meta);
    li.addEventListener("click", () => selectSnapshot(snapshot));
    snapshotListEl.appendChild(li);
  });
}

async function selectSnapshot(snapshot) {
  if (!snapshot) return;
  selectedSnapshotId = snapshot.id;
  selectedFilePath = null;
  viewerTitleEl.textContent = snapshot.name;
  viewerMetaEl.textContent = snapshot.fullPath;
  viewerContentEl.innerHTML = `<p class="muted">${snapshot.name} のファイルを選択してください。</p>`;
  renderSnapshotList();
  await loadFiles(snapshot.id);
}

async function loadFiles(snapshotId) {
  fileListEl.innerHTML = "";
  fileCountHintEl.textContent = "";
  if (!snapshotId) return;
  try {
    const res = await fetch(`/api/snapshots/${snapshotId}/files`);
    if (!res.ok) throw new Error("ファイル一覧の取得に失敗しました");
    files = await res.json();
    renderFileList();
  } catch (err) {
    setMessage(err.message, true);
  }
}

function renderFileList() {
  fileListEl.innerHTML = "";
  fileCountHintEl.textContent = files.length ? `${files.length} files` : "";
  files.forEach((file) => {
    const li = document.createElement("li");
    li.className = file.path === selectedFilePath ? "active" : "";
    const title = document.createElement("div");
    title.className = "title";
    title.textContent = file.path;
    const meta = document.createElement("div");
    meta.className = "meta";
    meta.textContent = `${formatBytes(file.sizeBytes)} / ${formatDate(file.lastModifiedUtc)}`;
    li.append(title, meta);
    li.addEventListener("click", () => selectFile(file));
    fileListEl.appendChild(li);
  });
}

async function selectFile(file) {
  if (!selectedSnapshotId) {
    setMessage("先にスナップショットを選択してください。", true);
    return;
  }
  selectedFilePath = file.path;
  renderFileList();
  viewerTitleEl.textContent = file.path;
  viewerMetaEl.textContent = `${formatBytes(file.sizeBytes)} / ${formatDate(file.lastModifiedUtc)}`;
  viewerContentEl.innerHTML = "<p class=\"muted\">読み込み中...</p>";

  try {
    const res = await fetch(`/api/snapshots/${selectedSnapshotId}/file?path=${encodeURIComponent(file.path)}`);
    if (!res.ok) throw new Error("ファイルの取得に失敗しました");
    const content = await res.json();
    renderViewer(content);
  } catch (err) {
    setMessage(err.message, true);
    viewerContentEl.innerHTML = `<p class="muted">${err.message}</p>`;
  }
}

function renderViewer(content) {
  if (content.html) {
    viewerContentEl.innerHTML = content.html;
  } else if (content.text) {
    viewerContentEl.innerHTML = `<pre>${escapeHtml(content.text)}</pre>`;
  } else {
    viewerContentEl.innerHTML = "<p class=\"muted\">コンテンツが空です。</p>";
  }

  if (content.isTruncated) {
    const notice = document.createElement("p");
    notice.className = "muted";
    notice.textContent = "プレビュー制限のためファイルの一部のみを表示しています。";
    viewerContentEl.appendChild(notice);
  }
}

function setMessage(text, isError = false) {
  messageEl.textContent = text || "";
  if (isError) {
    messageEl.classList.add("error");
  } else {
    messageEl.classList.remove("error");
  }
}

function formatDate(value) {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
}

function formatBytes(bytes) {
  if (!Number.isFinite(bytes) || bytes <= 0) return "0 B";
  const units = ["B", "KB", "MB", "GB"];
  let unit = 0;
  let value = bytes;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${value.toFixed(1)} ${units[unit]}`;
}

function escapeHtml(text) {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

loadSnapshots();
