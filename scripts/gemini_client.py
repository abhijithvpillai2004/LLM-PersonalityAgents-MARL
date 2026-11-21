"""
scripts/gemini_client.py
Robust shared client for calling Gemini 3.8 Flash offline via REST API.
"""

import os
import json
import time
import urllib.request
from dotenv import load_dotenv

load_dotenv()

def call_gemini(prompt: str, json_mode: bool = True, timeout: int = 120, retries: int = 3) -> str:
    """
    Sends prompt to Gemini 3.8 Flash via REST API.
    Reads GEMINI_API_KEY from environment only. Never prints or logs the key.
    """
    api_key = os.environ.get("GEMINI_API_KEY")
    if not api_key:
        raise ValueError("GEMINI_API_KEY environment variable is not set. Cannot run LLM reward designer.")

    url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent?key={api_key}"
    headers = {"Content-Type": "application/json"}
    
    body = {
        "contents": [{"parts": [{"text": prompt}]}],
    }
    if json_mode:
        body["generationConfig"] = {"response_mime_type": "application/json"}

    data_bytes = json.dumps(body).encode("utf-8")

    last_error = None
    for attempt in range(retries):
        try:
            print(f"[GeminiClient] Calling Gemini 3.8 Flash (attempt {attempt + 1}/{retries}, timeout {timeout}s)...", flush=True)
            t0 = time.time()
            req = urllib.request.Request(url, data=data_bytes, headers=headers)
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                resp_data = json.loads(resp.read().decode("utf-8"))
                candidates = resp_data.get("candidates", [])
                if not candidates:
                    raise ValueError(f"No candidates returned by Gemini: {resp_data}")
                text = candidates[0]["content"]["parts"][0]["text"]
                elapsed = time.time() - t0
                print(f"[GeminiClient] Response received in {elapsed:.1f}s.", flush=True)
                return text.strip()
        except Exception as e:
            last_error = e
            print(f"[GeminiClient] Attempt {attempt + 1} failed: {e}. Retrying in {(attempt + 1) * 3}s...", flush=True)
            time.sleep(3.0 * (attempt + 1))

    raise RuntimeError(f"Gemini API request failed after {retries} retries: {last_error}")

if __name__ == "__main__":
    print("[gemini_client] Testing connectivity...", flush=True)
    out = call_gemini("Reply with JSON: {\"status\": \"connected\"}")
    print("[gemini_client] Success:", out, flush=True)
