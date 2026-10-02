# Flying home with a fault

The Kestrel approach is the existing five-obstacle chart, with open recovery
room beyond the gate. It has no docking or strategic layer. A contact can dent
the exposed part and a hard quarter impact can jam that side's steering vane.
The jam adds a standing pull through its existing actuator. Structural damage
also slows and detunes that actuator, and lowers delivery without going below
the health floor.

Hold **G** while flying to free a jammed vane. A patch takes the authored repair
interval; letting go early abandons it. Steering settles back through the same
response when the latch clears, while the dent and its handling remain. **R**
rebuilds the hull at spawn and clears heat and reserve depletion; it keeps the
installed hardware's damage and jams. Selecting a loadout explicitly fits fresh
hardware instead.

The drive has a rechargeable burst reserve above its sustainable supply. Full
demand spends it; gentle manoeuvres and coasting refill it. A low-reserve
warning precedes the progressive output loss. Empty reserve leaves sustainable
thrust and does not take away steering or field coupling.

Working hardware gathers heat and sheds it at an authored cooling rate. The
drive heats faster than the other parts. Its warning precedes derating; sustained
burns fade through the actuator, while coasting cools it and restores output.
Heat is a rate and envelope, not a second stored-energy bar. The bridge fault
lamp, practical-light sag and repeater warn from the same telemetry as the HUD.
No repair menu, inventory, new part family or second handling path is involved.

All adjustable numbers live in `SpaceTuning`'s damage, thermal and reserve
records. Engine Mechanics `Stat`/`Track` own bounds for structural health and
stored energy. Each admitted fixed substep advances resources and response once;
no admitted time means no resource change.

## Playtesting

Use the ordinary hosted `rusty-space-hosted` profile. Its private CoreCLR host
runs the pinned Engine SDK/runtime pair. The Engine's shared `PlaytestDebugModule`
exposes live position, velocity, hardware, envelope readings and obstacle facts;
action queries resolve the physical bindings from the host-admitted manifest.

```sh
playtest start rusty-space-hosted
playtest assist SESSION --json '{"op":"discover"}'
playtest assist SESSION --json '{"op":"time","mode":"action-driven"}'
playtest assist SESSION --json '{"op":"act","id":"reset"}'
playtest assist SESSION --json '{"op":"act","id":"thrust","ms":1000,"capture":true}'
playtest assist SESSION --json '{"op":"advance","ms":1000}'
playtest assist SESSION --json '{"op":"act","id":"helm","capture":true}'
playtest stop SESSION
```

`patch` holds the ordinary patch control for the repair interval plus a small
release margin. `left`, `right`, `couple`, `uncouple`, `emergency-uncouple` and
`attitude-hold` use their ordinary input paths. Space has no mouse-look action;
the adapter refuses look instead of moving the hull or camera as a substitute.
Obstacle coordinates and part facts are semantic assistance, not proof that a
player found a route unaided. Verify a real contact and visible movement after
repair; counters and a cleared flag alone do not establish the handling loop.

Readout commands `space.hardware`, `space.impacts`, `space.controller` and
`space.telemetry` remain useful diagnostics. `space.loadout.stock`, `.scavenged`
and `.damaged` explicitly replace hardware at spawn; label this as assistance,
and do not treat a damaged-fit selection as an impact or a completed repair.
