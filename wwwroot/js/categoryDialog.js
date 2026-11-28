// Native modal dialogs provide focus containment and return focus to the opener.
const configuredDialogs = new WeakSet();

export function show(dialog) {
    if (!configuredDialogs.has(dialog)) {
        // Blazor owns Escape handling so an active deletion cannot be dismissed mid-request.
        dialog.addEventListener("cancel", event => event.preventDefault());
        configuredDialogs.add(dialog);
    }
    if (!dialog.open) dialog.showModal();
}

export function close(dialog) {
    if (dialog.open) dialog.close();
}
