document.addEventListener('DOMContentLoaded', function () {
    const toggleButton = document.getElementById('togglePassword');
    const passwordInput = document.getElementById('passwordInput');
    const toggleLabel = document.getElementById('toggleIcon');
    
    if (!toggleButton || !passwordInput || !toggleLabel) {
        return;
    }
    
    function setState(showPassword) {
        passwordInput.type = showPassword ? 'text' : 'password';
        toggleLabel.textContent = showPassword ? 'Hide' : 'Show';
        toggleButton.setAttribute('aria-label', showPassword ? 'Hide password' : 'Show password');
    }
    
    setState(passwordInput.type === 'text');
    
    toggleButton.addEventListener('click', function () {
        setState(passwordInput.type === 'password');
    });
});
