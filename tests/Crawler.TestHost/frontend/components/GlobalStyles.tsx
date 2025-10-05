import { createGlobalStyle } from 'styled-components';

const GlobalStyles = createGlobalStyle`
    * {
        border: 0;
        box-sizing: border-box;
        color: inherit;
        font-size: inherit;
        margin: 0;
        padding: 0;
    }

    html, body, #root {
        height: 100%;
    }

    body {
        background-color: #333;
    }

    body, button {
        color: rgba(255,255,255,0.9);
        font-family: "Raleway", sans-serif;
        font-optical-sizing: auto;
        font-weight: 400;
        font-style: normal;
        font-size: 18px;
        line-height: 1.4;
    }

    a {
        text-decoration: none;
    }

    h1, h2, h3, h4, h5, p {
        max-width: 60ch;
        position: relative;
        z-index: 0;
    }

    table {
        border-spacing: 0;
        border-collapse: collapse;

        th {
            text-align: left;
        }
    }

    a {
        text-decoration: underline;
    }

    /* Stylized elements */
    em {
        display: inline-block;
        position: relative;
        font-family: "UnifrakturCook", sans-serif !important;
        font-weight: 700;
        font-style: normal;
        transform: rotate(-5deg) scale(1.1) translate(0, 5%);
        margin-left: 10px;
        margin-right: 10px;
        line-height: 0.75;
        z-index: -1;
        
        opacity: 1;
        transform: rotate(-5deg) scale(1.1) translate(0, 5%); 

        // Animation when appearing
        animation-name: none;
        animation-duration: 800ms;
        animation-timing-function: cubic-bezier(0, 0.5, 0, 1);
        animation-fill-mode: both;
        animation-delay: 850ms;

        @keyframes appear {
            from {
                opacity: 0;
                transform: rotate(-5deg) scale(1.0) translate(0, -100%); 
            }
            to { 
                opacity: 1;
                transform: rotate(-5deg) scale(1.1) translate(0, 5%); 
            }
        }
        
        :before {
            content: '';
            position: absolute;
            display: block;
            width: 100%;
            height: 100%;
            z-index: -1;
            background-color: #00000016;
            
            padding: 16px 24px;
            margin: -10px -24px;
            transform: rotate(-3deg) translate(0, 0);
        }
    }

    .page-appear {
        // Em should not appear when page has been loaded first time
        em {
            opacity: 0;
            animation-name: appear;
        }
    }
`;

export { GlobalStyles };
