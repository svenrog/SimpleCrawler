import { createGlobalStyle } from 'styled-components';

export const pageTransitionDuration = 1000;

const TransitionStyles = createGlobalStyle`
    .page {
        width: 100%;
        height: 100%;
        left: 0;
        top: 0;
        overflow: hidden;
        position: absolute;
        backface-visibility: visible;
        transform-style: preserve-3d;
        transform: translate3d(0, 0, 0);

        animation-fill-mode: both;
        animation-duration: ${pageTransitionDuration}ms;
        animation-timing-function: cubic-bezier(.6,0,.4,1);
        
        will-change: transform; 
    }

    .page-appear {
        animation-name: rotateIn;
        transform-origin: 50% 100%;
    }

    .page-appeared, .load {
        transform-origin: 50% 50%;
    }

    .page-exit {
        animation-name: rotateOut;
        transform-origin: 50% 0%;
        z-index: 1;
    }

    @keyframes rotateOut {
        from { }
        to { opacity: 0.3; transform: translateY(100%) rotateX(90deg); }
    }

    @keyframes rotateIn {
        from { opacity: 0.3; transform: translateY(-100%) rotateX(-90deg); }
    }
`;

export { TransitionStyles };
