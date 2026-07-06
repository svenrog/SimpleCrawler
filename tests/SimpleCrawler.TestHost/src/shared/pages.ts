import definitions from '../../Response/default.json';

export type PageDefinition = {
    href: string;
    name: string;
};

export type Page = {
    url: string;
    title: string;
    color: string;
    titleHtml: string;
    body: string;
    found: boolean;
};

export const pages: PageDefinition[] = definitions;

const colors = ['#089068', '#06846c', '#007369', '#005A5B', '#003840', '#161616'];

const body =
    'Lorem ipsum dolor sit amet, consectetur adipiscing elit. Phasellus posuere nulla et ex facilisis tincidunt. Curabitur dignissim in felis ut luctus. Mauris hendrerit mauris quis congue consequat. Proin finibus libero neque, et cursus mauris blandit eget. Suspendisse nisl leo, gravida eget orci vitae, pellentesque euismod mauris. Sed sit amet diam dapibus risus mollis ornare. Nunc et finibus nulla, nec ornare nulla. Morbi consectetur elit non mollis sagittis. Sed nec pulvinar lorem, ut consectetur elit. Duis eget mauris quam. Donec mi purus, pharetra quis libero eget, lobortis vestibulum eros.';

function pickColor(href: string): string {
    let hash = 0;
    for (let i = 0; i < href.length; i++) hash = (hash * 31 + href.charCodeAt(i)) | 0;
    return colors[Math.abs(hash) % colors.length];
}

export function getPage(href: string): Page {
    const definition = pages.find((page) => page.href === href);
    if (!definition) {
        return {
            url: href,
            title: 'Not found',
            color: '#222',
            titleHtml: 'Oops, it looks like this page is <em>missing</em>',
            body: 'Try something else.',
            found: false,
        };
    }

    return {
        url: definition.href,
        title: definition.name,
        color: pickColor(definition.href),
        titleHtml: `<em>${definition.name}</em>`,
        body,
        found: true,
    };
}
