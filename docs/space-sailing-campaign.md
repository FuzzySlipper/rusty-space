# Space sailing handling campaign

Durable technical companion to
[`space_sailing_reactive_ship_ideas.md`](ideas/space_sailing_reactive_ship_ideas.md)
and
[`ship_physics_implementation_notes.md`](ideas/ship_physics_implementation_notes.md).

> Den owns sequencing, status, and progress. This document holds the canonical
> state split, ownership boundaries, invariants, and known hazards. It
> deliberately carries no task status, checklist, or roadmap state.

## Thesis

One dynamic planar rigid body, one heavily authored flight controller, one
fictional environmental force model, one logical collection of wonky ship
parts, and one fake-but-reactive interior set.

Rapier integrates. The product decides what every force means. The interior
performs. The physics solver is the ship's accountant, not its captain.

The game is a deliberate stack of compatible fakes. The part that matters is
that those fakes exchange meaningful state.

## Canonical state split

Owners below are target state. Some exist today; others are the owners this
campaign introduces.

Physical, authoritative state — pose, linear velocity, angular velocity, mass
properties — belongs to Engine Dynamics. The product reads it and never
rewrites it.

Authored simulation state — flight intent, actuator state, coupling,
stabilizer state, installed parts, health, temperature, faults — belongs to
the product's Flight and ShipSystems owners. This is where the personality
lives.

Derived observability state — accelerations, controller effort, saturation,
field load, collision impulse — belongs to product telemetry. It is
disposable and never authoritative.

Presentation state — camera, lights, props, audio, instruments, HUD —
belongs to product presentation and stays strictly downstream.

Presentation never reaches backward. Controls and repairs change authored
ship-system state; nothing a camera shake or a lamp does may alter the
solver.

## Three spatial fictions

Strategic space, local navigation space, and interior set space stay
separate. Local navigation uses the XZ plane with yaw around Y and
numerically comfortable chart units; FTL never means asking the solver for
literal superluminal speed. The interior is a stationary ship-local set that
never travels, never rotates with the rigid body, and never contains a
character standing on a moving body. Navigation exports telemetry; the set
interprets it.

## Invariants

These hold for every change in this campaign and are what a review checks.

1. **Heading is not velocity.** Input commands intent; it never sets a
   transform.
2. **Engine is the only integrator.** No C# integration, no post-step
   velocity correction, no parallel spatial authority, no emulated axis
   locking.
3. **No solver damping as space drag.** Solver linear damping is
   world-relative and would erase inertial flight. Any drag-like behavior is
   authored against the local field: `relative = ship_velocity -
   local_field_velocity`.
4. **Releasing thrust never silently brakes.** Rotational assistance never
   implies linear auto-braking. Any velocity or trajectory hold is an
   explicit ship system with visible effort and limits.
5. **Parts are logical effectors.** One body. No jointed modules, no
   per-part colliders. Only a component that physically breaks free becomes
   its own body.
6. **Good wobble only.** Actuator lag, underdamped response, saturation,
   hysteresis, asymmetry, and load-dependent oscillation are authored and
   reproducible. Turbulence is spatially and temporally continuous. No
   white-noise torque, no frame-rate-dependent noise, never raw solver
   instability sold as personality.
7. **Failure becomes a situation.** Physics mistakes create recoverable
   consequences that feed repair and scavenging, not deletion.
8. **Every secondary system bends back toward piloting.** If removing a
   system would not change how the ship flies, it is decorative or too
   disconnected.

## Known hazards

### Planar sign convention

The product planar frame is authoritative for ship attitude. `PlanarFrame`
in `Navigation` owns every expression of it: forward and right for a
heading, the heading of a direction, the yaw an Engine attitude carries, the
single heading to-attitude conversion, and the Engine-`Y` sign of a torque
built from an in-plane offset and force. Positions, velocities, and forces
cross into Dynamics with the plane's coordinates taken identically as `(X,
Z)`; no planar vector is mirrored on the way across.

The Engine is right-handed Y-up while a planar `(X, Z)` pair is left-handed
about `+Y`, so a heading `h` needs an Engine rotation of `-h` to face `(cos
h, sin h)`. At Engine yaw 30 degrees a body's local `+X` is `(0.866,
-0.500)` where a heading of the same angle faces `(0.866, 0.500)`: a body
stands at `+h` and anything drawn for it stands at `-h`.

The mirror is the part still open.

Hand-computed torque from an off-center force inherits the question. Call
`PlanarFrame.YawTorque` rather than a cross product already in hand: it
returns the Engine-`Y` sign that agrees with the authoritative frame, which
is the negation of the world-space cross product of the same two vectors.
Get it wrong and the ship weathercocks, trims, and asymmetry-corrects in the
mirrored direction, silently, with perfectly stable numbers.

Asymmetric collision silhouettes start mattering once local geometry exists.
The tuned ship half-extents are already unequal in X and Z, so a mirrored
attitude presents the wrong cross-section to the thing it hits. Settling
that means either authoring silhouettes in the body's mirrored frame or
making body attitude agree with the planar heading, which takes the steering
and input signs with it.

### Force staleness under catch-up

`AdmittedStepCount` can exceed one. If contributions are evaluated once from
a pre-step readout and then applied across several substeps, steering,
field, gravity, drift, and part forces all act on state that has already
moved. Recompute per substep.

### Mass consistency

Environmental sources that scale by mass must scale by the same real body
mass. A fixed constant in one response path silently diverges from the rest
the moment parts, cargo, or towing change mass.

## Engine boundary

The Engine requests named in `ship_physics_implementation_notes.md` §11 are
already satisfied by the pinned `Rusty.Engine` package: per-axis translation
and rotation locks, optional explicit mass properties, contacts and collision
impulses, body update, spatial sessions bindable to a dynamics world,
lights, sprites, billboards, particles, audio voices and buses, and a
generated product-facing debug command catalog.

Continuous collision also exists at the pinned revision and reaches the
managed surface as `DynamicsBodyProperties.ContinuousCollision`, with the
per-step motion limit widening for bodies that opt in. Spatial collision
shapes also become fixed bodies when a session is bound to a dynamics world,
so a collision space needs no new Engine mechanism. The one remaining gap is
narrower than it first appears: the generic create path omits the properties
bag the shape-typed configs carry. That is filed as a narrow Engine request
rather than worked around downstream.

Missing capability is a valid result. File one purpose-neutral owning
request and stop that slice; never substitute a C# renderer, loop, timer,
bridge, or browser simulation.

## Anti-goals

No real orbital mechanics, literal astronomical scale, physically moving ship
interior, character standing inside a moving world-space rigid body,
articulated jointed ship, realistic fluid dynamics, random torque noise sold
as turbulence, universal drag that erases inertia, or engine-level
space-sailing policy.

## Evidence

Smallest evidence that answers the seam: focused C# build, CoreCLR staging or
`rusty dev` startup, and NativeAOT only when a phase needs fidelity or
release proof. Pure product math — navigation helpers, response primitives,
field sampling — is covered by focused unit tests. This campaign does not
revive the retired Rust or TypeScript gates, browser bundles, packaging
frameworks, or interactive-parity claims.

Handling is a felt property, so a phase that changes feel also wants the ship
driven by real input while these values are read. The `playtest` service
documented in the separate `crew-services` repository provides that: `den-serve`
publishes the product on a LAN origin, `playtest start rusty-space` opens a
browser with a native Xbox controller on the GPU host, and key, stick, and
trigger holds arrive as ordinary Engine input. The live-debug commands are then
read from the same running product. This is an evidence lane, not a gate, and it
adds no product infrastructure.

One trap there: a `playtest` session can enter a degraded phase and refuse
further input while the product keeps running normally. A zero in the
contribution table after that says nothing about flight, so confirm the session
phase before reading a null result as a finding.

## Phase map

Scope and acceptance for each phase live in Den under campaign task
**rusty-space #8305**. The order below is a dependency order, not a status
board.

- Telemetry spine and product debug surfaces (#8306). Observability before
  content. Everything after this is tunable instead of guessable.
- One named planar navigation helper (#8307). Removes the mirrored-torque and
  mirrored-silhouette hazard before any mount offset or local geometry
  depends on attitude.
- Per-substep force recompute (#8308). Closes the catch-up staleness,
  measured rather than assumed.
- Field coupling as a real ship system (#8309). Restores the design's
  central handle and the ability to decline the environment.
- Installed parts as logical effectors (#8310). The hinge of the whole
  design: hardware that changes behavior, not numbers.
- Navigation-view legibility (#8311). Gives the player something to read
  before they are pushed.
- One collision space (#8312). Impacts become impulses and faults.
- Bridge set theater (#8313). Makes flight easier to read, strictly
  downstream.
- Failure becomes a situation (#8314). Closes the loop: faults change the
  hands, and repairs are felt before they are read.

One Engine-side dependency is filed separately: **rusty-engine #8304**,
body-properties parity on the generic rigid-body create path. It is not
blocking; see the Engine boundary section.
