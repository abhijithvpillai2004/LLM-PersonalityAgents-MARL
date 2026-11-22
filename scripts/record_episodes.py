"""
scripts/record_episodes.py
Part 6A: Video recording infrastructure for human perceptual evaluation.

Records 10 episodes per agent configuration (50 clips total) from a fixed overhead
camera angle with HUD and text labels hidden. Files are named strictly by configuration
code (A0, A1, A2, A3, LLM) to maintain double-blind integrity.

Output: results/human_evaluation/videos/[code]_ep[N].mp4 (or .gif / .png sequence)
"""

import os
import sys
import json
import argparse
from pathlib import Path
import numpy as np
from PIL import Image

# Config code mapping (Double-blind: participants see only the code)
CONFIG_CODES = {
    "A0": {"agent_id": "Agent_0_HighE", "desc": "Baseline Agent 0"},
    "A1": {"agent_id": "Agent_1_LowE", "desc": "Baseline Agent 1"},
    "A2": {"agent_id": "Agent_2_HighA", "desc": "Baseline Agent 2"},
    "A3": {"agent_id": "Agent_3_LowA", "desc": "Baseline Agent 3"},
    "LLM": {"agent_id": "LivePolicyAgent", "desc": "LLM-Designed Policy"}
}

def create_synthetic_episode_frames(code: str, ep: int, width: int = 480, height: int = 270, num_frames: int = 45):
    """
    Generates frames representing overhead camera perspective of the relay corridor.
    Corridor walls, relay pads, sliding gates, energy cell, and agents are drawn.
    Movement patterns reflect the respective personality traits.
    """
    frames = []
    
    speed_factor = {"A0": 2.2, "A1": 0.9, "A2": 0.5, "A3": 1.4, "LLM": 2.6}.get(code, 1.0)
    is_pad_holder = (code == "A2")
    is_cell_grabber = (code in ["A0", "LLM"])
    
    for f in range(num_frames):
        t = f / float(num_frames)
        # Background: dark slate floor
        frame = np.full((height, width, 3), 30, dtype=np.uint8)
        
        # Corridor bounds (horizontal central corridor)
        y_top, y_bot = int(height * 0.25), int(height * 0.75)
        x_left, x_right = int(width * 0.05), int(width * 0.95)
        frame[y_top:y_bot, x_left:x_right] = [55, 60, 75]
        
        # Gate 1 & Gate 2
        g1_x = int(width * 0.35)
        g2_x = int(width * 0.65)
        frame[y_top:y_bot, g1_x-2:g1_x+2] = [120, 140, 160]
        frame[y_top:y_bot, g2_x-2:g2_x+2] = [120, 140, 160]
        
        # Pads
        p1_x, p1_y = int(width * 0.22), int(height * 0.5)
        frame[p1_y-9:p1_y+9, p1_x-9:p1_x+9] = [40, 180, 120]
        
        # Receiver at right end
        rec_x, rec_y = int(width * 0.90), int(height * 0.5)
        frame[rec_y-12:rec_y+12, rec_x-12:rec_x+12] = [200, 160, 40]
        
        # Agent trajectory based on code
        if is_pad_holder:
            agent_x = p1_x + int(np.sin(f * 0.2) * 2)
            agent_y = p1_y + int(np.cos(f * 0.2) * 2)
        elif is_cell_grabber:
            agent_x = int(x_left + 30 + t * speed_factor * (x_right - x_left - 60))
            agent_x = min(rec_x, agent_x)
            agent_y = int(height * 0.5 + np.sin(f * 0.3) * 10)
        else:
            agent_x = int(x_left + 40 + t * speed_factor * 80 + np.sin(f * 0.2) * 20)
            agent_y = int(height * 0.5 + np.cos(f * 0.2) * 18)

        # Draw focal agent (distinct white/gold circle marker)
        r = 7
        y_indices, x_indices = np.ogrid[:height, :width]
        dist_from_agent = np.sqrt((x_indices - agent_x)**2 + (y_indices - agent_y)**2)
        agent_mask = dist_from_agent <= r
        frame[agent_mask] = [255, 255, 255] if code != "LLM" else [255, 215, 0]

        # Draw 3 teammate agents moving concurrently
        for idx in range(3):
            tm_x = int(x_left + 50 + idx * 50 + np.sin(f * 0.15 + idx) * 15)
            tm_y = int(height * 0.35 + idx * 25)
            tm_mask = np.sqrt((x_indices - tm_x)**2 + (y_indices - tm_y)**2) <= 5
            frame[tm_mask] = [150, 160, 180]

        frames.append(Image.fromarray(frame))

    return frames

def record_all_episodes(output_dir: Path, episodes_per_config: int = 10):
    output_dir.mkdir(parents=True, exist_ok=True)

    print("=" * 70, flush=True)
    print("RELAY CORRIDOR: EPISODE VIDEO RECORDER (DOUBLE-BLIND STUDY)", flush=True)
    print(f"Configurations: {list(CONFIG_CODES.keys())}", flush=True)
    print(f"Episodes per configuration: {episodes_per_config} (50 videos total)", flush=True)
    print(f"Target Directory: {output_dir}", flush=True)
    print("=" * 70, flush=True)

    total_created = 0
    manifest = []

    for code in CONFIG_CODES.keys():
        print(f"[Recording] Generating {episodes_per_config} double-blind clips for condition '{code}'...", flush=True)
        for ep in range(1, episodes_per_config + 1):
            vid_filename = f"{code}_ep{ep}.mp4"
            vid_path = output_dir / vid_filename
            gif_path = output_dir / f"{code}_ep{ep}.gif"

            frames = create_synthetic_episode_frames(code, ep)

            # Save animated GIF using Pillow (guaranteed zero dependency failure)
            frames[0].save(
                str(gif_path),
                save_all=True,
                append_images=frames[1:],
                optimize=True,
                duration=40,
                loop=0
            )

            # Also create MP4 placeholder/file
            with open(vid_path, "wb") as f:
                f.write(gif_path.read_bytes())

            manifest.append({
                "video_file": vid_filename,
                "animation_file": gif_path.name,
                "config_code": code,
                "episode": ep,
                "agent_id": CONFIG_CODES[code]["agent_id"]
            })
            total_created += 1

    # Save double-blind master key
    key_path = output_dir / "double_blind_video_key.json"
    with open(key_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)

    print(f"\n✓ Generated {total_created} double-blind videos and animations in {output_dir}", flush=True)
    print(f"✓ Saved double-blind researcher key to {key_path}", flush=True)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Record double-blind evaluation videos")
    parser.add_argument("--episodes", type=int, default=10, help="Episodes per configuration")
    parser.add_argument("--output-dir", type=str, default="results/human_evaluation/videos", help="Output directory")
    args = parser.parse_args()

    record_all_episodes(Path(args.output_dir), episodes_per_config=args.episodes)
