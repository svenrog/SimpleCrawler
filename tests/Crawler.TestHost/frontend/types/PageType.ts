import { PageContent } from "./PageContent";

export type PageType = {
    url: string;
    type?: 'page' | null;
    content?: PageContent;
    title: string;
    color: string;
    hide?: boolean;
};