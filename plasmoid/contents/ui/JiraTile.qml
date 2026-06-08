import QtQuick
import "Format.js" as Format

Item {
    id: root
    property string iconSource: "../images/jira-logo.png"
    property real tileWidth: 170
    property var data: null
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
                value: root.donePct()
                fillColor: root.errorText && !root.data ? Style.maroon : root.jiraColor()
                centerText: root.errorText && !root.data ? "Error" : root.barText()
            }

            Text {
                width: parent.width
                height: Math.max(9, (parent.height - 2) / 3)
                text: root.line1()
                color: Style.subtext
                font.pixelSize: 9
                horizontalAlignment: Text.AlignHCenter
                verticalAlignment: Text.AlignVCenter
                elide: Text.ElideRight
                minimumPixelSize: 6
                fontSizeMode: Text.Fit
            }

            Text {
                width: parent.width
                height: Math.max(9, (parent.height - 2) / 3)
                text: root.line2()
                color: root.line2Color()
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

    function myTotal() {
        if (!root.data || !root.data.myByCategory)
            return 0
        var sum = 0
        for (var key in root.data.myByCategory)
            sum += root.data.myByCategory[key]
        return sum
    }

    function myDone() {
        return root.data && root.data.myByCategory && root.data.myByCategory.done ? root.data.myByCategory.done : 0
    }

    function donePct() {
        var total = myTotal()
        return total > 0 ? Math.min(100, myDone() * 100 / total) : 0
    }

    function jiraColor() {
        var total = myTotal()
        var pct = donePct()
        if (total <= 0)
            return "#888888"
        if (pct >= 100)
            return Style.blue
        if (pct >= 50)
            return Style.green
        return Style.orange
    }

    function barText() {
        var total = myTotal()
        if (!root.data)
            return ""
        if (total <= 0)
            return "No assigned issues"
        return myDone() + "/" + total + " done · " +
            Number(root.data.myDoneStoryPoints || 0).toFixed(1).replace(".0", "") + "/" +
            Number(root.data.myStoryPoints || 0).toFixed(1).replace(".0", "") + " SP"
    }

    function line1() {
        if (!root.data)
            return ""
        var total = myTotal()
        if (total <= 0)
            return "Project: " + (root.data.projectKey || "")
        var todo = root.data.myByCategory.new || 0
        var doing = root.data.myByCategory.indeterminate || 0
        return todo + " todo · " + doing + " doing · " + myDone() + " done"
    }

    function line2() {
        if (!root.data)
            return ""
        if (root.data.myRank > 0 && root.data.developerRanking && root.data.developerRanking.length > 1)
            return "#" + root.data.myRank + " of " + root.data.developerRanking.length
        return root.data.me && root.data.me.displayName ? root.data.me.displayName : "—"
    }

    function line2Color() {
        if (!root.data || !root.data.developerRanking || root.data.myRank <= 0 || root.data.developerRanking.length <= 1)
            return Style.muted
        if (root.data.myRank === 1)
            return Style.gold
        if (root.data.myRank <= root.data.developerRanking.length / 2)
            return Style.green
        return Style.orange
    }
}
