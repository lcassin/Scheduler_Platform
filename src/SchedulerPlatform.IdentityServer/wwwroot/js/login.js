document.addEventListener('DOMContentLoaded', function () {
    const toggleButton = document.getElementById('togglePassword');
    if (toggleButton) {
        toggleButton.addEventListener('click', function () {
            const passwordInput = document.getElementById('passwordInput');
            const toggleIcon = document.getElementById('toggleIcon');
            
            if (passwordInput.type === 'password') {
                passwordInput.type = 'text';
                toggleIcon.classList.remove('glyphicon-eye-close');
                toggleIcon.classList.add('glyphicon-eye-open');
            } else {
                passwordInput.type = 'password';
                toggleIcon.classList.remove('glyphicon-eye-open');
                toggleIcon.classList.add('glyphicon-eye-close');
            }
        });
    }
});
