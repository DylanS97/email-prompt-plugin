(function () {
    'use strict';

    var DISMISSED_KEY = 'jellyseerrEmailPromptDismissed';
    var DELETION_BTN_ID = 'jellyseerr-delete-request-btn';
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
        label.textContent = 'Add your email to receive notifications: ';

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
        watchForMedia(banner);
    }

    function watchForMedia(banner) {
        function anyPlaying() {
            return Array.prototype.some.call(
                document.querySelectorAll('video, audio'),
                function (m) { return !m.paused && !m.ended && m.readyState > 0; }
            );
        }

        var interval = setInterval(function () {
            if (!document.body.contains(banner)) {
                clearInterval(interval);
                return;
            }
            banner.style.display = anyPlaying() ? 'none' : 'flex';
        }, 1000);
    }

    function showSuccess(banner, dismissBtn) {
        while (banner.firstChild) {
            banner.removeChild(banner.firstChild);
        }

        banner.style.background = '#1a7a1a';

        var msg = document.createElement('span');
        msg.setAttribute('style', 'flex:1');
        msg.textContent = 'Email saved. You can update it anytime in your request server account settings. Be sure to check your inbox (and spam folder) for a confirmation email!';

        banner.appendChild(msg);
        banner.appendChild(dismissBtn);
    }

    async function checkDeletionRequest() {
        // Remove stale button from previous page navigation
        var existing = document.getElementById(DELETION_BTN_ID);
        if (existing) {
            existing.remove();
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

        var hash = window.location.hash;
        var idMatch = hash.match(/[?&]id=([a-f0-9]+)/i);
        if (!idMatch) {
            return;
        }
        var jellyfinItemId = idMatch[1];

        try {
            var resp = await fetch(
                '/JellyseerrIntegration/MediaRequest/Status?jellyfinItemId=' + jellyfinItemId,
                { headers: { 'Authorization': 'MediaBrowser Token="' + token + '"' } }
            );
            if (!resp.ok) {
                return;
            }

            var status = await resp.json();
            if (status.HasRequest && status.WebhookConfigured) {
                showDeletionButton(token, jellyfinItemId, status);
            }
        } catch (e) {
            // Fail silently — never interrupt normal Jellyfin usage
        }
    }

    function showDeletionButton(token, jellyfinItemId, status) {
        if (document.getElementById(DELETION_BTN_ID)) {
            return;
        }

        var btn = document.createElement('button');
        btn.id = DELETION_BTN_ID;
        btn.textContent = 'Request Deletion';
        btn.setAttribute('style', [
            'position:fixed',
            'bottom:16px',
            'right:16px',
            'z-index:99998',
            'background:#c0392b',
            'color:#fff',
            'padding:8px 16px',
            'border:none',
            'border-radius:4px',
            'font-size:13px',
            'font-weight:bold',
            'cursor:pointer',
            'box-shadow:0 2px 6px rgba(0,0,0,.5)'
        ].join(';'));

        btn.addEventListener('click', async function () {
            if (!window.confirm('Submit a request to have this item deleted from the library?')) {
                return;
            }

            btn.disabled = true;
            btn.textContent = 'Sending…';

            try {
                var r = await fetch('/JellyseerrIntegration/DeletionRequest', {
                    method: 'POST',
                    headers: {
                        'Authorization': 'MediaBrowser Token="' + token + '"',
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        jellyfinMediaId: jellyfinItemId,
                        mediaType: status.MediaType,
                        mediaTitle: status.MediaTitle
                    })
                });

                if (r.status === 409) {
                    btn.textContent = 'Already requested';
                    btn.style.background = '#7a5c00';
                    btn.disabled = true;
                    setTimeout(function () {
                        if (document.body.contains(btn)) {
                            btn.remove();
                        }
                    }, 5000);
                } else if (r.ok || r.status === 204) {
                    btn.textContent = 'Request Sent';
                    btn.style.background = '#1a7a1a';
                    setTimeout(function () {
                        if (document.body.contains(btn)) {
                            btn.remove();
                        }
                    }, 3000);
                } else {
                    btn.textContent = 'Failed — try again';
                    btn.style.background = '#7a1a1a';
                    btn.disabled = false;
                }
            } catch (e) {
                btn.textContent = 'Failed — try again';
                btn.style.background = '#7a1a1a';
                btn.disabled = false;
            }
        });

        document.body.appendChild(btn);
        watchForMedia(btn);
    }

    // ----- JellySeerr Search Integration -----

    var SEARCH_CONTAINER_ID = 'jellyseerr-search-results';
    var TMDB_IMG_BASE = 'https://image.tmdb.org/t/p/w92';
    var searchDebounceTimer = null;
    var lastSearchQuery = null;
    var searchPagePollTimer = null;
    var currentSearchId = 0;

    function getSearchQuery() {
        var hash = window.location.hash;
        var match = hash.match(/[?&]query=([^&]*)/);
        if (!match || !match[1]) {
            return null;
        }
        try {
            return decodeURIComponent(match[1].replace(/\+/g, ' ')) || null;
        } catch (e) {
            return match[1] || null;
        }
    }

    function isSearchPage() {
        var hash = window.location.hash;
        return hash === '#/search'
            || hash.indexOf('#/search?') === 0
            || hash.indexOf('#/search&') === 0;
    }

    function removeSearchContainer() {
        var existing = document.getElementById(SEARCH_CONTAINER_ID);
        if (existing) {
            existing.remove();
        }
    }

    function startSearchPagePolling() {
        if (searchPagePollTimer) {
            return;
        }
        searchPagePollTimer = setInterval(function () {
            if (!isSearchPage()) {
                stopSearchPagePolling();
                removeSearchContainer();
                lastSearchQuery = null;
                return;
            }

            var query = getSearchQuery();
            if (query === lastSearchQuery) {
                return;
            }
            lastSearchQuery = query;

            clearTimeout(searchDebounceTimer);
            var sid = ++currentSearchId;

            if (!query) {
                removeSearchContainer();
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

            searchDebounceTimer = setTimeout(function () {
                runSearch(query, token, sid);
            }, 400);
        }, 300);
    }

    function stopSearchPagePolling() {
        if (searchPagePollTimer) {
            clearInterval(searchPagePollTimer);
            searchPagePollTimer = null;
        }
    }

    function findInsertTarget() {
        return document.querySelector('.searchResults')
            || document.querySelector('.noItemsMessage')
            || document.querySelector('.itemsContainer')
            || document.querySelector('.focuscontainer-x')
            || document.querySelector('.pageTabContent')
            || document.querySelector('.mainAnimatedPage')
            || document.querySelector('[data-role="content"] .content-primary')
            || null;
    }

    async function runSearch(query, token, sid) {
        removeSearchContainer();

        var results;
        try {
            var resp = await fetch(
                '/JellyseerrIntegration/Search?query=' + encodeURIComponent(query),
                { headers: { 'Authorization': 'MediaBrowser Token="' + token + '"' } }
            );
            if (!resp.ok) {
                return;
            }
            results = await resp.json();
        } catch (e) {
            return;
        }

        if (sid !== currentSearchId || !results || results.length === 0) {
            return;
        }

        var container = buildResultsContainer(results);

        var attempts = 0;
        var insertPoll = setInterval(function () {
            if (sid !== currentSearchId) {
                clearInterval(insertPoll);
                return;
            }
            attempts++;
            var target = findInsertTarget();
            if (target || attempts >= 30) {
                clearInterval(insertPoll);
                if (sid !== currentSearchId) {
                    return;
                }
                if (target) {
                    target.parentNode.insertBefore(container, target.nextSibling);
                } else {
                    document.body.appendChild(container);
                }
            }
        }, 100);
    }

    function buildResultsContainer(results) {
        var container = document.createElement('div');
        container.id = SEARCH_CONTAINER_ID;
        container.setAttribute('style', 'padding:16px;margin-top:8px');

        var heading = document.createElement('h2');
        heading.textContent = 'Request from JellySeerr';
        heading.setAttribute('style', 'font-size:16px;font-weight:bold;margin:0 0 10px 0;color:#ddd');
        container.appendChild(heading);

        var list = document.createElement('div');
        list.setAttribute('style', 'display:flex;flex-direction:column;gap:8px');
        container.appendChild(list);

        results.forEach(function (item) {
            if (item.MediaStatus === 5) {
                return;
            }

            var displayTitle = item.Title || item.Name || 'Unknown';
            var dateStr = item.ReleaseDate || item.FirstAirDate || '';
            var year = dateStr ? dateStr.substring(0, 4) : '';
            var mediaId = item.Id;
            var mediaType = item.MediaType;

            var card = document.createElement('div');
            card.setAttribute('style', [
                'display:flex',
                'align-items:center',
                'gap:12px',
                'background:#1a1a1a',
                'border-radius:6px',
                'padding:8px 12px',
                'overflow:hidden'
            ].join(';'));

            if (item.PosterPath) {
                var img = document.createElement('img');
                img.src = TMDB_IMG_BASE + item.PosterPath;
                img.alt = displayTitle;
                img.setAttribute('style', [
                    'width:46px',
                    'height:69px',
                    'object-fit:cover',
                    'border-radius:3px',
                    'flex-shrink:0'
                ].join(';'));
                img.onerror = function () { img.style.display = 'none'; };
                card.appendChild(img);
            }

            var info = document.createElement('div');
            info.setAttribute('style', 'flex:1;min-width:0');

            var titleEl = document.createElement('div');
            titleEl.setAttribute('style', 'font-size:14px;font-weight:bold;color:#eee;white-space:nowrap;overflow:hidden;text-overflow:ellipsis');
            titleEl.textContent = year ? displayTitle + ' (' + year + ')' : displayTitle;
            info.appendChild(titleEl);

            var typeEl = document.createElement('div');
            typeEl.setAttribute('style', 'font-size:11px;color:#aaa;margin-top:2px');
            typeEl.textContent = mediaType === 'tv' ? 'TV Series' : 'Movie';
            info.appendChild(typeEl);

            card.appendChild(info);

            var actionEl = document.createElement('div');
            actionEl.setAttribute('style', 'flex-shrink:0');

            var pendingStatuses = { 2: 'Pending', 3: 'Processing', 4: 'Partially Available' };
            if (pendingStatuses[item.MediaStatus]) {
                var badge = document.createElement('span');
                badge.textContent = pendingStatuses[item.MediaStatus];
                badge.setAttribute('style', [
                    'display:inline-block',
                    'padding:4px 10px',
                    'border-radius:4px',
                    'background:#444',
                    'color:#bbb',
                    'font-size:12px'
                ].join(';'));
                actionEl.appendChild(badge);
            } else {
                var btn = document.createElement('button');
                btn.textContent = 'Request';
                btn.setAttribute('style', [
                    'padding:5px 14px',
                    'border-radius:4px',
                    'border:none',
                    'background:#2196f3',
                    'color:#fff',
                    'font-size:13px',
                    'font-weight:bold',
                    'cursor:pointer'
                ].join(';'));

                btn.addEventListener('click', async function () {
                    btn.disabled = true;
                    btn.textContent = 'Sending…';
                    btn.style.background = '#555';

                    try {
                        var currentToken;
                        try {
                            currentToken = ApiClient.accessToken();
                        } catch (e) {
                            currentToken = null;
                        }
                        if (!currentToken) {
                            btn.textContent = 'Not signed in';
                            return;
                        }

                        var r = await fetch('/JellyseerrIntegration/MediaRequest', {
                            method: 'POST',
                            headers: {
                                'Authorization': 'MediaBrowser Token="' + currentToken + '"',
                                'Content-Type': 'application/json'
                            },
                            body: JSON.stringify({ mediaType: mediaType, mediaId: mediaId })
                        });

                        if (r.status === 409) {
                            btn.textContent = 'Already Requested';
                            btn.style.background = '#444';
                            btn.style.color = '#aaa';
                        } else if (r.ok || r.status === 204) {
                            btn.textContent = 'Requested ✓';
                            btn.style.background = '#1a7a1a';
                        } else {
                            btn.textContent = 'Failed — retry';
                            btn.style.background = '#7a1a1a';
                            btn.disabled = false;
                        }
                    } catch (e) {
                        btn.textContent = 'Failed — retry';
                        btn.style.background = '#7a1a1a';
                        btn.disabled = false;
                    }
                });

                actionEl.appendChild(btn);
            }

            card.appendChild(actionEl);
            list.appendChild(card);
        });

        return container;
    }

    function checkSearchPage() {
        if (isSearchPage()) {
            startSearchPagePolling();
        } else {
            stopSearchPagePolling();
            removeSearchContainer();
            lastSearchQuery = null;
        }
    }

    // ----- Initialisation -----

    // Poll every 500ms until ApiClient has an access token, then run the initial check.
    var initPoll = setInterval(function () {
        try {
            if (typeof ApiClient !== 'undefined' && ApiClient.accessToken()) {
                clearInterval(initPoll);
                check();
                checkDeletionRequest();
                checkSearchPage();
            }
        } catch (e) {
            clearInterval(initPoll);
        }
    }, 500);

    // Give up if the client never becomes ready (e.g. user is on login page)
    setTimeout(function () { clearInterval(initPoll); }, 30000);

    document.addEventListener('viewshow', function () {
        check();
        checkDeletionRequest();
        checkSearchPage();
    });
}());
