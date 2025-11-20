// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener('DOMContentLoaded', () => {
    const nav = document.querySelector('.app-nav');
    const toggle = document.querySelector('[data-nav-toggle]');
    const overlay = document.querySelector('[data-nav-overlay]');
    const navBody = document.getElementById('globalNavBody');
    const body = document.body;

    if (!nav || !toggle || !navBody) {
        return;
    }

    const setAriaState = () => {
        if (window.innerWidth > 900) {
            navBody.removeAttribute('aria-hidden');
            return;
        }
        navBody.setAttribute('aria-hidden', nav.classList.contains('is-open') ? 'false' : 'true');
    };

    const closeNav = () => {
        nav.classList.remove('is-open');
        toggle.setAttribute('aria-expanded', 'false');
        overlay?.classList.remove('is-visible');
        body.classList.remove('nav-open');
        setAriaState();
    };

    const openNav = () => {
        nav.classList.add('is-open');
        toggle.setAttribute('aria-expanded', 'true');
        overlay?.classList.add('is-visible');
        body.classList.add('nav-open');
        setAriaState();
    };

    toggle.addEventListener('click', () => {
        if (nav.classList.contains('is-open')) {
            closeNav();
        } else {
            openNav();
        }
    });

    overlay?.addEventListener('click', closeNav);

    navBody.addEventListener('click', (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }
        if (target.closest('a') || target.closest('button')) {
            closeNav();
        }
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') {
            closeNav();
        }
    });

    window.addEventListener('resize', () => {
        if (window.innerWidth > 900) {
            closeNav();
        } else {
            setAriaState();
        }
    });

    setAriaState();
});
