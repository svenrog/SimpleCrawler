namespace Crawler.AngleSharp.Js.Dom.Expando;

// Implemented explicitly by the expando node wrappers so the three hooks stay off the DOM surface the
// engines see. ExpandoMetaObject routes unknown property get/set here; real DOM members fall through.
internal interface IExpandoNode
{
    bool HasExpando(string name);

    object? ExpandoGet(string name);

    void ExpandoSet(string name, object? value);

    // React 18 stores fibers/props as expandos and `delete`s them on unmount; without a delete hook
    // ClearScript throws "Cannot delete property of HostInvocable" and aborts the commit.
    void ExpandoDelete(string name);

    // ClearScript/V8 only routes a property *get* through the meta object for names it sees here first;
    // without this the stored expandos read back as undefined on V8 (its set path has no such gate).
    IEnumerable<string> ExpandoNames();
}
