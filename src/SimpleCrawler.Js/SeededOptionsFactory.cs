using Microsoft.Extensions.Options;

namespace SimpleCrawler.Js;

/// <summary>
/// Builds an options object starting from an instance a caller already supplied, so that any registered
/// <c>Configure</c> runs over it rather than being lost.
/// <para>
/// This is what an <c>AddXyzEngine(XyzEngineOptions?)</c> overload registers instead of
/// <c>services.AddSingleton(Options.Create(instance))</c>. That closed <c>IOptions&lt;T&gt;</c> registration
/// wins over the open generic, so a caller's own <c>services.Configure&lt;T&gt;(…)</c> binds to nothing and
/// is silently ignored — the settings simply not arriving, defaults left in place, and no error to say so.
/// </para>
/// <para>
/// Seeding <see cref="OptionsFactory{TOptions}.CreateInstance"/> is used rather than copying the caller's
/// values onto a fresh instance because a copy is a property list, and one added to the options class but
/// not to the list is dropped from whatever the caller passed, silently again. There is no list here to
/// keep in step.
/// </para>
/// </summary>
public sealed class SeededOptionsFactory<TOptions> : OptionsFactory<TOptions>
    where TOptions : class
{
    private readonly TOptions _seed;

    public SeededOptionsFactory(
        TOptions seed,
        IEnumerable<IConfigureOptions<TOptions>> setups,
        IEnumerable<IPostConfigureOptions<TOptions>> postConfigures,
        IEnumerable<IValidateOptions<TOptions>> validations)
        : base(setups, postConfigures, validations)
    {
        _seed = seed;
    }

    /// <summary>
    /// The caller's own instance, which every registered <c>Configure</c> then runs over — so a setting made
    /// that way wins, and one the caller never touched keeps the value they passed.
    /// </summary>
    protected override TOptions CreateInstance(string name) => _seed;
}
