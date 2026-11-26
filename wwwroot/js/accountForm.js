// Submit after saving the cart without recursively triggering the Blazor handler.
export function submit(form) {
    HTMLFormElement.prototype.submit.call(form);
}
