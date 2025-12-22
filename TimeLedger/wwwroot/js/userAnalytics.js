// userAnalytics.js
// アクセスログ分析ページ用。サーバーから埋め込まれた JSON を読み込み、チャート/表を描画して簡易的なフィルタを提供する。

(() => {
    document.addEventListener("DOMContentLoaded", () => {
        const dataEl = document.getElementById("userAnalytics-data");
        if (!dataEl) {
            return;
        }

        let payload;
        try {
            payload = JSON.parse(dataEl.textContent || "{}");
        } catch {
            return;
        }

        updateMetrics(payload.metrics);
        renderBars("uaDailyChart", payload.daily);
        renderBars("uaHourlyChart", payload.hourly);
        renderTopUsers(payload.topUsers);

        const allLogs = Array.isArray(payload.logs) ? payload.logs : [];
        bindLogFilter(allLogs);
        renderLogs(filterLogs(allLogs, "all"));
    });

    function updateMetrics(metrics) {
        if (!metrics) {
            return;
        }

        setText("uaTotalUsers", metrics.totalUsers);
        setText("uaActiveUsers", metrics.activeUsers);
        setText("uaTotalLogs", metrics.totalLogs);
        setText("uaGeneratedAt", metrics.generatedAt);
    }

    function renderBars(targetId, series) {
        const container = document.getElementById(targetId);
        if (!container || !Array.isArray(series)) {
            return;
        }

        const max = Math.max(1, ...series.map(s => Number(getValue(s, "count", "Count")) || 0));
        container.innerHTML = "";

        series.forEach(point => {
            const row = document.createElement("div");
            row.className = "ua-bar-row";

            const label = document.createElement("span");
            label.className = "ua-bar-label";
            label.textContent = getValue(point, "label", "Label") ?? "";

            const track = document.createElement("div");
            track.className = "ua-bar-track";

            const fill = document.createElement("div");
            fill.className = "ua-bar-fill";
            const ratio = Math.min(100, Math.round(((Number(getValue(point, "count", "Count")) || 0) / max) * 100));
            fill.style.width = `${ratio}%`;

            const value = document.createElement("span");
            value.className = "ua-bar-value";
            value.textContent = `${getValue(point, "count", "Count") ?? 0}`;

            track.appendChild(fill);
            row.appendChild(label);
            row.appendChild(track);
            row.appendChild(value);
            container.appendChild(row);
        });
    }

    function renderTopUsers(users) {
        const list = document.getElementById("uaTopUsers");
        if (!list || !Array.isArray(users)) {
            return;
        }

        list.innerHTML = "";
        users.forEach(user => {
            const li = document.createElement("li");

            const name = document.createElement("div");
            name.className = "ua-top-user-name";
            name.textContent = `${getValue(user, "userLabel", "UserLabel") ?? "-"} (${getValue(user, "userId", "UserId") ?? ""})`;

            const meta = document.createElement("div");
            meta.className = "ua-top-user-meta";

            const countChip = document.createElement("span");
            countChip.className = "ua-chip";
            countChip.textContent = `${getValue(user, "accessCount", "AccessCount") ?? 0} 回`;
            meta.appendChild(countChip);

            const lastAccess = getValue(user, "lastAccess", "LastAccessAtUtc");
            if (lastAccess) {
                const last = document.createElement("span");
                last.className = "ua-chip secondary";
                last.textContent = `最終 ${formatTime(lastAccess)}`;
                meta.appendChild(last);
            }

            li.appendChild(name);
            li.appendChild(meta);
            list.appendChild(li);
        });
    }

    function renderLogs(logs) {
        const tbody = document.getElementById("uaLogTbody");
        if (!tbody || !Array.isArray(logs)) {
            return;
        }

        tbody.innerHTML = "";
        logs.forEach(log => {
            const tr = document.createElement("tr");

            tr.appendChild(cell(formatTime(getValue(log, "accessedAtUtc", "AccessedAtUtc"))));

            const userTd = document.createElement("td");
            const userLabel = document.createElement("div");
            userLabel.className = "ua-user-label";
            userLabel.textContent = getValue(log, "userLabel", "UserLabel") ?? "-";
            const userId = document.createElement("div");
            userId.className = "ua-user-id";
            userId.textContent = getValue(log, "userId", "UserId") ?? "";
            userTd.appendChild(userLabel);
            userTd.appendChild(userId);
            tr.appendChild(userTd);

            tr.appendChild(cell(getValue(log, "httpMethod", "HttpMethod") ?? ""));

            const pathTd = document.createElement("td");
            pathTd.className = "ua-path-cell";
            pathTd.textContent = getValue(log, "path", "Path") ?? "";
            tr.appendChild(pathTd);

            tr.appendChild(cell(getValue(log, "statusCode", "StatusCode") ?? "-"));

            const errorTd = document.createElement("td");
            const isError = Boolean(getValue(log, "isError", "IsError"));
            const chip = document.createElement("span");
            chip.className = `ua-chip ${isError ? "error" : "ok"}`;
            const errorType = getValue(log, "errorType", "ErrorType") ?? "Error";
            chip.textContent = isError ? errorType : "OK";
            const errorHash = getValue(log, "errorHash", "ErrorHash");
            if (errorHash) {
                chip.title = `hash:${errorHash}`;
            }
            errorTd.appendChild(chip);
            tr.appendChild(errorTd);

            tr.appendChild(cell(getValue(log, "durationMs", "DurationMs") ?? "-"));

            const agentTd = document.createElement("td");
            const agent = document.createElement("div");
            agent.className = "ua-agent";
            agent.textContent = getValue(log, "userAgent", "UserAgent") ?? "";
            const ip = document.createElement("div");
            ip.className = "ua-ip";
            ip.textContent = getValue(log, "remoteIp", "RemoteIp") ?? "";
            agentTd.appendChild(agent);
            agentTd.appendChild(ip);
            tr.appendChild(agentTd);

            tbody.appendChild(tr);
        });
    }

    function bindLogFilter(allLogs) {
        const buttons = Array.from(document.querySelectorAll(".ua-filter-btn"));
        if (!buttons.length) {
            return;
        }

        const applyFilter = (filter) => {
            buttons.forEach(btn => {
                const isActive = btn.dataset.filter === filter;
                btn.classList.toggle("active", isActive);
                btn.setAttribute("aria-pressed", String(isActive));
            });

            renderLogs(filterLogs(allLogs, filter));
        };

        buttons.forEach(btn => {
            btn.addEventListener("click", () => {
                const filter = btn.dataset.filter || "all";
                applyFilter(filter);
            });
        });

        applyFilter("all");
    }

    function filterLogs(logs, filter) {
        if (!Array.isArray(logs)) {
            return [];
        }

        if (filter === "error") {
            return logs.filter(l => Boolean(getValue(l, "isError", "IsError")));
        }

        if (filter === "ok") {
            return logs.filter(l => !Boolean(getValue(l, "isError", "IsError")));
        }

        return logs;
    }

    function setText(id, value) {
        const el = document.getElementById(id);
        if (el) {
            el.textContent = value ?? "";
        }
    }

    function cell(content) {
        const td = document.createElement("td");
        td.textContent = content ?? "";
        return td;
    }

    function formatTime(value) {
        if (!value) {
            return "-";
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "-";
        }

        return date.toLocaleString("ja-JP", {
            month: "2-digit",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit"
        });
    }

    function getValue(obj, ...keys) {
        for (const key of keys) {
            if (obj && obj[key] !== undefined && obj[key] !== null) {
                return obj[key];
            }
        }

        return undefined;
    }
})();
