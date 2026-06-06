.pragma library

function pad2(n) {
    return n < 10 ? "0" + n : "" + n
}

function resetTime(iso) {
    if (!iso)
        return ""
    var target = new Date(iso)
    var diff = target.getTime() - Date.now()
    if (diff <= 0)
        return "now"
    var minutes = Math.floor(diff / 60000)
    if (minutes < 60)
        return minutes + "m"
    var hours = Math.floor(minutes / 60)
    minutes = minutes % 60
    if (hours < 24)
        return hours + "h " + minutes + "m"
    var days = Math.floor(hours / 24)
    hours = hours % 24
    return days + "d " + hours + "h"
}

function localDateTime(iso) {
    if (!iso)
        return ""
    var d = new Date(iso)
    return d.getFullYear() + "-" + pad2(d.getMonth() + 1) + "-" + pad2(d.getDate()) +
        " " + pad2(d.getHours()) + ":" + pad2(d.getMinutes())
}

function labelSuffix(label) {
    if (!label)
        return ""
    var parts = label.split("-")
    return parts.length >= 2 ? parts[parts.length - 1].toUpperCase() : label.toUpperCase()
}

function czk(value, incognito) {
    if (incognito)
        return "••• Kč"
    if (!isFinite(value))
        return "— Kč"
    return Math.round(value).toLocaleString("cs-CZ") + " Kč"
}

function shortCzk(value, incognito) {
    if (incognito)
        return "••• Kč"
    if (value >= 1000000)
        return (value / 1000000).toFixed(1).replace(".0", "") + "M Kč"
    if (value >= 10000)
        return Math.round(value / 1000) + "k Kč"
    if (value >= 1000)
        return (value / 1000).toFixed(1).replace(".0", "") + "k Kč"
    return Math.round(value) + " Kč"
}

function rate(value, incognito) {
    return incognito ? "•••" : Math.round(value).toString()
}

function barColor(value) {
    if (value >= 100)
        return "#F44336"
    if (value >= 90)
        return "#9C27B0"
    if (value >= 75)
        return "#FF9800"
    return "#4CAF50"
}

function historyWindowLabel(label, overrides) {
    var hours = historyWindowHours(label, overrides)
    return hours >= 24 ? (hours / 24).toFixed(1).replace(".0", "") + "d" : hours + "h"
}

function historyWindowHours(label, overrides) {
    if (overrides && overrides[label] > 0)
        return overrides[label]
    label = (label || "").toLowerCase()
    if (label.indexOf("5h") >= 0)
        return 48
    if (label.indexOf("review") >= 0)
        return 168
    return 336
}
