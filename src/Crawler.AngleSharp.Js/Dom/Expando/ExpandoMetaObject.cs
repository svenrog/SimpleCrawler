using System.Collections.Concurrent;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace Crawler.AngleSharp.Js.Dom.Expando;

// Adds JS-expando semantics to a node wrapper without making it a DynamicObject (which would route ALL
// member access through the DLR and regress the V8 fast path). A name that matches a real CLR member binds
// normally; anything else (React's __reactFiber$/__reactProps$/...) is stored in the per-node side table.
// Used only when JsRenderOptions.EnableDomExpandos is on, so the default wrappers stay plain host objects.
internal sealed class ExpandoMetaObject : DynamicMetaObject
{
    private static readonly MethodInfo _hasExpando = typeof(IExpandoNode).GetMethod(nameof(IExpandoNode.HasExpando))!;
    private static readonly MethodInfo _expandoGet = typeof(IExpandoNode).GetMethod(nameof(IExpandoNode.ExpandoGet))!;
    private static readonly MethodInfo _expandoSet = typeof(IExpandoNode).GetMethod(nameof(IExpandoNode.ExpandoSet))!;
    private static readonly MethodInfo _expandoDelete = typeof(IExpandoNode).GetMethod(nameof(IExpandoNode.ExpandoDelete))!;

    private static readonly ConcurrentDictionary<Type, HashSet<string>> _members = new();
    private static readonly ConcurrentDictionary<Type, HashSet<string>> _writableMembers = new();

    public ExpandoMetaObject(Expression expression, object value)
        : base(expression, BindingRestrictions.Empty, value)
    {
    }

    // V8/ClearScript consults this before it will route a property *get* through BindGetMember; an
    // unlisted name is answered with `undefined` and never reaches the side table. Reporting the stored
    // keys is what makes round-tripping an expando work on V8 (Jint reads the table regardless).
    public override IEnumerable<string> GetDynamicMemberNames() =>
        Value is IExpandoNode node ? node.ExpandoNames() : [];

    public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
    {
        if (HasRealMember(binder.Name))
            return binder.FallbackGetMember(this);

        var fallback = binder.FallbackGetMember(this);
        var self = Expression.Convert(Expression, typeof(IExpandoNode));
        var name = Expression.Constant(binder.Name);

        // Prefer a stored expando, otherwise defer to the engine's own "missing member" result (undefined).
        var expression = Expression.Condition(
            Expression.Call(self, _hasExpando, name),
            Expression.Call(self, _expandoGet, name),
            Expression.Convert(fallback.Expression, typeof(object)));

        return new DynamicMetaObject(expression, fallback.Restrictions.Merge(TypeRestriction()));
    }

    public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
    {
        if (HasRealWritableMember(binder.Name))
            return binder.FallbackSetMember(this, value);

        var self = Expression.Convert(Expression, typeof(IExpandoNode));
        var name = Expression.Constant(binder.Name);
        var boxed = Expression.Convert(value.Expression, typeof(object));
        var assign = Expression.Block(Expression.Call(self, _expandoSet, name, boxed), boxed);

        return new DynamicMetaObject(assign, TypeRestriction().Merge(value.Restrictions));
    }

    public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder)
    {
        if (HasRealMember(binder.Name))
            return binder.FallbackDeleteMember(this);

        var self = Expression.Convert(Expression, typeof(IExpandoNode));
        var name = Expression.Constant(binder.Name);
        var delete = Expression.Call(self, _expandoDelete, name);

        // The binder asks for void (ClearScript), bool, or object; `delete` succeeds, so yield true where a
        // value is expected and nothing where it is not.
        Expression expression = binder.ReturnType == typeof(void)
            ? delete
            : Expression.Block(delete, binder.ReturnType == typeof(bool)
                ? Expression.Constant(true)
                : Expression.Convert(Expression.Constant(true), binder.ReturnType));

        return new DynamicMetaObject(expression, TypeRestriction());
    }

    private BindingRestrictions TypeRestriction() => BindingRestrictions.GetTypeRestriction(Expression, LimitType);

    private bool HasRealMember(string name) => Members(LimitType).Contains(name);

    private bool HasRealWritableMember(string name) => WritableMembers(LimitType).Contains(name);

    private static HashSet<string> Members(Type type) => _members.GetOrAdd(type, static t =>
        new HashSet<string>(t.GetMembers(BindingFlags.Public | BindingFlags.Instance).Select(member => member.Name), StringComparer.Ordinal));

    private static HashSet<string> WritableMembers(Type type) => _writableMembers.GetOrAdd(type, static t =>
        new HashSet<string>(
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanWrite).Select(property => property.Name)
                .Concat(t.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(field => !field.IsInitOnly).Select(field => field.Name)),
            StringComparer.Ordinal));
}
