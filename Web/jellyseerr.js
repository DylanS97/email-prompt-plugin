(function () {
    'use strict';

    var DISMISSED_KEY = 'jellyseerrEmailPromptDismissed';
    var checked = false;

    async function check() {
        if (checked || sessionStorage.getItem(DISMISSED_KEY)) {
            return;
        }

        var token;
        try {
            token = typeof ApiClient !== 'undefined' ? ApiClient.accessToken() : null;
        } catch (e) {
            return;
        }

        if (!token) {
            return;
        }

        try {
            var response = await fetch('/JellyseerrIntegration/EmailPrompt/Status', {
                headers: {
                    'Authorization': 'MediaBrowser Token="' + token + '"'
                }
            });

            if (!response.ok) {
                return;
            }

            // Mark checked as soon as we get a valid server response so we don't re-poll
            checked = true;

            var data = await response.json();
            if (data.needsEmail) {
                showBanner();
            }
        } catch (e) {
            // Fail silently — never interrupt normal Jellyfin usage
        }
    }

    function showBanner() {
        if (document.getElementById('jellyseerr-email-banner')) {
            return;
        }

        var banner = document.createElement('div');
        banner.id = 'jellyseerr-email-banner';
        banner.setAttribute('style', [
            'position:fixed',
            'bottom:0',
            'left:0',
            'right:0',
            'z-index:99999',
            'background:#b87800',
            'color:#fff',
            'padding:10px 16px',
            'display:flex',
            'align-items:center',
            'gap:12px',
            'font-size:13px',
            'box-shadow:0 -2px 6px rgba(0,0,0,.5)'
        ].join(';'));

        var left = document.createElement('div');
        left.setAttribute('style', 'display:flex;align-items:center;gap:8px;flex:1;flex-wrap:wrap');

        var label = document.createElement('span');
        label.textContent = 'Add your email to receive media request notifications:';

        var input = document.createElement('input');
        input.type = 'email';
        input.placeholder = 'your@email.com';
        input.setAttribute('style', [
            'padding:4px 8px',
            'border-radius:4px',
            'border:none',
            'font-size:13px',
            'color:#111',
            'min-width:200px'
        ].join(';'));

        var saveBtn = document.createElement('button');
        saveBtn.textContent = 'Save';
        saveBtn.setAttribute('style', [
            'padding:4px 12px',
            'border-radius:4px',
            'border:none',
            'background:#fff',
            'color:#b87800',
            'font-size:13px',
            'font-weight:bold',
            'cursor:pointer'
        ].join(';'));

        var errorMsg = document.createElement('span');
        errorMsg.setAttribute('style', 'color:#ffd0d0;font-size:12px');

        left.appendChild(label);
        left.appendChild(input);
        left.appendChild(saveBtn);
        left.appendChild(errorMsg);

        var dismissBtn = document.createElement('button');
        dismissBtn.setAttribute('aria-label', 'Dismiss notification');
        dismissBtn.setAttribute('style', [
            'background:none',
            'border:none',
            'color:#fff',
            'cursor:pointer',
            'font-size:20px',
            'line-height:1',
            'padding:0',
            'flex-shrink:0'
        ].join(';'));
        dismissBtn.textContent = '\xd7';
        dismissBtn.addEventListener('click', function () {
            banner.remove();
            sessionStorage.setItem(DISMISSED_KEY, '1');
        });

        saveBtn.addEventListener('click', async function () {
            var email = input.value.trim();
            if (!email || !email.includes('@')) {
                errorMsg.textContent = 'Please enter a valid email address.';
                return;
            }

            input.disabled = true;
            saveBtn.disabled = true;
            errorMsg.textContent = '';

            try {
                var response = await fetch('/JellyseerrIntegration/EmailPrompt/Email', {
                    method: 'PUT',
                    headers: {
                        'Authorization': 'MediaBrowser Token="' + ApiClient.accessToken() + '"',
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ email: email })
                });

                if (response.ok || response.status === 204) {
                    showSuccess(banner, dismissBtn);
                } else {
                    errorMsg.textContent = 'Failed to save — please try again.';
                    input.disabled = false;
                    saveBtn.disabled = false;
                }
            } catch (e) {
                errorMsg.textContent = 'Failed to save — please try again.';
                input.disabled = false;
                saveBtn.disabled = false;
            }
        });

        banner.appendChild(left);
        banner.appendChild(dismissBtn);
        document.body.appendChild(banner);
    }

    function showSuccess(banner, dismissBtn) {
        while (banner.firstChild) {
            banner.removeChild(banner.firstChild);
        }

        banner.style.background = '#1a7a1a';

        var msg = document.createElement('span');
        msg.setAttribute('style', 'flex:1');
        msg.textContent = 'Email saved. You can update it anytime in your request server account settings.';

        banner.appendChild(msg);
        banner.appendChild(dismissBtn);
    }

    // Poll every 500ms until ApiClient has an access token, then run the initial check.
    // Falls back to viewshow for SPA navigation after that.
    var initPoll = setInterval(function () {
        try {
            if (typeof ApiClient !== 'undefined' && ApiClient.accessToken()) {
                clearInterval(initPoll);
                check();
            }
        } catch (e) {
            clearInterval(initPoll);
        }
    }, 500);

    // Give up if the client never becomes ready (e.g. user is on login page)
    setTimeout(function () { clearInterval(initPoll); }, 30000);

    document.addEventListener('viewshow', check);
}());
