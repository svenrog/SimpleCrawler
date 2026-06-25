(function () {
    var routes = [
        { href: '/', text: 'Home' },
        { href: '/docs', text: 'Docs' },
        { href: '/docs/getting-started', text: 'Getting started' },
        { href: '/docs/api', text: 'API' },
        { href: '/blog', text: 'Blog' },
        { href: '/about', text: 'About' }
    ];

    var current = window.location.pathname;
    var root = document.getElementById('root');

    var header = document.createElement('header');
    header.textContent = 'Path: ' + current;
    root.appendChild(header);

    var nav = document.createElement('nav');
    routes.forEach(function (route) {
        var anchor = document.createElement('a');
        anchor.setAttribute('href', route.href);
        if (route.href === current) {
            anchor.setAttribute('aria-current', 'page');
        }
        anchor.textContent = route.text;
        nav.appendChild(anchor);
    });
    root.appendChild(nav);
})();
