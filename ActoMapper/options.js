const AUTH_STORAGE_KEY = "extensionAuth";

chrome.storage.sync.get(["searchDirs", "apiUrl", "projectId"], (result) => {
    const searchDirs = result.searchDirs || [];
    document.getElementById("searchDirs").value = searchDirs.join("\n");
    document.getElementById("apiUrl").value = result.apiUrl || "";
    document.getElementById("projectId").value = result.projectId || "";
});

refreshAuthStatus();

document.getElementById("saveBtn").addEventListener("click", () => {
    const textarea = document.getElementById("searchDirs");
    const btn = document.getElementById("saveBtn");

    const dirs = textarea.value
        .split("\n")
        .map((line) => line.trim())
        .filter((line) => line.length > 0);

    const apiUrl = document.getElementById("apiUrl").value.trim();
    const projectId = parseInt(document.getElementById("projectId").value, 10) || 0;

    btn.disabled = true;
    btn.textContent = "Saving...";

    chrome.storage.sync.set({ searchDirs: dirs, apiUrl, projectId }, () => {
        const status = document.getElementById("status");
        btn.disabled = false;
        btn.textContent = "Save Configuration";
        status.classList.add("visible");
        setTimeout(() => status.classList.remove("visible"), 3000);
    });
});

document.getElementById("authBtn")?.addEventListener("click", () => {
    const authBtn = document.getElementById("authBtn");
    const authStatus = document.getElementById("authStatus");
    authBtn.disabled = true;
    authBtn.textContent = "Authenticating...";

    chrome.runtime.sendMessage({ action: "authenticate" }, (response) => {
        authBtn.disabled = false;
        authBtn.textContent = "Authenticate Extension";

        if (!response?.success) {
            authStatus.textContent = response?.message || "Authentication failed.";
            authStatus.style.color = "#b91c1c";
            return;
        }

        authStatus.textContent = "Authenticated";
        authStatus.style.color = "#166534";
        refreshAuthStatus();
    });
});

function refreshAuthStatus() {
    chrome.storage.local.get([AUTH_STORAGE_KEY], (result) => {
        const authStatus = document.getElementById("authStatus");
        const auth = result[AUTH_STORAGE_KEY];
        if (auth?.accessToken && auth?.expiresAt && auth.expiresAt > Date.now()) {
            authStatus.textContent = `Authenticated (expires ${new Date(auth.expiresAt).toLocaleString()})`;
            authStatus.style.color = "#166534";
            return;
        }
        authStatus.textContent = "Not authenticated";
        authStatus.style.color = "#92400e";
    });
}
