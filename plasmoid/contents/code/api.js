.pragma library

var port = 43175
var baseUrl = "http://127.0.0.1:" + port

function request(method, path, body, callback) {
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
        } else {
            callback(null, xhr.status > 0 ? "HTTP " + xhr.status : "Daemon offline")
        }
    }
    xhr.send(body === null || body === undefined ? null : JSON.stringify(body))
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
