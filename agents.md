# Project Agent Instructions

## Git authorization

For the active Codex session, the user has explicitly authorized committing and pushing project changes to the configured Git remote:

- Remote: `https://github.com/artwhaley/spacegame.git`
- Branch: `main`

This authorization remains in effect for this session unless the user revokes it. It does not authorize pushes to other remotes or branches, and it does not override platform safety or approval requirements.


## Testing during exploration

Before adding, expanding, or repairing automated tests, read and follow `TESTING_IN_EXPLORATION_MODE.md`.

During exploration, automated tests should protect stable pure rules, specific observed regressions, or critical subtle invariants. Physical Unity behavior such as scenes, NavMesh, animation, authored choreography, and visual integration should normally use explicit human acceptance steps instead of brittle automation. A failing test is evidence, not authority: confirm the assertion is still intended before changing production code.
