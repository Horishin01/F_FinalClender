// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', () => {
    const nav = document.querySelector('.app-nav');
    const toggle = document.querySelector('.nav-toggle');
    const navBlock = nav?.querySelector('.app-nav-block');
    const mobileMenu = document.getElementById('mobileMenu');
    if (!nav || !toggle || !mobileMenu) return;

    const body = document.body;

    const openNav = () => {
        nav.classList.add('is-open');
        toggle.setAttribute('aria-expanded', 'true');
        mobileMenu.classList.add('is-open');
        mobileMenu.setAttribute('aria-hidden', 'false');
        body.classList.add('nav-open');
        mobileMenu.focus();
    };

    const closeNav = () => {
        nav.classList.remove('is-open');
        toggle.setAttribute('aria-expanded', 'false');
        mobileMenu.classList.remove('is-open');
        mobileMenu.setAttribute('aria-hidden', 'true');
        body.classList.remove('nav-open');
        toggle.focus();
    };

    toggle.addEventListener('click', () => {
        const isOpen = nav.classList.contains('is-open');
        if (isOpen) {
            closeNav();
        } else {
            openNav();
        }
    });

    mobileMenu.addEventListener('click', (e) => {
        if (e.target.closest('.nav-close')) {
            closeNav();
            return;
        }
        const link = e.target.closest('a');
        if (link) closeNav();
    });

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') closeNav();
    });

    // 自動で開閉状態をリセット（横幅を広げた時など）
    const mq = window.matchMedia('(min-width: 1101px)');
    mq.addEventListener('change', (e) => {
        if (e.matches) closeNav();
    });
});
