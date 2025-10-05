import { PageType } from "./PageType"

export type PageComponent = {
    page: PageType;
    nextPage?: () => void;
}