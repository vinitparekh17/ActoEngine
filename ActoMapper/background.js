const AUTH_STORAGE_KEY = "extensionAuth";

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
    if (msg.action === "runSearch") {
        runSearch().then(sendResponse);
        return true;
    }
    if (msg.action === "syncMappings") {
        syncMappings(msg.scanResults).then(sendResponse);
        return true;
    }
    if (msg.action === "authenticate") {
        authenticateExtension().then(sendResponse);
        return true;
    }
});

async function runSearch() {
    const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
    const tab = tabs[0];

    if (!tab?.url?.startsWith("http")) {
        return { success: false, message: "Cannot search on this page (not a website)." };
    }

    const config = await chrome.storage.sync.get(["searchDirs"]);
    const searchDirs = config.searchDirs || [];

    const scriptUrls = await getAllScriptUrls(tab.id);
    if (!scriptUrls || scriptUrls.length === 0) {
        return { success: false, message: "No JavaScript files found on this page." };
    }

    const filteredUrls = filterUrlsByDirectories(scriptUrls, searchDirs);
    if (filteredUrls.length === 0 && searchDirs.length > 0) {
        return {
            success: false,
            message: `No JS files found matching configured filters.\nConfigured: ${searchDirs.join(", ")}\nFound ${scriptUrls.length} total JS files.`
        };
    }

    const urlsToSearch = filteredUrls.length > 0 ? filteredUrls : scriptUrls;
    const results = [];

    for (const scriptUrl of urlsToSearch) {
        try {
            const content = await fetch(scriptUrl).then((r) => r.text());
            const matches = findServiceNamePatterns(content);
            if (matches.length > 0) {
                results.push({ file: scriptUrl, matches });
            }
        } catch {
            console.warn("Could not fetch script:", scriptUrl);
        }
    }

    const scanResults = {
        success: true,
        results,
        totalFilesSearched: urlsToSearch.length,
        totalFilesFound: scriptUrls.length
    };

    const syncResult = await syncMappings(scanResults, true);
    return { ...scanResults, syncResult };
}

async function authenticateExtension() {
    try {
        const config = await chrome.storage.sync.get(["apiUrl"]);
        if (!config.apiUrl) {
            return { success: false, message: "ActoEngine API URL not configured. Go to Settings." };
        }

        await ensureAuthenticated(config.apiUrl, true);
        return { success: true, message: "Extension authentication successful." };
    } catch (error) {
        console.error("Authentication error:", error);
        return { success: false, message: error.message || "Authentication failed." };
    }
}

async function syncMappings(scanResults, silent = false) {
    try {
        const config = await chrome.storage.sync.get(["apiUrl", "projectId"]);
        if (!config.apiUrl) {
            return { success: false, message: "ActoEngine API URL not configured. Go to Settings." };
        }
        if (!config.projectId) {
            return { success: false, message: "Project ID not configured. Go to Settings." };
        }

        const detections = buildMappingDetections(scanResults);
        if (detections.length === 0) {
            return { success: true, message: "No mappings found to sync.", syncedCount: 0 };
        }

        const response = await callPrivateApi(
            config.apiUrl,
            `/api/projects/${config.projectId}/mapping-detections`,
            "POST",
            detections,
            true
        );

        if (!response.status) {
            return { success: false, message: response.message || "Mapping sync failed." };
        }

        if (!silent) {
            showNotification("Mappings Synced", "Mappings sent to ActoEngine. Generate patch from frontend Patcher page.");
        }

        return {
            success: true,
            message: response.message || "Mappings synced successfully.",
            syncedCount: detections.length
        };
    } catch (error) {
        console.error("Mapping sync error:", error);
        if (!silent) {
            showNotification("Mapping Sync Failed", error.message || "Unexpected error occurred.");
        }
        return { success: false, message: error.message || "Unexpected error occurred." };
    }
}

function buildMappingDetections(scanResults) {
    if (!scanResults || !scanResults.results) return [];

    const detections = [];
    const seen = new Set();

    for (const result of scanResults.results) {
        const { pageName, domainName } = extractPageAndDomain(result.file);
        for (const match of result.matches || []) {
            const serviceName = (match.serviceName || "").trim();
            if (!serviceName) continue;

            const key = `${domainName}/${pageName}/${serviceName}`;
            if (seen.has(key)) continue;
            seen.add(key);

            detections.push({
                page: pageName,
                domainName,
                storedProcedure: serviceName,
                confidence: 1,
                source: "extension"
            });
        }
    }

    return detections;
}

function extractPageAndDomain(fileUrl) {
    try {
        const url = new URL(fileUrl);
        const parts = url.pathname.split("/").filter((p) => p);
        const fileName = parts[parts.length - 1] || "";
        const pageName = fileName.replace(/\.js(\?.*)?$/i, "");
        const domainName = parts.length >= 2 ? parts[parts.length - 2] : "General";
        return { pageName, domainName };
    } catch {
        return { pageName: fileUrl, domainName: "General" };
    }
}

async function callPrivateApi(baseUrl, endpoint, method, body, interactive) {
    const token = await ensureAuthenticated(baseUrl, interactive);
    const url = baseUrl.replace(/\/$/, "") + endpoint;

    const response = await fetch(url, {
        method,
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
        body: body ? JSON.stringify(body) : undefined
    });

    if (response.status === 401) {
        await refreshAuthToken(baseUrl);
        const retryToken = await ensureAuthenticated(baseUrl, false);
        const retryResponse = await fetch(url, {
            method,
            headers: { "Content-Type": "application/json", Authorization: `Bearer ${retryToken}` },
            body: body ? JSON.stringify(body) : undefined
        });
        const retryData = await retryResponse.json();
        if (!retryResponse.ok) {
            throw new Error(retryData?.message || "API request failed.");
        }
        return retryData;
    }

    const data = await response.json();
    if (!response.ok) {
        throw new Error(data?.message || "API request failed.");
    }
    return data;
}

async function ensureAuthenticated(apiUrl, interactive) {
    const state = await chrome.storage.local.get([AUTH_STORAGE_KEY]);
    const auth = state[AUTH_STORAGE_KEY];

    if (auth?.accessToken && auth?.expiresAt > Date.now() + 30_000) {
        return auth.accessToken;
    }

    if (auth?.refreshToken) {
        try {
            return await refreshAuthToken(apiUrl);
        } catch {
            // ignore and fall back to interactive flow
        }
    }

    if (!interactive) throw new Error("Authentication required.");
    return launchAuthFlow(apiUrl);
}

async function launchAuthFlow(apiUrl) {
    const clientId = chrome.runtime.id;
    const redirectUri = chrome.identity.getRedirectURL("actoengine");
    const state = randomString(24);
    const codeVerifier = randomString(64);
    const codeChallenge = await sha256Base64Url(codeVerifier);

    const authUrl = `${apiUrl.replace(/\/$/, "")}/api/auth/extension/authorize` +
        `?client_id=${encodeURIComponent(clientId)}` +
        `&redirect_uri=${encodeURIComponent(redirectUri)}` +
        `&code_challenge=${encodeURIComponent(codeChallenge)}` +
        `&code_challenge_method=S256` +
        `&state=${encodeURIComponent(state)}`;

    const callbackUrl = await launchWebAuthFlowAsync(authUrl);
    const parsed = new URL(callbackUrl);
    const returnedState = parsed.searchParams.get("state");
    const code = parsed.searchParams.get("code");
    if (!code || returnedState !== state) throw new Error("Invalid authorization response.");

    const tokenResp = await fetch(`${apiUrl.replace(/\/$/, "")}/api/auth/extension/token`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ code, codeVerifier, clientId, redirectUri })
    });
    const payload = await tokenResp.json();
    if (!tokenResp.ok || !payload?.status) throw new Error(payload?.message || "Token exchange failed.");

    const authData = {
        accessToken: payload.data.accessToken,
        refreshToken: payload.data.refreshToken,
        expiresAt: Number(payload.data.expiresAt),
        state,
        codeVerifier,
        clientId
    };
    await chrome.storage.local.set({ [AUTH_STORAGE_KEY]: authData });
    return authData.accessToken;
}

async function refreshAuthToken(apiUrl) {
    const state = await chrome.storage.local.get([AUTH_STORAGE_KEY]);
    const auth = state[AUTH_STORAGE_KEY];
    if (!auth?.refreshToken || !auth?.clientId) throw new Error("No refresh token available.");

    const response = await fetch(`${apiUrl.replace(/\/$/, "")}/api/auth/extension/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: auth.refreshToken, clientId: auth.clientId })
    });

    const payload = await response.json();
    if (!response.ok || !payload?.status) throw new Error(payload?.message || "Token refresh failed.");

    const nextAuth = {
        ...auth,
        accessToken: payload.data.accessToken,
        refreshToken: payload.data.refreshToken,
        expiresAt: Number(payload.data.expiresAt)
    };
    await chrome.storage.local.set({ [AUTH_STORAGE_KEY]: nextAuth });
    return nextAuth.accessToken;
}

function launchWebAuthFlowAsync(url) {
    return new Promise((resolve, reject) => {
        chrome.identity.launchWebAuthFlow({ url, interactive: true }, (callbackUrl) => {
            const err = chrome.runtime.lastError;
            if (err) return reject(new Error(err.message || "Authentication failed."));
            resolve(callbackUrl);
        });
    });
}

function randomString(length) {
    const bytes = new Uint8Array(length);
    crypto.getRandomValues(bytes);
    return Array.from(bytes, (b) => (b % 36).toString(36)).join("");
}

async function sha256Base64Url(input) {
    const data = new TextEncoder().encode(input);
    const hashBuffer = await crypto.subtle.digest("SHA-256", data);
    const bytes = new Uint8Array(hashBuffer);
    let binary = "";
    bytes.forEach((b) => { binary += String.fromCharCode(b); });
    return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function showNotification(title, message) {
    chrome.notifications.create({
        type: "basic",
        iconUrl: "icon48.png",
        title,
        message,
        priority: 2
    });
}

function filterUrlsByDirectories(urls, directories) {
    if (!directories || directories.length === 0) return urls;
    return urls.filter((url) => directories.some((dir) => url.toLowerCase().includes(dir.trim().toLowerCase())));
}

async function getAllScriptUrls(tabId) {
    return new Promise((resolve) => {
        chrome.scripting.executeScript(
            {
                target: { tabId },
                func: () => [...document.querySelectorAll("script[src]")]
                    .map((s) => s.src)
                    .filter((u) => u.endsWith(".js") || u.includes(".js?"))
            },
            (result) => resolve(result?.[0]?.result || [])
        );
    });
}

function findServiceNamePatterns(content) {
    const matches = [];
    const filterRulesByVar = extractStaticFilterRules(content);
    const lines = content.split("\n");

    lines.forEach((line, index) => {
        let match;
        const lineRegex = /ServiceName\s*=\s*([^\s&'"]+)(?:\s*&\s*myfilters\s*=\s*['"]?([^'"\s&]*)['"]?)?/gi;
        while ((match = lineRegex.exec(line)) !== null) {
            const serviceName = match[1];
            let myfilters = match[2] || "N/A";

            if (myfilters === "N/A") {
                const filterVar = findMyfiltersVariable(line);
                if (filterVar && filterRulesByVar.has(filterVar)) {
                    const staticRules = formatStaticRules(filterRulesByVar.get(filterVar));
                    if (staticRules) myfilters = staticRules;
                }
            }

            matches.push({
                lineNumber: index + 1,
                lineContent: line.trim(),
                serviceName,
                myfilters
            });
        }
    });

    return matches;
}

function extractStaticFilterRules(content) {
    const map = new Map();
    const pushRegex = /(\w+)\.rules\.push\s*\(\s*\{([\s\S]*?)\}\s*\)\s*;?/g;
    let match;

    while ((match = pushRegex.exec(content)) !== null) {
        const varName = match[1];
        const body = match[2];
        const field = extractPropValue(body, "field");
        const op = extractPropValue(body, "op");
        const data = extractPropValue(body, "data");
        if (!data || !isStaticLiteral(data)) continue;

        const rule = { field: field ? stripQuotes(field) : "", op: op ? stripQuotes(op) : "", data: parseLiteral(data) };
        if (!map.has(varName)) map.set(varName, []);
        map.get(varName).push(rule);
    }
    return map;
}

function extractPropValue(body, prop) {
    const propRegex = new RegExp(prop + "\\s*:\\s*([^,\\n\\r]+)", "i");
    const match = propRegex.exec(body);
    return match ? match[1].trim() : "";
}

function isStaticLiteral(raw) {
    if (!raw) return false;
    const trimmed = raw.trim();
    return /^['"][^'"]*['"]$/.test(trimmed) || /^-?\d+(\.\d+)?$/.test(trimmed) || /^(true|false|null)$/i.test(trimmed);
}

function parseLiteral(raw) {
    const trimmed = raw.trim();
    if (/^['"]/.test(trimmed)) return stripQuotes(trimmed);
    if (/^(true|false)$/i.test(trimmed)) return trimmed.toLowerCase() === "true";
    if (/^null$/i.test(trimmed)) return null;
    const num = Number(trimmed);
    return Number.isNaN(num) ? trimmed : num;
}

function stripQuotes(value) {
    return value.replace(/^['"]|['"]$/g, "");
}

function findMyfiltersVariable(line) {
    const regex = /myfilters\s*=\s*[^\n]*?JSON\.stringify\s*\(\s*([A-Za-z_$][\w$]*)\s*\)/i;
    const match = regex.exec(line);
    return match ? match[1] : "";
}

function formatStaticRules(rules) {
    if (!rules || rules.length === 0) return "";
    return rules
        .map((r) => {
            const prefixParts = ["WHERE"];
            if (r.op) prefixParts.push(r.op.toUpperCase());
            if (r.field) prefixParts.push(r.field);
            const prefix = prefixParts.join("_");
            return r.data !== undefined ? `${prefix} = ${r.data}` : prefix;
        })
        .join(", ");
}
