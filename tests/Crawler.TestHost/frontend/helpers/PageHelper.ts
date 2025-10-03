import type { PageType } from "../types/PageType";

export type PageDefinition = {
    href: string;
    name: string;
}

export class PageHelper {
    private static readonly _colors = ['#089068', '#06846c', '#007369', '#005A5B', '#003840', '#161616'];
    public static getPage(definition: PageDefinition): PageType {
        if (!definition?.href) return undefined;
        const color = this._colors[Math.floor(Math.random()*this._colors.length)];    
        return {
            title: definition?.name,
            url: definition.href,
            type: 'page',
            color,
            content: {
                title: `<em>${definition.name}</em>`,
                body: 'Lorem ipsum dolor sit amet, consectetur adipiscing elit. Phasellus posuere nulla et ex facilisis tincidunt. Curabitur dignissim in felis ut luctus. Mauris hendrerit mauris quis congue consequat. Proin finibus libero neque, et cursus mauris blandit eget. Suspendisse nisl leo, gravida eget orci vitae, pellentesque euismod mauris. Sed sit amet diam dapibus risus mollis ornare. Nunc et finibus nulla, nec ornare nulla. Morbi consectetur elit non mollis sagittis. Sed nec pulvinar lorem, ut consectetur elit. Duis eget mauris quam. Donec mi purus, pharetra quis libero eget, lobortis vestibulum eros.'
            }
        }
    }
}