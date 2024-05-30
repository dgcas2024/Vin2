const app = {
    alert: function (message, duration, state) {
        Toastify({
            text: message,
            duration: duration,
            close: true,
            gravity: "top", // `top` or `bottom`
            position: "right", // `left`, `center` or `right`
            stopOnFocus: true,
            style: {
                background: state,
            },
            onClick: function () { }
        }).showToast();
    },
    error: function (message) {
        this.alert(message, -1, "#f4516c");
    },
    warn: function (message) {
        this.alert(message, 15000, "#ffb822");
    },
    success: function (message) {
        this.alert(message, 5000, "#34bfa3");
    },
    info: function (message) {
        this.alert(message, 5000, "#36a3f7");
    },
    connectHub: function (url, hanleMessage, reconnectTime, callback) {
        reconnectTime = reconnectTime ? reconnectTime : 300000;
        const hub = new signalR.HubConnectionBuilder().withUrl(url).build();
        if (hanleMessage && typeof (hanleMessage) == 'function') {
            hanleMessage(hub);
        }
        var tryStart = null;
        var manualStop = false;
        const start = () => {
            manualStop = false;
            if (hub.state == 'Disconnected') {
                hub.start().then(() => {
                    console.log(new Date().toLocaleString(), 'Hub "' + url + '" connected.');
                    if (callback && typeof (callback) == 'function') {
                        callback(hub);
                    }
                }).catch(err => {
                    console.error(new Date().toLocaleString(), 'Hub "' + url + '" connect error.', err);
                    console.warn(new Date().toLocaleString(), 'Reconnect hub "' + url + '" after ' + reconnectTime + ' millisecond.');
                    tryStart = setTimeout(start, reconnectTime);
                });
            }
        };
        const stop = () => {
            manualStop = true;
            if (tryStart != null) {
                clearTimeout(tryStart);
                tryStart = null;
            }
            hub.stop().then(() => {
                console.info(new Date().toLocaleString(), 'Hub "' + url + '" disconnected.');
            }).catch(err => {
                console.error(new Date().toLocaleString(), 'Hub "' + url + '" disconnect error.', err);
            });
        }
        hub.onclose(() => {
            if (!manualStop) {
                console.warn(new Date().toLocaleString(), 'Hub "' + url + '" disconnected. Reconnect after ' + reconnectTime + ' millisecond.');
                tryStart = setTimeout(start, reconnectTime);
            }
        });
        return {
            hub: hub,
            start: start,
            stop: stop
        };
    },

    htmlEntities: function (rawStr) {
        rawStr = rawStr + '';
        return rawStr.length < 1000 ? rawStr : rawStr.replace(/[\u00A0-\u9999<>\&"']/gim, function (i) { return '&#' + i.charCodeAt(0) + ';'; });
    },

    textareaAutoSize: function (elements) {
        for (var i = 0; i < elements.length; i++) {
            const element = elements[i];
            element.setAttribute('style', 'height:' + (this.scrollHeight + 10) + 'px;overflow-y:hidden;');
            const autosize = function () {
                element.style.height = 'auto';
                element.style.height = (this.scrollHeight + 10) + 'px';
            }
            element.addEventListener('input', autosize);
            element.addEventListener('keyup', autosize);
            element.addEventListener('change', autosize);
            var event = new Event('change');
            element.dispatchEvent(event);
        }
    }
};
