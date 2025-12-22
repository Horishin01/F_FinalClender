// app-viewport.js
// 役割: 画面幅に応じて <html> に data-viewport/data-vp を付与し、JS からも現在のブレークポイントを参照できるようにする。
// 主な利用箇所: events-integrated.css のレスポンシブ分岐、その他デバイス別 UI 調整。
// カスタマイズ: breakpoints の値を変更すればサイト全体の「mobile/tablet/desktop」の境界を一元管理できる。
// 使い方: window.appViewport.subscribe(cb) でリスナー登録（解除は返り値の関数を呼ぶ）。cb には 'mobile' | 'tablet' | 'desktop' が渡る。

(function () {
    const breakpoints = {
        mobile: 768,
        tablet: 1024
    };

    const listeners = new Set();
    const docEl = document.documentElement;

    const getMode = () => {
        const w = window.innerWidth || docEl.clientWidth;
        if (w < breakpoints.mobile) return 'mobile';
        if (w < breakpoints.tablet) return 'tablet';
        return 'desktop';
    };

    let current = getMode();

    function apply(next) {
        if (!next || next === current) return;
        current = next;
        docEl.dataset.viewport = next;
        docEl.dataset.vp = next; // shorthand for CSS hooks
        listeners.forEach(fn => {
            try { fn(current); } catch (err) { console.error('[appViewport] listener error', err); }
        });
    }

    function handleResize() {
        apply(getMode());
    }

    docEl.dataset.viewport = current;
    docEl.dataset.vp = current;

    window.addEventListener('resize', handleResize, { passive: true });
    window.addEventListener('orientationchange', handleResize, { passive: true });

    window.appViewport = {
        breakpoints: { ...breakpoints },
        current: () => current,
        isMobile: () => current === 'mobile',
        isTablet: () => current === 'tablet',
        isDesktop: () => current === 'desktop',
        subscribe(cb) {
            if (typeof cb !== 'function') return () => { };
            listeners.add(cb);
            // fire immediately so subscribers can sync state on init
            cb(current);
            return () => listeners.delete(cb);
        }
    };
})();
