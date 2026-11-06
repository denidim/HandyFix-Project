// Booking Confirmed (Views/Booking/Confirmed.cshtml): confetti falls for six seconds when the
// page opens.
// Simple Confetti Animation
const canvas = document.getElementById('confetti');
const ctx = canvas.getContext('2d');
let pieces = [];
const numberOfPieces = 75;
const colors = ['#006591', '#39b8fd', '#1e293b', '#4edea3'];

function updateCanvasSize() {
    canvas.width = window.innerWidth;
    canvas.height = window.innerHeight;
}

class Piece {
    constructor() {
        this.x = Math.random() * canvas.width;
        this.y = Math.random() * canvas.height - canvas.height;
        this.rotation = Math.random() * 360;
        this.color = colors[Math.floor(Math.random() * colors.length)];
        this.diameter = Math.random() * 10 + 5;
        this.speed = Math.random() * 3 + 2;
    }

    update() {
        this.y += this.speed;
        this.rotation += this.speed;
        if (this.y > canvas.height) {
            this.y = -20;
            this.x = Math.random() * canvas.width;
        }
    }

    draw() {
        ctx.save();
        ctx.translate(this.x, this.y);
        ctx.rotate(this.rotation * Math.PI / 180);
        ctx.fillStyle = this.color;
        ctx.fillRect(-this.diameter / 2, -this.diameter / 2, this.diameter, this.diameter);
        ctx.restore();
    }
}

function initConfetti() {
    updateCanvasSize();
    for (let i = 0; i < numberOfPieces; i++) {
        pieces.push(new Piece());
    }
    animate();
}

function animate() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    pieces.forEach(p => {
        p.update();
        p.draw();
    });
    requestAnimationFrame(animate);
}

window.addEventListener('resize', updateCanvasSize);
window.addEventListener('load', () => {
    initConfetti();
    // Stop confetti after 6 seconds
    setTimeout(() => {
        pieces = [];
    }, 6000);
});
