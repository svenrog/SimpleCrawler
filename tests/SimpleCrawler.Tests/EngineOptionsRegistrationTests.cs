using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SimpleCrawler.Js.Jint;
using SimpleCrawler.Js.V8;

namespace SimpleCrawler.Tests;

/// <summary>
/// Pins how an engine's options reach it. Each <c>AddXyzJsEngine(options)</c> seeds the options factory with
/// the instance it was handed rather than registering it as a closed <c>IOptions&lt;T&gt;</c>, which would win
/// over the open generic and leave a caller's own <c>Configure</c> bound to nothing — the settings not
/// arriving, defaults left in place, and no error to say so. That failure is invisible at the call site and
/// showed up only as a ceiling that never fired, so it is pinned per engine rather than reasoned about.
/// </summary>
public class EngineOptionsRegistrationTests
{
    private static TOptions Resolve<TOptions>(Action<IServiceCollection> register)
        where TOptions : class
    {
        var services = new ServiceCollection();
        register(services);
        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<TOptions>>().Value;
    }

    [Fact]
    public void An_instance_passed_to_AddJintJsEngine_is_what_resolves()
    {
        var resolved = Resolve<JintEngineOptions>(services =>
            services.AddJintJsEngine(new JintEngineOptions { ScriptTimeout = TimeSpan.FromSeconds(3) }));

        Assert.Equal(TimeSpan.FromSeconds(3), resolved.ScriptTimeout);
    }

    [Fact]
    public void An_instance_passed_to_AddV8JsEngine_is_what_resolves()
    {
        var resolved = Resolve<V8EngineOptions>(services =>
            services.AddV8JsEngine(new V8EngineOptions { PageTimeout = TimeSpan.FromSeconds(3), MaxUsesPerRuntime = 7 }));

        Assert.Equal(TimeSpan.FromSeconds(3), resolved.PageTimeout);
        Assert.Equal(7, resolved.MaxUsesPerRuntime);
    }

    [Fact]
    public void A_callers_own_Configure_reaches_Jint()
    {
        var resolved = Resolve<JintEngineOptions>(services =>
        {
            services.AddJintJsEngine();
            services.Configure<JintEngineOptions>(o => o.ScriptTimeout = TimeSpan.FromSeconds(5));
        });

        Assert.Equal(TimeSpan.FromSeconds(5), resolved.ScriptTimeout);
    }

    [Fact]
    public void A_callers_own_Configure_reaches_V8()
    {
        var resolved = Resolve<V8EngineOptions>(services =>
        {
            services.AddV8JsEngine();
            services.Configure<V8EngineOptions>(o => o.PageTimeout = TimeSpan.FromSeconds(5));
        });

        Assert.Equal(TimeSpan.FromSeconds(5), resolved.PageTimeout);
    }

    // The two compose rather than one silently replacing the other: Configure runs over the passed instance,
    // so it wins where it speaks and leaves every setting it does not mention as the caller passed it.
    [Fact]
    public void Configure_runs_over_a_passed_instance_rather_than_replacing_it()
    {
        var resolved = Resolve<V8EngineOptions>(services =>
        {
            services.AddV8JsEngine(new V8EngineOptions { PageTimeout = TimeSpan.FromSeconds(3), MaxUsesPerRuntime = 7 });
            services.Configure<V8EngineOptions>(o => o.PageTimeout = TimeSpan.FromSeconds(9));
        });

        Assert.Equal(TimeSpan.FromSeconds(9), resolved.PageTimeout);
        Assert.Equal(7, resolved.MaxUsesPerRuntime);
    }

    [Fact]
    public void The_engine_only_registrations_resolve_defaults()
    {
        Assert.Equal(
            new JintEngineOptions().ScriptTimeout,
            Resolve<JintEngineOptions>(services => services.AddJintJsEngine()).ScriptTimeout);

        Assert.Equal(
            new V8EngineOptions().PageTimeout,
            Resolve<V8EngineOptions>(services => services.AddV8JsEngine()).PageTimeout);
    }
}
