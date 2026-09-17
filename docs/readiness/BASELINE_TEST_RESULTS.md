# T00 Baseline Test Results

Baseline source revision: `15fb66ba52ea210d57f593acc4ce4d70ce618a31`

Unity version: `6000.5.9f1` (`b57deb96f08d`)

## Runner attempts

| Suite | Command | Result |
|---|---|---|
| EditMode | Unity batchmode `-runTests -testPlatform editmode` | Blocked before Test Runner startup |
| PlayMode | Unity batchmode `-runTests -testPlatform playmode` | Blocked before Test Runner startup |

Neither attempt produced a result XML, so no test count or pass/fail total is claimed.

The blocking log signature is Unity Licensing IPC failure:

```text
Connection to channel LicenseClient-artwh refused
Timed-out after 60.01s, waiting for Licensing to initialize
Licensing initialization failed after 74.81s
```

Evidence logs:

- `docs/readiness/BASELINE_EDITMODE.log` — initial EditMode attempt;
- `docs/readiness/FINAL_EDITMODE.log` — final EditMode attempt;
- `docs/readiness/FINAL_PLAYMODE.log` — final PlayMode attempt.

The CLI Unity processes were terminated after the repeated licensing timeout. The independent `99_FINAL_ACCEPTANCE_GATE_PROMPT.md` was not run.

## Baseline code observations used by the packet

- The project uses Unity `MonoBehaviour` simulation components and serialized scene state.
- Runtime source compilation is separately checkable with the generated `ColonyPrototype.Runtime.csproj`, but that is not a substitute for Unity Test Runner evidence.
- The packet's requested pre-change Unity totals remain unavailable until a licensed Unity editor can run the suites.
