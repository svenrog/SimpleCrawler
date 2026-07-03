using Jint;
using Jint.Runtime;
using System.Collections;
using System.Reflection;

namespace Crawler.Js.Jint;

// The one reflection site in the backend: Jint has no public reset for a pooled realm's ES-module registry or
// global lexical record, so both are cleared reflectively between pages. It won't survive trimming/NativeAOT,
// so the pool gates reuse on IsSupported (and JintEngineOptions.AllowReflectiveRealmReset), falling back to a
// fresh engine per page when either is off.
internal static class JintRealmReset
{
    // Modules and their builders live in two per-realm dictionaries on the internal Engine.Modules; a reused
    // engine must clear them or re-adding a stable-URL chunk throws "same key has already been added".
    private static readonly FieldInfo? _modulesField = ResolveModuleField("_modules");
    private static readonly FieldInfo? _buildersField = ResolveModuleField("_builders");

    private static FieldInfo? ResolveModuleField(string name)
        => typeof(Engine).GetNestedType("ModuleOperations", BindingFlags.Public | BindingFlags.NonPublic)?
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    // A classic <script>'s top-level let/const/class binds into Realm.GlobalEnv._declarativeRecord, not onto
    // the global object, so __crawlerReset can't reach it; without clearing, a reused engine re-running the
    // same chunk throws "X has already been declared".
    private static readonly PropertyInfo? _realmProperty =
        typeof(Engine).GetProperty("Realm", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly PropertyInfo? _globalEnvProperty =
        typeof(Realm).GetProperty("GlobalEnv", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo? _declarativeRecordField =
        _globalEnvProperty?.PropertyType.GetField("_declarativeRecord", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo? _clearLexicalRecordMethod =
        _declarativeRecordField?.FieldType.GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);

    // False if any lookup above failed (a future Jint rename); the pool then disables reuse instead of leaking.
    public static bool IsSupported =>
        _modulesField is not null && _buildersField is not null &&
        _realmProperty is not null && _globalEnvProperty is not null &&
        _declarativeRecordField is not null && _clearLexicalRecordMethod is not null;

    public static void Reset(Engine engine)
    {
        ResetModuleRegistry(engine);
        ResetGlobalLexicalRecord(engine);
    }

    private static void ResetModuleRegistry(Engine engine)
    {
        if (_modulesField is null || _buildersField is null)
            return;

        var modules = engine.Modules;
        (_modulesField.GetValue(modules) as IDictionary)?.Clear();
        (_buildersField.GetValue(modules) as IDictionary)?.Clear();
    }

    private static void ResetGlobalLexicalRecord(Engine engine)
    {
        if (_realmProperty is null || _globalEnvProperty is null || _declarativeRecordField is null || _clearLexicalRecordMethod is null)
            return;

        var realm = _realmProperty.GetValue(engine);
        if (realm is null)
            return;

        var globalEnv = _globalEnvProperty.GetValue(realm);
        if (globalEnv is null)
            return;

        var record = _declarativeRecordField.GetValue(globalEnv);
        if (record is null)
            return;

        _clearLexicalRecordMethod.Invoke(record, null);
    }
}
