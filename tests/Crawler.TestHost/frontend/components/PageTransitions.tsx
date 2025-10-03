import Pages from "./Pages";
import { useEffect, useState } from "react";
import { PageTransitionGroup } from "./transitions/PageTransitionGroup";
import { pageTransitionDuration, TransitionStyles } from "./transitions/TransitionStyles";
import { useLocation } from "preact-iso";

const initialClassName = "page load";
const enterClassName = "page page-appear";
const exitClassName = "page page-exit";

let initialized = false;

function PageTransitions() {
    const { url } = useLocation();
    const [initialRender, setInitialRender] = useState(true);
    const [currentUrl, setCurrentUrl] = useState<string>(url);
    const [previousUrl, setPreviousUrl] = useState<string>()
    const [renderPrevious, setRenderPrevious] = useState<boolean>(false);

    useEffect(() => {
        if (currentUrl === url) return;
        setInitialRender(false);
        setPreviousUrl(currentUrl);
        setCurrentUrl(url);
        setRenderPrevious(true);
        setTimeout(() => {
            setRenderPrevious(false)
        }, pageTransitionDuration)
    }, [url])

    return (
        <>
            <TransitionStyles />
            <PageTransitionGroup>
                {<div key={currentUrl} className={initialRender ? initialClassName : enterClassName}><Pages url={currentUrl} /></div>}
                {renderPrevious && <div key={previousUrl} className={exitClassName}><Pages url={previousUrl} /></div>}
            </PageTransitionGroup>
        </>
    );
}

export default PageTransitions;