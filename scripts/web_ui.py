"""
scripts/web_ui.py
Option B: Local web UI for LLM-assisted reward design pipeline (Flask + SSE).

Runs at http://localhost:7860.
Provides real-time pipeline event streaming, architecture badge, persona cards,
coefficient JSON viewer, and iteration progress trackers.
"""

import os
import sys
import json
import queue
import threading
import time
from flask import Flask, Response, request, render_template_string, jsonify

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
try:
    from scripts.llm_reward_loop import run_pipeline
except ImportError:
    from llm_reward_loop import run_pipeline

app = Flask(__name__)
event_queue = queue.Queue()
pipeline_thread = None
is_running = False

HTML_TEMPLATE = """<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>RelayCorridor — LLM-Assisted Policy Generator</title>
    <style>
        :root {
            --bg: #0f1117;
            --surface: #1a1d27;
            --border: #2a2f3f;
            --accent: #f59e0b;
            --primary: #3b82f6;
            --success: #10b981;
            --text: #e2e8f0;
            --muted: #94a3b8;
        }
        body {
            margin: 0; padding: 24px;
            background: var(--bg); color: var(--text);
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
            line-height: 1.5;
        }
        .container { max-width: 1100px; margin: 0 auto; display: flex; flex-direction: column; gap: 20px; }
        header { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--border); padding-bottom: 16px; }
        h1 { margin: 0; font-size: 24px; font-weight: 700; color: #fff; display: flex; align-items: center; gap: 10px; }
        .badge { background: #3b82f622; color: #60a5fa; border: 1px solid #3b82f644; padding: 4px 10px; border-radius: 999px; font-size: 12px; font-weight: 600; }
        
        /* Architecture Notice (Non-negotiable) */
        .architecture-banner {
            background: #1e293b; border-left: 4px solid var(--accent); padding: 14px 18px; border-radius: 6px; font-size: 13.5px; color: #cbd5e1;
        }
        .architecture-banner strong { color: var(--accent); }

        .main-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
        .card { background: var(--surface); border: 1px solid var(--border); border-radius: 8px; padding: 18px; display: flex; flex-direction: column; gap: 14px; }
        .card-title { font-size: 15px; font-weight: 600; color: #fff; margin: 0; display: flex; justify-content: space-between; align-items: center; }

        textarea {
            width: 100%; box-sizing: border-box; height: 95px; background: #0b0d13; border: 1px solid var(--border);
            border-radius: 6px; color: var(--text); padding: 12px; font-size: 14px; resize: vertical; outline: none;
        }
        textarea:focus { border-color: var(--primary); }

        .btn-row { display: flex; gap: 10px; }
        button {
            padding: 10px 18px; border-radius: 6px; font-size: 14px; font-weight: 600; cursor: pointer; border: none; transition: 0.15s;
        }
        .btn-primary { background: var(--primary); color: #fff; }
        .btn-primary:hover { background: #2563eb; }
        .btn-primary:disabled { background: #475569; cursor: not-allowed; }
        .btn-secondary { background: #334155; color: #e2e8f0; }
        .btn-secondary:hover { background: #475569; }

        /* Status & Log */
        .log-box {
            background: #090b10; border: 1px solid var(--border); border-radius: 6px; padding: 12px;
            height: 340px; overflow-y: auto; font-family: "JetBrains Mono", Consolas, monospace; font-size: 12.5px;
            display: flex; flex-direction: column; gap: 6px;
        }
        .log-line { line-height: 1.4; color: #cbd5e1; }
        .log-stage { color: var(--accent); font-weight: 600; margin-right: 6px; }

        /* Persona & Coefficient Cards */
        .trait-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; font-size: 13px; }
        .trait-item { background: #11141d; padding: 8px 12px; border-radius: 4px; border: 1px solid #232736; }
        .trait-label { color: var(--muted); font-size: 11px; text-transform: uppercase; }
        .trait-val { font-weight: 600; color: #fff; }

        pre { margin: 0; background: #090b10; padding: 12px; border-radius: 6px; font-size: 12px; overflow-x: auto; color: #38bdf8; }
        
        .score-pill { font-size: 16px; font-weight: 700; color: #fff; background: #10b98122; border: 1px solid #10b98155; padding: 4px 12px; border-radius: 6px; }
        .progress-bar-bg { width: 100%; height: 8px; background: #242938; border-radius: 4px; overflow: hidden; }
        .progress-bar-fill { width: 0%; height: 100%; background: var(--primary); transition: width 0.3s; }
    </style>
</head>
<body>
    <div class="container">
        <header>
            <h1>RelayCorridor <span class="badge">LLM-Assisted Reward Design</span></h1>
            <div id="statusBadge" class="badge" style="background:#10b98122; color:#34d399; border-color:#10b98144;">SYSTEM READY</div>
        </header>

        <div class="architecture-banner">
            <strong>How this works:</strong> The LLM reads your description and designs reward coefficients. A standard PPO policy is trained with those coefficients. The LLM is <em>not the agent</em> — it is an offline reward designer. The trained PPO policy runs live in Unity with zero LLM calls at runtime.
        </div>

        <div class="main-grid">
            <!-- Left Column: Input and Log -->
            <div style="display:flex; flex-direction:column; gap:20px;">
                <div class="card">
                    <h3 class="card-title">1. Describe Agent Personality</h3>
                    <textarea id="promptInput" placeholder="e.g. Reckless Charger: extremely fast, impatient, pushes teammates out of the way to grab the energy cell and deliver it immediately.">Reckless Charger: extremely fast, impatient, pushes teammates out of the way to grab the energy cell and deliver it immediately.</textarea>
                    <div class="btn-row">
                        <button id="btnGenerate" class="btn-primary" onclick="startGeneration()">Generate Policy</button>
                        <button id="btnNew" class="btn-secondary" onclick="resetUI()">New Persona</button>
                    </div>
                </div>

                <div class="card" style="flex:1;">
                    <h3 class="card-title">Pipeline Live Event Stream</h3>
                    <div id="logBox" class="log-box"></div>
                </div>
            </div>

            <!-- Right Column: Persona Card & Live Inspection -->
            <div style="display:flex; flex-direction:column; gap:20px;">
                <div class="card">
                    <h3 class="card-title">
                        <span>Extracted Persona Profile</span>
                        <span id="personaNameBadge" style="color:var(--accent); font-size:13px;">Awaiting input</span>
                    </h3>
                    <div class="trait-grid">
                        <div class="trait-item"><div class="trait-label">Extraversion</div><div id="tExtraversion" class="trait-val">-</div></div>
                        <div class="trait-item"><div class="trait-label">Agreeableness</div><div id="tAgreeableness" class="trait-val">-</div></div>
                        <div class="trait-item"><div class="trait-label">Speed Preference</div><div id="tSpeed" class="trait-val">-</div></div>
                        <div class="trait-item"><div class="trait-label">Pad Holding</div><div id="tPad" class="trait-val">-</div></div>
                        <div class="trait-item"><div class="trait-label">Push Tolerance</div><div id="tPush" class="trait-val">-</div></div>
                        <div class="trait-item"><div class="trait-label">Exploration</div><div id="tExploration" class="trait-val">-</div></div>
                    </div>
                    <div style="font-size:13px; color:var(--muted);" id="personaSummaryText"></div>
                </div>

                <div class="card">
                    <h3 class="card-title">
                        <span>Iteration Tracker</span>
                        <span id="scoreDisplay" class="score-pill">Score: -/10</span>
                    </h3>
                    <div style="display:flex; justify-content:space-between; font-size:13px;">
                        <span id="iterLabel">Iteration: 0/10</span>
                        <span id="stepLabel">PPO Steps: 0</span>
                    </div>
                    <div class="progress-bar-bg"><div id="stepProgress" class="progress-bar-fill"></div></div>
                </div>

                <div class="card" style="flex:1;">
                    <h3 class="card-title">Active Reward Coefficients (llm_current.json)</h3>
                    <pre id="coeffDisplay">{\n  "status": "Awaiting pipeline start"\n}</pre>
                </div>
            </div>
        </div>
    </div>

    <script>
        const evtSource = new EventSource("/stream");
        const logBox = document.getElementById("logBox");

        evtSource.onmessage = function(event) {
            const data = JSON.parse(event.data);
            appendLog(data.stage, data.message);

            if (data.stage === "parsing" && data.data) {
                document.getElementById("personaNameBadge").textContent = data.data.persona_name;
                document.getElementById("tExtraversion").textContent = data.data.extraversion;
                document.getElementById("tAgreeableness").textContent = data.data.agreeableness;
                document.getElementById("tSpeed").textContent = data.data.speed_preference;
                document.getElementById("tPad").textContent = data.data.pad_holding_tendency;
                document.getElementById("tPush").textContent = data.data.push_tolerance;
                document.getElementById("tExploration").textContent = data.data.exploration_tendency;
                document.getElementById("personaSummaryText").textContent = data.data.natural_language_summary;
            }

            if (data.stage === "coefficients" && data.data) {
                document.getElementById("coeffDisplay").textContent = JSON.stringify(data.data, null, 2);
            }

            if (data.stage === "critique" && data.data) {
                if (data.data.score) {
                    document.getElementById("scoreDisplay").textContent = `Score: ${data.data.score}/10`;
                    document.getElementById("scoreDisplay").style.borderColor = data.data.score >= 8 ? "#10b981" : "#f59e0b";
                }
                if (data.data.iteration !== undefined) {
                    document.getElementById("iterLabel").textContent = `Iteration: ${data.data.iteration + 1}/10`;
                }
            }

            if (data.stage === "converged") {
                document.getElementById("statusBadge").textContent = "✓ CONVERGED & TRAINED";
                document.getElementById("statusBadge").style.background = "#10b98144";
                document.getElementById("btnGenerate").disabled = false;
            }

            if (data.stage === "spawning") {
                document.getElementById("statusBadge").textContent = "LIVE IN UNITY";
            }
        };

        function appendLog(stage, msg) {
            const line = document.createElement("div");
            line.className = "log-line";
            line.innerHTML = `<span class="log-stage">[${stage.toUpperCase()}]</span> ${msg}`;
            logBox.appendChild(line);
            logBox.scrollTop = logBox.scrollHeight;
        }

        function startGeneration() {
            const text = document.getElementById("promptInput").value;
            document.getElementById("btnGenerate").disabled = true;
            document.getElementById("statusBadge").textContent = "DESIGNING REWARD...";
            fetch("/generate", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ description: text, fast: true })
            });
        }

        function resetUI() {
            logBox.innerHTML = "";
            document.getElementById("statusBadge").textContent = "SYSTEM READY";
            document.getElementById("scoreDisplay").textContent = "Score: -/10";
            document.getElementById("btnGenerate").disabled = false;
            document.getElementById("coeffDisplay").textContent = "{\\n  \"status\": \"Awaiting pipeline start\"\\n}";
        }
    </script>
</body>
</html>
"""

@app.route("/")
def index():
    return render_template_string(HTML_TEMPLATE)

@app.route("/stream")
def sse_stream():
    def event_stream():
        while True:
            try:
                item = event_queue.get(timeout=20.0)
                yield f"data: {json.dumps(item)}\n\n"
            except queue.Empty:
                yield ": keep-alive\n\n"
    return Response(event_stream(), mimetype="text/event-stream")

@app.route("/generate", methods=["POST"])
def generate():
    global pipeline_thread, is_running
    if is_running:
        return jsonify({"status": "error", "message": "Pipeline already running"}), 400

    data = request.json or {}
    description = data.get("description", "Reckless Charger").strip()
    fast = data.get("fast", True)

    def worker():
        global is_running
        is_running = True
        try:
            def callback(stage, message, d):
                event_queue.put({"stage": stage, "message": message, "data": d})

            run_pipeline(description, progress_callback=callback, fast_mode=fast)
        finally:
            is_running = False

    pipeline_thread = threading.Thread(target=worker, daemon=True)
    pipeline_thread.start()
    return jsonify({"status": "started", "description": description})

if __name__ == "__main__":
    port = 7860
    print(f"[WebUI] Starting RelayCorridor LLM Policy Generator on http://localhost:{port}")
    app.run(host="0.0.0.0", port=port, debug=False)
