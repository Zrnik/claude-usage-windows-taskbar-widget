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
            Image { width: 20; height: 20; anchors.centerIn: parent; source: root.iconSource; smooth: true }
        }
        Item { width: 4; height: root.height }
        Column {
            width: Math.max(0, root.width - 28)
            height: root.height - 10
            anchors.verticalCenter: parent.verticalCenter
            spacing: 2

            ProgressBarLite {
                width: parent.width
                height: Math.max(10, (parent.height - 2) / 3)
                value: root.data && root.data.targetCzk > 0 ? Math.min(100, root.data.earnedCzk / root.data.targetCzk * 100) : 0
                fillColor: root.errorText && !root.data ? Style.maroon :
                    root.data && root.data.targetCzk > 0 && root.data.earnedCzk >= root.data.targetCzk ? Style.blue :
                    root.data && root.data.targetCzk > 0 ? Style.green : "#888888"
                centerText: root.errorText && !root.data ? "Error" : monthlyText()
            }
            ProgressBarLite {
                width: parent.width
                height: Math.max(10, (parent.height - 2) / 3)
                value: todayPct()
                fillColor: todayColor()
                centerText: root.errorText && !root.data ? "Error" : todayText()
            }
            Text {
                width: parent.width
                height: Math.max(9, (parent.height - 2) / 3)
                text: line2Text()
                color: Style.muted
                font.pixelSize: 9
                horizontalAlignment: Text.AlignHCenter
                verticalAlignment: Text.AlignVCenter
                elide: Text.ElideRight
                minimumPixelSize: 6
                fontSizeMode: Text.Fit
            }
        }
    }

    MouseArea {
        anchors.fill: parent
        hoverEnabled: true
        onEntered: root.hovered(root)
        onExited: root.unhovered()
        onDoubleClicked: root.forceRefresh()
    }

    function monthlyText() {
        if (!root.data)
            return ""
        var pct = root.data.targetCzk > 0 ? Math.min(100, root.data.earnedCzk / root.data.targetCzk * 100) : 0
        if (root.incognito)
            return root.data.targetCzk > 0 ? Math.round(pct) + "%" : "•••"
        return root.data.targetCzk > 0
            ? Math.round(pct) + "%  " + Format.shortCzk(root.data.earnedCzk, false) + " / " + Format.shortCzk(root.data.targetCzk, false)
            : Format.shortCzk(root.data.earnedCzk, false) + "  (no target)"
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
