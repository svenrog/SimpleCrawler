import pages from '../../Response/default.json'
import { lazy, memo, Suspense } from 'react';
import { useLocation } from 'preact-iso';
import { PageType } from '../types/PageType';
import { PageComponent } from '../types/PageComponent';
import { PageHelper } from '../helpers/PageHelper';

const NotFound = lazy(() => import('./pages/NotFound'));
const Page = lazy(() => import('./pages/Page'));

interface PagesProps {
    url: string;
}

function Pages({ url }: PagesProps) {
    const { route } = useLocation();
    const getComponent = (page: PageType, index: number) => {
        const props: PageComponent = {
            page,
            nextPage:
                index < pages.length - 1
                    ? () => route(pages[index + 1].href)
                    : undefined,
        };

        switch (page?.type) {
            case 'page': 
                return <Page {...props} />;
            default:
                return <NotFound {...props} />;
        }
    };

    const index = pages.findIndex(x => x.href === url);
    const pageDefinition = pages[index];
    const page = PageHelper.getPage(pageDefinition);
    
    return (
        <Suspense fallback={null}>
            {getComponent(page, index)}
        </Suspense>
    );
}

export default memo(Pages);
