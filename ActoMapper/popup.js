let lastScanResults = null;

document.getElementById("searchBtn").addEventListener("click", async () => {
    const btn = document.getElementById("searchBtn");
    const syncBtn = document.getElementById("patchBtn");
    const output = document.getElementById("output");

    syncBtn.disabled = true;
    lastScanResults = null;

    btn.disabled = true;
    const originalText = btn.innerHTML;
    btn.innerHTML = `<span class="spinner"></span> Analyzing...`;
    output.innerHTML = '<div class="status-box status-info">Scanning JavaScript files...</div>';

    chrome.runtime.sendMessage({ action: "runSearch" }, (response) => {
        btn.disabled = false;
        btn.innerHTML = originalText;

        if (!response?.success) {
            output.innerHTML = `<div class="status-box status-error">${response?.message || "Scan failed."}</div>`;
            return;
        }

        if (response.results.length === 0) {
            output.innerHTML = `
                <div class="status-box status-info">
                    <strong>Scan Complete</strong>
                    <span>Checked ${response.totalFilesSearched} JS files (${response.totalFilesFound} found on page).</span>
                </div>
                <div class="status-box status-empty">
                    No patterns found matching:<br>
                    <code>ServiceName=[word]&myfilters=[value]</code>
                </div>
            `;
            return;
        }

        lastScanResults = response;
        syncBtn.disabled = false;

        const totalMatches = response.results.reduce((sum, r) => sum + r.matches.length, 0);
        let html = `
            <div class="status-box status-info" style="background-color: #f0fdf4; color: #166534; border-color: #bbf7d0;">
                <strong>Success</strong>
                <span>Found ${totalMatches} matches in ${response.results.length} files.</span>
            </div>
        `;

        if (response.syncResult?.success) {
            html += `
                <div class="status-box status-info">
                    <strong>Mappings Synced</strong>
                    <span>${response.syncResult.syncedCount || 0} mappings pushed to ActoEngine.</span>
                </div>
            `;
        }

        response.results.forEach((result) => {
            html += `<div class="result-card">`;
            html += `
                <div class="file-header">
                    <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round"><path d="M13 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z"></path><polyline points="13 2 13 9 20 9"></polyline></svg>
                    ${getFileName(result.file)}
                </div>`;

            result.matches.forEach((match) => {
                html += `<div class="match-item">`;
                html += `
                    <div class="match-row">
                        <span class="label">Service:</span>
                        <span class="value">${escapeHtml(match.serviceName)}</span>
                    </div>`;
                html += `
                    <div class="match-row">
                        <span class="label">MyFilters:</span>
                        <span class="value" style="color: ${match.myfilters === "N/A" ? "#94a3b8" : "#059669"}">${escapeHtml(match.myfilters)}</span>
                    </div>`;
                html += `<div class="line-info">Line ${match.lineNumber}</div>`;
                html += `</div>`;
            });

            html += `</div>`;
        });

        output.innerHTML = html;
    });
});

document.getElementById("patchBtn").addEventListener("click", async () => {
    if (!lastScanResults) return;

    const syncBtn = document.getElementById("patchBtn");
    const output = document.getElementById("output");
    syncBtn.disabled = true;
    const originalText = syncBtn.innerHTML;
    syncBtn.innerHTML = `<span class="spinner"></span> Syncing...`;

    const syncStatus = document.createElement("div");
    syncStatus.className = "status-box status-info";
    syncStatus.innerHTML = "<strong>Syncing Mappings</strong><span>Sending detections to ActoEngine...</span>";
    output.prepend(syncStatus);

    chrome.runtime.sendMessage({ action: "syncMappings", scanResults: lastScanResults }, (response) => {
        syncBtn.disabled = false;
        syncBtn.innerHTML = originalText;

        if (!response?.success) {
            syncStatus.className = "status-box status-error";
            syncStatus.innerHTML = `<strong>Sync Failed</strong><span>${response?.message || "Unknown error"}</span>`;
            return;
        }

        syncStatus.className = "status-box status-patch";
        syncStatus.innerHTML = `
            <strong>Mappings Synced!</strong>
            <span>${response.syncedCount || 0} mappings uploaded.</span>
            <span style="font-size:11px;">Generate patch from frontend: /project/{id}/patcher</span>
        `;
    });
});

document.getElementById("openSettings").addEventListener("click", (e) => {
    e.preventDefault();
    chrome.runtime.openOptionsPage();
});

function getFileName(url) {
    try {
        const urlObj = new URL(url);
        return urlObj.pathname.split("/").pop() || url;
    } catch {
        return url;
    }
}

function escapeHtml(text) {
    if (!text) return "";
    const div = document.createElement("div");
    div.textContent = text;
    return div.innerHTML;
}
