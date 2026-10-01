// Control del panel lateral del visor (fijo vs oculto).
// Permite anclar el panel (fijo, desplazando el mapa) o colapsarlo para vista completa del mapa.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var CLAVE_LOCALSTORAGE = 'visorSIG.panelFijo';

    function obtenerElementos() {
        return {
            shell: document.querySelector('.visor-shell'),
            panel: document.querySelector('.visor-panel'),
            btnFijar: document.getElementById('btn-fijar-panel'),
            btnReabrir: document.getElementById('btn-reabrir-panel')
        };
    }

    function refrescarMapa() {
        if (ns.mapa && typeof ns.mapa.refrescarTamano === 'function') {
            ns.mapa.refrescarTamano();
            setTimeout(function () {
                ns.mapa.refrescarTamano();
            }, 100);
            setTimeout(function () {
                ns.mapa.refrescarTamano();
            }, 300);
        }
    }

    function aplicarEstado(fijo, elementos) {
        elementos = elementos || obtenerElementos();
        if (!elementos.shell) {
            return;
        }

        if (fijo) {
            elementos.shell.classList.remove('visor-shell--panel-oculto');
            if (elementos.btnFijar) {
                elementos.btnFijar.setAttribute('aria-pressed', 'true');
                elementos.btnFijar.title = 'Ocultar panel lateral (ampliar mapa)';
                var txt = elementos.btnFijar.querySelector('.visor-panel__btn-fijar-texto');
                if (txt) {
                    txt.textContent = 'Fijo';
                }
            }
            if (elementos.btnReabrir) {
                elementos.btnReabrir.hidden = true;
            }
        } else {
            elementos.shell.classList.add('visor-shell--panel-oculto');
            if (elementos.btnFijar) {
                elementos.btnFijar.setAttribute('aria-pressed', 'false');
                elementos.btnFijar.title = 'Fijar panel de capas';
                var txt2 = elementos.btnFijar.querySelector('.visor-panel__btn-fijar-texto');
                if (txt2) {
                    txt2.textContent = 'Oculto';
                }
            }
            if (elementos.btnReabrir) {
                elementos.btnReabrir.hidden = false;
            }
        }

        try {
            window.localStorage.setItem(CLAVE_LOCALSTORAGE, fijo ? '1' : '0');
        } catch (e) {
            // Sin localStorage disponible no se persiste.
        }

        refrescarMapa();
    }

    function alternar() {
        var elementos = obtenerElementos();
        if (!elementos.shell) {
            return;
        }
        var estaOculto = elementos.shell.classList.contains('visor-shell--panel-oculto');
        aplicarEstado(estaOculto, elementos);
    }

    function iniciar() {
        var elementos = obtenerElementos();
        if (!elementos.shell) {
            return;
        }

        if (elementos.btnFijar) {
            elementos.btnFijar.addEventListener('click', function (e) {
                e.preventDefault();
                alternar();
            });
        }

        if (elementos.btnReabrir) {
            elementos.btnReabrir.addEventListener('click', function (e) {
                e.preventDefault();
                aplicarEstado(true, elementos);
            });
        }

        var guardado = null;
        try {
            guardado = window.localStorage.getItem(CLAVE_LOCALSTORAGE);
        } catch (e) {
            // Ignorar
        }

        if (guardado === '0') {
            aplicarEstado(false, elementos);
        } else {
            aplicarEstado(true, elementos);
        }
    }

    ns.panel = {
        aplicarEstado: aplicarEstado,
        alternar: alternar,
        iniciar: iniciar
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
})(window.VisorSIG);
