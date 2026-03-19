// JavaScript for BC ASP Bakery

// Auto-hide alerts after 5 seconds
document.addEventListener("DOMContentLoaded", function() {
    setTimeout(function() {
        var alerts = document.querySelectorAll(".alert");
        alerts.forEach(function(alert) {
            var bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        });
    }, 5000);
});

// Confirm delete actions
function confirmDelete(message) {
    return confirm(message || "Bạn có chắc chắn muốn xóa?");
}

// Format currency
function formatCurrency(amount) {
    return new Intl.NumberFormat("vi-VN", {
        style: "currency",
        currency: "VND",
    }).format(amount);
}