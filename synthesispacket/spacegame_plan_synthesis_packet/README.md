# Spacegame Blank-Space Synthesis + 28-Day Plan Packet

This packet synthesizes:

- the OpenAI blank-space audit,
- DeepSeek's audit (which already incorporates Muse/GLM),
- Claude's independent audit,
- and the owner's new direction to integrate human avatars, NavMesh facility spaces, the parallel interactable-facility prototype, and physical shuttle docking/boarding immediately.

## Most useful files

- `01_SYNTHESIS_REPORT.md` — what was strengthened, changed, newly caught, and rejected.
- `planning/02_HUMAN_28_DAY_PLAN.md` — human-facing 28-day plan, with Days 1–3 committed at working depth.
- `planning/07_AGENT_28_DAY_PROMPTS.md` — a copy-paste starting prompt for each of the 28 days.
- `planning/04_DECISION_BACKLOG.md` — trigger-gated open questions.
- `planning/05_PROTECT_LIST.md` — code invariants and owner-stated intent not to lose.
- `codex/09_CODEX_INSTALL_AND_PATCH_PROMPT.md` — prompt for a local Codex session to install these artifacts into the repo and reconcile all active planning docs.
- `codex/08_REPOSITORY_PATCH_MAP.md` — exact documentation mutation checklist.

## Recommended local use

1. Download and unzip this packet.
2. Put the extracted folder somewhere your local Codex session can read.
3. Start Codex from the **spacegame repo root**.
4. Open `codex/09_CODEX_INSTALL_AND_PATCH_PROMPT.md`, replace `<PACKET_DIR>` with the extracted packet path if necessary, and paste it into Codex.
5. Review the resulting planning-only git diff before committing.
6. In a fresh execution session, start Day 1 from the generated `planning/CURRENT_WINDOW.md`.

The install prompt explicitly forbids gameplay/runtime changes; it only re-frames the repository's planning and documentation so Day 1 can begin cleanly.
