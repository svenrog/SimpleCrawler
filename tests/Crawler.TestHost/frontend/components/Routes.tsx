import pages from '../../Response/default.json'
import { lazy, Route, Router } from "preact-iso";
import PageTransitions from "./PageTransitions";
import { PageHelper } from '../helpers/PageHelper';

const Page = lazy(() => import('./pages/Page'));

function Routes() {
    return (
        <Router>
            <Route path="/" component={() => <Page page={PageHelper.getPage(pages[0])} />} />
            <Route path="/*" component={() => <PageTransitions />} />
        </Router>
    );
}

export { Routes };
