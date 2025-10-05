import { LocationProvider } from 'preact-iso';
import { GlobalStyles } from './components/GlobalStyles';
import { FontStyles } from './components/FontStyles';
import { Routes } from './components/Routes';

export interface IApplicationProps {
    ssr?: boolean;
    url?: string;
}

function App({ ssr, url }: IApplicationProps) {
    return (
        <LocationProvider scope={ssr ? url : window.location.pathname}>
            <FontStyles />
            <GlobalStyles />
            <Routes />
        </LocationProvider>
    );
}

export default App;
