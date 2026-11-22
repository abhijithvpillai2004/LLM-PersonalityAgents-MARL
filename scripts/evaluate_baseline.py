"""
Baseline Evaluation Analysis Module (Paradigm A: Hand-Designed MDPI Baseline).

Evaluates 30 episodes of telemetry recorded in results/hand_designed/eval_baseline.csv.
Computes per-agent metrics, statistical comparisons, validates acceptance criteria,
and outputs a Markdown report and visual summary chart.

Usage:
    python scripts/evaluate_baseline.py
    python scripts/evaluate_baseline.py --csv path/to/eval.csv --output-dir results/hand_designed
"""

import argparse
import os
import sys
from pathlib import Path
import numpy as np
import pandas as pd
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

def evaluate_baseline(csv_path: Path, output_dir: Path):
    if not csv_path.exists():
        print(f"[Error] Telemetry file not found: {csv_path}")
        print("Please run 30 evaluation episodes in Unity with CorridorTelemetryLogger enabled first.")
        sys.exit(1)

    df = pd.read_csv(csv_path)
    print(f"[Evaluate] Loaded {len(df)} records across {df['episode'].nunique()} episodes.")

    output_dir.mkdir(parents=True, exist_ok=True)

    # 1. Summary Statistics per Agent / Personality
    metrics = ["mean_speed", "pad_hold_steps", "cell_carry_steps", "push_count"]
    agg_funcs = ["mean", "std", "min", "max"]
    summary_df = df.groupby(["agent_id", "personality"])[metrics].agg(agg_funcs).round(3)

    print("\n" + "="*80)
    print("RELAY CORRIDOR: BASELINE EVALUATION (PARADIGM A - 30 EPISODES)")
    print("="*80)

    # Calculate overall episode metrics
    ep_df = df.groupby("episode").first()
    total_episodes = len(ep_df)
    delivery_count = ep_df["delivered"].astype(str).str.lower().eq("true").sum()
    delivery_rate = (delivery_count / total_episodes) * 100.0 if total_episodes > 0 else 0.0
    mean_ep_length = ep_df["episode_length"].mean()

    print(f"Total Episodes:        {total_episodes}")
    print(f"Team Deliveries:       {delivery_count}/{total_episodes} ({delivery_rate:.1f}%)")
    print(f"Mean Episode Length:   {mean_ep_length:.1f} steps")
    print("-" * 80)

    agent_means = df.groupby("agent_id")[metrics].mean()
    print("Per-Agent Mean Metrics:")
    print(agent_means.to_string())

    # 2. Acceptance Criteria Verification
    # Acceptance criteria from brief:
    # - Agent 0 High-E: highest mean_speed
    # - Agent 1 Low-E: lowest mean_speed, highest pad_hold_steps
    # - Agent 2 High-A: highest pad_hold_steps
    # - Agent 3 Low-A: highest push_count
    # - Team delivery rate: reported honestly, target >50%

    print("\n" + "="*80)
    print("ACCEPTANCE CRITERIA VERIFICATION:")
    print("="*80)

    crit1_pass = False
    crit2_speed_pass = False
    crit2_pad_pass = False
    crit3_pass = False
    crit4_pass = False
    crit5_pass = delivery_rate >= 50.0

    if "Agent_0_HighE" in agent_means.index:
        crit1_pass = agent_means.loc["Agent_0_HighE", "mean_speed"] == agent_means["mean_speed"].max()
    if "Agent_1_LowE" in agent_means.index:
        crit2_speed_pass = agent_means.loc["Agent_1_LowE", "mean_speed"] == agent_means["mean_speed"].min()
        crit2_pad_pass = agent_means.loc["Agent_1_LowE", "pad_hold_steps"] >= agent_means.loc["Agent_0_HighE", "pad_hold_steps"]
    if "Agent_2_HighA" in agent_means.index:
        crit3_pass = agent_means.loc["Agent_2_HighA", "pad_hold_steps"] == agent_means["pad_hold_steps"].max()
    if "Agent_3_LowA" in agent_means.index:
        crit4_pass = agent_means.loc["Agent_3_LowA", "push_count"] == agent_means["push_count"].max()

    print(f"1. Agent 0 High-E highest mean_speed:       {'[PASS]' if crit1_pass else '[FAIL]'} (Value: {agent_means.get('mean_speed', {}).get('Agent_0_HighE', 0):.3f})")
    print(f"2. Agent 1 Low-E lowest mean_speed:         {'[PASS]' if crit2_speed_pass else '[FAIL]'} (Value: {agent_means.get('mean_speed', {}).get('Agent_1_LowE', 0):.3f})")
    print(f"3. Agent 2 High-A highest pad_hold_steps:   {'[PASS]' if crit3_pass else '[FAIL]'} (Value: {agent_means.get('pad_hold_steps', {}).get('Agent_2_HighA', 0):.1f})")
    print(f"4. Agent 3 Low-A highest push_count:        {'[PASS]' if crit4_pass else '[FAIL]'} (Value: {agent_means.get('push_count', {}).get('Agent_3_LowA', 0):.1f})")
    print(f"5. Team delivery rate > 50%:                {'[PASS]' if crit5_pass else '[FAIL]'} (Value: {delivery_rate:.1f}%)")

    # 3. Generate Visual Plots
    fig, axes = plt.subplots(2, 2, figsize=(14, 10))
    fig.suptitle(f"RelayCorridor: Hand-Designed Baseline Evaluation (30 Episodes)\nDelivery Rate: {delivery_rate:.1f}% | Mean Length: {mean_ep_length:.0f} steps", fontsize=14, fontweight="bold")

    colors = ["#22a7f0", "#4b77be", "#2ecc71", "#e74c3c"]
    agents_list = sorted(df["agent_id"].unique())

    # Plot 1: Mean Speed
    ax1 = axes[0, 0]
    speed_means = [df[df["agent_id"] == a]["mean_speed"].mean() for a in agents_list]
    speed_stds = [df[df["agent_id"] == a]["mean_speed"].std() for a in agents_list]
    ax1.bar(agents_list, speed_means, yerr=speed_stds, capsize=5, color=colors[:len(agents_list)])
    ax1.set_title("Mean Traversal Speed (units/s)", fontweight="bold")
    ax1.set_ylabel("Speed")
    ax1.grid(axis='y', linestyle='--', alpha=0.7)

    # Plot 2: Pad Hold Steps
    ax2 = axes[0, 1]
    pad_means = [df[df["agent_id"] == a]["pad_hold_steps"].mean() for a in agents_list]
    pad_stds = [df[df["agent_id"] == a]["pad_hold_steps"].std() for a in agents_list]
    ax2.bar(agents_list, pad_means, yerr=pad_stds, capsize=5, color=colors[:len(agents_list)])
    ax2.set_title("Pad Hold Duration (steps)", fontweight="bold")
    ax2.set_ylabel("Steps")
    ax2.grid(axis='y', linestyle='--', alpha=0.7)

    # Plot 3: Push Count
    ax3 = axes[1, 0]
    push_means = [df[df["agent_id"] == a]["push_count"].mean() for a in agents_list]
    push_stds = [df[df["agent_id"] == a]["push_count"].std() for a in agents_list]
    ax3.bar(agents_list, push_means, yerr=push_stds, capsize=5, color=colors[:len(agents_list)])
    ax3.set_title("Push / Collision Count", fontweight="bold")
    ax3.set_ylabel("Collisions")
    ax3.grid(axis='y', linestyle='--', alpha=0.7)

    # Plot 4: Cell Carry Steps
    ax4 = axes[1, 1]
    carry_means = [df[df["agent_id"] == a]["cell_carry_steps"].mean() for a in agents_list]
    carry_stds = [df[df["agent_id"] == a]["cell_carry_steps"].std() for a in agents_list]
    ax4.bar(agents_list, carry_means, yerr=carry_stds, capsize=5, color=colors[:len(agents_list)])
    ax4.set_title("Cell Carry Duration (steps)", fontweight="bold")
    ax4.set_ylabel("Steps")
    ax4.grid(axis='y', linestyle='--', alpha=0.7)

    plt.tight_layout()
    chart_path = output_dir / "baseline_metrics_summary.png"
    plt.savefig(chart_path, dpi=200)
    plt.close()
    print(f"\n[Evaluate] Saved chart to {chart_path}")

    # 4. Generate Markdown Report
    report_path = output_dir / "baseline_evaluation_report.md"
    with open(report_path, "w", encoding="utf-8") as rf:
        rf.write("# Baseline Evaluation Report (Paradigm A: Hand-Designed MDPI Baseline)\n\n")
        rf.write(f"- **Total Episodes Evaluated**: {total_episodes}\n")
        rf.write(f"- **Team Delivery Rate**: {delivery_count}/{total_episodes} ({delivery_rate:.1f}%)\n")
        rf.write(f"- **Mean Episode Length**: {mean_ep_length:.1f} steps\n\n")
        rf.write("## Per-Agent Behavioral Metrics (Mean ± Std)\n\n")
        rf.write("| Agent ID | Personality Profile | Mean Speed | Pad Hold Steps | Cell Carry Steps | Push Count |\n")
        rf.write("|---|---|---|---|---|---|\n")
        for a in agents_list:
            a_df = df[df["agent_id"] == a]
            p = a_df["personality"].iloc[0]
            rf.write(f"| {a} | {p} | {a_df['mean_speed'].mean():.3f} ± {a_df['mean_speed'].std():.3f} | "
                     f"{a_df['pad_hold_steps'].mean():.1f} ± {a_df['pad_hold_steps'].std():.1f} | "
                     f"{a_df['cell_carry_steps'].mean():.1f} ± {a_df['cell_carry_steps'].std():.1f} | "
                     f"{a_df['push_count'].mean():.1f} ± {a_df['push_count'].std():.1f} |\n")
        rf.write("\n## Acceptance Criteria Summary\n\n")
        rf.write(f"1. **Agent 0 High-E highest mean_speed**: {'PASS' if crit1_pass else 'FAIL'}\n")
        rf.write(f"2. **Agent 1 Low-E lowest mean_speed**: {'PASS' if crit2_speed_pass else 'FAIL'}\n")
        rf.write(f"3. **Agent 2 High-A highest pad_hold_steps**: {'PASS' if crit3_pass else 'FAIL'}\n")
        rf.write(f"4. **Agent 3 Low-A highest push_count**: {'PASS' if crit4_pass else 'FAIL'}\n")
        rf.write(f"5. **Team delivery rate > 50%**: {'PASS' if crit5_pass else 'FAIL'} ({delivery_rate:.1f}%)\n")

    print(f"[Evaluate] Saved report to {report_path}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Evaluate baseline ML-Agents telemetry.")
    parser.add_argument("--csv", type=str, default="results/hand_designed/eval_baseline.csv", help="Path to telemetry CSV")
    parser.add_argument("--output-dir", type=str, default="results/hand_designed", help="Output directory")
    args = parser.parse_args()

    evaluate_baseline(Path(args.csv), Path(args.output_dir))