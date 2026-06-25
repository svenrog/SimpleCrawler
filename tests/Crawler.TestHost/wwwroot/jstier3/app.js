(function () {
    var h = preact.h;
    var render = preact.render;

    var routes = [
        { href: '/', text: 'Home' },
        { href: '/features', text: 'Features' },
        { href: '/features/search', text: 'Search' },
        { href: '/features/sitemap', text: 'Sitemap' },
        { href: '/pricing', text: 'Pricing' },
        { href: '/docs', text: 'Docs' },
        { href: '/contact', text: 'Contact' }
    ];

    function Nav(props) {
        return h('nav', null, props.routes.map(function (route) {
            return h('a', {
                href: route.href,
                'aria-current': route.href === props.current ? 'page' : null
            }, route.text);
        }));
    }

    function App(props) {
        return h('div', { class: 'app' },
            h('h1', null, 'Tier 3'),
            h('p', null, 'Current path: ' + props.current),
            h(Nav, { routes: props.routes, current: props.current })
        );
    }

    var current = window.location.pathname;
    render(h(App, { routes: routes, current: current }), document.getElementById('root'));
})();
