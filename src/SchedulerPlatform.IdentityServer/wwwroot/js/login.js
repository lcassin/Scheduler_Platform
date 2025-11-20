document.addEventListener('DOMContentLoaded', function () {
    const toggleButton = document.getElementById('togglePassword');
    const passwordInput = document.getElementById('passwordInput');
    const toggleIcon = document.getElementById('toggleIcon');
    
    if (!toggleButton || !passwordInput || !toggleIcon) {
        return;
    }
    
    function setState(showPassword) {
        passwordInput.type = showPassword ? 'text' : 'password';
        toggleButton.setAttribute('aria-label', showPassword ? 'Hide password' : 'Show password');
        
        if (showPassword) {
            toggleIcon.classList.remove('glyphicon-eye-close');
            toggleIcon.classList.add('glyphicon-eye-open');
        } else {
            toggleIcon.classList.remove('glyphicon-eye-open');
            toggleIcon.classList.add('glyphicon-eye-close');
        }
    }
    
    setState(passwordInput.type === 'text');
    
    toggleButton.addEventListener('click', function () {
        setState(passwordInput.type === 'password');
    });
});
