(() => {
    let dialog = null;
    let previousFocus = null;
    let inertElements = [];
    const focusable = () => [...dialog.querySelectorAll('a[href], button, input, select, textarea, summary, [tabindex]')]
        .filter(element => !element.disabled && element.tabIndex >= 0 && element.getClientRects().length);
    const update = () => {
        const next = document.querySelector('[role="dialog"][aria-modal="true"]');
        if (next === dialog) return;
        for (const element of inertElements) element.inert = false;
        inertElements = [];
        const returningTo = previousFocus;
        dialog = next;
        if (dialog) {
            previousFocus = document.activeElement;
            for (let node = dialog; node.parentElement; node = node.parentElement) {
                for (const sibling of node.parentElement.children) {
                    if (sibling !== node && sibling instanceof HTMLElement && !sibling.inert) {
                        sibling.inert = true;
                        inertElements.push(sibling);
                    }
                }
                if (node.parentElement === document.body) break;
            }
            dialog.focus();
        } else {
            previousFocus = null;
            if (returningTo?.isConnected) returningTo.focus();
        }
    };
    document.addEventListener('keydown', event => {
        if (!dialog || event.key !== 'Tab') return;
        const controls = focusable();
        const first = controls[0];
        const last = controls.at(-1);
        if (!first) { event.preventDefault(); dialog.focus(); return; }
        if (event.shiftKey && (!dialog.contains(document.activeElement) || document.activeElement === first || document.activeElement === dialog)) {
            event.preventDefault(); last.focus();
        } else if (!event.shiftKey && (!dialog.contains(document.activeElement) || document.activeElement === last || document.activeElement === dialog)) {
            event.preventDefault(); first.focus();
        }
    }, true);
    document.addEventListener('focusin', event => {
        if (dialog && !dialog.contains(event.target)) dialog.focus();
    });
    new MutationObserver(update).observe(document.body, { childList: true, subtree: true });
    update();
})();
