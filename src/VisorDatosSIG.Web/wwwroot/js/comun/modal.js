// Diálogo modal reutilizable del workspace autenticado.
//
// Cubre las tres necesidades reales de /Roles y /Bitacora:
//   - modal.formulario(...) -> alta y edición de un rol (nombre y descripción)
//   - modal.confirmar(...)  -> confirmación de una acción sensible o de descartar cambios
//   - modal.detalle(...)    -> panel de solo lectura con pares etiqueta/valor
//
// No se usa window.confirm: la confirmación conserva la identidad visual del sistema y el foco
// se administra (foco inicial, trampa de Tab y devolución del foco al control que abrió).
// Seguridad del DOM: todo nodo se crea con document.createElement y los textos se asignan con
// textContent; nunca se usa innerHTML.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    var ID_TITULO = 'modal-titulo';
    var FOCALIZABLES = 'a[href], button:not([disabled]), input:not([disabled]),'
        + ' select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

    // Diálogo abierto: { capa, dialogo, resolver, focoPrevio, cerrado }.
    var activo = null;

    function crearNodo(etiqueta, clase, texto) {
        var nodo = document.createElement(etiqueta);
        if (clase) {
            nodo.className = clase;
        }
        if (typeof texto === 'string') {
            nodo.textContent = texto;
        }
        return nodo;
    }

    function vaciar(nodo) {
        while (nodo && nodo.firstChild) {
            nodo.removeChild(nodo.firstChild);
        }
    }

    function crearBoton(texto, clase) {
        var boton = document.createElement('button');
        boton.type = 'button';
        boton.className = clase;
        boton.textContent = texto;
        return boton;
    }

    function cerrar(resultado) {
        if (!activo || activo.cerrado) {
            return;
        }

        var referencia = activo;
        referencia.cerrado = true;
        activo = null;

        if (referencia.capa.parentNode) {
            referencia.capa.parentNode.removeChild(referencia.capa);
        }

        document.body.classList.remove('modal-abierto');

        if (referencia.focoPrevio && typeof referencia.focoPrevio.focus === 'function') {
            referencia.focoPrevio.focus();
        }

        if (typeof referencia.resolver === 'function') {
            referencia.resolver(resultado === undefined ? null : resultado);
            referencia.resolver = null;
        }
    }

    function manejarTecla(evento) {
        if (!activo) {
            return;
        }

        if (evento.key === 'Escape') {
            evento.preventDefault();
            cerrar(null);
            return;
        }

        if (evento.key !== 'Tab') {
            return;
        }

        // Trampa de tabulación: el foco no sale del diálogo mientras está abierto.
        var focalizables = activo.dialogo.querySelectorAll(FOCALIZABLES);
        if (focalizables.length === 0) {
            return;
        }

        var primero = focalizables[0];
        var ultimo = focalizables[focalizables.length - 1];

        if (evento.shiftKey && document.activeElement === primero) {
            evento.preventDefault();
            ultimo.focus();
            return;
        }

        if (!evento.shiftKey && document.activeElement === ultimo) {
            evento.preventDefault();
            primero.focus();
        }
    }

    // Monta el diálogo y devuelve sus zonas para que cada caso de uso las complete.
    function montar(opciones) {
        opciones = opciones || {};
        cerrar(null);

        var capa = crearNodo('div', 'modal', null);
        capa.setAttribute('data-modal', '');

        var fondo = crearNodo('div', 'modal__fondo', null);
        var dialogo = crearNodo('div', 'modal__dialogo', null);
        dialogo.setAttribute('role', 'dialog');
        dialogo.setAttribute('aria-modal', 'true');

        var encabezado = crearNodo('div', 'modal__encabezado', null);
        var titulo = crearNodo('h2', 'modal__titulo', opciones.titulo || '');
        titulo.id = ID_TITULO;
        dialogo.setAttribute('aria-labelledby', ID_TITULO);
        encabezado.appendChild(titulo);

        if (typeof opciones.subtitulo === 'string' && opciones.subtitulo !== '') {
            encabezado.appendChild(crearNodo('p', 'modal__subtitulo', opciones.subtitulo));
        }

        var botonCerrar = crearNodo('button', 'modal__cerrar', '×');
        botonCerrar.type = 'button';
        botonCerrar.setAttribute('aria-label', 'Cerrar');
        encabezado.appendChild(botonCerrar);

        var cuerpo = crearNodo('div', 'modal__cuerpo', null);
        var pie = crearNodo('div', 'modal__pie', null);

        dialogo.appendChild(encabezado);
        dialogo.appendChild(cuerpo);
        dialogo.appendChild(pie);

        capa.appendChild(fondo);
        capa.appendChild(dialogo);
        document.body.appendChild(capa);
        document.body.classList.add('modal-abierto');

        var referencia = {
            capa: capa,
            dialogo: dialogo,
            focoPrevio: document.activeElement,
            cerrado: false,
            resolver: null
        };
        activo = referencia;

        botonCerrar.addEventListener('click', function () {
            cerrar(null);
        });
        fondo.addEventListener('click', function () {
            cerrar(null);
        });
        capa.addEventListener('keydown', manejarTecla);

        return { dialogo: dialogo, cuerpo: cuerpo, pie: pie, referencia: referencia };
    }

    function esArreglo(valor) {
        return Object.prototype.toString.call(valor) === '[object Array]';
    }

    function mostrarError(nodo, texto) {
        nodo.textContent = texto;
        nodo.hidden = false;
    }

    // La promesa del diálogo se resuelve cuando este se cierra (resultado o null).
    function crearPromesa(referencia) {
        return new Promise(function (resolver) {
            referencia.resolver = resolver;
        });
    }

    function enfocarInicial(montado, controles) {
        if (esArreglo(controles) && controles.length > 0) {
            controles[0].control.focus();
            if (typeof controles[0].control.select === 'function') {
                controles[0].control.select();
            }
            return;
        }

        var boton = montado.pie.querySelector('button') || montado.dialogo.querySelector('button');
        if (boton) {
            boton.focus();
        }
    }

    // Formulario modal. Resuelve con los valores confirmados o con null si se cancela.
    function formulario(opciones) {
        opciones = opciones || {};
        var campos = esArreglo(opciones.campos) ? opciones.campos : [];

        var montado = montar({ titulo: opciones.titulo, subtitulo: opciones.subtitulo });
        var promesa = crearPromesa(montado.referencia);
        var controles = [];

        var mensaje = crearNodo('p', 'modal__error', null);
        mensaje.setAttribute('role', 'alert');
        mensaje.hidden = true;

        for (var i = 0; i < campos.length; i++) {
            var definicion = campos[i];
            var id = 'modal-campo-' + String(definicion.nombre);
            var grupo = crearNodo('div', 'modal__campo', null);

            var etiqueta = crearNodo('label', 'modal__etiqueta',
                definicion.etiqueta || definicion.nombre);
            etiqueta.setAttribute('for', id);
            grupo.appendChild(etiqueta);

            var control = definicion.tipo === 'textarea'
                ? document.createElement('textarea')
                : document.createElement('input');
            if (definicion.tipo === 'textarea') {
                control.rows = 3;
            } else {
                control.type = 'text';
            }
            control.id = id;
            control.className = 'modal__control';
            control.value = typeof definicion.valor === 'string' ? definicion.valor : '';
            if (definicion.requerido === true) {
                control.required = true;
            }
            if (typeof definicion.longitudMaxima === 'number' && definicion.longitudMaxima > 0) {
                control.setAttribute('maxlength', String(definicion.longitudMaxima));
            }
            grupo.appendChild(control);

            if (typeof definicion.ayuda === 'string' && definicion.ayuda !== '') {
                grupo.appendChild(crearNodo('p', 'modal__ayuda', definicion.ayuda));
            }

            controles.push({ definicion: definicion, control: control });
            montado.cuerpo.appendChild(grupo);
        }

        montado.cuerpo.appendChild(mensaje);

        function errorLocal(texto, control) {
            mostrarError(mensaje, texto);
            if (control) {
                control.focus();
            }
        }

        var cancelar = crearNodo('button', 'btn-secundario', opciones.textoCancelar || 'Cancelar');
        cancelar.type = 'button';
        cancelar.addEventListener('click', function () {
            cerrar(null);
        });

        var aceptar = crearNodo('button', 'btn-primario', opciones.textoConfirmar || 'Guardar');
        aceptar.type = 'button';
        aceptar.addEventListener('click', function () {
            var valores = {};

            for (var j = 0; j < controles.length; j++) {
                var entrada = controles[j];
                var valor = String(entrada.control.value === null ? '' : entrada.control.value).trim();
                var nombre = entrada.definicion.etiqueta || entrada.definicion.nombre;
                var maximo = entrada.definicion.longitudMaxima;

                if (entrada.definicion.requerido === true && valor === '') {
                    errorLocal('Complete el campo "' + nombre + '".', entrada.control);
                    return;
                }

                if (typeof maximo === 'number' && valor.length > maximo) {
                    errorLocal('El campo "' + nombre + '" no puede superar los '
                        + maximo + ' caracteres.', entrada.control);
                    return;
                }

                valores[entrada.definicion.nombre] = valor;
            }

            var propio = typeof opciones.validar === 'function' ? opciones.validar(valores) : null;
            if (typeof propio === 'string' && propio !== '') {
                errorLocal(propio, null);
                return;
            }

            if (typeof opciones.alConfirmar !== 'function') {
                cerrar(valores);
                return;
            }

            aceptar.disabled = true;
            aceptar.setAttribute('aria-busy', 'true');

            Promise.resolve(opciones.alConfirmar(valores)).then(function (resultado) {
                if (activo !== montado.referencia) {
                    // El diálogo ya se cerró: no se modifica su interfaz.
                    return;
                }

                aceptar.disabled = false;
                aceptar.removeAttribute('aria-busy');

                if (resultado === true) {
                    cerrar(valores);
                    return;
                }

                if (typeof resultado === 'string' && resultado !== '') {
                    errorLocal(resultado, null);
                }
            });
        });

        montado.pie.appendChild(cancelar);
        montado.pie.appendChild(aceptar);
        enfocarInicial(montado, controles);

        return promesa;
    }

    // Confirmación modal. Resuelve con true (aceptado) o false/null (cancelado).
    function confirmar(opciones) {
        opciones = opciones || {};

        var montado = montar({ titulo: opciones.titulo, subtitulo: opciones.subtitulo });
        var promesa = crearPromesa(montado.referencia);

        if (typeof opciones.mensaje === 'string' && opciones.mensaje !== '') {
            montado.cuerpo.appendChild(crearNodo('p', 'modal__mensaje', opciones.mensaje));
        }

        var cancelar = crearNodo('button', 'btn-secundario', opciones.textoCancelar || 'Cancelar');
        cancelar.type = 'button';
        cancelar.addEventListener('click', function () {
            cerrar(false);
        });

        var aceptar = crearNodo('button',
            opciones.tono === 'peligro' ? 'btn-primario modal__peligro' : 'btn-primario',
            opciones.textoConfirmar || 'Confirmar');
        aceptar.type = 'button';
        aceptar.addEventListener('click', function () {
            cerrar(true);
        });

        montado.pie.appendChild(cancelar);
        montado.pie.appendChild(aceptar);
        aceptar.focus();

        return promesa;
    }

    // Panel de solo lectura con pares etiqueta/valor. Solo informa: no resuelve nada.
    function detalle(opciones) {
        opciones = opciones || {};

        var montado = montar({ titulo: opciones.titulo, subtitulo: opciones.subtitulo });
        var filas = esArreglo(opciones.filas) ? opciones.filas : [];
        var lista = crearNodo('dl', 'modal__lista', null);

        for (var i = 0; i < filas.length; i++) {
            var fila = crearNodo('div', 'modal__fila', null);
            fila.appendChild(crearNodo('dt', 'modal__clave', String(filas[i].etiqueta)));

            var valor = filas[i].valor;
            fila.appendChild(crearNodo('dd', 'modal__valor',
                typeof valor === 'string' && valor !== '' ? valor : '—'));

            lista.appendChild(fila);
        }

        montado.cuerpo.appendChild(lista);

        var boton = crearNodo('button', 'btn-secundario', opciones.textoCerrar || 'Cerrar');
        boton.type = 'button';
        boton.addEventListener('click', function () {
            cerrar(null);
        });
        montado.pie.appendChild(boton);
        boton.focus();

        return montado.referencia;
    }

    ns.modal = {
        formulario: formulario,
        confirmar: confirmar,
        detalle: detalle,
        cerrar: function () {
            cerrar(null);
        },
        estaAbierto: function () {
            return activo !== null;
        }
    };
})(window.VisorSIG);
