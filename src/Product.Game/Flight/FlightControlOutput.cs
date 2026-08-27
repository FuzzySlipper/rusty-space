namespace Rusty.Space.Product.Flight;

internal readonly record struct FlightControlOutput(
    FlightWrench Wrench,
    double ThrottleLevel);
