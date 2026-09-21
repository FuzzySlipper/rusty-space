using System.Numerics;
using Rusty.Engine;
using Rusty.Space.Product.Approach;
using Rusty.Space.Product.Navigation;

namespace Rusty.Space.Product.Flight;

/// <summary>
/// Reads what the Engine's contacts did to the hull, and names the authored
/// obstacle on the far end of the hardest one.
/// </summary>
/// <remarks>
/// <para>
/// The Engine reports a body's contacts two ways: the body's own readout carries
/// how many there were and the push they amounted to for that body, and the
/// world's contact list carries which bodies each one was between. The first is
/// the answer; the second is walked only to put a name on it, and only when the
/// hull reports something at all, so a hull with nothing against it costs one
/// comparison rather than a scan.
/// </para>
/// <para>
/// Nothing here decides whether the hull and a rock have met, and nothing here
/// tells the hull about a push it has not already been given. Both of those are
/// the Engine's, and the hull's own velocity is where they show up.
/// </para>
/// </remarks>
internal sealed class HullContacts
{
    private const uint NoContacts = 0u;
    private const uint MaximumContactsToRead = 8u;

    private readonly IDynamicsService dynamics;
    private readonly ApproachField approach;

    internal HullContacts(IDynamicsService dynamics, ApproachField approach)
    {
        this.dynamics = dynamics ?? throw new ArgumentNullException(nameof(dynamics));
        this.approach = approach ?? throw new ArgumentNullException(nameof(approach));
    }

    /// <summary>
    /// What the hull's contacts amounted to on the step the given readout came
    /// from, or nothing when the hull reports none. Where the world's list says
    /// which of those contacts the hull was in, the hardest one it names is what
    /// gets reported, since that is the one worth putting a name on: the readout
    /// stands on its own only when the list has nothing about this hull to say.
    /// </summary>
    internal HullImpact Read(
        DynamicsWorld world,
        DynamicsBody hull,
        DynamicsReadout hullReadout,
        double headingRadians)
    {
        DynamicsContactFact reported = hullReadout.FirstContact;
        if (hullReadout.ContactCount == NoContacts || !reported.Present)
        {
            return HullImpact.None;
        }

        double magnitude = reported.ImpulseMagnitude;
        PlanarVector impulse = OnHull(reported.Impulse);
        ObstacleId? struck = null;
        bool reportedByPair = false;

        ulong hullValue = hull.Handle.Value;
        uint contacts = Math.Min(
            dynamics.ReadWorld(new DynamicsWorldReadRequest(world)).ContactCount,
            MaximumContactsToRead);
        for (uint index = NoContacts; index < contacts; index++)
        {
            DynamicsContactAtReceipt contact = dynamics.ReadContactAt(
                new DynamicsContactAtRequest(world, index));
            if (!contact.Present)
            {
                continue;
            }

            bool hullIsFirst = contact.First.Value == hullValue;
            bool hullIsSecond = !hullIsFirst && contact.Second.Value == hullValue;
            if (!hullIsFirst && !hullIsSecond)
            {
                continue;
            }

            if (reportedByPair && contact.ImpulseMagnitude <= magnitude)
            {
                continue;
            }

            reportedByPair = true;

            // A pair's impulse is stated for the pair. The Engine reports it to
            // the body named first and negated to the body named second, so the
            // hull's own copy depends on which end of the pair it is on.
            magnitude = contact.ImpulseMagnitude;
            impulse = OnHull(hullIsFirst ? contact.Impulse : -contact.Impulse);
            struck = approach.ObstacleAt(hullIsFirst ? contact.Second : contact.First);
        }

        return new HullImpact(
            Present: true,
            impulse,
            PlanarFrame.Rotate(impulse, -headingRadians),
            magnitude,
            struck);
    }

    private static PlanarVector OnHull(Vector3 engineImpulse) =>
        new(engineImpulse.X, engineImpulse.Z);
}
