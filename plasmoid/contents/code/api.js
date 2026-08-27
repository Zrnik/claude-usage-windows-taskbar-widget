.pragma library

var firstPort = 43175
var lastPort = 43195
var port = 0
var baseUrl = ""
var discovering = false
var discoveryCallbacks = []

function request(method, path, body, callback) {
    ensureDaemon(function(error) {
        if (error) {
            callback(null, error)
            return
        }
        sendRequest(method, path, body, callback, true)
    })
}

function sendRequest(method, path, body, callback, canRediscover) {
    var xhr = new XMLHttpRequest()
    xhr.open(method, baseUrl + path)
    xhr.setRequestHeader("Content-Type", "application/json")
    xhr.onreadystatechange = function() {
        if (xhr.readyState !== XMLHttpRequest.DONE)
            return
        if (xhr.status >= 200 && xhr.status < 300) {
            try {
                callback(JSON.parse(xhr.responseText), null)
            } catch (e) {
                callback(null, "Invalid daemon response")
            }
        } else if (xhr.status === 0 && canRediscover) {
            // The daemon may have restarted on a different port after an update.
            port = 0
            baseUrl = ""
            ensureDaemon(function(error) {
                if (error)
                    callback(null, error)
                else
                    sendRequest(method, path, body, callback, false)
            })
        } else {
            var message = xhr.status > 0 ? "HTTP " + xhr.status : "Daemon offline"
            try {
                var errorBody = JSON.parse(xhr.responseText)
                if (errorBody && errorBody.error)
                    message = errorBody.error
            } catch (e) {
            }
            callback(null, message)
        }
    }
    xhr.send(body === null || body === undefined ? null : JSON.stringify(body))
}

function ensureDaemon(callback) {
    if (baseUrl) {
        callback(null)
        return
    }

    discoveryCallbacks.push(callback)
    if (discovering)
        return

    discovering = true
    discoverPort(firstPort)
}

function discoverPort(candidatePort) {
    if (candidatePort > lastPort) {
        finishDiscovery("Daemon offline")
        return
    }

    var xhr = new XMLHttpRequest()
    var handled = false
    function tryNextPort() {
        if (handled)
            return
        handled = true
        discoverPort(candidatePort + 1)
    }
    xhr.timeout = 500
    xhr.open("GET", "http://127.0.0.1:" + candidatePort + "/health")
    xhr.onreadystatechange = function() {
        if (xhr.readyState !== XMLHttpRequest.DONE)
            return

        if (xhr.status >= 200 && xhr.status < 300) {
            try {
                var health = JSON.parse(xhr.responseText)
                if (health && health.ok === true && health.app === "ai-usage-widget") {
                    handled = true
                    port = candidatePort
                    baseUrl = "http://127.0.0.1:" + port
                    finishDiscovery(null)
                    return
                }
            } catch (e) {
            }
        }
        tryNextPort()
    }
    xhr.ontimeout = tryNextPort
    xhr.send()
}

function finishDiscovery(error) {
    discovering = false
    var callbacks = discoveryCallbacks
    discoveryCallbacks = []
    for (var i = 0; i < callbacks.length; i++)
        callbacks[i](error)
}

function loadState(callback) {
    request("GET", "/state", null, callback)
}

function saveSettings(settings, callback) {
    request("POST", "/settings", settings, callback)
}

function refresh(service, callback) {
    request("POST", "/refresh/" + service, null, callback)
}

function update(callback) {
    request("POST", "/update", null, callback)
}

function loadTogglProjects(callback) {
    request("GET", "/projects/toggl", null, callback)
}

function loadJiraProjects(callback) {
    request("GET", "/projects/jira", null, callback)
}

function loadJiraUsers(callback) {
    request("GET", "/users/jira", null, callback)
}
