using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Field;

internal readonly record struct FieldSample(
    PlanarVector FlowVelocity,
    double Intensity,
    FieldFlowGradient Gradient,
    PlanarVector Turbulence);
