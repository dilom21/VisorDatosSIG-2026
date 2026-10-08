// FE-AUTH 1: arranque del formulario de inicio de sesión.
// No envía credenciales a la Web: llama directamente a la API desde el navegador.
// Nunca imprime la contraseña ni el token.

window.VisorSIG = window.VisorSIG || {};

(function (ns) {
    'use strict';

    function normalizarReturnUrl(valor) {
        // Solo se aceptan rutas locales de la aplicación (evita open redirect).
        if (!valor) {
            return '/Dashboard';
        }
        var texto = String(valor).trim();
        if (texto.charAt(0) !== '/') {
            return '/Dashboard';
        }
        if (texto.indexOf('//') === 0) {
            return '/Dashboard';
        }
        if (texto.indexOf('/\\') === 0) {
            return '/Dashboard';
        }
        if (texto.indexOf('\\') !== -1) {
            return '/Dashboard';
        }
        if (texto.indexOf('://') !== -1) {
            return '/Dashboard';
        }
        return texto;
    }

    function mostrarError(mensaje, info, texto) {
        if (info) {
            info.textContent = '';
            info.hidden = true;
        }
        if (!mensaje) {
            return;
        }
        mensaje.textContent = texto;
        mensaje.classList.add('login-error');
        mensaje.hidden = false;
    }

    function mostrarMotivo(formulario, info) {
        if (!info) {
            return;
        }
        var motivo = formulario.getAttribute('data-motivo');
        var texto = null;
        if (motivo === 'expirada') {
            texto = 'Su sesión ha expirado. Inicie sesión nuevamente.';
        } else if (motivo === 'sesion-finalizada') {
            texto = 'Ha cerrado sesión correctamente.';
        }
        if (texto) {
            info.textContent = texto;
            info.hidden = false;
        }
    }

    function marcarCargando(boton, cargando, textoRestaurar) {
        if (!boton) {
            return;
        }
        if (cargando) {
            if (!boton.getAttribute('data-texto-original')) {
                boton.setAttribute('data-texto-original', boton.textContent);
            }
            boton.disabled = true;
            boton.textContent = 'Iniciando sesión...';
            boton.setAttribute('aria-busy', 'true');
            boton.classList.add('login-cargando');
        } else {
            boton.disabled = false;
            boton.textContent = textoRestaurar
                || boton.getAttribute('data-texto-original')
                || 'Iniciar sesión';
            boton.setAttribute('aria-busy', 'false');
            boton.classList.remove('login-cargando');
        }
    }

    function mensajeParaResultado(resultado) {
        if (resultado.error === 'red' || resultado.error === 'timeout') {
            return 'No fue posible comunicarse con el servidor.';
        }
        if (resultado.status === 400) {
            return 'Revise los datos ingresados.';
        }
        if (resultado.status === 401) {
            return 'Usuario o contraseña incorrectos.';
        }
        if (resultado.status === 403) {
            return 'Su cuenta está deshabilitada. Contacte al administrador.';
        }
        return 'No fue posible iniciar sesión. Intente nuevamente.';
    }

    function iniciar() {
        var formulario = document.getElementById('form-login');
        if (!formulario) {
            return;
        }

        var campoUsuario = document.getElementById('usuario');
        var campoPassword = document.getElementById('password');
        var botonMostrar = document.getElementById('btn-mostrar-password');
        var botonEnviar = document.getElementById('btn-iniciar-sesion');
        var mensaje = document.getElementById('login-mensaje');
        var info = document.getElementById('login-info');

        function activarManejoTeclado(campo) {
            if (!campo) { return; }
            campo.addEventListener('focus', function () {
                document.body.classList.add('login-teclado-activo');
                setTimeout(function () {
                    campo.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }, 220);
            });
            campo.addEventListener('blur', function () {
                setTimeout(function () {
                    var activo = document.activeElement;
                    if (!activo || (activo !== campoUsuario && activo !== campoPassword)) {
                        document.body.classList.remove('login-teclado-activo');
                    }
                }, 120);
            });
        }

        activarManejoTeclado(campoUsuario);
        activarManejoTeclado(campoPassword);

        mostrarMotivo(formulario, info);

        if (botonMostrar && campoPassword) {
            botonMostrar.addEventListener('click', function () {
                var visible = campoPassword.type === 'text';
                campoPassword.type = visible ? 'password' : 'text';
                botonMostrar.setAttribute('aria-pressed', visible ? 'false' : 'true');
                botonMostrar.textContent = visible ? 'Mostrar' : 'Ocultar';
            });
        }

        var enviando = false;

        formulario.addEventListener('submit', function (evento) {
            evento.preventDefault();

            if (enviando) {
                return;
            }

            var login = campoUsuario ? campoUsuario.value.trim() : '';
            var password = campoPassword ? campoPassword.value : '';

            // Validación local: sin petición HTTP cuando faltan datos.
            if (!login) {
                mostrarError(mensaje, info, 'Ingrese su usuario.');
                if (campoUsuario) {
                    campoUsuario.focus();
                }
                return;
            }
            if (!password) {
                mostrarError(mensaje, info, 'Ingrese su contraseña.');
                if (campoPassword) {
                    campoPassword.focus();
                }
                return;
            }

            enviando = true;
            var textoOriginal = botonEnviar ? botonEnviar.textContent : '';
            marcarCargando(botonEnviar, true);

            ns.api.iniciarSesion(login, password).then(function (resultado) {
                if (resultado.ok && resultado.status === 200
                    && resultado.datos && resultado.datos.accessToken
                    && resultado.datos.usuario) {
                    ns.sesion.guardar(resultado.datos);
                    // La contraseña no se conserva en memoria tras el login.
                    if (campoPassword) {
                        campoPassword.value = '';
                    }
                    var returnUrl = normalizarReturnUrl(
                        formulario.getAttribute('data-return-url'));
                    window.location.href = returnUrl;
                    return;
                }
                mostrarError(mensaje, info, mensajeParaResultado(resultado));
            }).catch(function () {
                // `peticion` nunca lanza, pero se cubre por seguridad.
                mostrarError(mensaje, info,
                    'No fue posible iniciar sesión. Intente nuevamente.');
            }).then(function () {
                // Equivalente a finally: restaurar el botón.
                enviando = false;
                marcarCargando(botonEnviar, false, textoOriginal);
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', iniciar);
    } else {
        iniciar();
    }
})(window.VisorSIG);
