import featureDefinitions from '../../Response/features.json';
import type { FeatureDefinition } from './types/FeatureDefinition';
import type { FeatureProbe } from './types/FeatureProbe';

// Probes are bundled eagerly rather than dynamically imported: the engine would drain the async
// import chain before serializing, but the headless-browser backends snapshot the DOM before lazy
// chunks (and the top-level await) resolve, so the links must be appended synchronously to be seen.
const probes = import.meta.glob<FeatureProbe>('./features/*.ts', {
    eager: true,
    import: 'default',
});

function supported(probe: FeatureProbe): boolean {
    try {
        return probe();
    } catch {
        return false;
    }
}

if (typeof document !== 'undefined') {
    const list = document.createElement('ul');
    list.setAttribute('id', 'feature-nav');

    for (const feature of featureDefinitions as FeatureDefinition[]) {
        const probe = probes[`./features/${feature.key}.ts`];
        if (!probe || !supported(probe)) continue;

        const anchor = document.createElement('a');
        anchor.setAttribute('href', feature.href);
        anchor.textContent = feature.name;

        const item = document.createElement('li');
        item.appendChild(anchor);
        list.appendChild(item);
    }

    if (list.childNodes.length > 0) document.body.appendChild(list);
}
