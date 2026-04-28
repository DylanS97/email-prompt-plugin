(function () {
    'use strict';

    var DISMISSED_KEY = 'jellyseerrEmailPromptDismissed';
    var checked = false;

    async function check() {
        if (checked || sessionStorage.getItem(DISMISSED_KEY)) {
            return;
        }

        if (typeof ApiClient === 'undefined' || !ApiClient.isLoggedIn()) {
            return;
        }

        try {
            var response = await fetch('/JellyseerrIntegration/EmailPrompt/Status', {
                headers: {
                    'Authorization': 'MediaBrowser Token="' + ApiClient.accessToken() + '"'
                }
            });

            if (!response.ok) {
                return;
            }

            var data = await response.json();
            if (data.needsEmail && data.settingsUrl) {
                showBanner(data.settingsUrl);
            }
        } catch (e) {
            // Fail silently — never interrupt normal Jellyfin usage
        }

        checked = true;
    }

    function showBanner(url) {
        if (document.getElementById('jellyseerr-email-banner')) {
            return;
        }

        var banner = document.createElement('div');
        banner.id = 'jellyseerr-email-banner';
        banner.setAttribute('style', [
            'position:fixed',
            'top:0',
            'left:0',
            'right:0',
            'z-index:99999',
            'background:#b87800',
            'color:#fff',
            'padding:10px 16px',
            'display:flex',
            'align-items:center',
            'justify-content:space-between',
            'font-size:13px',
            'box-shadow:0 2px 6px rgba(0,0,0,.5)'
        ].join(';'));

        var message = document.createElement('span');
        var link = document.createElement('a');
        link.href = url;
        link.target = '_blank';
        link.rel = 'noopener noreferrer';
        link.textContent = 'request server account';
        link.setAttribute('style', 'color:#fff;text-decoration:underline;font-weight:bold');

        message.appendChild(document.createTextNode('Add your email to your '));
        message.appendChild(link);
        message.appendChild(document.createTextNode(' to receive media notifications.'));

        var dismissBtn = document.createElement('button');
        dismissBtn.setAttribute('aria-label', 'Dismiss notification');
        dismissBtn.setAttribute('style', [
            'background:none',
            'border:none',
            'color:#fff',
            'cursor:pointer',
            'font-size:20px',
            'line-height:1',
            'padding:0 0 0 16px',
            'flex-shrink:0'
        ].join(';'));
        dismissBtn.textContent = '\xd7';
        dismissBtn.addEventListener('click', function () {
            banner.remove();
            sessionStorage.setItem(DISMISSED_KEY, '1');
        });

        banner.appendChild(message);
        banner.appendChild(dismissBtn);
        document.body.insertBefore(banner, document.body.firstChild);
    }

    // Check on SPA navigation
    document.addEventListener('viewshow', check);

    // Initial check after a short delay to allow ApiClient to initialize
    setTimeout(check, 2000);
}());
