import { useLocation } from 'preact-iso';
import { useEffect } from 'react';

interface RedirectProps {
    to: string;
}

function Redirect({ to }: RedirectProps) {
    const { route } = useLocation();
    useEffect(() => {
        route(to, true);
    }, [])

    return null;
}

export { Redirect };