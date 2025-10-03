import { createGlobalStyle } from 'styled-components';

const FontStyles = createGlobalStyle`
    @font-face {
        font-family: 'Raleway';
        font-style: normal;
        font-weight: 300;
        font-display: swap;
        src: url(/fonts/raleway-300.woff2) format('woff2');
    }
    @font-face {
        font-family: 'Raleway';
        font-style: normal;
        font-weight: 400;
        font-display: swap;
        src: url(/fonts/raleway-400.woff2) format('woff2');
    }
    @font-face {
        font-family: 'Raleway';
        font-style: normal;
        font-weight: 700;
        font-display: swap;
        src: url(/fonts/raleway-700.woff2) format('woff2');
    }
    @font-face {
        font-family: 'UnifrakturCook';
        font-style: normal;
        font-weight: 700;
        font-display: swap;
        src: url(/fonts/unifraktur-cook-700.woff2) format('woff2');
    }
`

export { FontStyles }