window.addEventListener('load', function() {
    // Auto-focus on authorization input when opened
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.type === 'childList') {
                const authInput = document.querySelector('.auth-container input[type="text"]');
                if (authInput) {
                    authInput.focus();
                    authInput.placeholder = 'Paste your access token here...';
                }
            }
        });
    });
    
    observer.observe(document.body, {
        childList: true,
        subtree: true
    });
});