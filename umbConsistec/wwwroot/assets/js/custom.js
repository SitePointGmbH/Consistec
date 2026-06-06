function initApp() {
    console.log("Initializing App...");
    // 1. Initialize Component Logic
    initFaqAccordion();
    initHeaderLogic();
    initHeroLogic();
    initStackingLayers();
    initRoadmapScrub();
    initScrollReveal();
    initSlidingText();
    initParallax();
    initCaseStudiesParticles();
    initVideoTriggers();
    initBenefitsFocus();
    initParticles();
    initStickyBadge();

    // 2. Setup Router (Removed: Not needed in multi-page Umbraco site, causes pages to go blank)
    // setupRouter();

    // 3. Global Handlers for Interactive Sections
    window.switchIndustry = (type, el) => {
        const contentArea = document.getElementById('industry-content-placeholder');
        if (!contentArea) return;

        const targetSection = document.getElementById('desktop-content-' + type);
        if (!targetSection) return;

        document.querySelectorAll('.industry-tab-box').forEach(box => box.classList.remove('active'));
        el.classList.add('active');

        contentArea.style.opacity = '0';

        setTimeout(() => {
            document.querySelectorAll('.industry-content-section').forEach(section => {
                section.classList.add('d-none');
            });

            targetSection.classList.remove('d-none');

            contentArea.style.opacity = '1';
            contentArea.style.transition = 'opacity 0.3s ease';
        }, 150);
    };
}

function setupRouter() {

    function router() {

        const hash = window.location.hash || '#home';

        const pages = {
            '#home': document.getElementById('home-page'),
            '#loesungen': document.getElementById('solutions-page'),
            '#einsatzbereiche': document.getElementById('einsatzbereiche-page'),
            '#ueber-uns': document.getElementById('ueber-uns-page'),
            '#referenzen': document.getElementById('referenzen-page'),
            '#kontakt': document.getElementById('kontakt-page'),
            '#impressum': document.getElementById('impressum-page')
        };

        Object.values(pages).forEach(p => {
            if (p) p.style.display = 'none';
        });

        const target = pages[hash] || pages['#home'];

        if (target) target.style.display = 'block';

        window.scrollTo(0, 0);

        setTimeout(initScrollReveal, 100);

        updateActiveMenu(hash);
    }

    function updateActiveMenu(hash) {

        document.querySelectorAll('.menu-item').forEach(link => {

            link.classList.toggle(
                'active',
                link.getAttribute('href') === hash
            );

        });
    }

    window.addEventListener('hashchange', router);

    router();
}

function initHeaderLogic() {

    const navbar = document.querySelector('.header-nav');

    if (navbar) {

        window.addEventListener('scroll', () => {

            navbar.classList.toggle(
                'scrolled',
                window.scrollY > 50
            );

            navbar.classList.toggle(
                'py-2',
                window.scrollY > 50
            );

        });
    }
}

function initHeroLogic() {

    const heroLines = [
        "Cybersicherheit beginnt mit Sichtbarkeit.",
        "Monitoring-Systeme aus Deutschland für IT & OT.",
        "Souverän. Sicher. Beherrschbar."
    ];

    const rotator = document.getElementById('heroRotator');

    let idx = 0;

    if (rotator) {

        setInterval(() => {

            rotator.style.opacity = '0';
            rotator.style.filter = 'blur(15px)';

            setTimeout(() => {

                idx = (idx + 1) % heroLines.length;

                rotator.textContent = heroLines[idx];

                rotator.style.opacity = '1';
                rotator.style.filter = 'blur(0)';

            }, 600);

        }, 5000);
    }
}

function initStackingLayers() {

    const wrapper = document.getElementById('third-section');

    if (!wrapper || window.innerWidth < 992) return;

    const sections = wrapper.querySelectorAll('.p-section');

    window.addEventListener('scroll', () => {

        const vh = window.innerHeight;

        sections.forEach((sec, idx) => {

            const rect = sec.getBoundingClientRect();

            if (rect.top <= 0 && idx < sections.length - 1) {

                const prog = Math.min(
                    1,
                    Math.max(
                        0,
                        (vh - sections[idx + 1].getBoundingClientRect().top) / vh
                    )
                );

                // No scale() — prevents browser from rasterizing/blurring text
                sec.style.transform = `translateY(${-prog * 80}px)`;
                sec.style.opacity = '1';

            } else if (rect.top > 0) {

                sec.style.transform = 'translateY(0)';
                sec.style.opacity = '1';
            }
        });
    });
}

function initRoadmapScrub() {
    const pin = document.getElementById('roadmap-pin-container');
    const bar = document.getElementById('timelineProgress');
    const steps = document.querySelectorAll('.roadmap-step');

    if (!pin || steps.length === 0) return;

    // Desktop logic
    window.addEventListener('scroll', () => {
        if (window.innerWidth <= 1024) return; // Skip desktop logic on mobile
        
        const rect = pin.getBoundingClientRect();
        const scrollDistance = rect.height - window.innerHeight;
        const currentScroll = -rect.top;

        if (currentScroll >= 0 && currentScroll <= scrollDistance) {
            let prog = (currentScroll / scrollDistance) * 100;
            prog = Math.max(0, Math.min(100, prog));

            if (bar) bar.style.width = `${prog}%`;

            steps.forEach((s, i) => {
                s.classList.toggle(
                    'active',
                    prog >= ((i + 0.1) / steps.length) * 100
                );
            });
        }
    });

    // Mobile logic: IntersectionObserver
    const observer = new IntersectionObserver((entries) => {
        if (window.innerWidth > 1024) return; // Skip mobile logic on desktop
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('active');
            } else {
                entry.target.classList.remove('active'); 
            }
        });
    }, {
        root: null,
        rootMargin: '-30% 0px -30% 0px', // Triggers when item reaches the middle 40% of the screen
        threshold: 0
    });

    steps.forEach(step => observer.observe(step));
}

function initScrollReveal() {

    const obs = new IntersectionObserver((es) => {

        es.forEach(e => {

            if (e.isIntersecting) {
                e.target.classList.add('is-visible');
            }

        });

    }, {
        threshold: 0.15,
        rootMargin: "0px 0px -100px 0px"
    });

    document.querySelectorAll('.blur-reveal')
        .forEach(el => obs.observe(el));
}

function initSlidingText() {

    const el = document.getElementById('sliding-bg-text');

    if (!el) return;

    window.addEventListener('scroll', () => {

        const r = el.parentElement.getBoundingClientRect();

        if (r.top < window.innerHeight && r.bottom > 0) {

            el.style.transform =
                `translateX(${20 - ((window.innerHeight - r.top) / (window.innerHeight + r.height) * 40)}%)`;
        }
    });
}

function initParallax() {

    // --- Ecotel: drive the parallax-bg div via translateY ---
    const ecotelBg = document.getElementById('ecotel-parallax-bg');
    const ecotelWrap = document.getElementById('ecotel-video-container');

    if (ecotelBg && ecotelWrap) {
        window.addEventListener('scroll', () => {
            const r = ecotelWrap.getBoundingClientRect();
            if (r.top < window.innerHeight && r.bottom > 0) {
                const progress = (window.innerHeight - r.top) / (window.innerHeight + r.height);
                const offset = (progress - 0.5) * 100; // -50px → +50px
                ecotelBg.style.transform = `translateY(${offset}px)`;
            }
        }, { passive: true });
    }

    // --- Generic parallax-element items (other wrappers) ---
    document.querySelectorAll('.parallax-wrapper').forEach(w => {

        const c = w.querySelector('.parallax-content, .parallax-element');

        if (!c) return;

        window.addEventListener('scroll', () => {

            const r = w.getBoundingClientRect();

            if (r.top < window.innerHeight && r.bottom > 0) {
                c.style.transform =
                    `translateY(${((window.innerHeight - r.top) / (window.innerHeight + r.height) - 0.5) * 100}px) scale(1.1)`;
            }
        }, { passive: true });
    });
}


function initCaseStudiesParticles() {

    const canvas = document.getElementById('case-studies-particles');

    if (!canvas) return;

    const ctx = canvas.getContext('2d');

    let ps = [];

    let mx = 0, my = 0;

    document.addEventListener('mousemove', (e) => {

        mx = e.clientX;
        my = e.clientY;

    });

    function resize() {

        canvas.width = canvas.parentElement.offsetWidth;
        canvas.height = canvas.parentElement.offsetHeight;
    }

    window.addEventListener('resize', resize);

    resize();

    class P {

        constructor() {

            this.x = Math.random() * canvas.width;
            this.y = Math.random() * canvas.height;
            this.size = Math.random() * 2 + 1;
            this.sx = Math.random() * 0.5 - 0.25;
            this.sy = Math.random() * 0.5 - 0.25;
            this.bx = this.x;
            this.by = this.y;
        }

        u() {

            this.bx += this.sx;
            this.by += this.sy;

            if (this.bx > canvas.width) this.bx = 0;
            if (this.bx < 0) this.bx = canvas.width;
            if (this.by > canvas.height) this.by = 0;
            if (this.by < 0) this.by = canvas.height;

            const dx = (mx - window.innerWidth / 2) * 0.05;
            const dy = (my - window.innerHeight / 2) * 0.05;

            this.x = this.bx - dx * this.size;
            this.y = this.by - dy * this.size;
        }

        d() {

            ctx.fillStyle = 'rgba(150,150,150,0.4)';

            ctx.beginPath();

            ctx.arc(
                this.x,
                this.y,
                this.size,
                0,
                Math.PI * 2
            );

            ctx.fill();
        }
    }

    for (let i = 0; i < 80; i++) {
        ps.push(new P());
    }

    function animate() {

        ctx.clearRect(0, 0, canvas.width, canvas.height);

        ps.forEach((p, i) => {

            ps.slice(i + 1).forEach(p2 => {

                const dist = Math.hypot(
                    p.x - p2.x,
                    p.y - p2.y
                );

                if (dist < 100) {

                    ctx.beginPath();

                    ctx.strokeStyle =
                        `rgba(150,150,150,${0.2 * (1 - dist / 100)})`;

                    ctx.lineWidth = 0.5;

                    ctx.moveTo(p.x, p.y);

                    ctx.lineTo(p2.x, p2.y);

                    ctx.stroke();
                }
            });

            p.u();
            p.d();
        });

        requestAnimationFrame(animate);
    }

    animate();
}
function initStickyBadge() {
    const stickyBadge = document.querySelector(".sticky-badge");
    const closeBtn = document.querySelector(".cancle-button");

    if (stickyBadge) {
        // Start collapsed by default (programmatic safety)
        stickyBadge.classList.add("collapsed");

        let userCollapsed = false;

        // If collapsed, clicking/tapping the badge itself (the visible tab handle) expands it
        stickyBadge.addEventListener("click", function (e) {
            if (stickyBadge.classList.contains("collapsed")) {
                e.stopPropagation();
                stickyBadge.classList.remove("collapsed");
                userCollapsed = false;
            }
        });

        stickyBadge.addEventListener("touchstart", function (e) {
            if (stickyBadge.classList.contains("collapsed")) {
                e.preventDefault();
                e.stopPropagation();
                stickyBadge.classList.remove("collapsed");
                userCollapsed = false;
            }
        }, { passive: false });

        // Close badge on click / tap of the X button
        if (closeBtn) {
            const handleClose = function (e) {
                e.stopPropagation();
                e.preventDefault();
                stickyBadge.classList.add("collapsed");
                userCollapsed = true;
            };
            closeBtn.addEventListener("click", handleClose);
            closeBtn.addEventListener("touchstart", handleClose, { passive: false });
        }

        // Hover expand (Desktop/Laptop with Mouse)
        stickyBadge.addEventListener("mouseenter", function () {
            if (!userCollapsed) {
                stickyBadge.classList.remove("collapsed");
            }
        });

        // Hover collapse (Desktop/Laptop with Mouse)
        stickyBadge.addEventListener("mouseleave", function () {
            stickyBadge.classList.add("collapsed");
            userCollapsed = false; // Reset so next mouse hover works normally
        });

        // Close when clicking/tapping anywhere else on the document
        const handleOutside = function (e) {
            if (!stickyBadge.contains(e.target)) {
                stickyBadge.classList.add("collapsed");
                userCollapsed = false;
            }
        };
        document.addEventListener("click", handleOutside);
        document.addEventListener("touchstart", handleOutside, { passive: true });
    }
}

function initVideoTriggers() {
    const modalEl = document.getElementById('videoModalPopup');
    if (!modalEl) return;

    const modalVideo = document.getElementById('modalVideoPlayer');

    // Listen to Bootstrap modal show event to set video source and play
    modalEl.addEventListener('show.bs.modal', (event) => {
        const triggerEl = event.relatedTarget;
        if (!triggerEl) return;

        const videoEl = triggerEl.querySelector('video');
        if (videoEl && videoEl.src) {
            modalVideo.src = videoEl.src;
            modalVideo.load();
            modalVideo.play().catch(err => console.log("Play failed:", err));
        }
    });

    // Stop, clear, and unload video when modal is closed
    modalEl.addEventListener('hidden.bs.modal', () => {
        modalVideo.pause();
        modalVideo.src = '';
        modalVideo.load();
    });

    // Custom click-to-toggle play/pause that avoids native controls conflict
    modalVideo.addEventListener('click', (e) => {
        const rect = modalVideo.getBoundingClientRect();
        const clickY = e.clientY - rect.top;
        // Native video controls bar height is typically around 45px at the bottom
        const controlsHeight = 45;
        if (clickY < rect.height - controlsHeight) {
            e.preventDefault();
            if (modalVideo.paused) {
                modalVideo.play().catch(err => console.log("Play failed:", err));
            } else {
                modalVideo.pause();
            }
        }
    });
}


function initBenefitsFocus() {

    const c = document.getElementById('benefits-pin-container');

    const is = document.querySelectorAll('.benefit-item');

    if (!c || is.length === 0) return;

    window.addEventListener('scroll', () => {

        const r = c.getBoundingClientRect();

        const vh = window.innerHeight;

        const sd = c.offsetHeight - vh;

        let p = Math.max(
            0,
            Math.min(1, -r.top / sd)
        );

        is.forEach((it, idx) =>

            it.classList.toggle(
                'in-focus',
                p >= (idx / is.length) - 0.05
            )
        );
    });
}


const stickyBadge = document.querySelector(".sticky-badge");
const closeBtn = document.querySelector(".cancle-button");

if (stickyBadge && closeBtn) {

    // Close sidebar
    closeBtn.addEventListener("click", function () {

        stickyBadge.classList.add("collapsed");

    });

    // Show sidebar on hover
    stickyBadge.addEventListener("mouseenter", function () {

        stickyBadge.classList.remove("collapsed");

    });

}

function initParticles() {

    if (!document.getElementById("particle-style")) return;

    particlesJS("particle-style", {
        particles: {
            number: {
                value: 80,
                density: {
                    enable: true,
                    value_area: 800
                }
            },
            color: {
                value: "#232323"
            },
            shape: {
                type: "circle"
            },
            opacity: {
                value: 0.4
            },
            size: {
                value: 4,
                random: true
            },
            line_linked: {
                enable: true,
                distance: 150,
                color: "#232323",
                opacity: 0.3,
                width: 1
            },
            move: {
                enable: true,
                speed: 6
            }
        },
        interactivity: {
            detect_on: "canvas",
            events: {
                onhover: {
                    enable: true,
                    mode: "repulse"
                },
                onclick: {
                    enable: true,
                    mode: "push"
                },
                resize: true
            },
            modes: {
                repulse: {
                    distance: 200,
                    duration: 0.4
                },
                push: {
                    particles_nb: 4
                }
            }
        },
        retina_detect: true
    });
}

function initFaqAccordion() {

    const buttons = document.querySelectorAll(
        '.accordion .accordion-button'
    );

    buttons.forEach(button => {
        button.addEventListener('click', function () {
            const target = document.querySelector(
                this.getAttribute('data-bs-target')
            );

            if (!target) return;

            const isOpen = target.classList.contains('show');
            const accordion = this.closest('.accordion');

            // If already open → close it
            if (isOpen) {
                target.classList.remove('show');
                target.style.maxHeight = null;
                this.classList.add('collapsed');
                this.setAttribute('aria-expanded', 'false');
                return;
            }

            // Close all in this accordion
            if (accordion) {
                accordion.querySelectorAll('.accordion-collapse').forEach(item => {
                    item.classList.remove('show');
                    item.style.maxHeight = null;
                });
                accordion.querySelectorAll('.accordion-button').forEach(btn => {
                    btn.classList.add('collapsed');
                    btn.setAttribute('aria-expanded', 'false');
                });
            }

            // Open current
            target.classList.add('show');
            target.style.maxHeight = target.scrollHeight + 'px';
            this.classList.remove('collapsed');
            this.setAttribute('aria-expanded', 'true');
        });
    });
}


document.addEventListener(
    'DOMContentLoaded',
    initApp
);