# Space UI lane

`host/index.html` is the minimal static bundle served by the standard Engine
product host. It reports only that the C# product host is available.

Future DOM UI may use standard Engine host routes once it has a concrete
product need. It must not retain gameplay state, simulate input, render world
elements, create a canvas, or add an alternate transport.
