// Deterministic catalog data so a route renders the same production-weight DOM every build (stable
// fixtures + stable measurement). Content is derived from the route path via a seeded PRNG — no Math.random.

export type Product = {
    id: string;
    name: string;
    brand: string;
    price: number;
    listPrice: number;
    rating: number;
    reviews: number;
    colorway: string;
    tags: string[];
    blurb: string;
    inStock: boolean;
};

export type Facet = {
    name: string;
    values: { label: string; count: number }[];
};

const brands = ['Nordkapp', 'Fjällström', 'Vindö', 'Brattberg', 'Sörmland', 'Klarälven', 'Höga Kusten'];
const materials = ['merino', 'ripstop', 'gore-tex', 'primaloft', 'cordura', 'ventile', 'softshell'];
const colorways = ['Charcoal', 'Moss', 'Rust', 'Slate', 'Sand', 'Petrol', 'Bark', 'Fjord'];
const categories = ['Jackets', 'Base layers', 'Trousers', 'Footwear', 'Packs', 'Accessories'];
const adjectives = ['lightweight', 'insulated', 'packable', 'windproof', 'breathable', 'reinforced', 'seam-sealed'];

function hashSeed(input: string): number {
    let h = 2166136261;
    for (let i = 0; i < input.length; i++) {
        h ^= input.charCodeAt(i);
        h = Math.imul(h, 16777619);
    }
    return h >>> 0;
}

function mulberry32(seed: number): () => number {
    let a = seed;
    return () => {
        a |= 0;
        a = (a + 0x6d2b79f5) | 0;
        let t = Math.imul(a ^ (a >>> 15), 1 | a);
        t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

function pick<T>(rng: () => number, items: T[]): T {
    return items[Math.floor(rng() * items.length)];
}

export function getCatalog(path: string, count = 240): Product[] {
    const rng = mulberry32(hashSeed(path));
    const products: Product[] = [];

    for (let i = 0; i < count; i++) {
        const brand = pick(rng, brands);
        const category = pick(rng, categories);
        const material = pick(rng, materials);
        const listPrice = 40 + Math.floor(rng() * 60) * 10;
        const discounted = rng() > 0.65;
        const price = discounted ? Math.round(listPrice * (0.6 + rng() * 0.25)) : listPrice;
        const tags = [material, pick(rng, adjectives), pick(rng, adjectives)].filter(
            (tag, index, all) => all.indexOf(tag) === index
        );

        products.push({
            id: `${category.slice(0, 3).toLowerCase()}-${(i + 1).toString().padStart(4, '0')}`,
            name: `${brand} ${pick(rng, adjectives)} ${category.replace(/s$/, '')}`,
            brand,
            price,
            listPrice,
            rating: Math.round((3 + rng() * 2) * 10) / 10,
            reviews: Math.floor(rng() * 900) + 5,
            colorway: pick(rng, colorways),
            tags,
            blurb: `A ${pick(rng, adjectives)} ${material} ${category.toLowerCase().replace(/s$/, '')} built for the ${pick(rng, colorways).toLowerCase()} season.`,
            inStock: rng() > 0.15,
        });
    }

    return products;
}

export function getFacets(products: Product[]): Facet[] {
    const count = <K extends keyof Product>(key: K) => {
        const totals = new Map<string, number>();
        for (const product of products) {
            const value = String(product[key]);
            totals.set(value, (totals.get(value) ?? 0) + 1);
        }
        return [...totals.entries()]
            .sort((a, b) => b[1] - a[1])
            .map(([label, n]) => ({ label, count: n }));
    };

    return [
        { name: 'Brand', values: count('brand') },
        { name: 'Colour', values: count('colorway') },
    ];
}
