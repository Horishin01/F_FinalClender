// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', () => {
    console.info('[site.js] loaded');
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

    const installButtons = Array.from(document.querySelectorAll('[data-pwa-install-button]'));
    const installHints = Array.from(document.querySelectorAll('[data-pwa-install-hint]'));
    console.info('[site.js] install buttons found:', installButtons.length);

    const setVisible = (show, reason) => {
        installButtons.forEach(btn => btn.dataset.visible = show ? 'true' : 'false');
        installHints.forEach(hint => hint.dataset.visible = show ? 'true' : 'false');
        if (!show && reason) console.info('[site.js] install hidden:', reason);
    };

    const isSecure = location.protocol === 'https:' || location.hostname === 'localhost' || location.hostname === '127.0.0.1';
    const isStandalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
    let deferredPrompt = null;

    if (!isSecure) {
        setVisible(false, 'insecure-context');
    } else if (isStandalone) {
        setVisible(false, 'already-installed');
    } else {
        setVisible(false, 'waiting-beforeinstallprompt');
    }

    if ('serviceWorker' in navigator && isSecure) {
        window.addEventListener('load', () => {
            navigator.serviceWorker.register('/sw.js')
                .then(reg => console.info('[site.js] sw registered', reg.scope))
                .catch(err => console.warn('[site.js] sw register failed', err));
        });
    } else {
        console.info('[site.js] sw registration skipped (insecure or unsupported)');
    }

    window.addEventListener('beforeinstallprompt', (e) => {
        console.info('[site.js] beforeinstallprompt fired');
        e.preventDefault();
        deferredPrompt = e;
        setVisible(true);
    });

    window.addEventListener('appinstalled', () => {
        console.info('[site.js] appinstalled');
        deferredPrompt = null;
        setVisible(false, 'installed');
    });

    installButtons.forEach(btn => {
        btn.addEventListener('click', async () => {
            console.info('[site.js] install clicked');
            if (!deferredPrompt) {
                setVisible(false, 'no-beforeinstallprompt');
                return;
            }

            deferredPrompt.prompt();
            const { outcome } = await deferredPrompt.userChoice;
            console.info('[site.js] userChoice:', outcome);
            if (outcome === 'accepted') {
                setVisible(false, 'accepted');
            }
            deferredPrompt = null;
        });
    });

    setTimeout(() => {
        if (!deferredPrompt && !isStandalone && isSecure) {
            console.info('[site.js] beforeinstallprompt not fired (browser/state not eligible?)');
        }
    }, 5000);
});
