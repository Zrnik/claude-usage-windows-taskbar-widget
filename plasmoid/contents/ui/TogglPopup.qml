import QtQuick
import "Format.js" as Format

Column {
    id: root
    property var service: null
    property var state: null
    property bool incognito: state && state.settings ? state.settings.incognitoMode : false
    width: 260
    spacing: 0

    Text { text: "TOGGL TRACK"; color: Style.muted; font.pixelSize: 9; height: 16 }

    ProgressBarLite {
        width: parent.width
        height: 14
        radius: 2
        value: usage() && usage().targetCzk > 0 ? Math.min(100, usage().earnedCzk / usage().targetCzk * 100) : 0
        fillColor: usage() && usage().targetCzk > 0 && usage().earnedCzk >= usage().targetCzk ? Style.blue : Style.green
        centerText: usage() ? Format.czk(usage().earnedCzk, incognito) + " / " + Format.czk(usage().targetCzk, incognito) + "  (" + Math.round(value) + "%)" : ""
    }

    Text { width: parent.width; text: planText(); color: "#D3D3D3"; font.pixelSize: 9; height: 14 }
    Text { width: parent.width; text: deltaText(); color: deltaColor(); font.pixelSize: 9; height: 14 }
    Text { width: parent.width; text: remainingText(); color: "#D3D3D3"; font.pixelSize: 9; height: 14 }
    Text {
        width: parent.width
        text: needText(false)
        color: needColor(false)
        font.pixelSize: 11
        font.bold: true
        height: text.length > 0 ? 17 : 0
        visible: text.length > 0
    }
    Text {
        width: parent.width
        text: needText(true)
        color: needColor(true)
        font.pixelSize: 10
        height: text.length > 0 ? 16 : 0
        visible: text.length > 0
    }

    Rectangle { width: parent.width; height: 1; color: Style.separator; visible: projects().length > 0 }
    Item { width: 1; height: 4; visible: projects().length > 0 }
    Text { text: "PROJECTS"; color: Style.muted; font.pixelSize: 9; height: 14; visible: projects().length > 0 }
    Repeater {
        model: projects()
        delegate: Row {
            width: root.width
            height: 14
            Text {
                width: 170
                text: modelData.clientName ? modelData.clientName + " / " + modelData.projectName : modelData.projectName
                color: Style.subtext
                font.pixelSize: 9
                elide: Text.ElideRight
            }
            Text {
                width: root.width - 170
                text: modelData.rateCzk > 0
                    ? Number(modelData.hours).toFixed(1).replace(".0", "") + "h × " + Format.rate(modelData.rateCzk, root.incognito) + " = " + Format.czk(modelData.earned, root.incognito)
                    : Number(modelData.hours).toFixed(1).replace(".0", "") + "h (no rate)"
                color: modelData.rateCzk > 0 ? Style.subtext : Style.dim
                font.pixelSize: 9
                horizontalAlignment: Text.AlignRight
                elide: Text.ElideRight
            }
        }
    }

    Rectangle { width: parent.width; height: 1; color: Style.separator; }
    Item { width: 1; height: 4 }
    Text { text: "MONTH PROGRESS"; color: Style.muted; font.pixelSize: 9; height: 14 }
    TogglMonthChart { width: parent.width; data: usage() }

    Rectangle { width: parent.width; height: 1; color: Style.separator; visible: service && service.lastError }
    Text {
        width: parent.width
        text: service && service.lastError ? service.lastError : ""
        color: Style.red
        font.pixelSize: 9
        wrapMode: Text.Wrap
        visible: text.length > 0
    }

    Rectangle { width: parent.width; height: 1; color: Style.separator; }
    Item { width: 1; height: 4 }
    Row {
        width: parent.width
        Text { width: parent.width - 50; text: "Toggl Track"; color: Style.dim; font.pixelSize: 8 }
        Text { width: 50; text: "v" + (state ? state.version : ""); color: Style.version; font.pixelSize: 8; horizontalAlignment: Text.AlignRight }
    }

    function usage() { return service ? service.usage : null }
    function projects() { return usage() && usage().breakdown ? usage().breakdown : [] }
    function totalWorkdays() {
        if (!usage()) return 0
        var start = new Date(usage().monthStart)
        var end = new Date(usage().monthResetsAt)
        var count = 0
        for (var d = new Date(start); d < end; d.setDate(d.getDate() + 1))
            if (d.getDay() !== 0 && d.getDay() !== 6) count++
        return count
    }
    function elapsedWorkdays() {
        if (!usage()) return 0
        var start = new Date(usage().monthStart)
        var now = new Date()
        var count = 0
        for (var d = new Date(start); d <= now; d.setDate(d.getDate() + 1))
            if (d.getDay() !== 0 && d.getDay() !== 6) count++
        return count
    }
    function remainingWorkdays() {
        if (!usage()) return 0
        var end = new Date(usage().monthResetsAt)
        var now = new Date()
        var count = 0
        for (var d = new Date(now.getFullYear(), now.getMonth(), now.getDate()); d < end; d.setDate(d.getDate() + 1))
            if (d.getDay() !== 0 && d.getDay() !== 6) count++
        return count
    }
    function expectedSoFar() {
        return usage() && totalWorkdays() > 0 ? usage().targetCzk / totalWorkdays() * elapsedWorkdays() : 0
    }
    function delta() { return usage() ? usage().earnedCzk - expectedSoFar() : 0 }
    function planText() { return usage() ? "Plan: " + Format.czk(usage().earnedCzk, incognito) + " / " + Format.czk(expectedSoFar(), incognito) + " expected" : "" }
    function deltaText() {
        if (!usage()) return ""
        var perDay = totalWorkdays() > 0 ? usage().targetCzk / totalWorkdays() : 0
        var days = perDay > 0 ? delta() / perDay : 0
        var sign = delta() >= 0 ? "+" : ""
        return "Delta: " + sign + Format.czk(delta(), incognito) + " (" + sign + days.toFixed(1).replace(".0", "") + "d)"
    }
    function deltaColor() {
        var perDay = totalWorkdays() > 0 && usage() ? usage().targetCzk / totalWorkdays() : 0
        var days = perDay > 0 ? delta() / perDay : 0
        if (delta() >= 0) return Style.green
        if (days > -1) return Style.orange
        return Style.red
    }
    function remainingText() {
        if (!usage()) return ""
        var rem = Math.max(0, usage().targetCzk - usage().earnedCzk)
        var days = remainingWorkdays()
        return "Remaining: " + days + " work days · " + Format.czk(days > 0 ? rem / days : 0, incognito) + "/day"
    }
    function requiredHours(includeWeekends) {
        if (!usage() || usage().targetCzk <= 0) return 0
        var rem = Math.max(0, usage().targetCzk - usage().earnedCzk)
        var implied = totalWorkdays() > 0 ? usage().targetCzk / (totalWorkdays() * 8) : 0
        var days = includeWeekends ? calendarDaysRemaining() : remainingWorkdays()
        return implied > 0 && days > 0 ? rem / (implied * days) : 0
    }
    function calendarDaysRemaining() {
        if (!usage()) return 0
        return Math.max(0, Math.ceil((new Date(usage().monthResetsAt) - Date.now()) / 86400000))
    }
    function needText(includeWeekends) {
        if (!usage()) return ""
        if (Math.max(0, usage().targetCzk - usage().earnedCzk) <= 0 && usage().targetCzk > 0)
            return includeWeekends ? "" : "✓ Target reached"
        var h = requiredHours(includeWeekends)
        if (h <= 0) return ""
        return includeWeekends
            ? "Need: " + h.toFixed(1).replace(".0", "") + " h/day (incl. weekends, " + calendarDaysRemaining() + "d)"
            : "Need: " + h.toFixed(1).replace(".0", "") + " h/day (Mon–Fri)"
    }
    function needColor(includeWeekends) {
        if (usage() && Math.max(0, usage().targetCzk - usage().earnedCzk) <= 0) return Style.blue
        var h = requiredHours(includeWeekends)
        if (h <= 8) return Style.green
        if (h <= 10) return Style.orange
        return Style.red
    }
}
