using Rusty.Engine;
using Xunit;

namespace Rusty.Space.Product.Engine.Tests;

public class RecordingServiceProxyTests
{
    [Fact]
    public void AnUnrecordedServiceOrCallFailsLoudly()
    {
        RecordingEngine engine = new();
        Assert.Throws<NotSupportedException>(() => engine.Context.Input);
        Assert.Throws<NotSupportedException>(() => engine.Dynamics.Service.ConfigureRopes(default));
    }

    [Fact]
    public void InjectedFailuresKeepTheirOriginalTypeAcrossTheProxy()
    {
        RecordingEngine engine = new();
        engine.Faults.FailOn = nameof(IAudioService.OpenClip);
        InjectedFault error = Assert.Throws<InjectedFault>(() => engine.Audio.Service.OpenClip(default));
        Assert.Equal(nameof(IAudioService.OpenClip), error.Operation);
    }
}
