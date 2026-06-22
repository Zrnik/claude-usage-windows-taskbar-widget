import QtQuick
import "Format.js" as Format

Item {
    id: root
    property string iconSource: "../images/toggl-logo.png"
    property real tileWidth: 170
    property var data: null
    property bool incognito: false
    property string errorText: ""
    signal hovered(var tile)
    signal unhovered()
    signal forceRefresh()

    width: tileWidth
    height: parent ? parent.height : 48

    Row {
        anchors.fill: parent
        Item {
            width: 24
            height: root.height
            Rectangle {
                width: 18
                height: 18
                anchors.centerIn: parent
                radius: 3
                color: "#E57CD8"
                visible: !togglIcon.status || togglIcon.status === Image.Error
                Text {
                    anchors.centerIn: parent
                    text: "T"
                    color: "white"
                    font.pixelSize: 11
                    font.bold: true
                }
            }
            Image {
                id: togglIcon
                width: 20
                height: 20
                anchors.centerIn: parent
                source: root.iconSource
                smooth: true
                fillMode: Image.PreserveAspectFit
            }
        }
        Item { width: 4; height: root.height }
        Column {
            id: barColumn
            width: Math.max(0, root.width - 28)
            height: Math.max(22, root.height - 8)
            anchors.verticalCenter: parent.verticalCenter
            spacing: root.height < 30 ? 1 : 2

            ProgressBarLite {
                width: parent.width
                height: Math.max(9, Math.floor((barColumn.height - barColumn.spacing) / 2))
                fontSize: Math.max(6, Math.min(8, Math.floor(height * 0.72)))
                value: monthlyPct()
                fillColor: !root.data && root.errorText ? Style.maroon :
                    root.data && targetCzk() > 0 && earnedCzk() >= targetCzk() ? Style.blue :
                    root.data && targetCzk() > 0 ? Style.green : Style.dim
                leftText: root.data ? Math.round(monthlyPct()) + "%" : ""
                rightText: root.data ? monthlyCompactText() : ""
                centerText: root.data ? "" : emptyText()
            }
            ProgressBarLite {
                width: parent.width
                height: Math.max(9, Math.floor((barColumn.height - barColumn.spacing) / 2))
                fontSize: Math.max(6, Math.min(8, Math.floor(height * 0.72)))
                value: todayPct()
                fillColor: todayColor()
                leftText: root.data ? todayHours().toFixed(1).replace(".0", "") + "h" : ""
                rightText: root.data ? dailyNeedText() : ""
                centerText: root.data ? "" : errorShortText()
            }
        }
    }

    MouseArea {
        anchors.fill: parent
        hoverEnabled: true
        acceptedButtons: Qt.LeftButton
        onEntered: root.hovered(root)
        onExited: root.unhovered()
        onDoubleClicked: root.forceRefresh()
    }

    function numeric(value) {
        var number = Number(value)
        return isFinite(number) ? number : 0
    }

    function earnedCzk() {
        return root.data ? numeric(root.data.earnedCzk) : 0
    }

    function targetCzk() {
        return root.data ? numeric(root.data.targetCzk) : 0
    }

    function monthlyPct() {
        return targetCzk() > 0 ? Math.min(100, earnedCzk() / targetCzk() * 100) : 0
    }

    function monthlyCompactText() {
        if (!root.data)
            return ""
        if (root.incognito)
            return targetCzk() > 0 ? "target" : "earned"
        return targetCzk() > 0
            ? Format.shortCzk(earnedCzk(), false) + "/" + Format.shortCzk(targetCzk(), false)
            : Format.shortCzk(earnedCzk(), false)
    }

    function emptyText() {
        return root.errorText ? "Toggl error" : "Toggl"
    }

    function errorShortText() {
        if (!root.errorText)
            return "No data"
        if (root.errorText.indexOf("rate limit") >= 0 || root.errorText.indexOf("Rate limit") >= 0)
            return "Rate limit"
        if (root.errorText.indexOf("key") >= 0 || root.errorText.indexOf("credentials") >= 0)
            return "Setup"
        return "Error"
    }

    function monthlyText() {
        if (!root.data)
            return ""
        var pct = monthlyPct()
        if (root.incognito)
            return targetCzk() > 0 ? Math.round(pct) + "%" : "..."
        return targetCzk() > 0
            ? Math.round(pct) + "%  " + Format.shortCzk(earnedCzk(), false) + " / " + Format.shortCzk(targetCzk(), false)
            : Format.shortCzk(earnedCzk(), false) + "  (no target)"
    }

    function todayHours() {
        if (!root.data || !root.data.dailyBreakdown)
            return 0
        var today = new Date().toDateString()
        for (var i = 0; i < root.data.dailyBreakdown.length; i++) {
            if (new Date(root.data.dailyBreakdown[i].date).toDateString() === today)
                return root.data.dailyBreakdown[i].hours
        }
        return 0
    }

    function requiredHoursPerDay() {
        if (!root.data || root.data.targetCzk <= 0)
            return 0
        var remaining = Math.max(0, root.data.targetCzk - root.data.earnedCzk)
        var monthStart = new Date(root.data.monthStart)
        var monthEnd = new Date(root.data.monthResetsAt)
        var workdays = 0
        for (var d = new Date(monthStart); d < monthEnd; d.setDate(d.getDate() + 1)) {
            if (d.getDay() !== 0 && d.getDay() !== 6)
                workdays++
        }
        var impliedRate = workdays > 0 ? root.data.targetCzk / (workdays * 8.0) : 0
        var rem = workdaysRemaining()
        return impliedRate > 0 && rem > 0 ? remaining / (impliedRate * rem) : 0
    }

    function dailyNeedText() {
        var target = requiredHoursPerDay()
        if (target <= 0)
            target = 8
        return "need " + target.toFixed(1).replace(".0", "") + "h"
    }

    function workdaysRemaining() {
        if (!root.data)
            return 0
        var monthEnd = new Date(root.data.monthResetsAt)
        var now = new Date()
        var count = 0
        for (var d = new Date(now.getFullYear(), now.getMonth(), now.getDate()); d < monthEnd; d.setDate(d.getDate() + 1)) {
            if (d.getDay() !== 0 && d.getDay() !== 6)
                count++
        }
        return count
    }

    function todayPct() {
        var target = requiredHoursPerDay()
        if (target <= 0)
            target = 8
        return Math.min(100, todayHours() / target * 100)
    }

    function todayColor() {
        var hours = todayHours()
        var target = requiredHoursPerDay()
        if (target <= 0)
            return "#888888"
        if (hours >= target)
            return Style.blue
        if (hours >= target * 0.5)
            return Style.green
        return Style.orange
    }

    function todayText() {
        if (!root.data)
            return ""
        var hours = todayHours()
        var remaining = Math.max(0, root.data.targetCzk - root.data.earnedCzk)
        var target = requiredHoursPerDay() > 0 ? requiredHoursPerDay() : 8
        if (root.data.targetCzk <= 0)
            return "Today: " + hours.toFixed(1).replace(".0", "") + "h  (no monthly target)"
        if (remaining <= 0)
            return "✓ Target reached · " + hours.toFixed(1).replace(".0", "") + "h today"
        if (workdaysRemaining() <= 0)
            return "Month over · " + hours.toFixed(1).replace(".0", "") + "h today"
        return "Today: " + hours.toFixed(1).replace(".0", "") + "h / " + target.toFixed(1).replace(".0", "") + "h"
    }

    function line2Text() {
        if (!root.data)
            return ""
        var remaining = Math.max(0, root.data.targetCzk - root.data.earnedCzk)
        var days = workdaysRemaining()
        if (days > 0 && remaining > 0 && root.data.targetCzk > 0)
            return root.incognito ? days + "d left" : days + "d left · " + Format.shortCzk(remaining / days, false) + "/day"
        return root.data.hoursWorked.toFixed(1).replace(".0", "") + "h worked this month"
    }
}
